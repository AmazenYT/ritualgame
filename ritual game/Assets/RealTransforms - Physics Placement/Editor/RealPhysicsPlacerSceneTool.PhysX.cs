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
        private static void BeginPhysXSession(Transform[] roots, string undoName, PhysicsModeProfile profile)
        {
            // A session is one continuous editor interaction. Selected objects get
            // temporary Rigidbody/collider setup, then the simulated result is
            // written back into normal transforms before all temporary state is restored.
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            EndPhysXSession();
            CleanupStaleTemporaryPhysicsComponents();
            suppressSceneProxyHierarchyDirty = true;

            try
            {
                var activeSettings = GetSettings();
                physXSessionProfile = profile;
                var center = TryGetCombinedBounds(roots, out var bounds) ? bounds.center : AveragePosition(roots);
                if (TryBeginIsolatedPhysicsSession(roots, activeSettings, center, profile))
                {
                    EditorPrefs.SetBool(ActivePhysicsSessionKey, true);
                    Physics.SyncTransforms();
                    physXSessionActive = PhysXBodies.Count > 0;
                    return;
                }

                if (!hasPreviousSimulationMode)
                {
                    previousSimulationMode = Physics.simulationMode;
                    hasPreviousSimulationMode = true;
                }

                EditorPrefs.SetBool(ActivePhysicsSessionKey, true);
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();

                foreach (var root in roots)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    // Capture every user-authored physics value before editing it
                    // so temporary editor changes never leak into the real scene.
                    RemoveCopiedTemporaryPhysicsComponents(root);
                    var state = new PhysXBodyState(root, center, handleRotation);
                    state.CaptureMeshColliderState();
                    state.EnsureSelectionCollider(activeSettings);
                    state.Body = root.GetComponent<Rigidbody>();
                    state.CreatedBody = state.Body == null;
                    if (state.CreatedBody)
                    {
                        state.Body = root.gameObject.AddComponent<Rigidbody>();
                        state.Body.hideFlags = HideFlags.HideAndDontSave;
                    }

                    state.CaptureBodyState();
                    state.ConfigureForGrab(activeSettings, profile);
                    PhysXBodies.Add(state);
                }

                if (activeSettings.freezeSceneRigidbodiesDuringAssist)
                {
                    FreezeNonSelectedRigidbodies(roots);
                }
                if (ShouldIgnoreSelectedBodyCollisions(profile))
                {
                    IgnoreSelectedBodyCollisions();
                }

                IgnoreShadowedSceneCollisions(roots);
                Physics.SyncTransforms();
                physXSessionActive = PhysXBodies.Count > 0;
            }
            finally
            {
                suppressSceneProxyHierarchyDirtyUntil = EditorApplication.timeSinceStartup + 0.25d;
                suppressSceneProxyHierarchyDirty = false;
            }
        }

        private static void EndPhysXSession()
        {
            RestoreIgnoredSelectedCollisions();

            for (var i = PhysXBodies.Count - 1; i >= 0; i--)
            {
                PhysXBodies[i].Restore();
            }

            PhysXBodies.Clear();
            physXSessionActive = false;
            CleanupIsolatedPhysicsScene();
            RestoreFrozenSceneBodies();
            CleanupStaleTemporaryPhysicsComponents();

            if (hasPreviousSimulationMode)
            {
                Physics.simulationMode = previousSimulationMode;
                hasPreviousSimulationMode = false;
            }

            EditorPrefs.SetBool(ActivePhysicsSessionKey, false);
            Physics.SyncTransforms();
        }

        private static void StepPhysX(float deltaTime, bool pullToTarget, Transform[] rootsOverride = null)
        {
            if (!EditorPhysicsAllowed)
            {
                StopContinuousModes(false);
                return;
            }

            var roots = rootsOverride ?? GetLiveSelectionRoots();
            if (roots.Length == 0)
            {
                StopContinuousModes();
                return;
            }

            if (!fallActive
                && !handlePullActive
                && !rotationPullActive
                && !placementOverlayPullActive
                && !arrangePullActive
                && !arrangeRotationPullActive
                && !arrangeRandomSpinActive)
            {
                return;
            }

            RemoveCopiedPhysicsFromActiveSessionDuplicatesBeforeSimulation(roots);
            if (physXSessionActive && !PhysXSessionMatchesRoots(roots))
            {
                StopContinuousModes(false);
                return;
            }

            if (!physXSessionActive)
            {
                var profile = GetActivePhysicsProfile(pullToTarget);
                BeginPhysXSession(roots, GetPhysXSessionName(profile), profile);
            }

            var activeSettings = GetSettings();
            var activeProfile = GetActivePhysicsProfile(pullToTarget);
            var simulatedDeltaTime = deltaTime * Mathf.Max(0.01f, GetActiveSimulationSpeed(activeSettings));
            simulatedDeltaTime = Mathf.Min(simulatedDeltaTime, GetMaxSimulatedDeltaTime(activeProfile));
            var rotationDriveActive = activeProfile == PhysicsModeProfile.Rotate || activeProfile == PhysicsModeProfile.ArrangeRotate;

            if (pullToTarget && !arrangePullActive && !rotationDriveActive)
            {
                UpdateSmoothedPhysXTarget(simulatedDeltaTime);
            }

            var subStepSize = Mathf.Max(0.001f, GetActivePhysicsStep(activeSettings, activeProfile));
            var maxSubSteps = Mathf.Max(1, GetActiveMaxSubsteps(activeSettings, activeProfile));
            var subSteps = Mathf.Clamp(Mathf.CeilToInt(simulatedDeltaTime / subStepSize), 1, maxSubSteps);
            var step = simulatedDeltaTime / subSteps;
            // Substeps cap the amount of physics work per editor update while
            // keeping contact response stable during slow frames.
            for (var i = 0; i < subSteps; i++)
            {
                foreach (var body in PhysXBodies)
                {
                    if (body.Body == null)
                    {
                        continue;
                    }

                    if (rotationDriveActive)
                    {
                        if (arrangeRandomSpinActive)
                        {
                            ApplyArrangeRandomSpinDrive(body, activeSettings, step);
                            ApplyActiveRotationConstraint(body.Body, activeSettings, activeProfile);
                            continue;
                        }

                        ApplyRotationDrive(body, activeSettings, activeProfile, step);
                        ApplyActiveRotationConstraint(body.Body, activeSettings, activeProfile);
                        continue;
                    }

                    var acceleration = activeSettings.drop.gravity;
                    if (pullToTarget)
                    {
                        var target = arrangePullActive
                            ? GetArrangePhysicsTarget(body)
                            : smoothedPullTarget + body.TargetOffset;
                        var toTarget = target - body.Body.position;
                        var springStrength = GetActiveMoveSpring(activeSettings);
                        var dampingStrength = GetActiveMoveDamping(activeSettings);
                        var spring = toTarget * springStrength;
                        var damping = -body.Body.linearVelocity * dampingStrength;
                        acceleration = spring + damping;
                    }

                    body.Body.AddForce(acceleration, ForceMode.Acceleration);
                    if (pullToTarget)
                    {
                        body.Body.linearVelocity = Vector3.ClampMagnitude(body.Body.linearVelocity, GetActiveMoveSpeed(activeSettings));
                    }
                    else
                    {
                        body.Body.linearVelocity = Vector3.ClampMagnitude(body.Body.linearVelocity, GetActiveMoveSpeed(activeSettings));
                    }

                    if (arrangePullActive)
                    {
                        body.Body.angularVelocity = Vector3.zero;
                    }
                    else if (pullToTarget)
                    {
                        var angularDamping = IsMovePullActive()
                            ? activeSettings.move.angularDamping
                            : activeSettings.move.angularDamping * 0.25f;
                        if (placementOverlayPullActive && !placementOverlayHasContact)
                        {
                            angularDamping *= 2f;
                        }

                        body.Body.angularVelocity *= Mathf.Exp(-angularDamping * step);
                    }

                    ApplyActiveRotationConstraint(body.Body, activeSettings, activeProfile);
                }

                SimulateActivePhysicsScene(step);
            }

            foreach (var body in PhysXBodies)
            {
                if (body.Root != null)
                {
                    EditorUtility.SetDirty(body.Root);
                }
            }
        }

        private static string GetPhysXSessionName(PhysicsModeProfile profile)
        {
            switch (profile)
            {
                case PhysicsModeProfile.Drop:
                    return "RealTransforms Drop";
                case PhysicsModeProfile.Rotate:
                    return "RealTransforms PhysX Rotate";
                case PhysicsModeProfile.Arrange:
                    return "RealTransforms Arrange";
                case PhysicsModeProfile.ArrangeRotate:
                    return "RealTransforms Arrange Rotate";
                default:
                    return "RealTransforms PhysX Move";
            }
        }

        private static void ApplyArrangeRandomSpinDrive(PhysXBodyState body, RealPhysicsPlacerSettings activeSettings, float step)
        {
            if (body == null
                || body.Body == null
                || body.Root == null
                || !TryGetArrangeRandomSpinAngularVelocity(body.Root, out var localAngularVelocity))
            {
                return;
            }

            var targetAngularVelocity = body.Body.rotation * localAngularVelocity;
            var maxAngularSpeed = Mathf.Max(0.1f, activeSettings.rotate.maxAngularSpeed);
            targetAngularVelocity = Vector3.ClampMagnitude(targetAngularVelocity, maxAngularSpeed * 0.9f);

            var response = Mathf.Max(6f, activeSettings.rotate.angularSpring * 0.08f);
            var blend = 1f - Mathf.Exp(-response * step);
            body.Body.angularVelocity = Vector3.Lerp(body.Body.angularVelocity, targetAngularVelocity, blend);
            body.Body.angularVelocity = Vector3.ClampMagnitude(body.Body.angularVelocity, maxAngularSpeed);
            body.Body.linearVelocity *= Mathf.Exp(-Mathf.Max(0f, activeSettings.rotate.linearDamping + 2f) * step);
            body.Body.WakeUp();
        }

        private static void FinalizePhysXPlacement()
        {
            if (PhysXBodies.Count == 0)
            {
                return;
            }

            var activeSettings = GetSettings();
            var settleSteps = Mathf.Clamp(activeSettings.drop.settleSteps, 0, 64);
            var step = Mathf.Max(0.001f, activeSettings.drop.physicsStep);
            for (var i = 0; i < settleSteps; i++)
            {
                foreach (var body in PhysXBodies)
                {
                    if (body.Body == null)
                    {
                        continue;
                    }

                    body.Body.AddForce(activeSettings.drop.gravity, ForceMode.Acceleration);
                    body.Body.linearVelocity = Vector3.ClampMagnitude(body.Body.linearVelocity, activeSettings.drop.maxSpeed);
                    body.Body.angularVelocity = Vector3.ClampMagnitude(body.Body.angularVelocity, 50f);
                }

                SimulateActivePhysicsScene(step);
            }
        }

        private static void UpdateSmoothedPhysXTarget(float deltaTime)
        {
            if (!IsMovePullActive() && !hasPullTarget)
            {
                return;
            }

            var target = pullTarget;
            if (IsMovePullActive() && hasMovePullTarget)
            {
                movePullTarget = ClampMovePullTarget(handlePosition, movePullTarget);
                target = movePullTarget;
            }
            if (!hasSmoothedPullTarget)
            {
                smoothedPullTarget = target;
                hasSmoothedPullTarget = true;
                return;
            }

            var activeSettings = GetSettings();
            var blend = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.01f, GetActiveTargetSmoothTime(activeSettings)));
            smoothedPullTarget = Vector3.Lerp(smoothedPullTarget, target, blend);
        }

        private static Vector3 ClampMovePullTarget(Vector3 anchor, Vector3 target)
        {
            var activeSettings = GetSettings();
            var leash = Mathf.Max(0.05f, activeSettings.move.targetMaxDistance);
            var fromAnchor = target - anchor;
            return fromAnchor.sqrMagnitude > leash * leash
                ? anchor + fromAnchor.normalized * leash
                : target;
        }

        private static float GetActiveMoveSpring(RealPhysicsPlacerSettings activeSettings)
        {
            if (arrangePullActive)
            {
                return activeSettings.move.spring;
            }

            return activeSettings.move.spring;
        }

        private static float GetActiveMoveDamping(RealPhysicsPlacerSettings activeSettings)
        {
            if (arrangePullActive)
            {
                return activeSettings.move.damping;
            }

            return activeSettings.move.damping;
        }

        private static float GetActiveMoveSpeed(RealPhysicsPlacerSettings activeSettings)
        {
            if (fallActive)
            {
                return activeSettings.drop.maxSpeed;
            }

            if (arrangePullActive)
            {
                return activeSettings.move.followSpeed;
            }

            return activeSettings.move.followSpeed;
        }

        private static float GetActiveTargetSmoothTime(RealPhysicsPlacerSettings activeSettings)
        {
            if (arrangePullActive)
            {
                return activeSettings.move.targetSmoothTime;
            }

            return activeSettings.move.targetSmoothTime;
        }

        private static float GetActiveSimulationSpeed(RealPhysicsPlacerSettings activeSettings)
        {
            if (activeSettings.useGlobalPhysicsSettings)
            {
                return activeSettings.globalSimulationSpeed;
            }

            if (fallActive)
            {
                return activeSettings.drop.simulationSpeed;
            }

            if (rotationPullActive || arrangeRotationPullActive)
            {
                return activeSettings.rotate.simulationSpeed;
            }

            if (arrangePullActive)
            {
                return activeSettings.move.simulationSpeed;
            }

            return activeSettings.move.simulationSpeed;
        }

        private static float GetMaxSimulatedDeltaTime(PhysicsModeProfile profile)
        {
            return profile == PhysicsModeProfile.Drop
                ? MaxDropSimulatedDeltaTime
                : MaxInteractiveSimulatedDeltaTime;
        }

        private static PhysicsModeProfile GetActivePhysicsProfile(bool pullToTarget)
        {
            if (fallActive || (!pullToTarget && Mode == RealPhysicsPlacerMode.Fall))
            {
                return PhysicsModeProfile.Drop;
            }

            if (rotationPullActive || Mode == RealPhysicsPlacerMode.Rotation)
            {
                return PhysicsModeProfile.Rotate;
            }

            if (arrangeRotationPullActive || arrangeRandomSpinActive)
            {
                return PhysicsModeProfile.ArrangeRotate;
            }

            if (arrangePullActive || Mode == RealPhysicsPlacerMode.Alignment)
            {
                return PhysicsModeProfile.Arrange;
            }

            return PhysicsModeProfile.Move;
        }

        private static float GetActivePhysicsStep(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (activeSettings.useGlobalPhysicsSettings)
            {
                return activeSettings.globalPhysicsStep;
            }

            switch (profile)
            {
                case PhysicsModeProfile.Rotate:
                case PhysicsModeProfile.ArrangeRotate:
                    return activeSettings.rotate.physicsStep;
                case PhysicsModeProfile.Drop:
                    return activeSettings.drop.physicsStep;
                default:
                    return activeSettings.move.physicsStep;
            }
        }

        private static int GetActiveMaxSubsteps(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (activeSettings.useGlobalPhysicsSettings)
            {
                return activeSettings.globalMaxSubsteps;
            }

            switch (profile)
            {
                case PhysicsModeProfile.Rotate:
                case PhysicsModeProfile.ArrangeRotate:
                    return activeSettings.rotate.maxSubsteps;
                case PhysicsModeProfile.Drop:
                    return activeSettings.drop.maxSubsteps;
                default:
                    return activeSettings.move.maxSubsteps;
            }
        }

        private static int GetActiveSolverIterations(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (activeSettings.useGlobalPhysicsSettings)
            {
                return activeSettings.globalSolverIterations;
            }

            switch (profile)
            {
                case PhysicsModeProfile.Rotate:
                case PhysicsModeProfile.ArrangeRotate:
                    return activeSettings.rotate.solverIterations;
                case PhysicsModeProfile.Drop:
                    return activeSettings.drop.bodySolverIterations;
                default:
                    return activeSettings.move.solverIterations;
            }
        }

        private static int GetActiveSolverVelocityIterations(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (activeSettings.useGlobalPhysicsSettings)
            {
                return activeSettings.globalSolverVelocityIterations;
            }

            switch (profile)
            {
                case PhysicsModeProfile.Rotate:
                case PhysicsModeProfile.ArrangeRotate:
                    return activeSettings.rotate.solverVelocityIterations;
                case PhysicsModeProfile.Drop:
                    return activeSettings.drop.bodySolverVelocityIterations;
                default:
                    return activeSettings.move.solverVelocityIterations;
            }
        }

        private static RigidbodyConstraints GetActiveRigidbodyConstraints(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (profile == PhysicsModeProfile.Arrange)
            {
                return RigidbodyConstraints.FreezeRotation;
            }

            return ToRigidbodyRotationConstraints(GetActiveRotationConstraint(activeSettings, profile));
        }

        private static RealPhysicsPlacerRotationConstraint GetActiveRotationConstraint(RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (activeSettings == null || profile == PhysicsModeProfile.Arrange)
            {
                return profile == PhysicsModeProfile.Arrange
                    ? RealPhysicsPlacerSettings.AllRotationConstraintAxes
                    : RealPhysicsPlacerRotationConstraint.None;
            }

            if (activeSettings.useGlobalPhysicsSettings)
            {
                return RealPhysicsPlacerSettings.SanitizeRotationConstraint(activeSettings.globalRotationConstraint);
            }

            var constraint = profile switch
            {
                PhysicsModeProfile.Rotate => activeSettings.rotate.rotationConstraint,
                PhysicsModeProfile.ArrangeRotate => activeSettings.rotate.rotationConstraint,
                PhysicsModeProfile.Drop => activeSettings.drop.rotationConstraint,
                _ => activeSettings.move.rotationConstraint
            };
            return RealPhysicsPlacerSettings.SanitizeRotationConstraint(constraint);
        }

        private static RigidbodyConstraints ToRigidbodyRotationConstraints(RealPhysicsPlacerRotationConstraint constraint)
        {
            constraint = RealPhysicsPlacerSettings.SanitizeRotationConstraint(constraint);
            var constraints = RigidbodyConstraints.None;
            if ((constraint & RealPhysicsPlacerRotationConstraint.X) != 0)
            {
                constraints |= RigidbodyConstraints.FreezeRotationX;
            }

            if ((constraint & RealPhysicsPlacerRotationConstraint.Y) != 0)
            {
                constraints |= RigidbodyConstraints.FreezeRotationY;
            }

            if ((constraint & RealPhysicsPlacerRotationConstraint.Z) != 0)
            {
                constraints |= RigidbodyConstraints.FreezeRotationZ;
            }

            return constraints;
        }

        private static void ApplyActiveRotationConstraint(Rigidbody body, RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile profile)
        {
            if (body == null)
            {
                return;
            }

            var constraint = GetActiveRotationConstraint(activeSettings, profile);
            if (constraint == RealPhysicsPlacerRotationConstraint.None)
            {
                return;
            }

            body.angularVelocity = ConstrainRotationVector(body.angularVelocity, constraint);
        }

        private static Vector3 ConstrainRotationVector(Vector3 vector, RealPhysicsPlacerRotationConstraint constraint)
        {
            constraint = RealPhysicsPlacerSettings.SanitizeRotationConstraint(constraint);
            if ((constraint & RealPhysicsPlacerRotationConstraint.X) != 0)
            {
                vector.x = 0f;
            }

            if ((constraint & RealPhysicsPlacerRotationConstraint.Y) != 0)
            {
                vector.y = 0f;
            }

            if ((constraint & RealPhysicsPlacerRotationConstraint.Z) != 0)
            {
                vector.z = 0f;
            }

            return vector;
        }

        private static Quaternion ConstrainRotationTarget(
            Quaternion currentRotation,
            Quaternion requestedRotation,
            RealPhysicsPlacerRotationConstraint constraint)
        {
            constraint = RealPhysicsPlacerSettings.SanitizeRotationConstraint(constraint);
            if (constraint == RealPhysicsPlacerRotationConstraint.None)
            {
                return requestedRotation;
            }

            var delta = requestedRotation * Quaternion.Inverse(currentRotation);
            delta.ToAngleAxis(out var angleDegrees, out var axis);
            if (float.IsNaN(angleDegrees) || float.IsNaN(axis.x) || axis.sqrMagnitude <= Epsilon)
            {
                return currentRotation;
            }

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            var rotationVector = ConstrainRotationVector(axis.normalized * angleDegrees, constraint);
            var constrainedAngle = rotationVector.magnitude;
            if (constrainedAngle <= 0.001f)
            {
                return currentRotation;
            }

            return Quaternion.AngleAxis(constrainedAngle, rotationVector / constrainedAngle) * currentRotation;
        }

        private static bool IsMovePullActive()
        {
            return handlePullActive || placementOverlayPullActive;
        }

        private static void ApplyRotationDrive(PhysXBodyState body, RealPhysicsPlacerSettings activeSettings, PhysicsModeProfile activeProfile, float step)
        {
            if (body == null || body.Body == null)
            {
                return;
            }

            var targetRotation = GetRotationPhysicsTarget(body, activeProfile);
            var error = targetRotation * Quaternion.Inverse(body.Body.rotation);
            error.ToAngleAxis(out var angleDegrees, out var axis);
            if (float.IsNaN(angleDegrees) || float.IsNaN(axis.x) || axis.sqrMagnitude <= Epsilon)
            {
                axis = Vector3.zero;
                angleDegrees = 0f;
            }

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            var activeConstraint = GetActiveRotationConstraint(activeSettings, activeProfile);
            var angularError = ConstrainRotationVector(axis.normalized * (angleDegrees * Mathf.Deg2Rad), activeConstraint);
            var idle = RotationInputIsIdle(activeProfile);
            var nearTarget = Mathf.Abs(angleDegrees) <= 2.5f;
            var angularDamping = activeSettings.rotate.angularDamping;
            var linearDamping = activeSettings.rotate.linearDamping;
            if (idle)
            {
                angularDamping += Mathf.Max(8f, activeSettings.rotate.angularDamping * 0.75f);
                linearDamping += 6f;
            }

            if (nearTarget)
            {
                angularDamping += 14f;
                linearDamping += 8f;
            }

            var angularVelocity = ConstrainRotationVector(body.Body.angularVelocity, activeConstraint);
            var torque = angularError * activeSettings.rotate.angularSpring
                - angularVelocity * angularDamping;
            body.Body.AddTorque(torque, ForceMode.Acceleration);
            body.Body.angularVelocity = ConstrainRotationVector(
                Vector3.ClampMagnitude(body.Body.angularVelocity, activeSettings.rotate.maxAngularSpeed),
                activeConstraint);
            body.Body.linearVelocity *= Mathf.Exp(-Mathf.Max(0f, linearDamping) * step);

            if ((idle || nearTarget)
                && Mathf.Abs(angleDegrees) <= 0.75f
                && body.Body.linearVelocity.sqrMagnitude <= 0.0025f
                && body.Body.angularVelocity.sqrMagnitude <= 0.0025f)
            {
                body.Body.linearVelocity = Vector3.zero;
                body.Body.angularVelocity = Vector3.zero;
            }
        }

        private static Quaternion GetRotationPhysicsTarget(PhysXBodyState body, PhysicsModeProfile activeProfile)
        {
            if (activeProfile == PhysicsModeProfile.Rotate
                && body != null
                && body.Root != null
                && TryGetRotationPhysicsTarget(body.Root, out var rotateTarget))
            {
                return rotateTarget;
            }

            if (activeProfile == PhysicsModeProfile.ArrangeRotate
                && body != null
                && body.Root != null
                && TryGetArrangePhysicsRotationTarget(body.Root, out var arrangeTarget))
            {
                return arrangeTarget;
            }

            return rotationPullTarget * body.TargetRotationOffset;
        }

        private static bool RotationInputIsIdle(PhysicsModeProfile activeProfile)
        {
            if (rotationUprightPullActive || arrangeUprightPullActive || arrangeRandomSpinActive)
            {
                return false;
            }

            var lastInputTime = activeProfile == PhysicsModeProfile.ArrangeRotate
                ? arrangeRotationLastInputTime
                : rotationLastInputTime;
            return lastInputTime < 0d || EditorApplication.timeSinceStartup - lastInputTime > 0.08d;
        }

        private static bool ShouldIgnoreSelectedBodyCollisions(PhysicsModeProfile profile)
        {
            if (profile == PhysicsModeProfile.Arrange)
            {
                return arrangeGridPullActive || !GetSettings().arrange.collideSelectedObjects;
            }

            if (profile == PhysicsModeProfile.ArrangeRotate)
            {
                return !GetSettings().arrange.collideSelectedObjects;
            }

            return false;
        }

        private static bool PhysXSessionMatchesRoots(Transform[] roots)
        {
            if (PhysXBodies.Count != roots.Length)
            {
                return false;
            }

            foreach (var root in roots)
            {
                if (root == null || !PhysXBodies.Any(body => body.Root == root))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RestoreDuplicatedPhysicsState(Transform[] previousRoots, Transform[] currentRoots)
        {
            if (previousRoots == null
                || currentRoots == null
                || PhysXBodies.Count == 0
                || !SelectionLooksLikeRecentDuplicate(previousRoots, currentRoots))
            {
                return;
            }

            var unmatchedPrevious = previousRoots
                .Where(root => root != null)
                .ToList();
            var neutralizedDuplicate = false;
            foreach (var currentRoot in currentRoots)
            {
                if (currentRoot == null)
                {
                    continue;
                }

                var matchIndex = unmatchedPrevious.FindIndex(previousRoot => LooksLikeDuplicateOf(previousRoot, currentRoot));
                if (matchIndex < 0)
                {
                    continue;
                }

                var sourceRoot = unmatchedPrevious[matchIndex];
                if (!HasCopiedTemporaryPhysicsState(currentRoot)
                    && !LooksLikeFreshDuplicatePose(sourceRoot, currentRoot))
                {
                    continue;
                }

                unmatchedPrevious.RemoveAt(matchIndex);
                var sourceState = PhysXBodies.FirstOrDefault(body => body.Root == sourceRoot);
                if (sourceState != null)
                {
                    IgnoreDuplicateCollisionsWithRoots(new[] { sourceState.Root }, new[] { currentRoot });
                    NeutralizeDuplicateSourceBody(sourceState);
                }

                sourceState?.RemoveCopiedTemporaryComponentsFromDuplicate(currentRoot);
                RemoveCopiedTemporaryPhysicsComponents(currentRoot);
                RemoveCopiedSelectionProxyColliders(currentRoot);
                NeutralizeDuplicateSourceBody(sourceState);
                neutralizedDuplicate = true;

                var copiedBody = currentRoot.GetComponent<Rigidbody>();
                if (copiedBody == null)
                {
                    continue;
                }

                if (sourceState != null)
                {
                    sourceState.RestoreCopiedBodyState(copiedBody);
                    copiedBody = currentRoot.GetComponent<Rigidbody>();
                    FreezeDuplicateRigidbodyForCurrentSession(copiedBody);
                    continue;
                }

                copiedBody.linearVelocity = Vector3.zero;
                copiedBody.angularVelocity = Vector3.zero;
                copiedBody.Sleep();
                FreezeDuplicateRigidbodyForCurrentSession(copiedBody);
            }

            if (neutralizedDuplicate)
            {
                activeDuplicatePhysicsSweepDirty = true;
                Physics.SyncTransforms();
            }
        }

        private static void RemoveCopiedPhysicsFromActiveSessionDuplicates(Transform[] candidateRoots = null)
        {
            if (PhysXBodies.Count == 0)
            {
                return;
            }

            var candidates = candidateRoots != null && candidateRoots.Length > 0
                ? candidateRoots.Where(root => root != null)
                : EnumerateActiveSessionDuplicateCandidates();

            var neutralizedDuplicate = false;
            foreach (var candidateRoot in candidates.ToArray())
            {
                if (candidateRoot == null
                    || PhysXBodies.Any(body => body.Root == candidateRoot)
                    || PhysXBodies.Any(body => body.Root != null && candidateRoot.IsChildOf(body.Root)))
                {
                    continue;
                }

                var hasCopiedTemporaryState = HasCopiedTemporaryPhysicsState(candidateRoot);
                var sourceState = PhysXBodies.FirstOrDefault(body => body.Root != null
                    && (hasCopiedTemporaryState
                        ? LooksLikeActiveSessionDuplicateOf(body.Root, candidateRoot)
                        : LooksLikeFreshDuplicatePose(body.Root, candidateRoot)));
                if (sourceState == null)
                {
                    continue;
                }

                IgnoreDuplicateCollisionsWithRoots(new[] { sourceState.Root }, new[] { candidateRoot });
                NeutralizeDuplicateSourceBody(sourceState);
                if (hasCopiedTemporaryState)
                {
                    sourceState.RemoveCopiedTemporaryComponentsFromDuplicate(candidateRoot);
                    RemoveCopiedTemporaryPhysicsComponents(candidateRoot);
                    RemoveCopiedSelectionProxyColliders(candidateRoot);
                }

                NeutralizeDuplicateSourceBody(sourceState);
                neutralizedDuplicate = true;
                var copiedBody = candidateRoot.GetComponent<Rigidbody>();
                if (copiedBody != null && hasCopiedTemporaryState)
                {
                    sourceState.RestoreCopiedBodyState(copiedBody);
                    copiedBody = candidateRoot.GetComponent<Rigidbody>();
                    FreezeDuplicateRigidbodyForCurrentSession(copiedBody);
                }

                IgnoreDuplicateCollisionsWithRoots(PhysXBodies.Select(body => body.Root), new[] { candidateRoot });
            }

            if (neutralizedDuplicate)
            {
                Physics.SyncTransforms();
            }
        }

        private static void NeutralizeDuplicateSourceBody(PhysXBodyState sourceState)
        {
            var body = sourceState?.Body;
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private static void RemoveCopiedPhysicsFromActiveSessionDuplicatesBeforeSimulation(Transform[] currentSelectionRoots)
        {
            RemoveCopiedPhysicsFromActiveSessionDuplicates(currentSelectionRoots);

            var shouldSweepScene = leftMouseButtonDown || activeDuplicatePhysicsSweepDirty || DuplicateIgnoredCollisionPairs.Count > 0;
            if (!shouldSweepScene)
            {
                RemoveCopiedPhysicsFromActiveSessionDuplicates();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!activeDuplicatePhysicsSweepDirty
                && lastActiveDuplicatePhysicsSweepTime > 0d
                && now - lastActiveDuplicatePhysicsSweepTime < 0.04d)
            {
                RemoveCopiedPhysicsFromActiveSessionDuplicates();
                return;
            }

            lastActiveDuplicatePhysicsSweepTime = now;
            activeDuplicatePhysicsSweepDirty = false;
            RemoveCopiedPhysicsFromActiveSessionDuplicates();
        }

        private static IEnumerable<Transform> EnumerateActiveSessionDuplicateCandidates()
        {
            var emitted = new HashSet<EntityId>();
            foreach (var body in PhysXBodies)
            {
                var root = body?.Root;
                if (root == null || root.gameObject == null || !root.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (root.parent != null)
                {
                    for (var i = 0; i < root.parent.childCount; i++)
                    {
                        var candidate = root.parent.GetChild(i);
                        if (candidate != null && emitted.Add(candidate.GetEntityId()))
                        {
                            yield return candidate;
                        }
                    }

                    continue;
                }

                foreach (var gameObject in root.gameObject.scene.GetRootGameObjects())
                {
                    var candidate = gameObject != null ? gameObject.transform : null;
                    if (candidate != null && emitted.Add(candidate.GetEntityId()))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        private static bool LooksLikeActiveSessionDuplicateOf(Transform sourceRoot, Transform candidateRoot)
        {
            if (sourceRoot == null || candidateRoot == null)
            {
                return false;
            }

            if (sourceRoot == candidateRoot || candidateRoot.IsChildOf(sourceRoot))
            {
                return false;
            }

            if (sourceRoot.parent != candidateRoot.parent)
            {
                return false;
            }

            if (!string.Equals(
                    StripUnityDuplicateSuffix(sourceRoot.name),
                    StripUnityDuplicateSuffix(candidateRoot.name),
                    StringComparison.Ordinal))
            {
                return false;
            }

            if ((sourceRoot.localScale - candidateRoot.localScale).sqrMagnitude > 0.0001f)
            {
                return false;
            }

            var positionTolerance = GetActiveSessionDuplicatePositionTolerance(sourceRoot, candidateRoot);
            return (sourceRoot.position - candidateRoot.position).sqrMagnitude <= positionTolerance * positionTolerance;
        }

        private static float GetActiveSessionDuplicatePositionTolerance(Transform sourceRoot, Transform candidateRoot)
        {
            var activeSettings = GetSettings();
            var tolerance = Mathf.Max(
                0.5f,
                activeSettings.skinWidth * 8f,
                activeSettings.move.physicsAssistDistance * 3f);

            if (TryGetWorldBounds(sourceRoot, out var sourceBounds))
            {
                tolerance = Mathf.Max(tolerance, sourceBounds.extents.magnitude * 0.75f);
            }

            if (TryGetWorldBounds(candidateRoot, out var candidateBounds))
            {
                tolerance = Mathf.Max(tolerance, candidateBounds.extents.magnitude * 0.75f);
            }

            return Mathf.Min(tolerance, 6f);
        }

        private static bool HasCopiedTemporaryPhysicsState(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            if (IsTemporaryPhysicsComponent(root.GetComponent<Rigidbody>()))
            {
                return true;
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (IsTemporaryPhysicsComponent(collider))
                {
                    return true;
                }

                if (collider != null && IsSelectionProxyColliderObject(collider.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AnyRootHasCopiedTemporaryPhysicsState(Transform[] roots)
        {
            return roots != null && roots.Any(HasCopiedTemporaryPhysicsState);
        }

        private static void RemoveCopiedTemporaryPhysicsComponents(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(false))
            {
                if (IsTemporaryPhysicsComponent(collider))
                {
                    SafeDestroyImmediate(collider);
                }
            }

            var body = root.GetComponent<Rigidbody>();
            if (IsTemporaryPhysicsComponent(body))
            {
                SafeDestroyImmediate(body);
            }
        }

        private static void RemoveCopiedSelectionProxyColliders(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider == null
                    || LeasedSelectionProxyColliderIds.Contains(meshCollider.GetEntityId()))
                {
                    continue;
                }

                try
                {
                    var colliderGameObject = meshCollider.gameObject;
                    if (IsSelectionProxyColliderObject(colliderGameObject))
                    {
                        SafeDestroyImmediate(colliderGameObject);
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

        private static void RemoveCopiedTemporaryPhysicsComponents(Transform[] roots)
        {
            if (roots == null)
            {
                return;
            }

            foreach (var root in roots)
            {
                if (root != null)
                {
                    RemoveCopiedTemporaryPhysicsComponents(root);
                    RemoveCopiedSelectionProxyColliders(root);
                }
            }
        }

        private static void CleanupStaleTemporaryPhysicsComponents()
        {
            CleanupStaleTemporaryPhysicsComponents(false);
        }

        private static void CleanupStaleTemporaryPhysicsComponents(bool force)
        {
            if (physXSessionActive)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!force && (leftMouseButtonDown || HasActiveContinuousMode()))
            {
                return;
            }

            if (!force && now - lastGlobalTemporaryPhysicsCleanupTime < GlobalTemporaryPhysicsCleanupIntervalSeconds)
            {
                return;
            }

            lastGlobalTemporaryPhysicsCleanupTime = now;
            var bodies = Resources.FindObjectsOfTypeAll<Rigidbody>();
            foreach (var body in bodies)
            {
                if (IsTemporaryPhysicsComponent(body))
                {
                    SafeDestroyImmediate(body);
                }
            }

            var colliders = Resources.FindObjectsOfTypeAll<Collider>();
            foreach (var collider in colliders)
            {
                if (IsTemporaryPhysicsComponent(collider))
                {
                    SafeDestroyImmediate(collider);
                }
            }
        }

        private static bool IsTemporaryPhysicsComponent(Component component)
        {
            try
            {
                return component != null
                    && component.gameObject != null
                    && component.gameObject.scene.IsValid()
                    && !IsSceneProxyObject(component.gameObject)
                    && !IsSelectionProxyColliderObject(component.gameObject)
                    && (component.hideFlags & HideFlags.HideAndDontSave) != 0;
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

        private static bool IsSceneProxyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (proxyRoot != null && gameObject.transform.IsChildOf(proxyRoot.transform))
            {
                return true;
            }

            return (gameObject.hideFlags & HideFlags.HideAndDontSave) != 0
                && (string.Equals(gameObject.name, ProxyRootName, StringComparison.Ordinal)
                    || gameObject.name.StartsWith("Proxy_", StringComparison.Ordinal));
        }

        private static void FreezeNonSelectedRigidbodies(Transform[] roots)
        {
            RestoreFrozenSceneBodies();
            var bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
            foreach (var body in bodies)
            {
                if (body == null
                    || body.gameObject == null
                    || !body.gameObject.scene.IsValid()
                    || body.isKinematic
                    || IsUnderAnyRoot(body.transform, roots))
                {
                    continue;
                }

                FreezeDuplicateRigidbodyForCurrentSession(body);
            }
        }

        private static void FreezeDuplicateRigidbodyForCurrentSession(Rigidbody body)
        {
            if (body == null || FrozenSceneBodies.Any(state => state.Tracks(body)))
            {
                return;
            }

            FrozenSceneBodies.Add(new FrozenRigidbodyState(body));
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.Sleep();
        }

        private static void RestoreFrozenSceneBodies()
        {
            for (var i = FrozenSceneBodies.Count - 1; i >= 0; i--)
            {
                FrozenSceneBodies[i].Restore();
            }

            FrozenSceneBodies.Clear();
        }

        private static void IgnoreDuplicateCollisionsWithRoots(IEnumerable<Transform> sourceRoots, IEnumerable<Transform> duplicateRoots)
        {
            if (sourceRoots == null || duplicateRoots == null)
            {
                return;
            }

            var sources = sourceRoots
                .Where(root => root != null)
                .Distinct()
                .ToArray();
            if (sources.Length == 0)
            {
                return;
            }

            foreach (var duplicateRoot in duplicateRoots.Where(root => root != null).Distinct().ToArray())
            {
                var duplicateColliders = GetDuplicateCollisionSuppressionColliders(duplicateRoot);
                if (duplicateColliders.Length == 0)
                {
                    continue;
                }

                foreach (var sourceRoot in sources)
                {
                    if (sourceRoot == null
                        || sourceRoot == duplicateRoot
                        || sourceRoot.IsChildOf(duplicateRoot)
                        || duplicateRoot.IsChildOf(sourceRoot))
                    {
                        continue;
                    }

                    var sourceColliders = GetDuplicateCollisionSuppressionColliders(sourceRoot);
                    foreach (var sourceCollider in sourceColliders)
                    {
                        foreach (var duplicateCollider in duplicateColliders)
                        {
                            AddDuplicateIgnoredCollision(sourceCollider, duplicateCollider, duplicateRoot);
                        }
                    }
                }
            }
        }

        private static Collider[] GetDuplicateCollisionSuppressionColliders(Transform root)
        {
            return root == null
                ? Array.Empty<Collider>()
                : root.GetComponentsInChildren<Collider>(false)
                    .Where(collider => collider != null
                        && collider.enabled
                        && !collider.isTrigger)
                    .ToArray();
        }

        private static void AddDuplicateIgnoredCollision(Collider sourceCollider, Collider duplicateCollider, Transform duplicateRoot)
        {
            if (sourceCollider == null
                || duplicateCollider == null
                || duplicateRoot == null
                || sourceCollider == duplicateCollider
                || DuplicateIgnoredCollisionPairs.Any(pair => pair.Matches(sourceCollider, duplicateCollider)))
            {
                return;
            }

            try
            {
                if (!Physics.GetIgnoreCollision(sourceCollider, duplicateCollider))
                {
                    Physics.IgnoreCollision(sourceCollider, duplicateCollider, true);
                }

                DuplicateIgnoredCollisionPairs.Add(new DuplicateIgnoredCollisionPair(sourceCollider, duplicateCollider, duplicateRoot));
            }
            catch (MissingReferenceException)
            {
            }
            catch (NullReferenceException)
            {
            }
        }

        private static void RefreshDuplicateIgnoredCollisions()
        {
            if (DuplicateIgnoredCollisionPairs.Count == 0)
            {
                return;
            }

            for (var i = DuplicateIgnoredCollisionPairs.Count - 1; i >= 0; i--)
            {
                if (!DuplicateIgnoredCollisionPairs[i].IsValid)
                {
                    DuplicateIgnoredCollisionPairs.RemoveAt(i);
                }
            }

            if (DuplicateIgnoredCollisionPairs.Count == 0)
            {
                return;
            }

            Physics.SyncTransforms();
            var duplicateRoots = DuplicateIgnoredCollisionPairs
                .Select(pair => pair.DuplicateRoot)
                .Where(root => root != null)
                .Distinct()
                .ToArray();
            foreach (var duplicateRoot in duplicateRoots)
            {
                if (!DuplicateIgnoredCollisionPairsStillNear(duplicateRoot))
                {
                    RestoreDuplicateIgnoredCollisionsForRoot(duplicateRoot);
                }
            }
        }

        private static bool DuplicateIgnoredCollisionPairsStillNear(Transform duplicateRoot)
        {
            var padding = GetDuplicateCollisionReleasePadding();
            foreach (var pair in DuplicateIgnoredCollisionPairs)
            {
                if (!pair.TracksRoot(duplicateRoot) || !pair.IsValid)
                {
                    continue;
                }

                try
                {
                    var leftBounds = pair.Left.bounds;
                    var rightBounds = pair.Right.bounds;
                    leftBounds.Expand(padding * 2f);
                    rightBounds.Expand(padding * 2f);
                    if (leftBounds.Intersects(rightBounds))
                    {
                        return true;
                    }
                }
                catch (MissingReferenceException)
                {
                }
                catch (NullReferenceException)
                {
                }
            }

            return false;
        }

        private static float GetDuplicateCollisionReleasePadding()
        {
            var activeSettings = GetSettings();
            return Mathf.Clamp(Mathf.Max(0.002f, activeSettings.skinWidth * 0.08f), 0.002f, 0.025f);
        }

        private static bool IsDuplicateCollisionSuppressedCollider(Collider collider)
        {
            return collider != null
                && DuplicateIgnoredCollisionPairs.Any(pair => pair.TracksCollider(collider));
        }

        private static void RestoreDuplicateIgnoredCollisions()
        {
            var restoredAny = DuplicateIgnoredCollisionPairs.Count > 0;
            for (var i = DuplicateIgnoredCollisionPairs.Count - 1; i >= 0; i--)
            {
                DuplicateIgnoredCollisionPairs[i].Restore();
            }

            DuplicateIgnoredCollisionPairs.Clear();
            if (restoredAny)
            {
                Physics.SyncTransforms();
            }
        }

        private static void RestoreDuplicateIgnoredCollisionsForRoot(Transform duplicateRoot)
        {
            var restoredAny = false;
            for (var i = DuplicateIgnoredCollisionPairs.Count - 1; i >= 0; i--)
            {
                if (!DuplicateIgnoredCollisionPairs[i].TracksRoot(duplicateRoot))
                {
                    continue;
                }

                DuplicateIgnoredCollisionPairs[i].Restore();
                DuplicateIgnoredCollisionPairs.RemoveAt(i);
                restoredAny = true;
            }

            if (restoredAny)
            {
                Physics.SyncTransforms();
            }
        }

        private static void IgnoreShadowedSceneCollisions(Transform[] roots)
        {
            if (!GetSettings().useSceneProxyCollidersForAllObjects || SceneCollidersShadowedByProxies.Count == 0)
            {
                return;
            }

            var selectedColliders = PhysXBodies
                .SelectMany(body => body.GetColliders())
                .Where(collider => collider != null && collider.enabled)
                .ToArray();
            if (selectedColliders.Length == 0)
            {
                return;
            }

            foreach (var sceneCollider in SceneCollidersShadowedByProxies.Values)
            {
                if (sceneCollider == null
                    || !sceneCollider.enabled
                    || IsUnderAnyRoot(sceneCollider.transform, roots)
                    || !IsSceneColliderShadowedByProxy(sceneCollider))
                {
                    continue;
                }

                foreach (var selectedCollider in selectedColliders)
                {
                    if (selectedCollider == null || selectedCollider == sceneCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(selectedCollider, sceneCollider, true);
                    IgnoredCollisionPairs.Add(new IgnoredCollisionPair(selectedCollider, sceneCollider));
                }
            }
        }

        private static void IgnoreSelectedBodyCollisions()
        {
            RestoreIgnoredSelectedCollisions();
            for (var i = 0; i < PhysXBodies.Count; i++)
            {
                var leftColliders = PhysXBodies[i].GetColliders();
                for (var j = i + 1; j < PhysXBodies.Count; j++)
                {
                    var rightColliders = PhysXBodies[j].GetColliders();
                    foreach (var left in leftColliders)
                    {
                        foreach (var right in rightColliders)
                        {
                            if (left == null || right == null || left == right)
                            {
                                continue;
                            }

                            Physics.IgnoreCollision(left, right, true);
                            IgnoredCollisionPairs.Add(new IgnoredCollisionPair(left, right));
                        }
                    }
                }
            }
        }

        private static void RestoreIgnoredSelectedCollisions()
        {
            foreach (var pair in IgnoredCollisionPairs)
            {
                try
                {
                    if (pair.Left != null && pair.Right != null)
                    {
                        Physics.IgnoreCollision(pair.Left, pair.Right, false);
                    }
                }
                catch (MissingReferenceException)
                {
                }
                catch (NullReferenceException)
                {
                }
            }

            IgnoredCollisionPairs.Clear();
        }
    }
}
