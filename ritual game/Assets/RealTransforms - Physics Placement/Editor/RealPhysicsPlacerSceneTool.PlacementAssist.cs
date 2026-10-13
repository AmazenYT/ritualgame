﻿using System;
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
        // Placement assist watches normal Unity handle movement and takes over
        // only when nearby contacts need physics-aware movement.
        private static void UpdatePlacementPhysicsOverlay(Transform[] roots)
        {
            if (!Enabled || Mode != RealPhysicsPlacerMode.Placement || handlePullActive || fallActive)
            {
                if (placementOverlayPullActive)
                {
                    StopContinuousModes(false);
                }

                ResetPlacementOverlayTracking();
                return;
            }

            if (!PhysicsAssistEnabled && !placementOverlayHandoffActive)
            {
                ResetPlacementOverlayTracking();
                return;
            }

            if (roots.Length == 0)
            {
                if (placementOverlayPullActive)
                {
                    StopContinuousModes(false);
                }

                ResetPlacementOverlayTracking();
                return;
            }

            if (ShouldSuppressDuplicatePhysics(roots))
            {
                if (placementOverlayPullActive)
                {
                    StopContinuousModes(false);
                }

                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (placementOverlayHandoffActive)
            {
                UpdatePlacementOverlayHandoff(roots);
                return;
            }

            if (!TryGetTrackedPlacementDelta(roots, out var requestedDelta, out var currentPositions))
            {
                TrackPlacementOverlayPositions(roots);
                return;
            }

            var activeSettings = GetSettings();
            if (requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (!placementOverlayPullActive && ShouldSuppressPlacementAssistUntilClear(roots, activeSettings, currentPositions))
            {
                TrackPlacementOverlayPositions(roots);
                return;
            }

            RewindPlacementOverlayPositions(currentPositions);
            if (placementOverlayPullActive)
            {
                var pullAllowedDelta = ResolveDeltaForRoots(roots, requestedDelta, true, null, out var pullReachedContact);
                var targetDelta = GetPlacementOverlayPullTargetDelta(currentPositions, requestedDelta);
                var contactLimited = pullReachedContact
                    || DidContactLimitMove(requestedDelta, pullAllowedDelta, activeSettings);
                var nearbyContact = SelectionHasNearbyContact(roots, activeSettings);
                var hasActiveContact = contactLimited || nearbyContact;
                var hasReleaseProximity = hasActiveContact
                    || SelectionHasNearbyContact(roots, activeSettings, GetPlacementAssistReleaseProbeDistance(activeSettings));
                if (!placementOverlayHasPullDirection && hasActiveContact)
                {
                    SetPlacementOverlayPullDirection(targetDelta);
                }

                if (placementOverlayHasPullDirection && !contactLimited && !hasReleaseProximity && PlacementPullDirectionReversed(targetDelta))
                {
                    ResetPlacementOverlayPullTargetToSelection(roots);
                    UpdatePlacementOverlayContactState(false);
                    if (ShouldReleasePlacementOverlayPull())
                    {
                        ReleasePlacementOverlayPullAtCurrentPosition(roots, currentPositions);
                    }
                    else
                    {
                        TrackPlacementOverlayPullNativePositions(currentPositions);
                    }

                    return;
                }

                if (!hasReleaseProximity)
                {
                    ResetPlacementOverlayPullTargetToSelection(roots);
                }

                UpdatePlacementOverlayContactState(hasReleaseProximity);
                if (ShouldReleasePlacementOverlayPull())
                {
                    ReleasePlacementOverlayPullAtCurrentPosition(roots, currentPositions);
                    return;
                }

                if (hasReleaseProximity && targetDelta.sqrMagnitude > MotionEpsilonSqr)
                {
                    UpdatePlacementOverlayPullTarget(roots, ClampPlacementPullTargetDelta(targetDelta, activeSettings), true);
                }

                TrackPlacementOverlayPullNativePositions(currentPositions);
                return;
            }

            var allowedDelta = ResolveDeltaForRoots(roots, requestedDelta, true, null, out var reachedContact);
            var blocked = reachedContact
                || DidContactLimitMove(requestedDelta, allowedDelta, activeSettings)
                || PlacementAssistPathHasNearbyContact(roots, currentPositions, activeSettings);
            if (!blocked)
            {
                RestorePlacementOverlayPositions(currentPositions);
                TrackPlacementOverlayPositions(roots);
                return;
            }

            StartPlacementOverlayPull(roots, requestedDelta, currentPositions, true);
            TrackPlacementOverlayPositions(roots);
            RequestSceneRepaintFromEditorUpdate();
        }

        private static void StartPlacementOverlayPull(
            Transform[] roots,
            Vector3 requestedDelta,
            Dictionary<Transform, Vector3> nativePositions = null,
            bool absorbInitialNativeDelta = false)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms Assist";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.Move);
                hasSmoothedPullTarget = false;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            fallActive = false;
            placementOverlayPullActive = true;
            placementOverlayHasContact = true;
            placementOverlayHandoffActive = false;
            ClearDuplicateOverlapSuppression();
            if (absorbInitialNativeDelta)
            {
                placementOverlayHasPullDirection = false;
            }
            else
            {
                SetPlacementOverlayPullDirection(requestedDelta);
            }

            PlacementOverlayHandoffNativePositions.Clear();
            if (nativePositions != null)
            {
                TrackPlacementOverlayPullNativePositions(nativePositions);
            }
            else
            {
                PlacementOverlayPullNativePositions.Clear();
            }

            placementOverlayClearFrames = 0;
            placementOverlayPullFrames = 0;
            placementOverlayPullStartedAt = EditorApplication.timeSinceStartup;
            placementOverlayContactLostAt = -1d;
            handlePullActive = false;
            if (absorbInitialNativeDelta && nativePositions != null)
            {
                ResetPlacementOverlayPullTargetToSelection(roots);
            }
            else
            {
                UpdatePlacementOverlayPullTarget(roots, requestedDelta);
            }
        }

        private static void UpdatePlacementOverlayContactState(bool contactLimited)
        {
            var hadContact = placementOverlayHasContact;
            placementOverlayHasContact = contactLimited;
            placementOverlayPullFrames++;
            placementOverlayClearFrames = placementOverlayHasContact
                ? 0
                : placementOverlayClearFrames + 1;

            if (placementOverlayHasContact)
            {
                placementOverlayContactLostAt = -1d;
            }
            else if (hadContact || placementOverlayContactLostAt < 0d)
            {
                placementOverlayContactLostAt = EditorApplication.timeSinceStartup;
            }
        }

        private static bool ShouldReleasePlacementOverlayPull()
        {
            if (!placementOverlayPullActive || placementOverlayHasContact)
            {
                return false;
            }

            var now = EditorApplication.timeSinceStartup;
            var hasMinimumLifetimeElapsed = placementOverlayPullStartedAt < 0d
                || now - placementOverlayPullStartedAt >= PlacementOverlayMinimumPullSeconds;
            var hasClearTimeElapsed = placementOverlayContactLostAt >= 0d
                && now - placementOverlayContactLostAt >= PlacementOverlayReleaseClearSeconds;

            return placementOverlayPullActive
                && !placementOverlayHasContact
                && placementOverlayPullFrames >= PlacementOverlayPullGraceFrames
                && placementOverlayClearFrames >= PlacementOverlayReleaseClearFrames
                && hasMinimumLifetimeElapsed
                && hasClearTimeElapsed;
        }

        private static void ResetPlacementOverlayReleaseTiming()
        {
            placementOverlayPullStartedAt = -1d;
            placementOverlayContactLostAt = -1d;
        }

        private static void ReleasePlacementOverlayPullAtCurrentPosition(Transform[] roots, Dictionary<Transform, Vector3> nativePositions = null)
        {
            StopContinuousModes(false);
            if (SceneDragInProgress())
            {
                BeginPlacementOverlayHandoff(roots, nativePositions);
            }
            else
            {
                TrackPlacementOverlayPositions(roots);
            }

            ResetHandleToSelection();
            SceneView.RepaintAll();
        }

        private static void BeginPlacementOverlayHandoff(Transform[] roots, Dictionary<Transform, Vector3> nativePositions = null)
        {
            placementOverlayHandoffActive = SceneDragInProgress();
            PlacementOverlayHandoffNativePositions.Clear();
            if (!placementOverlayHandoffActive)
            {
                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (nativePositions != null)
            {
                TrackPlacementOverlayHandoffNativePositions(nativePositions);
            }

            TrackPlacementOverlayPositions(roots);
        }

        private static void EndPlacementOverlayHandoff()
        {
            placementOverlayHandoffActive = false;
            PlacementOverlayHandoffNativePositions.Clear();
        }

        private static void UpdatePlacementOverlayHandoff(Transform[] roots)
        {
            if (!SceneDragInProgress())
            {
                EndPlacementOverlayHandoff();
                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (!TryGetTrackedPlacementDelta(roots, out _, out var currentNativePositions))
            {
                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (CurrentPositionsMatchTrackedPlacement(currentNativePositions))
            {
                return;
            }

            var activeSettings = GetSettings();
            if (PhysicsAssistEnabled && ShouldSuppressPlacementAssistUntilClear(roots, activeSettings, currentNativePositions))
            {
                TrackPlacementOverlayHandoffNativePositions(currentNativePositions);
                TrackPlacementOverlayPositions(roots);
                return;
            }

            RewindPlacementOverlayPositions(currentNativePositions);
            if (!TryGetPlacementOverlayHandoffDelta(currentNativePositions, out var requestedDelta))
            {
                TrackPlacementOverlayHandoffNativePositions(currentNativePositions);
                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                TrackPlacementOverlayHandoffNativePositions(currentNativePositions);
                TrackPlacementOverlayPositions(roots);
                return;
            }

            if (PhysicsAssistEnabled)
            {
                var allowedDelta = ResolveDeltaForRoots(roots, requestedDelta, true, null, out var reachedContact);
                var blocked = reachedContact
                    || DidContactLimitMove(requestedDelta, allowedDelta, activeSettings)
                    || PlacementAssistPathHasNearbyContact(roots, currentNativePositions, activeSettings);
                if (blocked)
                {
                    StartPlacementOverlayPull(roots, requestedDelta, currentNativePositions, true);
                    TrackPlacementOverlayPositions(roots);
                    SceneView.RepaintAll();
                    return;
                }
            }

            TranslateRoots(roots, requestedDelta);
            TrackPlacementOverlayHandoffNativePositions(currentNativePositions);
            TrackPlacementOverlayPositions(roots);
            SceneView.RepaintAll();
        }

        private static bool ShouldSuppressPlacementAssistUntilClear(
            Transform[] roots,
            RealPhysicsPlacerSettings activeSettings,
            Dictionary<Transform, Vector3> currentPositions)
        {
            if (!SelectionMatchesDuplicateSuppression(roots)
                && !TryArmDuplicateOverlapSuppressionFromScene(roots, activeSettings))
            {
                ClearDuplicateOverlapSuppression();
                return false;
            }

            if (placementOverlaySuppressUntilClear)
            {
                if (SelectionHasExternalOverlap(roots, activeSettings))
                {
                    return true;
                }

                placementOverlaySuppressUntilClear = false;
                ClearDuplicateOverlapSuppression();
                return true;
            }

            if (currentPositions == null || currentPositions.Count == 0)
            {
                return false;
            }

            RewindPlacementOverlayPositions(currentPositions);
            var wasAlreadyOverlapping = SelectionHasExternalOverlap(roots, activeSettings);
            RestorePlacementOverlayPositions(currentPositions);

            if (!wasAlreadyOverlapping)
            {
                ClearDuplicateOverlapSuppression();
                return false;
            }

            placementOverlaySuppressUntilClear = SelectionHasExternalOverlap(roots, activeSettings);
            if (!placementOverlaySuppressUntilClear)
            {
                ClearDuplicateOverlapSuppression();
            }

            return true;
        }

        private static void MarkDuplicateOverlapSuppression(Transform[] previousRoots, Transform[] currentRoots, bool duplicateHasCopiedTemporaryState)
        {
            ClearDuplicateOverlapSuppression();
            if (!Enabled
                || Mode != RealPhysicsPlacerMode.Placement
                || previousRoots == null
                || currentRoots == null
                || !SelectionLooksLikeRecentDuplicate(previousRoots, currentRoots))
            {
                TryArmDuplicateOverlapSuppressionFromScene(currentRoots, GetSettings());
                return;
            }

            if (!duplicateHasCopiedTemporaryState)
            {
                return;
            }

            if (!SelectionHasExternalOverlap(currentRoots, GetSettings()))
            {
                return;
            }

            foreach (var root in currentRoots)
            {
                if (root != null)
                {
                    PlacementOverlayDuplicateSuppressRoots.Add(root.GetEntityId());
                }
            }
        }

        private static bool TryArmDuplicateOverlapSuppressionFromScene(Transform[] roots, RealPhysicsPlacerSettings activeSettings)
        {
            if (roots == null
                || roots.Length == 0
                || PlacementOverlayDuplicateSuppressRoots.Count > 0
                || !SelectionHasExternalOverlap(roots, activeSettings)
                || !AnyRootHasCopiedTemporaryPhysicsState(roots)
                || !SelectionLooksLikeEmbeddedDuplicate(roots))
            {
                return false;
            }

            RemoveCopiedTemporaryPhysicsComponents(roots);
            foreach (var root in roots)
            {
                if (root != null)
                {
                    PlacementOverlayDuplicateSuppressRoots.Add(root.GetEntityId());
                }
            }

            placementOverlaySuppressUntilClear = true;
            return true;
        }

        private static void ClearDuplicateOverlapSuppression()
        {
            PlacementOverlayDuplicateSuppressRoots.Clear();
            placementOverlaySuppressUntilClear = false;
        }

        private static bool SelectionMatchesDuplicateSuppression(Transform[] roots)
        {
            if (roots == null || roots.Length == 0 || PlacementOverlayDuplicateSuppressRoots.Count == 0)
            {
                return false;
            }

            foreach (var root in roots)
            {
                if (root == null || !PlacementOverlayDuplicateSuppressRoots.Contains(root.GetEntityId()))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SelectionLooksLikeRecentDuplicate(Transform[] previousRoots, Transform[] currentRoots)
        {
            if (previousRoots.Length == 0 || currentRoots.Length == 0 || previousRoots.Length != currentRoots.Length)
            {
                return false;
            }

            if (currentRoots.Any(root => root != null && previousRoots.Contains(root)))
            {
                return false;
            }

            var unmatchedPrevious = previousRoots.Where(root => root != null).ToList();
            foreach (var currentRoot in currentRoots)
            {
                if (currentRoot == null)
                {
                    return false;
                }

                var matchIndex = unmatchedPrevious.FindIndex(previousRoot => LooksLikeDuplicateOf(previousRoot, currentRoot));
                if (matchIndex < 0)
                {
                    return false;
                }

                unmatchedPrevious.RemoveAt(matchIndex);
            }

            return true;
        }

        private static bool SelectionLooksLikeFreshDuplicatePose(Transform[] previousRoots, Transform[] currentRoots)
        {
            if (previousRoots == null
                || currentRoots == null
                || previousRoots.Length == 0
                || currentRoots.Length == 0
                || previousRoots.Length != currentRoots.Length)
            {
                return false;
            }

            var unmatchedPrevious = previousRoots.Where(root => root != null).ToList();
            foreach (var currentRoot in currentRoots)
            {
                if (currentRoot == null)
                {
                    return false;
                }

                var matchIndex = unmatchedPrevious.FindIndex(previousRoot => LooksLikeFreshDuplicatePose(previousRoot, currentRoot));
                if (matchIndex < 0)
                {
                    return false;
                }

                unmatchedPrevious.RemoveAt(matchIndex);
            }

            return true;
        }

        private static bool SelectionLooksLikeEmbeddedDuplicate(Transform[] roots)
        {
            foreach (var root in roots)
            {
                if (root == null || !HasSimilarUnselectedOverlapSource(root, roots))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSimilarUnselectedOverlapSource(Transform root, Transform[] roots)
        {
            var candidates = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            foreach (var candidate in candidates)
            {
                if (candidate == null
                    || candidate == root
                    || !candidate.gameObject.scene.IsValid()
                    || (candidate.hideFlags & HideFlags.HideAndDontSave) != 0
                    || IsUnderAnyRoot(candidate, roots))
                {
                    continue;
                }

                if (LooksLikeDuplicateOf(candidate, root))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeDuplicateOf(Transform previousRoot, Transform currentRoot)
        {
            if (previousRoot == null || currentRoot == null)
            {
                return false;
            }

            if (previousRoot.parent != currentRoot.parent)
            {
                return false;
            }

            if (!string.Equals(
                    StripUnityDuplicateSuffix(previousRoot.name),
                    StripUnityDuplicateSuffix(currentRoot.name),
                    StringComparison.Ordinal))
            {
                return false;
            }

            var positionTolerance = Mathf.Max(0.02f, GetSettings().skinWidth * 4f);
            return (previousRoot.position - currentRoot.position).sqrMagnitude <= positionTolerance * positionTolerance
                && Quaternion.Dot(previousRoot.rotation, currentRoot.rotation) > 0.999f
                && (previousRoot.localScale - currentRoot.localScale).sqrMagnitude <= 0.0001f;
        }

        private static string StripUnityDuplicateSuffix(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || objectName[objectName.Length - 1] != ')')
            {
                return objectName;
            }

            var suffixStart = objectName.LastIndexOf(" (", StringComparison.Ordinal);
            var suffixLength = 2;
            if (suffixStart < 0)
            {
                suffixStart = objectName.LastIndexOf('(');
                suffixLength = 1;
            }

            if (suffixStart <= 0 || suffixStart >= objectName.Length - suffixLength)
            {
                return objectName;
            }

            for (var i = suffixStart + suffixLength; i < objectName.Length - 1; i++)
            {
                if (!char.IsDigit(objectName[i]))
                {
                    return objectName;
                }
            }

            return objectName.Substring(0, suffixStart);
        }

        private static bool LooksLikeFreshDuplicatePose(Transform previousRoot, Transform currentRoot)
        {
            if (previousRoot == null || currentRoot == null || previousRoot.parent != currentRoot.parent)
            {
                return false;
            }

            if (!string.Equals(
                    StripUnityDuplicateSuffix(previousRoot.name),
                    StripUnityDuplicateSuffix(currentRoot.name),
                    StringComparison.Ordinal))
            {
                return false;
            }

            return (previousRoot.position - currentRoot.position).sqrMagnitude <= 0.000001f
                && Mathf.Abs(Quaternion.Dot(previousRoot.rotation, currentRoot.rotation)) > 0.999999f
                && (previousRoot.localScale - currentRoot.localScale).sqrMagnitude <= 0.00000001f;
        }

        private static void UpdatePlacementOverlayPullTarget(Transform[] roots, Vector3 requestedDelta, bool incremental = false)
        {
            var center = TryGetCombinedBounds(roots, out var bounds) ? bounds.center : AveragePosition(roots);
            var target = incremental && hasMovePullTarget
                ? movePullTarget + requestedDelta
                : center + requestedDelta;
            movePullTarget = ClampMovePullTarget(center, target);
            pullTarget = movePullTarget;
            hasMovePullTarget = true;
            hasPullTarget = true;
            RequestSceneRepaintFromEditorUpdate();
        }

        private static void ResetPlacementOverlayPullTargetToSelection(Transform[] roots)
        {
            var center = TryGetCombinedBounds(roots, out var bounds) ? bounds.center : AveragePosition(roots);
            handlePosition = center;
            movePullTarget = center;
            pullTarget = center;
            hasMovePullTarget = true;
            hasPullTarget = true;
            hasSmoothedPullTarget = false;
            RequestSceneRepaintFromEditorUpdate();
        }

        private static Vector3 GetPlacementOverlayPullTargetDelta(Dictionary<Transform, Vector3> currentNativePositions, Vector3 fallbackDelta)
        {
            if (TryGetPlacementOverlayNativeDelta(currentNativePositions, PlacementOverlayPullNativePositions, out var nativeDelta))
            {
                return nativeDelta;
            }

            TrackPlacementOverlayPullNativePositions(currentNativePositions);
            return Vector3.zero;
        }

        private static Vector3 ClampPlacementPullTargetDelta(Vector3 targetDelta, RealPhysicsPlacerSettings activeSettings)
        {
            var maxStep = Mathf.Max(
                0.12f,
                Mathf.Min(activeSettings.move.targetMaxDistance, activeSettings.move.physicsAssistDistance * 2f));
            if (targetDelta.sqrMagnitude <= maxStep * maxStep)
            {
                return targetDelta;
            }

            return targetDelta.normalized * maxStep;
        }

        private static void SetPlacementOverlayPullDirection(Vector3 requestedDelta)
        {
            if (requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                return;
            }

            placementOverlayPullDirection = requestedDelta.normalized;
            placementOverlayHasPullDirection = true;
        }

        private static bool PlacementPullDirectionReversed(Vector3 requestedDelta)
        {
            if (!placementOverlayHasPullDirection || requestedDelta.sqrMagnitude <= MotionEpsilonSqr)
            {
                return false;
            }

            return Vector3.Dot(requestedDelta.normalized, placementOverlayPullDirection) < PlacementPullReverseDot;
        }

        private static bool TryGetTrackedPlacementDelta(Transform[] roots, out Vector3 requestedDelta, out Dictionary<Transform, Vector3> currentPositions)
        {
            requestedDelta = Vector3.zero;
            currentPositions = new Dictionary<Transform, Vector3>();
            if (PlacementOverlayPositions.Count == 0)
            {
                return false;
            }

            var totalDelta = Vector3.zero;
            var trackedCount = 0;
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                currentPositions[root] = root.position;
                if (!PlacementOverlayPositions.TryGetValue(root, out var previousPosition))
                {
                    return false;
                }

                totalDelta += root.position - previousPosition;
                trackedCount++;
            }

            if (trackedCount == 0)
            {
                return false;
            }

            requestedDelta = totalDelta / trackedCount;
            return true;
        }

        private static void TrackPlacementOverlayPositions(Transform[] roots)
        {
            PlacementOverlayPositions.Clear();
            foreach (var root in roots)
            {
                if (root != null)
                {
                    PlacementOverlayPositions[root] = root.position;
                }
            }
        }

        private static void TrackPlacementOverlayHandoffNativePositions(Dictionary<Transform, Vector3> nativePositions)
        {
            PlacementOverlayHandoffNativePositions.Clear();
            foreach (var pair in nativePositions)
            {
                if (pair.Key != null)
                {
                    PlacementOverlayHandoffNativePositions[pair.Key] = pair.Value;
                }
            }
        }

        private static void TrackPlacementOverlayPullNativePositions(Dictionary<Transform, Vector3> nativePositions)
        {
            PlacementOverlayPullNativePositions.Clear();
            foreach (var pair in nativePositions)
            {
                if (pair.Key != null)
                {
                    PlacementOverlayPullNativePositions[pair.Key] = pair.Value;
                }
            }
        }

        private static bool CurrentPositionsMatchTrackedPlacement(Dictionary<Transform, Vector3> currentPositions)
        {
            foreach (var pair in currentPositions)
            {
                if (pair.Key == null || !PlacementOverlayPositions.TryGetValue(pair.Key, out var trackedPosition))
                {
                    return false;
                }

                if ((pair.Value - trackedPosition).sqrMagnitude > MotionEpsilonSqr)
                {
                    return false;
                }
            }

            return currentPositions.Count > 0;
        }

        private static bool TryGetPlacementOverlayHandoffDelta(Dictionary<Transform, Vector3> currentNativePositions, out Vector3 requestedDelta)
        {
            return TryGetPlacementOverlayNativeDelta(currentNativePositions, PlacementOverlayHandoffNativePositions, out requestedDelta);
        }

        private static bool TryGetPlacementOverlayNativeDelta(
            Dictionary<Transform, Vector3> currentNativePositions,
            Dictionary<Transform, Vector3> previousNativePositions,
            out Vector3 requestedDelta)
        {
            requestedDelta = Vector3.zero;
            if (previousNativePositions.Count == 0)
            {
                return false;
            }

            var totalDelta = Vector3.zero;
            var trackedCount = 0;
            foreach (var pair in currentNativePositions)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                if (!previousNativePositions.TryGetValue(pair.Key, out var previousNativePosition))
                {
                    return false;
                }

                totalDelta += pair.Value - previousNativePosition;
                trackedCount++;
            }

            if (trackedCount == 0)
            {
                return false;
            }

            requestedDelta = totalDelta / trackedCount;
            return true;
        }

        private static void ResetPlacementOverlayTracking()
        {
            PlacementOverlayPositions.Clear();
            PlacementOverlayPullNativePositions.Clear();
            ClearDuplicateOverlapSuppression();
            ResetPlacementOverlayReleaseTiming();
            EndPlacementOverlayHandoff();
        }

        private static void RewindPlacementOverlayPositions(Dictionary<Transform, Vector3> currentPositions)
        {
            foreach (var pair in currentPositions)
            {
                if (pair.Key != null && PlacementOverlayPositions.TryGetValue(pair.Key, out var previousPosition))
                {
                    pair.Key.position = previousPosition;
                    EditorUtility.SetDirty(pair.Key);
                }
            }

            SyncPhysXBodiesToRoots();
            Physics.SyncTransforms();
        }

        private static void RestorePlacementOverlayPositions(Dictionary<Transform, Vector3> currentPositions)
        {
            foreach (var pair in currentPositions)
            {
                if (pair.Key != null)
                {
                    pair.Key.position = pair.Value;
                    EditorUtility.SetDirty(pair.Key);
                }
            }

            SyncPhysXBodiesToRoots();
            Physics.SyncTransforms();
        }

        private static void SyncPhysXBodiesToRoots()
        {
            if (!physXSessionActive)
            {
                return;
            }

            foreach (var body in PhysXBodies)
            {
                if (body.Body == null || body.Root == null)
                {
                    continue;
                }

                body.Body.position = body.Root.position;
                body.Body.rotation = body.Root.rotation;
                body.Body.WakeUp();
            }
        }

        private static void MovePlacementHandleDirectly(Transform[] roots, Vector3 visiblePosition, Vector3 nextPosition, Vector3 requestedDelta, RealPhysicsPlacerSettings activeSettings)
        {
            StopContinuousModes(false);
            Undo.RecordObjects(roots.Cast<Object>().ToArray(), "RealTransforms Move");
            var actualDelta = MoveRootsAsGroup(roots, requestedDelta, true);
            handlePosition = visiblePosition + actualDelta;
            hasHandle = true;

            if (DidContactLimitMove(requestedDelta, actualDelta, activeSettings))
            {
                movePullTarget = ClampMovePullTarget(handlePosition, nextPosition);
                hasMovePullTarget = true;
                StartHandlePull(roots, movePullTarget);
            }

            SceneView.RepaintAll();
        }
    }
}
