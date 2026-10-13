using System;
using System.Collections.Generic;
using System.Linq;
using RealPhysicsPlacer;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    /// <summary>
    /// Clean-room RealTransforms convex decomposition solver.
    /// It is intentionally pure C# and does not wrap external decomposition
    /// libraries or native solver plugins.
    /// </summary>
    internal static class RealPhysicsPlacerRealHullsSolver
    {
        private const float Epsilon = 0.00001f;
        private const int MaxCacheEntries = 40;
        private static readonly Dictionary<string, List<List<Vector3>>> PointCloudCache = new Dictionary<string, List<List<Vector3>>>();
        private static readonly Queue<string> CacheOrder = new Queue<string>();

        internal static List<RealPhysicsPlacerColliderDecomposer.Piece> Decompose(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            RealPhysicsPlacerSettings settings,
            string sourceName)
        {
            if (sourceVertices == null || sourceTriangles == null || settings == null)
            {
                return new List<RealPhysicsPlacerColliderDecomposer.Piece>();
            }

            var options = SolverOptions.FromSettings(settings);
            var cacheKey = BuildCacheKey(sourceVertices, sourceTriangles, options);
            if (settings.bakerRealHullsCacheResults && TryReadCache(cacheKey, sourceName, options, out var cachedPieces))
            {
                return cachedPieces;
            }

            var totalTimer = RealPhysicsPlacerTimingLog.Start();
            var pointClouds = SolvePointClouds(sourceVertices, sourceTriangles, sourceName, options, totalTimer);
            if (settings.bakerRealHullsCacheResults && pointClouds.Count > 0)
            {
                WriteCache(cacheKey, pointClouds);
            }

            var pieces = BuildPiecesFromPointClouds(pointClouds, sourceName, options);
            RealPhysicsPlacerTimingLog.Total($"Real Hulls '{sourceName}' complete ({pieces.Count} hulls)", totalTimer);
            return pieces;
        }

        // Core solve: source mesh arrays in, convex hull point clouds out.
        // Unity MeshCollider objects are created only by the adapter path.
        private static List<List<Vector3>> SolvePointClouds(
            Vector3[] sourceVertices,
            int[] sourceTriangles,
            string sourceName,
            SolverOptions options,
            System.Diagnostics.Stopwatch totalTimer)
        {
            if (!TryPreprocess(sourceVertices, sourceTriangles, out var mesh))
            {
                return new List<List<Vector3>>();
            }

            RealPhysicsPlacerTimingLog.Step($"Real Hulls '{sourceName}' preprocess ({mesh.Vertices.Count} verts, {mesh.Triangles.Count} tris)", totalTimer);
            if (options.MaxHulls <= 1)
            {
                return CreateSingleHullPointCloud(mesh);
            }

            if (TryCreateAlreadyConvexPointCloud(mesh, out var convexPointClouds))
            {
                return convexPointClouds;
            }

            var samples = BuildSurfaceSamples(mesh, options);
            RealPhysicsPlacerTimingLog.Step($"Real Hulls '{sourceName}' sample ({samples.Count})", totalTimer);
            var voxels = BuildVoxelCells(mesh, samples, options);
            RealPhysicsPlacerTimingLog.Step($"Real Hulls '{sourceName}' voxelize ({voxels.Count})", totalTimer);
            var clusters = BuildInitialClusters(mesh, samples, voxels);
            if (clusters.Count == 0)
            {
                return new List<List<Vector3>>();
            }

            SolveClusters(mesh, samples, voxels, clusters, options);
            if (options.MergePass)
            {
                MergeCompatibleClusters(mesh, samples, voxels, clusters, options);
            }

            CompactClustersToBudget(mesh, samples, voxels, clusters, options);
            return BuildFinalPointClouds(mesh, samples, clusters, options);
        }

        private static List<List<Vector3>> CreateSingleHullPointCloud(NormalizedMesh mesh)
        {
            var points = new List<Vector3>(mesh.Vertices.Count);
            foreach (var vertex in mesh.Vertices)
            {
                AddUniquePoint(points, mesh.Restore(vertex), mesh.RestoreEpsilon);
            }

            return points.Count >= 3
                ? new List<List<Vector3>> { points }
                : new List<List<Vector3>>();
        }

        private static bool TryCreateAlreadyConvexPointCloud(
            NormalizedMesh mesh,
            out List<List<Vector3>> pointClouds)
        {
            pointClouds = null;
            if (!IsClosedSingleConvexComponent(mesh))
            {
                return false;
            }

            var points = new List<Vector3>(mesh.Vertices.Count);
            foreach (var vertex in mesh.Vertices)
            {
                AddUniquePoint(points, mesh.Restore(vertex), mesh.RestoreEpsilon);
            }

            pointClouds = new List<List<Vector3>> { points };
            return true;
        }

        private static bool IsClosedSingleConvexComponent(NormalizedMesh mesh)
        {
            if (mesh.Triangles.Count < 4 || mesh.Triangles.Any(triangle => triangle.HasBoundary))
            {
                return false;
            }

            if (BuildConnectedTriangleGroups(mesh).Count != 1)
            {
                return false;
            }

            var tolerance = Mathf.Max(0.00025f, mesh.Vertices.Count > 0 ? CalculateNormalizedBounds(mesh.Vertices).size.magnitude * 0.00075f : 0.00025f);
            foreach (var triangle in mesh.Triangles)
            {
                var planePoint = mesh.Vertices[triangle.A];
                var positive = false;
                var negative = false;
                foreach (var vertex in mesh.Vertices)
                {
                    var distance = Vector3.Dot(vertex - planePoint, triangle.Normal);
                    if (distance > tolerance)
                    {
                        positive = true;
                    }
                    else if (distance < -tolerance)
                    {
                        negative = true;
                    }

                    if (positive && negative)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void SolveClusters(NormalizedMesh mesh, List<SamplePoint> samples, List<VoxelCell> voxels, List<Cluster> clusters, SolverOptions options)
        {
            foreach (var cluster in clusters)
            {
                UpdateCluster(mesh, samples, voxels, cluster, options);
            }

            while (clusters.Count < options.MaxHulls && !RealPhysicsPlacerColliderBakeBudget.IsExpired)
            {
                var splitIndex = FindWorstCluster(clusters, clusters.Count < options.TargetHullCount ? 0f : options.ShapeAccuracy);
                if (splitIndex < 0)
                {
                    break;
                }

                var cluster = clusters[splitIndex];
                if (!TrySplitCluster(mesh, samples, voxels, cluster, options, out var left, out var right))
                {
                    cluster.Terminal = true;
                    continue;
                }

                clusters[splitIndex] = left;
                clusters.Add(right);
            }

            clusters.Sort((left, right) => right.Score.CompareTo(left.Score));
        }

        private static int FindWorstCluster(List<Cluster> clusters, float tolerance)
        {
            var index = -1;
            var score = tolerance;
            for (var i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                if (cluster.Terminal || cluster.Triangles.Count < 2 || cluster.Samples.Count < 6)
                {
                    continue;
                }

                var clusterTolerance = cluster.Closed ? tolerance * 4f : tolerance;
                if (cluster.Score <= clusterTolerance || cluster.Score <= score)
                {
                    continue;
                }

                score = cluster.Score;
                index = i;
            }

            return index;
        }

        private static bool TrySplitCluster(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            Cluster cluster,
            SolverOptions options,
            out Cluster left,
            out Cluster right)
        {
            left = null;
            right = null;
            var directions = BuildSplitDirections(mesh, samples, cluster);
            if (directions.Count == 0)
            {
                return false;
            }

            var bestScore = float.NegativeInfinity;
            Cluster bestLeft = null;
            Cluster bestRight = null;
            var tested = 0;
            foreach (var direction in directions)
            {
                if (tested >= options.MaxSplitCandidates)
                {
                    break;
                }

                var offsets = BuildSplitOffsets(samples, cluster, direction);
                foreach (var offset in offsets)
                {
                    if (tested++ >= options.MaxSplitCandidates)
                    {
                        break;
                    }

                    if (!TryCreateSplit(mesh, samples, voxels, cluster, options, direction, offset, false, out var candidateLeft, out var candidateRight))
                    {
                        continue;
                    }

                    var score = ScoreSplit(cluster, candidateLeft, candidateRight);
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestLeft = candidateLeft;
                    bestRight = candidateRight;
                }
            }

            if (bestLeft == null || bestRight == null || bestScore <= 0.01f)
            {
                return false;
            }

            UpdateCluster(mesh, samples, voxels, bestLeft, options);
            UpdateCluster(mesh, samples, voxels, bestRight, options);
            if (ScoreSplit(cluster, bestLeft, bestRight) <= 0.005f)
            {
                return false;
            }

            left = bestLeft;
            right = bestRight;
            return true;
        }

        private static bool TryCreateSplit(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            Cluster parent,
            SolverOptions options,
            Vector3 direction,
            float offset,
            bool fullScore,
            out Cluster left,
            out Cluster right)
        {
            left = new Cluster();
            right = new Cluster();
            foreach (var triangleIndex in parent.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                if (Vector3.Dot(triangle.Center, direction) <= offset)
                {
                    left.Triangles.Add(triangleIndex);
                }
                else
                {
                    right.Triangles.Add(triangleIndex);
                }
            }

            if (left.Triangles.Count == 0 || right.Triangles.Count == 0)
            {
                return false;
            }

            var leftTriangles = new HashSet<int>(left.Triangles);
            var rightTriangles = new HashSet<int>(right.Triangles);
            foreach (var sampleIndex in parent.Samples)
            {
                var sample = samples[sampleIndex];
                if (leftTriangles.Contains(sample.TriangleIndex))
                {
                    left.Samples.Add(sampleIndex);
                }
                else if (rightTriangles.Contains(sample.TriangleIndex))
                {
                    right.Samples.Add(sampleIndex);
                }
                else if (Vector3.Dot(sample.Point, direction) <= offset)
                {
                    left.Samples.Add(sampleIndex);
                }
                else
                {
                    right.Samples.Add(sampleIndex);
                }
            }

            foreach (var voxelIndex in parent.Voxels)
            {
                var voxel = voxels[voxelIndex];
                if (voxel.TriangleIndex >= 0 && leftTriangles.Contains(voxel.TriangleIndex))
                {
                    left.Voxels.Add(voxelIndex);
                }
                else if (voxel.TriangleIndex >= 0 && rightTriangles.Contains(voxel.TriangleIndex))
                {
                    right.Voxels.Add(voxelIndex);
                }
                else if (Vector3.Dot(voxel.Center, direction) <= offset)
                {
                    left.Voxels.Add(voxelIndex);
                }
                else
                {
                    right.Voxels.Add(voxelIndex);
                }
            }

            if (left.Samples.Count < 4 || right.Samples.Count < 4)
            {
                return false;
            }

            if (fullScore)
            {
                UpdateCluster(mesh, samples, voxels, left, options);
                UpdateCluster(mesh, samples, voxels, right, options);
            }
            else
            {
                UpdateClusterCheap(mesh, samples, voxels, left, options);
                UpdateClusterCheap(mesh, samples, voxels, right, options);
            }

            return true;
        }

        private static float ScoreSplit(Cluster parent, Cluster left, Cluster right)
        {
            var worstChild = Mathf.Max(left.Score, right.Score);
            var averageChild = (left.Score + right.Score) * 0.5f;
            var balance = Mathf.Min(left.Samples.Count, right.Samples.Count) / (float)Mathf.Max(left.Samples.Count, right.Samples.Count);
            var parentVolume = BoundsVolume(parent.Bounds);
            var childVolume = BoundsVolume(left.Bounds) + BoundsVolume(right.Bounds);
            var volumeReduction = Mathf.Clamp01((parentVolume - childVolume) / Mathf.Max(Epsilon, parentVolume));
            return parent.Score - worstChild * 0.65f - averageChild * 0.25f + balance * 0.12f + volumeReduction * 0.18f;
        }

        private static void MergeCompatibleClusters(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            List<Cluster> clusters,
            SolverOptions options)
        {
            var merged = true;
            var mergeCount = 0;
            var mergeLimit = Mathf.Clamp(options.MaxHulls * 3, 3, 48);
            while (merged && clusters.Count > 1 && mergeCount < mergeLimit && !RealPhysicsPlacerColliderBakeBudget.IsExpired)
            {
                merged = false;
                var bestLeft = -1;
                var bestRight = -1;
                var bestScore = float.PositiveInfinity;
                Cluster bestMergedCluster = null;
                for (var i = 0; i < clusters.Count; i++)
                {
                    for (var j = i + 1; j < clusters.Count; j++)
                    {
                        if (!TryScoreMergeCandidate(mesh, samples, voxels, clusters[i], clusters[j], options, out var score, out var mergedCluster))
                        {
                            continue;
                        }

                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestLeft = i;
                        bestRight = j;
                        bestMergedCluster = mergedCluster;
                    }
                }

                if (bestLeft < 0 || bestRight < 0 || bestMergedCluster == null)
                {
                    continue;
                }

                clusters[bestLeft] = bestMergedCluster;
                clusters.RemoveAt(bestRight);
                merged = true;
                mergeCount++;
            }
        }

        private static void CompactClustersToBudget(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            List<Cluster> clusters,
            SolverOptions options)
        {
            var targetCount = Mathf.Clamp(options.TargetHullCount, 1, options.MaxHulls);
            var mergeLimit = Mathf.Max(0, clusters.Count - targetCount);
            for (var mergeCount = 0; clusters.Count > targetCount && mergeCount < mergeLimit && !RealPhysicsPlacerColliderBakeBudget.IsExpired; mergeCount++)
            {
                var bestLeft = -1;
                var bestRight = -1;
                var bestScore = float.PositiveInfinity;
                Cluster bestMergedCluster = null;
                for (var i = 0; i < clusters.Count; i++)
                {
                    for (var j = i + 1; j < clusters.Count; j++)
                    {
                        if (!TryScoreBudgetMergeCandidate(mesh, samples, voxels, clusters[i], clusters[j], options, out var score, out var mergedCluster))
                        {
                            continue;
                        }

                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestLeft = i;
                        bestRight = j;
                        bestMergedCluster = mergedCluster;
                    }
                }

                if (bestLeft < 0 || bestRight < 0 || bestMergedCluster == null)
                {
                    break;
                }

                UpdateCluster(mesh, samples, voxels, bestMergedCluster, options);
                clusters[bestLeft] = bestMergedCluster;
                clusters.RemoveAt(bestRight);
            }
        }

        private static bool TryScoreBudgetMergeCandidate(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            Cluster left,
            Cluster right,
            SolverOptions options,
            out float score,
            out Cluster mergedCluster)
        {
            score = 0f;
            mergedCluster = CombineClusters(left, right);
            UpdateClusterCheap(mesh, samples, voxels, mergedCluster, options);

            var leftStats = CalculateClusterStats(mesh, left);
            var rightStats = CalculateClusterStats(mesh, right);
            var mergedBounds = mergedCluster.Bounds;
            var leftVolume = BoundsVolume(left.Bounds);
            var rightVolume = BoundsVolume(right.Bounds);
            var expansion = BoundsVolume(mergedBounds) / Mathf.Max(Epsilon, leftVolume + rightVolume);
            var gap = BoundsGap(left.Bounds, right.Bounds);
            var diagonal = Mathf.Max(Epsilon, mergedBounds.size.magnitude);
            var normalizedGap = gap / diagonal;
            var normalAlignment = Mathf.Abs(Vector3.Dot(leftStats.Normal, rightStats.Normal));
            var normalPenalty = 1f - normalAlignment;
            var planeDistance = CalculatePlaneSeparation(leftStats, rightStats) / diagonal;
            var openFeaturePressure = Mathf.Max(leftStats.OpenFeaturePressure, rightStats.OpenFeaturePressure);
            var mergedScorePenalty = Mathf.Max(0f, mergedCluster.Score - Mathf.Max(left.Score, right.Score));
            var strictness = CalculateAccuracyStrictness(options);
            var projectedFillWaste = normalAlignment > 0.78f
                ? CalculateProjectedFillWaste(mesh, mergedCluster, CalculateClusterStats(mesh, mergedCluster).Normal)
                : 0f;

            // This pass is allowed to merge because every source triangle must be
            // represented. The score strongly prefers planks and panels that are
            // coplanar or close, while making hole-spanning and opposite-wall
            // cavity bridges expensive. Lower Shape Accuracy means stricter
            // scoring, never looser scoring.
            score = expansion * Mathf.Lerp(1.2f, 1.75f, strictness)
                + normalizedGap * Mathf.Lerp(0.9f, 1.6f, strictness)
                + normalPenalty * Mathf.Lerp(0.75f, 1.35f, strictness)
                + planeDistance * Mathf.Lerp(1.2f, 3.2f, Mathf.Max(openFeaturePressure, strictness))
                + projectedFillWaste * normalAlignment * Mathf.Lerp(0.65f, 4.5f, strictness)
                + mergedScorePenalty * Mathf.Lerp(1.75f, 3.25f, strictness)
                + Mathf.Abs(leftStats.Area - rightStats.Area) / Mathf.Max(Epsilon, leftStats.Area + rightStats.Area) * 0.12f;
            return true;
        }

        private static bool TryScoreMergeCandidate(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            Cluster left,
            Cluster right,
            SolverOptions options,
            out float score,
            out Cluster mergedCluster)
        {
            score = 0f;
            mergedCluster = null;
            var mergedBounds = left.Bounds;
            mergedBounds.Encapsulate(right.Bounds.min);
            mergedBounds.Encapsulate(right.Bounds.max);

            var gap = BoundsGap(left.Bounds, right.Bounds);
            var leftVolume = BoundsVolume(left.Bounds);
            var rightVolume = BoundsVolume(right.Bounds);
            var mergedVolume = BoundsVolume(mergedBounds);
            var summedVolume = Mathf.Max(Epsilon, leftVolume + rightVolume);
            var expansion = mergedVolume / summedVolume;
            var diagonal = Mathf.Max(Epsilon, mergedBounds.size.magnitude);
            var normalizedGap = gap / diagonal;
            var leftStats = CalculateClusterStats(mesh, left);
            var rightStats = CalculateClusterStats(mesh, right);
            var normalAlignment = Mathf.Abs(Vector3.Dot(leftStats.Normal, rightStats.Normal));
            var openFeaturePressure = Mathf.Max(leftStats.OpenFeaturePressure, rightStats.OpenFeaturePressure);
            var strictness = CalculateAccuracyStrictness(options);

            // Real Hulls should not merge opposite walls into a filled box. A
            // bounds expansion gate is a cheap proxy for "would this bridge empty
            // space?", so it protects hollow meshes before any expensive scoring.
            var allowedExpansion = Mathf.Clamp(1.2f - openFeaturePressure * 0.08f - strictness * 0.06f + options.ShapeAccuracy * 0.25f, 1.06f, 1.24f);
            if (expansion > allowedExpansion)
            {
                return false;
            }

            var touchDistance = options.VoxelCellSize * 3.5f;
            if (gap > touchDistance && normalizedGap > 0.08f)
            {
                return false;
            }

            var childWorst = Mathf.Max(left.Score, right.Score);
            var expansionPenalty = Mathf.Max(0f, expansion - 1f);
            var combined = CombineClusters(left, right);
            UpdateCluster(mesh, samples, voxels, combined, options);
            if (normalAlignment > 0.82f)
            {
                var fillWaste = CalculateProjectedFillWaste(mesh, combined, CalculateClusterStats(mesh, combined).Normal);
                var allowedFillWaste = Mathf.Lerp(0.78f, 0.38f, Mathf.Max(strictness, openFeaturePressure));
                if (fillWaste > allowedFillWaste)
                {
                    return false;
                }
            }

            var allowedScore = childWorst + Mathf.Max(0.015f, options.ShapeAccuracy * Mathf.Lerp(0.12f, 0.32f, 1f - Mathf.Max(openFeaturePressure, strictness)));
            if (combined.Score > allowedScore)
            {
                return false;
            }

            score = combined.Score + expansionPenalty * 2.2f + normalizedGap * 0.6f + childWorst * 0.15f;
            mergedCluster = combined;
            return true;
        }

        private static float CalculateOpenFeaturePressure(NormalizedMesh mesh, Cluster cluster)
        {
            return CalculateClusterStats(mesh, cluster).OpenFeaturePressure;
        }

        private static float CalculateAccuracyStrictness(SolverOptions options)
        {
            return Mathf.Clamp01((0.18f - options.ShapeAccuracy) / 0.18f);
        }

        private static ClusterStats CalculateClusterStats(NormalizedMesh mesh, Cluster cluster)
        {
            var area = 0f;
            var featureArea = 0f;
            var weightedNormal = Vector3.zero;
            var weightedCenter = Vector3.zero;
            foreach (var triangleIndex in cluster.Triangles)
            {
                if (triangleIndex < 0 || triangleIndex >= mesh.Triangles.Count)
                {
                    continue;
                }

                var triangle = mesh.Triangles[triangleIndex];
                area += triangle.Area;
                weightedNormal += triangle.Normal * triangle.Area;
                weightedCenter += triangle.Center * triangle.Area;
                if (triangle.HasBoundary)
                {
                    featureArea += triangle.Area;
                }

                if (triangle.HasSharp)
                {
                    featureArea += triangle.Area * 0.5f;
                }
            }

            if (area <= Epsilon)
            {
                return new ClusterStats(Vector3.up, cluster.Bounds.center, 0f, 0f);
            }

            var normal = weightedNormal.sqrMagnitude > Epsilon * Epsilon
                ? weightedNormal.normalized
                : DominantBoundsAxis(cluster.Bounds);
            return new ClusterStats(normal, weightedCenter / area, area, Mathf.Clamp01(featureArea / area));
        }

        private static float CalculatePlaneSeparation(ClusterStats left, ClusterStats right)
        {
            var offset = right.Center - left.Center;
            return (Mathf.Abs(Vector3.Dot(offset, left.Normal)) + Mathf.Abs(Vector3.Dot(offset, right.Normal))) * 0.5f;
        }

        private static float CalculateProjectedFillWaste(NormalizedMesh mesh, Cluster cluster, Vector3 normal)
        {
            if (cluster == null || cluster.Triangles.Count == 0 || normal.sqrMagnitude <= Epsilon * Epsilon)
            {
                return 0f;
            }

            normal.Normalize();
            BuildProjectionAxes(normal, out var axisX, out var axisY);
            var initialized = false;
            var min = Vector2.zero;
            var max = Vector2.zero;
            var projectedTriangleArea = 0f;
            foreach (var triangleIndex in cluster.Triangles)
            {
                if (triangleIndex < 0 || triangleIndex >= mesh.Triangles.Count)
                {
                    continue;
                }

                var triangle = mesh.Triangles[triangleIndex];
                var a = ProjectPoint(mesh.Vertices[triangle.A], axisX, axisY);
                var b = ProjectPoint(mesh.Vertices[triangle.B], axisX, axisY);
                var c = ProjectPoint(mesh.Vertices[triangle.C], axisX, axisY);
                projectedTriangleArea += Mathf.Abs(Cross2D(b - a, c - a)) * 0.5f;
                EncapsulateProjectedPoint(a, ref min, ref max, ref initialized);
                EncapsulateProjectedPoint(b, ref min, ref max, ref initialized);
                EncapsulateProjectedPoint(c, ref min, ref max, ref initialized);
            }

            if (!initialized)
            {
                return 0f;
            }

            var size = max - min;
            var projectedBoundsArea = Mathf.Max(Epsilon, size.x * size.y);
            var occupiedRatio = Mathf.Clamp01(projectedTriangleArea / projectedBoundsArea);
            return 1f - occupiedRatio;
        }

        private static void BuildProjectionAxes(Vector3 normal, out Vector3 axisX, out Vector3 axisY)
        {
            axisX = Vector3.Cross(Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right, normal);
            if (axisX.sqrMagnitude <= Epsilon * Epsilon)
            {
                axisX = Vector3.Cross(Vector3.forward, normal);
            }

            axisX.Normalize();
            axisY = Vector3.Cross(normal, axisX).normalized;
        }

        private static Vector2 ProjectPoint(Vector3 point, Vector3 axisX, Vector3 axisY)
        {
            return new Vector2(Vector3.Dot(point, axisX), Vector3.Dot(point, axisY));
        }

        private static float Cross2D(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static void EncapsulateProjectedPoint(Vector2 point, ref Vector2 min, ref Vector2 max, ref bool initialized)
        {
            if (!initialized)
            {
                min = point;
                max = point;
                initialized = true;
                return;
            }

            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private static Vector3 DominantBoundsAxis(Bounds bounds)
        {
            var size = bounds.size;
            if (size.x <= size.y && size.x <= size.z)
            {
                return Vector3.right;
            }

            return size.y <= size.z ? Vector3.up : Vector3.forward;
        }

        private static Cluster CombineClusters(Cluster left, Cluster right)
        {
            var combined = new Cluster();
            combined.Triangles.AddRange(left.Triangles);
            combined.Triangles.AddRange(right.Triangles);
            combined.Samples.AddRange(left.Samples);
            combined.Samples.AddRange(right.Samples);
            combined.Voxels.AddRange(left.Voxels);
            combined.Voxels.AddRange(right.Voxels);
            return combined;
        }

        private static List<List<Vector3>> BuildFinalPointClouds(NormalizedMesh mesh, List<SamplePoint> samples, List<Cluster> clusters, SolverOptions options)
        {
            clusters.Sort((left, right) => CalculateFinalClusterPriority(mesh, right).CompareTo(CalculateFinalClusterPriority(mesh, left)));
            var pointClouds = new List<List<Vector3>>(Mathf.Min(clusters.Count, options.MaxHulls));
            for (var clusterIndex = 0; clusterIndex < clusters.Count && pointClouds.Count < options.MaxHulls; clusterIndex++)
            {
                var cluster = clusters[clusterIndex];
                var points = new List<Vector3>(cluster.Samples.Count + cluster.Triangles.Count * 3);
                foreach (var sampleIndex in cluster.Samples)
                {
                    AddUniquePoint(points, mesh.Restore(samples[sampleIndex].Point), mesh.RestoreEpsilon);
                }

                foreach (var triangleIndex in cluster.Triangles)
                {
                    var triangle = mesh.Triangles[triangleIndex];
                    AddUniquePoint(points, mesh.Restore(mesh.Vertices[triangle.A]), mesh.RestoreEpsilon);
                    AddUniquePoint(points, mesh.Restore(mesh.Vertices[triangle.B]), mesh.RestoreEpsilon);
                    AddUniquePoint(points, mesh.Restore(mesh.Vertices[triangle.C]), mesh.RestoreEpsilon);
                }

                if (points.Count >= 3)
                {
                    pointClouds.Add(points);
                }
            }

            return pointClouds;
        }

        private static float CalculateFinalClusterPriority(NormalizedMesh mesh, Cluster cluster)
        {
            if (cluster == null)
            {
                return 0f;
            }

            var area = 0f;
            var featureWeight = 0f;
            foreach (var triangleIndex in cluster.Triangles)
            {
                if (triangleIndex < 0 || triangleIndex >= mesh.Triangles.Count)
                {
                    continue;
                }

                var triangle = mesh.Triangles[triangleIndex];
                area += triangle.Area;
                if (triangle.HasBoundary)
                {
                    featureWeight += triangle.Area * 0.35f;
                }

                if (triangle.HasSharp)
                {
                    featureWeight += triangle.Area * 0.2f;
                }
            }

            var sampleWeight = cluster.Samples.Count * 0.001f;
            return cluster.Score * 2.5f + area + featureWeight + sampleWeight;
        }

        private static List<RealPhysicsPlacerColliderDecomposer.Piece> BuildPiecesFromPointClouds(List<List<Vector3>> pointClouds, string sourceName, SolverOptions options)
        {
            var pieces = new List<RealPhysicsPlacerColliderDecomposer.Piece>(pointClouds.Count);
            for (var i = 0; i < pointClouds.Count && pieces.Count < options.MaxHulls; i++)
            {
                if (RealPhysicsPlacerColliderDecomposer.TryCreateConvexPieceFromPoints(
                        pointClouds[i],
                        $"{sourceName}_RealTransforms_RealHull_{i + 1:000}",
                        RealPhysicsPlacerBakedColliderShape.RealHulls,
                        options.HullDetail,
                        out var piece))
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        private static List<Vector3> BuildSplitDirections(NormalizedMesh mesh, List<SamplePoint> samples, Cluster cluster)
        {
            var directions = new List<Vector3>(24);
            AddDirection(directions, Vector3.right);
            AddDirection(directions, Vector3.up);
            AddDirection(directions, Vector3.forward);
            var size = cluster.Bounds.size;
            AddDirection(directions, size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward);
            AddPcaDirections(directions, samples, cluster);
            AddDominantNormalDirections(mesh, directions, cluster);

            var center = cluster.Bounds.center;
            var worst = Vector3.zero;
            var worstDistance = 0f;
            foreach (var sampleIndex in cluster.Samples)
            {
                var offset = samples[sampleIndex].Point - center;
                var distance = offset.sqrMagnitude;
                if (distance <= worstDistance)
                {
                    continue;
                }

                worstDistance = distance;
                worst = offset;
            }

            AddDirection(directions, worst);
            return directions;
        }

        private static void AddPcaDirections(List<Vector3> directions, List<SamplePoint> samples, Cluster cluster)
        {
            if (cluster.Samples.Count < 3)
            {
                return;
            }

            var mean = Vector3.zero;
            foreach (var sampleIndex in cluster.Samples)
            {
                mean += samples[sampleIndex].Point;
            }

            mean /= cluster.Samples.Count;
            var covariance = new Matrix3();
            foreach (var sampleIndex in cluster.Samples)
            {
                var offset = samples[sampleIndex].Point - mean;
                covariance.XX += offset.x * offset.x;
                covariance.XY += offset.x * offset.y;
                covariance.XZ += offset.x * offset.z;
                covariance.YY += offset.y * offset.y;
                covariance.YZ += offset.y * offset.z;
                covariance.ZZ += offset.z * offset.z;
            }

            var axisX = PowerIterate(covariance, Vector3.right, Vector3.zero);
            var axisY = PowerIterate(covariance, MostOrthogonalAxis(axisX), axisX);
            var axisZ = Vector3.Cross(axisX, axisY);
            AddDirection(directions, axisX);
            AddDirection(directions, axisY);
            AddDirection(directions, axisZ);
        }

        private static void AddDominantNormalDirections(NormalizedMesh mesh, List<Vector3> directions, Cluster cluster)
        {
            var ordered = cluster.Triangles
                .Select(index => mesh.Triangles[index])
                .Where(triangle => triangle.Area > Epsilon)
                .OrderByDescending(triangle => triangle.Area * (triangle.HasBoundary || triangle.HasSharp ? 2f : 1f))
                .Take(18);
            foreach (var triangle in ordered)
            {
                AddDirection(directions, triangle.Normal);
            }
        }

        private static List<float> BuildSplitOffsets(List<SamplePoint> samples, Cluster cluster, Vector3 direction)
        {
            var projections = cluster.Samples
                .Select(index => Vector3.Dot(samples[index].Point, direction))
                .OrderBy(value => value)
                .ToList();
            var offsets = new List<float>(7);
            if (projections.Count < 4)
            {
                return offsets;
            }

            AddOffset(offsets, projections, 0.2f);
            AddOffset(offsets, projections, 0.35f);
            AddOffset(offsets, projections, 0.5f);
            AddOffset(offsets, projections, 0.65f);
            AddOffset(offsets, projections, 0.8f);
            return offsets;
        }

        private static void AddOffset(List<float> offsets, List<float> projections, float fraction)
        {
            var index = Mathf.Clamp(Mathf.RoundToInt((projections.Count - 1) * fraction), 1, projections.Count - 2);
            var value = projections[index];
            if (!offsets.Any(existing => Mathf.Abs(existing - value) <= Epsilon))
            {
                offsets.Add(value);
            }
        }

        private static void UpdateCluster(NormalizedMesh mesh, List<SamplePoint> samples, List<VoxelCell> voxels, Cluster cluster, SolverOptions options)
        {
            cluster.Bounds = CalculateClusterBounds(mesh, samples, cluster);
            cluster.Closed = TryCollectClosedClusterVertices(mesh, cluster, out var closedVertices);
            if (cluster.Closed && IsConvexCluster(mesh, cluster, closedVertices))
            {
                cluster.Score = 0f;
                cluster.Terminal = true;
                return;
            }

            cluster.Score = ScoreCluster(mesh, samples, voxels, cluster, options);
        }

        private static void UpdateClusterCheap(NormalizedMesh mesh, List<SamplePoint> samples, List<VoxelCell> voxels, Cluster cluster, SolverOptions options)
        {
            cluster.Bounds = CalculateClusterBounds(mesh, samples, cluster);
            cluster.Closed = false;
            cluster.Terminal = false;
            cluster.Score = ScoreClusterCheap(mesh, samples, voxels, cluster, options);
        }

        private static bool TryCollectClosedClusterVertices(NormalizedMesh mesh, Cluster cluster, out List<Vector3> vertices)
        {
            vertices = null;
            if (cluster == null || cluster.Triangles.Count < 4)
            {
                return false;
            }

            var edgeCounts = new Dictionary<ulong, int>(cluster.Triangles.Count * 3);
            var vertexSet = new HashSet<int>();
            foreach (var triangleIndex in cluster.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                vertexSet.Add(triangle.A);
                vertexSet.Add(triangle.B);
                vertexSet.Add(triangle.C);
                CountEdge(edgeCounts, triangle.A, triangle.B);
                CountEdge(edgeCounts, triangle.B, triangle.C);
                CountEdge(edgeCounts, triangle.C, triangle.A);
            }

            if (vertexSet.Count < 4 || edgeCounts.Any(pair => pair.Value != 2))
            {
                return false;
            }

            vertices = vertexSet.Select(index => mesh.Vertices[index]).ToList();
            return true;
        }

        private static bool IsConvexCluster(NormalizedMesh mesh, Cluster cluster, List<Vector3> vertices)
        {
            if (vertices == null || vertices.Count < 4)
            {
                return false;
            }

            var tolerance = Mathf.Max(0.00025f, CalculateNormalizedBounds(vertices).size.magnitude * 0.00075f);
            foreach (var triangleIndex in cluster.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                var planePoint = mesh.Vertices[triangle.A];
                var positive = false;
                var negative = false;
                foreach (var vertex in vertices)
                {
                    var distance = Vector3.Dot(vertex - planePoint, triangle.Normal);
                    if (distance > tolerance)
                    {
                        positive = true;
                    }
                    else if (distance < -tolerance)
                    {
                        negative = true;
                    }

                    if (positive && negative)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void CountEdge(Dictionary<ulong, int> edgeCounts, int a, int b)
        {
            var key = EdgeKey(a, b);
            edgeCounts.TryGetValue(key, out var count);
            edgeCounts[key] = count + 1;
        }

        private static float ScoreCluster(NormalizedMesh mesh, List<SamplePoint> samples, List<VoxelCell> voxels, Cluster cluster, SolverOptions options)
        {
            var boundsVolume = BoundsVolume(cluster.Bounds);
            var voxelVolume = Mathf.Max(Epsilon, cluster.Voxels.Count * options.VoxelCellVolume);
            if (cluster.Voxels.Count == 0)
            {
                var area = 0f;
                foreach (var triangleIndex in cluster.Triangles)
                {
                    area += mesh.Triangles[triangleIndex].Area;
                }

                voxelVolume = Mathf.Max(Epsilon, area * options.VoxelCellSize);
            }

            var waste = Mathf.Clamp01((boundsVolume - voxelVolume) / Mathf.Max(Epsilon, boundsVolume));
            var size = cluster.Bounds.size;
            var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var shortest = Mathf.Max(Epsilon, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            var thinness = Mathf.Clamp01(longest / shortest / 18f);
            var featureWeight = 0f;
            foreach (var triangleIndex in cluster.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                if (triangle.HasBoundary)
                {
                    featureWeight += 0.015f;
                }

                if (triangle.HasSharp)
                {
                    featureWeight += 0.01f;
                }
            }

            featureWeight = Mathf.Clamp01(featureWeight / Mathf.Max(1, cluster.Triangles.Count) * 20f) * Mathf.Clamp01(waste * 2.5f);
            var fittedHullWaste = EstimateSupportHullWaste(mesh, samples, voxels, cluster, options);
            var samplePressure = Mathf.Clamp01(cluster.Samples.Count / (float)Mathf.Max(16, options.HullDetail * 2));
            return fittedHullWaste * 0.62f + waste * 0.2f + thinness * fittedHullWaste * 0.08f + featureWeight * 0.08f + samplePressure * 0.02f;
        }

        private static float ScoreClusterCheap(NormalizedMesh mesh, List<SamplePoint> samples, List<VoxelCell> voxels, Cluster cluster, SolverOptions options)
        {
            var boundsVolume = BoundsVolume(cluster.Bounds);
            var voxelVolume = Mathf.Max(Epsilon, cluster.Voxels.Count * options.VoxelCellVolume);
            if (cluster.Voxels.Count == 0)
            {
                var area = 0f;
                foreach (var triangleIndex in cluster.Triangles)
                {
                    area += mesh.Triangles[triangleIndex].Area;
                }

                voxelVolume = Mathf.Max(Epsilon, area * options.VoxelCellSize);
            }

            var waste = Mathf.Clamp01((boundsVolume - voxelVolume) / Mathf.Max(Epsilon, boundsVolume));
            var size = cluster.Bounds.size;
            var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var shortest = Mathf.Max(Epsilon, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            var thinness = Mathf.Clamp01(longest / shortest / 18f);
            var featureWeight = 0f;
            foreach (var triangleIndex in cluster.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                if (triangle.HasBoundary)
                {
                    featureWeight += 0.015f;
                }

                if (triangle.HasSharp)
                {
                    featureWeight += 0.01f;
                }
            }

            featureWeight = Mathf.Clamp01(featureWeight / Mathf.Max(1, cluster.Triangles.Count) * 20f) * Mathf.Clamp01(waste * 2.5f);
            var samplePressure = Mathf.Clamp01(cluster.Samples.Count / (float)Mathf.Max(16, options.HullDetail * 2));
            return waste * 0.48f + thinness * waste * 0.12f + featureWeight * 0.18f + samplePressure * 0.04f;
        }

        private static float EstimateSupportHullWaste(
            NormalizedMesh mesh,
            List<SamplePoint> samples,
            List<VoxelCell> voxels,
            Cluster cluster,
            SolverOptions options)
        {
            if (cluster.Samples.Count < 4 || cluster.Voxels.Count == 0)
            {
                return 0f;
            }

            var directions = BuildSupportDirections(mesh, samples, cluster);
            if (directions.Count < 6)
            {
                return 0f;
            }

            var support = new float[directions.Count];
            for (var i = 0; i < support.Length; i++)
            {
                support[i] = float.NegativeInfinity;
            }

            foreach (var sampleIndex in cluster.Samples)
            {
                ExtendSupport(samples[sampleIndex].Point, directions, support);
            }

            foreach (var triangleIndex in cluster.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                ExtendSupport(mesh.Vertices[triangle.A], directions, support);
                ExtendSupport(mesh.Vertices[triangle.B], directions, support);
                ExtendSupport(mesh.Vertices[triangle.C], directions, support);
            }

            var occupied = new HashSet<VoxelKey>();
            foreach (var voxelIndex in cluster.Voxels)
            {
                AddDilatedVoxel(occupied, voxels[voxelIndex].Key, options.VoxelResolution);
            }

            var min = ToVoxelKey(cluster.Bounds.min - Vector3.one * options.VoxelCellSize, options.VoxelResolution);
            var max = ToVoxelKey(cluster.Bounds.max + Vector3.one * options.VoxelCellSize, options.VoxelResolution);
            var totalCells = Mathf.Max(1, (max.X - min.X + 1) * (max.Y - min.Y + 1) * (max.Z - min.Z + 1));
            var stride = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(totalCells / 24000f, 1f / 3f)));
            var tested = 0;
            var insideHull = 0;
            var occupiedInsideHull = 0;
            var padding = options.VoxelCellSize * 1.2f;

            for (var x = min.X; x <= max.X; x += stride)
            {
                if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    return 0f;
                }

                for (var y = min.Y; y <= max.Y; y += stride)
                {
                    for (var z = min.Z; z <= max.Z; z += stride)
                    {
                        tested++;
                        var key = new VoxelKey(x, y, z);
                        if (!PointInsideSupportHull(VoxelCenter(key, options.VoxelResolution), directions, support, padding))
                        {
                            continue;
                        }

                        insideHull++;
                        if (HasNearbyOccupiedVoxel(occupied, key, stride))
                        {
                            occupiedInsideHull++;
                        }
                    }
                }
            }

            if (tested == 0 || insideHull == 0)
            {
                return 0f;
            }

            var emptyHullRatio = Mathf.Clamp01((insideHull - occupiedInsideHull) / (float)insideHull);
            var hullCoverage = Mathf.Clamp01(insideHull / (float)tested);
            return emptyHullRatio * Mathf.Lerp(0.55f, 1f, hullCoverage);
        }

        private static bool HasNearbyOccupiedVoxel(HashSet<VoxelKey> occupied, VoxelKey key, int radius)
        {
            radius = Mathf.Max(1, radius);
            for (var x = key.X - radius; x <= key.X + radius; x++)
            {
                for (var y = key.Y - radius; y <= key.Y + radius; y++)
                {
                    for (var z = key.Z - radius; z <= key.Z + radius; z++)
                    {
                        if (occupied.Contains(new VoxelKey(x, y, z)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void AddDilatedVoxel(HashSet<VoxelKey> occupied, VoxelKey key, int resolution)
        {
            for (var x = key.X - 1; x <= key.X + 1; x++)
            {
                if (x < 0 || x >= resolution)
                {
                    continue;
                }

                for (var y = key.Y - 1; y <= key.Y + 1; y++)
                {
                    if (y < 0 || y >= resolution)
                    {
                        continue;
                    }

                    for (var z = key.Z - 1; z <= key.Z + 1; z++)
                    {
                        if (z < 0 || z >= resolution)
                        {
                            continue;
                        }

                        occupied.Add(new VoxelKey(x, y, z));
                    }
                }
            }
        }

        private static List<Vector3> BuildSupportDirections(NormalizedMesh mesh, List<SamplePoint> samples, Cluster cluster)
        {
            var directions = new List<Vector3>(72);
            AddSignedDirection(directions, Vector3.right);
            AddSignedDirection(directions, -Vector3.right);
            AddSignedDirection(directions, Vector3.up);
            AddSignedDirection(directions, -Vector3.up);
            AddSignedDirection(directions, Vector3.forward);
            AddSignedDirection(directions, -Vector3.forward);

            var pca = new List<Vector3>(6);
            AddPcaDirections(pca, samples, cluster);
            foreach (var direction in pca)
            {
                AddSignedDirection(directions, direction);
                AddSignedDirection(directions, -direction);
            }

            var normalDirections = new List<Vector3>(18);
            AddDominantNormalDirections(mesh, normalDirections, cluster);
            foreach (var direction in normalDirections)
            {
                AddSignedDirection(directions, direction);
                AddSignedDirection(directions, -direction);
            }

            for (var i = 0; i < 24; i++)
            {
                var direction = FibonacciDirection(i, 24);
                AddSignedDirection(directions, direction);
                AddSignedDirection(directions, -direction);
            }

            return directions;
        }

        private static void ExtendSupport(Vector3 point, List<Vector3> directions, float[] support)
        {
            for (var i = 0; i < directions.Count; i++)
            {
                support[i] = Mathf.Max(support[i], Vector3.Dot(point, directions[i]));
            }
        }

        private static bool PointInsideSupportHull(Vector3 point, List<Vector3> directions, float[] support, float padding)
        {
            for (var i = 0; i < directions.Count; i++)
            {
                if (Vector3.Dot(point, directions[i]) > support[i] + padding)
                {
                    return false;
                }
            }

            return true;
        }

        private static Bounds CalculateClusterBounds(NormalizedMesh mesh, List<SamplePoint> samples, Cluster cluster)
        {
            var initialized = false;
            var bounds = new Bounds();
            foreach (var sampleIndex in cluster.Samples)
            {
                var point = samples[sampleIndex].Point;
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }

            foreach (var triangleIndex in cluster.Triangles)
            {
                var triangle = mesh.Triangles[triangleIndex];
                Encapsulate(mesh.Vertices[triangle.A], ref bounds, ref initialized);
                Encapsulate(mesh.Vertices[triangle.B], ref bounds, ref initialized);
                Encapsulate(mesh.Vertices[triangle.C], ref bounds, ref initialized);
            }

            return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.001f);
        }

        private static void Encapsulate(Vector3 point, ref Bounds bounds, ref bool initialized)
        {
            if (!initialized)
            {
                bounds = new Bounds(point, Vector3.zero);
                initialized = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        private static Bounds CalculateNormalizedBounds(List<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return bounds;
        }

        private static List<Cluster> BuildInitialClusters(NormalizedMesh mesh, List<SamplePoint> samples, List<VoxelCell> voxels)
        {
            var groups = BuildConnectedTriangleGroups(mesh);
            var clusters = new List<Cluster>(groups.Count);
            var triangleToGroup = new Dictionary<int, int>();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var cluster = new Cluster();
                cluster.Triangles.AddRange(groups[groupIndex]);
                clusters.Add(cluster);
                foreach (var triangleIndex in groups[groupIndex])
                {
                    triangleToGroup[triangleIndex] = groupIndex;
                }
            }

            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                if (triangleToGroup.TryGetValue(samples[sampleIndex].TriangleIndex, out var groupIndex))
                {
                    clusters[groupIndex].Samples.Add(sampleIndex);
                }
            }

            for (var voxelIndex = 0; voxelIndex < voxels.Count; voxelIndex++)
            {
                var triangleIndex = voxels[voxelIndex].TriangleIndex;
                if (triangleIndex >= 0 && triangleToGroup.TryGetValue(triangleIndex, out var groupIndex))
                {
                    clusters[groupIndex].Voxels.Add(voxelIndex);
                }
                else if (clusters.Count == 1)
                {
                    clusters[0].Voxels.Add(voxelIndex);
                }
            }

            return clusters.Where(cluster => cluster.Triangles.Count > 0 && cluster.Samples.Count > 0).ToList();
        }

        private static List<List<int>> BuildConnectedTriangleGroups(NormalizedMesh mesh)
        {
            var visited = new bool[mesh.Triangles.Count];
            var groups = new List<List<int>>();
            var queue = new Queue<int>();
            for (var triangleIndex = 0; triangleIndex < mesh.Triangles.Count; triangleIndex++)
            {
                if (visited[triangleIndex])
                {
                    continue;
                }

                var group = new List<int>();
                visited[triangleIndex] = true;
                queue.Enqueue(triangleIndex);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);
                    foreach (var neighbor in mesh.Neighbors[current])
                    {
                        if (visited[neighbor])
                        {
                            continue;
                        }

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                groups.Add(group);
            }

            return groups.OrderByDescending(group => group.Count).ToList();
        }

        private static List<SamplePoint> BuildSurfaceSamples(NormalizedMesh mesh, SolverOptions options)
        {
            var samples = new List<SamplePoint>(Mathf.Min(options.MaxSamples, mesh.Vertices.Count + mesh.Triangles.Count * 4));
            var keys = new HashSet<PointKey>();
            void Add(Vector3 point, int triangleIndex, bool feature)
            {
                if (samples.Count >= options.MaxSamples)
                {
                    return;
                }

                var key = new PointKey(point, 10000f);
                if (!keys.Add(key))
                {
                    return;
                }

                samples.Add(new SamplePoint(point, triangleIndex, feature));
            }

            for (var triangleIndex = 0; triangleIndex < mesh.Triangles.Count; triangleIndex++)
            {
                var triangle = mesh.Triangles[triangleIndex];
                Add(mesh.Vertices[triangle.A], triangleIndex, triangle.HasBoundary || triangle.HasSharp);
                Add(mesh.Vertices[triangle.B], triangleIndex, triangle.HasBoundary || triangle.HasSharp);
                Add(mesh.Vertices[triangle.C], triangleIndex, triangle.HasBoundary || triangle.HasSharp);
                Add(triangle.Center, triangleIndex, false);
                if (triangle.HasBoundary || triangle.HasSharp)
                {
                    Add((mesh.Vertices[triangle.A] + mesh.Vertices[triangle.B]) * 0.5f, triangleIndex, true);
                    Add((mesh.Vertices[triangle.B] + mesh.Vertices[triangle.C]) * 0.5f, triangleIndex, true);
                    Add((mesh.Vertices[triangle.C] + mesh.Vertices[triangle.A]) * 0.5f, triangleIndex, true);
                }
            }

            if (samples.Count >= options.MaxSamples)
            {
                return samples;
            }

            var totalArea = Mathf.Max(Epsilon, mesh.Triangles.Sum(triangle => triangle.Area));
            var remaining = options.MaxSamples - samples.Count;
            var density = remaining / totalArea;
            foreach (var pair in mesh.Triangles.Select((triangle, index) => new { triangle, index }).OrderByDescending(item => item.triangle.Area))
            {
                if (samples.Count >= options.MaxSamples || RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    break;
                }

                var count = Mathf.Clamp(Mathf.RoundToInt(pair.triangle.Area * density), 0, 18);
                for (var i = 0; i < count && samples.Count < options.MaxSamples; i++)
                {
                    var bary = BarycentricPattern(i);
                    var a = mesh.Vertices[pair.triangle.A];
                    var b = mesh.Vertices[pair.triangle.B];
                    var c = mesh.Vertices[pair.triangle.C];
                    Add(a * bary.x + b * bary.y + c * bary.z, pair.index, false);
                }
            }

            return samples;
        }

        private static List<VoxelCell> BuildVoxelCells(NormalizedMesh mesh, List<SamplePoint> samples, SolverOptions options)
        {
            var resolution = options.VoxelResolution;
            var surface = new Dictionary<VoxelKey, int>();
            foreach (var sample in samples)
            {
                MarkVoxel(surface, sample.Point, sample.TriangleIndex, resolution);
            }

            foreach (var pair in mesh.Triangles.Select((triangle, index) => new { triangle, index }))
            {
                if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    break;
                }

                var steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(pair.triangle.Area) * resolution * 1.5f), 1, 12);
                for (var u = 0; u <= steps; u++)
                {
                    for (var v = 0; v <= steps - u; v++)
                    {
                        var fu = u / (float)Mathf.Max(1, steps);
                        var fv = v / (float)Mathf.Max(1, steps);
                        var fw = 1f - fu - fv;
                        var point = mesh.Vertices[pair.triangle.A] * fw
                            + mesh.Vertices[pair.triangle.B] * fu
                            + mesh.Vertices[pair.triangle.C] * fv;
                        MarkVoxel(surface, point, pair.index, resolution);
                    }
                }
            }

            var occupied = new HashSet<VoxelKey>(surface.Keys);
            FillInteriorVoxels(occupied, resolution);
            var cells = new List<VoxelCell>(occupied.Count);
            foreach (var key in occupied)
            {
                surface.TryGetValue(key, out var triangleIndex);
                cells.Add(new VoxelCell(key, VoxelCenter(key, resolution), surface.ContainsKey(key) ? triangleIndex : -1));
            }

            return cells;
        }

        private static void FillInteriorVoxels(HashSet<VoxelKey> occupied, int resolution)
        {
            if (occupied.Count == 0)
            {
                return;
            }

            var exterior = new HashSet<VoxelKey>();
            var queue = new Queue<VoxelKey>();
            void Seed(int x, int y, int z)
            {
                var key = new VoxelKey(x, y, z);
                if (occupied.Contains(key) || !exterior.Add(key))
                {
                    return;
                }

                queue.Enqueue(key);
            }

            for (var i = 0; i < resolution; i++)
            {
                for (var j = 0; j < resolution; j++)
                {
                    Seed(0, i, j);
                    Seed(resolution - 1, i, j);
                    Seed(i, 0, j);
                    Seed(i, resolution - 1, j);
                    Seed(i, j, 0);
                    Seed(i, j, resolution - 1);
                }
            }

            var directions = new[]
            {
                new VoxelKey(1, 0, 0),
                new VoxelKey(-1, 0, 0),
                new VoxelKey(0, 1, 0),
                new VoxelKey(0, -1, 0),
                new VoxelKey(0, 0, 1),
                new VoxelKey(0, 0, -1)
            };

            while (queue.Count > 0)
            {
                if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    return;
                }

                var current = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = new VoxelKey(current.X + direction.X, current.Y + direction.Y, current.Z + direction.Z);
                    if (next.X < 0 || next.Y < 0 || next.Z < 0 || next.X >= resolution || next.Y >= resolution || next.Z >= resolution)
                    {
                        continue;
                    }

                    if (occupied.Contains(next) || !exterior.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            for (var x = 0; x < resolution; x++)
            {
                if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    return;
                }

                for (var y = 0; y < resolution; y++)
                {
                    for (var z = 0; z < resolution; z++)
                    {
                        var key = new VoxelKey(x, y, z);
                        if (occupied.Contains(key) || exterior.Contains(key))
                        {
                            continue;
                        }

                        occupied.Add(key);
                    }
                }
            }
        }

        private static void MarkVoxel(Dictionary<VoxelKey, int> voxels, Vector3 point, int triangleIndex, int resolution)
        {
            var key = ToVoxelKey(point, resolution);
            if (!voxels.ContainsKey(key))
            {
                voxels[key] = triangleIndex;
            }
        }

        private static VoxelKey ToVoxelKey(Vector3 point, int resolution)
        {
            var normalized = point + Vector3.one * 0.5f;
            return new VoxelKey(
                Mathf.Clamp(Mathf.FloorToInt(normalized.x * resolution), 0, resolution - 1),
                Mathf.Clamp(Mathf.FloorToInt(normalized.y * resolution), 0, resolution - 1),
                Mathf.Clamp(Mathf.FloorToInt(normalized.z * resolution), 0, resolution - 1));
        }

        private static Vector3 VoxelCenter(VoxelKey key, int resolution)
        {
            var inv = 1f / resolution;
            return new Vector3((key.X + 0.5f) * inv - 0.5f, (key.Y + 0.5f) * inv - 0.5f, (key.Z + 0.5f) * inv - 0.5f);
        }

        private static bool TryPreprocess(Vector3[] sourceVertices, int[] sourceTriangles, out NormalizedMesh mesh)
        {
            mesh = null;
            if (sourceVertices.Length < 3 || sourceTriangles.Length < 3)
            {
                return false;
            }

            var sourceBounds = new Bounds(sourceVertices[0], Vector3.zero);
            for (var i = 1; i < sourceVertices.Length; i++)
            {
                sourceBounds.Encapsulate(sourceVertices[i]);
            }

            var size = sourceBounds.size;
            var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest <= Epsilon)
            {
                return false;
            }

            var scale = 1f / longest;
            var center = sourceBounds.center;
            var vertices = new List<Vector3>(sourceVertices.Length);
            var remap = new int[sourceVertices.Length];
            var weld = new Dictionary<PointKey, int>();
            for (var i = 0; i < sourceVertices.Length; i++)
            {
                var normalized = (sourceVertices[i] - center) * scale;
                var key = new PointKey(normalized, 100000f);
                if (!weld.TryGetValue(key, out var index))
                {
                    index = vertices.Count;
                    weld[key] = index;
                    vertices.Add(normalized);
                }

                remap[i] = index;
            }

            var triangles = new List<TriangleInfo>(sourceTriangles.Length / 3);
            for (var i = 0; i + 2 < sourceTriangles.Length; i += 3)
            {
                var a = remap[Mathf.Clamp(sourceTriangles[i], 0, sourceVertices.Length - 1)];
                var b = remap[Mathf.Clamp(sourceTriangles[i + 1], 0, sourceVertices.Length - 1)];
                var c = remap[Mathf.Clamp(sourceTriangles[i + 2], 0, sourceVertices.Length - 1)];
                if (a == b || b == c || c == a)
                {
                    continue;
                }

                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                var area = normal.magnitude * 0.5f;
                if (area <= Epsilon)
                {
                    continue;
                }

                triangles.Add(new TriangleInfo(a, b, c, normal.normalized, area, (vertices[a] + vertices[b] + vertices[c]) / 3f));
            }

            if (triangles.Count == 0)
            {
                return false;
            }

            mesh = new NormalizedMesh(vertices, triangles, center, longest);
            BuildMeshAdjacency(mesh);
            return true;
        }

        private static void BuildMeshAdjacency(NormalizedMesh mesh)
        {
            mesh.Neighbors = new List<int>[mesh.Triangles.Count];
            for (var i = 0; i < mesh.Neighbors.Length; i++)
            {
                mesh.Neighbors[i] = new List<int>(3);
            }

            var edgeToTriangles = new Dictionary<ulong, List<int>>();
            for (var triangleIndex = 0; triangleIndex < mesh.Triangles.Count; triangleIndex++)
            {
                var triangle = mesh.Triangles[triangleIndex];
                AddEdge(edgeToTriangles, triangle.A, triangle.B, triangleIndex);
                AddEdge(edgeToTriangles, triangle.B, triangle.C, triangleIndex);
                AddEdge(edgeToTriangles, triangle.C, triangle.A, triangleIndex);
            }

            var sharpDot = Mathf.Cos(34f * Mathf.Deg2Rad);
            foreach (var pair in edgeToTriangles)
            {
                var linked = pair.Value;
                if (linked.Count == 1)
                {
                    mesh.Triangles[linked[0]].HasBoundary = true;
                    continue;
                }

                for (var i = 0; i < linked.Count; i++)
                {
                    for (var j = i + 1; j < linked.Count; j++)
                    {
                        var left = linked[i];
                        var right = linked[j];
                        AddNeighbor(mesh.Neighbors, left, right);
                        if (Mathf.Abs(Vector3.Dot(mesh.Triangles[left].Normal, mesh.Triangles[right].Normal)) < sharpDot)
                        {
                            mesh.Triangles[left].HasSharp = true;
                            mesh.Triangles[right].HasSharp = true;
                        }
                    }
                }
            }
        }

        private static void AddEdge(Dictionary<ulong, List<int>> edges, int a, int b, int triangleIndex)
        {
            var key = EdgeKey(a, b);
            if (!edges.TryGetValue(key, out var linked))
            {
                linked = new List<int>(2);
                edges[key] = linked;
            }

            linked.Add(triangleIndex);
        }

        private static void AddNeighbor(List<int>[] neighbors, int left, int right)
        {
            if (!neighbors[left].Contains(right))
            {
                neighbors[left].Add(right);
            }

            if (!neighbors[right].Contains(left))
            {
                neighbors[right].Add(left);
            }
        }

        private static ulong EdgeKey(int a, int b)
        {
            var min = (uint)Mathf.Min(a, b);
            var max = (uint)Mathf.Max(a, b);
            return ((ulong)min << 32) | max;
        }

        private static Vector3 BarycentricPattern(int index)
        {
            var u = Halton(index + 1, 2);
            var v = Halton(index + 1, 3);
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }

            return new Vector3(1f - u - v, u, v);
        }

        private static float Halton(int index, int radix)
        {
            var result = 0f;
            var fraction = 1f / radix;
            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }

            return result;
        }

        private static void AddDirection(List<Vector3> directions, Vector3 direction)
        {
            if (direction.sqrMagnitude <= Epsilon * Epsilon)
            {
                return;
            }

            direction.Normalize();
            foreach (var existing in directions)
            {
                if (Mathf.Abs(Vector3.Dot(existing, direction)) > 0.965f)
                {
                    return;
                }
            }

            directions.Add(direction);
        }

        private static void AddSignedDirection(List<Vector3> directions, Vector3 direction)
        {
            if (direction.sqrMagnitude <= Epsilon * Epsilon)
            {
                return;
            }

            direction.Normalize();
            foreach (var existing in directions)
            {
                if (Vector3.Dot(existing, direction) > 0.985f)
                {
                    return;
                }
            }

            directions.Add(direction);
        }

        private static Vector3 FibonacciDirection(int index, int count)
        {
            count = Mathf.Max(1, count);
            var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            var y = 1f - 2f * (index + 0.5f) / count;
            var radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            var theta = index * goldenAngle;
            return new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
        }

        private static void AddUniquePoint(List<Vector3> points, Vector3 point, float epsilon)
        {
            var sqr = epsilon * epsilon;
            foreach (var existing in points)
            {
                if ((existing - point).sqrMagnitude <= sqr)
                {
                    return;
                }
            }

            points.Add(point);
        }

        private static float BoundsVolume(Bounds bounds)
        {
            var size = bounds.size;
            return Mathf.Max(Epsilon, Mathf.Abs(size.x * size.y * size.z));
        }

        private static float BoundsGap(Bounds left, Bounds right)
        {
            var x = Mathf.Max(0f, Mathf.Max(left.min.x - right.max.x, right.min.x - left.max.x));
            var y = Mathf.Max(0f, Mathf.Max(left.min.y - right.max.y, right.min.y - left.max.y));
            var z = Mathf.Max(0f, Mathf.Max(left.min.z - right.max.z, right.min.z - left.max.z));
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        private static Vector3 PowerIterate(Matrix3 matrix, Vector3 seed, Vector3 reject)
        {
            var value = seed.sqrMagnitude <= Epsilon ? Vector3.right : seed.normalized;
            for (var i = 0; i < 10; i++)
            {
                value = matrix.Multiply(value);
                if (reject.sqrMagnitude > 0.5f)
                {
                    value -= reject.normalized * Vector3.Dot(value, reject.normalized);
                }

                if (value.sqrMagnitude <= Epsilon)
                {
                    return Vector3.zero;
                }

                value.Normalize();
            }

            return value;
        }

        private static Vector3 MostOrthogonalAxis(Vector3 axis)
        {
            axis = axis.sqrMagnitude <= Epsilon ? Vector3.right : axis.normalized;
            var absX = Mathf.Abs(axis.x);
            var absY = Mathf.Abs(axis.y);
            var absZ = Mathf.Abs(axis.z);
            var seed = absX <= absY && absX <= absZ
                ? Vector3.right
                : absY <= absZ ? Vector3.up : Vector3.forward;
            return (seed - axis * Vector3.Dot(seed, axis)).normalized;
        }

        private static string BuildCacheKey(Vector3[] vertices, int[] triangles, SolverOptions options)
        {
            return $"{RealPhysicsPlacerColliderDecomposer.CalculateSourceMeshHash(vertices, triangles)}:{options.CacheKey}";
        }

        private static bool TryReadCache(string key, string sourceName, SolverOptions options, out List<RealPhysicsPlacerColliderDecomposer.Piece> pieces)
        {
            pieces = null;
            if (!PointCloudCache.TryGetValue(key, out var cachedPointClouds))
            {
                return false;
            }

            pieces = BuildPiecesFromPointClouds(ClonePointClouds(cachedPointClouds), sourceName, options);
            return pieces.Count > 0;
        }

        private static void WriteCache(string key, List<List<Vector3>> pointClouds)
        {
            if (!PointCloudCache.ContainsKey(key))
            {
                CacheOrder.Enqueue(key);
            }

            PointCloudCache[key] = ClonePointClouds(pointClouds);
            while (CacheOrder.Count > MaxCacheEntries)
            {
                PointCloudCache.Remove(CacheOrder.Dequeue());
            }
        }

        private static List<List<Vector3>> ClonePointClouds(List<List<Vector3>> source)
        {
            return source.Select(points => new List<Vector3>(points)).ToList();
        }

        private sealed class NormalizedMesh
        {
            internal readonly List<Vector3> Vertices;
            internal readonly List<TriangleInfo> Triangles;
            internal readonly Vector3 OriginalCenter;
            internal readonly float OriginalScale;
            internal readonly float RestoreEpsilon;
            internal List<int>[] Neighbors;

            internal NormalizedMesh(List<Vector3> vertices, List<TriangleInfo> triangles, Vector3 originalCenter, float originalScale)
            {
                Vertices = vertices;
                Triangles = triangles;
                OriginalCenter = originalCenter;
                OriginalScale = originalScale;
                RestoreEpsilon = Mathf.Max(0.00001f, originalScale * 0.00001f);
            }

            internal Vector3 Restore(Vector3 normalized)
            {
                return normalized * OriginalScale + OriginalCenter;
            }
        }

        private sealed class Cluster
        {
            internal readonly List<int> Triangles = new List<int>();
            internal readonly List<int> Samples = new List<int>();
            internal readonly List<int> Voxels = new List<int>();
            internal Bounds Bounds;
            internal float Score;
            internal bool Terminal;
            internal bool Closed;
        }

        private sealed class TriangleInfo
        {
            internal readonly int A;
            internal readonly int B;
            internal readonly int C;
            internal readonly Vector3 Normal;
            internal readonly float Area;
            internal readonly Vector3 Center;
            internal bool HasBoundary;
            internal bool HasSharp;

            internal TriangleInfo(int a, int b, int c, Vector3 normal, float area, Vector3 center)
            {
                A = a;
                B = b;
                C = c;
                Normal = normal;
                Area = area;
                Center = center;
            }
        }

        private readonly struct ClusterStats
        {
            internal readonly Vector3 Normal;
            internal readonly Vector3 Center;
            internal readonly float Area;
            internal readonly float OpenFeaturePressure;

            internal ClusterStats(Vector3 normal, Vector3 center, float area, float openFeaturePressure)
            {
                Normal = normal;
                Center = center;
                Area = area;
                OpenFeaturePressure = openFeaturePressure;
            }
        }

        private readonly struct SamplePoint
        {
            internal readonly Vector3 Point;
            internal readonly int TriangleIndex;
            internal readonly bool Feature;

            internal SamplePoint(Vector3 point, int triangleIndex, bool feature)
            {
                Point = point;
                TriangleIndex = triangleIndex;
                Feature = feature;
            }
        }

        private readonly struct VoxelCell
        {
            internal readonly VoxelKey Key;
            internal readonly Vector3 Center;
            internal readonly int TriangleIndex;

            internal VoxelCell(VoxelKey key, Vector3 center, int triangleIndex)
            {
                Key = key;
                Center = center;
                TriangleIndex = triangleIndex;
            }
        }

        private readonly struct SolverOptions
        {
            internal readonly int MaxHulls;
            internal readonly int HullDetail;
            internal readonly int VoxelResolution;
            internal readonly int MaxSamples;
            internal readonly int MaxSplitCandidates;
            internal readonly int TargetHullCount;
            internal readonly float ShapeAccuracy;
            internal readonly bool MergePass;
            internal readonly float VoxelCellSize;
            internal readonly float VoxelCellVolume;
            internal readonly string CacheKey;

            private SolverOptions(
                int maxHulls,
                int hullDetail,
                int voxelResolution,
                int maxSamples,
                int maxSplitCandidates,
                int targetHullCount,
                float shapeAccuracy,
                bool mergePass)
            {
                MaxHulls = maxHulls;
                HullDetail = hullDetail;
                VoxelResolution = voxelResolution;
                MaxSamples = maxSamples;
                MaxSplitCandidates = maxSplitCandidates;
                TargetHullCount = Mathf.Clamp(targetHullCount, 1, maxHulls);
                ShapeAccuracy = shapeAccuracy;
                MergePass = mergePass;
                VoxelCellSize = 1f / Mathf.Max(1, voxelResolution);
                VoxelCellVolume = VoxelCellSize * VoxelCellSize * VoxelCellSize;
                CacheKey = $"{maxHulls}:{hullDetail}:{voxelResolution}:{maxSamples}:{maxSplitCandidates}:{TargetHullCount}:{shapeAccuracy:0.0000}:{mergePass}";
            }

            internal static SolverOptions FromSettings(RealPhysicsPlacerSettings settings)
            {
                var quality = settings.bakerRealHullsQuality;
                var qualitySampleBoost = quality switch
                {
                    RealPhysicsPlacerRealHullsQuality.Fast => 0.7f,
                    RealPhysicsPlacerRealHullsQuality.Accurate => 1.2f,
                    RealPhysicsPlacerRealHullsQuality.Maximum => 1.45f,
                    _ => 1f
                };
                var qualityCandidateBoost = quality switch
                {
                    RealPhysicsPlacerRealHullsQuality.Fast => 0.75f,
                    RealPhysicsPlacerRealHullsQuality.Accurate => 1.25f,
                    RealPhysicsPlacerRealHullsQuality.Maximum => 1.5f,
                    _ => 1f
                };

                return new SolverOptions(
                    Mathf.Clamp(settings.bakerMaxHulls <= 0 ? 24 : settings.bakerMaxHulls, 1, 512),
                    Mathf.Clamp(settings.bakerMaxHullVertices <= 0 ? 128 : settings.bakerMaxHullVertices, 8, 255),
                    Mathf.Clamp(settings.bakerRealHullsVoxelResolution <= 0 ? 48 : settings.bakerRealHullsVoxelResolution, 8, 256),
                    Mathf.Clamp(Mathf.RoundToInt((settings.bakerRealHullsSurfaceSamples <= 0 ? 1600 : settings.bakerRealHullsSurfaceSamples) * qualitySampleBoost), 64, 80000),
                    Mathf.Clamp(Mathf.RoundToInt((settings.bakerRealHullsMaxSplitCandidates <= 0 ? 64 : settings.bakerRealHullsMaxSplitCandidates) * qualityCandidateBoost), 4, 1536),
                    Mathf.Clamp(settings.bakerMaxHulls <= 0 ? 24 : settings.bakerMaxHulls, 1, 512),
                    Mathf.Clamp(settings.bakerRealHullsShapeAccuracy <= 0f ? 0.04f : settings.bakerRealHullsShapeAccuracy, 0.001f, 0.5f),
                    settings.bakerRealHullsMergePass);
            }
        }

        private readonly struct VoxelKey : IEquatable<VoxelKey>
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Z;

            internal VoxelKey(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(VoxelKey other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is VoxelKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = X;
                    hash = hash * 397 ^ Y;
                    hash = hash * 397 ^ Z;
                    return hash;
                }
            }
        }

        private readonly struct PointKey : IEquatable<PointKey>
        {
            private readonly int x;
            private readonly int y;
            private readonly int z;

            internal PointKey(Vector3 point, float scale)
            {
                x = Mathf.RoundToInt(point.x * scale);
                y = Mathf.RoundToInt(point.y * scale);
                z = Mathf.RoundToInt(point.z * scale);
            }

            public bool Equals(PointKey other)
            {
                return x == other.x && y == other.y && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is PointKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = x;
                    hash = hash * 397 ^ y;
                    hash = hash * 397 ^ z;
                    return hash;
                }
            }
        }

        private struct Matrix3
        {
            internal float XX;
            internal float XY;
            internal float XZ;
            internal float YY;
            internal float YZ;
            internal float ZZ;

            internal Vector3 Multiply(Vector3 value)
            {
                return new Vector3(
                    XX * value.x + XY * value.y + XZ * value.z,
                    XY * value.x + YY * value.y + YZ * value.z,
                    XZ * value.x + YZ * value.y + ZZ * value.z);
            }
        }
    }
}
