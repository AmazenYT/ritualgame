using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Selection proxy colliders are reused during editor drags to avoid
        // repeatedly allocating hidden MeshCollider objects for the same owner.
        private const string SelectionProxyColliderName = "__RealTransforms_ConcaveHull";
        private const string LegacySelectionProxyColliderName = "__RPP_FacetCollider";

        private static readonly Dictionary<EntityId, Queue<MeshCollider>> SelectionProxyColliderPool = new Dictionary<EntityId, Queue<MeshCollider>>();
        private static readonly HashSet<EntityId> LeasedSelectionProxyColliderIds = new HashSet<EntityId>();
        private static readonly HashSet<EntityId> RetainedSelectionProxyMeshIds = new HashSet<EntityId>();

        private static bool TryAcquireSelectionProxyCollider(GameObject owner, Mesh mesh, string colliderName, bool retainSharedMesh, out MeshCollider collider)
        {
            collider = null;
            if (owner == null || mesh == null)
            {
                return false;
            }

            var ownerId = owner.GetEntityId();
            if (!SelectionProxyColliderPool.TryGetValue(ownerId, out var pool))
            {
                pool = new Queue<MeshCollider>();
                SelectionProxyColliderPool[ownerId] = pool;
            }

            while (pool.Count > 0 && collider == null)
            {
                var candidate = pool.Dequeue();
                if (!TryGetColliderGameObject(candidate, out var candidateGameObject))
                {
                    continue;
                }

                if (candidate.transform.parent == owner.transform)
                {
                    collider = candidate;
                    continue;
                }

                SafeDestroyImmediate(candidateGameObject);
            }

            if (collider == null)
            {
                var proxy = new GameObject(SelectionProxyColliderName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                proxy.transform.SetParent(owner.transform, false);
                collider = proxy.AddComponent<MeshCollider>();
                collider.hideFlags = HideFlags.HideAndDontSave;
            }

            var transform = collider.transform;
            transform.SetParent(owner.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var colliderId = collider.GetEntityId();
            var meshChanged = collider.sharedMesh != mesh;
            collider.enabled = false;
            collider.gameObject.name = $"{SelectionProxyColliderName}_{colliderName}";
            if (meshChanged)
            {
                collider.sharedMesh = null;
            }

            if (mesh.isReadable)
            {
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning
                    | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
            }

            collider.convex = true;
            if (meshChanged)
            {
                collider.sharedMesh = mesh;
            }

            collider.enabled = true;
            LeasedSelectionProxyColliderIds.Add(colliderId);
            if (retainSharedMesh)
            {
                RetainedSelectionProxyMeshIds.Add(colliderId);
            }
            else
            {
                RetainedSelectionProxyMeshIds.Remove(colliderId);
            }

            return true;
        }

        private static void ReleaseSelectionProxyCollider(Collider collider)
        {
            if (collider is not MeshCollider meshCollider)
            {
                return;
            }

            var colliderId = meshCollider.GetEntityId();
            if (!LeasedSelectionProxyColliderIds.Remove(colliderId))
            {
                return;
            }

            if (!TryGetColliderGameObject(meshCollider, out var colliderGameObject))
            {
                RetainedSelectionProxyMeshIds.Remove(colliderId);
                return;
            }

            try
            {
                meshCollider.enabled = false;
                if (!RetainedSelectionProxyMeshIds.Contains(colliderId))
                {
                    meshCollider.sharedMesh = null;
                }

                colliderGameObject.name = SelectionProxyColliderName;
                colliderGameObject.hideFlags = HideFlags.HideAndDontSave;
                meshCollider.hideFlags = HideFlags.HideAndDontSave;

                var parent = meshCollider.transform.parent;
                if (parent == null)
                {
                    SafeDestroyImmediate(colliderGameObject);
                    return;
                }

                var ownerId = parent.gameObject.GetEntityId();
                if (!SelectionProxyColliderPool.TryGetValue(ownerId, out var pool))
                {
                    pool = new Queue<MeshCollider>();
                    SelectionProxyColliderPool[ownerId] = pool;
                }

                pool.Enqueue(meshCollider);
            }
            catch (MissingReferenceException)
            {
                SafeDestroyImmediate(colliderGameObject);
                return;
            }
            catch (NullReferenceException)
            {
                SafeDestroyImmediate(colliderGameObject);
                return;
            }
        }

        private static void ClearSelectionProxyColliderPool()
        {
            foreach (var pair in SelectionProxyColliderPool)
            {
                while (pair.Value.Count > 0)
                {
                    var collider = pair.Value.Dequeue();
                    if (TryGetColliderGameObject(collider, out var colliderGameObject))
                    {
                        SafeDestroyImmediate(colliderGameObject);
                    }
                }
            }

            SelectionProxyColliderPool.Clear();
            LeasedSelectionProxyColliderIds.Clear();
            RetainedSelectionProxyMeshIds.Clear();
        }

        private static bool HasSelectionProxyColliderState()
        {
            return SelectionProxyColliderPool.Count > 0
                || LeasedSelectionProxyColliderIds.Count > 0
                || RetainedSelectionProxyMeshIds.Count > 0;
        }

        private static void CleanupOrphanedSelectionProxyColliders()
        {
            var pooledColliderIds = new HashSet<EntityId>();
            var ownerIds = new List<EntityId>(SelectionProxyColliderPool.Keys);
            foreach (var ownerId in ownerIds)
            {
                if (!SelectionProxyColliderPool.TryGetValue(ownerId, out var pool))
                {
                    continue;
                }

                var retainedPool = new Queue<MeshCollider>();
                while (pool.Count > 0)
                {
                    var collider = pool.Dequeue();
                    if (TryGetColliderGameObject(collider, out var colliderGameObject)
                        && IsSelectionProxyColliderObject(colliderGameObject))
                    {
                        collider.enabled = false;
                        pooledColliderIds.Add(collider.GetEntityId());
                        retainedPool.Enqueue(collider);
                    }
                }

                SelectionProxyColliderPool[ownerId] = retainedPool;
            }

            foreach (var collider in Resources.FindObjectsOfTypeAll<MeshCollider>())
            {
                if (!TryGetColliderGameObject(collider, out var colliderGameObject)
                    || !IsSelectionProxyColliderObject(colliderGameObject))
                {
                    continue;
                }

                var colliderId = collider.GetEntityId();
                if (LeasedSelectionProxyColliderIds.Contains(colliderId)
                    || pooledColliderIds.Contains(colliderId))
                {
                    continue;
                }

                SafeDestroyImmediate(colliderGameObject);
            }
        }

        private static bool TryGetColliderGameObject(Collider collider, out GameObject gameObject)
        {
            gameObject = null;
            try
            {
                if (collider == null)
                {
                    return false;
                }

                gameObject = collider.gameObject;
                return gameObject != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        private static bool IsSelectionProxyColliderObject(GameObject gameObject)
        {
            return gameObject != null
                && (gameObject.name.StartsWith(SelectionProxyColliderName, StringComparison.Ordinal)
                    || gameObject.name.StartsWith(LegacySelectionProxyColliderName, StringComparison.Ordinal));
        }
    }
}
