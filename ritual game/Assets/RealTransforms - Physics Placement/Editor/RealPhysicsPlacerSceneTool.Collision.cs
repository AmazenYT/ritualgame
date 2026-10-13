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
        // Collision helpers are intentionally conservative. They favor stable
        // placement feedback over deep penetration checks on every frame.
        private static bool DidContactLimitMove(Vector3 requestedDelta, Vector3 actualDelta, RealPhysicsPlacerSettings activeSettings)
        {
            if (requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                return false;
            }

            var requestedDistance = requestedDelta.magnitude;
            var direction = requestedDelta / requestedDistance;
            var forwardProgress = Mathf.Clamp(Vector3.Dot(actualDelta, direction), 0f, requestedDistance);
            var lostForwardMotion = requestedDistance - forwardProgress;
            var contactSlop = Mathf.Max(
                0.001f,
                Mathf.Min(requestedDistance * 0.2f, activeSettings.skinWidth + activeSettings.move.physicsAssistDistance * 0.25f));

            if (lostForwardMotion > contactSlop)
            {
                return true;
            }

            var lateralMotion = actualDelta - direction * forwardProgress;
            return lateralMotion.magnitude > Mathf.Max(activeSettings.skinWidth * 2f, activeSettings.move.physicsAssistDistance);
        }

        private static bool PlacementAssistPathHasNearbyContact(Transform[] roots, Dictionary<Transform, Vector3> targetPositions, RealPhysicsPlacerSettings activeSettings)
        {
            if (roots.Length == 0 || targetPositions.Count == 0)
            {
                return false;
            }

            var totalDistance = 0f;
            var trackedCount = 0;
            foreach (var pair in targetPositions)
            {
                if (pair.Key == null || !PlacementOverlayPositions.TryGetValue(pair.Key, out var previousPosition))
                {
                    continue;
                }

                totalDistance += (pair.Value - previousPosition).magnitude;
                trackedCount++;
            }

            if (trackedCount == 0)
            {
                return false;
            }

            var averageDistance = totalDistance / trackedCount;
            var assistProbeDistance = GetPlacementAssistBroadProbeDistance(activeSettings);
            var sampleSpacing = Mathf.Max(0.025f, assistProbeDistance * 0.5f);
            var sampleCount = Mathf.Clamp(Mathf.CeilToInt(averageDistance / sampleSpacing), 1, 5);

            // Sample along the handle path so fast drags can wake contact assist
            // before the selection visibly intersects a wall or floor.
            for (var sample = 1; sample <= sampleCount; sample++)
            {
                var t = sample / (float)sampleCount;
                foreach (var pair in targetPositions)
                {
                    if (pair.Key == null || !PlacementOverlayPositions.TryGetValue(pair.Key, out var previousPosition))
                    {
                        continue;
                    }

                    pair.Key.position = Vector3.Lerp(previousPosition, pair.Value, t);
                    EditorUtility.SetDirty(pair.Key);
                }

                Physics.SyncTransforms();
                if (SelectionHasNearbyContact(roots, activeSettings, assistProbeDistance))
                {
                    RewindPlacementOverlayPositions(targetPositions);
                    return true;
                }
            }

            RewindPlacementOverlayPositions(targetPositions);
            return false;
        }

        private static float GetPlacementAssistBroadProbeDistance(RealPhysicsPlacerSettings activeSettings)
        {
            return Mathf.Min(
                Mathf.Max(0.015f, activeSettings.move.physicsAssistDistance),
                Mathf.Max(0.04f, activeSettings.skinWidth * 8f));
        }

        private static float GetPlacementAssistReleaseProbeDistance(RealPhysicsPlacerSettings activeSettings)
        {
            return Mathf.Max(
                GetPlacementAssistBroadProbeDistance(activeSettings),
                activeSettings.move.physicsAssistDistance + Mathf.Max(0.01f, activeSettings.skinWidth * 4f));
        }

        private static bool SelectionHasNearbyContact(Transform[] roots, RealPhysicsPlacerSettings activeSettings, float probeDistance = -1f)
        {
            if (roots.Length == 0)
            {
                return false;
            }

            probeDistance = probeDistance > 0f
                ? Mathf.Max(0.015f, probeDistance)
                : Mathf.Max(0.015f, activeSettings.skinWidth * 4f);
            var ignoredTags = GetIgnoredTagSet(activeSettings);
            foreach (var root in roots)
            {
                if (root == null || !TryGetWorldBounds(root, out var bounds))
                {
                    continue;
                }

                var overlapCount = OverlapBoxLimited(
                    bounds.center,
                    ExpandExtents(bounds.extents, probeDistance),
                    Quaternion.identity,
                    activeSettings.collisionMask,
                    activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

                for (var i = 0; i < overlapCount; i++)
                {
                    var overlap = OverlapBuffer[i];
                    if (overlap != null && !ShouldIgnoreCollider(overlap, roots, true, root, ignoredTags, false))
                    {
                        return true;
                    }
                }

                foreach (var direction in ContactProbeDirections)
                {
                    if (FindNearestHit(bounds, direction, probeDistance, roots, true, out _, root, ignoredTags, false))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SelectionHasExternalOverlap(Transform[] roots, RealPhysicsPlacerSettings activeSettings)
        {
            if (roots.Length == 0)
            {
                return false;
            }

            var minimumPenetration = Mathf.Max(0.002f, activeSettings.skinWidth * 0.25f);
            var ignoredTags = GetIgnoredTagSet(activeSettings);
            var penetrationChecks = 0;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var hasCollider = false;
                var colliders = root.GetComponentsInChildren<Collider>(false);
                foreach (var selectedCollider in colliders)
                {
                    if (selectedCollider == null || !selectedCollider.enabled || (!activeSettings.includeTriggers && selectedCollider.isTrigger))
                    {
                        continue;
                    }

                    hasCollider = true;
                    var overlapCount = OverlapBoxLimited(
                        selectedCollider.bounds.center,
                        selectedCollider.bounds.extents,
                        Quaternion.identity,
                        activeSettings.collisionMask,
                        activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

                    for (var i = 0; i < overlapCount; i++)
                    {
                        var overlap = OverlapBuffer[i];
                        if (overlap == null
                            || overlap == selectedCollider
                            || ShouldIgnoreCollider(overlap, roots, true, root, ignoredTags))
                        {
                            continue;
                        }

                        if (NeedsAabbOverlapFallback(selectedCollider) || NeedsAabbOverlapFallback(overlap))
                        {
                            if (TryGetAabbCorrection(selectedCollider.bounds, overlap.bounds, 0f, out var correction)
                                && correction.magnitude > minimumPenetration)
                            {
                                return true;
                            }

                            continue;
                        }

                        if (penetrationChecks++ >= MaxPenetrationChecksPerPass)
                        {
                            return false;
                        }

                        if (Physics.ComputePenetration(
                                selectedCollider,
                                selectedCollider.transform.position,
                                selectedCollider.transform.rotation,
                                overlap,
                                overlap.transform.position,
                                overlap.transform.rotation,
                                out _,
                                out var distance)
                            && distance > minimumPenetration)
                        {
                            return true;
                        }
                    }
                }

                if (hasCollider || !TryGetWorldBounds(root, out var bounds))
                {
                    continue;
                }

                var boundsOverlapCount = OverlapBoxLimited(
                    bounds.center,
                    bounds.extents,
                    Quaternion.identity,
                    activeSettings.collisionMask,
                    activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

                for (var i = 0; i < boundsOverlapCount; i++)
                {
                    var overlap = OverlapBuffer[i];
                    if (overlap == null || ShouldIgnoreCollider(overlap, roots, true, root, ignoredTags))
                    {
                        continue;
                    }

                    if (TryGetAabbCorrection(bounds, overlap.bounds, 0f, out var correction)
                        && correction.magnitude > minimumPenetration)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int OverlapBoxLimited(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask, QueryTriggerInteraction query)
        {
            var count = Physics.OverlapBoxNonAlloc(center, halfExtents, OverlapBuffer, orientation, layerMask, query);
            return Mathf.Min(count, OverlapBuffer.Length);
        }

        private static bool NeedsAabbOverlapFallback(Collider collider)
        {
            return collider is MeshCollider meshCollider && !meshCollider.convex;
        }

        private static Vector3 MoveRootsAsGroup(Transform[] roots, Vector3 requestedDelta, bool ignoreAllSelected)
        {
            if (requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                return Vector3.zero;
            }

            var activeSettings = GetSettings();
            if (activeSettings.move.teleportWhenTooFar && requestedDelta.magnitude > activeSettings.move.teleportDistance)
            {
                TranslateRoots(roots, requestedDelta);
                return requestedDelta;
            }

            var distance = requestedDelta.magnitude;
            var stepCount = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(0.01f, activeSettings.maxMoveStep)), 1, 256);
            var stepDelta = requestedDelta / stepCount;
            var total = Vector3.zero;

            for (var i = 0; i < stepCount; i++)
            {
                var allowed = ResolveDeltaForRoots(roots, stepDelta, ignoreAllSelected, null);
                if (activeSettings.allowAxisSliding && (allowed - stepDelta).sqrMagnitude > Epsilon)
                {
                    allowed = ResolveAxisSliding(roots, stepDelta, ignoreAllSelected);
                }

                if (allowed.sqrMagnitude <= MotionEpsilonSqr)
                {
                    break;
                }

                TranslateRoots(roots, allowed);
                total += allowed;

                if ((allowed - stepDelta).sqrMagnitude > activeSettings.skinWidth * activeSettings.skinWidth)
                {
                    break;
                }
            }

            return total;
        }

        private static Vector3 MoveSingleRoot(Transform root, Vector3 requestedDelta, bool ignoreOtherSelected)
        {
            if (root == null || requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                return Vector3.zero;
            }

            var activeSettings = GetSettings();
            if (activeSettings.move.teleportWhenTooFar && requestedDelta.magnitude > activeSettings.move.teleportDistance)
            {
                TranslateRoots(new[] { root }, requestedDelta);
                return requestedDelta;
            }

            var stepCount = Mathf.Clamp(Mathf.CeilToInt(requestedDelta.magnitude / Mathf.Max(0.01f, activeSettings.maxMoveStep)), 1, 256);
            var stepDelta = requestedDelta / stepCount;
            var total = Vector3.zero;

            for (var i = 0; i < stepCount; i++)
            {
                var allowed = ResolveDeltaForRoots(new[] { root }, stepDelta, ignoreOtherSelected, root);
                if (activeSettings.allowAxisSliding && (allowed - stepDelta).sqrMagnitude > Epsilon)
                {
                    allowed = ResolveAxisSliding(new[] { root }, stepDelta, ignoreOtherSelected);
                }

                if (allowed.sqrMagnitude <= MotionEpsilonSqr)
                {
                    break;
                }

                TranslateRoots(new[] { root }, allowed);
                total += allowed;

                if ((allowed - stepDelta).sqrMagnitude > activeSettings.skinWidth * activeSettings.skinWidth)
                {
                    break;
                }
            }

            return total;
        }

        private static Vector3 ResolveAxisSliding(Transform[] roots, Vector3 requestedDelta, bool ignoreAllSelected)
        {
            var components = new List<Vector3>
            {
                new Vector3(requestedDelta.x, 0f, 0f),
                new Vector3(0f, requestedDelta.y, 0f),
                new Vector3(0f, 0f, requestedDelta.z)
            };
            components.Sort((left, right) => right.sqrMagnitude.CompareTo(left.sqrMagnitude));

            var total = Vector3.zero;
            foreach (var component in components)
            {
                if (component.sqrMagnitude <= MotionEpsilonSqr)
                {
                    continue;
                }

                var allowed = ResolveDeltaForRoots(roots, component, ignoreAllSelected, roots.Length == 1 ? roots[0] : null);
                if (allowed.sqrMagnitude <= MotionEpsilonSqr)
                {
                    continue;
                }

                TranslateRoots(roots, allowed);
                total += allowed;
            }

            if (total.sqrMagnitude > MotionEpsilonSqr)
            {
                TranslateRoots(roots, -total);
            }

            return total;
        }

        private static Vector3 ResolveDeltaForRoots(Transform[] roots, Vector3 delta, bool ignoreAllSelected, Transform movingRoot)
        {
            return ResolveDeltaForRoots(roots, delta, ignoreAllSelected, movingRoot, out _);
        }

        private static Vector3 ResolveDeltaForRoots(Transform[] roots, Vector3 delta, bool ignoreAllSelected, Transform movingRoot, out bool reachedContact)
        {
            if (delta.sqrMagnitude <= MotionEpsilonSqr)
            {
                reachedContact = false;
                return Vector3.zero;
            }

            var activeSettings = GetSettings();
            var distance = delta.magnitude;
            var direction = delta / distance;
            var allowedDistance = distance;
            reachedContact = false;
            var ignoredTags = GetIgnoredTagSet(activeSettings);
            var contactSlop = Mathf.Max(0.002f, activeSettings.skinWidth * 2f);
            var assistDistance = Mathf.Max(contactSlop, activeSettings.move.physicsAssistDistance);
            var castDistance = distance + activeSettings.skinWidth + assistDistance;

            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var foundHit = TryFindNearestColliderCastHit(root, direction, castDistance, roots, ignoreAllSelected, movingRoot, ignoredTags, out var hit);
                if (!foundHit && TryGetWorldBounds(root, out var bounds))
                {
                    foundHit = FindNearestHit(bounds, direction, castDistance, roots, ignoreAllSelected, out hit, movingRoot, ignoredTags);
                }

                if (foundHit)
                {
                    if (hit.distance <= distance + activeSettings.skinWidth + assistDistance)
                    {
                        reachedContact = true;
                    }

                    allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - activeSettings.skinWidth));
                }
            }

            return direction * Mathf.Clamp(allowedDistance, 0f, distance);
        }

        private static bool TryFindNearestColliderCastHit(Transform root, Vector3 direction, float distance, Transform[] roots, bool ignoreAllSelected, Transform movingRoot, HashSet<string> ignoredTags, out RaycastHit nearestHit)
        {
            nearestHit = default;
            var activeSettings = GetSettings();
            ColliderCastBuffer.Clear();
            root.GetComponentsInChildren(false, ColliderCastBuffer);
            var hasHit = false;
            var bestDistance = float.PositiveInfinity;

            foreach (var collider in ColliderCastBuffer)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (collider is MeshCollider meshCollider && !meshCollider.convex)
                {
                    continue;
                }

                var hitCount = CastSupportedColliderNonAlloc(collider, direction, distance, activeSettings);
                UpdateNearestCastHit(CastHitBuffer, hitCount, roots, ignoreAllSelected, movingRoot ?? root, ignoredTags, activeSettings, ref hasHit, ref bestDistance, ref nearestHit);
            }

            ColliderCastBuffer.Clear();
            return hasHit;
        }

        private static int CastSupportedColliderNonAlloc(Collider collider, Vector3 direction, float distance, RealPhysicsPlacerSettings activeSettings)
        {
            var query = activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            var layerMask = activeSettings.collisionMask;
            switch (collider)
            {
                case BoxCollider boxCollider:
                {
                    var center = boxCollider.transform.TransformPoint(boxCollider.center);
                    var halfExtents = Vector3.Scale(boxCollider.size * 0.5f, Abs(boxCollider.transform.lossyScale));
                    return Physics.BoxCastNonAlloc(center, ShrinkExtents(halfExtents, activeSettings.skinWidth), direction, CastHitBuffer, boxCollider.transform.rotation, distance, layerMask, query);
                }
                case SphereCollider sphereCollider:
                {
                    var center = sphereCollider.transform.TransformPoint(sphereCollider.center);
                    var radius = Mathf.Max(0.001f, sphereCollider.radius * MaxAbsComponent(sphereCollider.transform.lossyScale) - activeSettings.skinWidth);
                    return Physics.SphereCastNonAlloc(center, radius, direction, CastHitBuffer, distance, layerMask, query);
                }
                case CapsuleCollider capsuleCollider:
                {
                    GetCapsuleWorldGeometry(capsuleCollider, activeSettings.skinWidth, out var pointA, out var pointB, out var radius);
                    return Physics.CapsuleCastNonAlloc(pointA, pointB, radius, direction, CastHitBuffer, distance, layerMask, query);
                }
                default:
                    return 0;
            }
        }

        private static void UpdateNearestCastHit(RaycastHit[] hits, int hitCount, Transform[] roots, bool ignoreAllSelected, Transform movingRoot, HashSet<string> ignoredTags, RealPhysicsPlacerSettings activeSettings, ref bool hasHit, ref float bestDistance, ref RaycastHit nearestHit)
        {
            for (var i = 0; i < hitCount; i++)
            {
                var hit = hits[i];
                if (hit.collider == null
                    || (!activeSettings.includeTriggers && hit.collider.isTrigger)
                    || ShouldIgnoreCollider(hit.collider, roots, ignoreAllSelected, movingRoot, ignoredTags))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    hasHit = true;
                    bestDistance = hit.distance;
                    nearestHit = hit;
                }
            }
        }

        private static void GetCapsuleWorldGeometry(CapsuleCollider capsuleCollider, float skinWidth, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            var transform = capsuleCollider.transform;
            var scale = Abs(transform.lossyScale);
            var localAxis = capsuleCollider.direction switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };

            var axisScale = capsuleCollider.direction switch
            {
                0 => scale.x,
                1 => scale.y,
                _ => scale.z
            };

            var radiusScale = capsuleCollider.direction switch
            {
                0 => Mathf.Max(scale.y, scale.z),
                1 => Mathf.Max(scale.x, scale.z),
                _ => Mathf.Max(scale.x, scale.y)
            };

            radius = Mathf.Max(0.001f, capsuleCollider.radius * radiusScale - skinWidth);
            var height = Mathf.Max(radius * 2f, capsuleCollider.height * axisScale);
            var halfLine = Mathf.Max(0f, height * 0.5f - radius);
            var center = transform.TransformPoint(capsuleCollider.center);
            var axis = transform.TransformDirection(localAxis).normalized;
            pointA = center + axis * halfLine;
            pointB = center - axis * halfLine;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static bool FindNearestHit(
            Bounds bounds,
            Vector3 direction,
            float distance,
            Transform[] roots,
            bool ignoreAllSelected,
            out RaycastHit nearestHit,
            Transform movingRoot = null,
            HashSet<string> ignoredTags = null,
            bool ignoreShadowedSceneColliders = true)
        {
            nearestHit = default;
            var activeSettings = GetSettings();
            var extents = ShrinkExtents(bounds.extents, activeSettings.skinWidth);
            var hitCount = Physics.BoxCastNonAlloc(
                bounds.center,
                extents,
                direction,
                CastHitBuffer,
                Quaternion.identity,
                distance,
                activeSettings.collisionMask,
                activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

            var hasHit = false;
            var bestDistance = float.PositiveInfinity;
            ignoredTags ??= GetIgnoredTagSet(activeSettings);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = CastHitBuffer[i];
                if (hit.collider == null
                    || ShouldIgnoreCollider(hit.collider, roots, ignoreAllSelected, movingRoot, ignoredTags, ignoreShadowedSceneColliders))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    hasHit = true;
                    bestDistance = hit.distance;
                    nearestHit = hit;
                }
            }

            return hasHit;
        }

        private static void TranslateRoots(Transform[] roots, Vector3 delta)
        {
            if (delta.sqrMagnitude <= MotionEpsilonSqr)
            {
                return;
            }

            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                root.position += delta;
                EditorUtility.SetDirty(root);
            }

            Physics.SyncTransforms();
        }

        private static bool TryGetAabbCorrection(Bounds moving, Bounds obstacle, float skin, out Vector3 correction)
        {
            correction = Vector3.zero;
            var delta = moving.center - obstacle.center;
            var overlapX = moving.extents.x + obstacle.extents.x - Mathf.Abs(delta.x);
            var overlapY = moving.extents.y + obstacle.extents.y - Mathf.Abs(delta.y);
            var overlapZ = moving.extents.z + obstacle.extents.z - Mathf.Abs(delta.z);

            if (overlapX <= 0f || overlapY <= 0f || overlapZ <= 0f)
            {
                return false;
            }

            if (overlapX <= overlapY && overlapX <= overlapZ)
            {
                correction = Vector3.right * (delta.x >= 0f ? overlapX + skin : -overlapX - skin);
                return true;
            }

            if (overlapY <= overlapX && overlapY <= overlapZ)
            {
                correction = Vector3.up * (delta.y >= 0f ? overlapY + skin : -overlapY - skin);
                return true;
            }

            correction = Vector3.forward * (delta.z >= 0f ? overlapZ + skin : -overlapZ - skin);
            return true;
        }
    }
}
