﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    [InitializeOnLoad]
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Central editor coordinator. Mode-specific behavior is split into
        // partial files so the Scene GUI, physics session, collision, proxy, and
        // arrange logic can be reviewed independently.
        private const string EnabledKey = "RealTransforms.Enabled";
        private const string ModeKey = "RealTransforms.Mode";
        private const string PhysicsAssistKey = "RealTransforms.PhysicsAssist";
        private const string ActivePhysicsSessionKey = "RealTransforms.ActivePhysicsSession";
        private const float Epsilon = 0.00001f;
        private const float MotionEpsilonSqr = 0.0000000001f;
        private const int PlacementOverlayReleaseClearFrames = 2;
        private const int PlacementOverlayPullGraceFrames = 2;
        private const double PlacementOverlayMinimumPullSeconds = 0.2d;
        private const double PlacementOverlayReleaseClearSeconds = 0.16d;
        private const double HookValidationIntervalSeconds = 15d;
        private const double StaleMouseStateSeconds = 2d;
        private const double ForceStaleMouseStateSeconds = 12d;
        private const double ActiveSceneProxyRebuildIntervalSeconds = 0.18d;
        private const double SceneProxySnapshotCheckIntervalSeconds = 1d;
        private const double GlobalTemporaryPhysicsCleanupIntervalSeconds = 30d;
        private const float PlacementPullReverseDot = -0.25f;
        private const string ProxyRootName = "__RealTransforms_EditorPhysicsProxies";
        private const int SceneProxyCacheLimit = 256;
        private const int MaxOverlapResults = 96;
        private const int MaxCastResults = 128;
        private const int MaxPenetrationChecksPerPass = 48;
        private const float MaxInteractiveSimulatedDeltaTime = 0.032f;
        private const float MaxDropSimulatedDeltaTime = 0.05f;
        private const string IsolatedPhysicsSceneName = "__RealTransforms_PhysicsScene";
        private const string IsolatedPhysicsRootName = "__RealTransforms_PhysicsRoot";

        // Shared buffers keep interactive editor work allocation-free where Unity
        // exposes non-alloc APIs. They are intentionally reused across frames.
        private static readonly List<GameObject> Proxies = new List<GameObject>();
        private static readonly Collider[] OverlapBuffer = new Collider[MaxOverlapResults];
        private static readonly RaycastHit[] CastHitBuffer = new RaycastHit[MaxCastResults];
        private static readonly List<Collider> ColliderCastBuffer = new List<Collider>(32);
        private static readonly List<Collider> BoundsColliderBuffer = new List<Collider>(32);
        private static readonly List<Renderer> BoundsRendererBuffer = new List<Renderer>(32);
        private static readonly Dictionary<Transform, Vector3> PlacementOverlayPositions = new Dictionary<Transform, Vector3>();
        private static readonly Dictionary<Transform, Vector3> PlacementOverlayHandoffNativePositions = new Dictionary<Transform, Vector3>();
        private static readonly Dictionary<Transform, Vector3> PlacementOverlayPullNativePositions = new Dictionary<Transform, Vector3>();
        private static readonly Dictionary<Transform, Quaternion> RotationPhysicsTargets = new Dictionary<Transform, Quaternion>();
        private static readonly HashSet<EntityId> PlacementOverlayDuplicateSuppressRoots = new HashSet<EntityId>();
        private static readonly HashSet<EntityId> DuplicatePhysicsSuppressRoots = new HashSet<EntityId>();
        private static readonly HashSet<string> EmptyIgnoredTags = new HashSet<string>();
        private static readonly Dictionary<EntityId, Collider> SceneCollidersShadowedByProxies = new Dictionary<EntityId, Collider>();
        private static readonly List<FrozenRigidbodyState> FrozenSceneBodies = new List<FrozenRigidbodyState>();
        private static readonly Vector3[] ContactProbeDirections =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        // Scene-view state is kept static because Unity editor tools are driven
        // by callbacks instead of normal MonoBehaviour lifetimes.
        private static RealPhysicsPlacerSettings settings;
        private static Transform[] selectedRoots = Array.Empty<Transform>();
        private static readonly GUIContent PhysicsAssistBadgeContent = new GUIContent("\u03BB");
        private static GUIStyle physicsAssistBadgeStyle;
        private static GUIStyle physicsAssistBadgeShadowStyle;
        private static RealPhysicsPlacerMode physicsAssistBadgeTooltipMode = (RealPhysicsPlacerMode)(-1);
        private static Vector3 handlePosition;
        private static Quaternion handleRotation = Quaternion.identity;
        private static bool hasHandle;
        private static bool hooked;
        private static bool proxyScanDirty = true;
        private static GameObject proxyRoot;
        private static readonly Dictionary<EntityId, SceneProxyCacheEntry> SceneProxyCache = new Dictionary<EntityId, SceneProxyCacheEntry>();
        private static int sceneProxyBuildStamp;
        private static bool suppressSceneProxyHierarchyDirty;
        private static double suppressSceneProxyHierarchyDirtyUntil = -1d;
        private static bool suspendedForPlayMode;
        private static bool sceneRendererCacheDirty = true;
        private static Scene sceneRendererCacheScene;
        private static Renderer[] sceneRendererCache = Array.Empty<Renderer>();
        // Renderer metadata is cached separately from proxy instances so a scan
        // can skip known LOD, baked-collider, and runtime-physics renderers fast.
        private static readonly Dictionary<EntityId, SceneRendererProxyMetadata> SceneRendererProxyMetadataCache = new Dictionary<EntityId, SceneRendererProxyMetadata>();
        private static bool hasSceneProxyCacheCenter;
        private static Vector3 sceneProxyCacheCenter;
        private static float sceneProxyCacheRadius;
        private static double lastSceneProxyRebuildTime = -1d;
        private static bool previousToolsHidden;
        private static PivotRotation previousPivotRotation;
        private static PivotMode previousPivotMode;
        private static bool hasPreviousPivotRotation;
        private static bool fallActive;
        private static bool hasPullTarget;
        private static Vector3 pullTarget;
        private static Vector3 smoothedPullTarget;
        private static bool hasSmoothedPullTarget;
        private static readonly List<PhysXBodyState> PhysXBodies = new List<PhysXBodyState>();
        private static readonly List<IgnoredCollisionPair> IgnoredCollisionPairs = new List<IgnoredCollisionPair>();
        private static readonly List<DuplicateIgnoredCollisionPair> DuplicateIgnoredCollisionPairs = new List<DuplicateIgnoredCollisionPair>();
        private static readonly List<GameObject> IsolatedStaticColliderObjects = new List<GameObject>();
        private static bool physXSessionActive;
        private static Scene isolatedPhysicsScene;
        private static PhysicsScene isolatedPhysicsWorld;
        private static GameObject isolatedPhysicsRoot;
        private static bool isolatedPhysicsSessionActive;
        private static bool placementOverlayPullActive;
        private static bool placementOverlayHasContact;
        private static bool placementOverlayHandoffActive;
        private static bool placementOverlayHasPullDirection;
        private static bool placementOverlaySuppressUntilClear;
        private static Vector3 placementOverlayPullDirection;
        private static int placementOverlayClearFrames;
        private static int placementOverlayPullFrames;
        private static double placementOverlayPullStartedAt = -1d;
        private static double placementOverlayContactLostAt = -1d;
        private static bool handlePullActive;
        private static bool arrangePullActive;
        private static bool arrangeGridPullActive;
        private static bool rotationPullActive;
        private static bool arrangeRotationPullActive;
        private static bool arrangeRandomSpinActive;
        private static bool rotationUprightPullActive;
        private static bool arrangeUprightPullActive;
        private static bool hasMovePullTarget;
        private static Vector3 movePullTarget;
        private static Quaternion rotationPullTarget = Quaternion.identity;
        private static double rotationLastInputTime = -1d;
        private static double arrangeRotationLastInputTime = -1d;
        // When RealTransforms drives the normal Unity physics scene, simulation is
        // temporarily switched to Script and restored when the session ends.
        private static SimulationMode previousSimulationMode;
        private static bool hasPreviousSimulationMode;
        private static PhysicsModeProfile physXSessionProfile = PhysicsModeProfile.Move;
        private static int continuousUndoGroup = -1;
        private static double lastUpdateTime;
        private static bool assistKeyHeld;
        private static bool shiftToggleHeld;
        private static bool shiftToggleChorded;
        private static bool leftMouseButtonDown;
        private static bool rightMouseButtonDown;
        private static bool duplicatePhysicsSuppressActionKey;
        private static bool disablePhysicsAssistWhenDragEnds;
        private static double shiftToggleStartTime;
        private static double lastAssistKeyEventTime = -1d;
        private static double lastHookValidationTime = -1d;
        private static double lastSceneMouseEventTime = -1d;
        private static double duplicatePhysicsSuppressStartedAt = -1d;
        private static double lastActiveDuplicatePhysicsSweepTime = -1d;
        private static double lastSceneProxySnapshotCheckTime = -1d;
        private static double lastGlobalTemporaryPhysicsCleanupTime = -999d;
        private static bool activeDuplicatePhysicsSweepDirty;

        private enum PhysicsModeProfile
        {
            Move,
            Rotate,
            Arrange,
            ArrangeRotate,
            Drop
        }

        static RealPhysicsPlacerSceneTool()
        {
            EnsureHooked();
        }

        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                if (value && !EditorPhysicsAllowed)
                {
                    return;
                }

                if (value == Enabled)
                {
                    return;
                }

                EditorPrefs.SetBool(EnabledKey, value);
                if (value)
                {
                    previousToolsHidden = Tools.hidden;
                    RebuildSelection();
                    MarkSceneProxyScanDirty();
                    RefreshToolVisibility();
                }
                else
                {
                    StopContinuousModes();
                    ClearSceneProxies(true);
                    ResetPlacementOverlayTracking();
                    Tools.hidden = previousToolsHidden;
                    RestoreToolOrientation();
                }

                SceneView.RepaintAll();
            }
        }

        internal static bool PhysicsAssistEnabled
        {
            get => EditorPrefs.GetBool(PhysicsAssistKey, false);
            set
            {
                if (value && !EditorPhysicsAllowed)
                {
                    return;
                }

                if (SceneDragInProgress())
                {
                    var roots = GetLiveSelectionRoots();
                    var beginHandoff = !value && (placementOverlayPullActive || placementOverlayHandoffActive);
                    EditorPrefs.SetBool(PhysicsAssistKey, value);
                    if (beginHandoff)
                    {
                        StopContinuousModes(false);
                        BeginPlacementOverlayHandoff(roots);
                    }
                    else if (value)
                    {
                        ResetPlacementOverlayTracking();
                    }
                    else
                    {
                        ResetPlacementOverlayTracking();
                    }

                    SceneView.RepaintAll();
                    return;
                }

                if (value == PhysicsAssistEnabled)
                {
                    return;
                }

                ApplyPhysicsAssistEnabled(value);
            }
        }

        internal static bool PhysicsAssistMasterEnabled => EditorPhysicsAllowed && Enabled && PhysicsAssistEnabled;
        internal static bool EditorPhysicsAllowed => !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static void SetPhysicsAssistMasterEnabled(bool value)
        {
            if (value && !EditorPhysicsAllowed)
            {
                return;
            }

            if (value)
            {
                disablePhysicsAssistWhenDragEnds = false;
                CaptureToolOrientation();
                if (!Enabled)
                {
                    Enabled = true;
                }

                PhysicsAssistEnabled = true;
                ForceGlobalToolOrientation();
                SyncModeToUnityTool();
                RefreshToolVisibility();
                SceneView.RepaintAll();
                return;
            }

            PhysicsAssistEnabled = false;
            if (SceneDragInProgress())
            {
                disablePhysicsAssistWhenDragEnds = true;
                RefreshToolVisibility();
                SceneView.RepaintAll();
                return;
            }

            disablePhysicsAssistWhenDragEnds = false;
            Enabled = false;
        }

        private static void ApplyPhysicsAssistEnabled(bool value)
        {
            if (value && !EditorPhysicsAllowed)
            {
                return;
            }

            EditorPrefs.SetBool(PhysicsAssistKey, value);
            StopContinuousModes(false);
            ResetHandleToSelection();
            ResetPlacementOverlayTracking();
            if (value)
            {
                CaptureToolOrientation();
                ForceGlobalToolOrientation();
                QueueSurfaceShellPrewarm(GetLiveSelectionRoots());
            }

            RefreshToolVisibility();
            SceneView.RepaintAll();
        }

        internal static RealPhysicsPlacerMode Mode
        {
            get
            {
                var rawMode = EditorPrefs.GetInt(ModeKey, (int)RealPhysicsPlacerMode.Placement);
                var mode = SanitizeMode(rawMode);
                if ((int)mode != rawMode)
                {
                    EditorPrefs.SetInt(ModeKey, (int)mode);
                }

                return mode;
            }
            set
            {
                value = SanitizeMode((int)value);
                if (value == Mode)
                {
                    return;
                }

                EditorPrefs.SetInt(ModeKey, (int)value);
                SyncUnityToolToMode(value);
                StopContinuousModes();
                ResetArrangeState();
                ResetHandleToSelection();
                RefreshToolVisibility();
                SceneView.RepaintAll();
            }
        }

        private static RealPhysicsPlacerMode SanitizeMode(int rawMode)
        {
            return Enum.IsDefined(typeof(RealPhysicsPlacerMode), rawMode)
                ? (RealPhysicsPlacerMode)rawMode
                : RealPhysicsPlacerMode.Placement;
        }

        internal static int SelectionCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < selectedRoots.Length; i++)
                {
                    if (selectedRoots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal static int ProxyCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Proxies.Count; i++)
                {
                    if (Proxies[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal static bool HasLiveEditorPhysicsActivity => EditorPhysicsAllowed
            && (physXSessionActive
                || HasActiveContinuousMode()
                || placementOverlayPullActive
                || placementOverlayHandoffActive
                || HasSceneProxyState()
                || (proxyScanDirty && SceneProxyFeatureEnabled()));

        private static bool HasActiveContinuousMode()
        {
            return fallActive
                || handlePullActive
                || rotationPullActive
                || rotationUprightPullActive
                || arrangePullActive
                || arrangeRotationPullActive
                || arrangeRandomSpinActive
                || arrangeUprightPullActive
                || arrangeEvenSpacingActive
                || placementOverlayPullActive
                || placementOverlayHandoffActive;
        }

        private static bool HasTransientEditorPhysicsState()
        {
            return physXSessionActive
                || HasActiveContinuousMode()
                || PhysXBodies.Count > 0
                || hasPreviousSimulationMode
                || Proxies.Count > 0
                || SceneProxyCache.Count > 0
                || proxyRoot != null
                || SelectionProxyColliderPool.Count > 0
                || LeasedSelectionProxyColliderIds.Count > 0
                || PlacementOverlayDuplicateSuppressRoots.Count > 0
                || DuplicatePhysicsSuppressRoots.Count > 0
                || DuplicateIgnoredCollisionPairs.Count > 0;
        }

        private static bool HasTrackedEditorInputState()
        {
            return leftMouseButtonDown
                || rightMouseButtonDown
                || shiftToggleHeld
                || assistKeyHeld
                || duplicatePhysicsSuppressActionKey
                || disablePhysicsAssistWhenDragEnds;
        }

        private static void RequestSceneRepaintFromEditorUpdate()
        {
            if (Event.current != null)
            {
                return;
            }

            RepaintActiveSceneView();
        }

        private static void RepaintActiveSceneView()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Repaint();
                return;
            }

            SceneView.RepaintAll();
        }

        private static HashSet<string> GetIgnoredTagSet(RealPhysicsPlacerSettings activeSettings)
        {
            return activeSettings == null || string.IsNullOrWhiteSpace(activeSettings.ignoredTags)
                ? EmptyIgnoredTags
                : activeSettings.BuildIgnoredTagSet();
        }

        private static void MarkDuplicatePhysicsSuppression(Transform[] roots, bool waitForActionKeyRelease)
        {
            DuplicatePhysicsSuppressRoots.Clear();
            if (roots == null || roots.Length == 0)
            {
                ClearDuplicatePhysicsSuppression();
                return;
            }

            foreach (var root in roots)
            {
                if (root != null)
                {
                    DuplicatePhysicsSuppressRoots.Add(root.GetEntityId());
                }
            }

            duplicatePhysicsSuppressActionKey = waitForActionKeyRelease;
            duplicatePhysicsSuppressStartedAt = EditorApplication.timeSinceStartup;
        }

        private static void ClearDuplicatePhysicsSuppression()
        {
            DuplicatePhysicsSuppressRoots.Clear();
            duplicatePhysicsSuppressActionKey = false;
            duplicatePhysicsSuppressStartedAt = -1d;
        }

        private static bool SelectionMatchesDuplicatePhysicsSuppression(Transform[] roots)
        {
            if (roots == null || roots.Length == 0 || DuplicatePhysicsSuppressRoots.Count == 0)
            {
                return false;
            }

            foreach (var root in roots)
            {
                if (root == null || !DuplicatePhysicsSuppressRoots.Contains(root.GetEntityId()))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldSuppressDuplicatePhysics(Transform[] roots)
        {
            return SelectionMatchesDuplicatePhysicsSuppression(roots);
        }

        private static bool IsDuplicatePhysicsSuppressionInputActive()
        {
            if (leftMouseButtonDown || rightMouseButtonDown)
            {
                return true;
            }

            if (!duplicatePhysicsSuppressActionKey)
            {
                return false;
            }

            return fallActive
                || rotationUprightPullActive
                || arrangeUprightPullActive
                || arrangeRandomSpinActive
                || arrangeEvenSpacingActive;
        }

        private static void RefreshDuplicatePhysicsSuppression()
        {
            if (DuplicatePhysicsSuppressRoots.Count == 0)
            {
                duplicatePhysicsSuppressActionKey = false;
                return;
            }

            var roots = selectedRoots.Where(root => root != null).ToArray();
            if (!SelectionMatchesDuplicatePhysicsSuppression(roots))
            {
                ClearDuplicatePhysicsSuppression();
                return;
            }

            if (!IsDuplicatePhysicsSuppressionInputActive())
            {
                ClearDuplicatePhysicsSuppression();
                return;
            }

            if (duplicatePhysicsSuppressStartedAt > 0d
                && EditorApplication.timeSinceStartup - duplicatePhysicsSuppressStartedAt > ForceStaleMouseStateSeconds)
            {
                ClearDuplicatePhysicsSuppression();
            }
        }

        private static void RefreshToolVisibility()
        {
            if (!EditorPhysicsAllowed)
            {
                Tools.hidden = previousToolsHidden;
                RestoreToolOrientation();
                return;
            }

            if (!Enabled || (!PhysicsAssistEnabled && !placementOverlayHandoffActive))
            {
                return;
            }

            if (SceneDragInProgress())
            {
                return;
            }

            ForceGlobalToolOrientation();
            var nextHidden = ShouldHideUnityTools() ? true : previousToolsHidden;
            if (Tools.hidden != nextHidden)
            {
                Tools.hidden = nextHidden;
            }
        }

        private static bool ShouldHideUnityTools()
        {
            if (!Enabled || (!PhysicsAssistEnabled && !placementOverlayHandoffActive))
            {
                return false;
            }

            return Mode != RealPhysicsPlacerMode.Placement
                || fallActive
                || handlePullActive
                || arrangePullActive;
        }

        private static bool SceneDragInProgress()
        {
            return leftMouseButtonDown;
        }

        internal static void EnsureHooked()
        {
            var now = EditorApplication.timeSinceStartup;
            var firstHook = !hooked;
            if (!firstHook && now - lastHookValidationTime < HookValidationIntervalSeconds)
            {
                return;
            }

            lastHookValidationTime = now;
            SceneView.duringSceneGui -= DuringSceneGui;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;

            SceneView.duringSceneGui += DuringSceneGui;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            if (!firstHook)
            {
                return;
            }

            hooked = true;
            RecoverInterruptedPhysicsSession();
            RebuildSelection();
        }

        internal static void ResetHandleToSelection(bool repaint = true)
        {
            var roots = GetLiveSelectionRoots();
            if (roots.Length == 0)
            {
                hasHandle = false;
                return;
            }

            handlePosition = TryGetCombinedBounds(roots, out var bounds) ? bounds.center : AveragePosition(roots);
            handleRotation = ResolveTransformHandleRotation(roots);
            hasHandle = true;
            if (repaint)
            {
                SceneView.RepaintAll();
            }
        }

        private static Quaternion ResolveTransformHandleRotation(Transform[] roots)
        {
            if (PhysicsAssistMasterEnabled || Tools.pivotRotation == PivotRotation.Global)
            {
                return Quaternion.identity;
            }

            return roots.Length == 1 && roots[0] != null
                ? roots[0].rotation
                : Quaternion.identity;
        }

        internal static void DropSelectionOnce()
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            var roots = GetLiveSelectionRoots();
            if (roots.Length == 0)
            {
                return;
            }

            var gravity = GetSettings().drop.gravity.normalized;
            if (gravity.sqrMagnitude <= Epsilon)
            {
                gravity = Vector3.down;
            }

            const string undoName = "RealTransforms Drop Once";
            Undo.RegisterCompleteObjectUndo(roots.Cast<Object>().ToArray(), undoName);
            Undo.FlushUndoRecordObjects();
            MoveRootsAsGroup(roots, gravity * GetSettings().collisionIndicatorDistance, true);
            ResetHandleToSelection();
        }

        internal static void RefreshToolState()
        {
            if (!EditorPhysicsAllowed)
            {
                SuspendForPlayMode();
                return;
            }

            StopContinuousModes(false);
            RestoreDuplicateIgnoredCollisions();
            ClearSceneProxies(true);
            RecoverInterruptedPhysicsSession();
            CleanupStaleTemporaryPhysicsComponents(true);
            hasSmoothedPullTarget = false;
            hasMovePullTarget = false;
            hasPullTarget = false;
            ResetPlacementOverlayTracking();
            RebuildSelection();
            MarkSceneProxyScanDirty();
            if (Enabled)
            {
                RefreshToolVisibility();
            }

            lastUpdateTime = EditorApplication.timeSinceStartup;
            RepaintActiveSceneView();
        }

        private static RealPhysicsPlacerSettings GetSettings()
        {
            if (settings == null)
            {
                settings = RealPhysicsPlacerSettings.GetOrCreate();
            }

            return settings;
        }

        private static void OnSelectionChanged()
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            var previousRoots = selectedRoots.Where(root => root != null).ToArray();
            var currentRoots = ReadSelectionRoots();
            var hadTransientState = HasTransientEditorPhysicsState();
            var isDuplicateSelection = SelectionLooksLikeRecentDuplicate(previousRoots, currentRoots);
            var duplicateHasCopiedTemporaryState = isDuplicateSelection && AnyRootHasCopiedTemporaryPhysicsState(currentRoots);
            var duplicateCanSuppressCollisions = duplicateHasCopiedTemporaryState
                || isDuplicateSelection && SelectionLooksLikeFreshDuplicatePose(previousRoots, currentRoots);
            if (!isDuplicateSelection)
            {
                ClearDuplicateCommandUndoFence();
                ClearDuplicatePhysicsSuppression();
            }

            var duplicateNeedsInputRelease = isDuplicateSelection
                && (leftMouseButtonDown
                    || fallActive
                    || handlePullActive
                    || placementOverlayPullActive
                    || rotationPullActive
                    || rotationUprightPullActive
                    || arrangePullActive
                    || arrangeRotationPullActive
                    || arrangeRandomSpinActive
                    || arrangeEvenSpacingActive
                    || arrangeUprightPullActive
                    || physXSessionActive);
            var duplicateNeedsActionRelease = isDuplicateSelection
                && (fallActive
                    || rotationUprightPullActive
                    || arrangeUprightPullActive
                    || arrangeRandomSpinActive
                    || arrangeEvenSpacingActive);
            if (isDuplicateSelection && duplicateCanSuppressCollisions)
            {
                IgnoreDuplicateCollisionsWithRoots(previousRoots, currentRoots);
            }

            RestoreDuplicatedPhysicsState(previousRoots, currentRoots);
            if (isDuplicateSelection && duplicateCanSuppressCollisions)
            {
                IgnoreDuplicateCollisionsWithRoots(previousRoots, currentRoots);
            }

            if (!Enabled && !PhysicsAssistEnabled && !hadTransientState)
            {
                selectedRoots = currentRoots;
                hasHandle = false;
                if (isDuplicateSelection)
                {
                    StopContinuousModesForDuplicateSelection();
                    RemoveCopiedTemporaryPhysicsComponents(currentRoots);
                }

                return;
            }

            if (isDuplicateSelection)
            {
                StopContinuousModesForDuplicateSelection();
            }
            else
            {
                StopContinuousModes(false);
            }

            ClearSelectionProxyColliderPool();
            CleanupStaleTemporaryPhysicsComponents(true);
            CleanupOrphanedSelectionProxyColliders();
            selectedRoots = currentRoots;
            ResetPlacementOverlayTracking();
            ResetArrangeState();
            ResetHandleToSelection();
            if (Enabled || PhysicsAssistEnabled)
            {
                QueueSurfaceShellPrewarm(selectedRoots);
            }

            RemoveCopiedTemporaryPhysicsComponents(selectedRoots);
            CleanupOrphanedSelectionProxyColliders();
            MarkDuplicateOverlapSuppression(previousRoots, selectedRoots, duplicateCanSuppressCollisions);
            if (duplicateNeedsInputRelease && duplicateCanSuppressCollisions)
            {
                MarkDuplicatePhysicsSuppression(selectedRoots, duplicateNeedsActionRelease);
            }

            if (Proxies.Count > 0 || SceneProxyCache.Count > 0 || proxyRoot != null)
            {
                ClearSceneProxies(true);
            }

            if (Enabled || PhysicsAssistEnabled)
            {
                MarkSceneProxyScanDirty();
            }
        }

        private static void CaptureToolOrientation()
        {
            if (hasPreviousPivotRotation)
            {
                return;
            }

            previousPivotRotation = Tools.pivotRotation;
            previousPivotMode = Tools.pivotMode;
            hasPreviousPivotRotation = true;
        }

        private static void ForceGlobalToolOrientation()
        {
            if (PhysicsAssistMasterEnabled)
            {
                if (Tools.pivotRotation != PivotRotation.Global)
                {
                    Tools.pivotRotation = PivotRotation.Global;
                }

                if (SelectionCount > 1 && Tools.pivotMode != PivotMode.Center)
                {
                    Tools.pivotMode = PivotMode.Center;
                }
            }
        }

        private static void RestoreToolOrientation()
        {
            if (!hasPreviousPivotRotation)
            {
                return;
            }

            Tools.pivotRotation = previousPivotRotation;
            Tools.pivotMode = previousPivotMode;
            hasPreviousPivotRotation = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                SuspendForPlayMode();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                suspendedForPlayMode = false;
                RecoverInterruptedPhysicsSession();
                RebuildSelection();
                if (Enabled)
                {
                    MarkSceneProxyScanDirty();
                    RefreshToolVisibility();
                }
            }
        }

        private static void OnEditorQuitting()
        {
            StopContinuousModes(false);
            RestoreDuplicateIgnoredCollisions();
            ClearSceneProxies(true);
            CloseIsolatedPhysicsScene();
            ClearSelectionProxyColliderPool();
        }

        private static void SuspendForPlayMode()
        {
            if (suspendedForPlayMode
                && !physXSessionActive
                && Proxies.Count == 0
                && SceneProxyCache.Count == 0)
            {
                return;
            }

            suspendedForPlayMode = true;
            StopContinuousModes(false);
            ClearSceneProxies(true);
            CloseIsolatedPhysicsScene();
            ClearSelectionProxyColliderPool();
            CleanupOrphanedSelectionProxyColliders();
            RestoreFrozenSceneBodies();
            ResetPlacementOverlayTracking();
            ResetArrangeState();
            ResetRotationPhysicsTargets();
            ResetArrangePhysicsTargets();
            ResetArrangePhysicsRotationTargets();
            ClearDuplicatePhysicsSuppression();
            selectedRoots = Array.Empty<Transform>();
            hasHandle = false;
            leftMouseButtonDown = false;
            rightMouseButtonDown = false;
            assistKeyHeld = false;
            shiftToggleHeld = false;
            shiftToggleChorded = false;
            disablePhysicsAssistWhenDragEnds = false;
            Tools.hidden = previousToolsHidden;
            RestoreToolOrientation();
            EditorPrefs.SetBool(ActivePhysicsSessionKey, false);
            SceneView.RepaintAll();
        }

        private static void OnEditorHierarchyChanged()
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            var sceneProxyRelevant = SceneProxyFeatureEnabled() || HasSceneProxyState();
            if (sceneProxyRelevant)
            {
                sceneRendererCacheDirty = true;
                SceneRendererProxyMetadataCache.Clear();
            }

            if (physXSessionActive)
            {
                activeDuplicatePhysicsSweepDirty = true;
                RemoveCopiedPhysicsFromActiveSessionDuplicates();
            }

            if (!Enabled && !PhysicsAssistEnabled && !HasTransientEditorPhysicsState())
            {
                return;
            }

            if (!physXSessionActive)
            {
                if (ShouldCleanupTemporaryPhysicsOnHierarchyChange())
                {
                    CleanupStaleTemporaryPhysicsComponents(false);
                }

                if (HasSelectionProxyColliderState())
                {
                    CleanupOrphanedSelectionProxyColliders();
                }
            }

            if (sceneProxyRelevant
                && !suppressSceneProxyHierarchyDirty
                && !physXSessionActive
                && EditorApplication.timeSinceStartup >= suppressSceneProxyHierarchyDirtyUntil)
            {
                MarkSceneProxyScanDirty();
            }
        }

        private static bool ShouldCleanupTemporaryPhysicsOnHierarchyChange()
        {
            return PhysXBodies.Count > 0
                || hasPreviousSimulationMode
                || EditorPrefs.GetBool(ActivePhysicsSessionKey, false);
        }

        private static void RecoverInterruptedPhysicsSession()
        {
            ClearSceneProxies(true);
            CleanupOrphanedSelectionProxyColliders();
            if (!EditorPrefs.GetBool(ActivePhysicsSessionKey, false))
            {
                CleanupStaleTemporaryPhysicsComponents(true);
                return;
            }

            if (Physics.simulationMode == SimulationMode.Script)
            {
                Physics.simulationMode = SimulationMode.FixedUpdate;
            }

            EditorPrefs.SetBool(ActivePhysicsSessionKey, false);
            CleanupStaleTemporaryPhysicsComponents(true);
        }

        private static void RebuildSelection()
        {
            if (!EditorPhysicsAllowed)
            {
                selectedRoots = Array.Empty<Transform>();
                hasHandle = false;
                return;
            }

            selectedRoots = ReadSelectionRoots();
            if (Enabled || PhysicsAssistEnabled || HasTransientEditorPhysicsState())
            {
                CleanupStaleTemporaryPhysicsComponents();
            }

            ResetPlacementOverlayTracking();
            ResetArrangeState();
            if (!Enabled && !PhysicsAssistEnabled)
            {
                hasHandle = false;
                return;
            }

            ResetHandleToSelection();
            if (Enabled || PhysicsAssistEnabled)
            {
                QueueSurfaceShellPrewarm(selectedRoots);
            }
        }

        private static Transform[] ReadSelectionRoots()
        {
            if (!EditorPhysicsAllowed)
            {
                return Array.Empty<Transform>();
            }

            var roots = Selection.transforms
                .Where(transform => transform != null && transform.gameObject.scene.IsValid())
                .Where(transform => (transform.hideFlags & HideFlags.HideAndDontSave) == 0)
                .ToArray();

            return roots
                .Where(transform => !roots.Any(other => other != transform && transform.IsChildOf(other)))
                .OrderBy(transform => transform.GetEntityId())
                .ToArray();
        }

        private static Transform[] GetLiveSelectionRoots()
        {
            if (!EditorPhysicsAllowed)
            {
                selectedRoots = Array.Empty<Transform>();
                return selectedRoots;
            }

            Transform[] currentRoots = null;
            if (ShouldForceLiveSelectionRefresh())
            {
                currentRoots = ReadSelectionRoots();
            }

            if (currentRoots == null && CachedSelectionRootsAreValid())
            {
                return selectedRoots;
            }

            currentRoots ??= ReadSelectionRoots();
            selectedRoots = selectedRoots.Where(root => root != null).ToArray();
            if (!SelectionRootsMatch(selectedRoots, currentRoots))
            {
                var previousRoots = selectedRoots;
                var isDuplicateSelection = SelectionLooksLikeRecentDuplicate(previousRoots, currentRoots);
                var duplicateHasCopiedTemporaryState = isDuplicateSelection && AnyRootHasCopiedTemporaryPhysicsState(currentRoots);
                var duplicateCanSuppressCollisions = duplicateHasCopiedTemporaryState
                    || isDuplicateSelection && SelectionLooksLikeFreshDuplicatePose(previousRoots, currentRoots);
                var duplicateNeedsInputRelease = isDuplicateSelection
                    && (leftMouseButtonDown
                        || fallActive
                        || handlePullActive
                        || placementOverlayPullActive
                        || rotationPullActive
                        || rotationUprightPullActive
                        || arrangePullActive
                        || arrangeRotationPullActive
                        || arrangeRandomSpinActive
                        || arrangeEvenSpacingActive
                        || arrangeUprightPullActive
                        || physXSessionActive);
                var duplicateNeedsActionRelease = isDuplicateSelection
                    && (fallActive
                        || rotationUprightPullActive
                        || arrangeUprightPullActive
                        || arrangeRandomSpinActive
                        || arrangeEvenSpacingActive);

                if (isDuplicateSelection)
                {
                    if (duplicateCanSuppressCollisions)
                    {
                        IgnoreDuplicateCollisionsWithRoots(previousRoots, currentRoots);
                    }

                    RestoreDuplicatedPhysicsState(previousRoots, currentRoots);
                    if (duplicateCanSuppressCollisions)
                    {
                        IgnoreDuplicateCollisionsWithRoots(previousRoots, currentRoots);
                    }
                }
                else
                {
                    ClearDuplicateCommandUndoFence();
                    ClearDuplicatePhysicsSuppression();
                }

                if (isDuplicateSelection)
                {
                    StopContinuousModesForDuplicateSelection();
                }
                else
                {
                    StopContinuousModes(false);
                }

                selectedRoots = currentRoots;
                CleanupStaleTemporaryPhysicsComponents();
                RemoveCopiedTemporaryPhysicsComponents(selectedRoots);
                RemoveCopiedPhysicsFromActiveSessionDuplicates(selectedRoots);
                ResetPlacementOverlayTracking();
                ResetArrangeState();
                hasHandle = false;
                if (duplicateNeedsInputRelease && duplicateCanSuppressCollisions)
                {
                    MarkDuplicatePhysicsSuppression(selectedRoots, duplicateNeedsActionRelease);
                }

                ClearSceneProxies(true);
                MarkSceneProxyScanDirty();
            }

            return selectedRoots;
        }

        private static bool ShouldForceLiveSelectionRefresh()
        {
            return physXSessionActive
                || HasActiveContinuousMode()
                || activeDuplicatePhysicsSweepDirty
                || DuplicatePhysicsSuppressRoots.Count > 0;
        }

        private static bool CachedSelectionRootsAreValid()
        {
            if (selectedRoots == null)
            {
                selectedRoots = Array.Empty<Transform>();
                return true;
            }

            for (var i = 0; i < selectedRoots.Length; i++)
            {
                var root = selectedRoots[i];
                if (root == null
                    || !root.gameObject.scene.IsValid()
                    || (root.hideFlags & HideFlags.HideAndDontSave) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SelectionRootsMatch(Transform[] left, Transform[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void DuringSceneGui(SceneView sceneView)
        {
            EnsureHooked();
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            var evt = Event.current;
            TrackSceneNavigation(evt);
            HandleSceneInput(evt);

            if (!Enabled || (!PhysicsAssistEnabled && !placementOverlayHandoffActive))
            {
                return;
            }

            ForceGlobalToolOrientation();
            SyncModeToUnityTool();
            RefreshToolVisibility();
            var roots = GetLiveSelectionRoots();
            if (roots.Length == 0)
            {
                DrawPhysicsAssistBadge();
                return;
            }

            if (ShouldRefreshSceneProxies(roots))
            {
                RebuildSceneProxies();
            }

            if (!hasHandle)
            {
                ResetHandleToSelection();
            }

            DrawModeHandles(roots);
            UpdatePlacementPhysicsOverlay(roots);

            if (GetSettings().displayCollisionIndicators && evt.type == EventType.Repaint)
            {
                DrawCollisionIndicators(roots);
            }

            if (evt.rawType == EventType.MouseUp)
            {
                if (Mode == RealPhysicsPlacerMode.Alignment)
                {
                    FinishArrangeDrag();
                }

                if (Mode == RealPhysicsPlacerMode.Rotation && rotationPullActive)
                {
                    StopContinuousModes();
                    ResetHandleToSelection();
                }

                if (GetSettings().move.readjustHandleOnMouseUp)
                {
                    if (placementOverlayPullActive || handlePullActive || rotationPullActive)
                    {
                        StopContinuousModes();
                    }

                    ResetHandleToSelection();
                }

                RefreshToolVisibility();
            }

            DrawPhysicsAssistBadge();
        }

        private static void TrackSceneNavigation(Event evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt.rawType == EventType.MouseDown
                || evt.rawType == EventType.MouseUp
                || evt.type == EventType.MouseDrag
                || evt.type == EventType.ScrollWheel)
            {
                lastSceneMouseEventTime = EditorApplication.timeSinceStartup;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                leftMouseButtonDown = true;
                SeedPlacementOverlayTrackingForDrag();
            }
            else if (evt.rawType == EventType.MouseUp && evt.button == 0)
            {
                leftMouseButtonDown = false;
                RefreshDuplicateIgnoredCollisions();
                if (!duplicatePhysicsSuppressActionKey)
                {
                    ClearDuplicatePhysicsSuppression();
                }

                if (disablePhysicsAssistWhenDragEnds)
                {
                    disablePhysicsAssistWhenDragEnds = false;
                    Enabled = false;
                }
            }
            else if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                rightMouseButtonDown = true;
                StopKeyboardActionsForSceneNavigation();
                if (shiftToggleHeld)
                {
                    shiftToggleChorded = true;
                }
            }
            else if (evt.rawType == EventType.MouseUp && evt.button == 1)
            {
                rightMouseButtonDown = false;
            }
            else if ((evt.type == EventType.MouseDrag || evt.type == EventType.ScrollWheel) && shiftToggleHeld)
            {
                shiftToggleChorded = true;
            }
        }

        private static void StopKeyboardActionsForSceneNavigation()
        {
            if (!fallActive
                && !rotationUprightPullActive
                && !arrangeUprightPullActive
                && !arrangeEvenSpacingActive
                && !arrangeRandomSpinActive)
            {
                return;
            }

            StopContinuousModes(false);
            ResetArrangeState();
            ResetHandleToSelection();
            RefreshToolVisibility();
            SceneView.RepaintAll();
        }

        private static void SeedPlacementOverlayTrackingForDrag()
        {
            if (!Enabled
                || Mode != RealPhysicsPlacerMode.Placement
                || !PhysicsAssistEnabled
                || placementOverlayPullActive
                || placementOverlayHandoffActive
                || handlePullActive
                || fallActive)
            {
                return;
            }

            var roots = GetLiveSelectionRoots();
            if (roots.Length > 0)
            {
                RefreshSceneProxiesIfNeeded(roots);
                TrackPlacementOverlayPositions(roots);
            }
        }

        private static void HandleSceneInput(Event evt)
        {
            if (evt == null)
            {
                return;
            }

            PrepareForUnityUndoRedoShortcut(evt);
            PrepareForUnityDuplicateCommand(evt);

            if (TryHandlePhysicsAssistShortcut(evt))
            {
                return;
            }

            if (EditorGUIUtility.editingTextField)
            {
                return;
            }

            if (IsShiftKey(evt.keyCode))
            {
                HandleShiftToggle(evt);
                return;
            }

            if (shiftToggleHeld && evt.type == EventType.KeyDown)
            {
                shiftToggleChorded = true;
            }

            if (TryHandleRandomSpinShortcut(evt))
            {
                return;
            }

            if (TryHandleArrangeEvenSpacingShortcut(evt))
            {
                return;
            }

            if (TryHandleUnityToolShortcut(evt))
            {
                return;
            }

            TryHandlePrimaryActionShortcut(evt);
        }

        internal static void FocusSceneViewForControlPanelInput()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                SceneView.RepaintAll();
                return;
            }

            sceneView.Focus();
            sceneView.Repaint();
        }

        internal static bool TryHandleControlPanelActionShortcut(Event evt)
        {
            if (evt == null
                || EditorGUIUtility.editingTextField
                || !PhysicsAssistMasterEnabled
                || !TryGetShortcutEvent(evt, out var actionKey, out _, out _)
                || !IsActivationKey(actionKey)
                || evt.alt
                || evt.control
                || evt.command)
            {
                return false;
            }

            if (Mode == RealPhysicsPlacerMode.Alignment && TryHandleArrangeEvenSpacingShortcut(evt))
            {
                return true;
            }

            return TryHandlePrimaryActionShortcut(evt);
        }

        private static bool TryHandlePrimaryActionShortcut(Event evt)
        {
            if (!PhysicsAssistMasterEnabled
                || !TryGetShortcutEvent(evt, out var actionKey, out var actionPressed, out var actionReleased)
                || !IsActivationKey(actionKey))
            {
                return false;
            }

            if (actionReleased)
            {
                var activeAction = fallActive
                    || handlePullActive
                    || rotationUprightPullActive
                    || arrangeUprightPullActive
                    || arrangeEvenSpacingActive;
                if (activeAction)
                {
                    StopContinuousModes();
                    evt.Use();
                }

                ClearDuplicatePhysicsSuppression();
                return activeAction;
            }

            if (!actionPressed || rightMouseButtonDown)
            {
                return false;
            }

            if (ShouldSuppressDuplicatePhysics(GetLiveSelectionRoots()))
            {
                evt.Use();
                return true;
            }

            if (TryStartUprightRotationPull())
            {
                evt.Use();
                return true;
            }

            StartDropMode();
            evt.Use();
            return true;
        }

        private static bool TryHandleUnityToolShortcut(Event evt)
        {
            if (!TryGetShortcutEvent(evt, out var eventKey, out var pressed, out _)
                || !pressed
                || !PhysicsAssistMasterEnabled
                || evt.alt
                || evt.control
                || evt.command
                || rightMouseButtonDown)
            {
                return false;
            }

            if (!TryGetToolModeForShortcut(eventKey, out var unityTool, out var nextMode))
            {
                return false;
            }

            Tools.current = unityTool;
            Mode = nextMode;
            SceneView.RepaintAll();
            evt.Use();
            return true;
        }

        private static bool TryHandlePhysicsAssistShortcut(Event evt)
        {
            if (!TryGetShortcutEvent(evt, out var eventKey, out var pressed, out var released)
                || evt.alt
                || evt.control
                || evt.command)
            {
                return false;
            }

            var activeSettings = GetSettings();
            if (!IsConfiguredKey(activeSettings.physicsAssistKey, eventKey))
            {
                return false;
            }

            if (EditorGUIUtility.editingTextField && activeSettings.physicsAssistKey != KeyCode.CapsLock)
            {
                return false;
            }

            lastAssistKeyEventTime = EditorApplication.timeSinceStartup;

            if (released)
            {
                assistKeyHeld = false;
                if (activeSettings.assistKeyBehavior == RealPhysicsPlacerAssistKeyBehavior.Hold)
                {
                    SetPhysicsAssistMasterEnabled(false);
                }

                evt.Use();
                return true;
            }

            if (!pressed || rightMouseButtonDown)
            {
                return false;
            }

            if (activeSettings.assistKeyBehavior == RealPhysicsPlacerAssistKeyBehavior.Hold)
            {
                assistKeyHeld = true;
                if (!PhysicsAssistMasterEnabled)
                {
                    SetPhysicsAssistMasterEnabled(true);
                }

                evt.Use();
                return true;
            }

            if (!assistKeyHeld)
            {
                assistKeyHeld = true;
                SetPhysicsAssistMasterEnabled(!PhysicsAssistMasterEnabled);
            }

            evt.Use();
            return true;
        }

        private static bool TryGetToolModeForShortcut(KeyCode keyCode, out Tool unityTool, out RealPhysicsPlacerMode mode)
        {
            var activeSettings = GetSettings();
            if (IsConfiguredKey(activeSettings.moveToolKey, keyCode))
            {
                unityTool = Tool.Move;
                mode = RealPhysicsPlacerMode.Placement;
                return true;
            }

            if (IsConfiguredKey(activeSettings.arrangeToolKey, keyCode))
            {
                unityTool = Tool.Scale;
                mode = RealPhysicsPlacerMode.Alignment;
                return true;
            }

            if (IsConfiguredKey(activeSettings.rotateToolKey, keyCode))
            {
                unityTool = Tool.Rotate;
                mode = RealPhysicsPlacerMode.Rotation;
                return true;
            }

            unityTool = default;
            mode = default;
            return false;
        }

        private static void SyncModeToUnityTool()
        {
            if (!PhysicsAssistMasterEnabled || fallActive)
            {
                return;
            }

            switch (Tools.current)
            {
                case Tool.Move:
                    SetModeFromUnityTool(RealPhysicsPlacerMode.Placement);
                    break;
                case Tool.Rotate:
                    SetModeFromUnityTool(RealPhysicsPlacerMode.Rotation);
                    break;
                case Tool.Scale:
                    SetModeFromUnityTool(RealPhysicsPlacerMode.Alignment);
                    break;
            }
        }

        private static void SyncUnityToolToMode(RealPhysicsPlacerMode mode)
        {
            if (!PhysicsAssistMasterEnabled)
            {
                return;
            }

            switch (mode)
            {
                case RealPhysicsPlacerMode.Placement:
                    Tools.current = Tool.Move;
                    break;
                case RealPhysicsPlacerMode.Rotation:
                    Tools.current = Tool.Rotate;
                    break;
                case RealPhysicsPlacerMode.Alignment:
                    Tools.current = Tool.Scale;
                    break;
            }
        }

        private static void SetModeFromUnityTool(RealPhysicsPlacerMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
        }

        private static void HandleShiftToggle(Event evt)
        {
            if (evt.type == EventType.KeyDown && !shiftToggleHeld)
            {
                shiftToggleHeld = true;
                shiftToggleChorded = rightMouseButtonDown || evt.alt || evt.control || evt.command;
                shiftToggleStartTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (evt.rawType != EventType.KeyUp || !shiftToggleHeld)
            {
                return;
            }

            var tapDuration = EditorApplication.timeSinceStartup - shiftToggleStartTime;
            var shouldToggle = !shiftToggleChorded && !rightMouseButtonDown && tapDuration <= 0.45f;
            shiftToggleHeld = false;
            shiftToggleChorded = false;

            if (!shouldToggle)
            {
                return;
            }

            shiftToggleHeld = false;
            shiftToggleChorded = false;
        }

        private static bool IsShiftKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.LeftShift || keyCode == KeyCode.RightShift;
        }

        private static bool IsActivationKey(KeyCode keyCode)
        {
            return IsConfiguredKey(GetSettings().actionKey, keyCode);
        }

        private static bool TryGetShortcutEvent(Event evt, out KeyCode keyCode, out bool pressed, out bool released)
        {
            keyCode = KeyCode.None;
            pressed = false;
            released = false;
            if (evt == null)
            {
                return false;
            }

            if (evt.type == EventType.KeyDown)
            {
                keyCode = evt.keyCode;
                pressed = keyCode != KeyCode.None;
                return pressed;
            }

            if (evt.rawType == EventType.KeyUp)
            {
                keyCode = evt.keyCode;
                released = keyCode != KeyCode.None;
                return released;
            }

            if (evt.type == EventType.MouseDown && TryMouseButtonToKeyCode(evt.button, out keyCode))
            {
                pressed = true;
                return true;
            }

            if (evt.rawType == EventType.MouseUp && TryMouseButtonToKeyCode(evt.button, out keyCode))
            {
                released = true;
                return true;
            }

            return false;
        }

        private static bool TryMouseButtonToKeyCode(int mouseButton, out KeyCode keyCode)
        {
            if (mouseButton >= 0 && mouseButton <= 6)
            {
                keyCode = (KeyCode)((int)KeyCode.Mouse0 + mouseButton);
                return true;
            }

            keyCode = KeyCode.None;
            return false;
        }

        private static bool IsConfiguredKey(KeyCode configuredKey, KeyCode eventKey)
        {
            return configuredKey != KeyCode.None && configuredKey == eventKey;
        }

        private static string FormatShortcutKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.None ? "Unbound" : keyCode.ToString();
        }

        private static void OnEditorUpdate()
        {
            EnsureHooked();
            if (!EditorPhysicsAllowed)
            {
                SuspendForPlayMode();
                lastUpdateTime = EditorApplication.timeSinceStartup;
                return;
            }

            suspendedForPlayMode = false;
            RefreshDuplicatePhysicsSuppression();
            RefreshDuplicateIgnoredCollisions();
            if (!Enabled && !PhysicsAssistEnabled && !HasTransientEditorPhysicsState() && !HasTrackedEditorInputState())
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (HasTrackedEditorInputState())
            {
                RecoverStaleMouseState();
                RecoverStaleAssistKeyState();
            }

            if (Enabled || PhysicsAssistEnabled)
            {
                ProcessSurfaceShellPrewarm();
            }

            var placementOverlayActive = Enabled
                && Mode == RealPhysicsPlacerMode.Placement
                && (placementOverlayPullActive || placementOverlayHandoffActive);
            if (!Enabled
                || (!fallActive
                    && !handlePullActive
                    && !rotationPullActive
                    && !arrangePullActive
                    && !arrangeRotationPullActive
                    && !arrangeRandomSpinActive
                    && !placementOverlayActive))
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - lastUpdateTime), 0f, 0.05f);
            lastUpdateTime = now;

            if (deltaTime <= 0f)
            {
                return;
            }

            if (Mode == RealPhysicsPlacerMode.Alignment
                && (arrangePullActive || arrangeRotationPullActive)
                && !leftMouseButtonDown
                && !arrangeUprightPullActive
                && !arrangeEvenSpacingActive
                && !arrangeRandomSpinActive)
            {
                FinishArrangeDrag();
                return;
            }

            if (Mode == RealPhysicsPlacerMode.Rotation && rotationPullActive && !leftMouseButtonDown && !rotationUprightPullActive)
            {
                StopContinuousModes();
                ResetHandleToSelection();
                return;
            }

            Transform[] updateRoots = null;
            Transform[] GetUpdateRoots()
            {
                return updateRoots ??= GetLiveSelectionRoots();
            }

            if (Enabled && Mode == RealPhysicsPlacerMode.Placement && (PhysicsAssistEnabled || placementOverlayPullActive) && !placementOverlayHandoffActive)
            {
                var roots = GetUpdateRoots();
                RefreshSceneProxiesIfNeeded(roots);
                UpdatePlacementPhysicsOverlay(roots);
            }

            if (fallActive)
            {
                var roots = GetUpdateRoots();
                RefreshSceneProxiesIfNeeded(roots);
                StepPhysX(deltaTime, false, roots);
            }

            if (handlePullActive || placementOverlayPullActive || rotationPullActive || arrangePullActive || arrangeRotationPullActive || arrangeRandomSpinActive)
            {
                var roots = GetUpdateRoots();
                RefreshSceneProxiesIfNeeded(roots);
                StepPhysX(deltaTime, true, roots);
            }

            if (placementOverlayPullActive)
            {
                TrackPlacementOverlayPositions(GetUpdateRoots());
            }

            Physics.SyncTransforms();
            if (!arrangePullActive && !arrangeRotationPullActive)
            {
                ResetHandleToSelection(false);
            }

            SceneView.RepaintAll();
        }

        private static void RecoverStaleAssistKeyState()
        {
            if (!assistKeyHeld
                || GetSettings().assistKeyBehavior != RealPhysicsPlacerAssistKeyBehavior.Toggle
                || lastAssistKeyEventTime < 0d)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - lastAssistKeyEventTime > 0.75d)
            {
                assistKeyHeld = false;
            }
        }

        private static void RecoverStaleMouseState()
        {
            if (!leftMouseButtonDown && !rightMouseButtonDown && !shiftToggleHeld && !assistKeyHeld)
            {
                return;
            }

            if (lastSceneMouseEventTime < 0d)
            {
                return;
            }

            var staleFor = EditorApplication.timeSinceStartup - lastSceneMouseEventTime;
            if (staleFor < StaleMouseStateSeconds)
            {
                return;
            }

            if (GUIUtility.hotControl != 0 && staleFor < ForceStaleMouseStateSeconds)
            {
                return;
            }

            leftMouseButtonDown = false;
            rightMouseButtonDown = false;
            shiftToggleHeld = false;
            assistKeyHeld = false;
            shiftToggleChorded = false;
            disablePhysicsAssistWhenDragEnds = false;
            RestoreDuplicateIgnoredCollisions();
            RefreshToolVisibility();
            SceneView.RepaintAll();
        }

    }
}
