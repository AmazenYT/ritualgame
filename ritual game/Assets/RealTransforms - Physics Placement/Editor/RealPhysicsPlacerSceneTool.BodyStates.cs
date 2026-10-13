using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        private static PhysicsMaterial constrainedMoveAssistMaterial;

        // Per-selection state wrapper. It snapshots Rigidbody, collider, and mesh
        // state before an editor simulation and restores everything afterward.
        private sealed class PhysXBodyState
        {
            private readonly List<MeshColliderState> meshColliderStates = new List<MeshColliderState>();
            private readonly List<ColliderMaterialState> colliderMaterialStates = new List<ColliderMaterialState>();
            private readonly List<Collider> temporaryColliders = new List<Collider>();
            private readonly List<Collider> leasedTemporaryColliders = new List<Collider>();
            private readonly List<Mesh> temporaryMeshes = new List<Mesh>();

            internal readonly Transform Root;
            internal readonly Vector3 TargetOffset;
            internal readonly Quaternion TargetRotationOffset;
            internal Rigidbody Body;
            internal bool CreatedBody;
            internal bool UsesSimulationClone;
            internal GameObject SimulationCloneRoot;

            private bool originalIsKinematic;
            private bool originalUseGravity;
            private bool originalDetectCollisions;
            private float originalMass;
            private float originalLinearDamping;
            private float originalAngularDamping;
            private float originalMaxAngularVelocity;
            private Vector3 originalLinearVelocity;
            private Vector3 originalAngularVelocity;
            private CollisionDetectionMode originalCollisionDetectionMode;
            private RigidbodyInterpolation originalInterpolation;
            private RigidbodyConstraints originalConstraints;
            private int originalSolverIterations;
            private int originalSolverVelocityIterations;

            internal PhysXBodyState(Transform root, Vector3 sessionCenter, Quaternion sessionRotation)
            {
                Root = root;
                TargetOffset = root.position - sessionCenter;
                TargetRotationOffset = Quaternion.Inverse(sessionRotation) * root.rotation;
            }

            internal void CaptureBodyState()
            {
                if (Body == null)
                {
                    return;
                }

                originalIsKinematic = Body.isKinematic;
                originalUseGravity = Body.useGravity;
                originalDetectCollisions = Body.detectCollisions;
                originalMass = Body.mass;
                originalLinearDamping = Body.linearDamping;
                originalAngularDamping = Body.angularDamping;
                originalMaxAngularVelocity = Body.maxAngularVelocity;
                originalLinearVelocity = Body.linearVelocity;
                originalAngularVelocity = Body.angularVelocity;
                originalCollisionDetectionMode = Body.collisionDetectionMode;
                originalInterpolation = Body.interpolation;
                originalConstraints = Body.constraints;
                originalSolverIterations = Body.solverIterations;
                originalSolverVelocityIterations = Body.solverVelocityIterations;
            }

            internal void CaptureMeshColliderState()
            {
                if (UsesSimulationClone)
                {
                    return;
                }

                meshColliderStates.Clear();
                foreach (var meshCollider in Root.GetComponentsInChildren<MeshCollider>(false))
                {
                    if (meshCollider == null || meshCollider.convex)
                    {
                        continue;
                    }

                    meshColliderStates.Add(new MeshColliderState(meshCollider, meshCollider.convex, meshCollider.enabled));
                    meshCollider.enabled = false;
                }
            }

            internal void EnsureSelectionCollider(RealPhysicsPlacerSettings activeSettings)
            {
                if (HasEnabledCollider())
                {
                    return;
                }

                AddFallbackSelectionCollider(activeSettings);
            }

            internal void ConfigureForGrab(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
            {
                if (Body == null)
                {
                    return;
                }

                Body.isKinematic = false;
                Body.useGravity = false;
                Body.detectCollisions = true;
                Body.mass = CreatedBody
                    ? Mathf.Max(0.1f, activeSettings.temporaryBodyMass)
                    : Mathf.Max(0.1f, Body.mass);
                Body.linearDamping = 0f;
                Body.angularDamping = 0.05f;
                var profileMaxAngularVelocity = profile == PhysicsModeProfile.Rotate || profile == PhysicsModeProfile.ArrangeRotate
                    ? Mathf.Max(1f, activeSettings.rotate.maxAngularSpeed * 1.5f)
                    : 50f;
                Body.maxAngularVelocity = Mathf.Max(1f, Mathf.Min(activeSettings.temporaryBodyMaxAngularVelocity, profileMaxAngularVelocity));
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;

                Body.collisionDetectionMode = activeSettings.temporaryBodyCollisionDetection;
                Body.interpolation = activeSettings.temporaryBodyInterpolation;
                Body.constraints = GetActiveRigidbodyConstraints(activeSettings, profile);
                if (ShouldUseConstrainedMoveAssistMaterial(activeSettings, profile))
                {
                    ApplyTemporaryColliderMaterial(GetConstrainedMoveAssistMaterial());
                }

                var solverIterations = Mathf.Max(1, GetActiveSolverIterations(activeSettings, profile));
                var solverVelocityIterations = Mathf.Max(1, GetActiveSolverVelocityIterations(activeSettings, profile));
                Body.solverIterations = activeSettings.preserveHigherExistingBodySolverIterations
                    ? Mathf.Max(solverIterations, Body.solverIterations)
                    : solverIterations;
                Body.solverVelocityIterations = activeSettings.preserveHigherExistingBodySolverIterations
                    ? Mathf.Max(solverVelocityIterations, Body.solverVelocityIterations)
                    : solverVelocityIterations;
                Body.WakeUp();
            }

            internal Collider[] GetColliders()
            {
                var colliderRoot = UsesSimulationClone && SimulationCloneRoot != null
                    ? SimulationCloneRoot.transform
                    : Root;
                return colliderRoot == null
                    ? Array.Empty<Collider>()
                    : colliderRoot.GetComponentsInChildren<Collider>(false);
            }

            internal void UseSimulationClone(GameObject cloneRoot, Rigidbody cloneBody)
            {
                SimulationCloneRoot = cloneRoot;
                Body = cloneBody;
                UsesSimulationClone = cloneRoot != null && cloneBody != null;
                CreatedBody = UsesSimulationClone;
            }

            internal void Restore()
            {
                if (UsesSimulationClone)
                {
                    RestoreColliderMaterials();
                    SafeDestroyImmediate(SimulationCloneRoot);
                    SimulationCloneRoot = null;
                    Body = null;
                    UsesSimulationClone = false;
                    return;
                }

                foreach (var meshColliderState in meshColliderStates)
                {
                    meshColliderState.Restore();
                }

                meshColliderStates.Clear();
                RestoreColliderMaterials();

                for (var i = temporaryColliders.Count - 1; i >= 0; i--)
                {
                    SafeDestroyImmediate(temporaryColliders[i]);
                }

                temporaryColliders.Clear();

                for (var i = leasedTemporaryColliders.Count - 1; i >= 0; i--)
                {
                    RealPhysicsPlacerSceneTool.ReleaseSelectionProxyCollider(leasedTemporaryColliders[i]);
                }

                leasedTemporaryColliders.Clear();

                for (var i = temporaryMeshes.Count - 1; i >= 0; i--)
                {
                    SafeDestroyImmediate(temporaryMeshes[i]);
                }

                temporaryMeshes.Clear();

                if (Body == null)
                {
                    return;
                }

                if (CreatedBody)
                {
                    SafeDestroyImmediate(Body);
                    Body = null;
                    return;
                }

                RestoreBodyStateTo(Body, false);
            }

            private void ApplyTemporaryColliderMaterial(PhysicsMaterial material)
            {
                if (material == null || colliderMaterialStates.Count > 0)
                {
                    return;
                }

                foreach (var collider in GetColliders())
                {
                    if (collider == null)
                    {
                        continue;
                    }

                    colliderMaterialStates.Add(new ColliderMaterialState(collider, collider.sharedMaterial));
                    collider.sharedMaterial = material;
                }
            }

            private void RestoreColliderMaterials()
            {
                for (var i = colliderMaterialStates.Count - 1; i >= 0; i--)
                {
                    colliderMaterialStates[i].Restore();
                }

                colliderMaterialStates.Clear();
            }

            internal void RestoreCopiedBodyState(Rigidbody copiedBody)
            {
                if (copiedBody == null)
                {
                    return;
                }

                if (CreatedBody)
                {
                    SafeDestroyImmediate(copiedBody);
                    return;
                }

                RestoreBodyStateTo(copiedBody, true);
                copiedBody.Sleep();
            }

            internal void RemoveCopiedTemporaryComponentsFromDuplicate(Transform duplicateRoot)
            {
                if (duplicateRoot == null)
                {
                    return;
                }

                foreach (var sourceCollider in temporaryColliders)
                {
                    RemoveCopiedTemporaryColliderFromDuplicate(sourceCollider, duplicateRoot);
                }

                foreach (var sourceCollider in leasedTemporaryColliders)
                {
                    RemoveCopiedTemporaryColliderFromDuplicate(sourceCollider, duplicateRoot);
                }

                if (CreatedBody)
                {
                    SafeDestroyImmediate(duplicateRoot.GetComponent<Rigidbody>());
                }
            }

            private void RestoreBodyStateTo(Rigidbody targetBody, bool clearVelocities)
            {
                if (targetBody == null)
                {
                    return;
                }

                targetBody.isKinematic = originalIsKinematic;
                targetBody.useGravity = originalUseGravity;
                targetBody.detectCollisions = originalDetectCollisions;
                targetBody.mass = originalMass;
                targetBody.linearDamping = originalLinearDamping;
                targetBody.angularDamping = originalAngularDamping;
                targetBody.maxAngularVelocity = originalMaxAngularVelocity;
                targetBody.linearVelocity = clearVelocities ? Vector3.zero : originalLinearVelocity;
                targetBody.angularVelocity = clearVelocities ? Vector3.zero : originalAngularVelocity;
                targetBody.collisionDetectionMode = originalCollisionDetectionMode;
                targetBody.interpolation = originalInterpolation;
                targetBody.constraints = originalConstraints;
                targetBody.solverIterations = originalSolverIterations;
                targetBody.solverVelocityIterations = originalSolverVelocityIterations;
            }

            private void RemoveCopiedTemporaryColliderFromDuplicate(Collider sourceCollider, Transform duplicateRoot)
            {
                if (sourceCollider == null
                    || Root == null
                    || duplicateRoot == null
                    || !TryFindDuplicateTransform(sourceCollider.transform, duplicateRoot, out var duplicateTransform))
                {
                    return;
                }

                // Match the concrete Collider type so cleanup only removes the
                // temporary copy that mirrors this selected source collider.
                foreach (var component in duplicateTransform.GetComponents(sourceCollider.GetType()))
                {
                    if (component is Collider candidate
                        && LooksLikeCopiedTemporaryCollider(sourceCollider, candidate))
                    {
                        SafeDestroyImmediate(candidate);
                    }
                }
            }

            private bool TryFindDuplicateTransform(Transform sourceTransform, Transform duplicateRoot, out Transform duplicateTransform)
            {
                duplicateTransform = null;
                if (sourceTransform == null || duplicateRoot == null)
                {
                    return false;
                }

                var path = new List<int>();
                var current = sourceTransform;
                while (current != null && current != Root)
                {
                    path.Add(current.GetSiblingIndex());
                    current = current.parent;
                }

                if (current != Root)
                {
                    return false;
                }

                duplicateTransform = duplicateRoot;
                for (var i = path.Count - 1; i >= 0; i--)
                {
                    var childIndex = path[i];
                    if (duplicateTransform == null || childIndex < 0 || childIndex >= duplicateTransform.childCount)
                    {
                        duplicateTransform = null;
                        return false;
                    }

                    duplicateTransform = duplicateTransform.GetChild(childIndex);
                }

                return duplicateTransform != null;
            }

            private static bool LooksLikeCopiedTemporaryCollider(Collider sourceCollider, Collider candidate)
            {
                if (sourceCollider == null || candidate == null || candidate == sourceCollider)
                {
                    return false;
                }

                if (IsTemporaryPhysicsComponent(candidate))
                {
                    return true;
                }

                // Type comparison is used here to distinguish Unity collider
                // subclasses; it does not access Unity Editor internal APIs.
                if (candidate.GetType() != sourceCollider.GetType()
                    || candidate.isTrigger != sourceCollider.isTrigger)
                {
                    return false;
                }

                switch (sourceCollider)
                {
                    case BoxCollider sourceBox when candidate is BoxCollider candidateBox:
                        return Approximately(sourceBox.center, candidateBox.center)
                            && Approximately(sourceBox.size, candidateBox.size);
                    case SphereCollider sourceSphere when candidate is SphereCollider candidateSphere:
                        return Approximately(sourceSphere.center, candidateSphere.center)
                            && Mathf.Abs(sourceSphere.radius - candidateSphere.radius) <= 0.0001f;
                    case CapsuleCollider sourceCapsule when candidate is CapsuleCollider candidateCapsule:
                        return Approximately(sourceCapsule.center, candidateCapsule.center)
                            && Mathf.Abs(sourceCapsule.radius - candidateCapsule.radius) <= 0.0001f
                            && Mathf.Abs(sourceCapsule.height - candidateCapsule.height) <= 0.0001f
                            && sourceCapsule.direction == candidateCapsule.direction;
                    case MeshCollider sourceMesh when candidate is MeshCollider candidateMesh:
                        return sourceMesh.sharedMesh == candidateMesh.sharedMesh
                            && sourceMesh.convex == candidateMesh.convex;
                    default:
                        return false;
                }
            }

            private static bool Approximately(Vector3 left, Vector3 right)
            {
                return (left - right).sqrMagnitude <= 0.00000001f;
            }

            private static Vector3 WorldSizeToLocalSize(Transform transform, Vector3 worldSize)
            {
                var scale = transform.lossyScale;
                return new Vector3(
                    worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            }

            private int TemporaryColliderCount => temporaryColliders.Count + leasedTemporaryColliders.Count;

            private void AddFallbackSelectionCollider(RealPhysicsPlacerSettings activeSettings)
            {
                if (TryAddAnyMeshProxy(activeSettings))
                {
                    return;
                }

                var boxCollider = Root.gameObject.AddComponent<BoxCollider>();
                boxCollider.hideFlags = HideFlags.HideAndDontSave;
                var bounds = TryGetWorldBounds(Root, out var worldBounds)
                    ? worldBounds
                    : new Bounds(Root.position, Vector3.one * 0.5f);
                boxCollider.center = Root.InverseTransformPoint(bounds.center);
                boxCollider.size = WorldSizeToLocalSize(Root, bounds.size);
                temporaryColliders.Add(boxCollider);
            }

            private void AddTemporaryMeshColliders(RealPhysicsPlacerSettings activeSettings)
            {
                foreach (var meshFilter in Root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    if (AddReusableBakedColliderPieces(meshFilter.gameObject, meshFilter.sharedMesh))
                    {
                        continue;
                    }

                    var pieceCount = IsBuiltInPrimitiveMesh(meshFilter.sharedMesh)
                        ? 1
                        : Mathf.Max(1, activeSettings.fallbackHullPieces);

                    AddConvexMeshProxyPieces(meshFilter.gameObject, meshFilter.sharedMesh, pieceCount, meshFilter.sharedMesh.name);
                }

                foreach (var skinnedMeshRenderer in Root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
                    {
                        continue;
                    }

                    var bakedMesh = new Mesh
                    {
                        name = $"{skinnedMeshRenderer.sharedMesh.name}_RealTransforms_BakedCollider",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    skinnedMeshRenderer.BakeMesh(bakedMesh);
                    temporaryMeshes.Add(bakedMesh);

                    AddConvexMeshProxyPieces(skinnedMeshRenderer.gameObject, bakedMesh, Mathf.Max(1, activeSettings.fallbackHullPieces), bakedMesh.name);
                }
            }

            private bool AddReusableBakedColliderPieces(GameObject owner, Mesh sourceMesh)
            {
                if (owner == null || sourceMesh == null)
                {
                    return false;
                }

                if (!RealPhysicsPlacerBakedColliderLibrary.TryGetHullMeshes(sourceMesh, out var reusableMeshes)
                    || reusableMeshes == null
                    || reusableMeshes.Count == 0)
                {
                    return false;
                }

                var createdCount = 0;
                for (var i = 0; i < reusableMeshes.Count; i++)
                {
                    var mesh = reusableMeshes[i];
                    if (mesh == null)
                    {
                        continue;
                    }

                    AddConvexMeshProxy(owner, mesh, $"{sourceMesh.name}_RealTransforms_BakedHull_{i + 1:000}", true);
                    createdCount++;
                }

                return createdCount > 0;
            }

            private bool TryAddAnyMeshProxy(RealPhysicsPlacerSettings activeSettings)
            {
                var beforeCount = TemporaryColliderCount;
                AddTemporaryMeshColliders(activeSettings);
                return TemporaryColliderCount > beforeCount;
            }

            private bool HasEnabledCollider()
            {
                return Root.GetComponentsInChildren<Collider>(false)
                    .Any(collider => collider != null && collider.enabled && !collider.isTrigger);
            }

            private bool AddSurfaceShellPieces(GameObject owner, Mesh sourceMesh, RealPhysicsPlacerSettings activeSettings, string sourceName)
            {
                if (owner == null || sourceMesh == null)
                {
                    return false;
                }

                IReadOnlyList<Mesh> pieces;
                try
                {
                    var localShell = WorldDistanceToLocalDistance(owner.transform, activeSettings.surfaceShellSkin);
                    var shellBudget = Mathf.Max(8, activeSettings.surfaceShellBudget);
                    if (!RealPhysicsPlacerSceneTool.TryGetCachedSurfaceShellMeshes(
                        sourceMesh,
                        shellBudget,
                        localShell,
                        out pieces))
                    {
                        QueueSurfaceShellPrewarm(new[] { owner.transform });
                        return false;
                    }
                }
                catch (UnityException)
                {
                    return false;
                }

                if (pieces == null || pieces.Count == 0)
                {
                    return false;
                }

                var createdCount = 0;
                foreach (var piece in pieces)
                {
                    if (piece == null)
                    {
                        continue;
                    }

                    AddConvexMeshProxy(owner, piece, piece.name, true);
                    createdCount++;
                }

                return createdCount > 0;
            }

            private void AddConvexMeshProxyPieces(GameObject owner, Mesh sourceMesh, int pieceCount, string sourceName)
            {
                if (owner == null || sourceMesh == null)
                {
                    return;
                }

                if (pieceCount <= 1)
                {
                    AddConvexMeshProxy(owner, sourceMesh, $"{sourceName}_RealTransforms_Hull", ShouldRetainSourceMeshCollider(sourceMesh));
                    return;
                }

                List<List<int>> partitions;
                try
                {
                    partitions = BuildTrianglePartitions(sourceMesh, pieceCount);
                }
                catch (UnityException)
                {
                    AddConvexMeshProxy(owner, sourceMesh, $"{sourceName}_RealTransforms_Hull", ShouldRetainSourceMeshCollider(sourceMesh));
                    return;
                }

                if (partitions.Count <= 1)
                {
                    AddConvexMeshProxy(owner, sourceMesh, $"{sourceName}_RealTransforms_Hull", ShouldRetainSourceMeshCollider(sourceMesh));
                    return;
                }

                var createdCount = 0;
                for (var i = 0; i < partitions.Count; i++)
                {
                    var piece = CreateConvexPieceMesh(sourceMesh, partitions[i], $"{sourceName}_RealTransforms_Hull_{i + 1:00}");
                    if (piece == null)
                    {
                        continue;
                    }

                    temporaryMeshes.Add(piece);
                    AddConvexMeshProxy(owner, piece, piece.name);
                    createdCount++;
                }

                if (createdCount == 0)
                {
                    AddConvexMeshProxy(owner, sourceMesh, $"{sourceName}_RealTransforms_Hull", ShouldRetainSourceMeshCollider(sourceMesh));
                }
            }

            private static bool ShouldRetainSourceMeshCollider(Mesh mesh)
            {
                return mesh != null && (mesh.hideFlags & HideFlags.HideAndDontSave) == 0;
            }

            private void AddConvexMeshProxy(GameObject owner, Mesh mesh, string colliderName, bool retainSharedMesh = false)
            {
                if (RealPhysicsPlacerSceneTool.TryAcquireSelectionProxyCollider(owner, mesh, colliderName, retainSharedMesh, out var pooledCollider))
                {
                    leasedTemporaryColliders.Add(pooledCollider);
                    return;
                }

                var meshCollider = owner.AddComponent<MeshCollider>();
                meshCollider.hideFlags = HideFlags.HideAndDontSave;
                meshCollider.name = colliderName;
                if (mesh != null && mesh.isReadable)
                {
                    meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                        | MeshColliderCookingOptions.EnableMeshCleaning
                        | MeshColliderCookingOptions.WeldColocatedVertices
                        | MeshColliderCookingOptions.UseFastMidphase;
                }

                meshCollider.sharedMesh = mesh;
                meshCollider.convex = true;
                temporaryColliders.Add(meshCollider);
            }

            private static List<List<int>> BuildTrianglePartitions(Mesh sourceMesh, int targetPieces)
            {
                var triangles = sourceMesh.triangles;
                if (triangles == null || triangles.Length < 24)
                {
                    return new List<List<int>>();
                }

                var vertices = sourceMesh.vertices;
                var allTriangles = new List<int>(triangles.Length / 3);
                targetPieces = Mathf.Max(1, targetPieces);
                if (targetPieces <= 1)
                {
                    for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        allTriangles.Add(triangle);
                    }

                    return new List<List<int>> { allTriangles };
                }

                var bounds = sourceMesh.bounds;
                if (bounds.size.sqrMagnitude <= Epsilon)
                {
                    for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        allTriangles.Add(triangle);
                    }

                    return new List<List<int>> { allTriangles };
                }

                var cells = CalculatePartitionGrid(bounds.size, targetPieces);
                var bins = new List<int>[cells.x * cells.y * cells.z];
                var min = bounds.min;
                var size = bounds.size;
                for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    var center = (vertices[triangles[triangle]] + vertices[triangles[triangle + 1]] + vertices[triangles[triangle + 2]]) / 3f;
                    var x = GridIndex(center.x, min.x, size.x, cells.x);
                    var y = GridIndex(center.y, min.y, size.y, cells.y);
                    var z = GridIndex(center.z, min.z, size.z, cells.z);
                    var index = x + cells.x * (y + cells.y * z);
                    bins[index] ??= new List<int>();
                    bins[index].Add(triangle);
                }

                return bins
                    .Where(bin => bin != null && bin.Count > 0)
                    .ToList();
            }

            private static Vector3Int CalculatePartitionGrid(Vector3 size, int targetPieces)
            {
                var cells = new Vector3Int(1, 1, 1);
                var safeSize = new Vector3(
                    Mathf.Max(0.0001f, size.x),
                    Mathf.Max(0.0001f, size.y),
                    Mathf.Max(0.0001f, size.z));

                while (cells.x * cells.y * cells.z < targetPieces)
                {
                    var axis = 0;
                    var xScore = safeSize.x / cells.x;
                    var yScore = safeSize.y / cells.y;
                    var zScore = safeSize.z / cells.z;
                    if (yScore > xScore && yScore >= zScore)
                    {
                        axis = 1;
                    }
                    else if (zScore > xScore && zScore > yScore)
                    {
                        axis = 2;
                    }

                    var next = cells;
                    next[axis]++;
                    if (next.x * next.y * next.z > targetPieces)
                    {
                        break;
                    }

                    cells = next;
                }

                return cells;
            }

            private static int GridIndex(float value, float min, float size, int cellCount)
            {
                if (cellCount <= 1 || size <= Epsilon)
                {
                    return 0;
                }

                var normalized = Mathf.Clamp01((value - min) / size);
                return Mathf.Clamp(Mathf.FloorToInt(normalized * cellCount), 0, cellCount - 1);
            }

            private static Mesh CreateConvexPieceMesh(Mesh sourceMesh, List<int> triangleStarts, string meshName)
            {
                const int maxTrianglesPerPiece = 240;
                if (sourceMesh == null || triangleStarts == null || triangleStarts.Count == 0)
                {
                    return null;
                }

                var sourceVertices = sourceMesh.vertices;
                var sourceTriangles = sourceMesh.triangles;
                var stride = Mathf.Max(1, Mathf.CeilToInt(triangleStarts.Count / (float)maxTrianglesPerPiece));
                var indexMap = new Dictionary<int, int>();
                var vertices = new List<Vector3>();
                var triangles = new List<int>();

                for (var i = 0; i < triangleStarts.Count; i += stride)
                {
                    var triangle = triangleStarts[i];
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var sourceIndex = sourceTriangles[triangle + corner];
                        if (!indexMap.TryGetValue(sourceIndex, out var remappedIndex))
                        {
                            remappedIndex = vertices.Count;
                            indexMap[sourceIndex] = remappedIndex;
                            vertices.Add(sourceVertices[sourceIndex]);
                        }

                        triangles.Add(remappedIndex);
                    }
                }

                if (vertices.Count < 4 || triangles.Count < 12)
                {
                    return null;
                }

                var mesh = new Mesh
                {
                    name = meshName,
                    hideFlags = HideFlags.HideAndDontSave
                };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            private static float WorldDistanceToLocalDistance(Transform transform, float worldDistance)
            {
                if (transform == null)
                {
                    return worldDistance;
                }

                var scale = transform.lossyScale;
                var averageScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
                return worldDistance / Mathf.Max(0.0001f, averageScale);
            }

            private static bool IsBuiltInPrimitiveMesh(Mesh mesh)
            {
                if (mesh == null)
                {
                    return false;
                }

                var meshName = mesh.name;
                return string.Equals(meshName, "Cube", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(meshName, "Sphere", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(meshName, "Capsule", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(meshName, "Cylinder", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(meshName, "Plane", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(meshName, "Quad", StringComparison.OrdinalIgnoreCase);
            }
        }

        private readonly struct MeshColliderState
        {
            internal readonly MeshCollider Collider;
            internal readonly bool Convex;
            internal readonly bool Enabled;

            internal MeshColliderState(MeshCollider collider, bool convex, bool enabled)
            {
                Collider = collider;
                Convex = convex;
                Enabled = enabled;
            }

            internal void Restore()
            {
                try
                {
                    if (Collider != null)
                    {
                        Collider.convex = Convex;
                        Collider.enabled = Enabled;
                    }
                }
                catch (MissingReferenceException)
                {
                }
                catch (NullReferenceException)
                {
                }
            }
        }

        private readonly struct ColliderMaterialState
        {
            private readonly Collider collider;
            private readonly PhysicsMaterial material;

            internal ColliderMaterialState(Collider collider, PhysicsMaterial material)
            {
                this.collider = collider;
                this.material = material;
            }

            internal void Restore()
            {
                try
                {
                    if (collider != null)
                    {
                        collider.sharedMaterial = material;
                    }
                }
                catch (MissingReferenceException)
                {
                }
                catch (NullReferenceException)
                {
                }
            }
        }

        private static bool ShouldUseConstrainedMoveAssistMaterial(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            return profile == PhysicsModeProfile.Move
                && GetActiveRotationConstraint(activeSettings, profile) != RealPhysicsPlacerRotationConstraint.None;
        }

        private static PhysicsMaterial GetConstrainedMoveAssistMaterial()
        {
            if (constrainedMoveAssistMaterial != null)
            {
                return constrainedMoveAssistMaterial;
            }

            constrainedMoveAssistMaterial = new PhysicsMaterial("RealTransforms Constrained Move Assist")
            {
                hideFlags = HideFlags.HideAndDontSave,
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            return constrainedMoveAssistMaterial;
        }

        private readonly struct IgnoredCollisionPair
        {
            internal readonly Collider Left;
            internal readonly Collider Right;

            internal IgnoredCollisionPair(Collider left, Collider right)
            {
                Left = left;
                Right = right;
            }
        }

        private readonly struct DuplicateIgnoredCollisionPair
        {
            internal readonly Collider Left;
            internal readonly Collider Right;
            internal readonly Transform DuplicateRoot;

            internal DuplicateIgnoredCollisionPair(Collider left, Collider right, Transform duplicateRoot)
            {
                Left = left;
                Right = right;
                DuplicateRoot = duplicateRoot;
            }

            internal bool IsValid => Left != null && Right != null && DuplicateRoot != null;

            internal bool Matches(Collider left, Collider right)
            {
                return (Left == left && Right == right)
                    || (Left == right && Right == left);
            }

            internal bool TracksRoot(Transform root)
            {
                return root != null && DuplicateRoot == root;
            }

            internal bool TracksCollider(Collider collider)
            {
                return collider != null && (Left == collider || Right == collider);
            }

            internal void Restore()
            {
                try
                {
                    if (Left != null && Right != null)
                    {
                        Physics.IgnoreCollision(Left, Right, false);
                    }
                }
                catch (MissingReferenceException)
                {
                }
                catch (NullReferenceException)
                {
                }
            }
        }

        private readonly struct FrozenRigidbodyState
        {
            private readonly Rigidbody body;
            private readonly bool isKinematic;
            private readonly bool useGravity;
            private readonly Vector3 linearVelocity;
            private readonly Vector3 angularVelocity;

            internal FrozenRigidbodyState(Rigidbody body)
            {
                this.body = body;
                isKinematic = body != null && body.isKinematic;
                useGravity = body != null && body.useGravity;
                linearVelocity = body != null ? body.linearVelocity : Vector3.zero;
                angularVelocity = body != null ? body.angularVelocity : Vector3.zero;
            }

            internal bool Tracks(Rigidbody candidate)
            {
                return body == candidate;
            }

            internal void Restore()
            {
                if (body == null)
                {
                    return;
                }

                body.isKinematic = isKinematic;
                body.useGravity = useGravity;
                if (isKinematic)
                {
                    return;
                }

                body.linearVelocity = linearVelocity;
                body.angularVelocity = angularVelocity;
                if (linearVelocity.sqrMagnitude > Epsilon || angularVelocity.sqrMagnitude > Epsilon)
                {
                    body.WakeUp();
                }
            }
        }

        private static void SafeDestroyImmediate(Object target)
        {
            try
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }
            catch (MissingReferenceException)
            {
            }
            catch (NullReferenceException)
            {
            }
        }

        private readonly struct AlignmentItem
        {
            internal readonly Transform Root;
            internal readonly Bounds Bounds;
            internal readonly float Min;
            internal readonly float Max;

            internal AlignmentItem(Transform root, Bounds bounds, float min, float max)
            {
                Root = root;
                Bounds = bounds;
                Min = min;
                Max = max;
            }
        }
    }
}
