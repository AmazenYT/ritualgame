using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Optional private-physics mode. It clones the selection and nearby static
        // colliders into a preview scene, then copies the simulated pose back.
        // Objects outside the configured radius are invisible to this simulation.
        private static bool TryBeginIsolatedPhysicsSession(Transform[] roots, RealPhysicsPlacerSettings activeSettings, Vector3 center, PhysicsModeProfile profile)
        {
            if (roots == null || roots.Length == 0 || activeSettings == null)
            {
                return false;
            }

            var protectMovableParts = activeSettings.preserveMovablePrefabParts
                && SelectionContainsMovablePrefabParts(roots);
            if (!activeSettings.useIsolatedPhysicsScene && !protectMovableParts)
            {
                return false;
            }

            CleanupIsolatedPhysicsObjects();

            try
            {
                if (!EnsureIsolatedPhysicsScene())
                {
                    CleanupIsolatedPhysicsObjects();
                    return false;
                }

                isolatedPhysicsRoot = new GameObject(IsolatedPhysicsRootName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                SceneManager.MoveGameObjectToScene(isolatedPhysicsRoot, isolatedPhysicsScene);

                BuildIsolatedStaticColliders(roots, activeSettings);

                foreach (var root in roots)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    var state = new PhysXBodyState(root, center, handleRotation);
                    if (!TryCreateIsolatedBody(root, activeSettings, out var cloneRoot, out var body))
                    {
                        SafeDestroyImmediate(cloneRoot);
                        continue;
                    }

                    state.UseSimulationClone(cloneRoot, body);
                    state.ConfigureForGrab(activeSettings, profile);
                    PhysXBodies.Add(state);
                }

                if (PhysXBodies.Count == 0)
                {
                    CleanupIsolatedPhysicsObjects();
                    return false;
                }

                if (ShouldIgnoreSelectedBodyCollisions(profile))
                {
                    IgnoreSelectedBodyCollisions();
                }

                isolatedPhysicsSessionActive = true;
                return true;
            }
            catch (Exception)
            {
                CleanupIsolatedPhysicsObjects();
                return false;
            }
        }

        private static bool SelectionContainsMovablePrefabParts(Transform[] roots)
        {
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                // A Rigidbody below the selected root owns an independently simulated
                // transform. Any Joint in the hierarchy can likewise move a child or
                // constrain the root. Simulating a collider-only clone keeps the entire
                // authored hierarchy untouched while RealTransforms places the root.
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    if (body != null && body.transform != root)
                    {
                        return true;
                    }
                }

                if (root.GetComponentInChildren<Joint>(true) != null
                    || root.GetComponentInChildren<ArticulationBody>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CleanupIsolatedPhysicsScene()
        {
            CleanupIsolatedPhysicsObjects();
        }

        private static void CleanupIsolatedPhysicsObjects()
        {
            isolatedPhysicsSessionActive = false;

            ClearIsolatedStaticColliders();
            SafeDestroyImmediate(isolatedPhysicsRoot);
            isolatedPhysicsRoot = null;
        }

        private static void CloseIsolatedPhysicsScene()
        {
            CleanupIsolatedPhysicsObjects();
            if (isolatedPhysicsScene.IsValid())
            {
                try
                {
                    EditorSceneManager.ClosePreviewScene(isolatedPhysicsScene);
                }
                catch (Exception)
                {
                    // Unity can invalidate editor-only scenes during domain reload. Best effort cleanup is enough here.
                }
            }

            isolatedPhysicsWorld = default;
            isolatedPhysicsScene = default;
        }

        private static bool EnsureIsolatedPhysicsScene()
        {
            if (isolatedPhysicsScene.IsValid() && isolatedPhysicsWorld.IsValid())
            {
                return true;
            }

            isolatedPhysicsScene = EditorSceneManager.NewPreviewScene();
            isolatedPhysicsWorld = isolatedPhysicsScene.GetPhysicsScene();
            return isolatedPhysicsScene.IsValid() && isolatedPhysicsWorld.IsValid();
        }

        private static void SimulateActivePhysicsScene(float step)
        {
            if (isolatedPhysicsSessionActive && isolatedPhysicsWorld.IsValid())
            {
                isolatedPhysicsWorld.Simulate(step);
                SyncRootsFromIsolatedBodies();
                return;
            }

            Physics.Simulate(step);
            Physics.SyncTransforms();
        }

        private static void SyncRootsFromIsolatedBodies()
        {
            foreach (var body in PhysXBodies)
            {
                if (body == null
                    || !body.UsesSimulationClone
                    || body.SimulationCloneRoot == null
                    || body.Root == null)
                {
                    continue;
                }

                var nextPosition = body.SimulationCloneRoot.transform.position;
                var nextRotation = body.SimulationCloneRoot.transform.rotation;
                if ((body.Root.position - nextPosition).sqrMagnitude <= MotionEpsilonSqr
                    && Mathf.Abs(Quaternion.Dot(body.Root.rotation, nextRotation)) >= 0.999999f)
                {
                    continue;
                }

                body.Root.SetPositionAndRotation(nextPosition, nextRotation);
            }
        }

        private static void BuildIsolatedStaticColliders(Transform[] roots, RealPhysicsPlacerSettings activeSettings)
        {
            if (isolatedPhysicsRoot == null
                || roots == null
                || roots.Length == 0
                || activeSettings == null)
            {
                return;
            }

            var query = activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            var ignoredTags = activeSettings.BuildIgnoredTagSet();
            var center = TryGetCombinedBounds(roots, out var bounds) ? bounds.center : AveragePosition(roots);
            // A larger mirror radius improves accuracy but increases setup cost.
            var radius = Mathf.Max(
                0.25f,
                activeSettings.isolatedPhysicsRadius,
                activeSettings.move.physicsAssistDistance * 8f,
                activeSettings.skinWidth * 16f);
            var halfExtents = ExpandExtents(bounds.size.sqrMagnitude > Epsilon ? bounds.extents : Vector3.one * 0.25f, radius);
            var candidates = Physics.OverlapBox(
                center,
                halfExtents,
                Quaternion.identity,
                activeSettings.collisionMask,
                query);

            if (candidates == null || candidates.Length == 0)
            {
                return;
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                var source = candidates[i];
                if (ShouldIgnoreCollider(source, roots, true, null, ignoredTags)
                    || IsTemporaryPhysicsComponent(source)
                    || source.attachedRigidbody != null && IsUnderAnyRoot(source.attachedRigidbody.transform, roots))
                {
                    continue;
                }

                var clone = new GameObject("Static_" + source.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = source.gameObject.layer,
                    isStatic = true
                };
                SceneManager.MoveGameObjectToScene(clone, isolatedPhysicsScene);
                CopyWorldTransform(source.transform, clone.transform);
                if (!TryCopyCollider(source, clone, false, activeSettings))
                {
                    SafeDestroyImmediate(clone);
                    continue;
                }

                clone.transform.SetParent(isolatedPhysicsRoot.transform, true);
                IsolatedStaticColliderObjects.Add(clone);
            }
        }

        private static void ClearIsolatedStaticColliders()
        {
            for (var i = IsolatedStaticColliderObjects.Count - 1; i >= 0; i--)
            {
                SafeDestroyImmediate(IsolatedStaticColliderObjects[i]);
            }

            IsolatedStaticColliderObjects.Clear();
        }

        private static bool TryCreateIsolatedBody(Transform sourceRoot, RealPhysicsPlacerSettings activeSettings, out GameObject cloneRoot, out Rigidbody body)
        {
            cloneRoot = null;
            body = null;
            if (sourceRoot == null || !isolatedPhysicsScene.IsValid())
            {
                return false;
            }

            cloneRoot = new GameObject("Body_" + sourceRoot.name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = sourceRoot.gameObject.layer
            };
            SceneManager.MoveGameObjectToScene(cloneRoot, isolatedPhysicsScene);
            CopyWorldTransform(sourceRoot, cloneRoot.transform);
            cloneRoot.transform.SetParent(isolatedPhysicsRoot.transform, true);

            var copiedColliders = 0;
            foreach (var sourceCollider in sourceRoot.GetComponentsInChildren<Collider>(false))
            {
                if (sourceCollider == null
                    || !sourceCollider.enabled
                    || (!activeSettings.includeTriggers && sourceCollider.isTrigger))
                {
                    continue;
                }

                var colliderObject = new GameObject("Collider_" + sourceCollider.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = sourceCollider.gameObject.layer
                };
                SceneManager.MoveGameObjectToScene(colliderObject, isolatedPhysicsScene);
                CopyWorldTransform(sourceCollider.transform, colliderObject.transform);
                colliderObject.transform.SetParent(cloneRoot.transform, true);
                if (!TryCopyCollider(sourceCollider, colliderObject, true, activeSettings))
                {
                    SafeDestroyImmediate(colliderObject);
                    continue;
                }

                copiedColliders++;
            }

            if (copiedColliders == 0)
            {
                copiedColliders += AddIsolatedMeshFallbackColliders(sourceRoot, cloneRoot, activeSettings);
            }

            if (copiedColliders == 0)
            {
                AddIsolatedBoundsFallbackCollider(sourceRoot, cloneRoot);
                copiedColliders = 1;
            }

            body = cloneRoot.AddComponent<Rigidbody>();
            body.hideFlags = HideFlags.HideAndDontSave;
            return copiedColliders > 0;
        }

        private static int AddIsolatedMeshFallbackColliders(Transform sourceRoot, GameObject cloneRoot, RealPhysicsPlacerSettings activeSettings)
        {
            var copied = 0;
            foreach (var meshFilter in sourceRoot.GetComponentsInChildren<MeshFilter>(false))
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                var colliderObject = new GameObject("Fallback_" + meshFilter.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = meshFilter.gameObject.layer
                };
                SceneManager.MoveGameObjectToScene(colliderObject, isolatedPhysicsScene);
                CopyWorldTransform(meshFilter.transform, colliderObject.transform);
                colliderObject.transform.SetParent(cloneRoot.transform, true);
                var meshCollider = colliderObject.AddComponent<MeshCollider>();
                meshCollider.hideFlags = HideFlags.HideAndDontSave;
                ConfigureMeshCollider(meshCollider, meshFilter.sharedMesh, true, activeSettings);
                copied++;
            }

            return copied;
        }

        private static void AddIsolatedBoundsFallbackCollider(Transform sourceRoot, GameObject cloneRoot)
        {
            var boxCollider = cloneRoot.AddComponent<BoxCollider>();
            boxCollider.hideFlags = HideFlags.HideAndDontSave;
            var bounds = TryGetWorldBounds(sourceRoot, out var worldBounds)
                ? worldBounds
                : new Bounds(sourceRoot.position, Vector3.one * 0.5f);
            boxCollider.center = cloneRoot.transform.InverseTransformPoint(bounds.center);
            boxCollider.size = WorldSizeToLocalColliderSize(cloneRoot.transform, bounds.size);
        }

        private static bool TryCopyCollider(Collider source, GameObject target, bool dynamicBody, RealPhysicsPlacerSettings activeSettings)
        {
            if (source == null || target == null)
            {
                return false;
            }

            Collider copied = null;
            switch (source)
            {
                case BoxCollider box:
                {
                    var collider = target.AddComponent<BoxCollider>();
                    collider.center = box.center;
                    collider.size = box.size;
                    copied = collider;
                    break;
                }
                case SphereCollider sphere:
                {
                    var collider = target.AddComponent<SphereCollider>();
                    collider.center = sphere.center;
                    collider.radius = sphere.radius;
                    copied = collider;
                    break;
                }
                case CapsuleCollider capsule:
                {
                    var collider = target.AddComponent<CapsuleCollider>();
                    collider.center = capsule.center;
                    collider.radius = capsule.radius;
                    collider.height = capsule.height;
                    collider.direction = capsule.direction;
                    copied = collider;
                    break;
                }
                case MeshCollider meshCollider when meshCollider.sharedMesh != null:
                {
                    var collider = target.AddComponent<MeshCollider>();
                    ConfigureMeshCollider(collider, meshCollider.sharedMesh, dynamicBody || meshCollider.convex, activeSettings);
                    copied = collider;
                    break;
                }
                case TerrainCollider terrainCollider when !dynamicBody && terrainCollider.terrainData != null:
                {
                    var collider = target.AddComponent<TerrainCollider>();
                    collider.terrainData = terrainCollider.terrainData;
                    copied = collider;
                    break;
                }
            }

            if (copied == null)
            {
                return false;
            }

            copied.hideFlags = HideFlags.HideAndDontSave;
            copied.isTrigger = source.isTrigger && activeSettings.includeTriggers;
            copied.sharedMaterial = source.sharedMaterial;
            return true;
        }

        private static void ConfigureMeshCollider(MeshCollider collider, Mesh mesh, bool convex, RealPhysicsPlacerSettings activeSettings)
        {
            collider.sharedMesh = mesh;
            collider.convex = convex;
            if (mesh != null && mesh.isReadable)
            {
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning
                    | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
            }
        }

        private static void CopyWorldTransform(Transform source, Transform target)
        {
            target.SetPositionAndRotation(source.position, source.rotation);
            target.localScale = source.lossyScale;
        }

        private static Vector3 WorldSizeToLocalColliderSize(Transform transform, Vector3 worldSize)
        {
            var scale = transform.lossyScale;
            return new Vector3(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

    }
}
