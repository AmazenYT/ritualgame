﻿using System;
using System.Collections.Generic;
using System.Linq;
using RealPhysicsPlacer;
using UnityEditor;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    // Editor-only lookup table for baked hull meshes. It lets live placement reuse
    // a previously baked collider set for the same source mesh instead of creating
    // a fresh temporary convex proxy every drag.
    internal sealed class RealPhysicsPlacerBakedColliderLibrary : ScriptableObject
    {
        private static string GetLibraryPath(RealPhysicsPlacerSettings settings = null)
        {
            return RealPhysicsPlacerAssetPaths.GetBakedColliderLibraryPath(settings ?? RealPhysicsPlacerSettings.GetOrCreate());
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        internal static bool TryGetHullMeshes(Mesh sourceMesh, out IReadOnlyList<Mesh> hullMeshes)
        {
            hullMeshes = Array.Empty<Mesh>();
            if (sourceMesh == null)
            {
                return false;
            }

            var library = LoadLibrary();
            if (library == null)
            {
                return false;
            }

            var sourceHash = RealPhysicsPlacerColliderDecomposer.CalculateSourceMeshHash(sourceMesh);
            var changed = library.PruneInvalidEntries();
            var entry = library.entries.FirstOrDefault(candidate => candidate != null && candidate.sourceMeshHash == sourceHash);
            if (entry == null || entry.hullMeshes == null)
            {
                if (changed)
                {
                    EditorUtility.SetDirty(library);
                }

                return false;
            }

            var meshes = entry.hullMeshes
                .Where(mesh => mesh != null)
                .ToArray();
            if (meshes.Length == 0)
            {
                library.entries.Remove(entry);
                EditorUtility.SetDirty(library);
                return false;
            }

            if (changed)
            {
                EditorUtility.SetDirty(library);
            }

            hullMeshes = meshes;
            return true;
        }

        internal static void RegisterBake(
            Mesh sourceMesh,
            RealPhysicsPlacerSettings settings,
            string assetPath,
            IReadOnlyList<Mesh> hullMeshes)
        {
            if (sourceMesh == null || settings == null || hullMeshes == null || hullMeshes.Count == 0)
            {
                return;
            }

            var meshes = hullMeshes
                .Where(mesh => mesh != null)
                .ToList();
            if (meshes.Count == 0)
            {
                return;
            }

            var library = GetOrCreateLibrary(settings);
            if (library == null)
            {
                return;
            }

            var sourceHash = RealPhysicsPlacerColliderDecomposer.CalculateSourceMeshHash(sourceMesh);
            library.entries.RemoveAll(entry => entry == null || string.Equals(entry.sourceMeshHash, sourceHash, StringComparison.Ordinal));
            library.entries.Add(new Entry
            {
                sourceMeshName = sourceMesh.name,
                sourceMeshHash = sourceHash,
                shape = settings.bakerColliderShape,
                maxHulls = settings.bakerMaxHulls,
                maxHullVertices = settings.bakerMaxHullVertices,
                realHullsQuality = settings.bakerRealHullsQuality,
                realHullsVoxelResolution = settings.bakerRealHullsVoxelResolution,
                realHullsSurfaceSamples = settings.bakerRealHullsSurfaceSamples,
                realHullsMaxSplitCandidates = settings.bakerRealHullsMaxSplitCandidates,
                realHullsShapeAccuracy = settings.bakerRealHullsShapeAccuracy,
                realHullsMergePass = settings.bakerRealHullsMergePass,
                mergeSmallHulls = settings.bakerMergeSmallHulls,
                assetPath = assetPath ?? string.Empty,
                hullMeshes = meshes
            });

            library.PruneInvalidEntries();
            EditorUtility.SetDirty(library);
        }

        internal static void RemoveEntryForSourceMesh(Mesh sourceMesh)
        {
            if (sourceMesh == null)
            {
                return;
            }

            var library = LoadLibrary();
            if (library == null)
            {
                return;
            }

            var sourceHash = RealPhysicsPlacerColliderDecomposer.CalculateSourceMeshHash(sourceMesh);
            var removed = library.entries.RemoveAll(entry => entry == null || string.Equals(entry.sourceMeshHash, sourceHash, StringComparison.Ordinal));
            if (removed > 0)
            {
                EditorUtility.SetDirty(library);
            }
        }

        private static RealPhysicsPlacerBakedColliderLibrary LoadLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<RealPhysicsPlacerBakedColliderLibrary>(GetLibraryPath());
        }

        private static RealPhysicsPlacerBakedColliderLibrary GetOrCreateLibrary(RealPhysicsPlacerSettings settings)
        {
            var libraryPath = GetLibraryPath(settings);
            var library = AssetDatabase.LoadAssetAtPath<RealPhysicsPlacerBakedColliderLibrary>(libraryPath);
            if (library != null)
            {
                return library;
            }

            RealPhysicsPlacerAssetPaths.EnsureFolder(settings.GetColliderBakerOutputFolder());
            library = CreateInstance<RealPhysicsPlacerBakedColliderLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            AssetDatabase.ImportAsset(libraryPath, ImportAssetOptions.ForceSynchronousImport);
            return library;
        }

        private bool PruneInvalidEntries()
        {
            var before = entries.Count;
            entries.RemoveAll(entry => entry == null
                || string.IsNullOrEmpty(entry.sourceMeshHash)
                || entry.hullMeshes == null
                || entry.hullMeshes.Count == 0
                || entry.hullMeshes.All(mesh => mesh == null));
            return entries.Count != before;
        }

        [Serializable]
        private sealed class Entry
        {
            public string sourceMeshName = string.Empty;
            public string sourceMeshHash = string.Empty;
            public RealPhysicsPlacerBakedColliderShape shape = RealPhysicsPlacerBakedColliderShape.RealHulls;
            public int maxHulls;
            public int maxHullVertices;
            public RealPhysicsPlacerRealHullsQuality realHullsQuality;
            public int realHullsVoxelResolution;
            public int realHullsSurfaceSamples;
            public int realHullsMaxSplitCandidates;
            public float realHullsShapeAccuracy;
            public bool realHullsMergePass;
            public bool mergeSmallHulls;
            public string assetPath = string.Empty;
            public List<Mesh> hullMeshes = new List<Mesh>();
        }
    }
}
