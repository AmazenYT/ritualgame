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
        // Bounds are used as broad-phase data for handles, probes, and fallback
        // collision checks. Collider bounds are preferred; renderer bounds are an
        // optional fallback for objects that have no usable colliders yet.
        private static bool TryGetCombinedBounds(Transform[] roots, out Bounds combined)
        {
            combined = default;
            var hasBounds = false;
            foreach (var root in roots)
            {
                if (root == null || !TryGetWorldBounds(root, out var bounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetWorldBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            var hasBounds = false;
            BoundsColliderBuffer.Clear();
            root.GetComponentsInChildren(false, BoundsColliderBuffer);
            foreach (var collider in BoundsColliderBuffer)
            {
                if (collider == null || !collider.enabled || collider.gameObject == proxyRoot)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            BoundsColliderBuffer.Clear();
            if (hasBounds || !GetSettings().useRendererBoundsWhenNoCollider)
            {
                return hasBounds;
            }

            BoundsRendererBuffer.Clear();
            root.GetComponentsInChildren(false, BoundsRendererBuffer);
            foreach (var renderer in BoundsRendererBuffer)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            BoundsRendererBuffer.Clear();
            if (hasBounds)
            {
                return true;
            }

            bounds = new Bounds(root.position, Vector3.one * 0.25f);
            return true;
        }

        private static bool ShouldIgnoreCollider(
            Collider collider,
            Transform[] roots,
            bool ignoreAllSelected,
            Transform movingRoot,
            HashSet<string> ignoredTags,
            bool ignoreShadowedSceneColliders = true)
        {
            if (collider == null || !collider.enabled)
            {
                return true;
            }

            var activeSettings = GetSettings();
            if (!LayerIsIncluded(collider.gameObject.layer, activeSettings.collisionMask))
            {
                return true;
            }

            if (!activeSettings.includeTriggers && collider.isTrigger)
            {
                return true;
            }

            if (ignoredTags.Contains(collider.gameObject.tag))
            {
                return true;
            }

            if (ignoreShadowedSceneColliders
                && activeSettings.useSceneProxyCollidersForAllObjects
                && IsSceneColliderShadowedByProxy(collider))
            {
                return true;
            }

            if (IsDuplicateCollisionSuppressedCollider(collider))
            {
                return true;
            }

            if (ignoreAllSelected && IsUnderAnyRoot(collider.transform, roots))
            {
                return true;
            }

            if (!ignoreAllSelected && movingRoot != null && collider.transform.IsChildOf(movingRoot))
            {
                return true;
            }

            return false;
        }

        private static bool IsSceneColliderShadowedByProxy(Collider collider)
        {
            return collider != null
                && !IsSceneProxyObject(collider.gameObject)
                && SceneCollidersShadowedByProxies.ContainsKey(collider.GetEntityId());
        }

        private static bool IsUnderAnyRoot(Transform transform, Transform[] roots)
        {
            return roots.Any(root => root != null && transform != null && transform.IsChildOf(root));
        }

        private static bool LayerIsIncluded(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private static Vector3 ShrinkExtents(Vector3 extents, float amount)
        {
            return new Vector3(
                Mathf.Max(0.001f, extents.x - amount),
                Mathf.Max(0.001f, extents.y - amount),
                Mathf.Max(0.001f, extents.z - amount));
        }

        private static Vector3 ExpandExtents(Vector3 extents, float amount)
        {
            return new Vector3(
                Mathf.Max(0.001f, extents.x + amount),
                Mathf.Max(0.001f, extents.y + amount),
                Mathf.Max(0.001f, extents.z + amount));
        }

        private static bool TryGetAverageRootPosition(Transform[] roots, out Vector3 position)
        {
            position = Vector3.zero;
            if (roots == null || roots.Length == 0)
            {
                return false;
            }

            var count = 0;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                position += root.position;
                count++;
            }

            if (count == 0)
            {
                position = Vector3.zero;
                return false;
            }

            position /= count;
            return true;
        }

        private static Vector3 AveragePosition(Transform[] roots)
        {
            return TryGetAverageRootPosition(roots, out var position) ? position : Vector3.zero;
        }

        private static Vector3 GetPhysicsTruthPoint(Transform root)
        {
            if (root == null)
            {
                return Vector3.zero;
            }

            return GetSettings().useCenterAsPhysicsTruth && TryGetWorldBounds(root, out var bounds)
                ? bounds.center
                : root.position;
        }

        private static Vector3 GetRootPositionForTruthTarget(Transform root, Vector3 targetTruthPoint)
        {
            return root != null
                ? root.position + targetTruthPoint - GetPhysicsTruthPoint(root)
                : targetTruthPoint;
        }

        private static float ProjectMin(Bounds bounds, Vector3 axis)
        {
            var radius = Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.y) * bounds.extents.y + Mathf.Abs(axis.z) * bounds.extents.z;
            return Vector3.Dot(bounds.center, axis) - radius;
        }

        private static float ProjectMax(Bounds bounds, Vector3 axis)
        {
            var radius = BoundsRadiusOnAxis(bounds, axis);
            return Vector3.Dot(bounds.center, axis) + radius;
        }

        private static float BoundsRadiusOnAxis(Bounds bounds, Vector3 axis)
        {
            return Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.y) * bounds.extents.y + Mathf.Abs(axis.z) * bounds.extents.z;
        }
    }
}
