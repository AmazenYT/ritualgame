﻿using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    public sealed class RealPhysicsPlacerWindow : EditorWindow
    {
        // Main user-facing editor window. Common controls live on the first
        // tabs; costlier tuning stays behind foldouts for advanced users.
        private const string AdvancedKey = "RealTransforms.ShowAdvanced";
        private const string AdvancedModeKey = "RealTransforms.AdvancedMode";
        private const string ModeSettingsKey = "RealTransforms.ShowModeSettings";
        private const string ProxyAdvancedKey = "RealTransforms.ShowProxyAdvanced";
        private const string WindowTabKey = "RealTransforms.WindowTab";
        private const string BakerModeKey = "RealTransforms.BakerMode";
        private const string BakerLodFoldoutKey = "RealTransforms.ShowBakerLods";
        private const string BakerOutputFoldoutKey = "RealTransforms.ShowBakerOutput";
        private const string BakerRealHullsAdvancedKey = "RealTransforms.ShowRealHullsAdvanced";
        private const int ColliderBakerMode = 0;
        private const int ObjectBakerMode = 1;
        private const float NarrowLayoutWidth = 420f;
        private const float WideLayoutWidth = 760f;
        private const float GridGap = 6f;
        private const float NavigationButtonHeight = 46f;
        private const float ModeButtonHeight = 40f;
        private const float StatusCardHeight = 54f;
        private const float RealHullsHighCostThreshold = 40f;
        private const float RealHullsExtremeCostThreshold = 115f;
        private const double PassiveStatusRepaintIntervalSeconds = 1.0d;

        private static readonly RealPhysicsPlacerMode[] PrimaryModes =
        {
            RealPhysicsPlacerMode.Placement,
            RealPhysicsPlacerMode.Rotation,
            RealPhysicsPlacerMode.Alignment,
            RealPhysicsPlacerMode.Fall
        };

        private static readonly string[] ModeNames =
        {
            "Move",
            "Rotate",
            "Arrange",
            "Drop"
        };

        private static readonly RealPhysicsPlacerMode[] ControlModes =
        {
            RealPhysicsPlacerMode.Placement,
            RealPhysicsPlacerMode.Rotation,
            RealPhysicsPlacerMode.Alignment
        };

        private static readonly GUIContent[] WindowTabs =
        {
            new GUIContent("\U0001F39B Control", "Turn physics assist on or off and switch modes without shortcuts."),
            new GUIContent("\u03BB Physics Settings", "Tune collision, mode feel, and scene-view indicators."),
            new GUIContent("\U0001F9F1 Baker", "Bake colliders or combine selected rendered objects into optimized assets."),
            new GUIContent("\u2699 Tool Settings", "Configure shortcuts and input behavior.")
        };

        private static readonly string[] ControlModeLabels =
        {
            "Move",
            "Rotate",
            "Arrange"
        };

        private static readonly string[] ControlModeHints =
        {
            "Physics-assisted version of Unity Move. Best for placing objects against surfaces.",
            "Physics-assisted version of Unity Rotate. Best for rotating while respecting nearby colliders.",
            "Physics-aware spacing controls for multiple selected objects."
        };

        private static readonly string[] SceneProxyModeNames =
        {
            "Missing Colliders Only",
            "Prefer Mesh Proxies"
        };

        private static readonly Color AccentOrange = new Color(1f, 0.48f, 0.08f, 1f);
        private static readonly Color ActiveBlue = new Color(0.22f, 0.42f, 0.62f, 1f);
        private static readonly Color ActiveGreen = new Color(0.24f, 0.48f, 0.38f, 1f);
        private static readonly Color InactiveGray = new Color(0.42f, 0.42f, 0.42f, 1f);

        private RealPhysicsPlacerSettings settings;
        private SerializedObject serializedSettings;
        private bool showAdvanced;
        private bool showModeSettings;
        private bool showProxyAdvanced;
        private bool showBakerLods;
        private bool showBakerOutput;
        private bool showRealHullsAdvanced;
        private int advancedModeIndex;
        private int windowTabIndex;
        private int bakerModeIndex;
        private Vector2 scroll;
        private string recordingKeyPropertyName;
        private string recordingKeyLabel;
        private bool controlPanelActionShortcutArmed;
        private bool controlPanelActionShortcutActive;
        private double nextPassiveStatusRepaintTime;

        private readonly struct RealHullsCostReport
        {
            internal readonly float Score;
            internal readonly string Message;
            internal readonly string DialogMessage;
            internal readonly MessageType MessageType;

            internal RealHullsCostReport(float score, string message, string dialogMessage, MessageType messageType)
            {
                Score = score;
                Message = message;
                DialogMessage = dialogMessage;
                MessageType = messageType;
            }
        }

        [MenuItem("Window/RealTransforms", false, 2050)]
        public static void OpenFromWindowMenu()
        {
            Open();
        }

        [MenuItem("Tools/RealTransforms/Open Window", false, 0)]
        public static void Open()
        {
            var window = GetWindow<RealPhysicsPlacerWindow>();
            window.titleContent = new GUIContent("RealTransforms");
            window.minSize = new Vector2(320f, 480f);
            window.Show();
        }

        [MenuItem("Tools/RealTransforms/Toggle Enabled", false, 1)]
        public static void ToggleEnabled()
        {
            RealPhysicsPlacerSceneTool.SetPhysicsAssistMasterEnabled(!RealPhysicsPlacerSceneTool.PhysicsAssistMasterEnabled);
        }

        [MenuItem("Tools/RealTransforms/Refresh Tool State", false, 2)]
        public static void RefreshToolState()
        {
            RealPhysicsPlacerSceneTool.RefreshToolState();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("RealTransforms");
            minSize = new Vector2(320f, 480f);
            RealPhysicsPlacerSceneTool.EnsureHooked();
            settings = RealPhysicsPlacerSettings.GetOrCreate();
            serializedSettings = new SerializedObject(settings);
            showAdvanced = EditorPrefs.GetBool(AdvancedKey, false);
            showModeSettings = EditorPrefs.GetBool(ModeSettingsKey, false);
            showProxyAdvanced = EditorPrefs.GetBool(ProxyAdvancedKey, false);
            showBakerLods = EditorPrefs.GetBool(BakerLodFoldoutKey, false);
            showBakerOutput = EditorPrefs.GetBool(BakerOutputFoldoutKey, false);
            showRealHullsAdvanced = EditorPrefs.GetBool(BakerRealHullsAdvancedKey, false);
            advancedModeIndex = Mathf.Clamp(EditorPrefs.GetInt(AdvancedModeKey, 0), 0, PrimaryModes.Length - 1);
            windowTabIndex = Mathf.Clamp(EditorPrefs.GetInt(WindowTabKey, 0), 0, WindowTabs.Length - 1);
            bakerModeIndex = Mathf.Clamp(EditorPrefs.GetInt(BakerModeKey, ColliderBakerMode), ColliderBakerMode, ObjectBakerMode);
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            RealPhysicsPlacerColliderPreview.Clear();
        }

        private void OnInspectorUpdate()
        {
            if (!ShouldPassiveRepaint())
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < nextPassiveStatusRepaintTime)
            {
                return;
            }

            nextPassiveStatusRepaintTime = now + PassiveStatusRepaintIntervalSeconds;
            Repaint();
        }

        private bool ShouldPassiveRepaint()
        {
            if (!string.IsNullOrEmpty(recordingKeyPropertyName))
            {
                return true;
            }

            return RealPhysicsPlacerSceneTool.HasLiveEditorPhysicsActivity;
        }

        private void OnGUI()
        {
            var previousGuiColor = GUI.color;
            var previousContentColor = GUI.contentColor;
            var previousBackgroundColor = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;

            RealPhysicsPlacerSceneTool.EnsureHooked();

            try
            {
                if (settings == null)
                {
                    settings = RealPhysicsPlacerSettings.GetOrCreate();
                    serializedSettings = new SerializedObject(settings);
                }

                settings.EnsureModeProfiles();
                serializedSettings.Update();
                HandleKeyBindingCapture(Event.current);
                HandleControlPanelActionShortcut(Event.current);

                DrawHeader();
                DrawWindowTabs();

                using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = scrollScope.scrollPosition;
                    switch (windowTabIndex)
                    {
                        case 0:
                            DrawControlControls();
                        break;
                    case 1:
                        DrawAdvancedControls();
                        break;
                        case 2:
                            DrawBakerControls();
                            break;
                        default:
                            DrawSettingsControls();
                            break;
                    }
                }

                if (serializedSettings.ApplyModifiedProperties())
                {
                    RealPhysicsPlacerSceneTool.MarkSceneProxyScanDirty();
                    if (windowTabIndex == 2 && bakerModeIndex == ColliderBakerMode)
                    {
                        HandleColliderBakerSettingsChanged();
                    }
                }
            }
            finally
            {
                GUI.color = previousGuiColor;
                GUI.contentColor = previousContentColor;
                GUI.backgroundColor = previousBackgroundColor;
            }
        }

        private void HandleColliderBakerSettingsChanged()
        {
            if (settings == null || !settings.bakerPreviewResult || !CanPreviewColliderBakerMethod())
            {
                RealPhysicsPlacerColliderPreview.Clear();
            }
        }

        private bool CanPreviewColliderBakerMethod()
        {
            return settings != null
                && (settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.MultiHull
                    || settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls);
        }

        private void DrawWindowTabs()
        {
            var nextTab = windowTabIndex;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var columns = GetNavigationColumnCount();
                for (var start = 0; start < WindowTabs.Length; start += columns)
                {
                    var rowRect = GUILayoutUtility.GetRect(0f, NavigationButtonHeight, GUILayout.ExpandWidth(true));
                    for (var column = 0; column < columns; column++)
                    {
                        var index = start + column;
                        if (index >= WindowTabs.Length)
                        {
                            continue;
                        }

                        if (DrawNavigationButton(GetGridCellRect(rowRect, column, columns), index))
                        {
                            nextTab = index;
                        }
                    }

                    if (start + columns < WindowTabs.Length)
                    {
                        GUILayout.Space(GridGap);
                    }
                }
            }

            if (nextTab == windowTabIndex)
            {
                return;
            }

            windowTabIndex = nextTab;
            ClearKeyBindingCapture();
            EditorPrefs.SetInt(WindowTabKey, windowTabIndex);
        }

        private int GetNavigationColumnCount()
        {
            if (position.width < NarrowLayoutWidth)
            {
                return 1;
            }

            return position.width < WideLayoutWidth ? 2 : WindowTabs.Length;
        }

        private bool DrawNavigationButton(Rect rect, int index)
        {
            var selected = index == windowTabIndex;
            var oldBackground = GUI.backgroundColor;
            var oldContent = GUI.contentColor;

            GUI.backgroundColor = selected ? ActiveBlue : InactiveGray;
            GUI.contentColor = selected ? Color.white : oldContent;
            var pressed = GUI.Button(rect, WindowTabs[index], GetNavigationButtonStyle());

            GUI.backgroundColor = oldBackground;
            GUI.contentColor = oldContent;
            return pressed;
        }

        private static Rect GetGridCellRect(Rect rowRect, int column, int columns)
        {
            var width = (rowRect.width - GridGap * (columns - 1)) / columns;
            return new Rect(rowRect.x + column * (width + GridGap), rowRect.y, width, rowRect.height);
        }

        private static GUIStyle GetNavigationButtonStyle()
        {
            return new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 9, 9),
                wordWrap = false
            };
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(76f)))
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    DrawLambdaMark(30);
                    var titleStyle = GetTitleStyle();
                    var title = new GUIContent("RealTransforms");
                    var titleWidth = titleStyle.CalcSize(title).x + 4f;
                    GUILayout.Label(title, titleStyle, GUILayout.Width(titleWidth), GUILayout.Height(36f));
                    GUILayout.FlexibleSpace();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static GUIStyle GetTitleStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 19,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawControlControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSectionHeader("Control");
                DrawAssistToggleCard();

                EditorGUILayout.Space(8f);
                DrawSectionHeader("Mode");
                DrawModeButtons();

                EditorGUILayout.Space(6f);
                DrawMiniHint("These buttons mirror the main Unity transform modes. Hold the primary action key while in Move to drop selected objects.");
            }

            EditorGUILayout.Space(8f);
            DrawPhysicsStatusControls();
        }

        private void DrawAssistToggleCard()
        {
            var enabled = RealPhysicsPlacerSceneTool.PhysicsAssistMasterEnabled;
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = enabled ? ActiveGreen : InactiveGray;
            var label = enabled ? "\u03BB Physics Assist On" : "\u03BB Physics Assist Off";
            if (GUILayout.Button(new GUIContent(label, "Toggle physics-assisted Unity tools."), GUILayout.Height(40f)))
            {
                RealPhysicsPlacerSceneTool.SetPhysicsAssistMasterEnabled(!enabled);
            }

            GUI.backgroundColor = previousBackground;
            DrawMiniHint(enabled
                ? "Physics assist is active. Select Move, Rotate, or Arrange below."
                : "Physics assist is off. Regular Unity transform tools keep working normally.");
        }

        private void DrawModeButtons()
        {
            var columns = position.width < NarrowLayoutWidth ? 1 : ControlModes.Length;
            for (var start = 0; start < ControlModes.Length; start += columns)
            {
                var rowRect = GUILayoutUtility.GetRect(0f, ModeButtonHeight, GUILayout.ExpandWidth(true));
                for (var column = 0; column < columns; column++)
                {
                    var index = start + column;
                    if (index >= ControlModes.Length)
                    {
                        continue;
                    }

                    DrawModeButton(GetGridCellRect(rowRect, column, columns), index);
                }

                if (start + columns < ControlModes.Length)
                {
                    GUILayout.Space(GridGap);
                }
            }
        }

        private void DrawModeButton(Rect rect, int index)
        {
            var mode = ControlModes[index];
            var selected = RealPhysicsPlacerSceneTool.Mode == mode;
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = selected ? ActiveBlue : Color.white;
            GUI.contentColor = selected ? Color.white : previousContent;

            if (GUI.Button(rect, new GUIContent(ControlModeLabels[index], ControlModeHints[index])))
            {
                if (!RealPhysicsPlacerSceneTool.PhysicsAssistMasterEnabled)
                {
                    RealPhysicsPlacerSceneTool.SetPhysicsAssistMasterEnabled(true);
                }

                RealPhysicsPlacerSceneTool.Mode = mode;
                ArmControlPanelActionShortcut();
                RealPhysicsPlacerSceneTool.FocusSceneViewForControlPanelInput();
            }

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        private void ArmControlPanelActionShortcut()
        {
            controlPanelActionShortcutArmed = true;
            controlPanelActionShortcutActive = false;
            GUI.FocusControl(null);
            Repaint();
        }

        private void HandleControlPanelActionShortcut(Event evt)
        {
            if (!controlPanelActionShortcutArmed
                || evt == null
                || windowTabIndex != 0
                || !string.IsNullOrEmpty(recordingKeyPropertyName))
            {
                return;
            }

            var wasKeyDown = evt.type == EventType.KeyDown;
            var wasKeyUp = evt.rawType == EventType.KeyUp;
            if (RealPhysicsPlacerSceneTool.TryHandleControlPanelActionShortcut(evt))
            {
                if (wasKeyDown)
                {
                    controlPanelActionShortcutActive = true;
                }

                if (wasKeyUp)
                {
                    controlPanelActionShortcutActive = false;
                    controlPanelActionShortcutArmed = false;
                }

                Repaint();
                return;
            }

            if (controlPanelActionShortcutActive)
            {
                return;
            }

            if (evt.type == EventType.MouseDown || evt.type == EventType.KeyDown)
            {
                controlPanelActionShortcutArmed = false;
            }
        }

        private static void DrawLambdaMark(int fontSize)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };

            var previousContent = GUI.contentColor;
            GUI.contentColor = AccentOrange;
            GUILayout.Label(new GUIContent("\u03BB", "Physics-assisted tool identity."), style, GUILayout.Width(fontSize + 8f), GUILayout.Height(fontSize + 4f));
            GUI.contentColor = previousContent;
        }

        private static void DrawSectionHeader(string title)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawMiniHint(string text)
        {
            var previousContent = GUI.contentColor;
            GUI.contentColor = Color.white;
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
            GUI.contentColor = previousContent;
        }

        private struct StatusCard
        {
            public readonly string Label;
            public readonly string Value;
            public readonly string Tooltip;

            public StatusCard(string label, string value, string tooltip)
            {
                Label = label;
                Value = value;
                Tooltip = tooltip;
            }
        }

        private static void DrawPhysicsStatusControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawCenteredSectionHeader("Status");
                var cards = new[]
                {
                    new StatusCard("Selection", $"{RealPhysicsPlacerSceneTool.SelectionCount}", "Number of selected root objects the tool can affect."),
                    new StatusCard("Physics Scan", $"{RealPhysicsPlacerSceneTool.ProxyCount}", "Temporary collision proxies currently available to the editor physics simulation."),
                    new StatusCard("Active Tool", ModeNames[GetPrimaryModeIndex(RealPhysicsPlacerSceneTool.Mode)], "The Unity transform tool currently receiving physics assistance."),
                    new StatusCard("Assist", RealPhysicsPlacerSceneTool.PhysicsAssistMasterEnabled ? "On" : "Off", "Whether the physics-assisted overlay is currently enabled.")
                };

                DrawStatusGrid(cards);
            }
        }

        private static void DrawCenteredSectionHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.Label(title, style, GUILayout.ExpandWidth(true));
        }

        private static void DrawStatusGrid(StatusCard[] cards)
        {
            var columns = EditorGUIUtility.currentViewWidth < NarrowLayoutWidth ? 1 : 2;
            var maxWidth = columns == 1 ? 240f : 420f;
            for (var start = 0; start < cards.Length; start += columns)
            {
                var rowRect = GUILayoutUtility.GetRect(0f, StatusCardHeight, GUILayout.ExpandWidth(true));
                rowRect = CenterRect(rowRect, Mathf.Min(rowRect.width, maxWidth));

                for (var column = 0; column < columns; column++)
                {
                    var index = start + column;
                    if (index >= cards.Length)
                    {
                        continue;
                    }

                    DrawStatusCard(GetGridCellRect(rowRect, column, columns), cards[index]);
                }

                if (start + columns < cards.Length)
                {
                    GUILayout.Space(GridGap);
                }
            }
        }

        private static Rect CenterRect(Rect rect, float width)
        {
            rect.x += (rect.width - width) * 0.5f;
            rect.width = width;
            return rect;
        }

        private static void DrawStatusCard(Rect rect, StatusCard card)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            const float inset = 6f;
            var labelRect = new Rect(rect.x + inset, rect.y + 6f, rect.width - inset * 2f, 18f);
            var valueRect = new Rect(rect.x + inset, rect.y + 25f, rect.width - inset * 2f, 22f);
            var previousContent = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Label(labelRect, new GUIContent(card.Label, card.Tooltip), GetCenteredMiniLabelStyle());
            GUI.Label(valueRect, card.Value, GetCenteredStatusValueStyle());
            GUI.contentColor = previousContent;
        }

        private static GUIStyle GetCenteredMiniLabelStyle()
        {
            return new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
        }

        private static GUIStyle GetCenteredStatusValueStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
        }

        private void DrawBakerControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawBakerModeSelector();
                EditorGUILayout.Space(6f);
                if (bakerModeIndex == ObjectBakerMode)
                {
                    DrawObjectBakerControls();
                }
                else
                {
                    DrawColliderBakerControls();
                }
            }
        }

        private void DrawBakerModeSelector()
        {
            GUILayout.Label("Baker", EditorStyles.boldLabel);
            var nextMode = GUILayout.Toolbar(bakerModeIndex, new[]
            {
                new GUIContent("Collider", "Bake persistent convex compound colliders for selected meshes."),
                new GUIContent("Object", "Combine selected rendered meshes into one optimized asset grouped by shared materials.")
            }, GUILayout.Height(30f));

            if (nextMode == bakerModeIndex)
            {
                return;
            }

            bakerModeIndex = nextMode;
            EditorPrefs.SetInt(BakerModeKey, bakerModeIndex);
            if (bakerModeIndex == ObjectBakerMode)
            {
                RealPhysicsPlacerColliderPreview.Clear();
            }
            else
            {
                HandleColliderBakerSettingsChanged();
            }
        }

        private void DrawColliderBakerControls()
        {
            var summary = DrawColliderBakerHeader();
            EditorGUILayout.Space(6f);
            DrawColliderBakerPresets();
            EditorGUILayout.Space(6f);
            DrawColliderBakerMethodSelector();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerBakeTimeBudgetSeconds)), new GUIContent("Time Limit", "Stops one Bake Selection run after this many seconds. Raise it for high-detail Real Hulls bakes; the maximum is 30 minutes."));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerMaxHulls)), new GUIContent("Max Hulls", "Requested hull count per mesh. Higher values can preserve difficult hollow shapes, but every hull adds collider and editor cost."));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerMaxHullVertices)), new GUIContent("Hull Detail", "Maximum detail per hull before Unity convex cooking. Higher values keep more shape; lower values produce simpler colliders."));
            DrawColliderBakerShapeControls(summary);
            DrawColliderBakerPreviewControls();
            DrawBakerLodControls();
            DrawBakerOutputControls();

            EditorGUILayout.Space(8f);
            DrawBakerActionButtons(summary);
        }

        private void DrawObjectBakerControls()
        {
            var summary = DrawObjectBakerHeader();
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("Combines every selected MeshRenderer into one mesh. Objects that share the same material become one submesh, so 50 identical bricks export as one material slot; mixed props export as one asset with one slot per shared material.", MessageType.None);

            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.objectBakerIncludeInactiveChildren)), new GUIContent("Include Inactive", "Include inactive child MeshRenderers under the selected roots."));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.objectBakerPivot)), new GUIContent("Pivot", "Choose where the baked asset origin is placed. Selection Center is best for loose objects; First Selected uses the first selected object's position."));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.objectBakerRotation)), new GUIContent("Rotation", "World Space keeps the baked asset aligned to the scene. First Selected Direction uses the first selected root rotation. Average Direction blends selected root forward/up directions."));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.objectBakerExportFbx)), new GUIContent("Export FBX", "Also write an FBX when Unity's FBX Exporter package is installed. A combined mesh asset and prefab are always created."));
            DrawAssetFolderControl(
                "Output Folder",
                nameof(RealPhysicsPlacerSettings.objectBakerOutputFolder),
                RealPhysicsPlacerAssetPaths.DefaultBakedObjectsFolder,
                "Where Bake Object writes combined meshes, prefabs, and optional FBX files. Leave on Default to follow the installed package folder.");

            var hasFbxExporter = RealPhysicsPlacerObjectBaker.HasFbxExporter();
            if (settings.objectBakerExportFbx && !hasFbxExporter)
            {
                EditorGUILayout.HelpBox("FBX Exporter is not installed. Bake Object will still create the combined mesh and prefab. To also write .fbx files, install com.unity.formats.fbx from Unity Package Manager.", MessageType.Info);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(summary.MeshCount == 0))
                {
                    if (GUILayout.Button(new GUIContent("Bake Object", "Create one combined mesh/prefab from the selected renderers, grouped by shared material."), GUILayout.Height(32f)))
                    {
                        serializedSettings.ApplyModifiedProperties();
                        RealPhysicsPlacerObjectBaker.BakeSelection(settings);
                    }
                }

                GUILayout.Label(hasFbxExporter ? "FBX Exporter ready" : "FBX Exporter optional", EditorStyles.miniLabel, GUILayout.MaxWidth(170f));
            }
        }

        private void DrawSettingsControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Key Bindings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.assistKeyBehavior)), new GUIContent("Assist Key Behavior", "Choose whether the physics assist key toggles the overlay or only works while held."));
                DrawKeyBindingButton("Physics Assist", nameof(RealPhysicsPlacerSettings.physicsAssistKey));

                EditorGUILayout.Space(4f);
                GUILayout.Label("Tools", EditorStyles.miniBoldLabel);
                DrawKeyBindingButton("Move", nameof(RealPhysicsPlacerSettings.moveToolKey));
                DrawKeyBindingButton("Rotate", nameof(RealPhysicsPlacerSettings.rotateToolKey));
                DrawKeyBindingButton("Arrange", nameof(RealPhysicsPlacerSettings.arrangeToolKey));

                EditorGUILayout.Space(4f);
                GUILayout.Label("Actions", EditorStyles.miniBoldLabel);
                DrawKeyBindingButton("Primary Action", nameof(RealPhysicsPlacerSettings.actionKey));

                var hint = string.IsNullOrEmpty(recordingKeyPropertyName)
                    ? "Click a binding, then press the key or mouse button to use. Tool actions repeat the active tool key: Rotate uses Rotate for random spin, Arrange uses Arrange for Z spacing. Primary Action stays separate."
                    : $"Press a key or mouse button for {recordingKeyLabel}. Escape cancels. Backspace or Delete clears the binding.";
                EditorGUILayout.HelpBox(hint, MessageType.Info);

                if (GUILayout.Button("Reset Key Bindings", GUILayout.Height(26f)))
                {
                    ResetKeyBindings();
                }
            }
        }

        private void DrawKeyBindingButton(string label, string propertyName)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                var recording = recordingKeyPropertyName == propertyName;
                var buttonLabel = recording ? "Press input..." : FormatKey(GetKeyBinding(propertyName));
                if (GUILayout.Button(buttonLabel, recording ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.MinWidth(120f)))
                {
                    StartKeyBindingCapture(propertyName, label);
                }
            }
        }

        private void StartKeyBindingCapture(string propertyName, string label)
        {
            recordingKeyPropertyName = propertyName;
            recordingKeyLabel = label;
            GUI.FocusControl(null);
            Repaint();
        }

        private void HandleKeyBindingCapture(Event evt)
        {
            if (string.IsNullOrEmpty(recordingKeyPropertyName) || evt == null)
            {
                return;
            }

            if (evt.rawType == EventType.MouseDown && TryMouseButtonToKeyCode(evt.button, out var mouseKey))
            {
                SetKeyBinding(recordingKeyPropertyName, recordingKeyLabel, mouseKey);
                evt.Use();
                return;
            }

            if (evt.type != EventType.KeyDown)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                ClearKeyBindingCapture();
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.Backspace || evt.keyCode == KeyCode.Delete)
            {
                SetKeyBinding(recordingKeyPropertyName, recordingKeyLabel, KeyCode.None);
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.None)
            {
                return;
            }

            SetKeyBinding(recordingKeyPropertyName, recordingKeyLabel, evt.keyCode);
            evt.Use();
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

        private void ClearKeyBindingCapture()
        {
            recordingKeyPropertyName = null;
            recordingKeyLabel = null;
            Repaint();
        }

        private KeyCode GetKeyBinding(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(RealPhysicsPlacerSettings.physicsAssistKey):
                    return settings.physicsAssistKey;
                case nameof(RealPhysicsPlacerSettings.moveToolKey):
                    return settings.moveToolKey;
                case nameof(RealPhysicsPlacerSettings.rotateToolKey):
                    return settings.rotateToolKey;
                case nameof(RealPhysicsPlacerSettings.arrangeToolKey):
                    return settings.arrangeToolKey;
                case nameof(RealPhysicsPlacerSettings.actionKey):
                    return settings.actionKey;
                default:
                    return KeyCode.None;
            }
        }

        private void SetKeyBinding(string propertyName, string label, KeyCode keyCode)
        {
            Undo.RecordObject(settings, $"Set {label} Key Binding");
            switch (propertyName)
            {
                case nameof(RealPhysicsPlacerSettings.physicsAssistKey):
                    settings.physicsAssistKey = keyCode;
                    break;
                case nameof(RealPhysicsPlacerSettings.moveToolKey):
                    settings.moveToolKey = keyCode;
                    break;
                case nameof(RealPhysicsPlacerSettings.rotateToolKey):
                    settings.rotateToolKey = keyCode;
                    break;
                case nameof(RealPhysicsPlacerSettings.arrangeToolKey):
                    settings.arrangeToolKey = keyCode;
                    break;
                case nameof(RealPhysicsPlacerSettings.actionKey):
                    settings.actionKey = keyCode;
                    break;
            }

            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            ClearKeyBindingCapture();
        }

        private RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary DrawColliderBakerHeader()
        {
            return RealPhysicsPlacerColliderGenerator.BuildBakeSelectionSummary(settings);
        }

        private RealPhysicsPlacerObjectBaker.SelectionSummary DrawObjectBakerHeader()
        {
            var summary = RealPhysicsPlacerObjectBaker.BuildSelectionSummary(settings);
            GUILayout.Label("Object Baker", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Selection", $"{summary.RootCount}", GUILayout.MaxWidth(118f));
                EditorGUILayout.LabelField("Meshes", $"{summary.MeshCount}", GUILayout.MaxWidth(108f));
                EditorGUILayout.LabelField("Materials", $"{summary.MaterialCount}", GUILayout.MaxWidth(118f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Vertices", $"{summary.VertexCount:n0}", GUILayout.MaxWidth(150f));
            }

            return summary;
        }

        private void DrawColliderBakerPresets()
        {
            GUILayout.Label("Preset", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Fast", "Recommended quick Real Hulls bake. Good default for crates, props, debris, and editor physics."), GUILayout.Height(30f)))
                {
                    ApplyColliderBakerPresetLow();
                }

                if (GUILayout.Button(new GUIContent("Balanced", "Recommended Real Hulls settings. No external tools, strong concave prop coverage."), GUILayout.Height(30f)))
                {
                    ApplyColliderBakerPresetMedium();
                }

                if (GUILayout.Button(new GUIContent("Accurate", "Higher quality Real Hulls bake for difficult hollow meshes."), GUILayout.Height(30f)))
                {
                    ApplyColliderBakerPresetHigh();
                }

                if (GUILayout.Button(new GUIContent("Maximum", "Highest Real Hulls search budget. Use when preview quality matters more than bake time."), GUILayout.Height(30f)))
                {
                    ApplyColliderBakerPresetMaximum();
                }
            }
        }

        private void DrawColliderBakerMethodSelector()
        {
            GUILayout.Label("Method", EditorStyles.miniBoldLabel);
            var current = settings.bakerColliderShape switch
            {
                RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.MultiHull => 0,
                RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls => 1,
                _ => 1
            };

            var next = GUILayout.Toolbar(current, new[]
            {
                new GUIContent("Smart Hulls", "Fast local convex decomposition with no external tools."),
                new GUIContent("Real Hulls", "Accurate built-in convex decomposition with no external tools or native plugins.")
            }, GUILayout.Height(28f));

            if (next == current)
            {
                return;
            }

            Undo.RecordObject(settings, "Set Collider Baker Method");
            settings.bakerColliderShape = next switch
            {
                0 => RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.MultiHull,
                _ => RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls
            };

            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            HandleColliderBakerSettingsChanged();
        }

        private void DrawColliderBakerShapeControls(RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary summary)
        {
            if (settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.MultiHull)
            {
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerSmartHullsUseFullBudget)), new GUIContent("Use Full Budget", "Keep splitting until Smart Hulls reaches Max Hulls or the mesh cannot be split further. Turn this off only when you want fewer automatic hulls."));
                DrawSmallHullMergeControls();
                DrawColliderPaddingControl();
                DrawCookMatchControls();
                EditorGUILayout.HelpBox("Smart Hulls is the fast built-in option. It follows sharp edges and open panels first, then spends Max Hulls on better coverage. Use Real Hulls when a difficult hollow mesh needs tighter collider coverage.", MessageType.None);
                return;
            }

            if (settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls)
            {
                DrawRealHullsControls(summary);
                return;
            }

            settings.bakerColliderShape = RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls;
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            DrawRealHullsControls(summary);
        }

        private void DrawRealHullsControls(RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary summary)
        {
            EditorGUILayout.HelpBox("Real Hulls is currently in beta. If a mesh is not represented accurately, please report the mesh and the settings used so the solver can be improved. Installing Unity's Burst package is optional, but recommended for faster bake times.", MessageType.Warning);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsQuality)), new GUIContent("Preset", "Internal Real Hulls quality multiplier. Fast reduces sampling/search, Balanced uses the slider values, Accurate increases them, and Maximum pushes them harder. The large preset buttons above also set the visible sliders."));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsShapeAccuracy)), new GUIContent("Shape Accuracy", "Concavity tolerance. Lower values split more aggressively and preserve hollow spaces; higher values accept looser, faster hulls."));
            DrawRealHullsCostWarning(summary);
            DrawColliderPaddingControl();
            DrawCookMatchControls();
            DrawSmallHullMergeControls();

            var nextShow = EditorGUILayout.Foldout(showRealHullsAdvanced, new GUIContent("Real Hulls Advanced", "Voxel, sampling, cache, and merge controls for the built-in solver."), true);
            if (nextShow != showRealHullsAdvanced)
            {
                showRealHullsAdvanced = nextShow;
                EditorPrefs.SetBool(BakerRealHullsAdvancedKey, showRealHullsAdvanced);
            }

            if (!showRealHullsAdvanced)
            {
                EditorGUILayout.HelpBox("Real Hulls is a built-in clean-room solver. It uses mesh sampling, voxel occupancy, concavity scoring, and recursive splitting without external tools or native plugins. Open Advanced to experiment with the solver budget.", MessageType.None);
                return;
            }

            EditorGUI.indentLevel++;
            try
            {
                EditorGUILayout.HelpBox("Advanced ranges are capped for production use. Raise them only when Preview Result shows a clear shape issue.", MessageType.None);
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsVoxelResolution)), new GUIContent("Voxel Resolution", "Internal occupancy grid resolution. Higher values make concavity analysis more precise, especially around holes and corners, but cost grows quickly."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsSurfaceSamples)), new GUIContent("Surface Samples", "Maximum source samples used for scoring and hull fitting. Higher values preserve more small features and worst-error points."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsMaxSplitCandidates)), new GUIContent("Split Candidates", "Candidate planes tested per recursive split. Higher values give the solver more chances to find a useful cut."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsMergePass)), new GUIContent("Merge Pass", "Merge compatible final hulls when the combined shape still matches the source."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerRealHullsCacheResults)), new GUIContent("Cache Results", "Reuse identical Real Hulls results for repeated previews and bakes."));
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawRealHullsCostWarning(RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary summary)
        {
            var report = BuildRealHullsCostReport(summary);
            EditorGUILayout.HelpBox(report.Message, report.MessageType);
        }

        private RealHullsCostReport BuildRealHullsCostReport(RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary summary)
        {
            if (summary.MeshCount <= 0)
            {
                return new RealHullsCostReport(
                    0f,
                    "Real Hulls cost: select a mesh to estimate preview and bake pressure.",
                    "No bakeable meshes are selected.",
                    MessageType.Info);
            }

            var meshFactor = Mathf.Max(0.25f, summary.TriangleCount / 1500f);
            var voxelFactor = Mathf.Pow(Mathf.Max(0.5f, settings.bakerRealHullsVoxelResolution / 96f), 2.15f);
            var sampleFactor = Mathf.Max(0.25f, settings.bakerRealHullsSurfaceSamples / 10000f);
            var candidateFactor = Mathf.Max(0.5f, settings.bakerRealHullsMaxSplitCandidates / 64f);
            var hullFactor = Mathf.Sqrt(Mathf.Max(1f, settings.bakerMaxHulls) / 6f);
            var accuracyFactor = Mathf.Clamp(0.03f / Mathf.Max(0.01f, settings.bakerRealHullsShapeAccuracy), 0.4f, 3f);
            var qualityFactor = settings.bakerRealHullsQuality switch
            {
                RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Fast => 0.6f,
                RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Accurate => 1.45f,
                RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Maximum => 2.4f,
                _ => 1f
            };

            var score = meshFactor * voxelFactor * sampleFactor * candidateFactor * hullFactor * accuracyFactor * qualityFactor;
            var level = score >= RealHullsExtremeCostThreshold
                ? "Extreme"
                : score >= RealHullsHighCostThreshold
                    ? "High"
                    : score >= 12f
                        ? "Medium"
                        : "Low";
            var messageType = score >= RealHullsHighCostThreshold ? MessageType.Warning : MessageType.Info;
            var selectionText = $"{summary.MeshCount:n0} mesh{(summary.MeshCount == 1 ? string.Empty : "es")}, {summary.VertexCount:n0} verts, {summary.TriangleCount:n0} tris";
            var message = $"Real Hulls cost: {level}. Selection: {selectionText}. Main drivers are Voxel Resolution, Surface Samples, Split Candidates, Max Hulls, and Shape Accuracy.";
            var dialogMessage =
                $"This Real Hulls bake is marked {level.ToLowerInvariant()} cost.\n\nSelection: {selectionText}\nMax Hulls: {settings.bakerMaxHulls}\nShape Accuracy: {settings.bakerRealHullsShapeAccuracy:0.###}\nVoxel Resolution: {settings.bakerRealHullsVoxelResolution}\nSurface Samples: {settings.bakerRealHullsSurfaceSamples:n0}\nSplit Candidates: {settings.bakerRealHullsMaxSplitCandidates}\nTime Limit: {FormatDuration(settings.bakerBakeTimeBudgetSeconds)}\n\nLower quality settings or continue with the bake.";
            return new RealHullsCostReport(score, message, dialogMessage, messageType);
        }

        private static string FormatDuration(int seconds)
        {
            seconds = Mathf.Max(1, seconds);
            if (seconds < 60)
            {
                return $"{seconds} seconds";
            }

            var minutes = seconds / 60f;
            return Mathf.Approximately(minutes, Mathf.Round(minutes))
                ? $"{Mathf.RoundToInt(minutes)} minutes"
                : $"{minutes:0.#} minutes";
        }

        private void DrawColliderPaddingControl()
        {
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerColliderPadding)), new GUIContent("Padding", "Signed hull padding shown in preview and bake. Positive expands hulls for extra clearance; negative tightens them inward. The slider is intentionally narrow because values around +/-0.002 to 0.015 usually make the visible difference."));
        }

        private void DrawCookMatchControls()
        {
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerCookMatch)), new GUIContent("Cook Match", "Optional validation pass that probes Unity's convex cook and splits simple hulls that cook too far from the generated shape. Leave off for faster, more predictable bakes."));
            using (new EditorGUI.DisabledScope(!settings.bakerCookMatch))
            {
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerCookMatchTolerance)), new GUIContent("Cook Tolerance", "Allowed local-space difference between generated hull support and Unity convex collider support before a hull is split."));
            }
        }

        private void DrawColliderBakerPreviewControls()
        {
            var canPreview = CanPreviewColliderBakerMethod();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(!canPreview))
                {
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerPreviewResult)), new GUIContent("Enable Preview", "Allow temporary colored hull meshes in the Scene view. Previews rebuild only when Refresh Preview is pressed."));
                }

                if (!canPreview)
                {
                    EditorGUILayout.HelpBox("Preview is available for Smart Hulls and Real Hulls. Switch to one of those methods to preview baked colliders.", MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(!settings.bakerPreviewResult || !canPreview))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent("Refresh Preview", "Rebuild the temporary collider preview for the current selection."), EditorStyles.miniButtonLeft))
                        {
                            serializedSettings.ApplyModifiedProperties();
                            RealPhysicsPlacerColliderPreview.Refresh(settings);
                        }

                        if (GUILayout.Button(new GUIContent("Clear Preview", "Remove temporary collider preview meshes from the scene."), EditorStyles.miniButtonRight))
                        {
                            RealPhysicsPlacerColliderPreview.Clear();
                        }
                    }
                }
            }
        }

        private void ApplyColliderBakerPresetLow()
        {
            Undo.RecordObject(settings, "Set Low Collider Baker Preset");
            settings.bakerColliderShape = RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls;
            settings.bakerBakeTimeBudgetSeconds = 60;
            settings.bakerMaxHulls = 6;
            settings.bakerMaxHullVertices = 96;
            settings.bakerRealHullsQuality = RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Fast;
            settings.bakerRealHullsVoxelResolution = 96;
            settings.bakerRealHullsSurfaceSamples = 10000;
            settings.bakerRealHullsMaxSplitCandidates = 64;
            settings.bakerRealHullsShapeAccuracy = 0.03f;
            settings.bakerRealHullsMergePass = true;
            settings.bakerRealHullsCacheResults = true;
            settings.bakerSmartHullsUseFullBudget = true;
            settings.bakerSmartHullSplitSearch = 2;
            settings.bakerSmartHullOpenEdgeBias = 0.2f;
            settings.bakerSmartHullMinSplitGain = 0f;
            settings.bakerMergeSmallHulls = false;
            settings.bakerSmallHullMergeExpansion = 1.25f;
            settings.bakerColliderPadding = 0f;
            settings.bakerCookMatch = false;
            settings.bakerCookMatchTolerance = 0.035f;
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            HandleColliderBakerSettingsChanged();
        }

        private void ApplyColliderBakerPresetMedium()
        {
            Undo.RecordObject(settings, "Set Medium Collider Baker Preset");
            settings.bakerColliderShape = RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls;
            settings.bakerBakeTimeBudgetSeconds = 60;
            settings.bakerMaxHulls = 6;
            settings.bakerMaxHullVertices = 96;
            settings.bakerRealHullsQuality = RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Fast;
            settings.bakerRealHullsVoxelResolution = 96;
            settings.bakerRealHullsSurfaceSamples = 10000;
            settings.bakerRealHullsMaxSplitCandidates = 64;
            settings.bakerRealHullsShapeAccuracy = 0.03f;
            settings.bakerRealHullsMergePass = true;
            settings.bakerRealHullsCacheResults = true;
            settings.bakerSmartHullsUseFullBudget = true;
            settings.bakerSmartHullSplitSearch = 4;
            settings.bakerSmartHullOpenEdgeBias = 0.35f;
            settings.bakerSmartHullMinSplitGain = 0f;
            settings.bakerMergeSmallHulls = false;
            settings.bakerSmallHullMergeTriangleLimit = 255;
            settings.bakerSmallHullMergeExpansion = 1.35f;
            settings.bakerColliderPadding = 0f;
            settings.bakerCookMatch = false;
            settings.bakerCookMatchTolerance = 0.035f;
            settings.bakerDrawGizmos = false;
            settings.bakerDrawWhenUnselected = false;
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            HandleColliderBakerSettingsChanged();
        }

        private void ApplyColliderBakerPresetHigh()
        {
            Undo.RecordObject(settings, "Set High Collider Baker Preset");
            settings.bakerColliderShape = RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls;
            settings.bakerBakeTimeBudgetSeconds = 120;
            settings.bakerMaxHulls = 10;
            settings.bakerMaxHullVertices = 112;
            settings.bakerRealHullsQuality = RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Accurate;
            settings.bakerRealHullsVoxelResolution = 96;
            settings.bakerRealHullsSurfaceSamples = 12000;
            settings.bakerRealHullsMaxSplitCandidates = 96;
            settings.bakerRealHullsShapeAccuracy = 0.02f;
            settings.bakerRealHullsMergePass = true;
            settings.bakerRealHullsCacheResults = true;
            settings.bakerMergeSmallHulls = false;
            settings.bakerSmallHullMergeTriangleLimit = 255;
            settings.bakerSmallHullMergeExpansion = 1.2f;
            settings.bakerColliderPadding = 0f;
            settings.bakerCookMatch = false;
            settings.bakerCookMatchTolerance = 0.035f;
            settings.bakerDrawGizmos = false;
            settings.bakerDrawWhenUnselected = false;
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            HandleColliderBakerSettingsChanged();
        }

        private void ApplyColliderBakerPresetMaximum()
        {
            Undo.RecordObject(settings, "Set Maximum Collider Baker Preset");
            settings.bakerColliderShape = RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls;
            settings.bakerBakeTimeBudgetSeconds = 300;
            settings.bakerMaxHulls = 16;
            settings.bakerMaxHullVertices = 128;
            settings.bakerRealHullsQuality = RealPhysicsPlacer.RealPhysicsPlacerRealHullsQuality.Maximum;
            settings.bakerRealHullsVoxelResolution = 96;
            settings.bakerRealHullsSurfaceSamples = 12000;
            settings.bakerRealHullsMaxSplitCandidates = 128;
            settings.bakerRealHullsShapeAccuracy = 0.018f;
            settings.bakerRealHullsMergePass = true;
            settings.bakerRealHullsCacheResults = true;
            settings.bakerMergeSmallHulls = false;
            settings.bakerSmallHullMergeTriangleLimit = 255;
            settings.bakerSmallHullMergeExpansion = 1.15f;
            settings.bakerColliderPadding = 0f;
            settings.bakerCookMatch = false;
            settings.bakerCookMatchTolerance = 0.02f;
            settings.bakerDrawGizmos = false;
            settings.bakerDrawWhenUnselected = false;
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            HandleColliderBakerSettingsChanged();
        }

        private void DrawSmallHullMergeControls()
        {
            EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerMergeSmallHulls)), new GUIContent("Connect Small Hulls", "Bake-time consolidation for neighboring small hulls. It reduces collider count without changing live physics settings."));
            using (new EditorGUI.DisabledScope(!settings.bakerMergeSmallHulls))
            {
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerSmallHullMergeTriangleLimit)), new GUIContent("Connect Limit", "Only hulls whose combined source mesh stays under this triangle count can be connected."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerSmallHullMergeExpansion)), new GUIContent("Connect Slack", "Lower values protect holes and open interiors. Higher values connect more hulls but can bridge empty space."));
            }
        }

        private void DrawBakerLodControls()
        {
            var lodMaskProperty = serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerLodMask));
            var nextMask = ResolveBakerLodMask(lodMaskProperty.intValue);

            var nextShow = EditorGUILayout.Foldout(showBakerLods, new GUIContent($"Bake LODs ({FormatBakerLodMask(nextMask)})", "Choose which LOD renderer levels are baked. Objects without an LOD Group are always included."), true);
            if (nextShow != showBakerLods)
            {
                showBakerLods = nextShow;
                EditorPrefs.SetBool(BakerLodFoldoutKey, showBakerLods);
            }

            if (showBakerLods)
            {
                var useLowest = nextMask == RealPhysicsPlacerSettings.LowestBakerLodMask;
                var nextUseLowest = GUILayout.Toggle(useLowest, new GUIContent("Lowest", "Bake only the lowest available LOD in each LOD Group. This is the default collider-friendly option."), EditorStyles.miniButton, GUILayout.MinWidth(72f));
                if (nextUseLowest != useLowest)
                {
                    nextMask = nextUseLowest
                        ? RealPhysicsPlacerSettings.LowestBakerLodMask
                        : 1;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var i = 0; i < RealPhysicsPlacerSettings.BakerLodCount; i++)
                    {
                        var bit = 1 << i;
                        var selected = nextMask != RealPhysicsPlacerSettings.LowestBakerLodMask && (nextMask & bit) != 0;
                        var nextSelected = GUILayout.Toggle(selected, new GUIContent($"LOD{i}", $"Include LOD{i} renderers when baking objects that use an LOD Group."), EditorStyles.miniButton, GUILayout.MinWidth(42f));
                        if (nextSelected)
                        {
                            nextMask |= bit;
                        }
                        else
                        {
                            nextMask &= ~bit;
                        }
                    }
                }
            }

            lodMaskProperty.intValue = nextMask <= 0
                ? RealPhysicsPlacerSettings.DefaultBakerLodMask
                : Mathf.Clamp(nextMask, 1, RealPhysicsPlacerSettings.AllBakerLodMask);
        }

        private static string FormatBakerLodMask(int mask)
        {
            if (mask <= 0)
            {
                return "Lowest";
            }

            mask = Mathf.Clamp(mask, 1, RealPhysicsPlacerSettings.AllBakerLodMask);
            var label = string.Empty;
            for (var i = 0; i < RealPhysicsPlacerSettings.BakerLodCount; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                label = string.IsNullOrEmpty(label) ? $"LOD{i}" : $"{label}, LOD{i}";
            }

            return label;
        }

        private int GetEffectiveBakerLodMask()
        {
            return ResolveBakerLodMask(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerLodMask)).intValue);
        }

        private static int ResolveBakerLodMask(int mask)
        {
            return mask <= 0
                ? RealPhysicsPlacerSettings.DefaultBakerLodMask
                : Mathf.Clamp(mask, 1, RealPhysicsPlacerSettings.AllBakerLodMask);
        }

        private void DrawBakerOutputControls()
        {
            var nextShow = EditorGUILayout.Foldout(showBakerOutput, new GUIContent("Output Options", "Baked collider replacement, gizmo, and generated child settings."), true);
            if (nextShow != showBakerOutput)
            {
                showBakerOutput = nextShow;
                EditorPrefs.SetBool(BakerOutputFoldoutKey, showBakerOutput);
            }

            if (!showBakerOutput)
            {
                return;
            }

            EditorGUI.indentLevel++;
            try
            {
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerReplaceExisting)), new GUIContent("Replace Baked", "Delete previous baked collider children before creating new ones."));
                DrawAssetFolderControl(
                    "Output Folder",
                    nameof(RealPhysicsPlacerSettings.bakerOutputFolder),
                    RealPhysicsPlacerAssetPaths.DefaultBakedCollidersFolder,
                    "Where collider hull assets and the baked collider cache are written. Leave on Default to follow the installed package folder.");
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerDisableExistingColliders)), new GUIContent("Disable Existing", "Disable existing colliders on the baked object so only baked colliders are used."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerIncludeInactiveChildren)), new GUIContent("Include Inactive", "Include inactive child renderers and meshes when baking selection."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerSkipPrimitiveMeshes)), new GUIContent("Skip Primitives", "Skip built-in primitive meshes that already have simple accurate collider shapes."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerAddMeshFilters)), new GUIContent("Add Mesh Filters", "Add MeshFilter components to baked hull children so their shapes are easy to inspect."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerLogTimings)), new GUIContent("Log Timings", "Write per-stage collider bake timings to the Console. Useful for diagnosing slow collider bakes; off by default to keep user projects quiet."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerDrawGizmos)), new GUIContent("Draw Gizmos", "Draw baked collider outlines in the scene view."));
                using (new EditorGUI.DisabledScope(!settings.bakerDrawGizmos))
                {
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerDrawWhenUnselected)), new GUIContent("Always Draw", "Keep baked collider gizmos visible even when the object is not selected."));
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerDrawSolidGizmos)), new GUIContent("Solid Preview", "Draw a translucent filled preview for baked collider hulls."));
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.bakerGizmoColor)), new GUIContent("Gizmo Color", "Color used for baked collider gizmo outlines and previews."));
                }
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAssetFolderControl(string label, string propertyName, string defaultFolder, string tooltip)
        {
            var property = serializedSettings.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            var effectiveFolder = RealPhysicsPlacerAssetPaths.ResolveGeneratedOutputFolder(property.stringValue, defaultFolder);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
                EditorGUI.BeginChangeCheck();
                var nextFolder = EditorGUILayout.DelayedTextField(effectiveFolder);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyOutputFolderText(property, nextFolder, defaultFolder);
                    effectiveFolder = RealPhysicsPlacerAssetPaths.ResolveGeneratedOutputFolder(property.stringValue, defaultFolder);
                }

                if (GUILayout.Button(new GUIContent("Pick", "Choose a folder under this project's Assets folder."), GUILayout.Width(48f)))
                {
                    var selectedFolder = EditorUtility.OpenFolderPanel($"Choose {label}", RealPhysicsPlacerAssetPaths.ToAbsoluteAssetPath(effectiveFolder), string.Empty);
                    if (!string.IsNullOrEmpty(selectedFolder))
                    {
                        if (RealPhysicsPlacerAssetPaths.TryConvertAbsoluteProjectFolderToAssetPath(selectedFolder, out var assetPath))
                        {
                            property.stringValue = string.Equals(assetPath, defaultFolder, System.StringComparison.Ordinal)
                                ? string.Empty
                                : assetPath;
                            effectiveFolder = RealPhysicsPlacerAssetPaths.ResolveGeneratedOutputFolder(property.stringValue, defaultFolder);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Output Folder", "Choose a folder inside this project's Assets folder.", "OK");
                        }
                    }
                }

                if (GUILayout.Button(new GUIContent("Default", $"Use {defaultFolder}."), GUILayout.Width(64f)))
                {
                    property.stringValue = string.Empty;
                    effectiveFolder = defaultFolder;
                }
            }

            var defaultText = string.IsNullOrEmpty(property.stringValue)
                ? $"Using default: {effectiveFolder}"
                : $"Custom: {effectiveFolder}";
            EditorGUILayout.LabelField(defaultText, EditorStyles.miniLabel);
        }

        private static void ApplyOutputFolderText(SerializedProperty property, string folder, string defaultFolder)
        {
            var normalizedFolder = RealPhysicsPlacerAssetPaths.NormalizeGeneratedOutputFolder(folder);
            if (string.IsNullOrEmpty(normalizedFolder))
            {
                property.stringValue = string.Empty;
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    EditorUtility.DisplayDialog("Output Folder", "Output folders must be inside this project's Assets folder.", "OK");
                }

                return;
            }

            property.stringValue = string.Equals(normalizedFolder, defaultFolder, System.StringComparison.Ordinal)
                ? string.Empty
                : normalizedFolder;
        }

        private void DrawBakerActionButtons(RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary summary)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Bake Selection", "Generate baked convex compound colliders for the selected objects."), GUILayout.Height(32f)))
                {
                    serializedSettings.ApplyModifiedProperties();
                    if (!ConfirmColliderBake(summary))
                    {
                        return;
                    }

                    RealPhysicsPlacerColliderGenerator.BakeSelection(settings);

                    RealPhysicsPlacerColliderPreview.Clear();
                }

                if (GUILayout.Button(new GUIContent("Delete Baked", "Remove baked collider children from the selected objects."), GUILayout.Height(32f)))
                {
                    RealPhysicsPlacerColliderGenerator.DeleteSelection();
                }
            }
        }

        private bool ConfirmColliderBake(RealPhysicsPlacerColliderGenerator.ColliderBakeSelectionSummary summary)
        {
            if (settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls && summary.MeshCount > 0)
            {
                var realHullsReport = BuildRealHullsCostReport(summary);
                if (realHullsReport.Score < RealHullsExtremeCostThreshold && settings.bakerBakeTimeBudgetSeconds <= 300)
                {
                    return true;
                }

                return EditorUtility.DisplayDialog(
                    "RealTransforms Real Hulls Bake",
                    realHullsReport.DialogMessage,
                    "Bake",
                    "Cancel");
            }

            return true;
        }

        private void ResetKeyBindings()
        {
            Undo.RecordObject(settings, "Reset RealTransforms Key Bindings");
            settings.assistKeyBehavior = RealPhysicsPlacerAssistKeyBehavior.Toggle;
            settings.physicsAssistKey = KeyCode.CapsLock;
            settings.moveToolKey = KeyCode.W;
            settings.rotateToolKey = KeyCode.E;
            settings.arrangeToolKey = KeyCode.R;
            settings.actionKey = KeyCode.Space;
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            ClearKeyBindingCapture();
        }

        private void DrawAdvancedControls()
        {
            var nextShowAdvanced = EditorGUILayout.Foldout(showAdvanced, "Settings", true);
            if (nextShowAdvanced != showAdvanced)
            {
                showAdvanced = nextShowAdvanced;
                EditorPrefs.SetBool(AdvancedKey, showAdvanced);
            }

            if (!showAdvanced)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPhysicsPresetButtons();
                EditorGUILayout.Space(8f);

                GUILayout.Label("Collision", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.collisionMask)), new GUIContent("Collision Mask", "Scene layers that physics-assisted movement can collide with."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.includeTriggers)), new GUIContent("Include Triggers", "Allow trigger colliders to participate in editor physics contacts."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.ignoredTags)), new GUIContent("Ignored Tags", "Comma-separated Unity tags that the tool should ignore for collision detection."));
                EditorGUILayout.Space(4f);
                GUILayout.Label("Simulation", EditorStyles.boldLabel);
                var isolatedSceneProperty = serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useIsolatedPhysicsScene));
                EditorGUILayout.PropertyField(isolatedSceneProperty, new GUIContent("Private Physics Scene", "Performance option. RealTransforms copies nearby colliders into a hidden physics scene once when assist starts. Faster in busy scenes, but objects outside Scene Radius are not recognized until the next physics session."));
                using (new EditorGUI.DisabledScope(!isolatedSceneProperty.boolValue))
                {
                    EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.isolatedPhysicsRadius)), new GUIContent("Scene Radius", "How far around the selection RealTransforms copies colliders into the private physics scene at the start of assist. Larger sees more of the scene; smaller is faster but can miss distant walls or props."));
                }

                var globalPhysicsProperty = serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useGlobalPhysicsSettings));
                EditorGUILayout.PropertyField(globalPhysicsProperty, new GUIContent("Global Physics Settings", "Use one shared simulation profile for Move, Rotate, Arrange, and Drop. Turn this on when you want direct performance control without tuning each mode separately."));
                using (new EditorGUI.DisabledScope(!globalPhysicsProperty.boolValue))
                {
                    DrawGlobalSimulationSettings(serializedSettings);
                }

                EditorGUILayout.Space(4f);
                GUILayout.Label("Scene Proxies", EditorStyles.boldLabel);
                var proxyEnabledProperty = serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.generateSceneProxyColliders));
                var proxyAllProperty = serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useSceneProxyCollidersForAllObjects));
                EditorGUILayout.PropertyField(proxyEnabledProperty, new GUIContent("Scene Proxies", "Create temporary editor-only colliders for nearby scene geometry. RealTransforms pauses this system in Play Mode."));
                using (new EditorGUI.DisabledScope(!proxyEnabledProperty.boolValue))
                {
                    var proxyMode = proxyAllProperty.boolValue ? 1 : 0;
                    EditorGUI.BeginChangeCheck();
                    proxyMode = EditorGUILayout.Popup(
                        new GUIContent("Mode", "Missing Colliders Only only helps renderer-only scene objects. Prefer Mesh Proxies uses cached static mesh proxies nearby, which is usually more accurate than gameplay colliders for editor placement."),
                        proxyMode,
                        SceneProxyModeNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        proxyAllProperty.boolValue = proxyMode == 1;
                    }

                    var localProxyProperty = serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useLocalSceneProxyCache));
                    EditorGUILayout.PropertyField(localProxyProperty, new GUIContent("Local Cache", "Only keep proxy colliders near the current selection. This should stay on for large production scenes."));
                    using (new EditorGUI.DisabledScope(!localProxyProperty.boolValue))
                    {
                        EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.sceneProxyCacheRadius)), new GUIContent("Cache Radius", "Distance around the selection where temporary scene proxies are kept alive."));
                        EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.sceneProxyMeshRadius)), new GUIContent("Mesh Radius", "Distance around the selection where accurate static mesh proxies are allowed."));
                    }

                    EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useRendererBoundsWhenNoCollider)), new GUIContent("Bounds Fallback", "Let render-only objects participate using their renderer bounds when no collider or mesh proxy is available. Useful coverage, but it scans renderer bounds and can cost more in dense scenes."));

                    var nextShowProxyAdvanced = EditorGUILayout.Foldout(showProxyAdvanced, "Proxy Advanced", true);
                    if (nextShowProxyAdvanced != showProxyAdvanced)
                    {
                        showProxyAdvanced = nextShowProxyAdvanced;
                        EditorPrefs.SetBool(ProxyAdvancedKey, showProxyAdvanced);
                    }

                    if (showProxyAdvanced)
                    {
                        EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.sceneProxyCacheRefreshDistance)), new GUIContent("Refresh Distance", "How far the selection must move before RealTransforms refreshes local scene proxies."));
                    }
                }

                if (!RealPhysicsPlacerSceneTool.EditorPhysicsAllowed)
                {
                    EditorGUILayout.HelpBox("RealTransforms editor physics is paused in Play Mode.", MessageType.Info);
                }

                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.allowAxisSliding)), new GUIContent("Slide On Contact", "Let motion slide along contacted surfaces instead of fully blocking along the active axis."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.skinWidth)), new GUIContent("Clearance", "Small contact buffer used to keep placed objects from visually clipping into surfaces."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.maxMoveStep)), new GUIContent("Step Size", "Maximum movement slice used while checking collisions. Smaller values are more accurate and more expensive."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.fallbackHullPieces)), new GUIContent("Fallback Hulls", "Temporary collider generation for selected meshes with no collider. 1 creates one convex MeshCollider from the mesh; higher values split triangles into multiple convex hulls."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useCenterAsPhysicsTruth)), new GUIContent("Arrange Centre", "Use each object's collider/render bounds center as the truth point for Arrange spacing and clumping. Move and Rotate keep Unity pivot/Rigidbody behavior."));

                EditorGUILayout.Space(8f);
                GUILayout.Label("Rigidbody", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.temporaryBodyMass)), new GUIContent("Mass", "Mass assigned to temporary Rigidbodies that RealTransforms adds to selected objects during editor physics."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.temporaryBodyCollisionDetection)), new GUIContent("Collision Detection", "Collision mode used by temporary editor Rigidbodies. Discrete is fastest; Continuous Dynamic is safest for fast movement."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.temporaryBodyInterpolation)), new GUIContent("Interpolation", "Interpolation used by temporary editor Rigidbodies. None is cheaper; Interpolate can look smoother."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.temporaryBodyMaxAngularVelocity)), new GUIContent("Max Angular Velocity", "Global cap for temporary Rigidbody spin speed. Lower values can reduce solver cost and runaway spinning."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.preserveHigherExistingBodySolverIterations)), new GUIContent("Keep Higher Existing Solvers", "When an object already has a Rigidbody, preserve solver counts that are higher than the active mode profile. Turn off to force cheaper mode values."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.freezeSceneRigidbodiesDuringAssist)), new GUIContent("Freeze Scene Rigidbodies", "Freeze non-selected scene Rigidbodies while editor physics is active. Safer, but scans scene Rigidbodies when assist starts."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.preserveMovablePrefabParts)), new GUIContent("Preserve Movable Parts", "Keep the authored local positions and rotations of hinges, doors, wheels, and other Rigidbody or Joint parts while placing the prefab. Articulated selections are simulated as one temporary rigid shape."));

                EditorGUILayout.Space(8f);
                DrawModeSettingsFoldout();

                EditorGUILayout.Space(6f);
                GUILayout.Label("Indicators", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.displayCollisionIndicators)), new GUIContent("Show Contact Hints", "Draw small scene-view indicators when physics movement is blocked or sliding."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.collisionIndicatorDistance)), new GUIContent("Hint Distance", "Maximum scene-view distance at which contact hints are drawn."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.xAxisColor)), new GUIContent("X Axis Color", "Scene-view color for X-axis physics hints."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.yAxisColor)), new GUIContent("Y Axis Color", "Scene-view color for Y-axis physics hints."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.zAxisColor)), new GUIContent("Z Axis Color", "Scene-view color for Z-axis physics hints."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.blockedColor)), new GUIContent("Blocked Color", "Scene-view color used when movement is blocked by a contact."));

                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!RealPhysicsPlacerSceneTool.EditorPhysicsAllowed))
                    {
                        if (GUILayout.Button(new GUIContent("Rebuild Scan", "Recreate the temporary scene collision proxy cache.")))
                        {
                            RealPhysicsPlacerSceneTool.RebuildSceneProxies();
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Reset", "Restore RealTransforms settings to their product defaults.")))
                    {
                        Undo.RecordObject(settings, "Reset RealTransforms Preferences");
                        settings.ResetToDefaults();
                        EditorUtility.SetDirty(settings);
                        RealPhysicsPlacerSceneTool.RebuildSceneProxies();
                    }
                }
            }
        }

        private void DrawPhysicsPresetButtons()
        {
            GUILayout.Label("Physics Preset", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Performant", "Fastest profile. Enables the private physics scene, disables scene proxies, and uses cheaper Rigidbody settings. Best for speed, but private-scene radius limits what collisions are recognized."), GUILayout.Height(30f)))
                {
                    ApplyPhysicsPreset(RealPhysicsPlacerPhysicsPreset.Performant);
                }

                if (GUILayout.Button(new GUIContent("Balanced", "Recommended default. Normal editor physics, scene proxies off, Continuous Dynamic bodies, and low shared physics steps."), GUILayout.Height(30f)))
                {
                    ApplyPhysicsPreset(RealPhysicsPlacerPhysicsPreset.Balanced);
                }

                if (GUILayout.Button(new GUIContent("Accurate", "Highest contact fidelity. Normal editor physics, more nearby colliders, mesh proxies, more solver work, and continuous collision."), GUILayout.Height(30f)))
                {
                    ApplyPhysicsPreset(RealPhysicsPlacerPhysicsPreset.Accurate);
                }
            }
        }

        private void ApplyPhysicsPreset(RealPhysicsPlacerPhysicsPreset preset)
        {
            Undo.RecordObject(settings, "Apply RealTransforms Physics Preset");
            serializedSettings.ApplyModifiedProperties();
            settings.ApplyPhysicsPreset(preset);
            EditorUtility.SetDirty(settings);
            serializedSettings.Update();
            RealPhysicsPlacerSceneTool.MarkSceneProxyScanDirty();
        }

        private void DrawMoveModeSettings(SerializedProperty profile, bool includeBehavior)
        {
            if (profile == null)
            {
                return;
            }

            if (includeBehavior)
            {
                EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.preserveSelectionOffsets)), new GUIContent("Preserve Selection Offsets", "Keep the relative spacing between selected objects while moving them."));
                EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.controlRotationInPlacement)), new GUIContent("Control Rotation", "Allow physics contacts to rotate moved objects while they are being placed."));
                EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.readjustHandleOnMouseUp)), new GUIContent("Readjust Handle On Release", "Re-anchor the transform handle when the mouse is released so handoff back to Unity movement stays smooth."));
                EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.teleportWhenTooFar)), new GUIContent("Teleport When Too Far", "Snap back to the handle if the physics body falls too far behind. Leave off for smoother handoff."));
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.followSpeed)), new GUIContent("Move Max Speed", "Maximum speed the physics body can use while following the Unity handle."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.spring)), new GUIContent("Move Spring", "How strongly the object is pulled toward the handle while physics assist is active."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.damping)), new GUIContent("Move Damping", "How much linear motion is damped while following the handle."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.angularDamping)), new GUIContent("Move Spin Damping", "How quickly unwanted rotation calms down during move placement."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.targetSmoothTime)), new GUIContent("Move Smoothing", "Mouse-handoff smoothing time used before the physics target updates."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.targetMaxDistance)), new GUIContent("Move Leash", "Maximum distance the physics target can stay from the current body before being clamped."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.physicsAssistDistance)), new GUIContent("Contact Assist", "How close an object must be to collision before move physics engages."));

            if (includeBehavior)
            {
                using (new EditorGUI.DisabledScope(!settings.move.teleportWhenTooFar))
                {
                    EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.teleportDistance)), new GUIContent("Teleport Distance", "Distance from the handle that triggers a direct transform jump when Teleport When Too Far is enabled."));
                }
            }
        }

        private void DrawModeSettingsFoldout()
        {
            var nextShowModeSettings = EditorGUILayout.Foldout(showModeSettings, new GUIContent("Mode Settings", "Open tool-specific physics tuning for Move, Rotate, Arrange, or Drop."), true);
            if (nextShowModeSettings != showModeSettings)
            {
                showModeSettings = nextShowModeSettings;
                EditorPrefs.SetBool(ModeSettingsKey, showModeSettings);
            }

            if (!showModeSettings)
            {
                return;
            }

            EditorGUI.indentLevel++;
            try
            {
                var nextAdvancedModeIndex = EditorGUILayout.Popup(new GUIContent("Mode", "Choose which physics-assisted tool profile these settings edit."), advancedModeIndex, ModeNames);
                if (nextAdvancedModeIndex != advancedModeIndex)
                {
                    advancedModeIndex = nextAdvancedModeIndex;
                    EditorPrefs.SetInt(AdvancedModeKey, advancedModeIndex);
                }

                var mode = PrimaryModes[advancedModeIndex];
                if (mode != RealPhysicsPlacerMode.Alignment)
                {
                    EditorGUILayout.Space(4f);
                    GUILayout.Label("Simulation", EditorStyles.miniBoldLabel);
                    if (serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.useGlobalPhysicsSettings)).boolValue)
                    {
                        EditorGUILayout.HelpBox("Global Physics Settings is on. Simulation sliders are shared above.", MessageType.Info);
                    }
                    else
                    {
                        DrawModeSimulationSettings(mode);
                    }
                }

                EditorGUILayout.Space(4f);
                GUILayout.Label("Feel", EditorStyles.miniBoldLabel);
                DrawAdvancedModeSettings(mode);
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAdvancedModeSettings(RealPhysicsPlacerMode mode)
        {
            switch (mode)
            {
                case RealPhysicsPlacerMode.Rotation:
                    DrawRotateModeSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.rotate)), true);
                    break;
                case RealPhysicsPlacerMode.Alignment:
                    DrawArrangeModeSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.arrange)), true);
                    break;
                case RealPhysicsPlacerMode.Fall:
                    DrawDropModeSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.drop)), true);
                    break;
                default:
                    DrawMoveModeSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.move)), true);
                    break;
            }
        }

        private void DrawModeSimulationSettings(RealPhysicsPlacerMode mode)
        {
            switch (mode)
            {
                case RealPhysicsPlacerMode.Rotation:
                    DrawRotateSimulationSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.rotate)));
                    break;
                case RealPhysicsPlacerMode.Fall:
                    DrawDropSimulationSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.drop)));
                    break;
                default:
                    DrawMoveSimulationSettings(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.move)));
                    break;
            }
        }

        private static void DrawGlobalSimulationSettings(SerializedObject serializedSettings)
        {
            if (serializedSettings == null)
            {
                return;
            }

            EditorGUI.indentLevel++;
            try
            {
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.globalSimulationSpeed)), new GUIContent("Simulation Speed", "Shared time multiplier for Move, Rotate, Arrange, and Drop physics. Higher values respond faster but cost more and can become less stable."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.globalPhysicsStep)), new GUIContent("Physics Step", "Shared fixed simulation step. Smaller values are smoother and more accurate, but more expensive."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.globalMaxSubsteps)), new GUIContent("Max Steps", "Shared maximum physics steps per editor update. Higher values recover from slow frames but cost more."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.globalSolverIterations)), new GUIContent("Solver", "Shared position solver iterations for temporary editor Rigidbodies. Higher is more stable and slower."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.globalSolverVelocityIterations)), new GUIContent("Velocity Solver", "Shared velocity solver iterations for temporary editor Rigidbodies. Higher improves contact/friction stability at a CPU cost."));
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(nameof(RealPhysicsPlacerSettings.globalRotationConstraint)), new GUIContent("Rotation Constraint", "Freeze rotation around selected world axes during temporary editor physics."));
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawMoveSimulationSettings(SerializedProperty profile)
        {
            if (profile == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.simulationSpeed)), new GUIContent("Simulation Speed", "Time multiplier for move physics. Higher values feel stronger but cost more and can become less stable."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.physicsStep)), new GUIContent("Physics Step", "Fixed simulation step for move assist. Smaller values are smoother and more expensive."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.maxSubsteps)), new GUIContent("Max Steps", "Maximum move physics steps allowed per editor update. Higher values recover from slow frames but cost more."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.solverIterations)), new GUIContent("Solver", "Position solver iterations used for move contacts. Higher is more stable and slower."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.solverVelocityIterations)), new GUIContent("Velocity Solver", "Velocity solver iterations used for move contacts. Higher improves friction and bounce stability at a CPU cost."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.MoveModeSettings.rotationConstraint)), new GUIContent("Rotation Constraint", "Freeze rotation around selected world axes while move physics is active."));
        }

        private static void DrawRotateSimulationSettings(SerializedProperty profile)
        {
            if (profile == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.simulationSpeed)), new GUIContent("Simulation Speed", "Time multiplier for rotate physics. Higher values respond faster but can feel more forceful."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.physicsStep)), new GUIContent("Physics Step", "Fixed simulation step for rotate assist. Smaller values are smoother and more expensive."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.maxSubsteps)), new GUIContent("Max Steps", "Maximum rotate physics steps allowed per editor update."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.solverIterations)), new GUIContent("Solver", "Position solver iterations used while rotating against contacts."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.solverVelocityIterations)), new GUIContent("Velocity Solver", "Velocity solver iterations used to stabilize sliding and contact response during rotation."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.rotationConstraint)), new GUIContent("Rotation Constraint", "Freeze rotation around selected world axes while rotate physics is active."));
        }

        private static void DrawDropSimulationSettings(SerializedProperty profile)
        {
            if (profile == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.simulationSpeed)), new GUIContent("Simulation Speed", "Time multiplier for drop simulation. Higher values settle faster but cost more per editor update."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.physicsStep)), new GUIContent("Physics Step", "Fixed simulation step for drop physics. Smaller values are more stable and slower."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.maxSubsteps)), new GUIContent("Max Steps", "Maximum drop physics steps allowed per editor update."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.bodySolverIterations)), new GUIContent("Solver", "Position solver iterations used by dropped rigidbodies."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.bodySolverVelocityIterations)), new GUIContent("Velocity Solver", "Velocity solver iterations used by dropped rigidbodies."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.rotationConstraint)), new GUIContent("Rotation Constraint", "Freeze rotation around selected world axes while drop physics is active."));
        }

        private static void DrawArrangeModeSettings(SerializedProperty profile, bool includeFullProfile)
        {
            if (profile == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.ArrangeModeSettings.handleStrength)), new GUIContent("Handle Strength", "How strongly arrange handles push or pull selected objects into spacing."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.ArrangeModeSettings.padding)), new GUIContent("Spacing", "Extra space kept between arranged objects after their bounds touch."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.ArrangeModeSettings.primaryAction)), new GUIContent("Space Action", "Action used by the primary action key while Arrange is active."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.ArrangeModeSettings.toolAction)), new GUIContent("Arrange Key Action", "Action used by pressing the Arrange tool key again while Arrange is already active."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.ArrangeModeSettings.collisionAware)), new GUIContent("Physics Aware", "Use collision checks while arranging instead of only changing transform spacing."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.ArrangeModeSettings.collideSelectedObjects)), new GUIContent("Selected Collide", "Let selected objects collide with each other while arranging."));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Clump", "Pull selected objects closer while respecting arrange collision settings.")))
                {
                    RealPhysicsPlacerSceneTool.ArrangeSelection(0.8f);
                }

                if (GUILayout.Button(new GUIContent("Spread", "Push selected objects farther apart while respecting arrange collision settings.")))
                {
                    RealPhysicsPlacerSceneTool.ArrangeSelection(1.25f);
                }
            }
        }

        private static void DrawRotateModeSettings(SerializedProperty profile, bool includeFullProfile)
        {
            if (profile == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.angularSpring)), new GUIContent("Turn Pull", "How strongly rotation follows the Unity rotate handle."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.angularDamping)), new GUIContent("Turn Damping", "How quickly rotational motion settles after input slows down."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.maxAngularSpeed)), new GUIContent("Turn Max Speed", "Maximum angular speed allowed while rotating with physics assist."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.linearDamping)), new GUIContent("Slide Damping", "How much sideways drift is damped while rotating against contacts."));

            if (includeFullProfile)
            {
                EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.maxStepDegrees)), new GUIContent("Turn Step", "Maximum degrees applied per rotation solve. Smaller values are smoother around contacts."));
                EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.RotateModeSettings.contactTolerance)), new GUIContent("Contact Tolerance", "Small buffer used to prevent rotation from sinking into nearby surfaces."));
            }
        }

        private static void DrawDropModeSettings(SerializedProperty profile, bool includeFullProfile)
        {
            if (profile == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.gravity)), new GUIContent("Gravity", "Gravity vector applied while dropping selected objects."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.solverIterations)), new GUIContent("Drop Passes", "Extra drop-placement passes used to settle selected objects into stable contact."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.maxSpeed)), new GUIContent("Max Speed", "Maximum fall speed while the editor drop simulation is running."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative(nameof(RealPhysicsPlacerSettings.DropModeSettings.settleSteps)), new GUIContent("Settle", "Additional quiet simulation steps after drop input stops."));
        }

        private static int GetPrimaryModeIndex(RealPhysicsPlacerMode mode)
        {
            for (var i = 0; i < PrimaryModes.Length; i++)
            {
                if (PrimaryModes[i] == mode)
                {
                    return i;
                }
            }

            return 0;
        }

        private static string FormatKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.None ? "Unbound" : keyCode.ToString();
        }
    }
}
