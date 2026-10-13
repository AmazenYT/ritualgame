using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Arrange mode stores anchor poses, then applies spacing, clump, grid, and
        // rotation targets relative to those anchors.
        private static Vector3 arrangeHandleScale = Vector3.one;
        private static Vector3 arrangeLinearFactor = Vector3.one;
        private static Vector3 arrangeLastRawHandleScale = Vector3.one;
        private static Vector3 arrangeAxisLock = Vector3.one;
        private static bool arrangeHasAxisLock;
        private static readonly Dictionary<Transform, Vector3> ArrangeAnchorPositions = new Dictionary<Transform, Vector3>();
        private static readonly Dictionary<Transform, Bounds> ArrangeAnchorBounds = new Dictionary<Transform, Bounds>();
        private static readonly Dictionary<Transform, Quaternion> ArrangeAnchorRotations = new Dictionary<Transform, Quaternion>();
        private static readonly Dictionary<Transform, Vector3> ArrangePhysicsTargets = new Dictionary<Transform, Vector3>();
        private static readonly Dictionary<Transform, Quaternion> ArrangePhysicsRotationTargets = new Dictionary<Transform, Quaternion>();
        private static readonly Dictionary<Transform, Vector3> ArrangeRandomSpinAngularVelocities = new Dictionary<Transform, Vector3>();
        private static readonly HashSet<KeyCode> ArrangeActionHeldKeys = new HashSet<KeyCode>();
        private static readonly System.Random ArrangeRandom = new System.Random();
        private static Transform[] arrangeAnchorRoots = Array.Empty<Transform>();
        private static Bounds arrangeAnchorBounds;
        private static Vector3 arrangeAnchorCenter;
        private static Quaternion arrangeHandleRotation = Quaternion.identity;
        private static Quaternion arrangeLastRawHandleRotation = Quaternion.identity;
        private static bool arrangeDragActive;
        private static bool arrangeEvenSpacingActive;
        private static KeyCode arrangeEvenSpacingKey = KeyCode.None;

        internal static void ArrangeSelection(float scaleFactor)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            var roots = GetLiveSelectionRoots();
            if (roots.Length < 2 || !TryGetCombinedBounds(roots, out var bounds))
            {
                return;
            }

            Undo.RecordObjects(roots.Cast<Object>().ToArray(), "RealTransforms Arrange");
            ApplyArrangeScale(roots, bounds.center, Vector3.one * Mathf.Max(0.05f, scaleFactor));
            ResetHandleToSelection();
            SceneView.RepaintAll();
        }

        private static void DrawArrangeHandles(Transform[] roots)
        {
            if (roots.Length < 2)
            {
                ResetArrangeState();
                Handles.Label(handlePosition + Vector3.up * HandleUtility.GetHandleSize(handlePosition) * 0.25f, "Select two or more objects");
                return;
            }

            if (!TryGetCombinedBounds(roots, out var bounds))
            {
                return;
            }

            EnsureArrangeAnchor(roots, bounds);
            var center = arrangeAnchorCenter;
            handlePosition = center;
            var size = HandleUtility.GetHandleSize(center);
            using (new Handles.DrawingScope(GetSettings().blockedColor))
            {
                Handles.DrawWireCube(arrangeDragActive ? arrangeAnchorBounds.center : bounds.center, arrangeDragActive ? arrangeAnchorBounds.size : bounds.size);
            }

            EditorGUI.BeginChangeCheck();
            var nextScale = Handles.ScaleHandle(arrangeHandleScale, center, Quaternion.identity, size * 0.82f);
            if (EditorGUI.EndChangeCheck())
            {
                var factor = BuildArrangeFactor(nextScale);
                if (ScaleHasMeaningfulDelta(factor))
                {
                    arrangeDragActive = true;
                    ApplyArrangeScaleFromAnchor(roots, factor);
                    SceneView.RepaintAll();
                }

                arrangeHandleScale = Vector3.one;
            }

            EditorGUI.BeginChangeCheck();
            var nextRotation = Handles.RotationHandle(arrangeHandleRotation, center);
            if (EditorGUI.EndChangeCheck())
            {
                nextRotation = ConstrainRotationTarget(
                    arrangeLastRawHandleRotation,
                    nextRotation,
                    GetActiveRotationConstraint(GetSettings(), PhysicsModeProfile.ArrangeRotate));
                var delta = nextRotation * Quaternion.Inverse(arrangeLastRawHandleRotation);
                if (Quaternion.Angle(Quaternion.identity, delta) > 0.05f)
                {
                    arrangeDragActive = true;
                    ApplyArrangeLocalRotationFromAnchor(roots, nextRotation);
                    SceneView.RepaintAll();
                }

                arrangeHandleRotation = nextRotation;
                arrangeLastRawHandleRotation = nextRotation;
            }

            if (Event.current.rawType == EventType.MouseUp)
            {
                FinishArrangeDrag();
            }
        }

        private static void EnsureArrangeAnchor(Transform[] roots, Bounds bounds)
        {
            if (arrangeDragActive && ArrangeAnchorMatches(roots))
            {
                return;
            }

            arrangeAnchorRoots = roots.Where(root => root != null).ToArray();
            arrangeAnchorBounds = bounds;
            arrangeAnchorCenter = bounds.center;
            ArrangeAnchorPositions.Clear();
            ArrangeAnchorBounds.Clear();
            ArrangeAnchorRotations.Clear();
            foreach (var root in arrangeAnchorRoots)
            {
                ArrangeAnchorPositions[root] = GetPhysicsTruthPoint(root);
                ArrangeAnchorRotations[root] = root.rotation;
                if (TryGetWorldBounds(root, out var rootBounds))
                {
                    ArrangeAnchorBounds[root] = rootBounds;
                }
            }

            arrangeHandleScale = Vector3.one;
            arrangeLinearFactor = Vector3.one;
            arrangeLastRawHandleScale = Vector3.one;
            arrangeHandleRotation = Quaternion.identity;
            arrangeLastRawHandleRotation = Quaternion.identity;
        }

        private static bool ArrangeAnchorMatches(Transform[] roots)
        {
            var liveRoots = roots.Where(root => root != null).OrderBy(root => root.GetEntityId()).ToArray();
            var anchoredRoots = arrangeAnchorRoots.Where(root => root != null).OrderBy(root => root.GetEntityId()).ToArray();
            if (liveRoots.Length != anchoredRoots.Length)
            {
                return false;
            }

            for (var i = 0; i < liveRoots.Length; i++)
            {
                if (liveRoots[i] != anchoredRoots[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 BuildArrangeFactor(Vector3 nextScale)
        {
            nextScale = ClampArrangeRawScale(nextScale);
            var strength = Mathf.Max(0.01f, GetSettings().arrange.handleStrength);
            if (!arrangeDragActive)
            {
                arrangeLinearFactor = Vector3.one;
                arrangeLastRawHandleScale = Vector3.one;
                arrangeAxisLock = Vector3.one;
                arrangeHasAxisLock = false;
            }

            var delta = FilterArrangeHandleDelta(nextScale - arrangeLastRawHandleScale);
            arrangeLastRawHandleScale = nextScale;
            arrangeLinearFactor = ClampArrangeFactor(arrangeLinearFactor + delta * strength);
            return arrangeLinearFactor;
        }

        private static Vector3 ClampArrangeRawScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Clamp(scale.x, -5f, 20f),
                Mathf.Clamp(scale.y, -5f, 20f),
                Mathf.Clamp(scale.z, -5f, 20f));
        }

        private static Vector3 ClampArrangeFactor(Vector3 factor)
        {
            return new Vector3(
                Mathf.Clamp(factor.x, -5f, 20f),
                Mathf.Clamp(factor.y, -5f, 20f),
                Mathf.Clamp(factor.z, -5f, 20f));
        }

        private static bool ScaleHasMeaningfulDelta(Vector3 factor)
        {
            return Mathf.Abs(factor.x - 1f) > 0.001f
                || Mathf.Abs(factor.y - 1f) > 0.001f
                || Mathf.Abs(factor.z - 1f) > 0.001f;
        }

        private static Vector3 FilterArrangeHandleDelta(Vector3 delta)
        {
            const float axisEpsilon = 0.0005f;
            if (!arrangeHasAxisLock)
            {
                var abs = new Vector3(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
                var max = Mathf.Max(abs.x, Mathf.Max(abs.y, abs.z));
                if (max <= axisEpsilon)
                {
                    return Vector3.zero;
                }

                var mask = new Vector3(
                    abs.x > axisEpsilon ? 1f : 0f,
                    abs.y > axisEpsilon ? 1f : 0f,
                    abs.z > axisEpsilon ? 1f : 0f);
                var changedCount = Mathf.RoundToInt(mask.x + mask.y + mask.z);
                if (changedCount > 1)
                {
                    var second = GetSecondLargest(abs);
                    if (second <= axisEpsilon || max > second * 1.35f)
                    {
                        mask = DominantAxisMask(abs);
                    }
                }

                arrangeAxisLock = mask;
                arrangeHasAxisLock = true;
            }

            return Vector3.Scale(delta, arrangeAxisLock);
        }

        private static float GetSecondLargest(Vector3 value)
        {
            var largest = Mathf.Max(value.x, Mathf.Max(value.y, value.z));
            var smallest = Mathf.Min(value.x, Mathf.Min(value.y, value.z));
            return value.x + value.y + value.z - largest - smallest;
        }

        private static Vector3 DominantAxisMask(Vector3 value)
        {
            if (value.x >= value.y && value.x >= value.z)
            {
                return Vector3.right;
            }

            return value.y >= value.z ? Vector3.up : Vector3.forward;
        }

        private static bool ArrangeFactorIsClumping(Vector3 factor)
        {
            return (factor.x + factor.y + factor.z) / 3f < 1f;
        }

        private static void ApplyArrangeScaleFromAnchor(Transform[] roots, Vector3 factor)
        {
            if (!arrangeDragActive || ArrangeAnchorPositions.Count == 0)
            {
                return;
            }

            if (GetSettings().arrange.collisionAware)
            {
                UpdateArrangePhysicsTargets(roots, arrangeAnchorCenter, factor, GetArrangeAnchorPosition, GetArrangeAnchorBounds);
                return;
            }

            Undo.RecordObjects(roots.Cast<Object>().ToArray(), "RealTransforms Arrange");
            ApplyArrangeTargets(roots, arrangeAnchorCenter, factor, GetArrangeAnchorPosition, GetArrangeAnchorBounds);
        }

        private static void ApplyArrangeScale(Transform[] roots, Vector3 center, Vector3 factor)
        {
            ApplyArrangeTargets(roots, center, factor, GetPhysicsTruthPoint, TryGetWorldBoundsOrDefault);
        }

        private static void ApplyArrangeLocalRotationFromAnchor(Transform[] roots, Quaternion localDelta)
        {
            if (!arrangeDragActive || ArrangeAnchorRotations.Count == 0)
            {
                return;
            }

            if (GetSettings().arrange.collisionAware)
            {
                UpdateArrangePhysicsRotationTargets(roots, localDelta);
                return;
            }

            Undo.RecordObjects(roots.Cast<Object>().ToArray(), "RealTransforms Arrange Rotate");
            foreach (var root in roots)
            {
                if (root == null || !ArrangeAnchorRotations.TryGetValue(root, out var anchorRotation))
                {
                    continue;
                }

                root.rotation = anchorRotation * localDelta;
                EditorUtility.SetDirty(root);
            }

            Physics.SyncTransforms();
        }

        private static void ApplyArrangeTargets(
            Transform[] roots,
            Vector3 center,
            Vector3 factor,
            Func<Transform, Vector3> sourcePosition,
            Func<Transform, Bounds> sourceBounds)
        {
            var activeSettings = GetSettings();
            var targetPositions = BuildArrangeTargetPositions(roots, center, factor, sourcePosition, sourceBounds);

            var moves = new List<ArrangeMove>();
            foreach (var pair in targetPositions)
            {
                var root = pair.Key;
                if (root == null)
                {
                    continue;
                }

                var delta = pair.Value - GetPhysicsTruthPoint(root);
                if (delta.sqrMagnitude > Epsilon)
                {
                    moves.Add(new ArrangeMove(root, delta, (sourcePosition(root) - center).sqrMagnitude));
                }
            }

            var clumping = ArrangeFactorIsClumping(factor);
            moves.Sort((left, right) => clumping
                ? left.AnchorDistance.CompareTo(right.AnchorDistance)
                : right.AnchorDistance.CompareTo(left.AnchorDistance));
            foreach (var move in moves)
            {
                if (move.Root == null)
                {
                    continue;
                }

                TranslateRoots(new[] { move.Root }, move.Delta);
            }

        }

        private static void UpdateArrangePhysicsTargets(
            Transform[] roots,
            Vector3 center,
            Vector3 factor,
            Func<Transform, Vector3> sourcePosition,
            Func<Transform, Bounds> sourceBounds)
        {
            var targetPositions = BuildArrangeTargetPositions(roots, center, factor, sourcePosition, sourceBounds);
            if (targetPositions.Count == 0)
            {
                return;
            }

            StartArrangePull(roots);
            ArrangePhysicsTargets.Clear();
            foreach (var pair in targetPositions)
            {
                if (pair.Key != null)
                {
                    ArrangePhysicsTargets[pair.Key] = pair.Value;
                }
            }
        }

        private static void UpdateArrangePhysicsRotationTargets(Transform[] roots, Quaternion localDelta)
        {
            StartArrangeRotationPull(roots);
            ArrangePhysicsRotationTargets.Clear();
            foreach (var root in roots)
            {
                if (root == null || !ArrangeAnchorRotations.TryGetValue(root, out var anchorRotation))
                {
                    continue;
                }

                ArrangePhysicsRotationTargets[root] = anchorRotation * localDelta;
            }
        }

        private static Dictionary<Transform, Vector3> BuildArrangeTargetPositions(
            Transform[] roots,
            Vector3 center,
            Vector3 factor,
            Func<Transform, Vector3> sourcePosition,
            Func<Transform, Bounds> sourceBounds)
        {
            var targetPositions = new Dictionary<Transform, Vector3>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                targetPositions[root] = sourcePosition(root);
            }

            if (ChangedArrangeAxisCount(factor) > 1)
            {
                foreach (var root in roots)
                {
                    if (root == null || !targetPositions.TryGetValue(root, out var position))
                    {
                        continue;
                    }

                    targetPositions[root] = center + Vector3.Scale(position - center, factor);
                }
            }
            else
            {
                ApplyArrangeAxisDisplacement(roots, center, Vector3.right, factor.x, sourcePosition, targetPositions);
                ApplyArrangeAxisDisplacement(roots, center, Vector3.up, factor.y, sourcePosition, targetPositions);
                ApplyArrangeAxisDisplacement(roots, center, Vector3.forward, factor.z, sourcePosition, targetPositions);
            }

            return targetPositions;
        }

        private static void StartArrangePull(Transform[] roots, bool ignoreSelectedCollisions = false)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            roots = roots.Where(root => root != null).ToArray();
            if (roots.Length < 2)
            {
                return;
            }

            if (physXSessionActive
                && physXSessionProfile == PhysicsModeProfile.Arrange
                && arrangeGridPullActive != ignoreSelectedCollisions)
            {
                StopContinuousModes(false);
            }

            arrangeGridPullActive = ignoreSelectedCollisions;

            if (fallActive
                || handlePullActive
                || rotationPullActive
                || placementOverlayPullActive
                || arrangeRotationPullActive
                || (physXSessionActive && (!PhysXSessionMatchesRoots(roots) || physXSessionProfile != PhysicsModeProfile.Arrange)))
            {
                StopContinuousModes(false);
            }

            arrangeGridPullActive = ignoreSelectedCollisions;

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms Arrange";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.Arrange);
                hasSmoothedPullTarget = false;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            fallActive = false;
            placementOverlayPullActive = false;
            placementOverlayHasContact = false;
            placementOverlayClearFrames = 0;
            placementOverlayPullFrames = 0;
            placementOverlayHasPullDirection = false;
            ResetPlacementOverlayReleaseTiming();
            handlePullActive = false;
            rotationPullActive = false;
            arrangePullActive = true;
            arrangeGridPullActive = ignoreSelectedCollisions;
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            arrangeEvenSpacingActive = false;
            arrangeEvenSpacingKey = KeyCode.None;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
        }

        private static void StartArrangeRotationPull(Transform[] roots)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            roots = roots.Where(root => root != null).ToArray();
            if (roots.Length < 1)
            {
                return;
            }

            if (fallActive
                || handlePullActive
                || rotationPullActive
                || placementOverlayPullActive
                || arrangePullActive
                || (physXSessionActive && (!PhysXSessionMatchesRoots(roots) || physXSessionProfile != PhysicsModeProfile.ArrangeRotate)))
            {
                StopContinuousModes(false);
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms Arrange Rotate";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.ArrangeRotate);
                hasSmoothedPullTarget = false;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            fallActive = false;
            placementOverlayPullActive = false;
            placementOverlayHasContact = false;
            placementOverlayClearFrames = 0;
            placementOverlayPullFrames = 0;
            placementOverlayHasPullDirection = false;
            ResetPlacementOverlayReleaseTiming();
            handlePullActive = false;
            rotationPullActive = false;
            arrangePullActive = false;
            arrangeGridPullActive = false;
            arrangeRotationPullActive = true;
            arrangeRandomSpinActive = false;
            arrangeEvenSpacingActive = false;
            arrangeEvenSpacingKey = KeyCode.None;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
            arrangeRotationLastInputTime = EditorApplication.timeSinceStartup;
        }

        private static void StartArrangeUprightRotationPull(Transform[] roots)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            roots = roots.Where(root => root != null).ToArray();
            if (roots.Length == 0)
            {
                return;
            }

            if (ShouldSuppressDuplicatePhysics(roots))
            {
                return;
            }

            if (fallActive
                || handlePullActive
                || rotationPullActive
                || placementOverlayPullActive
                || arrangePullActive
                || (physXSessionActive && (!PhysXSessionMatchesRoots(roots) || physXSessionProfile != PhysicsModeProfile.ArrangeRotate)))
            {
                StopContinuousModes(false);
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms Arrange Upright";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.ArrangeRotate);
                hasSmoothedPullTarget = false;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            fallActive = false;
            placementOverlayPullActive = false;
            placementOverlayHasContact = false;
            placementOverlayClearFrames = 0;
            placementOverlayPullFrames = 0;
            placementOverlayHasPullDirection = false;
            ResetPlacementOverlayReleaseTiming();
            handlePullActive = false;
            rotationPullActive = false;
            rotationUprightPullActive = false;
            arrangePullActive = false;
            arrangeGridPullActive = false;
            arrangeRotationPullActive = true;
            arrangeRandomSpinActive = false;
            arrangeEvenSpacingActive = false;
            arrangeEvenSpacingKey = KeyCode.None;
            arrangeUprightPullActive = true;
            hasMovePullTarget = false;
            hasPullTarget = false;
            ArrangePhysicsRotationTargets.Clear();
            foreach (var root in roots)
            {
                if (root != null)
                {
                    ArrangePhysicsRotationTargets[root] = Quaternion.identity;
                }
            }

            arrangeHandleRotation = Quaternion.identity;
            arrangeLastRawHandleRotation = Quaternion.identity;
            arrangeRotationLastInputTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        private static bool TryHandleRandomSpinShortcut(Event evt)
        {
            if (!TryGetShortcutEvent(evt, out var eventKey, out var pressed, out var released)
                || !PhysicsAssistMasterEnabled
                || !IsConfiguredKey(GetSettings().rotateToolKey, eventKey)
                || evt.alt
                || evt.control
                || evt.command)
            {
                return false;
            }

            if (released && arrangeRandomSpinActive)
            {
                StopContinuousModes(false);
                ResetArrangeState();
                ResetHandleToSelection();
                RefreshToolVisibility();
                SceneView.RepaintAll();
                evt.Use();
                return true;
            }

            if (released && SelectionMatchesDuplicatePhysicsSuppression(GetLiveSelectionRoots()))
            {
                ClearDuplicatePhysicsSuppression();
                evt.Use();
                return true;
            }

            if (rightMouseButtonDown)
            {
                return false;
            }

            if (Mode != RealPhysicsPlacerMode.Rotation)
            {
                return false;
            }

            if (pressed)
            {
                if (ShouldSuppressDuplicatePhysics(GetLiveSelectionRoots()))
                {
                    evt.Use();
                    return true;
                }

                if (!arrangeRandomSpinActive)
                {
                    StartArrangeRandomSpin(GetLiveSelectionRoots());
                }

                evt.Use();
                return true;
            }

            return false;
        }

        private static bool TryHandleArrangeEvenSpacingShortcut(Event evt)
        {
            if (!TryGetShortcutEvent(evt, out var eventKey, out var pressed, out var released)
                || !PhysicsAssistMasterEnabled
                || evt.alt
                || evt.control
                || evt.command)
            {
                return false;
            }

            var activeSettings = GetSettings();
            var isPrimaryAction = IsConfiguredKey(activeSettings.actionKey, eventKey);
            var isToolAction = IsConfiguredKey(activeSettings.arrangeToolKey, eventKey);
            if (!isPrimaryAction && !isToolAction)
            {
                return false;
            }

            if (released)
            {
                ArrangeActionHeldKeys.Remove(eventKey);
                if (arrangeEvenSpacingActive && eventKey == arrangeEvenSpacingKey)
                {
                    StopContinuousModes(false);
                    ResetArrangeState();
                    ResetHandleToSelection();
                    RefreshToolVisibility();
                    SceneView.RepaintAll();
                    evt.Use();
                    return true;
                }

                if (SelectionMatchesDuplicatePhysicsSuppression(GetLiveSelectionRoots()))
                {
                    ClearDuplicatePhysicsSuppression();
                    evt.Use();
                    return true;
                }

                return false;
            }

            if (rightMouseButtonDown)
            {
                return false;
            }

            if (Mode != RealPhysicsPlacerMode.Alignment)
            {
                return false;
            }

            if (!pressed)
            {
                return false;
            }

            if (ShouldSuppressDuplicatePhysics(GetLiveSelectionRoots()))
            {
                evt.Use();
                return true;
            }

            var action = isPrimaryAction
                ? activeSettings.arrange.primaryAction
                : activeSettings.arrange.toolAction;
            if (action == RealPhysicsPlacerArrangeAction.None)
            {
                return false;
            }

            if (ArrangeActionHeldKeys.Contains(eventKey))
            {
                evt.Use();
                return true;
            }

            if (!StartArrangeAction(GetLiveSelectionRoots(), action, eventKey))
            {
                return false;
            }

            ArrangeActionHeldKeys.Add(eventKey);
            evt.Use();
            return true;
        }

        private static void StartArrangeRandomSpin(Transform[] roots)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            roots = roots.Where(root => root != null).ToArray();
            if (roots.Length == 0)
            {
                return;
            }

            if (fallActive
                || handlePullActive
                || rotationPullActive
                || placementOverlayPullActive
                || arrangePullActive
                || arrangeUprightPullActive
                || (physXSessionActive && (!PhysXSessionMatchesRoots(roots) || physXSessionProfile != PhysicsModeProfile.ArrangeRotate)))
            {
                StopContinuousModes(false);
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms Randomize Rotation";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.ArrangeRotate);
                hasSmoothedPullTarget = false;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            fallActive = false;
            placementOverlayPullActive = false;
            placementOverlayHasContact = false;
            placementOverlayClearFrames = 0;
            placementOverlayPullFrames = 0;
            placementOverlayHasPullDirection = false;
            ResetPlacementOverlayReleaseTiming();
            handlePullActive = false;
            rotationPullActive = false;
            rotationUprightPullActive = false;
            arrangePullActive = false;
            arrangeGridPullActive = false;
            arrangeRotationPullActive = true;
            arrangeRandomSpinActive = true;
            arrangeEvenSpacingActive = false;
            arrangeEvenSpacingKey = KeyCode.None;
            ArrangeActionHeldKeys.Clear();
            arrangeUprightPullActive = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
            arrangeRotationLastInputTime = EditorApplication.timeSinceStartup;
            ArrangePhysicsRotationTargets.Clear();
            ArrangeRandomSpinAngularVelocities.Clear();
            SeedArrangeRandomSpinVelocities(roots);
            SceneView.RepaintAll();
        }

        private static void SeedArrangeRandomSpinVelocities(Transform[] roots)
        {
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                ArrangeRandomSpinAngularVelocities[root] = new Vector3(
                    RandomSignedRange(25f, 95f),
                    RandomSignedRange(25f, 95f),
                    RandomSignedRange(25f, 95f)) * Mathf.Deg2Rad;
            }

            arrangeRotationLastInputTime = EditorApplication.timeSinceStartup;
        }

        private static bool StartArrangeAction(Transform[] roots, RealPhysicsPlacerArrangeAction action, KeyCode key)
        {
            if (!EditorPhysicsAllowed)
            {
                return false;
            }

            roots = roots.Where(root => root != null).ToArray();
            if (roots.Length < 2 || !TryBuildArrangeActionTargets(roots, action, out var targetPositions))
            {
                return false;
            }

            if (ShouldSuppressDuplicatePhysics(roots))
            {
                return false;
            }

            var gridAction = action == RealPhysicsPlacerArrangeAction.GridXZ;
            if (!gridAction && !GetSettings().arrange.collisionAware)
            {
                Undo.RecordObjects(roots.Cast<Object>().ToArray(), "RealTransforms Arrange Action");
                ApplyArrangeTargetPositions(targetPositions);
                ResetArrangeState();
                ResetHandleToSelection();
                SceneView.RepaintAll();
                return true;
            }

            StartArrangePull(roots, gridAction);
            arrangeEvenSpacingActive = true;
            arrangeEvenSpacingKey = key;
            ArrangePhysicsTargets.Clear();
            foreach (var pair in targetPositions)
            {
                if (pair.Key != null)
                {
                    ArrangePhysicsTargets[pair.Key] = pair.Value;
                }
            }

            ResetArrangeState();
            SceneView.RepaintAll();
            return true;
        }

        private static bool TryBuildArrangeActionTargets(
            Transform[] roots,
            RealPhysicsPlacerArrangeAction action,
            out Dictionary<Transform, Vector3> targetPositions)
        {
            switch (action)
            {
                case RealPhysicsPlacerArrangeAction.PackX:
                    return TryBuildPackedLineTargets(roots, Vector3.right, out targetPositions);
                case RealPhysicsPlacerArrangeAction.PackZ:
                    return TryBuildPackedLineTargets(roots, Vector3.forward, out targetPositions);
                case RealPhysicsPlacerArrangeAction.DistributeX:
                    return TryBuildEvenSpacingTargets(roots, Vector3.right, out targetPositions);
                case RealPhysicsPlacerArrangeAction.DistributeZ:
                    return TryBuildEvenSpacingTargets(roots, Vector3.forward, out targetPositions);
                case RealPhysicsPlacerArrangeAction.GridXZ:
                    return TryBuildGridTargets(roots, out targetPositions);
                case RealPhysicsPlacerArrangeAction.Clump:
                    return TryBuildScaledArrangeActionTargets(roots, new Vector3(0.68f, 1f, 0.68f), out targetPositions);
                case RealPhysicsPlacerArrangeAction.Spread:
                    return TryBuildScaledArrangeActionTargets(roots, new Vector3(1.35f, 1f, 1.35f), out targetPositions);
                default:
                    targetPositions = new Dictionary<Transform, Vector3>();
                    return false;
            }
        }

        private static bool TryBuildScaledArrangeActionTargets(
            Transform[] roots,
            Vector3 factor,
            out Dictionary<Transform, Vector3> targetPositions)
        {
            targetPositions = new Dictionary<Transform, Vector3>();
            if (roots.Length < 2 || !TryGetCombinedBounds(roots, out var bounds))
            {
                return false;
            }

            targetPositions = BuildArrangeTargetPositions(roots, bounds.center, factor, GetPhysicsTruthPoint, TryGetWorldBoundsOrDefault);
            return targetPositions.Count > 0;
        }

        private static bool TryBuildPackedLineTargets(Transform[] roots, Vector3 axis, out Dictionary<Transform, Vector3> targetPositions)
        {
            targetPositions = new Dictionary<Transform, Vector3>();
            axis = axis.sqrMagnitude > Epsilon ? axis.normalized : Vector3.right;
            if (roots.Length < 2 || !TryGetCombinedBounds(roots, out var combinedBounds))
            {
                return false;
            }

            var items = BuildArrangeAxisItems(roots, axis);
            if (items.Count < 2)
            {
                return false;
            }

            var totalSize = items.Sum(item => item.Size);
            var gap = Mathf.Max(0f, GetSettings().arrange.padding);
            var arrangedSpan = totalSize + gap * (items.Count - 1);
            var cursor = Vector3.Dot(combinedBounds.center, axis) - arrangedSpan * 0.5f;
            foreach (var item in items)
            {
                var targetCenter = cursor + item.Size * 0.5f;
                targetPositions[item.Root] = GetPhysicsTruthPoint(item.Root) + axis * (targetCenter - item.Center);
                cursor += item.Size + gap;
            }

            return targetPositions.Count > 0;
        }

        private static List<ArrangeAxisItem> BuildArrangeAxisItems(Transform[] roots, Vector3 axis)
        {
            var items = new List<ArrangeAxisItem>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var bounds = TryGetWorldBoundsOrDefault(root);
                var min = ProjectMin(bounds, axis);
                var max = ProjectMax(bounds, axis);
                var size = Mathf.Max(0.001f, max - min);
                items.Add(new ArrangeAxisItem(root, min, max, size));
            }

            items.Sort((left, right) =>
            {
                var centerCompare = left.Center.CompareTo(right.Center);
                return centerCompare != 0
                    ? centerCompare
                    : left.Root.GetEntityId().CompareTo(right.Root.GetEntityId());
            });
            return items;
        }

        private static bool TryBuildGridTargets(Transform[] roots, out Dictionary<Transform, Vector3> targetPositions)
        {
            targetPositions = new Dictionary<Transform, Vector3>();
            if (roots.Length < 2 || !TryGetCombinedBounds(roots, out var combinedBounds))
            {
                return false;
            }

            var items = new List<ArrangeGridItem>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var bounds = TryGetWorldBoundsOrDefault(root);
                items.Add(new ArrangeGridItem(root, bounds));
            }

            if (items.Count < 2)
            {
                return false;
            }

            items.Sort((left, right) =>
            {
                var zCompare = right.Bounds.center.z.CompareTo(left.Bounds.center.z);
                return zCompare != 0
                    ? zCompare
                    : left.Bounds.center.x.CompareTo(right.Bounds.center.x);
            });

            var columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(items.Count)), 1, items.Count);
            var rows = Mathf.CeilToInt(items.Count / (float)columns);
            var padding = Mathf.Max(0f, GetSettings().arrange.padding);
            var columnWidths = new float[columns];
            var rowDepths = new float[rows];
            for (var i = 0; i < items.Count; i++)
            {
                var row = i / columns;
                var column = i % columns;
                columnWidths[column] = Mathf.Max(columnWidths[column], Mathf.Max(0.001f, items[i].Bounds.size.x));
                rowDepths[row] = Mathf.Max(rowDepths[row], Mathf.Max(0.001f, items[i].Bounds.size.z));
            }

            var totalWidth = columnWidths.Sum() + padding * Mathf.Max(0, columns - 1);
            var totalDepth = rowDepths.Sum() + padding * Mathf.Max(0, rows - 1);
            var startX = combinedBounds.center.x - totalWidth * 0.5f;
            var startZ = combinedBounds.center.z + totalDepth * 0.5f;
            var columnCenters = new float[columns];
            var rowCenters = new float[rows];
            var cursorX = startX;
            for (var column = 0; column < columns; column++)
            {
                columnCenters[column] = cursorX + columnWidths[column] * 0.5f;
                cursorX += columnWidths[column] + padding;
            }

            var cursorZ = startZ;
            for (var row = 0; row < rows; row++)
            {
                rowCenters[row] = cursorZ - rowDepths[row] * 0.5f;
                cursorZ -= rowDepths[row] + padding;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var targetCenter = new Vector3(
                    columnCenters[column],
                    items[i].Bounds.center.y,
                    rowCenters[row]);
                var offset = targetCenter - items[i].Bounds.center;
                offset.y = 0f;
                targetPositions[items[i].Root] = GetPhysicsTruthPoint(items[i].Root) + offset;
            }

            return targetPositions.Count > 0;
        }

        private static bool TryBuildEvenSpacingTargets(Transform[] roots, Vector3 axis, out Dictionary<Transform, Vector3> targetPositions)
        {
            targetPositions = new Dictionary<Transform, Vector3>();
            axis = axis.sqrMagnitude > Epsilon ? axis.normalized : Vector3.right;
            var items = BuildArrangeAxisItems(roots, axis);
            if (items.Count < 2)
            {
                return false;
            }

            var originalMin = items.Min(item => item.Min);
            var originalMax = items.Max(item => item.Max);
            var originalSpan = Mathf.Max(0.001f, originalMax - originalMin);
            var totalSize = items.Sum(item => item.Size);
            var minimumGap = Mathf.Max(0f, GetSettings().arrange.padding);
            var gap = Mathf.Max(minimumGap, (originalSpan - totalSize) / Mathf.Max(1, items.Count - 1));
            var arrangedSpan = totalSize + gap * (items.Count - 1);
            var cursor = (originalMin + originalMax) * 0.5f - arrangedSpan * 0.5f;

            foreach (var item in items)
            {
                var targetCenter = cursor + item.Size * 0.5f;
                targetPositions[item.Root] = GetPhysicsTruthPoint(item.Root) + axis * (targetCenter - item.Center);
                cursor += item.Size + gap;
            }

            return targetPositions.Count > 0;
        }

        private static void ApplyArrangeTargetPositions(Dictionary<Transform, Vector3> targetPositions)
        {
            foreach (var pair in targetPositions)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.position = GetRootPositionForTruthTarget(pair.Key, pair.Value);
                EditorUtility.SetDirty(pair.Key);
            }

            Physics.SyncTransforms();
        }

        private static float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)ArrangeRandom.NextDouble());
        }

        private static float RandomSignedRange(float min, float max)
        {
            return RandomRange(min, max) * (ArrangeRandom.NextDouble() < 0.5d ? -1f : 1f);
        }

        private static Vector3 GetArrangePhysicsTarget(PhysXBodyState body)
        {
            if (body != null
                && body.Root != null
                && ArrangePhysicsTargets.TryGetValue(body.Root, out var target))
            {
                return GetRootPositionForTruthTarget(body.Root, target);
            }

            return body != null && body.Body != null
                ? body.Body.position
                : Vector3.zero;
        }

        private static void ResetArrangePhysicsTargets()
        {
            ArrangePhysicsTargets.Clear();
            arrangeEvenSpacingActive = false;
            arrangeEvenSpacingKey = KeyCode.None;
            ArrangeActionHeldKeys.Clear();
            arrangeGridPullActive = false;
        }

        private static void ResetArrangePhysicsRotationTargets()
        {
            ArrangePhysicsRotationTargets.Clear();
            ArrangeRandomSpinAngularVelocities.Clear();
            arrangeRandomSpinActive = false;
        }

        private static bool TryGetArrangePhysicsRotationTarget(Transform root, out Quaternion target)
        {
            return ArrangePhysicsRotationTargets.TryGetValue(root, out target);
        }

        private static bool TryGetArrangeRandomSpinAngularVelocity(Transform root, out Vector3 angularVelocity)
        {
            return ArrangeRandomSpinAngularVelocities.TryGetValue(root, out angularVelocity);
        }

        private static void FinishArrangeDrag()
        {
            if (!arrangePullActive && !arrangeRotationPullActive && !arrangeDragActive && !arrangeHasAxisLock)
            {
                return;
            }

            if (arrangePullActive || arrangeRotationPullActive)
            {
                StopContinuousModes(false);
            }
            else
            {
                ResetArrangePhysicsTargets();
                ResetArrangePhysicsRotationTargets();
            }

            ResetArrangeState();
            ResetHandleToSelection();
            RefreshToolVisibility();
            SceneView.RepaintAll();
        }

        private static int ChangedArrangeAxisCount(Vector3 factor)
        {
            var count = 0;
            if (Mathf.Abs(factor.x - 1f) > 0.001f)
            {
                count++;
            }

            if (Mathf.Abs(factor.y - 1f) > 0.001f)
            {
                count++;
            }

            if (Mathf.Abs(factor.z - 1f) > 0.001f)
            {
                count++;
            }

            return count;
        }

        private static void ApplyArrangeAxisDisplacement(
            Transform[] roots,
            Vector3 center,
            Vector3 axis,
            float factor,
            Func<Transform, Vector3> sourcePosition,
            Dictionary<Transform, Vector3> targetPositions)
        {
            if (Mathf.Abs(factor - 1f) <= 0.001f)
            {
                return;
            }

            var centerOnAxis = Vector3.Dot(center, axis);
            foreach (var root in roots)
            {
                if (root == null || !targetPositions.ContainsKey(root))
                {
                    continue;
                }

                var sourceOnAxis = Vector3.Dot(sourcePosition(root), axis);
                var targetOnAxis = centerOnAxis + (sourceOnAxis - centerOnAxis) * factor;
                var currentTargetOnAxis = Vector3.Dot(targetPositions[root], axis);
                targetPositions[root] += axis * (targetOnAxis - currentTargetOnAxis);
            }
        }

        private static void ApplyArrangeAxisDistribution(
            Transform[] roots,
            Vector3 center,
            Vector3 axis,
            float factor,
            Func<Transform, Bounds> sourceBounds,
            Dictionary<Transform, Vector3> targetPositions)
        {
            if (Mathf.Abs(factor - 1f) <= 0.001f)
            {
                return;
            }

            var items = new List<ArrangeAxisItem>();
            foreach (var root in roots)
            {
                if (root == null || !targetPositions.ContainsKey(root))
                {
                    continue;
                }

                var bounds = sourceBounds(root);
                var min = ProjectMin(bounds, axis);
                var max = ProjectMax(bounds, axis);
                var size = Mathf.Max(0.001f, max - min);
                items.Add(new ArrangeAxisItem(root, min, max, size));
            }

            if (items.Count < 2)
            {
                return;
            }

            items.Sort((left, right) => left.Center.CompareTo(right.Center));
            var originalMin = items.Min(item => item.Min);
            var originalMax = items.Max(item => item.Max);
            var originalSpan = Mathf.Max(0.001f, originalMax - originalMin);
            var totalSize = items.Sum(item => item.Size);
            var minimumGap = Mathf.Max(0f, GetSettings().arrange.padding);
            var minimumPackedSpan = totalSize + minimumGap * (items.Count - 1);
            var physicsCompression = GetSettings().arrange.collisionAware && factor < 1f;
            var targetSpan = factor < 1f
                ? physicsCompression
                    ? originalSpan * Mathf.Clamp01(factor)
                    : Mathf.Clamp(originalSpan * Mathf.Clamp01(factor), Mathf.Min(originalSpan, minimumPackedSpan), originalSpan)
                : Mathf.Max(originalSpan, originalSpan * factor, minimumPackedSpan);
            var targetGap = 0f;
            if (items.Count > 1)
            {
                var rawGap = (targetSpan - totalSize) / (items.Count - 1);
                var maximumOverlap = physicsCompression
                    ? Mathf.Lerp(0.98f, 0.999f, Mathf.Clamp01(-factor))
                    : 0f;
                targetGap = physicsCompression
                    ? Mathf.Max(-items.Min(item => item.Size) * maximumOverlap, rawGap)
                    : Mathf.Max(minimumGap, rawGap);
            }

            var arrangedSpan = totalSize + targetGap * (items.Count - 1);
            var cursor = Vector3.Dot(center, axis) - arrangedSpan * 0.5f;
            foreach (var item in items)
            {
                var targetCenter = cursor + item.Size * 0.5f;
                var currentTargetCenter = Vector3.Dot(targetPositions[item.Root], axis);
                targetPositions[item.Root] += axis * (targetCenter - currentTargetCenter);
                cursor += item.Size + targetGap;
            }
        }

        private static Vector3 GetArrangeAnchorPosition(Transform root)
        {
            return root != null && ArrangeAnchorPositions.TryGetValue(root, out var position)
                ? position
                : root != null
                    ? GetPhysicsTruthPoint(root)
                    : Vector3.zero;
        }

        private static Bounds GetArrangeAnchorBounds(Transform root)
        {
            if (root != null && ArrangeAnchorBounds.TryGetValue(root, out var bounds))
            {
                return bounds;
            }

            return TryGetWorldBoundsOrDefault(root);
        }

        private static Bounds TryGetWorldBoundsOrDefault(Transform root)
        {
            return root != null && TryGetWorldBounds(root, out var bounds)
                ? bounds
                : new Bounds(root != null ? root.position : Vector3.zero, Vector3.one * 0.25f);
        }

        private static void ResetArrangeState()
        {
            arrangeHandleScale = Vector3.one;
            arrangeLinearFactor = Vector3.one;
            arrangeLastRawHandleScale = Vector3.one;
            arrangeAxisLock = Vector3.one;
            arrangeHasAxisLock = false;
            ArrangeAnchorPositions.Clear();
            ArrangeAnchorBounds.Clear();
            ArrangeAnchorRotations.Clear();
            arrangeAnchorRoots = Array.Empty<Transform>();
            arrangeAnchorBounds = default;
            arrangeAnchorCenter = Vector3.zero;
            arrangeHandleRotation = Quaternion.identity;
            arrangeLastRawHandleRotation = Quaternion.identity;
            arrangeDragActive = false;
        }

        private readonly struct ArrangeMove
        {
            internal readonly Transform Root;
            internal readonly Vector3 Delta;
            internal readonly float Distance;
            internal readonly float AnchorDistance;

            internal ArrangeMove(Transform root, Vector3 delta, float anchorDistance)
            {
                Root = root;
                Delta = delta;
                Distance = delta.sqrMagnitude;
                AnchorDistance = anchorDistance;
            }
        }

        private readonly struct ArrangeAxisItem
        {
            internal readonly Transform Root;
            internal readonly float Min;
            internal readonly float Max;
            internal readonly float Size;
            internal float Center => (Min + Max) * 0.5f;

            internal ArrangeAxisItem(Transform root, float min, float max, float size)
            {
                Root = root;
                Min = min;
                Max = max;
                Size = size;
            }
        }

        private readonly struct ArrangeGridItem
        {
            internal readonly Transform Root;
            internal readonly Bounds Bounds;

            internal ArrangeGridItem(Transform root, Bounds bounds)
            {
                Root = root;
                Bounds = bounds;
            }
        }
    }
}
