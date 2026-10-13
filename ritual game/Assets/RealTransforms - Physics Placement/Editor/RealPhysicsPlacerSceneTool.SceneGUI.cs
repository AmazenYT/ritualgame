using UnityEditor;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Scene-view handles stay thin: they gather user intent, then delegate
        // collision and physics decisions to the placement/rotation/arrange code.
        private static void DrawModeHandles(Transform[] roots)
        {
            if (!PhysicsAssistEnabled && !fallActive && !arrangePullActive)
            {
                return;
            }

            switch (Mode)
            {
                case RealPhysicsPlacerMode.Placement:
                    break;
                case RealPhysicsPlacerMode.Rotation:
                    DrawRotationHandle(roots);
                    break;
                case RealPhysicsPlacerMode.Alignment:
                    DrawArrangeHandles(roots);
                    break;
                case RealPhysicsPlacerMode.Fall:
                    DrawFallHandle(roots);
                    break;
            }
        }

        private static void DrawPlacementHandle(Transform[] roots)
        {
            var visiblePosition = handlePosition;
            EditorGUI.BeginChangeCheck();
            var nextPosition = Handles.PositionHandle(visiblePosition, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                var requestedDelta = nextPosition - visiblePosition;
                if (requestedDelta.sqrMagnitude > MotionEpsilonSqr)
                {
                    var activeSettings = GetSettings();
                    if (handlePullActive)
                    {
                        movePullTarget = ClampMovePullTarget(visiblePosition, nextPosition);
                        StartHandlePull(roots, movePullTarget);
                    }
                    else
                    {
                        MovePlacementHandleDirectly(roots, visiblePosition, nextPosition, requestedDelta, activeSettings);
                    }
                }
            }

            if (Event.current.rawType == EventType.MouseUp)
            {
                if (handlePullActive)
                {
                    StopContinuousModes();
                    ResetHandleToSelection();
                    SceneView.RepaintAll();
                }
            }

            if (handlePullActive && hasMovePullTarget)
            {
                DrawSoftTargetTether(handlePosition, movePullTarget);
            }

            if (!GetSettings().move.controlRotationInPlacement)
            {
                return;
            }

            DrawRotationHandle(roots);
        }

        private static void DrawRotationHandle(Transform[] roots)
        {
            EditorGUI.BeginChangeCheck();
            var nextRotation = Handles.RotationHandle(handleRotation, handlePosition);
            if (!EditorGUI.EndChangeCheck())
            {
                if (Event.current.rawType == EventType.MouseUp)
                {
                    if (rotationPullActive)
                    {
                        StopContinuousModes();
                    }

                    ResetHandleToSelection();
                }

                return;
            }

            var constrainedRotation = ConstrainRotationTarget(
                handleRotation,
                nextRotation,
                GetActiveRotationConstraint(GetSettings(), PhysicsModeProfile.Rotate));
            if (Quaternion.Angle(handleRotation, constrainedRotation) <= 0.05f)
            {
                handleRotation = constrainedRotation;
                SceneView.RepaintAll();
                return;
            }

            StartRotationPull(roots, constrainedRotation);
            handleRotation = constrainedRotation;
            SceneView.RepaintAll();
        }

        private static void DrawFallHandle(Transform[] roots)
        {
            var activeSettings = GetSettings();
            var gravity = activeSettings.drop.gravity.sqrMagnitude > Epsilon ? activeSettings.drop.gravity.normalized : Vector3.down;
            using (new Handles.DrawingScope(activeSettings.blockedColor))
            {
                var size = HandleUtility.GetHandleSize(handlePosition);
                Handles.ArrowHandleCap(0, handlePosition, Quaternion.LookRotation(gravity), size * 0.45f, EventType.Repaint);
                Handles.Label(handlePosition - gravity * size * 0.22f, fallActive ? "Dropping" : $"Hold {FormatShortcutKey(activeSettings.actionKey)}");
            }
        }

        private static void DrawSoftTargetTether(Vector3 objectPosition, Vector3 targetPosition)
        {
            targetPosition = ClampMovePullTarget(objectPosition, targetPosition);
            var size = HandleUtility.GetHandleSize(objectPosition);
            using (new Handles.DrawingScope(GetSettings().blockedColor))
            {
                Handles.DrawDottedLine(objectPosition, targetPosition, 4f);
                Handles.SphereHandleCap(0, targetPosition, Quaternion.identity, size * 0.055f, EventType.Repaint);
            }
        }

        private static void DrawCollisionIndicators(Transform[] roots)
        {
            if (!TryGetCombinedBounds(roots, out var bounds))
            {
                return;
            }

            DrawCollisionIndicator(bounds, Vector3.right, GetSettings().xAxisColor, roots);
            DrawCollisionIndicator(bounds, Vector3.left, GetSettings().xAxisColor, roots);
            DrawCollisionIndicator(bounds, Vector3.up, GetSettings().yAxisColor, roots);
            DrawCollisionIndicator(bounds, Vector3.down, GetSettings().yAxisColor, roots);
            DrawCollisionIndicator(bounds, Vector3.forward, GetSettings().zAxisColor, roots);
            DrawCollisionIndicator(bounds, Vector3.back, GetSettings().zAxisColor, roots);
        }

        private static void DrawCollisionIndicator(Bounds bounds, Vector3 direction, Color color, Transform[] roots)
        {
            if (!FindNearestHit(bounds, direction, GetSettings().collisionIndicatorDistance, roots, true, out var hit))
            {
                return;
            }

            var size = HandleUtility.GetHandleSize(hit.point) * 0.08f;
            using (new Handles.DrawingScope(color))
            {
                Handles.DrawLine(bounds.center, hit.point);
                Handles.SphereHandleCap(0, hit.point, Quaternion.identity, size, EventType.Repaint);
            }
        }

        private static void DrawPhysicsAssistBadge()
        {
            if (!PhysicsAssistMasterEnabled || !hasHandle || SelectionCount == 0)
            {
                return;
            }

            var currentMode = Mode;
            EnsurePhysicsAssistBadgeStyles();
            if (physicsAssistBadgeTooltipMode != currentMode)
            {
                PhysicsAssistBadgeContent.tooltip = $"Physics Assist: {ModeDisplayName(currentMode)}";
                physicsAssistBadgeTooltipMode = currentMode;
            }

            var anchor = GetPhysicsAssistBadgeAnchor(currentMode);
            var point = HandleUtility.WorldToGUIPoint(anchor);
            var rect = new Rect(point.x + 34f, point.y - 34f, 30f, 30f);

            Handles.BeginGUI();
            var previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), PhysicsAssistBadgeContent, physicsAssistBadgeShadowStyle);
            GUI.Label(rect, PhysicsAssistBadgeContent, physicsAssistBadgeStyle);
            GUI.depth = previousDepth;
            Handles.EndGUI();
        }

        private static void EnsurePhysicsAssistBadgeStyles()
        {
            if (physicsAssistBadgeStyle != null && physicsAssistBadgeShadowStyle != null)
            {
                return;
            }

            physicsAssistBadgeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            physicsAssistBadgeStyle.normal.textColor = new Color(1f, 0.48f, 0.02f, 1f);

            physicsAssistBadgeShadowStyle = new GUIStyle(physicsAssistBadgeStyle);
            physicsAssistBadgeShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
        }

        private static Vector3 GetPhysicsAssistBadgeAnchor(RealPhysicsPlacerMode currentMode)
        {
            if (currentMode == RealPhysicsPlacerMode.Placement)
            {
                return Tools.handlePosition;
            }

            return handlePosition;
        }

        private static string ModeDisplayName(RealPhysicsPlacerMode mode)
        {
            switch (mode)
            {
                case RealPhysicsPlacerMode.Placement:
                    return "Move";
                case RealPhysicsPlacerMode.Rotation:
                    return "Rotate";
                case RealPhysicsPlacerMode.Fall:
                    return "Drop";
                case RealPhysicsPlacerMode.Alignment:
                    return "Arrange";
                default:
                    return mode.ToString();
            }
        }
    }
}
