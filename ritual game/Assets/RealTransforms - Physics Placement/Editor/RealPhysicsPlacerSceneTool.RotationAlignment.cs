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
        // Rotation alignment applies requested rotations in contact-aware steps so
        // objects can level or turn without obvious collider intersections.
        private static float ApplyRotation(Transform[] roots, Quaternion requestedDelta, string undoName)
        {
            roots = roots.Where(root => root != null).ToArray();
            if (roots.Length == 0 || Quaternion.Dot(requestedDelta, Quaternion.identity) > 0.99999f)
            {
                return 0f;
            }

            var activeSettings = GetSettings();
            var savedPositions = new Vector3[roots.Length];
            var savedRotations = new Quaternion[roots.Length];
            CaptureRotationPose(roots, savedPositions, savedRotations);
            var contactBaseline = BuildRotationContactBaseline(roots, activeSettings);

            Undo.RecordObjects(roots.Cast<Object>().ToArray(), undoName);
            return ApplyRotationInContactSteps(roots, requestedDelta, activeSettings, savedPositions, savedRotations, contactBaseline);
        }

        private static void CaptureRotationPose(Transform[] roots, Vector3[] positions, Quaternion[] rotations)
        {
            for (var i = 0; i < roots.Length; i++)
            {
                positions[i] = roots[i].position;
                rotations[i] = roots[i].rotation;
            }
        }

        private static void RestoreRotationPose(Transform[] roots, Vector3[] positions, Quaternion[] rotations)
        {
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    roots[i].SetPositionAndRotation(positions[i], rotations[i]);
                }
            }

            Physics.SyncTransforms();
        }

        private static float ApplyRotationInContactSteps(
            Transform[] roots,
            Quaternion requestedDelta,
            RealPhysicsPlacerSettings activeSettings,
            Vector3[] savedPositions,
            Quaternion[] savedRotations,
            Dictionary<RotationContactPair, float> contactBaseline)
        {
            var angle = Quaternion.Angle(Quaternion.identity, requestedDelta);
            var stepCount = CalculateRotationStepCount(roots, angle, activeSettings);
            var appliedSteps = 0;
            var stepPositions = new Vector3[roots.Length];
            var stepRotations = new Quaternion[roots.Length];
            var candidatePositions = new Vector3[roots.Length];
            var candidateRotations = new Quaternion[roots.Length];

            for (var stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                CaptureRotationPose(roots, stepPositions, stepRotations);
                var stepDelta = BuildRotationStepDelta(requestedDelta, stepIndex, stepCount);
                ApplyRotationDelta(roots, stepDelta);
                Physics.SyncTransforms();

                if (RotationStepIsBlocked(
                        roots,
                        activeSettings,
                        contactBaseline,
                        stepPositions,
                        stepRotations,
                        candidatePositions,
                        candidateRotations))
                {
                    RestoreRotationPose(roots, stepPositions, stepRotations);
                    break;
                }

                appliedSteps++;
            }

            if (appliedSteps == 0)
            {
                RestoreRotationPose(roots, savedPositions, savedRotations);
                return 0f;
            }

            return appliedSteps / (float)stepCount;
        }

        private static int CalculateRotationStepCount(Transform[] roots, float angleDegrees, RealPhysicsPlacerSettings activeSettings)
        {
            var maxStepDegrees = Mathf.Max(0.25f, activeSettings.rotate.maxStepDegrees);
            var angularSteps = Mathf.CeilToInt(angleDegrees / maxStepDegrees);
            var surfaceTravel = EstimateRotationSurfaceRadius(roots) * angleDegrees * Mathf.Deg2Rad;
            var surfaceSteps = Mathf.CeilToInt(surfaceTravel / GetRotationSurfaceStep(activeSettings));
            return Mathf.Clamp(Mathf.Max(angularSteps, surfaceSteps), 1, 512);
        }

        private static float EstimateRotationSurfaceRadius(Transform[] roots)
        {
            var radius = 0.01f;
            foreach (var root in roots)
            {
                if (root == null || !TryGetWorldBounds(root, out var bounds))
                {
                    continue;
                }

                radius = Mathf.Max(radius, (bounds.center - root.position).magnitude + bounds.extents.magnitude);
            }

            return radius;
        }

        private static float GetRotationSurfaceStep(RealPhysicsPlacerSettings activeSettings)
        {
            var toleranceStep = Mathf.Max(0.004f, activeSettings.rotate.contactTolerance * 2f);
            var skinStep = Mathf.Max(0.004f, activeSettings.skinWidth * 0.5f);
            return Mathf.Clamp(Mathf.Min(toleranceStep, skinStep), 0.004f, 0.02f);
        }

        private static Quaternion BuildRotationStepDelta(Quaternion requestedDelta, int stepIndex, int stepCount)
        {
            var previous = Quaternion.Slerp(Quaternion.identity, requestedDelta, stepIndex / (float)stepCount);
            var next = Quaternion.Slerp(Quaternion.identity, requestedDelta, (stepIndex + 1f) / stepCount);
            return next * Quaternion.Inverse(previous);
        }

        private static bool RotationStepIsBlocked(
            Transform[] roots,
            RealPhysicsPlacerSettings activeSettings,
            Dictionary<RotationContactPair, float> contactBaseline,
            Vector3[] previousPositions,
            Quaternion[] previousRotations,
            Vector3[] candidatePositions,
            Quaternion[] candidateRotations)
        {
            if (RotationIncreasesPenetration(roots, activeSettings, contactBaseline))
            {
                return true;
            }

            CaptureRotationPose(roots, candidatePositions, candidateRotations);
            for (var sampleIndex = 1; sampleIndex <= 2; sampleIndex++)
            {
                var t = sampleIndex / 3f;
                for (var i = 0; i < roots.Length; i++)
                {
                    if (roots[i] == null)
                    {
                        continue;
                    }

                    roots[i].SetPositionAndRotation(
                        Vector3.Lerp(previousPositions[i], candidatePositions[i], t),
                        Quaternion.Slerp(previousRotations[i], candidateRotations[i], t));
                }

                Physics.SyncTransforms();
                if (RotationIncreasesPenetration(roots, activeSettings, contactBaseline))
                {
                    RestoreRotationPose(roots, candidatePositions, candidateRotations);
                    return true;
                }
            }

            RestoreRotationPose(roots, candidatePositions, candidateRotations);
            return false;
        }

        private static Dictionary<RotationContactPair, float> BuildRotationContactBaseline(Transform[] roots, RealPhysicsPlacerSettings activeSettings)
        {
            var baseline = new Dictionary<RotationContactPair, float>();
            AccumulateRotationContacts(roots, activeSettings, baseline, null);
            return baseline;
        }

        private static bool RotationIncreasesPenetration(
            Transform[] roots,
            RealPhysicsPlacerSettings activeSettings,
            Dictionary<RotationContactPair, float> contactBaseline)
        {
            var currentContacts = new Dictionary<RotationContactPair, float>();
            var worstIncrease = 0f;
            AccumulateRotationContacts(roots, activeSettings, currentContacts, (pair, distance) =>
            {
                contactBaseline.TryGetValue(pair, out var baselineDistance);
                worstIncrease = Mathf.Max(worstIncrease, distance - baselineDistance);
            });

            return worstIncrease > Mathf.Max(0.001f, activeSettings.rotate.contactTolerance);
        }

        private static void AccumulateRotationContacts(
            Transform[] roots,
            RealPhysicsPlacerSettings activeSettings,
            Dictionary<RotationContactPair, float> contacts,
            Action<RotationContactPair, float> visitor)
        {
            var query = activeSettings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            var ignoredTags = activeSettings.BuildIgnoredTagSet();

            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var ownRoot = new[] { root };
                var hasCollider = false;
                foreach (var selectedCollider in root.GetComponentsInChildren<Collider>(false))
                {
                    if (selectedCollider == null
                        || !selectedCollider.enabled
                        || (!activeSettings.includeTriggers && selectedCollider.isTrigger))
                    {
                        continue;
                    }

                    hasCollider = true;
                    var overlaps = Physics.OverlapBox(
                        selectedCollider.bounds.center,
                        ExpandExtents(selectedCollider.bounds.extents, activeSettings.rotate.contactTolerance),
                        Quaternion.identity,
                        activeSettings.collisionMask,
                        query);

                    foreach (var overlap in overlaps)
                    {
                        if (overlap == null || overlap == selectedCollider || ShouldIgnoreCollider(overlap, ownRoot, false, root, ignoredTags))
                        {
                            continue;
                        }

                        if (!TryGetRotationContactDistance(selectedCollider, overlap, out var distance))
                        {
                            continue;
                        }

                        var pair = new RotationContactPair(selectedCollider.GetEntityId(), overlap.GetEntityId());
                        RecordRotationContact(contacts, visitor, pair, distance);
                    }
                }

                if (hasCollider || !TryGetWorldBounds(root, out var bounds))
                {
                    continue;
                }

                var boundsOverlaps = Physics.OverlapBox(
                    bounds.center,
                    ExpandExtents(bounds.extents, activeSettings.rotate.contactTolerance),
                    Quaternion.identity,
                    activeSettings.collisionMask,
                    query);

                foreach (var overlap in boundsOverlaps)
                {
                    if (overlap == null || ShouldIgnoreCollider(overlap, ownRoot, false, root, ignoredTags))
                    {
                        continue;
                    }

                    if (!TryGetAabbCorrection(bounds, overlap.bounds, 0f, out var correction))
                    {
                        continue;
                    }

                    var pair = new RotationContactPair(root.GetEntityId(), overlap.GetEntityId());
                    RecordRotationContact(contacts, visitor, pair, correction.magnitude);
                }
            }
        }

        private static bool TryGetRotationContactDistance(Collider selectedCollider, Collider overlap, out float distance)
        {
            distance = 0f;
            if (Physics.ComputePenetration(
                    selectedCollider,
                    selectedCollider.transform.position,
                    selectedCollider.transform.rotation,
                    overlap,
                    overlap.transform.position,
                    overlap.transform.rotation,
                    out _,
                    out distance))
            {
                return distance > Epsilon;
            }

            if (CanUseRotationAabbFallback(selectedCollider, overlap)
                && TryGetAabbCorrection(selectedCollider.bounds, overlap.bounds, 0f, out var correction))
            {
                distance = correction.magnitude;
                return distance > Epsilon;
            }

            return false;
        }

        private static bool CanUseRotationAabbFallback(Collider selectedCollider, Collider overlap)
        {
            return selectedCollider is not TerrainCollider && overlap is not TerrainCollider;
        }

        private static void RecordRotationContact(
            Dictionary<RotationContactPair, float> contacts,
            Action<RotationContactPair, float> visitor,
            RotationContactPair pair,
            float distance)
        {
            if (distance <= Epsilon)
            {
                return;
            }

            if (!contacts.TryGetValue(pair, out var existing) || distance > existing)
            {
                contacts[pair] = distance;
            }

            visitor?.Invoke(pair, distance);
        }

        private static void ApplyRotationDelta(Transform[] roots, Quaternion delta)
        {
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var nextRotation = delta * root.rotation;
                root.rotation = nextRotation;
                EditorUtility.SetDirty(root);
            }
        }

        private static void ApplyAlignment(Transform[] roots, Vector3 axis, float signedDistance)
        {
            if (roots.Length < 2 || Mathf.Abs(signedDistance) <= Epsilon)
            {
                return;
            }

            var activeSettings = GetSettings();
            var ordered = roots
                .Where(root => root != null && TryGetWorldBounds(root, out _))
                .Select(root =>
                {
                    TryGetWorldBounds(root, out var bounds);
                    return new AlignmentItem(root, bounds, ProjectMin(bounds, axis), ProjectMax(bounds, axis));
                })
                .OrderBy(item => item.Min)
                .ToArray();

            if (ordered.Length < 2)
            {
                return;
            }

            var spacing = Mathf.Max(0f, signedDistance) * activeSettings.arrange.handleStrength + activeSettings.arrange.padding;
            var previousMax = ordered[0].Max;
            for (var i = 1; i < ordered.Length; i++)
            {
                var item = ordered[i];
                var desiredMin = previousMax + spacing;
                var deltaOnAxis = desiredMin - item.Min;
                var moved = MoveSingleRoot(item.Root, axis * deltaOnAxis, true);
                if (TryGetWorldBounds(item.Root, out var updatedBounds))
                {
                    previousMax = ProjectMax(updatedBounds, axis);
                }
                else
                {
                    previousMax += Vector3.Dot(moved, axis);
                }
            }
        }

        private readonly struct RotationContactPair : IEquatable<RotationContactPair>
        {
            private readonly EntityId selectedId;
            private readonly EntityId otherId;

            internal RotationContactPair(EntityId selectedId, EntityId otherId)
            {
                this.selectedId = selectedId;
                this.otherId = otherId;
            }

            public bool Equals(RotationContactPair other)
            {
                return selectedId.Equals(other.selectedId) && otherId.Equals(other.otherId);
            }

            public override bool Equals(object obj)
            {
                return obj is RotationContactPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (selectedId.GetHashCode() * 397) ^ otherId.GetHashCode();
                }
            }
        }
    }
}
