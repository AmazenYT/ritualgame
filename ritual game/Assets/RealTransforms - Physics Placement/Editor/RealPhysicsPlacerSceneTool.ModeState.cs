﻿using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Starts, stops, and switches continuous modes. Keeping these transitions
        // together prevents overlapping physics sessions from fighting each other.
        private static bool handlingUndoRedo;
        private static bool duplicateCommandAwaitingSelectionUndoFence;

        private static void StartDropMode()
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            var roots = GetLiveSelectionRoots();
            if (roots.Length == 0 || fallActive)
            {
                return;
            }

            if (ShouldSuppressDuplicatePhysics(roots))
            {
                return;
            }

            if (handlePullActive
                || rotationPullActive
                || placementOverlayPullActive
                || arrangePullActive
                || arrangeRotationPullActive
                || arrangeRandomSpinActive)
            {
                StopContinuousModes(false);
            }

            const string undoName = "RealTransforms Drop";
            BeginContinuousUndoGroup(roots, undoName);

            fallActive = true;
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
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            hasSmoothedPullTarget = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
            ResetPlacementOverlayTracking();
            lastUpdateTime = EditorApplication.timeSinceStartup;
            BeginPhysXSession(roots, undoName, PhysicsModeProfile.Drop);
        }

        private static void StopContinuousModes(bool finalizePlacement = true, bool collapseUndoGroup = true)
        {
            if (finalizePlacement && physXSessionActive && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (physXSessionProfile == PhysicsModeProfile.Drop)
                {
                    FinalizePhysXPlacement();
                }
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
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            hasSmoothedPullTarget = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
            ResetPlacementOverlayTracking();
            ResetRotationPhysicsTargets();
            ResetArrangePhysicsTargets();
            ResetArrangePhysicsRotationTargets();
            EndPhysXSession();
            if (continuousUndoGroup >= 0)
            {
                if (collapseUndoGroup)
                {
                    Undo.CollapseUndoOperations(continuousUndoGroup);
                }

                continuousUndoGroup = -1;
            }
        }

        private static void StopContinuousModesForDuplicateSelection()
        {
            // Selection changes arrive after Unity has created duplicates, so avoid
            // collapsing the duplicate's undo group into an active placer action.
            var needsUndoFence = duplicateCommandAwaitingSelectionUndoFence || HasDuplicateSensitiveUndoState();
            duplicateCommandAwaitingSelectionUndoFence = false;
            StopContinuousModes(false, false);
            if (needsUndoFence)
            {
                Undo.IncrementCurrentGroup();
            }
        }

        private static void ClearDuplicateCommandUndoFence()
        {
            duplicateCommandAwaitingSelectionUndoFence = false;
        }

        private static void StartHandlePull(Transform[] roots, Vector3 targetPosition)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms PhysX Move";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.Move);
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
            handlePullActive = true;
            rotationPullActive = false;
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            movePullTarget = targetPosition;
            hasMovePullTarget = true;
            pullTarget = targetPosition;
            hasPullTarget = true;
            SceneView.RepaintAll();
        }

        private static void StartRotationPull(Transform[] roots, Quaternion targetRotation)
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
                || placementOverlayPullActive
                || arrangePullActive
                || arrangeRotationPullActive
                || (physXSessionActive && (!PhysXSessionMatchesRoots(roots) || physXSessionProfile != PhysicsModeProfile.Rotate)))
            {
                StopContinuousModes(false);
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms PhysX Rotate";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.Rotate);
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
            arrangePullActive = false;
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            rotationPullActive = true;
            hasMovePullTarget = false;
            hasPullTarget = false;
            ResetRotationPhysicsTargets();
            rotationPullTarget = targetRotation;
            rotationLastInputTime = EditorApplication.timeSinceStartup;
        }

        private static bool TryStartUprightRotationPull()
        {
            if (!EditorPhysicsAllowed)
            {
                return false;
            }

            var roots = GetLiveSelectionRoots();
            if (roots.Length == 0)
            {
                return false;
            }

            if (ShouldSuppressDuplicatePhysics(roots))
            {
                return false;
            }

            if (Mode == RealPhysicsPlacerMode.Rotation)
            {
                StartRotationUprightPull(roots);
                return true;
            }

            if (Mode == RealPhysicsPlacerMode.Alignment)
            {
                StartArrangeUprightRotationPull(roots);
                return true;
            }

            return false;
        }

        private static void StartRotationUprightPull(Transform[] roots)
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
                || placementOverlayPullActive
                || arrangePullActive
                || arrangeRotationPullActive
                || (physXSessionActive && (!PhysXSessionMatchesRoots(roots) || physXSessionProfile != PhysicsModeProfile.Rotate)))
            {
                StopContinuousModes(false);
            }

            if (!physXSessionActive)
            {
                const string undoName = "RealTransforms Upright Rotate";
                BeginContinuousUndoGroup(roots, undoName);
                BeginPhysXSession(roots, undoName, PhysicsModeProfile.Rotate);
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
            arrangePullActive = false;
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            arrangeUprightPullActive = false;
            rotationPullActive = true;
            rotationUprightPullActive = true;
            hasMovePullTarget = false;
            hasPullTarget = false;
            rotationPullTarget = Quaternion.identity;
            handleRotation = Quaternion.identity;
            RotationPhysicsTargets.Clear();
            foreach (var root in roots)
            {
                if (root != null)
                {
                    RotationPhysicsTargets[root] = Quaternion.identity;
                }
            }

            rotationLastInputTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        private static void ResetRotationPhysicsTargets()
        {
            RotationPhysicsTargets.Clear();
        }

        private static bool TryGetRotationPhysicsTarget(Transform root, out Quaternion target)
        {
            return RotationPhysicsTargets.TryGetValue(root, out target);
        }

        private static void BeginContinuousUndoGroup(Transform[] roots, string undoName)
        {
            roots = roots
                .Where(root => root != null)
                .ToArray();
            if (roots.Length == 0)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            continuousUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            var objects = roots.Cast<Object>().ToArray();
            Undo.RegisterCompleteObjectUndo(objects, undoName);
            Undo.FlushUndoRecordObjects();
        }

        private static void PrepareForUnityDuplicateCommand(Event evt)
        {
            if ((evt.type != EventType.ValidateCommand && evt.type != EventType.ExecuteCommand)
                || evt.commandName != "Duplicate"
                || Selection.transforms.Length == 0
                || !HasDuplicateSensitiveUndoState())
            {
                return;
            }

            // Command events arrive before Unity performs Duplicate, letting the
            // active placer action finish as its own undo item first.
            StopContinuousModes(false);
            Undo.IncrementCurrentGroup();
            duplicateCommandAwaitingSelectionUndoFence = true;
        }

        private static bool HasDuplicateSensitiveUndoState()
        {
            return continuousUndoGroup >= 0
                || physXSessionActive
                || HasActiveContinuousMode()
                || PhysXBodies.Count > 0
                || hasPreviousSimulationMode;
        }

        private static void PrepareForUnityUndoRedoShortcut(Event evt)
        {
            if (evt.type != EventType.KeyDown
                || (!evt.control && !evt.command)
                || evt.alt
                || (evt.keyCode != KeyCode.Z && evt.keyCode != KeyCode.Y))
            {
                return;
            }

            if (physXSessionActive || fallActive || handlePullActive || rotationPullActive || placementOverlayPullActive || arrangePullActive || arrangeRotationPullActive || arrangeRandomSpinActive)
            {
                AbortContinuousModesForUndoRedo();
            }
        }

        private static void OnUndoRedoPerformed()
        {
            if (handlingUndoRedo)
            {
                return;
            }

            handlingUndoRedo = true;
            try
            {
                AbortContinuousModesForUndoRedo();
                RebuildSelection();
                MarkSceneProxyScanDirty();
                SceneView.RepaintAll();
            }
            finally
            {
                handlingUndoRedo = false;
            }
        }

        private static void AbortContinuousModesForUndoRedo()
        {
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
            arrangeRotationPullActive = false;
            arrangeRandomSpinActive = false;
            rotationUprightPullActive = false;
            arrangeUprightPullActive = false;
            hasSmoothedPullTarget = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
            ResetPlacementOverlayTracking();
            ResetRotationPhysicsTargets();
            ResetArrangePhysicsTargets();
            ResetArrangePhysicsRotationTargets();
            continuousUndoGroup = -1;

            RestoreIgnoredSelectedCollisions();
            for (var i = PhysXBodies.Count - 1; i >= 0; i--)
            {
                PhysXBodies[i].Restore();
            }

            PhysXBodies.Clear();
            physXSessionActive = false;
            RestoreFrozenSceneBodies();

            if (hasPreviousSimulationMode)
            {
                Physics.simulationMode = previousSimulationMode;
                hasPreviousSimulationMode = false;
            }

            EditorPrefs.SetBool(ActivePhysicsSessionKey, false);
            CleanupStaleTemporaryPhysicsComponents(true);
            ClearSelectionProxyColliderPool();
            CleanupOrphanedSelectionProxyColliders();
            ClearSceneProxies(true);
            Physics.SyncTransforms();
        }
    }
}
