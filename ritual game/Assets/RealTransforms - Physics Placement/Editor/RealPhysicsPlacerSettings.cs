﻿using System.Collections.Generic;
using System.IO;
using System;
using RealPhysicsPlacer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace RealPhysicsPlacer.EditorTools
{
    /// <summary>
    /// Scene-view tool modes shown in the RealTransforms window.
    /// </summary>
    public enum RealPhysicsPlacerMode
    {
        Placement,
        Rotation,
        Alignment,
        Fall
    }

    /// <summary>
    /// Defines whether the physics assist shortcut toggles or acts while held.
    /// </summary>
    public enum RealPhysicsPlacerAssistKeyBehavior
    {
        Toggle,
        Hold
    }

    /// <summary>
    /// Actions available from the Arrange mode shortcut.
    /// </summary>
    public enum RealPhysicsPlacerArrangeAction
    {
        [InspectorName("None")]
        None,
        [InspectorName("Pack X")]
        PackX,
        [InspectorName("Pack Z")]
        PackZ,
        [InspectorName("Distribute X")]
        DistributeX,
        [InspectorName("Distribute Z")]
        DistributeZ,
        [InspectorName("Grid XZ")]
        GridXZ,
        [InspectorName("Clump")]
        Clump,
        [InspectorName("Spread")]
        Spread
    }

    /// <summary>
    /// Preset quality targets used by the Physics Settings tab.
    /// </summary>
    public enum RealPhysicsPlacerPhysicsPreset
    {
        Performant,
        Balanced,
        Accurate
    }

    /// <summary>
    /// Rotation axes that temporary physics bodies should not rotate around.
    /// </summary>
    [Flags]
    public enum RealPhysicsPlacerRotationConstraint
    {
        [InspectorName("None")]
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2
    }

    /// <summary>
    /// Pivot placement used when combining selected objects into a baked asset.
    /// </summary>
    public enum RealPhysicsPlacerObjectBakePivot
    {
        SelectionCenter,
        FirstSelected
    }

    /// <summary>
    /// Rotation basis used when combining selected objects into a baked asset.
    /// </summary>
    public enum RealPhysicsPlacerObjectBakeRotation
    {
        [InspectorName("World Space")]
        WorldSpace,
        [InspectorName("First Selected Direction")]
        FirstSelectedDirection,
        [InspectorName("Average Direction")]
        AverageDirection
    }

    /// <summary>
    /// Serialized editor settings for RealTransforms. The custom editor window
    /// presents these fields with grouped controls and explanatory tooltips.
    /// </summary>
    public sealed class RealPhysicsPlacerSettings : ScriptableObject
    {
        // Unity serializes this asset directly. Keep field names stable unless a
        // migration is added, because users may already have saved settings.
        internal static string AssetPath => RealPhysicsPlacerAssetPaths.SettingsAssetPath;
        internal const int BakerLodCount = 7;
        internal const int LowestBakerLodMask = 0;
        internal const int DefaultBakerLodMask = LowestBakerLodMask;
        internal const int AllBakerLodMask = (1 << BakerLodCount) - 1;
        private const int CurrentSettingsVersion = 77;
        internal const RealPhysicsPlacerRotationConstraint AllRotationConstraintAxes =
            RealPhysicsPlacerRotationConstraint.X
            | RealPhysicsPlacerRotationConstraint.Y
            | RealPhysicsPlacerRotationConstraint.Z;

        [HideInInspector] public int settingsVersion;

        [Header("Mode Profiles")]
        public MoveModeSettings move = new MoveModeSettings();
        public RotateModeSettings rotate = new RotateModeSettings();
        public DropModeSettings drop = new DropModeSettings();
        public ArrangeModeSettings arrange = new ArrangeModeSettings();
        // Global physics settings give users one performance control surface.
        // Per-mode profiles remain available for advanced tuning.
        public bool useGlobalPhysicsSettings;
        [Min(0.01f)] public float globalSimulationSpeed = 2f;
        [Range(0.001f, 0.02f)] public float globalPhysicsStep = 0.01f;
        [Range(1, 64)] public int globalMaxSubsteps = 2;
        [Range(6, 96)] public int globalSolverIterations = 8;
        [Range(1, 48)] public int globalSolverVelocityIterations = 4;
        public RealPhysicsPlacerRotationConstraint globalRotationConstraint;

        [Header("Key Bindings")]
        public RealPhysicsPlacerAssistKeyBehavior assistKeyBehavior = RealPhysicsPlacerAssistKeyBehavior.Toggle;
        public KeyCode physicsAssistKey = KeyCode.CapsLock;
        public KeyCode moveToolKey = KeyCode.W;
        public KeyCode rotateToolKey = KeyCode.E;
        public KeyCode arrangeToolKey = KeyCode.R;
        public KeyCode actionKey = KeyCode.Space;
        [HideInInspector]
        public KeyCode arrangeRandomSpinKey = KeyCode.R;

        [Header("Collision")]
        public LayerMask collisionMask = -1;
        public bool includeTriggers;
        public string ignoredTags = string.Empty;
        public bool useRendererBoundsWhenNoCollider = true;
        // Scene proxies are editor-only fallback colliders for renderers that do
        // not have suitable gameplay colliders. They improve contact accuracy but
        // are intentionally opt-in because scanning large scenes can be expensive.
        public bool generateSceneProxyColliders = false;
        public bool useSceneProxyCollidersForAllObjects = true;
        public bool useLocalSceneProxyCache = true;
        [Range(1f, 80f)] public float sceneProxyCacheRadius = 8f;
        [Range(0.5f, 40f)] public float sceneProxyMeshRadius = 3f;
        [Range(0.1f, 20f)] public float sceneProxyCacheRefreshDistance = 1.5f;
        public bool useConvexStaticMeshProxies;
        [FormerlySerializedAs("usePreciseEditorColliders")]
        [FormerlySerializedAs("useContactHulls")]
        public bool useSurfaceShells;
        [FormerlySerializedAs("preciseColliderPieces")]
        [FormerlySerializedAs("contactHullBudget")]
        [Range(32, 2048)] public int surfaceShellBudget = 2048;
        [FormerlySerializedAs("preciseColliderShell")]
        [FormerlySerializedAs("contactHullSkin")]
        [Range(0.002f, 0.08f)] public float surfaceShellSkin = 0.08f;
        [FormerlySerializedAs("movingMeshProxyPieces")]
        [Range(1, 64)] public int fallbackHullPieces = 1;
        [Header("Collider Baker")]
        [HideInInspector] public RealPhysicsPlacerBakedColliderShape bakerColliderShape = RealPhysicsPlacerBakedColliderShape.RealHulls;
        [Range(1, 32)] public int bakerMaxHulls = 6;
        [Range(16, 128)] public int bakerMaxHullVertices = 96;
        [Range(10, 1800)] public int bakerBakeTimeBudgetSeconds = 60;
        [HideInInspector] public int bakerLodMask = DefaultBakerLodMask;
        public bool bakerSmartHullsUseFullBudget = true;
        public RealPhysicsPlacerRealHullsQuality bakerRealHullsQuality = RealPhysicsPlacerRealHullsQuality.Fast;
        [Range(16, 96)] public int bakerRealHullsVoxelResolution = 96;
        [Range(250, 12000)] public int bakerRealHullsSurfaceSamples = 10000;
        [Range(8, 128)] public int bakerRealHullsMaxSplitCandidates = 64;
        [Range(0.01f, 0.15f)] public float bakerRealHullsShapeAccuracy = 0.03f;
        public bool bakerRealHullsMergePass = true;
        public bool bakerRealHullsCacheResults = true;
        public string bakerOutputFolder = string.Empty;
        [HideInInspector]
        [Range(1, 5)] public int bakerSmartHullSplitSearch = 4;
        [HideInInspector]
        [Range(0f, 1f)] public float bakerSmartHullOpenEdgeBias = 0.35f;
        [HideInInspector]
        [Range(0f, 0.25f)] public float bakerSmartHullMinSplitGain;
        public bool bakerMergeSmallHulls;
        [Range(16, 255)] public int bakerSmallHullMergeTriangleLimit = 255;
        [Range(1f, 3f)] public float bakerSmallHullMergeExpansion = 1.2f;
        [Range(-0.03f, 0.03f)] public float bakerColliderPadding;
        public bool bakerCookMatch;
        [Range(0.001f, 0.25f)] public float bakerCookMatchTolerance = 0.035f;
        public bool bakerPreviewResult = true;
        public bool bakerReplaceExisting = true;
        public bool bakerDisableExistingColliders;
        public bool bakerIncludeInactiveChildren;
        public bool bakerSkipPrimitiveMeshes = true;
        public bool bakerAddMeshFilters;
        public bool bakerDrawGizmos;
        public bool bakerDrawWhenUnselected;
        public bool bakerDrawSolidGizmos;
        public bool bakerLogTimings;
        public Color bakerGizmoColor = new Color(1f, 0.55f, 0.08f, 0.85f);
        [Header("Object Baker")]
        public bool objectBakerIncludeInactiveChildren;
        public bool objectBakerExportFbx = true;
        public string objectBakerOutputFolder = string.Empty;
        public RealPhysicsPlacerObjectBakePivot objectBakerPivot = RealPhysicsPlacerObjectBakePivot.SelectionCenter;
        public RealPhysicsPlacerObjectBakeRotation objectBakerRotation = RealPhysicsPlacerObjectBakeRotation.WorldSpace;
        public bool allowAxisSliding;
        [Range(0.005f, 0.15f)] public float skinWidth = 0.15f;
        [Range(0.02f, 0.5f)] public float maxMoveStep = 0.25f;
        public bool useCenterAsPhysicsTruth = true;
        // The isolated scene can reduce interference from unrelated scene physics.
        // It only sees colliders inside the radius, so it is disabled by default.
        public bool useIsolatedPhysicsScene;
        [Range(0.5f, 30f)] public float isolatedPhysicsRadius = 6f;

        [Header("Rigidbody")]
        [Range(0.1f, 25f)] public float temporaryBodyMass = 1f;
        public CollisionDetectionMode temporaryBodyCollisionDetection = CollisionDetectionMode.Discrete;
        public RigidbodyInterpolation temporaryBodyInterpolation = RigidbodyInterpolation.None;
        [Range(1f, 100f)] public float temporaryBodyMaxAngularVelocity = 50f;
        public bool preserveHigherExistingBodySolverIterations = true;
        public bool freezeSceneRigidbodiesDuringAssist = true;
        public bool preserveMovablePrefabParts = true;

        [Header("Indicators")]
        public bool displayCollisionIndicators;
        [Min(0.1f)] public float collisionIndicatorDistance = 5f;
        public Color xAxisColor = new Color(0.95f, 0.22f, 0.18f, 1f);
        public Color yAxisColor = new Color(0.24f, 0.78f, 0.32f, 1f);
        public Color zAxisColor = new Color(0.24f, 0.45f, 1f, 1f);
        public Color blockedColor = new Color(0.18f, 0.47f, 1f, 1f);

        internal static RealPhysicsPlacerSettings GetOrCreate()
        {
            var assetPath = AssetPath;
            var settings = AssetDatabase.LoadAssetAtPath<RealPhysicsPlacerSettings>(assetPath);
            if (settings != null)
            {
                settings.ApplyProductDefaultsIfNeeded();
                return settings;
            }

            var directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                RealPhysicsPlacerAssetPaths.EnsureFolder(directory);
            }

            settings = CreateInstance<RealPhysicsPlacerSettings>();
            settings.ApplyProductDefaultsIfNeeded();
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        internal void ApplyProductDefaultsIfNeeded()
        {
            EnsureModeProfiles();
            if (settingsVersion >= CurrentSettingsVersion)
            {
                return;
            }

            // Migrations are ordered by settings version so existing user assets
            // keep their tuned values while receiving safer new defaults.
            if (settingsVersion <= 0)
            {
                ResetToDefaults();
                return;
            }

            if (settingsVersion < 14)
            {
                MigrateModeProfilesFromLegacy();
            }

            if (settingsVersion < 15)
            {
                MigrateModeProfileAccuracyFromLegacy();
            }

            if (settingsVersion < 16)
            {
                MigrateArrangeProfileFromLegacy();
            }

            if (settingsVersion < 17)
            {
                MigrateRotateProfileToDirectRotate();
            }

            if (settingsVersion < 18)
            {
                MigrateRotateProfileToPenetrationGuard();
            }

            if (settingsVersion < 19)
            {
                MigrateRotateProfileToPhysX();
            }

            if (settingsVersion < 24)
            {
                MigrateSurfaceShellSettings();
            }

            if (settingsVersion < 25)
            {
                MigrateSurfaceShellSettings();
            }

            if (settingsVersion < 26)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 27)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 28)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 29)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 30)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 31)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 32)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 33)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 34)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 35)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 36)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 37)
            {
                MigrateKeyBindings();
            }

            if (settingsVersion < 38)
            {
                MigrateColliderBakeBudget();
            }

            if (settingsVersion < 39)
            {
                MigrateSmallHullMergeSettings();
            }

            if (settingsVersion < 40)
            {
                MigrateBakerLodSettings();
            }

            if (settingsVersion < 41)
            {
                MigrateColliderBakerCleanupSettings();
            }

            if (settingsVersion < 42)
            {
                MigrateArrangeActionSettings();
            }

            if (settingsVersion < 43)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 44)
            {
                MigrateBakerPaddingSettings();
            }

            if (settingsVersion < 45)
            {
                MigrateCookMatchSettings();
            }

            if (settingsVersion < 47)
            {
                MigrateRigidbodySettings();
            }

            if (settingsVersion < 48)
            {
                MigrateCenterTruthSettings();
            }

            if (settingsVersion < 49)
            {
                MigrateIsolatedPhysicsSettings();
            }

            if (settingsVersion < 50)
            {
                MigrateObjectBakerSettings();
            }

            if (settingsVersion < 51)
            {
                MigrateObjectBakerRotationSettings();
            }

            if (settingsVersion < 52)
            {
                MigratePerformanceControlSettings();
            }

            if (settingsVersion < 53)
            {
                MigrateSceneProxyDefaultOff();
            }

            if (settingsVersion < 62)
            {
                MigrateRealHullsSettings();
            }

            if (settingsVersion < 63)
            {
                MigrateProductionBakerRanges();
            }

            if (settingsVersion < 64)
            {
                MigrateProductionBakerOutputDefaults();
            }

            if (settingsVersion < 65)
            {
                MigrateCookMatchDefaultOff();
            }

            if (settingsVersion < 66)
            {
                MigrateRotationConstraintSettings();
            }

            if (settingsVersion < 70)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 71)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 72)
            {
                MigrateLowerColliderBakerRanges();
            }

            if (settingsVersion < 73)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 74)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 75)
            {
                MigrateGeneratedOutputFolders();
            }

            if (settingsVersion < 76)
            {
                MigrateColliderBakerSettings();
            }

            if (settingsVersion < 77)
            {
                MigrateMovablePrefabPartSettings();
            }

            settingsVersion = CurrentSettingsVersion;
            EditorUtility.SetDirty(this);
        }

        internal void EnsureModeProfiles()
        {
            move ??= new MoveModeSettings();
            rotate ??= new RotateModeSettings();
            drop ??= new DropModeSettings();
            arrange ??= new ArrangeModeSettings();
        }

        private void MigrateModeProfilesFromLegacy()
        {
            EnsureModeProfiles();
            move.ResetToDefaults();
            rotate.ResetToDefaults();
            drop.ResetToDefaults();
        }

        private void MigrateModeProfileAccuracyFromLegacy()
        {
            EnsureModeProfiles();
        }

        private void MigrateArrangeProfileFromLegacy()
        {
            EnsureModeProfiles();
            arrange.ResetToDefaults();
        }

        private void MigrateRotateProfileToDirectRotate()
        {
            EnsureModeProfiles();
            rotate.ResetToDefaults();
        }

        private void MigrateRotateProfileToPenetrationGuard()
        {
            EnsureModeProfiles();
            rotate.ResetToDefaults();
        }

        private void MigrateRotateProfileToPhysX()
        {
            EnsureModeProfiles();
            rotate.ResetToDefaults();
        }

        private void MigrateSurfaceShellSettings()
        {
            surfaceShellBudget = Mathf.Clamp(surfaceShellBudget, 32, 2048);
            surfaceShellSkin = Mathf.Clamp(surfaceShellSkin, 0.002f, 0.08f);
            fallbackHullPieces = Mathf.Clamp(fallbackHullPieces, 1, 64);
        }

        private void MigrateColliderBakerSettings()
        {
            if (!Enum.IsDefined(typeof(RealPhysicsPlacerBakedColliderShape), bakerColliderShape))
            {
                bakerColliderShape = RealPhysicsPlacerBakedColliderShape.RealHulls;
            }

            bakerMaxHulls = Mathf.Clamp(bakerMaxHulls <= 0 ? 6 : bakerMaxHulls, 1, 32);
            bakerMaxHullVertices = Mathf.Clamp(bakerMaxHullVertices <= 0 ? 96 : bakerMaxHullVertices, 16, 128);
            MigrateRealHullsSettings();
            bakerSmartHullSplitSearch = Mathf.Clamp(bakerSmartHullSplitSearch <= 0 ? 4 : bakerSmartHullSplitSearch, 1, 5);
            bakerSmartHullOpenEdgeBias = Mathf.Clamp(bakerSmartHullOpenEdgeBias <= 0f ? 0.35f : bakerSmartHullOpenEdgeBias, 0f, 1f);
            bakerSmartHullMinSplitGain = Mathf.Clamp(bakerSmartHullMinSplitGain, 0f, 0.25f);
            MigrateColliderBakeBudget();
            MigrateBakerLodSettings();
            MigrateSmallHullMergeSettings();
            bakerColliderPadding = Mathf.Clamp(bakerColliderPadding, -0.03f, 0.03f);
            MigrateCookMatchSettings();
            if (bakerGizmoColor.a <= 0f)
            {
                bakerGizmoColor = new Color(1f, 0.55f, 0.08f, 0.85f);
            }

            MigrateGeneratedOutputFolders();
        }

        private void MigrateGeneratedOutputFolders()
        {
            bakerOutputFolder = RealPhysicsPlacerAssetPaths.NormalizeGeneratedOutputFolder(bakerOutputFolder);
            objectBakerOutputFolder = RealPhysicsPlacerAssetPaths.NormalizeGeneratedOutputFolder(objectBakerOutputFolder);
        }

        private void MigrateRealHullsSettings()
        {
            if (!Enum.IsDefined(typeof(RealPhysicsPlacerRealHullsQuality), bakerRealHullsQuality))
            {
                bakerRealHullsQuality = RealPhysicsPlacerRealHullsQuality.Fast;
            }

            bakerRealHullsVoxelResolution = Mathf.Clamp(bakerRealHullsVoxelResolution <= 0 ? 96 : bakerRealHullsVoxelResolution, 16, 96);
            bakerRealHullsSurfaceSamples = Mathf.Clamp(bakerRealHullsSurfaceSamples <= 0 ? 10000 : bakerRealHullsSurfaceSamples, 250, 12000);
            bakerRealHullsMaxSplitCandidates = Mathf.Clamp(bakerRealHullsMaxSplitCandidates <= 0 ? 64 : bakerRealHullsMaxSplitCandidates, 8, 128);
            bakerRealHullsShapeAccuracy = Mathf.Clamp(bakerRealHullsShapeAccuracy <= 0f ? 0.03f : bakerRealHullsShapeAccuracy, 0.01f, 0.15f);
        }

        private void MigrateLowerColliderBakerRanges()
        {
            bakerMaxHulls = Mathf.Clamp(bakerMaxHulls <= 0 ? 6 : bakerMaxHulls, 1, 32);
            bakerMaxHullVertices = Mathf.Clamp(bakerMaxHullVertices <= 0 ? 96 : bakerMaxHullVertices, 16, 128);
            bakerBakeTimeBudgetSeconds = Mathf.Clamp(bakerBakeTimeBudgetSeconds <= 0 ? 60 : bakerBakeTimeBudgetSeconds, 10, 1800);
            MigrateRealHullsSettings();
        }

        private void MigrateProductionBakerRanges()
        {
            bakerMaxHulls = Mathf.Clamp(bakerMaxHulls <= 0 ? 6 : bakerMaxHulls, 1, 32);
            bakerMaxHullVertices = Mathf.Clamp(bakerMaxHullVertices <= 0 ? 96 : bakerMaxHullVertices, 16, 128);
            bakerBakeTimeBudgetSeconds = Mathf.Clamp(bakerBakeTimeBudgetSeconds <= 0 ? 60 : bakerBakeTimeBudgetSeconds, 10, 1800);
            MigrateRealHullsSettings();
        }

        private void MigrateColliderBakeBudget()
        {
            bakerBakeTimeBudgetSeconds = Mathf.Clamp(bakerBakeTimeBudgetSeconds <= 0 ? 60 : bakerBakeTimeBudgetSeconds, 10, 1800);
        }

        private void MigrateColliderBakerCleanupSettings()
        {
            if (bakerBakeTimeBudgetSeconds == 30 || bakerBakeTimeBudgetSeconds == 1200)
            {
                bakerBakeTimeBudgetSeconds = 60;
            }
        }

        private void MigrateBakerLodSettings()
        {
            bakerLodMask = bakerLodMask <= 0
                ? DefaultBakerLodMask
                : Mathf.Clamp(bakerLodMask, 1, AllBakerLodMask);
        }

        private void MigrateProductionBakerOutputDefaults()
        {
            // The production default is intentionally quiet: bake from the lowest
            // available LOD and keep generated gizmos hidden unless the user opts in.
            bakerLodMask = DefaultBakerLodMask;
            bakerDrawGizmos = false;
            bakerDrawWhenUnselected = false;
        }

        private void MigrateCookMatchDefaultOff()
        {
            // Cook Match uses Unity's convex cooking probe, which can be slow on
            // complex generated hulls. Keep it opt-in so default bakes stay fast.
            bakerCookMatch = false;
        }

        private void MigrateSmallHullMergeSettings()
        {
            bakerSmallHullMergeTriangleLimit = Mathf.Clamp(bakerSmallHullMergeTriangleLimit <= 0 ? 255 : bakerSmallHullMergeTriangleLimit, 16, 255);
            bakerSmallHullMergeExpansion = Mathf.Clamp(bakerSmallHullMergeExpansion <= 0f ? 1.2f : bakerSmallHullMergeExpansion, 1f, 3f);
        }

        private void MigrateBakerPaddingSettings()
        {
            // Pre-44 this serialized value meant "shrink inward". Padding is signed:
            // positive expands, negative tightens.
            if (bakerColliderPadding > 0f)
            {
                bakerColliderPadding = -bakerColliderPadding;
            }

            bakerColliderPadding = Mathf.Clamp(bakerColliderPadding, -0.03f, 0.03f);
        }

        private void MigrateCookMatchSettings()
        {
            bakerCookMatchTolerance = Mathf.Clamp(bakerCookMatchTolerance <= 0f ? 0.035f : bakerCookMatchTolerance, 0.001f, 0.25f);
        }

        private void MigrateRigidbodySettings()
        {
            temporaryBodyMass = 1f;
            temporaryBodyCollisionDetection = CollisionDetectionMode.Discrete;
            temporaryBodyInterpolation = RigidbodyInterpolation.None;
            temporaryBodyMaxAngularVelocity = 50f;
            preserveHigherExistingBodySolverIterations = true;
            freezeSceneRigidbodiesDuringAssist = true;
        }

        private void MigrateMovablePrefabPartSettings()
        {
            // Existing settings assets predate this safety option. Opt them in so
            // articulated prefab children cannot be changed just by placing a root.
            preserveMovablePrefabParts = true;
        }

        private void MigrateCenterTruthSettings()
        {
            useCenterAsPhysicsTruth = true;
        }

        private void MigrateIsolatedPhysicsSettings()
        {
            useIsolatedPhysicsScene = false;
            isolatedPhysicsRadius = Mathf.Clamp(isolatedPhysicsRadius <= 0f ? 6f : isolatedPhysicsRadius, 0.5f, 30f);
        }

        private void MigratePerformanceControlSettings()
        {
            useIsolatedPhysicsScene = false;
            useGlobalPhysicsSettings = false;
            globalSimulationSpeed = Mathf.Max(0.01f, move.simulationSpeed > 0f ? move.simulationSpeed : 2f);
            globalPhysicsStep = Mathf.Clamp(move.physicsStep > 0f ? move.physicsStep : 0.01f, 0.001f, 0.02f);
            globalMaxSubsteps = Mathf.Clamp(move.maxSubsteps > 0 ? move.maxSubsteps : 2, 1, 64);
            globalSolverIterations = Mathf.Clamp(move.solverIterations > 0 ? move.solverIterations : 8, 6, 96);
            globalSolverVelocityIterations = Mathf.Clamp(move.solverVelocityIterations > 0 ? move.solverVelocityIterations : 4, 1, 48);
            MigrateRotationConstraintSettings();
        }

        private void MigrateRotationConstraintSettings()
        {
            EnsureModeProfiles();
            globalRotationConstraint = SanitizeRotationConstraint(globalRotationConstraint);
            move.rotationConstraint = SanitizeRotationConstraint(move.rotationConstraint);
            rotate.rotationConstraint = SanitizeRotationConstraint(rotate.rotationConstraint);
            drop.rotationConstraint = SanitizeRotationConstraint(drop.rotationConstraint);
        }

        private void MigrateSceneProxyDefaultOff()
        {
            generateSceneProxyColliders = false;
            useSceneProxyCollidersForAllObjects = false;
        }

        private void MigrateObjectBakerSettings()
        {
            objectBakerIncludeInactiveChildren = false;
            objectBakerExportFbx = true;
            if (!Enum.IsDefined(typeof(RealPhysicsPlacerObjectBakePivot), objectBakerPivot))
            {
                objectBakerPivot = RealPhysicsPlacerObjectBakePivot.SelectionCenter;
            }
        }

        private void MigrateObjectBakerRotationSettings()
        {
            if (!Enum.IsDefined(typeof(RealPhysicsPlacerObjectBakeRotation), objectBakerRotation))
            {
                objectBakerRotation = RealPhysicsPlacerObjectBakeRotation.WorldSpace;
            }

            if (settingsVersion == 50 && objectBakerPivot == RealPhysicsPlacerObjectBakePivot.FirstSelected)
            {
                objectBakerRotation = RealPhysicsPlacerObjectBakeRotation.FirstSelectedDirection;
            }
        }

        private void MigrateKeyBindings()
        {
            if (physicsAssistKey == KeyCode.None)
            {
                physicsAssistKey = KeyCode.CapsLock;
            }

            if (moveToolKey == KeyCode.None)
            {
                moveToolKey = KeyCode.W;
            }

            if (rotateToolKey == KeyCode.None)
            {
                rotateToolKey = KeyCode.E;
            }

            if (arrangeToolKey == KeyCode.None)
            {
                arrangeToolKey = KeyCode.R;
            }

            if (actionKey == KeyCode.None)
            {
                actionKey = KeyCode.Space;
            }

            if (arrangeRandomSpinKey == KeyCode.None)
            {
                arrangeRandomSpinKey = KeyCode.R;
            }
        }

        private void MigrateArrangeActionSettings()
        {
            EnsureModeProfiles();
            if (arrange.primaryAction == RealPhysicsPlacerArrangeAction.None)
            {
                arrange.primaryAction = RealPhysicsPlacerArrangeAction.GridXZ;
            }

            if (arrange.toolAction == RealPhysicsPlacerArrangeAction.None)
            {
                arrange.toolAction = RealPhysicsPlacerArrangeAction.Clump;
            }
        }

        internal HashSet<string> BuildIgnoredTagSet()
        {
            var ignored = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(ignoredTags))
            {
                return ignored;
            }

            var tags = ignoredTags.Split(',');
            foreach (var tag in tags)
            {
                var trimmed = tag.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    ignored.Add(trimmed);
                }
            }

            return ignored;
        }

        internal string GetColliderBakerOutputFolder()
        {
            return RealPhysicsPlacerAssetPaths.ResolveGeneratedOutputFolder(
                bakerOutputFolder,
                RealPhysicsPlacerAssetPaths.DefaultBakedCollidersFolder);
        }

        internal string GetObjectBakerOutputFolder()
        {
            return RealPhysicsPlacerAssetPaths.ResolveGeneratedOutputFolder(
                objectBakerOutputFolder,
                RealPhysicsPlacerAssetPaths.DefaultBakedObjectsFolder);
        }

        internal void ResetToDefaults()
        {
            EnsureModeProfiles();
            move.ResetToDefaults();
            rotate.ResetToDefaults();
            drop.ResetToDefaults();
            arrange.ResetToDefaults();
            useGlobalPhysicsSettings = true;
            globalSimulationSpeed = 2f;
            globalPhysicsStep = 0.01f;
            globalMaxSubsteps = 2;
            globalSolverIterations = 8;
            globalSolverVelocityIterations = 4;
            globalRotationConstraint = RealPhysicsPlacerRotationConstraint.None;

            assistKeyBehavior = RealPhysicsPlacerAssistKeyBehavior.Toggle;
            physicsAssistKey = KeyCode.CapsLock;
            moveToolKey = KeyCode.W;
            rotateToolKey = KeyCode.E;
            arrangeToolKey = KeyCode.R;
            actionKey = KeyCode.Space;
            arrangeRandomSpinKey = KeyCode.R;

            collisionMask = -1;
            includeTriggers = false;
            ignoredTags = string.Empty;
            useRendererBoundsWhenNoCollider = true;
            generateSceneProxyColliders = false;
            useSceneProxyCollidersForAllObjects = false;
            useLocalSceneProxyCache = true;
            sceneProxyCacheRadius = 8f;
            sceneProxyMeshRadius = 3f;
            sceneProxyCacheRefreshDistance = 1.5f;
            useConvexStaticMeshProxies = false;
            useSurfaceShells = false;
            surfaceShellBudget = 2048;
            surfaceShellSkin = 0.08f;
            fallbackHullPieces = 1;
            bakerColliderShape = RealPhysicsPlacerBakedColliderShape.RealHulls;
            bakerMaxHulls = 6;
            bakerMaxHullVertices = 96;
            bakerBakeTimeBudgetSeconds = 60;
            bakerLodMask = DefaultBakerLodMask;
            bakerSmartHullsUseFullBudget = true;
            bakerRealHullsQuality = RealPhysicsPlacerRealHullsQuality.Fast;
            bakerRealHullsVoxelResolution = 96;
            bakerRealHullsSurfaceSamples = 10000;
            bakerRealHullsMaxSplitCandidates = 64;
            bakerRealHullsShapeAccuracy = 0.03f;
            bakerRealHullsMergePass = true;
            bakerRealHullsCacheResults = true;
            bakerOutputFolder = string.Empty;
            bakerSmartHullSplitSearch = 4;
            bakerSmartHullOpenEdgeBias = 0.35f;
            bakerSmartHullMinSplitGain = 0f;
            bakerMergeSmallHulls = false;
            bakerSmallHullMergeTriangleLimit = 255;
            bakerSmallHullMergeExpansion = 1.35f;
            bakerColliderPadding = 0f;
            bakerCookMatch = false;
            bakerCookMatchTolerance = 0.035f;
            bakerPreviewResult = true;
            bakerReplaceExisting = true;
            bakerDisableExistingColliders = false;
            bakerIncludeInactiveChildren = false;
            bakerSkipPrimitiveMeshes = true;
            bakerAddMeshFilters = false;
            bakerDrawGizmos = false;
            bakerDrawWhenUnselected = false;
            bakerDrawSolidGizmos = false;
            bakerLogTimings = false;
            bakerGizmoColor = new Color(1f, 0.55f, 0.08f, 0.85f);
            objectBakerIncludeInactiveChildren = false;
            objectBakerExportFbx = true;
            objectBakerOutputFolder = string.Empty;
            objectBakerPivot = RealPhysicsPlacerObjectBakePivot.SelectionCenter;
            objectBakerRotation = RealPhysicsPlacerObjectBakeRotation.WorldSpace;
            allowAxisSliding = false;
            skinWidth = 0.15f;
            maxMoveStep = 0.25f;
            useCenterAsPhysicsTruth = true;
            useIsolatedPhysicsScene = false;
            isolatedPhysicsRadius = 8f;
            temporaryBodyMass = 1f;
            temporaryBodyCollisionDetection = CollisionDetectionMode.ContinuousDynamic;
            temporaryBodyInterpolation = RigidbodyInterpolation.None;
            temporaryBodyMaxAngularVelocity = 50f;
            preserveHigherExistingBodySolverIterations = true;
            freezeSceneRigidbodiesDuringAssist = true;
            preserveMovablePrefabParts = true;
            displayCollisionIndicators = false;
            collisionIndicatorDistance = 5f;
            xAxisColor = new Color(0.95f, 0.22f, 0.18f, 1f);
            yAxisColor = new Color(0.24f, 0.78f, 0.32f, 1f);
            zAxisColor = new Color(0.24f, 0.45f, 1f, 1f);
            blockedColor = new Color(0.18f, 0.47f, 1f, 1f);
            settingsVersion = CurrentSettingsVersion;
        }

        internal static RealPhysicsPlacerRotationConstraint SanitizeRotationConstraint(RealPhysicsPlacerRotationConstraint constraint)
        {
            return constraint & AllRotationConstraintAxes;
        }

        internal void ApplyPhysicsPreset(RealPhysicsPlacerPhysicsPreset preset)
        {
            EnsureModeProfiles();

            useIsolatedPhysicsScene = false;
            useGlobalPhysicsSettings = true;
            temporaryBodyMass = 1f;
            temporaryBodyMaxAngularVelocity = 50f;
            preserveHigherExistingBodySolverIterations = true;
            freezeSceneRigidbodiesDuringAssist = true;
            globalRotationConstraint = RealPhysicsPlacerRotationConstraint.None;
            move.rotationConstraint = RealPhysicsPlacerRotationConstraint.None;
            rotate.rotationConstraint = RealPhysicsPlacerRotationConstraint.None;
            drop.rotationConstraint = RealPhysicsPlacerRotationConstraint.None;

            switch (preset)
            {
                case RealPhysicsPlacerPhysicsPreset.Accurate:
                    generateSceneProxyColliders = true;
                    useSceneProxyCollidersForAllObjects = true;
                    useLocalSceneProxyCache = true;
                    useConvexStaticMeshProxies = false;
                    sceneProxyCacheRadius = 12f;
                    sceneProxyMeshRadius = 4f;
                    sceneProxyCacheRefreshDistance = 1.25f;
                    isolatedPhysicsRadius = 12f;
                    fallbackHullPieces = 4;
                    temporaryBodyCollisionDetection = CollisionDetectionMode.ContinuousDynamic;
                    temporaryBodyInterpolation = RigidbodyInterpolation.Interpolate;
                    move.maxSubsteps = 4;
                    move.solverIterations = 12;
                    move.solverVelocityIterations = 8;
                    rotate.maxSubsteps = 4;
                    rotate.solverIterations = 16;
                    rotate.solverVelocityIterations = 8;
                    drop.maxSubsteps = 4;
                    drop.bodySolverIterations = 8;
                    drop.bodySolverVelocityIterations = 6;
                    globalSimulationSpeed = 2f;
                    globalPhysicsStep = 0.008f;
                    globalMaxSubsteps = 4;
                    globalSolverIterations = 14;
                    globalSolverVelocityIterations = 8;
                    break;

                case RealPhysicsPlacerPhysicsPreset.Balanced:
                    generateSceneProxyColliders = false;
                    useSceneProxyCollidersForAllObjects = false;
                    useLocalSceneProxyCache = true;
                    useConvexStaticMeshProxies = false;
                    sceneProxyCacheRadius = 8f;
                    sceneProxyMeshRadius = 3f;
                    sceneProxyCacheRefreshDistance = 1.5f;
                    isolatedPhysicsRadius = 8f;
                    fallbackHullPieces = 1;
                    temporaryBodyCollisionDetection = CollisionDetectionMode.ContinuousDynamic;
                    temporaryBodyInterpolation = RigidbodyInterpolation.None;
                    move.maxSubsteps = 2;
                    move.solverIterations = 8;
                    move.solverVelocityIterations = 4;
                    rotate.maxSubsteps = 2;
                    rotate.solverIterations = 10;
                    rotate.solverVelocityIterations = 4;
                    drop.maxSubsteps = 2;
                    drop.bodySolverIterations = 4;
                    drop.bodySolverVelocityIterations = 2;
                    globalSimulationSpeed = 2f;
                    globalPhysicsStep = 0.01f;
                    globalMaxSubsteps = 2;
                    globalSolverIterations = 8;
                    globalSolverVelocityIterations = 4;
                    break;

                default:
                    useIsolatedPhysicsScene = true;
                    generateSceneProxyColliders = false;
                    useSceneProxyCollidersForAllObjects = false;
                    useLocalSceneProxyCache = true;
                    useConvexStaticMeshProxies = false;
                    sceneProxyCacheRadius = 6f;
                    sceneProxyMeshRadius = 2f;
                    sceneProxyCacheRefreshDistance = 2f;
                    isolatedPhysicsRadius = 6f;
                    fallbackHullPieces = 1;
                    temporaryBodyCollisionDetection = CollisionDetectionMode.Discrete;
                    temporaryBodyInterpolation = RigidbodyInterpolation.None;
                    move.maxSubsteps = 2;
                    move.solverIterations = 8;
                    move.solverVelocityIterations = 4;
                    rotate.maxSubsteps = 2;
                    rotate.solverIterations = 10;
                    rotate.solverVelocityIterations = 4;
                    drop.maxSubsteps = 2;
                    drop.bodySolverIterations = 4;
                    drop.bodySolverVelocityIterations = 2;
                    globalSimulationSpeed = 1.5f;
                    globalPhysicsStep = 0.012f;
                    globalMaxSubsteps = 2;
                    globalSolverIterations = 8;
                    globalSolverVelocityIterations = 4;
                    break;
            }
        }

        [Serializable]
        public sealed class MoveModeSettings
        {
            public bool preserveSelectionOffsets = true;
            public bool controlRotationInPlacement = true;
            public bool readjustHandleOnMouseUp = true;
            public bool teleportWhenTooFar;
            [Min(0.1f)] public float teleportDistance = 7f;
            [Range(0.5f, 80f)] public float followSpeed = 80f;
            [Min(0.01f)] public float simulationSpeed = 2f;
            [Range(0.001f, 0.02f)] public float physicsStep = 0.01f;
            [Range(1, 64)] public int maxSubsteps = 2;
            [Range(6, 96)] public int solverIterations = 8;
            [Range(1, 48)] public int solverVelocityIterations = 4;
            public RealPhysicsPlacerRotationConstraint rotationConstraint;
            [Range(1f, 240f)] public float spring = 240f;
            [Range(0f, 45f)] public float damping = 24f;
            [Range(1f, 90f)] public float angularDamping = 32f;
            [Range(0.02f, 0.35f)] public float targetSmoothTime = 0.15f;
            [Range(0.1f, 3f)] public float targetMaxDistance = 3f;
            [Range(0.01f, 0.75f)] public float physicsAssistDistance = 0.1f;

            internal void ResetToDefaults()
            {
                preserveSelectionOffsets = true;
                controlRotationInPlacement = true;
                readjustHandleOnMouseUp = true;
                teleportWhenTooFar = false;
                teleportDistance = 7f;
                followSpeed = 80f;
                simulationSpeed = 2f;
                physicsStep = 0.01f;
                maxSubsteps = 2;
                solverIterations = 8;
                solverVelocityIterations = 4;
                rotationConstraint = RealPhysicsPlacerRotationConstraint.None;
                spring = 240f;
                damping = 24f;
                angularDamping = 32f;
                targetSmoothTime = 0.15f;
                targetMaxDistance = 3f;
                physicsAssistDistance = 0.1f;
            }

        }

        [Serializable]
        public sealed class RotateModeSettings
        {
            [Min(0.01f)] public float simulationSpeed = 2f;
            [Range(0.001f, 0.02f)] public float physicsStep = 0.008f;
            [Range(1, 64)] public int maxSubsteps = 2;
            [Range(6, 96)] public int solverIterations = 10;
            [Range(1, 48)] public int solverVelocityIterations = 4;
            public RealPhysicsPlacerRotationConstraint rotationConstraint;
            [Range(5f, 160f)] public float angularSpring = 90f;
            [Range(0f, 50f)] public float angularDamping = 16f;
            [Range(1f, 40f)] public float maxAngularSpeed = 14f;
            [Range(0f, 12f)] public float linearDamping = 2.5f;
            [Range(0.25f, 5f)] public float maxStepDegrees = 5f;
            [Range(0.001f, 0.04f)] public float contactTolerance = 0.04f;

            internal void ResetToDefaults()
            {
                simulationSpeed = 2f;
                physicsStep = 0.008f;
                maxSubsteps = 2;
                solverIterations = 10;
                solverVelocityIterations = 4;
                rotationConstraint = RealPhysicsPlacerRotationConstraint.None;
                angularSpring = 90f;
                angularDamping = 16f;
                maxAngularSpeed = 14f;
                linearDamping = 2.5f;
                maxStepDegrees = 5f;
                contactTolerance = 0.04f;
            }

        }

        [Serializable]
        public sealed class DropModeSettings
        {
            public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
            [Min(0.01f)] public float simulationSpeed = 1f;
            [Range(0.001f, 0.02f)] public float physicsStep = 0.01f;
            [Range(1, 64)] public int maxSubsteps = 2;
            [Range(6, 96)] public int bodySolverIterations = 4;
            [Range(1, 48)] public int bodySolverVelocityIterations = 2;
            public RealPhysicsPlacerRotationConstraint rotationConstraint;
            [Range(1, 16)] public int solverIterations = 2;
            [Range(0.5f, 40f)] public float maxSpeed = 13.5f;
            [Range(0, 96)] public int settleSteps = 0;

            internal void ResetToDefaults()
            {
                gravity = new Vector3(0f, -9.81f, 0f);
                simulationSpeed = 1f;
                physicsStep = 0.01f;
                maxSubsteps = 2;
                bodySolverIterations = 4;
                bodySolverVelocityIterations = 2;
                rotationConstraint = RealPhysicsPlacerRotationConstraint.None;
                solverIterations = 2;
                maxSpeed = 13.5f;
                settleSteps = 0;
            }

        }

        [Serializable]
        public sealed class ArrangeModeSettings
        {
            [Range(0.05f, 2f)] public float handleStrength = 2f;
            [Range(0f, 0.5f)] public float padding = 0.5f;
            public RealPhysicsPlacerArrangeAction primaryAction = RealPhysicsPlacerArrangeAction.GridXZ;
            public RealPhysicsPlacerArrangeAction toolAction = RealPhysicsPlacerArrangeAction.Clump;
            public bool collideSelectedObjects = true;
            public bool collisionAware = true;

            internal void ResetToDefaults()
            {
                handleStrength = 2f;
                padding = 0.5f;
                primaryAction = RealPhysicsPlacerArrangeAction.GridXZ;
                toolAction = RealPhysicsPlacerArrangeAction.Clump;
                collideSelectedObjects = true;
                collisionAware = true;
            }

        }
    }

    internal static class RealPhysicsPlacerAssetPaths
    {
        private const string FallbackPackageRoot = "Assets";
        private const string FallbackGeneratedRoot = "Assets/RealTransforms Generated";
        private const string SettingsScriptName = nameof(RealPhysicsPlacerSettings);
        private static string cachedPackageRoot;

        internal static string PackageRoot => cachedPackageRoot ??= ResolvePackageRoot();
        internal static string EditorFolder => $"{PackageRoot}/Editor";
        internal static string SettingsAssetPath => $"{EditorFolder}/RealPhysicsPlacerSettings.asset";
        internal static string DefaultGeneratedRoot => IsGeneratedAssetFolder(PackageRoot) ? PackageRoot : FallbackGeneratedRoot;
        internal static string DefaultBakedCollidersFolder => $"{DefaultGeneratedRoot}/BakedColliders";
        internal static string DefaultBakedObjectsFolder => $"{DefaultGeneratedRoot}/BakedObjects";
        internal static string BakedCollidersFolder => DefaultBakedCollidersFolder;
        internal static string BakedObjectsFolder => DefaultBakedObjectsFolder;
        internal static string BakedColliderLibraryPath => $"{DefaultBakedCollidersFolder}/RealTransforms_BakedColliderLibrary.asset";

        internal static string GetBakedColliderLibraryPath(RealPhysicsPlacerSettings settings)
        {
            var folder = settings == null
                ? DefaultBakedCollidersFolder
                : settings.GetColliderBakerOutputFolder();
            return $"{folder}/RealTransforms_BakedColliderLibrary.asset";
        }

        internal static void EnsureFolder(string folder)
        {
            folder = NormalizeAssetPath(folder);
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = NormalizeAssetPath(Path.GetDirectoryName(folder));
            var name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        internal static bool IsPathInFolder(string assetPath, string folder)
        {
            assetPath = NormalizeAssetPath(assetPath);
            folder = NormalizeAssetPath(folder)?.TrimEnd('/');
            return !string.IsNullOrEmpty(assetPath)
                && !string.IsNullOrEmpty(folder)
                && (string.Equals(assetPath, folder, StringComparison.Ordinal)
                    || assetPath.StartsWith(folder + "/", StringComparison.Ordinal));
        }

        internal static string ResolveGeneratedOutputFolder(string folder, string fallbackFolder)
        {
            var normalized = NormalizeGeneratedOutputFolder(folder);
            return string.IsNullOrEmpty(normalized)
                ? NormalizeGeneratedOutputFolder(fallbackFolder)
                : normalized;
        }

        internal static string NormalizeGeneratedOutputFolder(string folder)
        {
            folder = NormalizeAssetPath(folder).Trim().Trim('/');
            return IsGeneratedAssetFolder(folder)
                ? folder
                : string.Empty;
        }

        internal static bool TryConvertAbsoluteProjectFolderToAssetPath(string absoluteFolder, out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(absoluteFolder))
            {
                return false;
            }

            var normalizedFolder = NormalizeAssetPath(Path.GetFullPath(absoluteFolder));
            var assetsPath = NormalizeAssetPath(Path.GetFullPath(Application.dataPath));
            if (string.Equals(normalizedFolder, assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                assetPath = "Assets";
                return true;
            }

            if (normalizedFolder.StartsWith(assetsPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                assetPath = "Assets/" + normalizedFolder.Substring(assetsPath.Length + 1);
                return true;
            }

            return false;
        }

        internal static string ToAbsoluteAssetPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.Equals(assetPath, "Assets", StringComparison.Ordinal))
            {
                return Application.dataPath;
            }

            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                return NormalizeAssetPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
            }

            return Application.dataPath;
        }

        private static string ResolvePackageRoot()
        {
            var scriptPath = FindScriptPath(typeof(RealPhysicsPlacerSettings), SettingsScriptName);
            if (!string.IsNullOrEmpty(scriptPath))
            {
                var editorFolder = NormalizeAssetPath(Path.GetDirectoryName(scriptPath));
                var packageRoot = NormalizeAssetPath(Path.GetDirectoryName(editorFolder));
                if (!string.IsNullOrEmpty(packageRoot))
                {
                    return packageRoot;
                }
            }

            return FallbackPackageRoot;
        }

        private static string FindScriptPath(Type type, string scriptName)
        {
            var guids = AssetDatabase.FindAssets($"{scriptName} t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return NormalizeAssetPath(path);
                }
            }

            return string.Empty;
        }

        private static bool IsGeneratedAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder).Trim('/');
            return string.Equals(folder, "Assets", StringComparison.Ordinal)
                || folder.StartsWith("Assets/", StringComparison.Ordinal);
        }

        internal static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }
    }
}
