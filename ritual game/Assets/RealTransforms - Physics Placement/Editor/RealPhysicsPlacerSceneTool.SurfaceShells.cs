using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Surface shells are optional temporary contact helpers for concave meshes.
        // They are cached and budgeted because they trade setup cost for tighter
        // editor-only contact around detailed selection geometry.
        private const int MaxSurfaceShellCandidateTriangles = 256000;
        private const int MaxSurfaceShellSourceTriangles = 10;
        private const int MaxSurfaceShellSourceVertices = 32;
        private const float SurfaceShellStartAngle = 18f;
        private const float SurfaceShellMaxAngle = 46f;
        private const float SurfaceShellAngleStep = 7f;
        private const float SurfaceShellMinimumNormalCoherence = 0.92f;
        private const float SurfaceShellMaximumSpanFraction = 0.045f;
        private const int SurfaceShellCacheLimit = 48;

        private static readonly Dictionary<SurfaceShellCacheKey, SurfaceShellCacheEntry> SurfaceShellCache = new Dictionary<SurfaceShellCacheKey, SurfaceShellCacheEntry>();
        private static readonly Queue<SurfaceShellPrewarmRequest> SurfaceShellPrewarmQueue = new Queue<SurfaceShellPrewarmRequest>();
        private static readonly HashSet<int> QueuedSurfaceShellPrewarmKeys = new HashSet<int>();
        private static double lastSurfaceShellPrewarmTime;

        private static void QueueSurfaceShellPrewarm(Transform[] roots)
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            if (roots == null || roots.Length == 0)
            {
                return;
            }

            var activeSettings = GetSettings();
            if (!activeSettings.useSurfaceShells)
            {
                return;
            }

            var budget = Mathf.Max(8, activeSettings.surfaceShellBudget);
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (meshFilter == null || meshFilter.sharedMesh == null || IsSurfaceShellBuiltInPrimitiveMesh(meshFilter.sharedMesh))
                    {
                        continue;
                    }

                    var localShell = SurfaceShellWorldDistanceToLocalDistance(meshFilter.transform, activeSettings.surfaceShellSkin);
                    if (TryGetCachedSurfaceShellMeshes(meshFilter.sharedMesh, budget, localShell, out _))
                    {
                        continue;
                    }

                    var key = CalculateSurfaceShellPrewarmKey(meshFilter.sharedMesh, budget, localShell);
                    if (!QueuedSurfaceShellPrewarmKeys.Add(key))
                    {
                        continue;
                    }

                    SurfaceShellPrewarmQueue.Enqueue(new SurfaceShellPrewarmRequest(meshFilter.sharedMesh, budget, localShell, meshFilter.sharedMesh.name, key));
                }
            }
        }

        private static void ProcessSurfaceShellPrewarm()
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            if (SurfaceShellPrewarmQueue.Count == 0 || leftMouseButtonDown)
            {
                return;
            }

            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - lastSurfaceShellPrewarmTime < 0.08d)
            {
                return;
            }

            lastSurfaceShellPrewarmTime = now;
            var request = SurfaceShellPrewarmQueue.Dequeue();
            QueuedSurfaceShellPrewarmKeys.Remove(request.Key);
            if (request.Mesh == null)
            {
                return;
            }

            try
            {
                GetSurfaceShellMeshes(request.Mesh, request.Budget, request.ShellThickness, request.SourceName, out _);
            }
            catch (UnityException)
            {
            }
        }

        private static int CalculateSurfaceShellPrewarmKey(Mesh mesh, int budget, float shellThickness)
        {
            unchecked
            {
                var hash = mesh != null ? mesh.GetEntityId().GetHashCode() : 0;
                hash = (hash * 397) ^ budget;
                hash = (hash * 397) ^ Mathf.RoundToInt(shellThickness * 100000f);
                return hash;
            }
        }

        private static float SurfaceShellWorldDistanceToLocalDistance(Transform transform, float worldDistance)
        {
            if (transform == null)
            {
                return worldDistance;
            }

            var scale = transform.lossyScale;
            var averageScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
            return worldDistance / Mathf.Max(0.0001f, averageScale);
        }

        private static bool IsSurfaceShellBuiltInPrimitiveMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            var meshName = mesh.name;
            return string.Equals(meshName, "Cube", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Sphere", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Capsule", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Cylinder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Plane", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Quad", StringComparison.OrdinalIgnoreCase);
        }

        internal static List<Mesh> GenerateConcaveCompoundColliderMeshes(
            Mesh sourceMesh,
            int hullDetail,
            int maxHulls,
            float hullThickness,
            string sourceName)
        {
            if (sourceMesh == null)
            {
                return new List<Mesh>();
            }

            var hardMaxHulls = Mathf.Clamp(maxHulls, 1, 256);
            var meshes = BuildSurfaceShellMeshes(
                sourceMesh,
                Mathf.Clamp(Mathf.Min(hullDetail, hardMaxHulls), 1, 256),
                Mathf.Max(0.0005f, hullThickness),
                sourceName,
                hardMaxHulls);
            BakeSurfaceShellMeshes(meshes);
            foreach (var mesh in meshes)
            {
                if (mesh == null)
                {
                    continue;
                }

                mesh.hideFlags = HideFlags.None;
            }

            return meshes;
        }

        private static IReadOnlyList<Mesh> GetSurfaceShellMeshes(
            Mesh sourceMesh,
            int shellBudget,
            float shellThickness,
            string sourceName,
            out bool cacheOwned)
        {
            cacheOwned = false;
            if (sourceMesh == null)
            {
                return Array.Empty<Mesh>();
            }

            shellBudget = Mathf.Max(8, shellBudget);
            var useCache = (sourceMesh.hideFlags & HideFlags.HideAndDontSave) == 0;
            var key = new SurfaceShellCacheKey(sourceMesh, shellBudget, shellThickness);
            if (useCache
                && SurfaceShellCache.TryGetValue(key, out var cachedEntry)
                && cachedEntry.IsValid)
            {
                cacheOwned = true;
                return cachedEntry.Meshes;
            }

            var meshes = BuildSurfaceShellMeshes(sourceMesh, shellBudget, shellThickness, sourceName);
            if (!useCache || meshes.Count == 0)
            {
                return meshes;
            }

            BakeSurfaceShellMeshes(meshes);
            SurfaceShellCache[key] = new SurfaceShellCacheEntry(meshes);
            TrimSurfaceShellCache();
            cacheOwned = true;
            return meshes;
        }

        private static bool TryGetCachedSurfaceShellMeshes(
            Mesh sourceMesh,
            int shellBudget,
            float shellThickness,
            out IReadOnlyList<Mesh> meshes)
        {
            meshes = Array.Empty<Mesh>();
            if (sourceMesh == null || (sourceMesh.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                return false;
            }

            var key = new SurfaceShellCacheKey(sourceMesh, Mathf.Max(8, shellBudget), shellThickness);
            if (!SurfaceShellCache.TryGetValue(key, out var cachedEntry) || !cachedEntry.IsValid)
            {
                return false;
            }

            meshes = cachedEntry.Meshes;
            return true;
        }

        private static List<Mesh> BuildSurfaceShellMeshes(Mesh sourceMesh, int shellBudget, float shellThickness, string sourceName, int hardMaxHulls = 0)
        {
            var partitions = BuildSurfaceShellPartitions(sourceMesh, shellBudget, hardMaxHulls);
            if (partitions.Count == 0)
            {
                return new List<Mesh>();
            }

            var meshes = new List<Mesh>(partitions.Count);
            for (var i = 0; i < partitions.Count; i++)
            {
                var piece = CreateSurfaceShellMesh(sourceMesh, partitions[i], $"{sourceName}_RealTransforms_SurfaceShell_{i + 1:000}", shellThickness);
                if (piece != null)
                {
                    meshes.Add(piece);
                }
            }

            return meshes;
        }

        private static void BakeSurfaceShellMeshes(IReadOnlyList<Mesh> meshes)
        {
            var cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                | MeshColliderCookingOptions.EnableMeshCleaning
                | MeshColliderCookingOptions.WeldColocatedVertices
                | MeshColliderCookingOptions.UseFastMidphase;
            foreach (var mesh in meshes)
            {
                if (mesh == null)
                {
                    continue;
                }

                try
                {
                    Physics.BakeMesh(mesh.GetEntityId(), true, cookingOptions);
                }
                catch (UnityException)
                {
                    return;
                }
            }
        }

        private static void TrimSurfaceShellCache()
        {
            if (SurfaceShellCache.Count <= SurfaceShellCacheLimit)
            {
                return;
            }

            var overflow = SurfaceShellCache.Count - SurfaceShellCacheLimit;
            foreach (var key in SurfaceShellCache.Keys.Take(overflow).ToArray())
            {
                if (SurfaceShellCache.TryGetValue(key, out var entry))
                {
                    entry.DestroyMeshes();
                }

                SurfaceShellCache.Remove(key);
            }
        }

        private static List<List<int>> BuildSurfaceShellPartitions(Mesh sourceMesh, int shellBudget, int hardMaxHulls = 0)
        {
            var sourceTriangles = sourceMesh.triangles;
            var sourceVertices = sourceMesh.vertices;
            if (sourceTriangles == null || sourceTriangles.Length < 3 || sourceVertices == null || sourceVertices.Length < 3)
            {
                return new List<List<int>>();
            }

            var facets = BuildSurfaceShellFacets(sourceMesh, Mathf.Max(1, shellBudget));
            if (facets.Count == 0)
            {
                return new List<List<int>>();
            }

            var adjacency = BuildSurfaceShellAdjacency(sourceTriangles, facets);
            List<List<int>> bestPatches = null;
            var maxLocalPatchCount = Mathf.Max(shellBudget, shellBudget * 2);
            for (var angle = SurfaceShellStartAngle; angle <= SurfaceShellMaxAngle + Epsilon; angle += SurfaceShellAngleStep)
            {
                var patches = BuildNormalConnectedSurfacePatches(facets, adjacency, angle);
                if (bestPatches == null)
                {
                    bestPatches = patches;
                }

                if (patches.Count <= maxLocalPatchCount)
                {
                    bestPatches = patches;
                    break;
                }
            }

            if (bestPatches == null || bestPatches.Count == 0)
            {
                return new List<List<int>>();
            }

            var partitions = SplitSurfaceShellPatchesToLimits(sourceMesh, bestPatches, Mathf.Max(1, shellBudget));
            var validPartitions = partitions
                .Where(partition => partition != null && partition.Count > 0)
                .ToList();
            if (hardMaxHulls > 0 && validPartitions.Count > hardMaxHulls)
            {
                validPartitions = MergeSurfaceShellPartitionsToLimit(sourceMesh, validPartitions, hardMaxHulls);
            }

            return validPartitions;
        }

        private static List<SurfaceShellFacet> BuildSurfaceShellFacets(Mesh sourceMesh, int shellBudget)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var triangleCount = sourceTriangles.Length / 3;
            var maxCandidates = Mathf.Clamp(shellBudget * 256, 2048, MaxSurfaceShellCandidateTriangles);
            var stride = Mathf.Max(1, Mathf.CeilToInt(triangleCount / (float)maxCandidates));
            var facets = new List<SurfaceShellFacet>(Mathf.Min(triangleCount, maxCandidates));

            for (var triangle = 0; triangle < sourceTriangles.Length; triangle += 3 * stride)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                var area = normal.magnitude * 0.5f;
                if (area <= Epsilon)
                {
                    continue;
                }

                facets.Add(new SurfaceShellFacet(
                    triangle,
                    (a + b + c) / 3f,
                    normal / Mathf.Max(Epsilon, normal.magnitude),
                    area));
            }

            return facets;
        }

        private static List<int>[] BuildSurfaceShellAdjacency(int[] sourceTriangles, List<SurfaceShellFacet> facets)
        {
            var adjacency = new List<int>[facets.Count];
            for (var i = 0; i < adjacency.Length; i++)
            {
                adjacency[i] = new List<int>(4);
            }

            var edgeMap = new Dictionary<EdgeKey, List<int>>();
            for (var facetIndex = 0; facetIndex < facets.Count; facetIndex++)
            {
                var triangle = facets[facetIndex].TriangleStart;
                if (triangle < 0 || triangle + 2 >= sourceTriangles.Length)
                {
                    continue;
                }

                AddSurfaceShellEdge(edgeMap, sourceTriangles[triangle], sourceTriangles[triangle + 1], facetIndex);
                AddSurfaceShellEdge(edgeMap, sourceTriangles[triangle + 1], sourceTriangles[triangle + 2], facetIndex);
                AddSurfaceShellEdge(edgeMap, sourceTriangles[triangle + 2], sourceTriangles[triangle], facetIndex);
            }

            foreach (var pair in edgeMap)
            {
                var linkedFacets = pair.Value;
                for (var i = 0; i < linkedFacets.Count; i++)
                {
                    for (var j = i + 1; j < linkedFacets.Count; j++)
                    {
                        var left = linkedFacets[i];
                        var right = linkedFacets[j];
                        if (!adjacency[left].Contains(right))
                        {
                            adjacency[left].Add(right);
                        }

                        if (!adjacency[right].Contains(left))
                        {
                            adjacency[right].Add(left);
                        }
                    }
                }
            }

            return adjacency;
        }

        private static void AddSurfaceShellEdge(Dictionary<EdgeKey, List<int>> edgeMap, int a, int b, int facetIndex)
        {
            var key = new EdgeKey(a, b);
            if (!edgeMap.TryGetValue(key, out var facets))
            {
                facets = new List<int>(2);
                edgeMap[key] = facets;
            }

            facets.Add(facetIndex);
        }

        private static List<List<int>> BuildNormalConnectedSurfacePatches(
            List<SurfaceShellFacet> facets,
            List<int>[] adjacency,
            float maxAngleDegrees)
        {
            var patches = new List<List<int>>();
            var visited = new bool[facets.Count];
            var neighborCos = Mathf.Cos(maxAngleDegrees * Mathf.Deg2Rad);
            var patchCos = Mathf.Cos(Mathf.Min(88f, maxAngleDegrees * 1.45f) * Mathf.Deg2Rad);
            var queue = new Queue<int>();

            for (var seed = 0; seed < facets.Count; seed++)
            {
                if (visited[seed])
                {
                    continue;
                }

                var patch = new List<int>();
                var normalSum = facets[seed].Normal * Mathf.Max(Epsilon, facets[seed].Area);
                visited[seed] = true;
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    patch.Add(facets[current].TriangleStart);
                    var patchNormal = normalSum.sqrMagnitude > Epsilon
                        ? normalSum.normalized
                        : facets[current].Normal;

                    foreach (var neighbor in adjacency[current])
                    {
                        if (neighbor < 0 || neighbor >= facets.Count || visited[neighbor])
                        {
                            continue;
                        }

                        var neighborNormal = facets[neighbor].Normal;
                        if (Vector3.Dot(facets[current].Normal, neighborNormal) < neighborCos
                            || Vector3.Dot(patchNormal, neighborNormal) < patchCos)
                        {
                            continue;
                        }

                        visited[neighbor] = true;
                        normalSum += neighborNormal * Mathf.Max(Epsilon, facets[neighbor].Area);
                        queue.Enqueue(neighbor);
                    }
                }

                if (patch.Count > 0)
                {
                    patches.Add(patch);
                }
            }

            return patches;
        }

        private static List<List<int>> SplitSurfaceShellPatchesToLimits(Mesh sourceMesh, List<List<int>> patches, int shellBudget)
        {
            var results = new List<List<int>>();
            var softLimit = Mathf.Clamp(shellBudget * 2, shellBudget, 2048);
            foreach (var patch in patches.OrderByDescending(patch => SurfaceShellPatchScore(sourceMesh, patch)))
            {
                SplitSurfaceShellPatchToLimits(sourceMesh, patch, results, softLimit);
            }

            return results;
        }

        private static List<List<int>> MergeSurfaceShellPartitionsToLimit(Mesh sourceMesh, List<List<int>> partitions, int maxPartitions)
        {
            maxPartitions = Mathf.Max(1, maxPartitions);
            if (partitions == null || partitions.Count <= maxPartitions)
            {
                return partitions ?? new List<List<int>>();
            }

            var ordered = partitions
                .Where(partition => partition != null && partition.Count > 0)
                .OrderByDescending(partition => SurfaceShellPatchScore(sourceMesh, partition))
                .ToList();
            var merged = ordered
                .Take(maxPartitions)
                .Select(partition => new List<int>(partition))
                .ToList();

            if (merged.Count == 0)
            {
                merged.Add(new List<int>());
            }

            for (var i = maxPartitions; i < ordered.Count; i++)
            {
                var targetIndex = FindNearestSurfaceShellPartition(sourceMesh, ordered[i], merged);
                merged[targetIndex].AddRange(ordered[i]);
            }

            return merged;
        }

        private static int FindNearestSurfaceShellPartition(Mesh sourceMesh, List<int> patch, List<List<int>> candidates)
        {
            var patchCenter = SurfaceShellPartitionCenter(sourceMesh, patch);
            var patchNormal = SurfaceShellPartitionNormal(sourceMesh, patch);
            var bestIndex = 0;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateCenter = SurfaceShellPartitionCenter(sourceMesh, candidates[i]);
                var candidateNormal = SurfaceShellPartitionNormal(sourceMesh, candidates[i]);
                var normalPenalty = 1f - Mathf.Abs(Vector3.Dot(patchNormal, candidateNormal));
                var score = (patchCenter - candidateCenter).sqrMagnitude + normalPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static void SplitSurfaceShellPatchToLimits(
            Mesh sourceMesh,
            List<int> patch,
            List<List<int>> results,
            int softLimit)
        {
            if (patch == null || patch.Count == 0)
            {
                return;
            }

            var stack = new Stack<List<int>>();
            stack.Push(patch);

            while (stack.Count > 0)
            {
                if (results.Count >= softLimit)
                {
                    return;
                }

                var current = stack.Pop();
                var forceFit = results.Count + stack.Count >= softLimit;
                if (SurfaceShellPatchFits(sourceMesh, current, forceFit)
                    || !TrySplitSurfaceShellPatch(sourceMesh, current, out var left, out var right))
                {
                    results.Add(current);
                    continue;
                }

                stack.Push(right);
                stack.Push(left);
            }
        }

        private static bool SurfaceShellPatchFits(Mesh sourceMesh, List<int> triangleStarts, bool forceBudgetFit)
        {
            if (triangleStarts.Count > MaxSurfaceShellSourceTriangles
                || CountUniqueTriangleVertices(sourceMesh, triangleStarts) > MaxSurfaceShellSourceVertices)
            {
                return false;
            }

            if (forceBudgetFit)
            {
                return true;
            }

            return SurfaceShellNormalCoherence(sourceMesh, triangleStarts) >= SurfaceShellMinimumNormalCoherence
                && SurfaceShellSpanFits(sourceMesh, triangleStarts);
        }

        private static float SurfaceShellPatchScore(Mesh sourceMesh, List<int> triangleStarts)
        {
            if (sourceMesh == null || triangleStarts == null || triangleStarts.Count == 0)
            {
                return 0f;
            }

            var bounds = CalculateTrianglePartitionBounds(sourceMesh, triangleStarts);
            return triangleStarts.Count + bounds.size.sqrMagnitude;
        }

        private static Vector3 SurfaceShellPartitionCenter(Mesh sourceMesh, List<int> triangleStarts)
        {
            if (sourceMesh == null || triangleStarts == null || triangleStarts.Count == 0)
            {
                return Vector3.zero;
            }

            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var center = Vector3.zero;
            var count = 0;
            foreach (var triangle in triangleStarts)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                center += (a + b + c) / 3f;
                count++;
            }

            return count == 0 ? Vector3.zero : center / count;
        }

        private static Vector3 SurfaceShellPartitionNormal(Mesh sourceMesh, List<int> triangleStarts)
        {
            if (sourceMesh == null || triangleStarts == null || triangleStarts.Count == 0)
            {
                return Vector3.up;
            }

            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var normalSum = Vector3.zero;
            foreach (var triangle in triangleStarts)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude > Epsilon)
                {
                    normalSum += normal.normalized;
                }
            }

            return normalSum.sqrMagnitude <= Epsilon ? Vector3.up : normalSum.normalized;
        }

        private static bool SurfaceShellSpanFits(Mesh sourceMesh, List<int> triangleStarts)
        {
            var bounds = CalculateTrianglePartitionBounds(sourceMesh, triangleStarts);
            var meshSpan = Mathf.Max(0.0001f, sourceMesh.bounds.size.magnitude);
            return bounds.size.magnitude <= meshSpan * SurfaceShellMaximumSpanFraction;
        }

        private static float SurfaceShellNormalCoherence(Mesh sourceMesh, List<int> triangleStarts)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var normalSum = Vector3.zero;
            var areaSum = 0f;
            foreach (var triangle in triangleStarts)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                var area = normal.magnitude;
                if (area <= Epsilon)
                {
                    continue;
                }

                normalSum += normal.normalized * area;
                areaSum += area;
            }

            return areaSum <= Epsilon ? 1f : Mathf.Clamp01(normalSum.magnitude / areaSum);
        }

        private static bool TrySplitSurfaceShellPatch(Mesh sourceMesh, List<int> triangleStarts, out List<int> left, out List<int> right)
        {
            left = null;
            right = null;
            if (sourceMesh == null || triangleStarts == null || triangleStarts.Count < 2)
            {
                return false;
            }

            var metric = ChooseSurfaceShellSplitMetric(sourceMesh, triangleStarts);
            var sorted = triangleStarts
                .OrderBy(triangle => SurfaceShellSplitValue(sourceMesh, triangle, metric))
                .ThenBy(triangle => triangle)
                .ToList();
            var splitIndex = sorted.Count / 2;
            if (splitIndex <= 0 || splitIndex >= sorted.Count)
            {
                return false;
            }

            left = sorted.GetRange(0, splitIndex);
            right = sorted.GetRange(splitIndex, sorted.Count - splitIndex);
            return left.Count > 0 && right.Count > 0;
        }

        private static int ChooseSurfaceShellSplitMetric(Mesh sourceMesh, List<int> triangleStarts)
        {
            if (SurfaceShellNormalCoherence(sourceMesh, triangleStarts) < SurfaceShellMinimumNormalCoherence)
            {
                return LongestNormalBoundsAxis(sourceMesh, triangleStarts) + 3;
            }

            return LongestCenterBoundsAxis(sourceMesh, triangleStarts);
        }

        private static float SurfaceShellSplitValue(Mesh sourceMesh, int triangle, int metric)
        {
            return metric < 3
                ? TriangleCenterOnAxis(sourceMesh, triangle, metric)
                : TriangleNormalOnAxis(sourceMesh, triangle, metric - 3);
        }

        private static int LongestCenterBoundsAxis(Mesh sourceMesh, List<int> triangleStarts)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var bounds = new Bounds();
            var initialized = false;

            foreach (var triangle in triangleStarts)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                var center = (a + b + c) / 3f;
                if (!initialized)
                {
                    bounds = new Bounds(center, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(center);
                }
            }

            if (!initialized)
            {
                return 0;
            }

            var size = bounds.size;
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static int LongestNormalBoundsAxis(Mesh sourceMesh, List<int> triangleStarts)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var bounds = new Bounds();
            var initialized = false;

            foreach (var triangle in triangleStarts)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude <= Epsilon)
                {
                    continue;
                }

                normal.Normalize();
                if (!initialized)
                {
                    bounds = new Bounds(normal, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(normal);
                }
            }

            if (!initialized)
            {
                return 0;
            }

            var size = bounds.size;
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static Bounds CalculateTrianglePartitionBounds(Mesh sourceMesh, List<int> triangleStarts)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var bounds = new Bounds();
            var initialized = false;

            foreach (var triangle in triangleStarts)
            {
                for (var corner = 0; corner < 3; corner++)
                {
                    var vertexIndex = sourceTriangles[triangle + corner];
                    if (vertexIndex < 0 || vertexIndex >= sourceVertices.Length)
                    {
                        continue;
                    }

                    var vertex = sourceVertices[vertexIndex];
                    if (!initialized)
                    {
                        bounds = new Bounds(vertex, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(vertex);
                    }
                }
            }

            return bounds;
        }

        private static int CountUniqueTriangleVertices(Mesh sourceMesh, List<int> triangleStarts)
        {
            var sourceTriangles = sourceMesh.triangles;
            var indices = new HashSet<int>();
            foreach (var triangle in triangleStarts)
            {
                if (triangle < 0 || triangle + 2 >= sourceTriangles.Length)
                {
                    continue;
                }

                indices.Add(sourceTriangles[triangle]);
                indices.Add(sourceTriangles[triangle + 1]);
                indices.Add(sourceTriangles[triangle + 2]);
            }

            return indices.Count;
        }

        private static float TriangleCenterOnAxis(Mesh sourceMesh, int triangle, int axis)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
            {
                return 0f;
            }

            return (a[axis] + b[axis] + c[axis]) / 3f;
        }

        private static float TriangleNormalOnAxis(Mesh sourceMesh, int triangle, int axis)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
            {
                return 0f;
            }

            var normal = Vector3.Cross(b - a, c - a);
            return normal.sqrMagnitude > Epsilon ? normal.normalized[axis] : 0f;
        }

        private static Mesh CreateSurfaceShellMesh(Mesh sourceMesh, List<int> triangleStarts, string meshName, float shellThickness)
        {
            if (sourceMesh == null || triangleStarts == null || triangleStarts.Count == 0)
            {
                return null;
            }

            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var selectedTriangles = SelectSurfaceShellTriangles(sourceMesh, triangleStarts);
            if (selectedTriangles.Count == 0)
            {
                return null;
            }

            var sourceToLocal = new Dictionary<int, int>();
            var localToSource = new List<int>();
            var localTriangles = new List<int>(selectedTriangles.Count * 3);
            var normalSums = new Dictionary<int, Vector3>();
            var patchNormal = Vector3.zero;

            foreach (var triangle in selectedTriangles)
            {
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude <= Epsilon)
                {
                    continue;
                }

                patchNormal += normal;
                for (var corner = 0; corner < 3; corner++)
                {
                    var sourceIndex = sourceTriangles[triangle + corner];
                    if (!sourceToLocal.TryGetValue(sourceIndex, out var localIndex))
                    {
                        localIndex = localToSource.Count;
                        sourceToLocal[sourceIndex] = localIndex;
                        localToSource.Add(sourceIndex);
                    }

                    localTriangles.Add(localIndex);
                    normalSums.TryGetValue(sourceIndex, out var currentNormal);
                    normalSums[sourceIndex] = currentNormal + normal;
                }
            }

            if (localToSource.Count < 3 || localTriangles.Count < 3)
            {
                return null;
            }

            var fallbackNormal = patchNormal.sqrMagnitude > Epsilon
                ? patchNormal.normalized
                : Vector3.up;
            var halfShell = Mathf.Max(0.0005f, shellThickness * 0.5f);
            var vertices = new List<Vector3>(localToSource.Count * 2);
            for (var i = 0; i < localToSource.Count; i++)
            {
                var sourceIndex = localToSource[i];
                var direction = normalSums.TryGetValue(sourceIndex, out var normalSum) && normalSum.sqrMagnitude > Epsilon
                    ? normalSum.normalized
                    : fallbackNormal;
                vertices.Add(sourceVertices[sourceIndex] + direction * halfShell);
            }

            for (var i = 0; i < localToSource.Count; i++)
            {
                var sourceIndex = localToSource[i];
                var direction = normalSums.TryGetValue(sourceIndex, out var normalSum) && normalSum.sqrMagnitude > Epsilon
                    ? normalSum.normalized
                    : fallbackNormal;
                vertices.Add(sourceVertices[sourceIndex] - direction * halfShell);
            }

            var triangles = new List<int>(localTriangles.Count * 2);
            var edges = new Dictionary<EdgeKey, SurfaceShellEdgeUse>();
            var backOffset = localToSource.Count;
            for (var i = 0; i < localTriangles.Count; i += 3)
            {
                var a = localTriangles[i];
                var b = localTriangles[i + 1];
                var c = localTriangles[i + 2];
                AddSurfaceShellBoundaryEdge(edges, a, b);
                AddSurfaceShellBoundaryEdge(edges, b, c);
                AddSurfaceShellBoundaryEdge(edges, c, a);
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(c + backOffset);
                triangles.Add(b + backOffset);
                triangles.Add(a + backOffset);
            }

            foreach (var pair in edges)
            {
                var edge = pair.Value;
                if (edge.Count != 1)
                {
                    continue;
                }

                var frontA = edge.Start;
                var frontB = edge.End;
                var backA = frontA + backOffset;
                var backB = frontB + backOffset;
                triangles.Add(frontA);
                triangles.Add(frontB);
                triangles.Add(backB);
                triangles.Add(frontA);
                triangles.Add(backB);
                triangles.Add(backA);
            }

            if (vertices.Count < 4 || triangles.Count < 6)
            {
                return null;
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> SelectSurfaceShellTriangles(Mesh sourceMesh, List<int> triangleStarts)
        {
            var sourceVertices = sourceMesh.vertices;
            var sourceTriangles = sourceMesh.triangles;
            var selected = new List<int>(Mathf.Min(MaxSurfaceShellSourceTriangles, triangleStarts.Count));
            var usedVertices = new HashSet<int>();
            var stride = Mathf.Max(1, Mathf.CeilToInt(triangleStarts.Count / (float)MaxSurfaceShellSourceTriangles));

            for (var i = 0; i < triangleStarts.Count && selected.Count < MaxSurfaceShellSourceTriangles; i += stride)
            {
                var triangle = triangleStarts[i];
                if (!TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c))
                {
                    continue;
                }

                if (Vector3.Cross(b - a, c - a).sqrMagnitude <= Epsilon)
                {
                    continue;
                }

                var newVertices = 0;
                for (var corner = 0; corner < 3; corner++)
                {
                    if (!usedVertices.Contains(sourceTriangles[triangle + corner]))
                    {
                        newVertices++;
                    }
                }

                if (usedVertices.Count + newVertices > MaxSurfaceShellSourceVertices && selected.Count > 0)
                {
                    continue;
                }

                selected.Add(triangle);
                usedVertices.Add(sourceTriangles[triangle]);
                usedVertices.Add(sourceTriangles[triangle + 1]);
                usedVertices.Add(sourceTriangles[triangle + 2]);
            }

            if (selected.Count > 0)
            {
                return selected;
            }

            for (var i = 0; i < triangleStarts.Count; i++)
            {
                var triangle = triangleStarts[i];
                if (TryGetTriangle(sourceVertices, sourceTriangles, triangle, out var a, out var b, out var c)
                    && Vector3.Cross(b - a, c - a).sqrMagnitude > Epsilon)
                {
                    selected.Add(triangle);
                    break;
                }
            }

            return selected;
        }

        private static void AddSurfaceShellBoundaryEdge(Dictionary<EdgeKey, SurfaceShellEdgeUse> edges, int start, int end)
        {
            var key = new EdgeKey(start, end);
            if (edges.TryGetValue(key, out var existing))
            {
                edges[key] = new SurfaceShellEdgeUse(existing.Start, existing.End, existing.Count + 1);
                return;
            }

            edges[key] = new SurfaceShellEdgeUse(start, end, 1);
        }

        private static bool TryGetTriangle(Vector3[] sourceVertices, int[] sourceTriangles, int triangle, out Vector3 a, out Vector3 b, out Vector3 c)
        {
            a = Vector3.zero;
            b = Vector3.zero;
            c = Vector3.zero;
            if (triangle < 0 || triangle + 2 >= sourceTriangles.Length)
            {
                return false;
            }

            var ia = sourceTriangles[triangle];
            var ib = sourceTriangles[triangle + 1];
            var ic = sourceTriangles[triangle + 2];
            if (ia < 0 || ib < 0 || ic < 0 || ia >= sourceVertices.Length || ib >= sourceVertices.Length || ic >= sourceVertices.Length)
            {
                return false;
            }

            a = sourceVertices[ia];
            b = sourceVertices[ib];
            c = sourceVertices[ic];
            return true;
        }

        private readonly struct SurfaceShellFacet
        {
            internal readonly int TriangleStart;
            internal readonly Vector3 Center;
            internal readonly Vector3 Normal;
            internal readonly float Area;

            internal SurfaceShellFacet(int triangleStart, Vector3 center, Vector3 normal, float area)
            {
                TriangleStart = triangleStart;
                Center = center;
                Normal = normal;
                Area = area;
            }
        }

        private readonly struct SurfaceShellCacheKey : IEquatable<SurfaceShellCacheKey>
        {
            private readonly EntityId meshId;
            private readonly int vertexCount;
            private readonly int triangleIndexCount;
            private readonly int shellBudget;
            private readonly int shellThickness;
            private readonly Vector3Int boundsSize;

            internal SurfaceShellCacheKey(Mesh mesh, int shellBudget, float shellThickness)
            {
                meshId = mesh != null ? mesh.GetEntityId() : EntityId.None;
                vertexCount = mesh != null ? mesh.vertexCount : 0;
                triangleIndexCount = mesh != null ? mesh.triangles.Length : 0;
                this.shellBudget = shellBudget;
                this.shellThickness = Mathf.RoundToInt(shellThickness * 100000f);
                var size = mesh != null ? mesh.bounds.size : Vector3.zero;
                boundsSize = new Vector3Int(
                    Mathf.RoundToInt(size.x * 10000f),
                    Mathf.RoundToInt(size.y * 10000f),
                    Mathf.RoundToInt(size.z * 10000f));
            }

            public bool Equals(SurfaceShellCacheKey other)
            {
                return meshId.Equals(other.meshId)
                    && vertexCount == other.vertexCount
                    && triangleIndexCount == other.triangleIndexCount
                    && shellBudget == other.shellBudget
                    && shellThickness == other.shellThickness
                    && boundsSize == other.boundsSize;
            }

            public override bool Equals(object obj)
            {
                return obj is SurfaceShellCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = meshId.GetHashCode();
                    hashCode = (hashCode * 397) ^ vertexCount;
                    hashCode = (hashCode * 397) ^ triangleIndexCount;
                    hashCode = (hashCode * 397) ^ shellBudget;
                    hashCode = (hashCode * 397) ^ shellThickness;
                    hashCode = (hashCode * 397) ^ boundsSize.GetHashCode();
                    return hashCode;
                }
            }
        }

        private sealed class SurfaceShellCacheEntry
        {
            internal readonly List<Mesh> Meshes;

            internal SurfaceShellCacheEntry(List<Mesh> meshes)
            {
                Meshes = meshes ?? new List<Mesh>();
            }

            internal bool IsValid => Meshes.Count > 0 && Meshes.All(mesh => mesh != null);

            internal void DestroyMeshes()
            {
                foreach (var mesh in Meshes)
                {
                    if (mesh != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
            }
        }

        private readonly struct SurfaceShellPrewarmRequest
        {
            internal readonly Mesh Mesh;
            internal readonly int Budget;
            internal readonly float ShellThickness;
            internal readonly string SourceName;
            internal readonly int Key;

            internal SurfaceShellPrewarmRequest(Mesh mesh, int budget, float shellThickness, string sourceName, int key)
            {
                Mesh = mesh;
                Budget = budget;
                ShellThickness = shellThickness;
                SourceName = sourceName;
                Key = key;
            }
        }

        private readonly struct SurfaceShellEdgeUse
        {
            internal readonly int Start;
            internal readonly int End;
            internal readonly int Count;

            internal SurfaceShellEdgeUse(int start, int end, int count)
            {
                Start = start;
                End = end;
                Count = count;
            }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int a;
            private readonly int b;

            internal EdgeKey(int left, int right)
            {
                if (left <= right)
                {
                    a = left;
                    b = right;
                }
                else
                {
                    a = right;
                    b = left;
                }
            }

            public bool Equals(EdgeKey other)
            {
                return a == other.a && b == other.b;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (a * 397) ^ b;
                }
            }
        }
    }
}
