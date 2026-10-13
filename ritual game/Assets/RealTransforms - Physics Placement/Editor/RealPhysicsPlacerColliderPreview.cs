﻿using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealPhysicsPlacer.EditorTools
{
    [InitializeOnLoad]
    internal static class RealPhysicsPlacerColliderPreview
    {
        // Draws non-persistent preview hulls for the baker. Smart Hulls and
        // Real Hulls preview through the same local decomposition path.
        private const string PreviewRootName = "__RealTransforms_ColliderPreview";
        private const double LocalPreviewStepDelaySeconds = 0.06d;
        private static readonly PreviewTheme[] PreviewThemes =
        {
            new PreviewTheme(new Color(1f, 0.42f, 0.02f, 0.15f), new Color(1f, 0.62f, 0.05f, 1f), new Color(1f, 0.78f, 0.16f, 1f)),
            new PreviewTheme(new Color(1f, 0.73f, 0.04f, 0.14f), new Color(1f, 0.86f, 0.18f, 1f), new Color(1f, 0.9f, 0.35f, 1f)),
            new PreviewTheme(new Color(0.12f, 0.72f, 1f, 0.12f), new Color(0.18f, 0.82f, 1f, 0.95f), new Color(0.58f, 0.9f, 1f, 1f)),
            new PreviewTheme(new Color(1f, 0.22f, 0.06f, 0.13f), new Color(1f, 0.36f, 0.12f, 0.96f), new Color(1f, 0.58f, 0.32f, 1f)),
            new PreviewTheme(new Color(1f, 0.55f, 0.12f, 0.12f), new Color(1f, 0.7f, 0.22f, 0.98f), new Color(1f, 0.8f, 0.36f, 1f)),
            new PreviewTheme(new Color(0.24f, 0.9f, 0.72f, 0.1f), new Color(0.26f, 0.95f, 0.78f, 0.9f), new Color(0.62f, 1f, 0.88f, 1f))
        };

        private static readonly Color PreviewBadgeBackground = new Color(0.035f, 0.026f, 0.018f, 0.82f);

        private static readonly List<Material> Materials = new List<Material>();
        private static readonly List<Mesh> Meshes = new List<Mesh>();
        private static readonly List<RealPhysicsPlacerColliderGenerator.ColliderPreviewHull> PreviewHulls = new List<RealPhysicsPlacerColliderGenerator.ColliderPreviewHull>();
        private static readonly List<PreviewVisual> PreviewVisuals = new List<PreviewVisual>();
        private static readonly Queue<RealPhysicsPlacerColliderGenerator.ColliderPreviewSource> PreviewQueue = new Queue<RealPhysicsPlacerColliderGenerator.ColliderPreviewSource>();
        private static readonly Dictionary<Mesh, int[]> EdgeCache = new Dictionary<Mesh, int[]>();
        private static GameObject root;
        private static bool localPreviewActive;
        private static bool localPreviewBudgetActive;
        private static RealPhysicsPlacerSettings localPreviewSettings;
        private static int localPreviewTotal;
        private static double localPreviewNextStepTime;
        private static int progressId = -1;
        private static GUIStyle labelStyle;

        static RealPhysicsPlacerColliderPreview()
        {
            EditorApplication.update += UpdateLocalPreview;
            SceneView.duringSceneGui += DrawPreviewOverlay;
            EditorApplication.playModeStateChanged += _ => Clear();
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        internal static bool HasPreview => root != null;

        internal static void Refresh(RealPhysicsPlacerSettings settings)
        {
            Clear();
            if (settings != null
                && (settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.MultiHull
                    || settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls))
            {
                StartLocalMultiHullPreview(settings);
                return;
            }

            var hulls = RealPhysicsPlacerColliderGenerator.BuildPreviewSelection(settings);
            if (hulls == null || hulls.Count == 0)
            {
                SceneView.RepaintAll();
                return;
            }

            root = new GameObject(PreviewRootName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var i = 0; i < hulls.Count; i++)
            {
                AddPreviewHull(hulls[i], i);
            }

            SceneView.RepaintAll();
        }

        private static void StartLocalMultiHullPreview(RealPhysicsPlacerSettings settings)
        {
            var sources = RealPhysicsPlacerColliderGenerator.GetPreviewSources(settings);
            if (sources.Count == 0)
            {
                SceneView.RepaintAll();
                return;
            }

            root = new GameObject(PreviewRootName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var i = 0; i < sources.Count; i++)
            {
                PreviewQueue.Enqueue(sources[i]);
            }

            localPreviewActive = true;
            localPreviewSettings = settings;
            localPreviewTotal = sources.Count;
            localPreviewNextStepTime = EditorApplication.timeSinceStartup + LocalPreviewStepDelaySeconds;
            RealPhysicsPlacerColliderBakeBudget.Begin(settings);
            localPreviewBudgetActive = true;
            var label = settings.bakerColliderShape == RealPhysicsPlacer.RealPhysicsPlacerBakedColliderShape.RealHulls
                ? "Real Hulls"
                : "Smart Hulls";
            progressId = Progress.Start($"RealTransforms {label} Preview", $"Starting {label} preview", Progress.Options.Sticky);
            SceneView.RepaintAll();
        }

        private static void UpdateLocalPreview()
        {
            if (!localPreviewActive)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < localPreviewNextStepTime)
            {
                return;
            }

            if (PreviewQueue.Count == 0)
            {
                localPreviewActive = false;
                localPreviewSettings = null;
                localPreviewTotal = 0;
                localPreviewNextStepTime = 0d;
                EndLocalPreviewBudget();
                FinishProgress(Progress.Status.Succeeded);
                SceneView.RepaintAll();
                return;
            }

            if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
            {
                Debug.LogWarning($"RealTransforms stopped collider preview after {localPreviewSettings.bakerBakeTimeBudgetSeconds} seconds. Increase Time Limit for a slower, more detailed preview.");
                PreviewQueue.Clear();
                localPreviewActive = false;
                localPreviewSettings = null;
                localPreviewTotal = 0;
                localPreviewNextStepTime = 0d;
                EndLocalPreviewBudget();
                FinishProgress(Progress.Status.Canceled);
                SceneView.RepaintAll();
                return;
            }

            var remainingBefore = PreviewQueue.Count;
            var source = PreviewQueue.Dequeue();
            if (progressId >= 0)
            {
                var completed = Mathf.Max(0, localPreviewTotal - remainingBefore);
                Progress.Report(progressId, localPreviewTotal <= 0 ? 0f : completed / (float)localPreviewTotal, $"{source?.Name ?? "Smart Hulls"}");
            }

            if (source?.Mesh == null)
            {
                return;
            }

            try
            {
                var pieces = RealPhysicsPlacerColliderDecomposer.Decompose(
                    source.Mesh,
                    localPreviewSettings != null ? localPreviewSettings : RealPhysicsPlacerSettings.GetOrCreate(),
                    source.Mesh.name);
                var startIndex = PreviewHulls.Count;
                RealPhysicsPlacerColliderGenerator.AppendPreviewHulls(PreviewHulls, source, pieces);
                for (var i = startIndex; i < PreviewHulls.Count; i++)
                {
                    AddPreviewHull(PreviewHulls[i], i);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"RealTransforms could not preview Smart Hull colliders for {source.Name}: {exception.Message}");
            }

            if (progressId >= 0)
            {
                var completed = Mathf.Max(0, localPreviewTotal - PreviewQueue.Count);
                Progress.Report(progressId, localPreviewTotal <= 0 ? 1f : completed / (float)localPreviewTotal, $"{source.Name}: ready");
            }

            SceneView.RepaintAll();
            localPreviewNextStepTime = EditorApplication.timeSinceStartup + LocalPreviewStepDelaySeconds;
        }

        internal static void Clear()
        {
            localPreviewActive = false;
            localPreviewSettings = null;
            localPreviewTotal = 0;
            localPreviewNextStepTime = 0d;
            EndLocalPreviewBudget();
            PreviewQueue.Clear();
            PreviewHulls.Clear();
            PreviewVisuals.Clear();
            EdgeCache.Clear();
            FinishProgress(Progress.Status.Canceled);

            foreach (var staleRoot in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (staleRoot != null && staleRoot.name == PreviewRootName)
                {
                    Object.DestroyImmediate(staleRoot);
                }
            }

            root = null;
            for (var i = 0; i < Meshes.Count; i++)
            {
                if (Meshes[i] != null)
                {
                    Object.DestroyImmediate(Meshes[i]);
                }
            }

            Meshes.Clear();
            for (var i = 0; i < Materials.Count; i++)
            {
                if (Materials[i] != null)
                {
                    Object.DestroyImmediate(Materials[i]);
                }
            }

            Materials.Clear();
            SceneView.RepaintAll();
        }

        private static void EndLocalPreviewBudget()
        {
            if (!localPreviewBudgetActive)
            {
                return;
            }

            RealPhysicsPlacerColliderBakeBudget.End();
            localPreviewBudgetActive = false;
        }

        private static void FinishProgress(Progress.Status status)
        {
            if (progressId < 0)
            {
                return;
            }

            Progress.Finish(progressId, status);
            progressId = -1;
        }

        private static void AddPreviewHull(RealPhysicsPlacerColliderGenerator.ColliderPreviewHull hull, int index)
        {
            if (hull?.Mesh == null)
            {
                return;
            }

            if (root == null)
            {
                root = new GameObject(PreviewRootName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            hull.Mesh.hideFlags = HideFlags.HideAndDontSave;
            Meshes.Add(hull.Mesh);
            var child = new GameObject(hull.Name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            child.transform.SetParent(root.transform, false);
            child.transform.position = hull.Position;
            child.transform.rotation = hull.Rotation;
            child.transform.localScale = hull.Scale;

            var meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = hull.Mesh;

            var renderer = child.AddComponent<MeshRenderer>();
            var theme = GetPreviewTheme(index);
            renderer.sharedMaterial = CreatePreviewMaterial(theme.Fill);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            PreviewVisuals.Add(new PreviewVisual(child.transform, hull.Mesh, index + 1, theme));
        }

        private static void DrawPreviewOverlay(SceneView sceneView)
        {
            if (root == null || PreviewVisuals.Count == 0)
            {
                return;
            }

            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            for (var i = 0; i < PreviewVisuals.Count; i++)
            {
                var visual = PreviewVisuals[i];
                if (visual.Transform == null || visual.Mesh == null)
                {
                    continue;
                }

                DrawPreviewWire(visual);
                DrawPreviewLabel(sceneView, visual);
            }

            Handles.zTest = previousZTest;
            Handles.color = previousColor;
        }

        private static void DrawPreviewWire(PreviewVisual visual)
        {
            var vertices = visual.Mesh.vertices;
            var edges = GetMeshEdges(visual.Mesh);
            if (vertices == null || edges == null || edges.Length < 2)
            {
                return;
            }

            var matrix = visual.Transform.localToWorldMatrix;
            Handles.color = visual.Theme.Line;
            for (var i = 0; i < edges.Length - 1; i += 2)
            {
                var a = edges[i];
                var b = edges[i + 1];
                if (a < 0 || b < 0 || a >= vertices.Length || b >= vertices.Length)
                {
                    continue;
                }

                Handles.DrawAAPolyLine(2.2f, matrix.MultiplyPoint3x4(vertices[a]), matrix.MultiplyPoint3x4(vertices[b]));
            }
        }

        private static int[] GetMeshEdges(Mesh mesh)
        {
            if (mesh == null)
            {
                return System.Array.Empty<int>();
            }

            if (EdgeCache.TryGetValue(mesh, out var cachedEdges))
            {
                return cachedEdges;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || vertices.Length < 3 || triangles == null || triangles.Length < 3)
            {
                EdgeCache[mesh] = System.Array.Empty<int>();
                return EdgeCache[mesh];
            }

            var edgeMap = new Dictionary<ulong, EdgeInfo>();
            for (var i = 0; i < triangles.Length - 2; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                {
                    continue;
                }

                AddMeshEdge(edgeMap, a, b);
                AddMeshEdge(edgeMap, b, c);
                AddMeshEdge(edgeMap, c, a);
            }

            var edges = new List<int>(edgeMap.Count * 2);
            foreach (var pair in edgeMap)
            {
                var edge = pair.Value;
                edges.Add(edge.A);
                edges.Add(edge.B);
            }

            var result = edges.ToArray();
            EdgeCache[mesh] = result;
            return result;
        }

        private static void AddMeshEdge(Dictionary<ulong, EdgeInfo> edgeMap, int a, int b)
        {
            var min = (uint)Mathf.Min(a, b);
            var max = (uint)Mathf.Max(a, b);
            var key = ((ulong)min << 32) | max;
            if (edgeMap.TryGetValue(key, out var existing))
            {
                existing.Count++;
                edgeMap[key] = existing;
                return;
            }

            edgeMap[key] = new EdgeInfo(a, b);
        }

        private static void DrawPreviewLabel(SceneView sceneView, PreviewVisual visual)
        {
            var camera = sceneView?.camera;
            if (camera == null)
            {
                return;
            }

            var bounds = visual.Mesh.bounds;
            var center = visual.Transform.TransformPoint(bounds.center);
            if (Vector3.Dot(camera.transform.forward, center - camera.transform.position) <= 0f)
            {
                return;
            }

            var handleSize = HandleUtility.GetHandleSize(center);
            var labelWorld = center + camera.transform.up * handleSize * 0.065f;
            var labelPoint = HandleUtility.WorldToGUIPoint(labelWorld);
            if (float.IsNaN(labelPoint.x) || float.IsNaN(labelPoint.y))
            {
                return;
            }

            var label = new GUIContent(visual.Number.ToString("00"));
            var style = GetLabelStyle();
            var size = style.CalcSize(label);
            size.x = Mathf.Max(28f, size.x + 12f);
            size.y = 18f;
            var rect = new Rect(labelPoint.x - size.x * 0.5f, labelPoint.y - size.y * 0.5f, size.x, size.y);
            var borderRect = new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f);

            Handles.BeginGUI();
            var previousColor = GUI.color;
            EditorGUI.DrawRect(borderRect, visual.Theme.Line);
            EditorGUI.DrawRect(rect, PreviewBadgeBackground);
            GUI.color = visual.Theme.Label;
            GUI.Label(rect, label, style);
            GUI.color = previousColor;
            Handles.EndGUI();
        }

        private static GUIStyle GetLabelStyle()
        {
            if (labelStyle != null)
            {
                return labelStyle;
            }

            labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
            return labelStyle;
        }

        private static Material CreatePreviewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("HDRP/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Hidden/Internal-Colored");

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
                renderQueue = (int)RenderQueue.Transparent
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_ALPHABLEND_ON");
            Materials.Add(material);
            return material;
        }

        private static PreviewTheme GetPreviewTheme(int index)
        {
            if (PreviewThemes.Length == 0)
            {
                return new PreviewTheme(new Color(1f, 0.45f, 0.04f, 0.14f), new Color(1f, 0.64f, 0.08f, 1f), new Color(1f, 0.8f, 0.22f, 1f));
            }

            return PreviewThemes[Mathf.Abs(index) % PreviewThemes.Length];
        }

        private readonly struct PreviewVisual
        {
            internal readonly Transform Transform;
            internal readonly Mesh Mesh;
            internal readonly int Number;
            internal readonly PreviewTheme Theme;

            internal PreviewVisual(Transform transform, Mesh mesh, int number, PreviewTheme theme)
            {
                Transform = transform;
                Mesh = mesh;
                Number = number;
                Theme = theme;
            }
        }

        private struct EdgeInfo
        {
            internal readonly int A;
            internal readonly int B;
            internal int Count;

            internal EdgeInfo(int a, int b)
            {
                A = a;
                B = b;
                Count = 1;
            }
        }

        private readonly struct PreviewTheme
        {
            internal readonly Color Fill;
            internal readonly Color Line;
            internal readonly Color Label;

            internal PreviewTheme(Color fill, Color line, Color label)
            {
                Fill = fill;
                Line = line;
                Label = label;
            }
        }
    }
}
