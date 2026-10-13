using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealPhysicsPlacer;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    internal static class RealPhysicsPlacerColliderGenerator
    {
        // Creates persistent collider children/assets for selected render meshes.
        // Generated colliders are marked so they can be found, drawn, and removed
        // without touching user-authored colliders.
        internal const string GeneratedRootName = "__RealTransforms_BakedColliders";
        private const int UnityConvexTriangleLimit = 255;

        internal sealed class ColliderPreviewHull
        {
            internal Mesh Mesh;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Scale;
            internal string Name;
        }

        internal sealed class ColliderPreviewSource
        {
            internal Mesh Mesh;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Scale;
            internal string Name;
        }

        internal readonly struct ColliderBakeSelectionSummary
        {
            internal readonly int RootCount;
            internal readonly int MeshCount;
            internal readonly int VertexCount;
            internal readonly int TriangleCount;

            internal ColliderBakeSelectionSummary(int rootCount, int meshCount, int vertexCount, int triangleCount)
            {
                RootCount = rootCount;
                MeshCount = meshCount;
                VertexCount = vertexCount;
                TriangleCount = triangleCount;
            }
        }

        internal static int SelectedRootCount => GetSelectionRoots().Length;

        internal static int SelectedBakedHullCount
        {
            get
            {
                return GetSelectionRoots()
                    .SelectMany(root => root.GetComponentsInChildren<RealPhysicsPlacerBakedCollider>(true))
                    .Where(marker => marker != null)
                    .Sum(marker => Mathf.Max(marker.hullCount, marker.GetComponentsInChildren<Collider>(true).Length));
            }
        }

        internal static ColliderBakeSelectionSummary BuildBakeSelectionSummary(RealPhysicsPlacerSettings settings)
        {
            if (settings == null)
            {
                settings = RealPhysicsPlacerSettings.GetOrCreate();
            }

            RealPhysicsPlacerTimingLog.Enabled = settings.bakerLogTimings;

            var roots = GetSelectionRoots();
            var meshCount = 0;
            var vertexCount = 0;
            var triangleCount = 0;
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                var meshFilters = GetBakeMeshFilters(root, settings);
                for (var meshIndex = 0; meshIndex < meshFilters.Length; meshIndex++)
                {
                    var mesh = meshFilters[meshIndex]?.sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    meshCount++;
                    vertexCount += mesh.vertexCount;
                    triangleCount += GetTriangleCount(mesh);
                }
            }

            return new ColliderBakeSelectionSummary(roots.Length, meshCount, vertexCount, triangleCount);
        }

        internal static void BakeSelection(RealPhysicsPlacerSettings settings)
        {
            if (settings == null)
            {
                settings = RealPhysicsPlacerSettings.GetOrCreate();
            }

            var roots = GetSelectionRoots();
            if (roots.Length == 0)
            {
                return;
            }

            EnsureAssetFolder(settings);
            var totalHulls = 0;
            var bakeTimer = RealPhysicsPlacerTimingLog.Start();
            try
            {
                RealPhysicsPlacerColliderBakeBudget.Begin(settings);
                for (var i = 0; i < roots.Length; i++)
                {
                    if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                    {
                        Debug.LogWarning($"RealTransforms stopped collider baking after {settings.bakerBakeTimeBudgetSeconds} seconds. Increase Time Limit for a slower, more detailed solve.");
                        break;
                    }

                    var root = roots[i];
                    if (root == null)
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "RealTransforms Collider Baker",
                        $"Baking {root.name}",
                        roots.Length <= 1 ? 0.15f : i / (float)roots.Length))
                    {
                        break;
                    }

                    totalHulls += BakeRoot(root, settings);
                }
            }
            finally
            {
                RealPhysicsPlacerTimingLog.Total($"Bake Selection ({roots.Length} roots, {totalHulls} hulls)", bakeTimer);
                RealPhysicsPlacerColliderBakeBudget.End();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            if (totalHulls > 0)
            {
                SceneView.RepaintAll();
                RealPhysicsPlacerSceneTool.RefreshToolState();
            }
        }

        internal static void DeleteSelection()
        {
            var roots = GetSelectionRoots();
            if (roots.Length == 0)
            {
                return;
            }

            foreach (var root in roots)
            {
                DeleteGeneratedColliders(root);
            }

            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            RealPhysicsPlacerSceneTool.RefreshToolState();
        }

        internal static List<ColliderPreviewHull> BuildPreviewSelection(RealPhysicsPlacerSettings settings)
        {
            if (settings == null)
            {
                settings = RealPhysicsPlacerSettings.GetOrCreate();
            }

            RealPhysicsPlacerTimingLog.Enabled = settings.bakerLogTimings;

            var preview = new List<ColliderPreviewHull>();
            var sources = GetPreviewSources(settings);
            try
            {
                // Preview uses the same decomposition path as baking so the Scene
                // view accurately reflects the colliders that will be generated.
                RealPhysicsPlacerColliderBakeBudget.Begin(settings);
                for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                    {
                        Debug.LogWarning($"RealTransforms stopped collider preview after {settings.bakerBakeTimeBudgetSeconds} seconds. Increase Time Limit for a slower, more detailed preview.");
                        break;
                    }

                    var source = sources[sourceIndex];
                    if (source?.Mesh == null)
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "RealTransforms Collider Preview",
                        $"Previewing {source.Name}",
                        sources.Count <= 1 ? 0.45f : sourceIndex / (float)sources.Count))
                    {
                        break;
                    }

                    List<RealPhysicsPlacerColliderDecomposer.Piece> pieces;
                    try
                    {
                        pieces = RealPhysicsPlacerColliderDecomposer.Decompose(source.Mesh, settings, source.Mesh.name);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"RealTransforms could not preview colliders for {source.Name}: {exception.Message}");
                        continue;
                    }

                    AppendPreviewHulls(preview, source, pieces);
                }
            }
            finally
            {
                RealPhysicsPlacerColliderBakeBudget.End();
                EditorUtility.ClearProgressBar();
            }

            return preview;
        }

        internal static List<ColliderPreviewSource> GetPreviewSources(RealPhysicsPlacerSettings settings)
        {
            if (settings == null)
            {
                settings = RealPhysicsPlacerSettings.GetOrCreate();
            }

            var sources = new List<ColliderPreviewSource>();
            var roots = GetSelectionRoots();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                var meshFilters = root.GetComponentsInChildren<MeshFilter>(settings.bakerIncludeInactiveChildren)
                    .Where(meshFilter => meshFilter != null
                        && meshFilter.sharedMesh != null
                        && ShouldBakeMeshFilterForSelectedLods(root, meshFilter, settings)
                        && (!settings.bakerSkipPrimitiveMeshes || !IsBuiltInPrimitiveMesh(meshFilter.sharedMesh))
                        && meshFilter.GetComponentInParent<RealPhysicsPlacerBakedCollider>() == null)
                    .ToArray();

                for (var meshIndex = 0; meshIndex < meshFilters.Length; meshIndex++)
                {
                    var meshFilter = meshFilters[meshIndex];
                    sources.Add(new ColliderPreviewSource
                    {
                        Mesh = meshFilter.sharedMesh,
                        Position = meshFilter.transform.position,
                        Rotation = meshFilter.transform.rotation,
                        Scale = meshFilter.transform.lossyScale,
                        Name = meshFilter.name
                    });
                }
            }

            return sources;
        }

        internal static void AppendPreviewHulls(
            List<ColliderPreviewHull> preview,
            ColliderPreviewSource source,
            List<RealPhysicsPlacerColliderDecomposer.Piece> pieces)
        {
            if (preview == null || source == null || pieces == null)
            {
                return;
            }

            foreach (var piece in pieces)
            {
                if (piece?.Mesh == null)
                {
                    continue;
                }

                var previewMesh = piece.Shape == RealPhysicsPlacerBakedColliderShape.MultiHull
                    ? RealPhysicsPlacerColliderDecomposer.CreatePreviewDisplayMesh(piece.Mesh, $"{piece.Mesh.name}_Preview")
                    : null;
                if (previewMesh != null && previewMesh != piece.Mesh)
                {
                    Object.DestroyImmediate(piece.Mesh);
                }

                preview.Add(new ColliderPreviewHull
                {
                    Mesh = previewMesh != null ? previewMesh : piece.Mesh,
                    Position = source.Position,
                    Rotation = source.Rotation,
                    Scale = source.Scale,
                    Name = $"{source.Name} Preview Hull {preview.Count + 1:000}"
                });
            }
        }

        private static int BakeRoot(Transform root, RealPhysicsPlacerSettings settings)
        {
            if (root == null)
            {
                return 0;
            }

            var rootTimer = RealPhysicsPlacerTimingLog.Start();
            var rootTotalTimer = RealPhysicsPlacerTimingLog.Start();
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Bake RealTransforms Colliders");
            var meshFilters = GetBakeMeshFilters(root, settings);
            RealPhysicsPlacerTimingLog.Step($"Bake root '{root.name}' collect meshes ({meshFilters.Length})", rootTimer);
            if (settings.bakerReplaceExisting)
            {
                DeleteGeneratedColliders(root);
                foreach (var replacementRoot in GetExternalReplacementRoots(root, meshFilters, settings))
                {
                    DeleteGeneratedColliders(replacementRoot);
                }

                if (root == null)
                {
                    return 0;
                }
            }

            if (settings.bakerDisableExistingColliders)
            {
                DisableExistingColliders(root);
            }

            var totalHulls = 0;
            for (var i = 0; i < meshFilters.Length; i++)
            {
                if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    Debug.LogWarning($"RealTransforms stopped collider baking after {settings.bakerBakeTimeBudgetSeconds} seconds. Increase Time Limit for a slower, more detailed solve.");
                    break;
                }

                var meshFilter = meshFilters[i];
                if (meshFilter == null)
                {
                    continue;
                }

                if (EditorUtility.DisplayCancelableProgressBar(
                    "RealTransforms Collider Baker",
                    $"Baking {root.name} / {meshFilter.name}",
                    meshFilters.Length <= 1 ? 0.35f : i / (float)meshFilters.Length))
                {
                    break;
                }

                totalHulls += BakeMeshFilter(root, meshFilter, settings);
            }

            RealPhysicsPlacerTimingLog.Step($"Bake root '{root.name}' bake meshes", rootTimer);
            if (totalHulls > 0)
            {
                EditorUtility.SetDirty(root.gameObject);
            }

            RealPhysicsPlacerTimingLog.Total($"Bake root '{root.name}' ({totalHulls} hulls)", rootTotalTimer);
            return totalHulls;
        }

        private static int BakeMeshFilter(Transform selectionRoot, MeshFilter meshFilter, RealPhysicsPlacerSettings settings)
        {
            var sourceMesh = meshFilter.sharedMesh;
            if (sourceMesh == null)
            {
                return 0;
            }

            var meshTimer = RealPhysicsPlacerTimingLog.Start();
            List<RealPhysicsPlacerColliderDecomposer.Piece> pieces;
            try
            {
                pieces = RealPhysicsPlacerColliderDecomposer.Decompose(sourceMesh, settings, sourceMesh.name);
                RealPhysicsPlacerTimingLog.Step($"Bake mesh '{meshFilter.name}' decompose ({pieces?.Count ?? 0} pieces)", meshTimer);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"RealTransforms could not bake colliders for {meshFilter.name}: {exception.Message}", meshFilter);
                return 0;
            }

            if (pieces == null || pieces.Count == 0)
            {
                return 0;
            }

            return CreateBakedCollidersFromPieces(selectionRoot, meshFilter, settings, pieces, meshTimer);
        }

        private static int CreateBakedCollidersFromPieces(
            Transform selectionRoot,
            MeshFilter meshFilter,
            RealPhysicsPlacerSettings settings,
            List<RealPhysicsPlacerColliderDecomposer.Piece> pieces,
            System.Diagnostics.Stopwatch meshTimer = null)
        {
            var sourceMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (selectionRoot == null || meshFilter == null || sourceMesh == null || settings == null || pieces == null || pieces.Count == 0)
            {
                return 0;
            }

            var generatedParent = ResolveGeneratedColliderParent(meshFilter, settings);
            var groupName = generatedParent == meshFilter.transform
                ? GeneratedRootName
                : $"{GeneratedRootName}_{meshFilter.name}";
            var group = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(group, "Bake RealTransforms Colliders");
            SetGeneratedColliderRootTransform(group.transform, generatedParent, meshFilter.transform);
            group.layer = meshFilter.gameObject.layer;

            var marker = group.AddComponent<RealPhysicsPlacerBakedCollider>();
            marker.drawGizmos = settings.bakerDrawGizmos;
            marker.drawWhenUnselected = settings.bakerDrawWhenUnselected;
            marker.drawSolidGizmos = settings.bakerDrawSolidGizmos;
            marker.gizmoColor = settings.bakerGizmoColor;
            marker.shape = settings.bakerColliderShape;
            marker.maxHulls = settings.bakerMaxHulls;
            marker.maxHullVertices = settings.bakerMaxHullVertices;
            marker.realHullsQuality = settings.bakerRealHullsQuality;
            marker.realHullsVoxelResolution = settings.bakerRealHullsVoxelResolution;
            marker.realHullsSurfaceSamples = settings.bakerRealHullsSurfaceSamples;
            marker.realHullsMaxSplitCandidates = settings.bakerRealHullsMaxSplitCandidates;
            marker.realHullsShapeAccuracy = settings.bakerRealHullsShapeAccuracy;
            marker.realHullsMergePass = settings.bakerRealHullsMergePass;
            marker.mergeSmallHulls = settings.bakerMergeSmallHulls;
            marker.smallHullMergeTriangleLimit = settings.bakerSmallHullMergeTriangleLimit;
            marker.smallHullMergeExpansion = settings.bakerSmallHullMergeExpansion;
            marker.concavityThreshold = settings.bakerRealHullsShapeAccuracy;
            marker.splitComponents = false;
            marker.directHullTriangles = settings.bakerMaxHullVertices;
            marker.hullPadding = settings.bakerColliderPadding;
            marker.sourceMeshName = sourceMesh.name;
            marker.sourceMeshHash = RealPhysicsPlacerColliderDecomposer.CalculateSourceMeshHash(sourceMesh);
            if (meshTimer != null)
            {
                RealPhysicsPlacerTimingLog.Step($"Bake mesh '{meshFilter.name}' create marker", meshTimer);
            }

            var meshPieces = pieces.Where(piece => piece.Mesh != null).ToList();
            if (meshPieces.Count > 0)
            {
                var assetMeshes = SaveHullMeshAsset(selectionRoot, meshFilter, settings, meshPieces.Select(piece => piece.Mesh).ToList());
                marker.assetPath = assetMeshes.AssetPath;
                for (var i = 0; i < meshPieces.Count && i < assetMeshes.Meshes.Count; i++)
                {
                    meshPieces[i].Mesh = assetMeshes.Meshes[i];
                }
            }
            if (meshTimer != null)
            {
                RealPhysicsPlacerTimingLog.Step($"Bake mesh '{meshFilter.name}' save mesh assets ({meshPieces.Count})", meshTimer);
            }

            var createdCount = 0;
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                if (CreatePieceChild(group.transform, pieces[i], i, settings.bakerAddMeshFilters, meshFilter.gameObject.layer))
                {
                    createdCount++;
                }
            }

            marker.hullCount = createdCount;
            EditorUtility.SetDirty(marker);
            if (createdCount == 0)
            {
                Object.DestroyImmediate(group);
            }
            else if (meshPieces.Count > 0)
            {
                RealPhysicsPlacerBakedColliderLibrary.RegisterBake(sourceMesh, settings, marker.assetPath, meshPieces.Select(piece => piece.Mesh).ToList());
            }

            if (meshTimer != null)
            {
                RealPhysicsPlacerTimingLog.Total($"Bake mesh '{meshFilter.name}' create colliders ({createdCount})", meshTimer);
            }

            return createdCount;
        }

        private static MeshFilter[] GetBakeMeshFilters(Transform root, RealPhysicsPlacerSettings settings)
        {
            if (root == null || settings == null)
            {
                return Array.Empty<MeshFilter>();
            }

            return root.GetComponentsInChildren<MeshFilter>(settings.bakerIncludeInactiveChildren)
                .Where(meshFilter => meshFilter != null
                    && meshFilter.sharedMesh != null
                    && ShouldBakeMeshFilterForSelectedLods(root, meshFilter, settings)
                    && (!settings.bakerSkipPrimitiveMeshes || !IsBuiltInPrimitiveMesh(meshFilter.sharedMesh))
                    && meshFilter.GetComponentInParent<RealPhysicsPlacerBakedCollider>() == null)
                .ToArray();
        }

        private static int GetTriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            long triangles = 0;
            var subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (var i = 0; i < subMeshCount; i++)
            {
                triangles += (long)mesh.GetIndexCount(i) / 3L;
            }

            return triangles > int.MaxValue ? int.MaxValue : (int)triangles;
        }

        private static Transform[] GetExternalReplacementRoots(Transform root, MeshFilter[] meshFilters, RealPhysicsPlacerSettings settings)
        {
            if (root == null || meshFilters == null || meshFilters.Length == 0)
            {
                return Array.Empty<Transform>();
            }

            return meshFilters
                .Select(meshFilter => ResolveGeneratedColliderParent(meshFilter, settings))
                .Where(parent => parent != null
                    && parent != root
                    && !parent.IsChildOf(root))
                .Distinct()
                .ToArray();
        }

        private static Transform ResolveGeneratedColliderParent(MeshFilter meshFilter, RealPhysicsPlacerSettings settings)
        {
            if (meshFilter == null)
            {
                return null;
            }

            if (!HasSingleSelectedBakerLod(settings))
            {
                return meshFilter.transform;
            }

            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return meshFilter.transform;
            }

            var lodGroup = meshRenderer.GetComponentInParent<LODGroup>();
            return lodGroup != null && lodGroup.transform != null
                ? lodGroup.transform
                : meshFilter.transform;
        }

        private static void SetGeneratedColliderRootTransform(Transform generatedRoot, Transform parent, Transform source)
        {
            if (generatedRoot == null || source == null)
            {
                return;
            }

            generatedRoot.SetParent(parent != null ? parent : source, false);
            if (parent == null || parent == source)
            {
                generatedRoot.localPosition = Vector3.zero;
                generatedRoot.localRotation = Quaternion.identity;
                generatedRoot.localScale = Vector3.one;
                return;
            }

            if (source.parent == parent)
            {
                generatedRoot.localPosition = source.localPosition;
                generatedRoot.localRotation = source.localRotation;
                generatedRoot.localScale = source.localScale;
                return;
            }

            generatedRoot.SetPositionAndRotation(source.position, source.rotation);
            generatedRoot.localScale = DivideLossyScale(source.lossyScale, parent.lossyScale);
        }

        private static Vector3 DivideLossyScale(Vector3 scale, Vector3 parentScale)
        {
            return new Vector3(
                SafeDivideScale(scale.x, parentScale.x),
                SafeDivideScale(scale.y, parentScale.y),
                SafeDivideScale(scale.z, parentScale.z));
        }

        private static float SafeDivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= 0.0001f ? value : value / divisor;
        }

        private static bool CreatePieceChild(Transform parent, RealPhysicsPlacerColliderDecomposer.Piece piece, int index, bool addMeshFilter, int layer)
        {
            var hull = new GameObject($"Hull {index + 1:000}");
            Undo.RegisterCreatedObjectUndo(hull, "Bake RealTransforms Colliders");
            hull.transform.SetParent(parent, false);
            var primitive = piece.ColliderKind != RealPhysicsPlacerColliderDecomposer.PrimitiveColliderKind.Mesh;
            hull.transform.localPosition = primitive ? piece.Center : Vector3.zero;
            hull.transform.localRotation = primitive ? piece.Rotation : Quaternion.identity;
            hull.transform.localScale = Vector3.one;
            hull.layer = layer;

            if (piece.ColliderKind == RealPhysicsPlacerColliderDecomposer.PrimitiveColliderKind.Box)
            {
                var boxCollider = hull.AddComponent<BoxCollider>();
                boxCollider.center = Vector3.zero;
                boxCollider.size = piece.Size;
                return true;
            }

            var mesh = piece.Mesh;
            if (mesh == null)
            {
                Object.DestroyImmediate(hull);
                return false;
            }

            var triangleCount = mesh.triangles == null ? 0 : mesh.triangles.Length / 3;
            if (triangleCount > UnityConvexTriangleLimit)
            {
                Debug.LogWarning(
                    $"RealTransforms skipped {hull.name}: Unity convex MeshColliders are limited to {UnityConvexTriangleLimit} triangles, but this hull has {triangleCount}. Rebake with lower Hull Detail or more hulls.");
                Object.DestroyImmediate(hull);
                return false;
            }

            if (addMeshFilter)
            {
                var meshFilter = hull.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
            }

            var collider = hull.AddComponent<MeshCollider>();
            if (mesh != null && mesh.isReadable)
            {
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning
                    | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
            }

            collider.convex = true;
            collider.sharedMesh = mesh;
            collider.convex = true;
            if (!collider.convex)
            {
                Debug.LogWarning(
                    $"RealTransforms skipped {hull.name}: Unity could not cook this mesh as a convex MeshCollider. Rebake with lower Hull Detail or more hulls.");
                Object.DestroyImmediate(hull);
                return false;
            }

            EditorUtility.SetDirty(collider);
            return true;
        }

        private static SavedHullMeshes SaveHullMeshAsset(Transform selectionRoot, MeshFilter meshFilter, RealPhysicsPlacerSettings settings, List<Mesh> meshes)
        {
            var sceneName = selectionRoot.gameObject.scene.IsValid()
                ? SanitizeName(selectionRoot.gameObject.scene.name)
                : "Assets";
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = "Scene";
            }

            var folder = $"{GetAssetFolder(settings)}/{sceneName}_{SanitizeName(selectionRoot.name)}";
            EnsureFolder(folder);
            var fileName = $"{SanitizeName(meshFilter.name)}_RealTransforms_BakedHulls.asset";
            var path = $"{folder}/{fileName}";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                if (!AssetDatabase.DeleteAsset(path))
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                }
            }

            var savedMeshes = new List<Mesh>(meshes.Count);
            for (var i = 0; i < meshes.Count; i++)
            {
                var sourceMesh = meshes[i];
                if (sourceMesh == null)
                {
                    continue;
                }

                var meshName = $"{SanitizeName(meshFilter.sharedMesh.name)}_RealTransforms_BakedHull_{savedMeshes.Count + 1:000}";
                var mesh = CloneMeshForAsset(sourceMesh, meshName);
                if (savedMeshes.Count == 0)
                {
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, path);
                }

                savedMeshes.Add(mesh);
            }

            AssetDatabase.SaveAssets();
            var loadedMeshes = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Mesh>()
                .OrderBy(mesh => mesh.name, StringComparer.Ordinal)
                .ToList();
            return new SavedHullMeshes(path, loadedMeshes.Count > 0 ? loadedMeshes : savedMeshes);
        }

        private static Mesh CloneMeshForAsset(Mesh source, string meshName)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = meshName;
            mesh.hideFlags = HideFlags.None;
            return mesh;
        }

        private static void DeleteGeneratedColliders(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var markers = root.GetComponentsInChildren<RealPhysicsPlacerBakedCollider>(true)
                .Where(marker => marker != null)
                .ToArray();

            foreach (var marker in markers)
            {
                DeleteGeneratedMeshAssets(marker);
                Undo.DestroyObjectImmediate(marker.gameObject);
            }
        }

        private static void DeleteGeneratedMeshAssets(RealPhysicsPlacerBakedCollider marker)
        {
            if (!string.IsNullOrEmpty(marker.assetPath)
                && IsGeneratedHullAssetPath(marker.assetPath)
                && AssetDatabase.LoadAssetAtPath<Object>(marker.assetPath) != null)
            {
                AssetDatabase.DeleteAsset(marker.assetPath);
                return;
            }

            foreach (var meshCollider in marker.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider == null || meshCollider.sharedMesh == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(meshCollider.sharedMesh);
                if (!string.IsNullOrEmpty(path)
                    && IsGeneratedHullAssetPath(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void DisableExistingColliders(Transform root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(false))
            {
                if (collider == null || collider.GetComponentInParent<RealPhysicsPlacerBakedCollider>() != null)
                {
                    continue;
                }

                Undo.RecordObject(collider, "Disable Existing Colliders");
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
            }
        }

        private static Transform[] GetSelectionRoots()
        {
            var transforms = Selection.transforms
                .Select(NormalizeBakerSelection)
                .Where(transform => transform != null && transform.gameObject.scene.IsValid())
                .Distinct()
                .ToArray();

            return transforms
                .Where(transform => !transforms.Any(other => other != transform && transform.IsChildOf(other)))
                .OrderBy(transform => transform.GetEntityId())
                .ToArray();
        }

        private static Transform NormalizeBakerSelection(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            var bakedCollider = transform.GetComponentInParent<RealPhysicsPlacerBakedCollider>(true);
            if (bakedCollider != null && bakedCollider.transform.parent != null)
            {
                return bakedCollider.transform.parent;
            }

            return transform;
        }

        private static bool ShouldBakeMeshFilterForSelectedLods(Transform root, MeshFilter meshFilter, RealPhysicsPlacerSettings settings)
        {
            if (meshFilter == null || settings == null)
            {
                return false;
            }

            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return true;
            }

            var lodGroup = meshRenderer.GetComponentInParent<LODGroup>();
            if (lodGroup == null)
            {
                return true;
            }

            var lods = lodGroup.GetLODs();
            var lodMask = GetBakerLodMask(settings);
            var maxLod = Mathf.Min(lods.Length, RealPhysicsPlacerSettings.BakerLodCount);
            var lowestLod = IsLowestBakerLodMask(lodMask)
                ? FindLowestAvailableLodIndex(lods, maxLod)
                : -1;
            for (var i = 0; i < maxLod; i++)
            {
                var renderers = lods[i].renderers;
                if (renderers == null)
                {
                    continue;
                }

                for (var j = 0; j < renderers.Length; j++)
                {
                    if (renderers[j] == meshRenderer)
                    {
                        if (lowestLod >= 0)
                        {
                            return i == lowestLod;
                        }

                        return (lodMask & (1 << i)) != 0;
                    }
                }
            }

            return true;
        }

        private static bool HasSingleSelectedBakerLod(RealPhysicsPlacerSettings settings)
        {
            var lodMask = GetBakerLodMask(settings);
            return IsLowestBakerLodMask(lodMask) || CountBits(lodMask) == 1;
        }

        private static int GetBakerLodMask(RealPhysicsPlacerSettings settings)
        {
            return settings == null || settings.bakerLodMask <= 0
                ? RealPhysicsPlacerSettings.DefaultBakerLodMask
                : Mathf.Clamp(settings.bakerLodMask, 1, RealPhysicsPlacerSettings.AllBakerLodMask);
        }

        private static bool IsLowestBakerLodMask(int lodMask)
        {
            return lodMask == RealPhysicsPlacerSettings.LowestBakerLodMask;
        }

        private static int FindLowestAvailableLodIndex(LOD[] lods, int maxLod)
        {
            for (var i = maxLod - 1; i >= 0; i--)
            {
                var renderers = lods[i].renderers;
                if (renderers != null && renderers.Any(renderer => renderer != null))
                {
                    return i;
                }
            }

            return maxLod > 0 ? maxLod - 1 : -1;
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
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

        private static string GetAssetFolder(RealPhysicsPlacerSettings settings)
        {
            return settings == null
                ? RealPhysicsPlacerAssetPaths.DefaultBakedCollidersFolder
                : settings.GetColliderBakerOutputFolder();
        }

        private static bool IsGeneratedHullAssetPath(string assetPath)
        {
            assetPath = RealPhysicsPlacerAssetPaths.NormalizeAssetPath(assetPath);
            return assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                && assetPath.EndsWith("_RealTransforms_BakedHulls.asset", StringComparison.Ordinal);
        }

        private static void EnsureAssetFolder(RealPhysicsPlacerSettings settings)
        {
            RealPhysicsPlacerAssetPaths.EnsureFolder(GetAssetFolder(settings));
        }

        private static void EnsureFolder(string folder)
        {
            RealPhysicsPlacerAssetPaths.EnsureFolder(folder);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Object";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
            return new string(chars).Trim();
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

        private readonly struct SavedHullMeshes
        {
            internal readonly string AssetPath;
            internal readonly List<Mesh> Meshes;

            internal SavedHullMeshes(string assetPath, List<Mesh> meshes)
            {
                AssetPath = assetPath;
                Meshes = meshes;
            }
        }
    }
}
