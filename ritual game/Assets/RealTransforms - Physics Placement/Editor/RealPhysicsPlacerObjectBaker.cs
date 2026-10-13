﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    internal static class RealPhysicsPlacerObjectBaker
    {
        // Combines selected MeshFilter/MeshRenderer pairs into a clean reusable
        // mesh/prefab, optionally using Unity's FBX Exporter when it is installed.
        private const string FbxExporterPackage = "com.unity.formats.fbx";
        private const string FbxExporterTypeName = "UnityEditor.Formats.Fbx.Exporter.ModelExporter";
        private const string FbxExporterMethodName = "ExportObject";

        internal readonly struct SelectionSummary
        {
            internal readonly int RootCount;
            internal readonly int MeshCount;
            internal readonly int SubMeshCount;
            internal readonly int MaterialCount;
            internal readonly int VertexCount;

            internal SelectionSummary(int rootCount, int meshCount, int subMeshCount, int materialCount, int vertexCount)
            {
                RootCount = rootCount;
                MeshCount = meshCount;
                SubMeshCount = subMeshCount;
                MaterialCount = materialCount;
                VertexCount = vertexCount;
            }
        }

        internal readonly struct BakeResult
        {
            internal readonly string MeshPath;
            internal readonly string PrefabPath;
            internal readonly string FbxPath;
            internal readonly int MeshCount;
            internal readonly int MaterialCount;

            internal BakeResult(string meshPath, string prefabPath, string fbxPath, int meshCount, int materialCount)
            {
                MeshPath = meshPath;
                PrefabPath = prefabPath;
                FbxPath = fbxPath;
                MeshCount = meshCount;
                MaterialCount = materialCount;
            }
        }

        private sealed class MeshSource
        {
            internal MeshFilter MeshFilter;
            internal MeshRenderer Renderer;
            internal Mesh Mesh;
            internal Material[] Materials;
        }

        private sealed class MaterialBucket
        {
            internal readonly Material Material;
            internal readonly List<CombineInstance> Combines = new List<CombineInstance>();

            internal MaterialBucket(Material material)
            {
                Material = material;
            }
        }

        private readonly struct BakePose
        {
            internal readonly Vector3 Position;
            internal readonly Quaternion Rotation;
            internal readonly Matrix4x4 WorldToLocal;

            internal BakePose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
                WorldToLocal = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            }
        }

        internal static SelectionSummary BuildSelectionSummary(RealPhysicsPlacerSettings settings)
        {
            settings ??= RealPhysicsPlacerSettings.GetOrCreate();
            var roots = GetSelectionRoots();
            var sources = CollectSources(roots, settings.objectBakerIncludeInactiveChildren);
            var materialCount = CountUniqueMaterials(sources);
            var subMeshCount = sources.Sum(source => Mathf.Max(1, source.Mesh.subMeshCount));
            var vertexCount = sources.Sum(source => source.Mesh.vertexCount);
            return new SelectionSummary(roots.Length, sources.Count, subMeshCount, materialCount, vertexCount);
        }

        internal static bool HasFbxExporter()
        {
            return ResolveFbxExportMethod() != null;
        }

        internal static BakeResult BakeSelection(RealPhysicsPlacerSettings settings)
        {
            settings ??= RealPhysicsPlacerSettings.GetOrCreate();
            var roots = GetSelectionRoots();
            if (roots.Length == 0)
            {
                EditorUtility.DisplayDialog("Object Baker", "Select one or more scene objects to bake.", "OK");
                return default;
            }

            var sources = CollectSources(roots, settings.objectBakerIncludeInactiveChildren);
            if (sources.Count == 0)
            {
                EditorUtility.DisplayDialog("Object Baker", "The selection does not contain any MeshFilter + MeshRenderer pairs to bake.", "OK");
                return default;
            }

            var assetFolder = settings.GetObjectBakerOutputFolder();
            EnsureFolder(assetFolder);
            var baseName = BuildBakeName(roots);
            var folder = BuildOutputFolder(assetFolder, roots, baseName);
            EnsureFolder(folder);

            try
            {
                if (EditorUtility.DisplayCancelableProgressBar("RealTransforms Object Baker", "Combining meshes by material", 0.2f))
                {
                    return default;
                }

                var pose = ResolveBakePose(roots, sources, settings.objectBakerPivot, settings.objectBakerRotation);
                var combined = BuildCombinedMesh(sources, pose.WorldToLocal, baseName, out var materials);
                if (combined == null || materials.Count == 0)
                {
                    EditorUtility.DisplayDialog("Object Baker", "No renderable mesh data could be combined.", "OK");
                    return default;
                }

                if (EditorUtility.DisplayCancelableProgressBar("RealTransforms Object Baker", "Saving combined mesh", 0.55f))
                {
                    Object.DestroyImmediate(combined);
                    return default;
                }

                var meshPath = SaveMeshAsset(combined, folder, baseName);
                var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) ?? combined;
                GameObject bakedObject = null;
                try
                {
                    bakedObject = CreateBakedObject(baseName, pose, savedMesh, materials);
                    var prefabPath = SavePrefabAsset(bakedObject, folder, baseName);
                    var fbxPath = string.Empty;

                    if (settings.objectBakerExportFbx)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("RealTransforms Object Baker", "Exporting FBX", 0.82f))
                        {
                            return new BakeResult(meshPath, prefabPath, string.Empty, sources.Count, materials.Count);
                        }

                        if (HasFbxExporter())
                        {
                            fbxPath = ExportFbx(bakedObject, folder, baseName);
                        }
                        else
                        {
                            Debug.LogWarning("RealTransforms Object Baker created the combined mesh and prefab. Install Unity's FBX Exporter package to generate FBX files from the same Bake Object button.");
                        }
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    PingResult(fbxPath, prefabPath, meshPath);
                    Debug.Log(BuildResultMessage(prefabPath, meshPath, fbxPath, settings.objectBakerExportFbx, sources.Count, materials.Count));
                    return new BakeResult(meshPath, prefabPath, fbxPath, sources.Count, materials.Count);
                }
                finally
                {
                    if (bakedObject != null)
                    {
                        Object.DestroyImmediate(bakedObject);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"RealTransforms Object Baker failed: {exception.Message}");
                EditorUtility.DisplayDialog("Object Baker", exception.Message, "OK");
                return default;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Mesh BuildCombinedMesh(
            List<MeshSource> sources,
            Matrix4x4 worldToBakeLocal,
            string baseName,
            out List<Material> materials)
        {
            // Meshes are bucketed by material first so the combined mesh preserves
            // material slots instead of flattening everything into one submesh.
            var buckets = new List<MaterialBucket>();
            for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var source = sources[sourceIndex];
                if (source?.Mesh == null || source.Mesh.vertexCount == 0)
                {
                    continue;
                }

                var subMeshCount = Mathf.Max(1, source.Mesh.subMeshCount);
                for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    if (source.Mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
                    {
                        Debug.LogWarning($"RealTransforms Object Baker skipped {source.Mesh.name} submesh {subMeshIndex}: only triangle meshes can be object-baked.");
                        continue;
                    }

                    if (source.Mesh.GetIndexCount(subMeshIndex) == 0)
                    {
                        continue;
                    }

                    var material = ResolveMaterial(source.Materials, subMeshIndex);
                    var bucket = GetOrCreateBucket(buckets, material);
                    bucket.Combines.Add(new CombineInstance
                    {
                        mesh = source.Mesh,
                        subMeshIndex = subMeshIndex,
                        transform = worldToBakeLocal * source.MeshFilter.transform.localToWorldMatrix
                    });
                }
            }

            materials = buckets.Select(bucket => bucket.Material).ToList();
            if (buckets.Count == 0)
            {
                return null;
            }

            var materialMeshes = new List<Mesh>(buckets.Count);
            try
            {
                foreach (var bucket in buckets)
                {
                    var materialMesh = new Mesh
                    {
                        name = $"{baseName}_{SanitizeName(bucket.Material != null ? bucket.Material.name : "Default")}",
                        indexFormat = IndexFormat.UInt32
                    };
                    materialMesh.CombineMeshes(bucket.Combines.ToArray(), true, true, false);
                    materialMesh.RecalculateBounds();
                    materialMeshes.Add(materialMesh);
                }

                var finalCombines = materialMeshes
                    .Select(mesh => new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    })
                    .ToArray();

                var combined = new Mesh
                {
                    name = $"{baseName}_Mesh",
                    indexFormat = IndexFormat.UInt32
                };
                combined.CombineMeshes(finalCombines, false, false, false);
                combined.RecalculateBounds();
                if (!HasNormals(combined))
                {
                    combined.RecalculateNormals();
                }

                return combined;
            }
            finally
            {
                foreach (var materialMesh in materialMeshes)
                {
                    if (materialMesh != null)
                    {
                        Object.DestroyImmediate(materialMesh);
                    }
                }
            }
        }

        private static bool HasNormals(Mesh mesh)
        {
            return mesh != null && mesh.normals != null && mesh.normals.Length == mesh.vertexCount;
        }

        private static MaterialBucket GetOrCreateBucket(List<MaterialBucket> buckets, Material material)
        {
            material ??= AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            foreach (var bucket in buckets)
            {
                if (bucket.Material == material)
                {
                    return bucket;
                }
            }

            var created = new MaterialBucket(material);
            buckets.Add(created);
            return created;
        }

        private static Material ResolveMaterial(Material[] materials, int subMeshIndex)
        {
            if (materials == null || materials.Length == 0)
            {
                return null;
            }

            return subMeshIndex >= 0 && subMeshIndex < materials.Length
                ? materials[subMeshIndex]
                : materials[materials.Length - 1];
        }

        private static GameObject CreateBakedObject(string baseName, BakePose pose, Mesh mesh, List<Material> materials)
        {
            var bakedObject = new GameObject(baseName);
            bakedObject.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            bakedObject.transform.localScale = Vector3.one;
            var meshFilter = bakedObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var meshRenderer = bakedObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials.ToArray();
            return bakedObject;
        }

        private static string SaveMeshAsset(Mesh mesh, string folder, string baseName)
        {
            var meshPath = $"{folder}/{baseName}_Mesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(meshPath) != null)
            {
                AssetDatabase.DeleteAsset(meshPath);
            }

            AssetDatabase.CreateAsset(mesh, meshPath);
            AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceSynchronousImport);
            return meshPath;
        }

        private static string SavePrefabAsset(GameObject bakedObject, string folder, string baseName)
        {
            var prefabPath = $"{folder}/{baseName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<Object>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            PrefabUtility.SaveAsPrefabAsset(bakedObject, prefabPath);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
            return prefabPath;
        }

        private static string ExportFbx(GameObject bakedObject, string folder, string baseName)
        {
            var fbxPath = $"{folder}/{baseName}.fbx";
            if (AssetDatabase.LoadAssetAtPath<Object>(fbxPath) != null)
            {
                AssetDatabase.DeleteAsset(fbxPath);
            }

            var method = ResolveFbxExportMethod();
            if (method == null)
            {
                return string.Empty;
            }

            try
            {
                method.Invoke(null, new object[] { fbxPath, bakedObject });
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(exception.InnerException?.Message ?? exception.Message, exception);
            }

            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Object>(fbxPath) != null || File.Exists(ToAbsoluteAssetPath(fbxPath))
                ? fbxPath
                : string.Empty;
        }

        private static MethodInfo ResolveFbxExportMethod()
        {
            // FBX export is optional. Reflection is used only to detect the public
            // FBX Exporter package API when the user has already installed it.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var exporterType = assembly.GetType(FbxExporterTypeName);
                if (exporterType == null)
                {
                    continue;
                }

                return exporterType.GetMethod(
                    FbxExporterMethodName,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Object) },
                    null);
            }

            return null;
        }

        private static BakePose ResolveBakePose(
            Transform[] roots,
            List<MeshSource> sources,
            RealPhysicsPlacerObjectBakePivot pivot,
            RealPhysicsPlacerObjectBakeRotation rotation)
        {
            var position = ResolveBakePosition(roots, sources, pivot);
            return new BakePose(position, ResolveBakeRotation(roots, rotation));
        }

        private static Vector3 ResolveBakePosition(Transform[] roots, List<MeshSource> sources, RealPhysicsPlacerObjectBakePivot pivot)
        {
            if (pivot == RealPhysicsPlacerObjectBakePivot.FirstSelected && roots.Length > 0 && roots[0] != null)
            {
                return roots[0].position;
            }

            var bounds = CalculateWorldBounds(sources);
            return bounds.center;
        }

        private static Quaternion ResolveBakeRotation(Transform[] roots, RealPhysicsPlacerObjectBakeRotation rotation)
        {
            return rotation switch
            {
                RealPhysicsPlacerObjectBakeRotation.FirstSelectedDirection => roots.Length > 0 && roots[0] != null
                    ? roots[0].rotation
                    : Quaternion.identity,
                RealPhysicsPlacerObjectBakeRotation.AverageDirection => ResolveAverageRotation(roots),
                _ => Quaternion.identity
            };
        }

        private static Quaternion ResolveAverageRotation(Transform[] roots)
        {
            var averageForward = Vector3.zero;
            var averageUp = Vector3.zero;
            var count = 0;

            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                averageForward += root.forward;
                averageUp += root.up;
                count++;
            }

            if (count == 0 || averageForward.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            averageForward.Normalize();
            if (averageUp.sqrMagnitude <= 0.0001f)
            {
                averageUp = Vector3.up;
            }
            else
            {
                averageUp.Normalize();
            }

            if (Vector3.Cross(averageForward, averageUp).sqrMagnitude <= 0.0001f)
            {
                averageUp = Mathf.Abs(Vector3.Dot(averageForward, Vector3.up)) < 0.98f
                    ? Vector3.up
                    : Vector3.right;
            }

            return Quaternion.LookRotation(averageForward, averageUp);
        }

        private static Bounds CalculateWorldBounds(List<MeshSource> sources)
        {
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var source in sources)
            {
                if (source?.Renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = source.Renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(source.Renderer.bounds);
                }
            }

            return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        private static List<MeshSource> CollectSources(Transform[] roots, bool includeInactive)
        {
            var sources = new List<MeshSource>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive);
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                    if (meshRenderer == null)
                    {
                        continue;
                    }

                    if (!ShouldBakeRendererForObjectLod0(meshRenderer))
                    {
                        continue;
                    }

                    sources.Add(new MeshSource
                    {
                        MeshFilter = meshFilter,
                        Renderer = meshRenderer,
                        Mesh = meshFilter.sharedMesh,
                        Materials = meshRenderer.sharedMaterials
                    });
                }
            }

            return sources;
        }

        private static bool ShouldBakeRendererForObjectLod0(MeshRenderer meshRenderer)
        {
            if (meshRenderer == null)
            {
                return false;
            }

            var lodGroup = meshRenderer.GetComponentInParent<LODGroup>();
            if (lodGroup == null)
            {
                return true;
            }

            var lods = lodGroup.GetLODs();
            if (lods == null || lods.Length == 0)
            {
                return true;
            }

            for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                var renderers = lods[lodIndex].renderers;
                if (renderers == null)
                {
                    continue;
                }

                foreach (var renderer in renderers)
                {
                    if (renderer != meshRenderer)
                    {
                        continue;
                    }

                    return lodIndex == 0;
                }
            }

            return true;
        }

        private static int CountUniqueMaterials(List<MeshSource> sources)
        {
            var buckets = new List<Material>();
            foreach (var source in sources)
            {
                var subMeshCount = Mathf.Max(1, source.Mesh.subMeshCount);
                for (var i = 0; i < subMeshCount; i++)
                {
                    var material = ResolveMaterial(source.Materials, i)
                        ?? AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                    if (!buckets.Any(existing => existing == material))
                    {
                        buckets.Add(material);
                    }
                }
            }

            return buckets.Count;
        }

        private static Transform[] GetSelectionRoots()
        {
            var transforms = Selection.transforms
                .Where(transform => transform != null && transform.gameObject.scene.IsValid())
                .Distinct()
                .ToArray();

            var roots = transforms
                .Where(transform => !transforms.Any(other => other != transform && transform.IsChildOf(other)))
                .ToArray();

            var activeTransform = Selection.activeTransform;
            var activeRoot = activeTransform == null
                ? null
                : roots.FirstOrDefault(root => root == activeTransform || activeTransform.IsChildOf(root));
            return activeRoot == null
                ? roots
                : new[] { activeRoot }.Concat(roots.Where(root => root != activeRoot)).ToArray();
        }

        private static string BuildBakeName(Transform[] roots)
        {
            var rawName = roots.Length == 1
                ? roots[0].name
                : $"{roots[0].name}_plus_{roots.Length - 1}";
            return $"{SanitizeName(rawName)}_RealTransforms_BakedObject";
        }

        private static string BuildOutputFolder(string assetFolder, Transform[] roots, string baseName)
        {
            var sceneName = roots.Length > 0 && roots[0].gameObject.scene.IsValid()
                ? SanitizeName(roots[0].gameObject.scene.name)
                : "Scene";
            return $"{assetFolder}/{sceneName}_{baseName}";
        }

        private static string BuildResultMessage(string prefabPath, string meshPath, string fbxPath, bool fbxRequested, int meshCount, int materialCount)
        {
            var fbxText = !fbxRequested
                ? "FBX export disabled."
                : string.IsNullOrEmpty(fbxPath)
                    ? "FBX export skipped because Unity's FBX Exporter package is not installed."
                    : $"FBX: {fbxPath}";
            return $"RealTransforms Object Baker combined {meshCount} meshes into {materialCount} material slot(s). Prefab: {prefabPath}. Mesh: {meshPath}. {fbxText}";
        }

        private static void PingResult(string fbxPath, string prefabPath, string meshPath)
        {
            var path = !string.IsNullOrEmpty(fbxPath)
                ? fbxPath
                : !string.IsNullOrEmpty(prefabPath)
                    ? prefabPath
                    : meshPath;
            var result = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (result != null)
            {
                EditorGUIUtility.PingObject(result);
            }
        }

        private static void EnsureFolder(string folder)
        {
            RealPhysicsPlacerAssetPaths.EnsureFolder(folder);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Object";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
            return new string(chars).Trim();
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
