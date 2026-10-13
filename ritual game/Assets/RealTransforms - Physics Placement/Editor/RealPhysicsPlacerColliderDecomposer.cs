using System;
using System.Collections.Generic;
using System.Linq;
using RealPhysicsPlacer;
using UnityEngine;

namespace RealPhysicsPlacer.EditorTools
{
    internal static class RealPhysicsPlacerColliderDecomposer
    {
        // Converts a source render mesh into Unity-safe convex collider pieces.
        // The selected backend creates candidate hulls; shared cleanup then keeps
        // the result inside Unity's convex MeshCollider limits.
        private const int UnityConvexTriangleLimit = 255;
        private const int CookMatchMaxProbeVertices = 72;
        private const int CookMatchMaxProbeTriangles = 72;
        private const int CookMatchMaxProbeCount = 6;
        private const double CookMatchProbeBudgetMilliseconds = 650d;
        private const int MultiHullFibonacciDirections = 32;
        private const int MultiHullNormalDirectionPairs = 12;
        private const int SmartHullFeatureMinimumTriangles = 24;
        private const int SmartHullFeatureGroupHardLimit = 192;
        private const float SmartHullFeatureAngleDegrees = 32f;
        private const float SmartHullFeaturePlaneTolerance = 0.015f;
        private const float SmartHullBoundaryFeatureRatio = 0.02f;
        private const float SmartHullSharpFeatureRatio = 0.08f;
        private const float HullEpsilon = 0.00001f;

        internal sealed class Piece
        {
            internal RealPhysicsPlacerBakedColliderShape Shape;
            internal PrimitiveColliderKind ColliderKind = PrimitiveColliderKind.Mesh;
            internal Vector3 Center = Vector3.zero;
            internal Quaternion Rotation = Quaternion.identity;
            internal Vector3 Size = Vector3.one;
            internal Mesh Mesh;
        }

        internal enum PrimitiveColliderKind
        {
            Mesh,
            Box
        }

        private readonly struct HullPlane
        {
            internal readonly Vector3 Normal;
            internal readonly float Distance;

            internal HullPlane(Vector3 normal, float distance)
            {
                Normal = normal;
                Distance = distance;
            }
        }

        private readonly struct FacePoint
        {
            internal readonly int Index;
            internal readonly Vector2 Point;

            internal FacePoint(int index, Vector2 point)
            {
                Index = index;
                Point = point;
            }
        }

        private readonly struct ProjectedPoint
        {
            internal readonly Vector2 Point;

            internal ProjectedPoint(Vector2 point)
            {
                Point = point;
            }
        }

        private readonly struct TriangleSample
        {
            internal readonly int TriangleStart;
            internal readonly Vector3 Center;
            internal readonly ulong SortKey;

            internal TriangleSample(int triangleStart, Vector3 center, ulong sortKey)
            {
                TriangleStart = triangleStart;
                Center = center;
                SortKey = sortKey;
            }
        }

        private readonly struct TriangleFeature
        {
            internal readonly Vector3 Normal;
            internal readonly Vector3 Center;
            internal readonly float Area;

            internal TriangleFeature(Vector3 normal, Vector3 center, float area)
            {
                Normal = normal;
                Center = center;
                Area = area;
            }
        }

        private readonly struct SmartSplitResult
        {
            internal readonly List<int> Left;
            internal readonly List<int> Right;
            internal readonly float Score;

            internal SmartSplitResult(List<int> left, List<int> right, float score)
            {
                Left = left;
                Right = right;
                Score = score;
            }
        }

        internal static List<Piece> Decompose(Mesh sourceMesh, RealPhysicsPlacerSettings settings, string sourceName)
        {
            if (sourceMesh == null || settings == null)
            {
                return new List<Piece>();
            }

            var vertices = sourceMesh.vertices;
            var triangles = sourceMesh.triangles;
            if (vertices == null || vertices.Length < 3 || triangles == null || triangles.Length < 3)
            {
                return new List<Piece>();
            }

            // Backend selection stays here so preview and baking use the exact
            // same collider generation path.
            return settings.bakerColliderShape switch
            {
                RealPhysicsPlacerBakedColliderShape.MultiHull => CreateMultiHullPieces(vertices, triangles, settings, sourceName),
                RealPhysicsPlacerBakedColliderShape.RealHulls => CreateRealHullsPieces(vertices, triangles, settings, sourceName),
                _ => CreateRealHullsPieces(vertices, triangles, settings, sourceName)
            };
        }

        internal static string CalculateSourceMeshHash(Mesh sourceMesh)
        {
            if (sourceMesh == null)
            {
                return string.Empty;
            }

            return CalculateSourceMeshHash(sourceMesh.vertices, sourceMesh.triangles);
        }

        internal static string CalculateSourceMeshHash(Vector3[] vertices, int[] triangles)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                void Add(uint value)
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }

                Add((uint)(vertices?.Length ?? 0));
                Add((uint)(triangles?.Length ?? 0));
                if (vertices != null)
                {
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        Add((uint)Mathf.RoundToInt(vertices[i].x * 100000f));
                        Add((uint)Mathf.RoundToInt(vertices[i].y * 100000f));
                        Add((uint)Mathf.RoundToInt(vertices[i].z * 100000f));
                    }
                }

                if (triangles != null)
                {
                    for (var i = 0; i < triangles.Length; i++)
                    {
                        Add((uint)triangles[i]);
                    }
                }

                return hash.ToString("X16");
            }
        }

        private static List<Piece> CreateRealHullsPieces(Vector3[] vertices, int[] triangles, RealPhysicsPlacerSettings settings, string sourceName)
        {
            var pieces = RealPhysicsPlacerRealHullsSolver.Decompose(vertices, triangles, settings, sourceName);
            if (pieces == null || pieces.Count == 0)
            {
                Debug.LogWarning($"RealTransforms Real Hulls could not solve {sourceName}. Falling back to Smart Hulls.");
                var fallback = CreateMultiHullPieces(vertices, triangles, settings, sourceName);
                return fallback.Count > 0
                    ? fallback
                    : CreateFallbackSingleHullPieces(vertices, sourceName, RealPhysicsPlacerBakedColliderShape.RealHulls);
            }

            FinalizeMeshPieces(pieces, settings, sourceName);
            if (pieces.Count == 0)
            {
                return CreateFallbackSingleHullPieces(vertices, sourceName, RealPhysicsPlacerBakedColliderShape.RealHulls);
            }

            return pieces;
        }

        private static List<Piece> CreateMultiHullPieces(Vector3[] vertices, int[] triangles, RealPhysicsPlacerSettings settings, string sourceName)
        {
            var maxHulls = Mathf.Clamp(settings.bakerMaxHulls <= 0 ? 24 : settings.bakerMaxHulls, 1, 512);
            var maxTrianglesPerHull = Mathf.Clamp(settings.bakerMaxHullVertices <= 0 ? 128 : settings.bakerMaxHullVertices, 8, 255);
            var groups = BuildSmartHullV2Groups(vertices, triangles, maxHulls, maxTrianglesPerHull);
            if (groups.Count == 0)
            {
                return CreateFallbackSingleHullPieces(vertices, sourceName, RealPhysicsPlacerBakedColliderShape.MultiHull);
            }

            SplitGroupsToBudget(vertices, triangles, groups, settings, maxHulls, maxTrianglesPerHull);
            MergeGroupsToBudget(vertices, triangles, groups, maxHulls);

            var pieces = new List<Piece>(Mathf.Min(groups.Count, maxHulls));
            for (var i = 0; i < groups.Count && pieces.Count < maxHulls; i++)
            {
                if (RealPhysicsPlacerColliderBakeBudget.IsExpired)
                {
                    break;
                }

                var piece = CreateMultiHullPiece(vertices, triangles, groups[i], $"{sourceName}_RealTransforms_SmartHull_{i + 1:000}", maxTrianglesPerHull);
                if (piece == null)
                {
                    continue;
                }

                piece.Shape = RealPhysicsPlacerBakedColliderShape.MultiHull;
                pieces.Add(piece);
            }

            FinalizeMeshPieces(pieces, settings, sourceName);
            if (pieces.Count == 0)
            {
                return CreateFallbackSingleHullPieces(vertices, sourceName, RealPhysicsPlacerBakedColliderShape.MultiHull);
            }

            return pieces;
        }

        private static Mesh CreateComponentMesh(Vector3[] vertices, int[] triangles, List<int> group, string meshName)
        {
            var sourceToLocal = new Dictionary<int, int>();
            var localVertices = new List<Vector3>();
            var localTriangles = new List<int>(group.Count * 3);
            foreach (var triangle in group)
            {
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                for (var corner = 0; corner < 3; corner++)
                {
                    var sourceIndex = triangles[triangle + corner];
                    if (sourceIndex < 0 || sourceIndex >= vertices.Length)
                    {
                        continue;
                    }

                    if (!sourceToLocal.TryGetValue(sourceIndex, out var localIndex))
                    {
                        localIndex = localVertices.Count;
                        sourceToLocal[sourceIndex] = localIndex;
                        localVertices.Add(vertices[sourceIndex]);
                    }

                    localTriangles.Add(localIndex);
                }
            }

            if (localVertices.Count < 4 || localTriangles.Count < 12)
            {
                return null;
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = localVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(localVertices);
            mesh.SetTriangles(localTriangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void ApplyHullPadding(List<Piece> pieces, float padding)
        {
            padding = Mathf.Clamp(padding, -0.03f, 0.03f);
            if (Mathf.Abs(padding) <= 0.000001f)
            {
                return;
            }

            foreach (var piece in pieces)
            {
                var mesh = piece?.Mesh;
                if (mesh == null)
                {
                    continue;
                }

                var vertices = mesh.vertices;
                if (vertices == null || vertices.Length == 0)
                {
                    continue;
                }

                var center = mesh.bounds.center;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var offset = vertices[i] - center;
                    var magnitude = offset.magnitude;
                    if (magnitude <= 0.000001f)
                    {
                        continue;
                    }

                    var displacement = padding < 0f
                        ? Mathf.Max(padding, -magnitude * 0.45f)
                        : padding;
                    vertices[i] += offset / magnitude * displacement;
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
            }
        }

        internal static void FinalizeMeshPieces(List<Piece> pieces, RealPhysicsPlacerSettings settings, string sourceName)
        {
            if (pieces == null || settings == null)
            {
                return;
            }

            var timer = RealPhysicsPlacerTimingLog.Start();
            if (settings.bakerMergeSmallHulls)
            {
                MergeSmallMeshPieces(pieces, settings, sourceName);
                RealPhysicsPlacerTimingLog.Step($"Finalize '{sourceName}' connect small hulls ({pieces.Count})", timer);
            }

            ApplyHullPadding(pieces, settings.bakerColliderPadding);
            RealPhysicsPlacerTimingLog.Step($"Finalize '{sourceName}' padding", timer);
            ApplyCookMatchRefinement(pieces, settings, sourceName);
            RepairUnsafeMeshPieces(pieces, sourceName);
            RealPhysicsPlacerTimingLog.Total($"Finalize '{sourceName}' cook match ({pieces.Count})", timer);
        }

        private static void RepairUnsafeMeshPieces(List<Piece> pieces, string sourceName)
        {
            if (pieces == null)
            {
                return;
            }

            for (var i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                if (piece == null || piece.ColliderKind != PrimitiveColliderKind.Mesh || piece.Mesh == null)
                {
                    pieces.RemoveAt(i);
                    continue;
                }

                if (IsUnitySafeHullMesh(piece.Mesh))
                {
                    continue;
                }

                var sourcePoints = piece.Mesh.vertices?.ToList() ?? new List<Vector3>();
                if (TryBuildOrientedBoxHullMesh(sourcePoints, $"{sourceName}_RealTransforms_RepairedHull_{i + 1:000}", out var repaired))
                {
                    UnityEngine.Object.DestroyImmediate(piece.Mesh);
                    piece.Mesh = repaired;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(piece.Mesh);
                    pieces.RemoveAt(i);
                }
            }
        }

        private static List<Piece> CreateFallbackSingleHullPieces(Vector3[] vertices, string sourceName, RealPhysicsPlacerBakedColliderShape shape)
        {
            if (vertices == null || vertices.Length == 0)
            {
                return new List<Piece>();
            }

            if (!TryBuildOrientedBoxHullMesh(vertices.ToList(), $"{sourceName}_RealTransforms_FallbackHull_001", out var mesh))
            {
                return new List<Piece>();
            }

            return new List<Piece>
            {
                new Piece
                {
                    Shape = shape,
                    ColliderKind = PrimitiveColliderKind.Mesh,
                    Mesh = mesh
                }
            };
        }

        private static void ApplyCookMatchRefinement(List<Piece> pieces, RealPhysicsPlacerSettings settings, string sourceName)
        {
            if (pieces == null
                || settings == null
                || !settings.bakerCookMatch
                || pieces.Count < 1)
            {
                return;
            }

            var maxPieces = Mathf.Clamp(settings.bakerMaxHulls <= 0 ? 12 : settings.bakerMaxHulls, 1, 512);
            var tolerance = Mathf.Clamp(settings.bakerCookMatchTolerance <= 0f ? 0.035f : settings.bakerCookMatchTolerance, 0.001f, 0.25f);
            var splitIndex = 1;
            // Unity's convex-collider cooking can change support points. Splitting
            // the worst mismatches keeps the final cooked collider close to the
            // preview while respecting the hull budget.
            while (pieces.Count < maxPieces && TryFindWorstCookMismatch(pieces, tolerance, out var pieceIndex, out var splitDirection))
            {
                if (!TrySplitMeshPiece(pieces[pieceIndex], splitDirection, $"{sourceName}_RealTransforms_CookMatch_{splitIndex++:000}", out var left, out var right))
                {
                    break;
                }

                pieces[pieceIndex] = left;
                pieces.Insert(pieceIndex + 1, right);
            }
        }

        private static bool TryFindWorstCookMismatch(List<Piece> pieces, float tolerance, out int pieceIndex, out Vector3 splitDirection)
        {
            pieceIndex = -1;
            splitDirection = Vector3.right;
            var worstDeviation = tolerance;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var probes = 0;
            for (var i = 0; i < pieces.Count; i++)
            {
                if (probes >= CookMatchMaxProbeCount || timer.Elapsed.TotalMilliseconds > CookMatchProbeBudgetMilliseconds)
                {
                    break;
                }

                var piece = pieces[i];
                if (piece == null
                    || piece.ColliderKind != PrimitiveColliderKind.Mesh
                    || piece.Mesh == null
                    || GetMeshTriangleCount(piece.Mesh) < 4
                    || !ShouldProbeCookMatch(piece.Mesh))
                {
                    continue;
                }

                probes++;
                if (!TryMeasureCookMismatch(piece.Mesh, out var deviation, out var direction))
                {
                    continue;
                }

                if (deviation <= worstDeviation)
                {
                    continue;
                }

                worstDeviation = deviation;
                splitDirection = direction;
                pieceIndex = i;
            }

            return pieceIndex >= 0;
        }

        private static bool TryMeasureCookMismatch(Mesh mesh, out float maxDeviation, out Vector3 worstDirection)
        {
            maxDeviation = 0f;
            worstDirection = Vector3.right;
            if (mesh == null || mesh.vertexCount < 4 || GetMeshTriangleCount(mesh) < 4 || !ShouldProbeCookMatch(mesh))
            {
                return false;
            }

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length < 4)
            {
                return false;
            }

            var temp = new GameObject("__RealTransforms_CookProbe")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                var collider = temp.AddComponent<MeshCollider>();
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning
                    | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
                collider.sharedMesh = mesh;
                collider.convex = true;
                if (!collider.convex)
                {
                    return false;
                }

                Physics.SyncTransforms();
                var bounds = mesh.bounds;
                var center = bounds.center;
                var radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                var probeDistance = radius * 4f + 2f;
                foreach (var rawDirection in BuildSupportDirections())
                {
                    if (rawDirection.sqrMagnitude <= HullEpsilon * HullEpsilon)
                    {
                        continue;
                    }

                    var direction = rawDirection.normalized;
                    var expected = float.NegativeInfinity;
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        expected = Mathf.Max(expected, Vector3.Dot(vertices[i], direction));
                    }

                    if (float.IsNegativeInfinity(expected))
                    {
                        continue;
                    }

                    var probe = center + direction * probeDistance;
                    var cookedPoint = collider.ClosestPoint(probe);
                    var cooked = Vector3.Dot(cookedPoint, direction);
                    var deviation = Mathf.Abs(cooked - expected);
                    if (deviation <= maxDeviation)
                    {
                        continue;
                    }

                    maxDeviation = deviation;
                    worstDirection = direction;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private sealed class QuickHullFace
        {
            internal readonly int A;
            internal readonly int B;
            internal readonly int C;
            internal readonly Vector3 Normal;
            internal readonly float Distance;

            internal QuickHullFace(int a, int b, int c, Vector3 normal, float distance)
            {
                A = a;
                B = b;
                C = c;
                Normal = normal;
                Distance = distance;
            }

            internal float SignedDistance(Vector3 point)
            {
                return Vector3.Dot(Normal, point) - Distance;
            }
        }

        private struct QuickHullEdge
        {
            internal readonly int A;
            internal readonly int B;
            internal int Count;

            internal QuickHullEdge(int a, int b)
            {
                A = a;
                B = b;
                Count = 1;
            }
        }

        private static bool ShouldProbeCookMatch(Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            // MeshCollider.convex cooking can become the dominant bake cost on
            // complex generated hulls. Cook Match is therefore limited to small
            // hulls where the probe is predictable and useful.
            return mesh.vertexCount <= CookMatchMaxProbeVertices
                && GetMeshTriangleCount(mesh) <= CookMatchMaxProbeTriangles;
        }

        private static bool TrySplitMeshPiece(Piece piece, Vector3 splitDirection, string namePrefix, out Piece left, out Piece right)
        {
            left = null;
            right = null;
            var mesh = piece?.Mesh;
            if (mesh == null)
            {
                return false;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || triangles == null || vertices.Length < 4 || triangles.Length < 24)
            {
                return false;
            }

            var groups = Enumerable.Range(0, triangles.Length / 3)
                .Select(index => index * 3)
                .ToList();
            if (!TrySplitGroup(vertices, triangles, groups, splitDirection, out var leftGroup, out var rightGroup))
            {
                var axis = LongestAxis(mesh.bounds.size);
                splitDirection = axis == 0
                    ? Vector3.right
                    : axis == 1 ? Vector3.up : Vector3.forward;
                if (!TrySplitGroup(vertices, triangles, groups, splitDirection, out leftGroup, out rightGroup))
                {
                    return false;
                }
            }

            var leftMesh = CreateComponentMesh(vertices, triangles, leftGroup, $"{namePrefix}_A");
            var rightMesh = CreateComponentMesh(vertices, triangles, rightGroup, $"{namePrefix}_B");
            if (leftMesh == null || rightMesh == null)
            {
                return false;
            }

            left = new Piece
            {
                Shape = piece.Shape,
                ColliderKind = PrimitiveColliderKind.Mesh,
                Mesh = leftMesh
            };
            right = new Piece
            {
                Shape = piece.Shape,
                ColliderKind = PrimitiveColliderKind.Mesh,
                Mesh = rightMesh
            };
            return true;
        }

        private static bool TrySplitGroup(Vector3[] vertices, int[] triangles, List<int> group, Vector3 splitDirection, out List<int> left, out List<int> right)
        {
            left = null;
            right = null;
            if (group == null || group.Count < 2)
            {
                return false;
            }

            if (splitDirection.sqrMagnitude <= HullEpsilon * HullEpsilon)
            {
                splitDirection = Vector3.right;
            }

            splitDirection.Normalize();
            var sorted = group
                .OrderBy(triangle => Vector3.Dot(TriangleCenter(vertices, triangles, triangle), splitDirection))
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

        private static void MergeSmallMeshPieces(List<Piece> pieces, RealPhysicsPlacerSettings settings, string sourceName)
        {
            var triangleLimit = Mathf.Clamp(settings.bakerSmallHullMergeTriangleLimit <= 0 ? 255 : settings.bakerSmallHullMergeTriangleLimit, 16, 255);
            var expansionLimit = Mathf.Clamp(settings.bakerSmallHullMergeExpansion <= 0f ? 1.35f : settings.bakerSmallHullMergeExpansion, 1f, 3f);
            var mergeIndex = 1;
            while (TryFindSmallHullMerge(pieces, triangleLimit, expansionLimit, out var left, out var right))
            {
                var merged = CreateMergedMeshPiece(
                    pieces[left],
                    pieces[right],
                    $"{sourceName}_RealTransforms_MergedHull_{mergeIndex++:000}");
                if (merged == null)
                {
                    break;
                }

                pieces[left] = merged;
                pieces.RemoveAt(right);
            }
        }

        private static bool TryFindSmallHullMerge(List<Piece> pieces, int triangleLimit, float expansionLimit, out int leftIndex, out int rightIndex)
        {
            leftIndex = -1;
            rightIndex = -1;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < pieces.Count; i++)
            {
                for (var j = i + 1; j < pieces.Count; j++)
                {
                    if (!CanMergeSmallHullPair(pieces[i], pieces[j], triangleLimit, expansionLimit, out var score))
                    {
                        continue;
                    }

                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    leftIndex = i;
                    rightIndex = j;
                }
            }

            return leftIndex >= 0 && rightIndex > leftIndex;
        }

        private static bool CanMergeSmallHullPair(Piece left, Piece right, int triangleLimit, float expansionLimit, out float score)
        {
            score = 0f;
            if (left == null
                || right == null
                || left.ColliderKind != PrimitiveColliderKind.Mesh
                || right.ColliderKind != PrimitiveColliderKind.Mesh
                || left.Mesh == null
                || right.Mesh == null)
            {
                return false;
            }

            var triangleCount = GetMeshTriangleCount(left.Mesh) + GetMeshTriangleCount(right.Mesh);
            if (triangleCount <= 0 || triangleCount > triangleLimit)
            {
                return false;
            }

            var leftBounds = left.Mesh.bounds;
            var rightBounds = right.Mesh.bounds;
            var combinedBounds = leftBounds;
            combinedBounds.Encapsulate(rightBounds);

            var combinedVolume = BoundsVolume(combinedBounds);
            var sourceVolume = BoundsVolume(leftBounds) + BoundsVolume(rightBounds);
            var expansion = combinedVolume / Mathf.Max(0.000001f, sourceVolume);
            if (expansion > expansionLimit)
            {
                return false;
            }

            var gap = BoundsGap(leftBounds, rightBounds);
            var diagonal = Mathf.Max(0.0001f, combinedBounds.size.magnitude);
            if (gap > diagonal * 0.075f)
            {
                return false;
            }

            score = expansion + gap / diagonal + triangleCount / (float)triangleLimit * 0.05f;
            return true;
        }

        private static Piece CreateMergedMeshPiece(Piece left, Piece right, string meshName)
        {
            var leftMesh = left.Mesh;
            var rightMesh = right.Mesh;
            var leftVertices = leftMesh.vertices;
            var rightVertices = rightMesh.vertices;
            var leftTriangles = leftMesh.triangles;
            var rightTriangles = rightMesh.triangles;
            if (leftVertices == null || rightVertices == null || leftTriangles == null || rightTriangles == null)
            {
                return null;
            }

            var vertices = new List<Vector3>(leftVertices.Length + rightVertices.Length);
            vertices.AddRange(leftVertices);
            vertices.AddRange(rightVertices);

            var triangles = new List<int>(leftTriangles.Length + rightTriangles.Length);
            triangles.AddRange(leftTriangles);
            var offset = leftVertices.Length;
            for (var i = 0; i < rightTriangles.Length; i++)
            {
                triangles.Add(rightTriangles[i] + offset);
            }

            if (vertices.Count < 4 || triangles.Count < 12)
            {
                return null;
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.None
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return new Piece
            {
                Shape = RealPhysicsPlacerBakedColliderShape.ConvexHull,
                ColliderKind = PrimitiveColliderKind.Mesh,
                Mesh = mesh
            };
        }

        private static int GetMeshTriangleCount(Mesh mesh)
        {
            return mesh == null || mesh.triangles == null ? 0 : mesh.triangles.Length / 3;
        }

        private static float BoundsVolume(Bounds bounds)
        {
            var size = bounds.size;
            return Mathf.Max(0.000001f, size.x * size.y * size.z);
        }

        private static float BoundsGap(Bounds left, Bounds right)
        {
            var x = Mathf.Max(0f, Mathf.Max(left.min.x - right.max.x, right.min.x - left.max.x));
            var y = Mathf.Max(0f, Mathf.Max(left.min.y - right.max.y, right.min.y - left.max.y));
            var z = Mathf.Max(0f, Mathf.Max(left.min.z - right.max.z, right.min.z - left.max.z));
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        private static List<List<int>> BuildSmartHullV2Groups(Vector3[] vertices, int[] triangles, int maxHulls, int maxTrianglesPerHull)
        {
            var connectedGroups = BuildConnectedTriangleGroups(triangles);
            if (vertices == null || triangles == null || connectedGroups.Count == 0)
            {
                return connectedGroups;
            }

            var triangleCount = triangles.Length / 3;
            var features = BuildTriangleFeatures(vertices, triangles);
            var edgeNeighbors = BuildEdgeTriangleNeighbors(triangles, triangleCount);
            var groups = new List<List<int>>(Mathf.Max(connectedGroups.Count, maxHulls));

            foreach (var connectedGroup in connectedGroups)
            {
                if (!ShouldUseSmartHullV2FeatureGrouping(features, edgeNeighbors, triangles, connectedGroup, maxTrianglesPerHull))
                {
                    groups.Add(connectedGroup);
                    continue;
                }

                var featureGroups = BuildPlanarFeatureGroupsForComponent(vertices, triangles, connectedGroup, features, edgeNeighbors);
                var featureGroupLimit = Mathf.Clamp(maxHulls * 12, 32, SmartHullFeatureGroupHardLimit);
                if (featureGroups.Count <= 1 || featureGroups.Count > featureGroupLimit)
                {
                    groups.Add(connectedGroup);
                    continue;
                }

                groups.AddRange(featureGroups);
            }

            return groups.Count == 0
                ? connectedGroups
                : groups
                    .Where(group => group != null && group.Count > 0)
                    .OrderByDescending(group => group.Count)
                    .ToList();
        }

        private static bool ShouldUseSmartHullV2FeatureGrouping(
            TriangleFeature[] features,
            List<int>[] edgeNeighbors,
            int[] triangles,
            List<int> component,
            int maxTrianglesPerHull)
        {
            if (component == null || component.Count < SmartHullFeatureMinimumTriangles)
            {
                return false;
            }

            var boundaryRatio = CalculateBoundaryEdgeRatio(triangles, component);
            if (boundaryRatio >= SmartHullBoundaryFeatureRatio)
            {
                return true;
            }

            var sharpRatio = CalculateSharpEdgeRatio(features, edgeNeighbors, component);
            if (sharpRatio >= SmartHullSharpFeatureRatio)
            {
                return true;
            }

            // Closed hard-surface meshes can still need splitting, but smooth
            // convex meshes should stay as one hull. This gate only opens when
            // there are enough sharp edges to justify spending more hulls.
            var detailPressure = component.Count > Mathf.Max(SmartHullFeatureMinimumTriangles, maxTrianglesPerHull * 2);
            return detailPressure && sharpRatio >= SmartHullSharpFeatureRatio * 0.35f;
        }

        private static List<List<int>> BuildPlanarFeatureGroupsForComponent(
            Vector3[] vertices,
            int[] triangles,
            List<int> component,
            TriangleFeature[] features,
            List<int>[] edgeNeighbors)
        {
            var componentTriangles = new HashSet<int>();
            foreach (var triangleStart in component)
            {
                var triangleIndex = triangleStart / 3;
                if (triangleIndex >= 0 && triangleIndex < features.Length)
                {
                    componentTriangles.Add(triangleIndex);
                }
            }

            if (componentTriangles.Count == 0)
            {
                return new List<List<int>>();
            }

            var bounds = CalculateBounds(vertices, triangles, component);
            var planeTolerance = Mathf.Max(HullEpsilon * 50f, bounds.size.magnitude * SmartHullFeaturePlaneTolerance);
            var normalDotThreshold = Mathf.Cos(SmartHullFeatureAngleDegrees * Mathf.Deg2Rad);
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            var groups = new List<List<int>>();

            foreach (var triangleStart in component)
            {
                var seedIndex = triangleStart / 3;
                if (!componentTriangles.Contains(seedIndex) || visited.Contains(seedIndex))
                {
                    continue;
                }

                var seed = features[seedIndex];
                var group = new List<int>();
                visited.Add(seedIndex);
                queue.Enqueue(seedIndex);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current * 3);
                    if (current < 0 || current >= edgeNeighbors.Length)
                    {
                        continue;
                    }

                    foreach (var neighbor in edgeNeighbors[current])
                    {
                        if (!componentTriangles.Contains(neighbor) || visited.Contains(neighbor))
                        {
                            continue;
                        }

                        if (!CanJoinFeaturePatch(seed, features[neighbor], normalDotThreshold, planeTolerance))
                        {
                            continue;
                        }

                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                if (group.Count > 0)
                {
                    groups.Add(group);
                }
            }

            return groups
                .OrderByDescending(group => group.Count)
                .ToList();
        }

        private static bool CanJoinFeaturePatch(TriangleFeature seed, TriangleFeature candidate, float normalDotThreshold, float planeTolerance)
        {
            if (seed.Area <= HullEpsilon || candidate.Area <= HullEpsilon)
            {
                return false;
            }

            var normalDot = Mathf.Abs(Vector3.Dot(seed.Normal, candidate.Normal));
            if (normalDot < normalDotThreshold)
            {
                return false;
            }

            var planeDistance = Mathf.Abs(Vector3.Dot(seed.Normal, candidate.Center - seed.Center));
            return planeDistance <= planeTolerance;
        }

        private static TriangleFeature[] BuildTriangleFeatures(Vector3[] vertices, int[] triangles)
        {
            var triangleCount = triangles.Length / 3;
            var features = new TriangleFeature[triangleCount];
            for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                var triangleStart = triangleIndex * 3;
                if (!TryGetTriangleVertices(vertices, triangles, triangleStart, out var a, out var b, out var c))
                {
                    features[triangleIndex] = new TriangleFeature(Vector3.up, Vector3.zero, 0f);
                    continue;
                }

                var cross = Vector3.Cross(b - a, c - a);
                var area = cross.magnitude * 0.5f;
                var normal = area <= HullEpsilon ? Vector3.up : cross.normalized;
                features[triangleIndex] = new TriangleFeature(normal, (a + b + c) / 3f, area);
            }

            return features;
        }

        private static List<int>[] BuildEdgeTriangleNeighbors(int[] triangles, int triangleCount)
        {
            var neighbors = new List<int>[triangleCount];
            for (var i = 0; i < neighbors.Length; i++)
            {
                neighbors[i] = new List<int>(3);
            }

            var edgeToTriangles = new Dictionary<ulong, List<int>>(triangleCount * 3);
            for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                var triangleStart = triangleIndex * 3;
                AddEdgeTriangle(edgeToTriangles, triangles[triangleStart], triangles[triangleStart + 1], triangleIndex);
                AddEdgeTriangle(edgeToTriangles, triangles[triangleStart + 1], triangles[triangleStart + 2], triangleIndex);
                AddEdgeTriangle(edgeToTriangles, triangles[triangleStart + 2], triangles[triangleStart], triangleIndex);
            }

            foreach (var linkedTriangles in edgeToTriangles.Values)
            {
                for (var i = 0; i < linkedTriangles.Count; i++)
                {
                    for (var j = i + 1; j < linkedTriangles.Count; j++)
                    {
                        AddTriangleNeighbor(neighbors, linkedTriangles[i], linkedTriangles[j]);
                    }
                }
            }

            return neighbors;
        }

        private static void AddEdgeTriangle(Dictionary<ulong, List<int>> edgeToTriangles, int a, int b, int triangleIndex)
        {
            if (a < 0 || b < 0 || a == b)
            {
                return;
            }

            var key = EdgeKey(a, b);
            if (!edgeToTriangles.TryGetValue(key, out var linkedTriangles))
            {
                linkedTriangles = new List<int>(2);
                edgeToTriangles[key] = linkedTriangles;
            }

            linkedTriangles.Add(triangleIndex);
        }

        private static void AddTriangleNeighbor(List<int>[] neighbors, int left, int right)
        {
            if (left < 0 || right < 0 || left == right || left >= neighbors.Length || right >= neighbors.Length)
            {
                return;
            }

            if (!neighbors[left].Contains(right))
            {
                neighbors[left].Add(right);
            }

            if (!neighbors[right].Contains(left))
            {
                neighbors[right].Add(left);
            }
        }

        private static float CalculateSharpEdgeRatio(TriangleFeature[] features, List<int>[] edgeNeighbors, List<int> component)
        {
            if (features == null || edgeNeighbors == null || component == null || component.Count == 0)
            {
                return 0f;
            }

            var componentTriangles = new HashSet<int>();
            foreach (var triangleStart in component)
            {
                var triangleIndex = triangleStart / 3;
                if (triangleIndex >= 0 && triangleIndex < features.Length)
                {
                    componentTriangles.Add(triangleIndex);
                }
            }

            var normalDotThreshold = Mathf.Cos(SmartHullFeatureAngleDegrees * Mathf.Deg2Rad);
            var sharedEdges = 0;
            var sharpEdges = 0;
            foreach (var triangleIndex in componentTriangles)
            {
                if (triangleIndex < 0 || triangleIndex >= edgeNeighbors.Length)
                {
                    continue;
                }

                foreach (var neighbor in edgeNeighbors[triangleIndex])
                {
                    if (neighbor <= triangleIndex || !componentTriangles.Contains(neighbor))
                    {
                        continue;
                    }

                    sharedEdges++;
                    if (features[triangleIndex].Area <= HullEpsilon || features[neighbor].Area <= HullEpsilon)
                    {
                        continue;
                    }

                    if (Mathf.Abs(Vector3.Dot(features[triangleIndex].Normal, features[neighbor].Normal)) < normalDotThreshold)
                    {
                        sharpEdges++;
                    }
                }
            }

            return sharedEdges == 0 ? 0f : sharpEdges / (float)sharedEdges;
        }

        private static List<List<int>> BuildConnectedTriangleGroups(int[] triangles)
        {
            var triangleCount = triangles.Length / 3;
            var triangleNeighbors = new List<int>[triangleCount];
            for (var i = 0; i < triangleNeighbors.Length; i++)
            {
                triangleNeighbors[i] = new List<int>(3);
            }

            var vertexToTriangles = new Dictionary<int, List<int>>();
            for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                var triangleStart = triangleIndex * 3;
                for (var corner = 0; corner < 3; corner++)
                {
                    var vertexIndex = triangles[triangleStart + corner];
                    if (!vertexToTriangles.TryGetValue(vertexIndex, out var linkedTriangles))
                    {
                        linkedTriangles = new List<int>(4);
                        vertexToTriangles[vertexIndex] = linkedTriangles;
                    }

                    linkedTriangles.Add(triangleIndex);
                }
            }

            foreach (var pair in vertexToTriangles)
            {
                var linkedTriangles = pair.Value;
                for (var i = 0; i < linkedTriangles.Count; i++)
                {
                    for (var j = i + 1; j < linkedTriangles.Count; j++)
                    {
                        var left = linkedTriangles[i];
                        var right = linkedTriangles[j];
                        if (!triangleNeighbors[left].Contains(right))
                        {
                            triangleNeighbors[left].Add(right);
                        }

                        if (!triangleNeighbors[right].Contains(left))
                        {
                            triangleNeighbors[right].Add(left);
                        }
                    }
                }
            }

            var visited = new bool[triangleCount];
            var groups = new List<List<int>>();
            var queue = new Queue<int>();
            for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
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
                    group.Add(current * 3);
                    foreach (var neighbor in triangleNeighbors[current])
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

        private static void SplitGroupsToBudget(Vector3[] vertices, int[] triangles, List<List<int>> groups, RealPhysicsPlacerSettings settings, int maxGroups, int maxTrianglesPerGroup)
        {
            while (groups.Count < maxGroups)
            {
                if (!TryFindBestSmartGroupSplit(vertices, triangles, groups, settings, maxTrianglesPerGroup, out var splitIndex, out var left, out var right))
                {
                    return;
                }

                groups[splitIndex] = left;
                groups.Add(right);
            }
        }

        private static bool TryFindBestSmartGroupSplit(
            Vector3[] vertices,
            int[] triangles,
            List<List<int>> groups,
            RealPhysicsPlacerSettings settings,
            int maxTrianglesPerGroup,
            out int splitIndex,
            out List<int> left,
            out List<int> right)
        {
            splitIndex = -1;
            left = null;
            right = null;
            var bestScore = settings != null && settings.bakerSmartHullsUseFullBudget
                ? float.NegativeInfinity
                : Mathf.Clamp(settings != null ? settings.bakerSmartHullMinSplitGain : 0.08f, 0f, 0.25f);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.Count < 2)
                {
                    continue;
                }

                if (!TryFindBestSmartSplit(vertices, triangles, group, settings, maxTrianglesPerGroup, out var candidate))
                {
                    continue;
                }

                var detailPressure = Mathf.Clamp01((group.Count - maxTrianglesPerGroup) / (float)Mathf.Max(1, maxTrianglesPerGroup));
                var score = candidate.Score + detailPressure * 0.55f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                splitIndex = i;
                left = candidate.Left;
                right = candidate.Right;
            }

            return splitIndex >= 0 && left != null && right != null;
        }

        private static bool TryFindBestSmartSplit(
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            RealPhysicsPlacerSettings settings,
            int maxTrianglesPerGroup,
            out SmartSplitResult result)
        {
            result = default;
            if (group == null || group.Count < 2)
            {
                return false;
            }

            var parentBounds = CalculateBounds(vertices, triangles, group);
            var parentCost = SmartBoundsCost(parentBounds, group.Count);
            var boundaryBonus = CalculateBoundaryEdgeRatio(triangles, group)
                * Mathf.Clamp(settings != null ? settings.bakerSmartHullOpenEdgeBias : 0.35f, 0f, 1f);
            var centers = group
                .Select(triangle => new TriangleSample(triangle, TriangleCenter(vertices, triangles, triangle), 0UL))
                .ToList();
            var points = CollectPoints(vertices, triangles, group);
            var splitSearch = Mathf.Clamp(settings != null ? settings.bakerSmartHullSplitSearch : 4, 1, 5);
            var directions = BuildSmartSplitDirections(vertices, triangles, group, points, parentBounds, splitSearch);
            var splitFractions = BuildSmartSplitFractions(splitSearch);
            var bestScore = float.NegativeInfinity;
            List<int> bestLeft = null;
            List<int> bestRight = null;

            foreach (var rawDirection in directions)
            {
                if (rawDirection.sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    continue;
                }

                var direction = rawDirection.normalized;
                var sorted = centers
                    .OrderBy(sample => Vector3.Dot(sample.Center, direction))
                    .ThenBy(sample => sample.TriangleStart)
                    .ToList();
                var first = Vector3.Dot(sorted[0].Center, direction);
                var last = Vector3.Dot(sorted[sorted.Count - 1].Center, direction);
                if (last - first <= HullEpsilon)
                {
                    continue;
                }

                foreach (var fraction in splitFractions)
                {
                    var splitAt = Mathf.Clamp(Mathf.RoundToInt(sorted.Count * fraction), 1, sorted.Count - 1);
                    var candidateLeft = new List<int>(splitAt);
                    var candidateRight = new List<int>(sorted.Count - splitAt);
                    for (var i = 0; i < sorted.Count; i++)
                    {
                        if (i < splitAt)
                        {
                            candidateLeft.Add(sorted[i].TriangleStart);
                        }
                        else
                        {
                            candidateRight.Add(sorted[i].TriangleStart);
                        }
                    }

                    if (candidateLeft.Count == 0 || candidateRight.Count == 0)
                    {
                        continue;
                    }

                    var score = ScoreSmartSplit(vertices, triangles, parentBounds, parentCost, candidateLeft, candidateRight, maxTrianglesPerGroup) + boundaryBonus;
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestLeft = candidateLeft;
                    bestRight = candidateRight;
                }
            }

            if (bestLeft == null || bestRight == null)
            {
                return false;
            }

            result = new SmartSplitResult(bestLeft, bestRight, bestScore);
            return true;
        }

        private static float ScoreSmartSplit(
            Vector3[] vertices,
            int[] triangles,
            Bounds parentBounds,
            float parentCost,
            List<int> left,
            List<int> right,
            int maxTrianglesPerGroup)
        {
            var leftBounds = CalculateBounds(vertices, triangles, left);
            var rightBounds = CalculateBounds(vertices, triangles, right);
            var leftCost = SmartBoundsCost(leftBounds, left.Count);
            var rightCost = SmartBoundsCost(rightBounds, right.Count);
            var reduction = (parentCost - leftCost - rightCost) / Mathf.Max(0.000001f, parentCost);
            var balance = Mathf.Min(left.Count, right.Count) / (float)Mathf.Max(left.Count, right.Count);
            var parentDiagonal = Mathf.Max(0.0001f, parentBounds.size.magnitude);
            var separation = Mathf.Clamp01((leftBounds.center - rightBounds.center).magnitude / parentDiagonal);
            var detailPressure = Mathf.Clamp01((left.Count + right.Count - maxTrianglesPerGroup) / (float)Mathf.Max(1, maxTrianglesPerGroup));
            var overlapPenalty = BoundsOverlapVolume(leftBounds, rightBounds) / Mathf.Max(0.000001f, BoundsVolume(parentBounds));
            return reduction + balance * 0.12f + separation * 0.08f + detailPressure * 0.18f - overlapPenalty * 0.22f;
        }

        private static List<float> BuildSmartSplitFractions(int splitSearch)
        {
            splitSearch = Mathf.Clamp(splitSearch, 1, 5);
            return splitSearch switch
            {
                1 => new List<float> { 0.5f },
                2 => new List<float> { 0.35f, 0.5f, 0.65f },
                3 => new List<float> { 0.25f, 0.4f, 0.5f, 0.6f, 0.75f },
                4 => new List<float> { 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f },
                _ => new List<float> { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f }
            };
        }

        private static List<Vector3> BuildSmartSplitDirections(
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            List<Vector3> points,
            Bounds bounds,
            int splitSearch)
        {
            var directions = new List<Vector3>(18);
            AddUniqueSplitDirection(directions, Vector3.right);
            AddUniqueSplitDirection(directions, Vector3.up);
            AddUniqueSplitDirection(directions, Vector3.forward);
            AddUniqueSplitDirection(directions, new Vector3(1f, 1f, 0f));
            AddUniqueSplitDirection(directions, new Vector3(1f, -1f, 0f));
            AddUniqueSplitDirection(directions, new Vector3(1f, 0f, 1f));
            AddUniqueSplitDirection(directions, new Vector3(1f, 0f, -1f));
            AddUniqueSplitDirection(directions, new Vector3(0f, 1f, 1f));
            AddUniqueSplitDirection(directions, new Vector3(0f, 1f, -1f));

            var longestAxis = LongestAxis(bounds.size);
            AddUniqueSplitDirection(directions, longestAxis == 0 ? Vector3.right : longestAxis == 1 ? Vector3.up : Vector3.forward);

            if (points != null && points.Count >= 3)
            {
                var orientation = EstimateOrientation(points);
                AddUniqueSplitDirection(directions, orientation * Vector3.right);
                AddUniqueSplitDirection(directions, orientation * Vector3.up);
                AddUniqueSplitDirection(directions, orientation * Vector3.forward);
            }

            AddSmartNormalSplitDirections(vertices, triangles, group, directions, splitSearch * 8);
            return directions;
        }

        private static void AddSmartNormalSplitDirections(Vector3[] vertices, int[] triangles, List<int> group, List<Vector3> directions, int maxNormals)
        {
            if (vertices == null || triangles == null || group == null || directions == null || maxNormals <= 0)
            {
                return;
            }

            var stride = Mathf.Max(1, group.Count / Mathf.Max(1, maxNormals));
            var added = 0;
            for (var i = 0; i < group.Count && added < maxNormals; i += stride)
            {
                var triangle = group[i];
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                var a = triangles[triangle];
                var b = triangles[triangle + 1];
                var c = triangles[triangle + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                {
                    continue;
                }

                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    continue;
                }

                normal.Normalize();
                AddUniqueSplitDirection(directions, normal);
                AddUniqueSplitDirection(directions, Vector3.Cross(normal, Vector3.right));
                AddUniqueSplitDirection(directions, Vector3.Cross(normal, Vector3.up));
                AddUniqueSplitDirection(directions, Vector3.Cross(normal, Vector3.forward));
                added++;
            }
        }

        private static void AddUniqueSplitDirection(List<Vector3> directions, Vector3 direction)
        {
            if (direction.sqrMagnitude <= HullEpsilon * HullEpsilon)
            {
                return;
            }

            direction.Normalize();
            foreach (var existing in directions)
            {
                if (Mathf.Abs(Vector3.Dot(existing, direction)) >= 0.985f)
                {
                    return;
                }
            }

            directions.Add(direction);
        }

        private static float SmartBoundsCost(Bounds bounds, int triangleCount)
        {
            var size = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(bounds.size.x)),
                Mathf.Max(0.0001f, Mathf.Abs(bounds.size.y)),
                Mathf.Max(0.0001f, Mathf.Abs(bounds.size.z)));
            var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var volume = size.x * size.y * size.z;
            var surface = size.x * size.y + size.x * size.z + size.y * size.z;
            var detail = Mathf.Max(0, triangleCount) * longest * longest * longest * 0.00012f;
            return Mathf.Max(0.000001f, volume + surface * longest * 0.2f + detail);
        }

        private static float BoundsOverlapVolume(Bounds left, Bounds right)
        {
            var min = Vector3.Max(left.min, right.min);
            var max = Vector3.Min(left.max, right.max);
            var size = max - min;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
            {
                return 0f;
            }

            return size.x * size.y * size.z;
        }

        private static float CalculateBoundaryEdgeRatio(int[] triangles, List<int> group)
        {
            if (triangles == null || group == null || group.Count == 0)
            {
                return 0f;
            }

            var edges = new Dictionary<ulong, int>(group.Count * 3);
            foreach (var triangle in group)
            {
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                AddEdgeCount(edges, triangles[triangle], triangles[triangle + 1]);
                AddEdgeCount(edges, triangles[triangle + 1], triangles[triangle + 2]);
                AddEdgeCount(edges, triangles[triangle + 2], triangles[triangle]);
            }

            if (edges.Count == 0)
            {
                return 0f;
            }

            var boundaryEdges = 0;
            foreach (var pair in edges)
            {
                if (pair.Value == 1)
                {
                    boundaryEdges++;
                }
            }

            return boundaryEdges / (float)edges.Count;
        }

        private static void AddEdgeCount(Dictionary<ulong, int> edges, int a, int b)
        {
            if (a < 0 || b < 0 || a == b)
            {
                return;
            }

            var key = EdgeKey(a, b);
            edges.TryGetValue(key, out var count);
            edges[key] = count + 1;
        }

        private static ulong EdgeKey(int a, int b)
        {
            var min = (uint)Mathf.Min(a, b);
            var max = (uint)Mathf.Max(a, b);
            return ((ulong)min << 32) | max;
        }

        private static void MergeGroupsToBudget(Vector3[] vertices, int[] triangles, List<List<int>> groups, int maxGroups)
        {
            while (groups.Count > maxGroups && groups.Count > 1)
            {
                var sourceIndex = 0;
                var sourceScore = float.PositiveInfinity;
                for (var i = 0; i < groups.Count; i++)
                {
                    var score = CalculateBounds(vertices, triangles, groups[i]).size.sqrMagnitude + groups[i].Count * 0.01f;
                    if (score < sourceScore)
                    {
                        sourceScore = score;
                        sourceIndex = i;
                    }
                }

                var targetIndex = FindNearestGroup(vertices, triangles, groups, sourceIndex);
                groups[targetIndex].AddRange(groups[sourceIndex]);
                groups.RemoveAt(sourceIndex);
            }
        }

        private static int FindNearestGroup(Vector3[] vertices, int[] triangles, List<List<int>> groups, int sourceIndex)
        {
            var sourceBounds = CalculateBounds(vertices, triangles, groups[sourceIndex]);
            var sourceCenter = sourceBounds.center;
            var bestIndex = sourceIndex == 0 ? 1 : 0;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < groups.Count; i++)
            {
                if (i == sourceIndex)
                {
                    continue;
                }

                var targetBounds = CalculateBounds(vertices, triangles, groups[i]);
                var score = (sourceCenter - targetBounds.center).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static Piece CreateMultiHullPiece(
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            string meshName,
            int maxTriangles)
        {
            var hullPiece = CreateConvexHullPiece(vertices, triangles, group, meshName, maxTriangles);
            if (hullPiece?.Mesh != null)
            {
                hullPiece.Shape = RealPhysicsPlacerBakedColliderShape.MultiHull;
                return hullPiece;
            }

            var mesh = CreateSampledTrianglePieceMesh(vertices, triangles, group, meshName, maxTriangles);
            if (mesh == null)
            {
                return null;
            }

            return new Piece
            {
                Shape = RealPhysicsPlacerBakedColliderShape.MultiHull,
                ColliderKind = PrimitiveColliderKind.Mesh,
                Mesh = mesh
            };
        }

        private static Mesh CreateSampledTrianglePieceMesh(
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            string meshName,
            int maxTriangles)
        {
            if (vertices == null || triangles == null || group == null || group.Count == 0)
            {
                return null;
            }

            maxTriangles = Mathf.Clamp(maxTriangles <= 0 ? UnityConvexTriangleLimit : maxTriangles, 4, UnityConvexTriangleLimit);
            var selectedTriangles = SelectTriangleSpread(vertices, triangles, group, maxTriangles);
            if (selectedTriangles.Count < 4)
            {
                selectedTriangles = group.Take(Mathf.Min(group.Count, maxTriangles)).ToList();
            }

            var indexMap = new Dictionary<int, int>();
            var localVertices = new List<Vector3>(selectedTriangles.Count * 3);
            var localTriangles = new List<int>(selectedTriangles.Count * 3);
            foreach (var triangle in selectedTriangles)
            {
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                for (var corner = 0; corner < 3; corner++)
                {
                    var sourceIndex = triangles[triangle + corner];
                    if (sourceIndex < 0 || sourceIndex >= vertices.Length)
                    {
                        continue;
                    }

                    if (!indexMap.TryGetValue(sourceIndex, out var localIndex))
                    {
                        localIndex = localVertices.Count;
                        indexMap[sourceIndex] = localIndex;
                        localVertices.Add(vertices[sourceIndex]);
                    }

                    localTriangles.Add(localIndex);
                }
            }

            if (localVertices.Count < 4 || localTriangles.Count < 12)
            {
                return null;
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.None,
                indexFormat = localVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(localVertices);
            mesh.SetTriangles(localTriangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static List<int> SelectTriangleSpread(Vector3[] vertices, int[] triangles, List<int> group, int maxTriangles)
        {
            if (group.Count <= maxTriangles)
            {
                return new List<int>(group);
            }

            var centers = new List<TriangleSample>(group.Count);
            var bounds = CalculateBounds(vertices, triangles, group);
            for (var i = 0; i < group.Count; i++)
            {
                var triangle = group[i];
                var center = TriangleCenter(vertices, triangles, triangle);
                centers.Add(new TriangleSample(triangle, center, MortonKey(center, bounds)));
            }

            var ordered = centers
                .OrderBy(sample => sample.SortKey)
                .ThenBy(sample => sample.TriangleStart)
                .ToList();
            var selected = new List<int>(maxTriangles);
            var selectedSet = new HashSet<int>();
            AddAxisExtremes(centers, selected, selectedSet, 0, maxTriangles);
            AddAxisExtremes(centers, selected, selectedSet, 1, maxTriangles);
            AddAxisExtremes(centers, selected, selectedSet, 2, maxTriangles);

            var target = Mathf.Max(0, maxTriangles - selected.Count);
            for (var i = 0; i < target && selected.Count < maxTriangles; i++)
            {
                var idealIndex = Mathf.Clamp(
                    Mathf.RoundToInt((i + 0.5f) * ordered.Count / Mathf.Max(1, target)),
                    0,
                    ordered.Count - 1);
                var chosenIndex = FindNearestUnselectedTriangle(ordered, selectedSet, idealIndex);
                if (chosenIndex < 0)
                {
                    break;
                }

                AddSelectedTriangle(ordered[chosenIndex].TriangleStart, selected, selectedSet, maxTriangles);
            }

            return selected
                .OrderBy(triangle => triangle)
                .ToList();
        }

        private static void AddAxisExtremes(List<TriangleSample> samples, List<int> selected, HashSet<int> selectedSet, int axis, int maxTriangles)
        {
            if (selected.Count >= maxTriangles || samples.Count == 0)
            {
                return;
            }

            var min = samples[0];
            var max = samples[0];
            for (var i = 1; i < samples.Count; i++)
            {
                if (samples[i].Center[axis] < min.Center[axis])
                {
                    min = samples[i];
                }

                if (samples[i].Center[axis] > max.Center[axis])
                {
                    max = samples[i];
                }
            }

            AddSelectedTriangle(min.TriangleStart, selected, selectedSet, maxTriangles);
            AddSelectedTriangle(max.TriangleStart, selected, selectedSet, maxTriangles);
        }

        private static bool AddSelectedTriangle(int triangleStart, List<int> selected, HashSet<int> selectedSet, int maxTriangles)
        {
            if (selected.Count >= maxTriangles || !selectedSet.Add(triangleStart))
            {
                return false;
            }

            selected.Add(triangleStart);
            return true;
        }

        private static int FindNearestUnselectedTriangle(List<TriangleSample> ordered, HashSet<int> selectedSet, int idealIndex)
        {
            if (ordered.Count == 0)
            {
                return -1;
            }

            for (var offset = 0; offset < ordered.Count; offset++)
            {
                var left = idealIndex - offset;
                if (left >= 0 && !selectedSet.Contains(ordered[left].TriangleStart))
                {
                    return left;
                }

                var right = idealIndex + offset;
                if (right < ordered.Count && !selectedSet.Contains(ordered[right].TriangleStart))
                {
                    return right;
                }
            }

            return -1;
        }

        private static ulong MortonKey(Vector3 point, Bounds bounds)
        {
            var size = bounds.size;
            var normalized = new Vector3(
                size.x <= HullEpsilon ? 0f : Mathf.Clamp01((point.x - bounds.min.x) / size.x),
                size.y <= HullEpsilon ? 0f : Mathf.Clamp01((point.y - bounds.min.y) / size.y),
                size.z <= HullEpsilon ? 0f : Mathf.Clamp01((point.z - bounds.min.z) / size.z));
            var x = (uint)Mathf.Clamp(Mathf.RoundToInt(normalized.x * 1023f), 0, 1023);
            var y = (uint)Mathf.Clamp(Mathf.RoundToInt(normalized.y * 1023f), 0, 1023);
            var z = (uint)Mathf.Clamp(Mathf.RoundToInt(normalized.z * 1023f), 0, 1023);
            return Part1By2(x) | (Part1By2(y) << 1) | (Part1By2(z) << 2);
        }

        private static ulong Part1By2(uint value)
        {
            ulong result = value & 0x3ffu;
            result = (result | (result << 16)) & 0x30000ffUL;
            result = (result | (result << 8)) & 0x300f00fUL;
            result = (result | (result << 4)) & 0x30c30c3UL;
            result = (result | (result << 2)) & 0x9249249UL;
            return result;
        }

        private static Piece CreateConvexHullPiece(Vector3[] vertices, int[] triangles, List<int> group, string meshName, int hullDetail)
        {
            if (group == null)
            {
                return null;
            }

            return CreateSupportHullPiece(vertices, triangles, group, meshName, hullDetail);
        }

        internal static bool TryCreateConvexPieceFromPoints(
            List<Vector3> points,
            string meshName,
            RealPhysicsPlacerBakedColliderShape shape,
            int hullDetail,
            out Piece piece)
        {
            piece = null;
            if (points == null || points.Count < 3)
            {
                return false;
            }

            var sourcePoints = DeduplicatePoints(points, HullEpsilon * 10f);
            var detail = Mathf.Clamp(hullDetail <= 0 ? 96 : hullDetail, 8, 255);
            var reduced = ReducePointCloudForConvexHull(sourcePoints, detail);
            if (!TryBuildCoveredConvexHullMesh(sourcePoints, reduced, meshName, out var mesh)
                && !TryBuildCoveredFallbackHullMesh(sourcePoints, meshName, out mesh))
            {
                return false;
            }

            piece = new Piece
            {
                Shape = shape,
                ColliderKind = PrimitiveColliderKind.Mesh,
                Mesh = mesh
            };
            return true;
        }

        private static Piece CreateSupportHullPiece(Vector3[] vertices, int[] triangles, List<int> group, string meshName, int hullDetail)
        {
            var points = DeduplicatePoints(CollectPoints(vertices, triangles, group), HullEpsilon * 10f);
            if (points.Count < 3)
            {
                return null;
            }

            var directPoints = ReducePointCloudForConvexHull(points, Mathf.Clamp(hullDetail <= 0 ? 96 : hullDetail, 8, 255));
            if (TryBuildCoveredConvexHullMesh(points, directPoints, meshName, out var directMesh))
            {
                return new Piece
                {
                    Shape = RealPhysicsPlacerBakedColliderShape.ConvexHull,
                    ColliderKind = PrimitiveColliderKind.Mesh,
                    Mesh = directMesh
                };
            }

            var rotation = EstimateOrientation(points);
            var inverse = Quaternion.Inverse(rotation);
            var localPoints = points.Select(point => inverse * point).ToList();
            if (!TryCreateSupportHullMesh(localPoints, vertices, triangles, group, rotation, inverse, meshName, out var mesh)
                || !TryKeepCoveredMesh(ref mesh, points))
            {
                if (!TryBuildCoveredFallbackHullMesh(points, meshName, out mesh))
                {
                    return null;
                }
            }

            return new Piece
            {
                Shape = RealPhysicsPlacerBakedColliderShape.ConvexHull,
                ColliderKind = PrimitiveColliderKind.Mesh,
                Mesh = mesh
            };
        }

        private static bool TryCreateSupportHullMesh(
            List<Vector3> localPoints,
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            Quaternion rotation,
            Quaternion inverseRotation,
            string meshName,
            out Mesh mesh)
        {
            mesh = null;
            var fibonacciPasses = new[] { MultiHullFibonacciDirections, 24, 16, 8, 0 };
            var normalPasses = new[] { MultiHullNormalDirectionPairs, 8, 4, 0, 0 };

            for (var pass = 0; pass < fibonacciPasses.Length; pass++)
            {
                var supportPoints = CollectSupportPoints(
                        localPoints,
                        vertices,
                        triangles,
                        group,
                        inverseRotation,
                        fibonacciPasses[pass],
                        normalPasses[pass])
                    .Select(point => rotation * point)
                    .ToList();

                supportPoints = DeduplicatePoints(supportPoints, HullEpsilon * 10f);
                if (supportPoints.Count < 3)
                {
                    continue;
                }

                if (TryBuildConvexHullMesh(supportPoints, meshName, out mesh)
                    || TryBuildReducedSupportHullMesh(supportPoints, meshName, out mesh))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildReducedSupportHullMesh(List<Vector3> points, string meshName, out Mesh mesh)
        {
            mesh = null;
            var budgets = new[] { 32, 24, 16, 12, 8 };
            foreach (var budget in budgets)
            {
                var reduced = ReducePointCloudBySpread(points, budget);
                if (reduced.Count < 3)
                {
                    continue;
                }

                if (TryBuildConvexHullMesh(reduced, meshName, out mesh))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Vector3> ReducePointCloudBySpread(List<Vector3> points, int maxCount)
        {
            points = DeduplicatePoints(points, HullEpsilon * 10f);
            if (points.Count <= maxCount)
            {
                return points;
            }

            var center = Vector3.zero;
            foreach (var point in points)
            {
                center += point;
            }

            center /= points.Count;
            var selected = new List<Vector3>(maxCount)
            {
                points.OrderByDescending(point => (point - center).sqrMagnitude).First()
            };

            while (selected.Count < maxCount)
            {
                var bestPoint = points[0];
                var bestDistance = float.NegativeInfinity;
                foreach (var point in points)
                {
                    var nearestDistance = float.PositiveInfinity;
                    foreach (var chosen in selected)
                    {
                        nearestDistance = Mathf.Min(nearestDistance, (point - chosen).sqrMagnitude);
                    }

                    if (nearestDistance <= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = nearestDistance;
                    bestPoint = point;
                }

                if (selected.Any(point => (point - bestPoint).sqrMagnitude <= HullEpsilon * HullEpsilon))
                {
                    break;
                }

                selected.Add(bestPoint);
            }

            return selected;
        }

        private static List<Vector3> ReducePointCloudForConvexHull(List<Vector3> points, int maxCount)
        {
            points = DeduplicatePoints(points ?? new List<Vector3>(), HullEpsilon * 10f);
            maxCount = Mathf.Clamp(maxCount, 8, 255);
            if (points.Count <= maxCount)
            {
                return points;
            }

            var selected = new List<Vector3>(maxCount);
            var repairBudget = Mathf.Clamp(maxCount / 4, 4, 32);
            var initialBudget = Mathf.Clamp(maxCount - repairBudget, 8, maxCount);
            var directionBudget = Mathf.Clamp(initialBudget - 26, 8, 160);
            foreach (var direction in BuildSupportDirections(directionBudget))
            {
                if (selected.Count >= initialBudget)
                {
                    break;
                }

                var bestDot = float.NegativeInfinity;
                var bestPoint = points[0];
                foreach (var point in points)
                {
                    var dot = Vector3.Dot(point, direction);
                    if (dot <= bestDot)
                    {
                        continue;
                    }

                    bestDot = dot;
                    bestPoint = point;
                }

                AddUniqueReducedPoint(selected, bestPoint);
            }

            if (selected.Count == 0)
            {
                selected.Add(points.OrderByDescending(point => point.sqrMagnitude).First());
            }

            // Fill any remaining budget with farthest-point sampling. The support
            // pass keeps the outer silhouette; this pass distributes extra detail
            // across large faces without allowing interior samples to dominate.
            while (selected.Count < initialBudget)
            {
                var bestPoint = points[0];
                var bestDistance = float.NegativeInfinity;
                foreach (var point in points)
                {
                    var nearestDistance = float.PositiveInfinity;
                    foreach (var chosen in selected)
                    {
                        nearestDistance = Mathf.Min(nearestDistance, (point - chosen).sqrMagnitude);
                    }

                    if (nearestDistance <= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = nearestDistance;
                    bestPoint = point;
                }

                if (!AddUniqueReducedPoint(selected, bestPoint))
                {
                    break;
                }
            }

            RepairPointCloudCoverage(points, selected, maxCount);
            return selected;
        }

        private static void RepairPointCloudCoverage(List<Vector3> sourcePoints, List<Vector3> selected, int maxCount)
        {
            if (sourcePoints == null || selected == null || selected.Count < 4)
            {
                return;
            }

            maxCount = Mathf.Clamp(maxCount, 8, 255);
            var tolerance = CalculateContainmentTolerance(sourcePoints);
            while (selected.Count < maxCount)
            {
                if (!TryBuildConvexHullMesh(selected, "RealTransforms_CoverageProbe", out var probeMesh))
                {
                    return;
                }

                var hasMiss = TryFindWorstOutsidePoint(probeMesh, sourcePoints, tolerance, out var missedPoint, out _);
                UnityEngine.Object.DestroyImmediate(probeMesh);
                if (!hasMiss)
                {
                    return;
                }

                if (!AddUniqueReducedPoint(selected, missedPoint))
                {
                    return;
                }
            }
        }

        private static bool AddUniqueReducedPoint(List<Vector3> points, Vector3 point)
        {
            foreach (var existing in points)
            {
                if ((existing - point).sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    return false;
                }
            }

            points.Add(point);
            return true;
        }

        private static List<Vector3> CollectSupportPoints(
            List<Vector3> localPoints,
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            Quaternion inverseRotation,
            int fibonacciDirections,
            int normalDirectionPairs)
        {
            var directions = BuildSupportDirections(fibonacciDirections);
            AddTriangleNormalDirections(vertices, triangles, group, inverseRotation, directions, normalDirectionPairs);

            var supportPoints = new List<Vector3>(directions.Count);
            foreach (var direction in directions)
            {
                var bestDot = float.NegativeInfinity;
                var bestPoint = Vector3.zero;
                foreach (var point in localPoints)
                {
                    var dot = Vector3.Dot(point, direction);
                    if (dot <= bestDot)
                    {
                        continue;
                    }

                    bestDot = dot;
                    bestPoint = point;
                }

                supportPoints.Add(bestPoint);
            }

            return supportPoints;
        }

        private static List<Vector3> BuildSupportDirections(int fibonacciDirections = MultiHullFibonacciDirections)
        {
            var directions = new List<Vector3>(26 + MultiHullFibonacciDirections + MultiHullNormalDirectionPairs * 2);
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    for (var z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0)
                        {
                            continue;
                        }

                        AddUniqueDirection(directions, new Vector3(x, y, z).normalized, 0.999f);
                    }
                }
            }

            var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            fibonacciDirections = Mathf.Max(0, fibonacciDirections);
            for (var i = 0; i < fibonacciDirections; i++)
            {
                var y = 1f - 2f * (i + 0.5f) / fibonacciDirections;
                var radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var theta = i * goldenAngle;
                AddUniqueDirection(directions, new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius), 0.995f);
            }

            return directions;
        }

        private static void AddTriangleNormalDirections(
            Vector3[] vertices,
            int[] triangles,
            List<int> group,
            Quaternion inverseRotation,
            List<Vector3> directions,
            int normalDirectionPairs)
        {
            normalDirectionPairs = Mathf.Max(0, normalDirectionPairs);
            if (vertices == null || triangles == null || group == null || group.Count == 0 || normalDirectionPairs == 0)
            {
                return;
            }

            var stride = Mathf.Max(1, group.Count / Mathf.Max(1, normalDirectionPairs));
            var addedPairs = 0;
            for (var i = 0; i < group.Count && addedPairs < normalDirectionPairs; i += stride)
            {
                var triangle = group[i];
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                var aIndex = triangles[triangle];
                var bIndex = triangles[triangle + 1];
                var cIndex = triangles[triangle + 2];
                if (aIndex < 0 || aIndex >= vertices.Length
                    || bIndex < 0 || bIndex >= vertices.Length
                    || cIndex < 0 || cIndex >= vertices.Length)
                {
                    continue;
                }

                var normal = Vector3.Cross(vertices[bIndex] - vertices[aIndex], vertices[cIndex] - vertices[aIndex]);
                if (normal.sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    continue;
                }

                normal = (inverseRotation * normal).normalized;
                AddUniqueDirection(directions, normal, 0.98f);
                AddUniqueDirection(directions, -normal, 0.98f);
                addedPairs++;
            }
        }

        private static void AddUniqueDirection(List<Vector3> directions, Vector3 direction, float dotThreshold)
        {
            if (direction.sqrMagnitude <= HullEpsilon * HullEpsilon)
            {
                return;
            }

            direction.Normalize();
            foreach (var existing in directions)
            {
                if (Vector3.Dot(existing, direction) >= dotThreshold)
                {
                    return;
                }
            }

            directions.Add(direction);
        }

        private static List<Vector3> DeduplicatePoints(List<Vector3> points, float epsilon)
        {
            var unique = new List<Vector3>(points.Count);
            var sqrEpsilon = epsilon * epsilon;
            foreach (var point in points)
            {
                if (unique.Any(existing => (existing - point).sqrMagnitude <= sqrEpsilon))
                {
                    continue;
                }

                unique.Add(point);
            }

            return unique;
        }

        internal static Mesh CreatePreviewDisplayMesh(Mesh sourceMesh, string meshName)
        {
            if (sourceMesh == null)
            {
                return null;
            }

            var points = sourceMesh.vertices;
            if (points == null || points.Length < 3)
            {
                return null;
            }

            var reduced = ReducePointCloudBySpread(points.ToList(), 64);
            if (TryBuildConvexHullMesh(reduced, meshName, out var mesh)
                || TryBuildPlanarPrismHullMesh(reduced, meshName, out mesh, true)
                || TryBuildLinearDiamondHullMesh(reduced, meshName, out mesh)
                || TryBuildOrientedBoxHullMesh(reduced, meshName, out mesh))
            {
                mesh.hideFlags = HideFlags.HideAndDontSave;
                return mesh;
            }

            return null;
        }

        private static bool TryBuildConvexHullMesh(List<Vector3> points, string meshName, out Mesh mesh)
        {
            mesh = null;
            points = DeduplicatePoints(points, HullEpsilon * 10f);
            if (points.Count < 3)
            {
                return false;
            }

            if (points.Count < 4)
            {
                return TryBuildPlanarPrismHullMesh(points, meshName, out mesh, true)
                    || TryBuildLinearDiamondHullMesh(points, meshName, out mesh)
                    || TryBuildOrientedBoxHullMesh(points, meshName, out mesh);
            }

            var faces = BuildQuickConvexHullFaces(points);
            if (faces.Count < 12 || faces.Count / 3 > UnityConvexTriangleLimit)
            {
                faces = BuildConvexHullFaces(points);
            }

            if (faces.Count >= 12 && faces.Count / 3 <= UnityConvexTriangleLimit)
            {
                mesh = new Mesh
                {
                    name = meshName,
                    hideFlags = HideFlags.None
                };
                mesh.SetVertices(points);
                mesh.SetTriangles(faces, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                if (AcceptHullMesh(ref mesh))
                {
                    return true;
                }
            }

            return TryBuildPlanarPrismHullMesh(points, meshName, out mesh)
                || TryBuildLinearDiamondHullMesh(points, meshName, out mesh)
                || TryBuildOrientedBoxHullMesh(points, meshName, out mesh);
        }

        private static bool TryBuildCoveredConvexHullMesh(List<Vector3> sourcePoints, List<Vector3> hullPoints, string meshName, out Mesh mesh)
        {
            mesh = null;
            if (!TryBuildConvexHullMesh(hullPoints, meshName, out mesh))
            {
                return false;
            }

            return TryKeepCoveredMesh(ref mesh, sourcePoints);
        }

        private static bool TryBuildCoveredFallbackHullMesh(List<Vector3> sourcePoints, string meshName, out Mesh mesh)
        {
            mesh = null;
            if (TryBuildPlanarPrismHullMesh(sourcePoints, meshName, out mesh, true) && TryKeepCoveredMesh(ref mesh, sourcePoints))
            {
                return true;
            }

            if (TryBuildPcaTubeHullMesh(sourcePoints, meshName, out mesh) && TryKeepCoveredMesh(ref mesh, sourcePoints))
            {
                return true;
            }

            if (TryBuildLinearDiamondHullMesh(sourcePoints, meshName, out mesh) && TryKeepCoveredMesh(ref mesh, sourcePoints))
            {
                return true;
            }

            return TryBuildOrientedBoxHullMesh(sourcePoints, meshName, out mesh)
                && TryKeepCoveredMesh(ref mesh, sourcePoints);
        }

        private static bool TryKeepCoveredMesh(ref Mesh mesh, List<Vector3> sourcePoints)
        {
            if (mesh == null)
            {
                return false;
            }

            if (!TryFindWorstOutsidePoint(mesh, sourcePoints, CalculateContainmentTolerance(sourcePoints), out _, out _))
            {
                return true;
            }

            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = null;
            return false;
        }

        private static bool TryFindWorstOutsidePoint(
            Mesh hullMesh,
            List<Vector3> sourcePoints,
            float tolerance,
            out Vector3 point,
            out float distance)
        {
            point = Vector3.zero;
            distance = 0f;
            if (hullMesh == null || sourcePoints == null || sourcePoints.Count == 0)
            {
                return false;
            }

            var hullVertices = hullMesh.vertices;
            var hullTriangles = hullMesh.triangles;
            if (hullVertices == null || hullTriangles == null || hullVertices.Length < 4 || hullTriangles.Length < 12)
            {
                return false;
            }

            var center = Vector3.zero;
            for (var i = 0; i < hullVertices.Length; i++)
            {
                center += hullVertices[i];
            }

            center /= hullVertices.Length;
            for (var triangle = 0; triangle + 2 < hullTriangles.Length; triangle += 3)
            {
                var a = hullVertices[hullTriangles[triangle]];
                var b = hullVertices[hullTriangles[triangle + 1]];
                var c = hullVertices[hullTriangles[triangle + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    continue;
                }

                normal.Normalize();
                if (Vector3.Dot(normal, center - a) > 0f)
                {
                    normal = -normal;
                }

                for (var pointIndex = 0; pointIndex < sourcePoints.Count; pointIndex++)
                {
                    var candidateDistance = Vector3.Dot(normal, sourcePoints[pointIndex] - a);
                    if (candidateDistance <= distance)
                    {
                        continue;
                    }

                    distance = candidateDistance;
                    point = sourcePoints[pointIndex];
                }
            }

            return distance > tolerance;
        }

        private static float CalculateContainmentTolerance(List<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                return HullEpsilon * 100f;
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return Mathf.Max(HullEpsilon * 100f, bounds.size.magnitude * 0.00075f);
        }

        private static bool TryBuildPlanarPrismHullMesh(List<Vector3> points, string meshName, out Mesh mesh, bool allowThickPrism = false)
        {
            mesh = null;
            points = ReducePointCloudBySpread(points, 96);
            if (!TryFindPlanarFrame(points, out var center, out var normal, out var axisX, out var axisY, out var maxPlaneDistance, out var diagonal))
            {
                return false;
            }

            var allowedPlaneError = Mathf.Max(0.0005f, diagonal * 0.025f);
            if (!allowThickPrism && maxPlaneDistance > allowedPlaneError)
            {
                return false;
            }

            var projected = new List<ProjectedPoint>(points.Count);
            foreach (var point in points)
            {
                var offset = point - center;
                projected.Add(new ProjectedPoint(new Vector2(
                    Vector3.Dot(offset, axisX),
                    Vector3.Dot(offset, axisY))));
            }

            var outline = BuildConvexHull2D(projected);
            if (outline.Count < 3)
            {
                return false;
            }

            outline = ReduceConvexLoop(outline, 56);
            var halfThickness = Mathf.Max(0.0005f, diagonal * 0.0025f, maxPlaneDistance + 0.00025f);
            var vertices = new List<Vector3>(outline.Count * 2);
            for (var i = 0; i < outline.Count; i++)
            {
                var point = center + axisX * outline[i].x + axisY * outline[i].y;
                vertices.Add(point - normal * halfThickness);
            }

            for (var i = 0; i < outline.Count; i++)
            {
                var point = center + axisX * outline[i].x + axisY * outline[i].y;
                vertices.Add(point + normal * halfThickness);
            }

            var triangles = new List<int>(outline.Count * 12);
            var topOffset = outline.Count;
            for (var i = 1; i < outline.Count - 1; i++)
            {
                triangles.Add(topOffset);
                triangles.Add(topOffset + i);
                triangles.Add(topOffset + i + 1);

                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add(i);
            }

            for (var i = 0; i < outline.Count; i++)
            {
                var next = (i + 1) % outline.Count;
                triangles.Add(i);
                triangles.Add(next);
                triangles.Add(topOffset + next);

                triangles.Add(i);
                triangles.Add(topOffset + next);
                triangles.Add(topOffset + i);
            }

            if (triangles.Count / 3 > UnityConvexTriangleLimit)
            {
                return false;
            }

            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.None
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return AcceptHullMesh(ref mesh);
        }

        private static bool TryBuildPcaTubeHullMesh(List<Vector3> points, string meshName, out Mesh mesh)
        {
            mesh = null;
            points = ReducePointCloudBySpread(points, 128);
            if (points == null || points.Count < 4)
            {
                return false;
            }

            var rotation = EstimateOrientation(points);
            var inverse = Quaternion.Inverse(rotation);
            var localPoints = points.Select(point => inverse * point).ToList();
            var bounds = new Bounds(localPoints[0], Vector3.zero);
            foreach (var point in localPoints)
            {
                bounds.Encapsulate(point);
            }

            var majorAxis = LongestAxis(bounds.size);
            var axisA = (majorAxis + 1) % 3;
            var axisB = (majorAxis + 2) % 3;
            var min = bounds.min[majorAxis];
            var max = bounds.max[majorAxis];
            if (max - min <= HullEpsilon)
            {
                return false;
            }

            var centerA = 0f;
            var centerB = 0f;
            foreach (var point in localPoints)
            {
                centerA += point[axisA];
                centerB += point[axisB];
            }

            centerA /= localPoints.Count;
            centerB /= localPoints.Count;
            var radiusA = 0f;
            var radiusB = 0f;
            foreach (var point in localPoints)
            {
                radiusA = Mathf.Max(radiusA, Mathf.Abs(point[axisA] - centerA));
                radiusB = Mathf.Max(radiusB, Mathf.Abs(point[axisB] - centerB));
            }

            var length = max - min;
            radiusA = Mathf.Max(radiusA, length * 0.015f, 0.0005f);
            radiusB = Mathf.Max(radiusB, length * 0.015f, 0.0005f);

            const int ringSegments = 10;
            var localVertices = new List<Vector3>(ringSegments * 2 + 2);
            for (var ring = 0; ring < 2; ring++)
            {
                var along = ring == 0 ? min : max;
                for (var segment = 0; segment < ringSegments; segment++)
                {
                    var angle = Mathf.PI * 2f * segment / ringSegments;
                    var local = Vector3.zero;
                    local[majorAxis] = along;
                    local[axisA] = centerA + Mathf.Cos(angle) * radiusA;
                    local[axisB] = centerB + Mathf.Sin(angle) * radiusB;
                    localVertices.Add(rotation * local);
                }
            }

            var minCap = Vector3.zero;
            minCap[majorAxis] = min - length * 0.025f;
            minCap[axisA] = centerA;
            minCap[axisB] = centerB;
            var maxCap = Vector3.zero;
            maxCap[majorAxis] = max + length * 0.025f;
            maxCap[axisA] = centerA;
            maxCap[axisB] = centerB;
            var minCapIndex = localVertices.Count;
            localVertices.Add(rotation * minCap);
            var maxCapIndex = localVertices.Count;
            localVertices.Add(rotation * maxCap);

            var triangles = new List<int>(ringSegments * 12);
            for (var segment = 0; segment < ringSegments; segment++)
            {
                var next = (segment + 1) % ringSegments;
                var a0 = segment;
                var a1 = next;
                var b0 = ringSegments + segment;
                var b1 = ringSegments + next;

                triangles.Add(a0);
                triangles.Add(b0);
                triangles.Add(b1);

                triangles.Add(a0);
                triangles.Add(b1);
                triangles.Add(a1);

                triangles.Add(minCapIndex);
                triangles.Add(a1);
                triangles.Add(a0);

                triangles.Add(maxCapIndex);
                triangles.Add(b0);
                triangles.Add(b1);
            }

            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.None
            };
            mesh.SetVertices(localVertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return AcceptHullMesh(ref mesh);
        }

        private static bool TryBuildLinearDiamondHullMesh(List<Vector3> points, string meshName, out Mesh mesh)
        {
            mesh = null;
            if (points == null || points.Count < 2)
            {
                return false;
            }

            var center = Vector3.zero;
            foreach (var point in points)
            {
                center += point;
            }

            center /= points.Count;
            var axis = Vector3.zero;
            var bestDistance = 0f;
            foreach (var point in points)
            {
                var offset = point - center;
                var distance = offset.sqrMagnitude;
                if (distance <= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                axis = offset;
            }

            if (axis.sqrMagnitude <= HullEpsilon * HullEpsilon)
            {
                return false;
            }

            axis.Normalize();
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            var radius = 0f;
            foreach (var point in points)
            {
                var offset = point - center;
                var along = Vector3.Dot(offset, axis);
                min = Mathf.Min(min, along);
                max = Mathf.Max(max, along);
                radius = Mathf.Max(radius, Vector3.ProjectOnPlane(offset, axis).magnitude);
            }

            if (max - min <= HullEpsilon)
            {
                return false;
            }

            radius = Mathf.Max(radius, (max - min) * 0.01f, 0.0005f);
            var axisY = MostOrthogonalAxis(axis);
            var axisZ = Vector3.Cross(axis, axisY).normalized;
            var vertices = new List<Vector3>
            {
                center + axis * min,
                center + axis * max,
                center + axisY * radius,
                center + axisZ * radius,
                center - axisY * radius,
                center - axisZ * radius
            };

            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.None
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(new[]
            {
                0, 2, 3,
                0, 3, 4,
                0, 4, 5,
                0, 5, 2,
                1, 3, 2,
                1, 4, 3,
                1, 5, 4,
                1, 2, 5
            }, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return AcceptHullMesh(ref mesh);
        }

        private static bool TryBuildOrientedBoxHullMesh(List<Vector3> points, string meshName, out Mesh mesh)
        {
            mesh = null;
            points = DeduplicatePoints(points ?? new List<Vector3>(), HullEpsilon * 10f);
            if (points.Count == 0)
            {
                return false;
            }

            var rotation = points.Count >= 3 ? EstimateOrientation(points) : Quaternion.identity;
            var inverse = Quaternion.Inverse(rotation);
            var localBounds = new Bounds(inverse * points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++)
            {
                localBounds.Encapsulate(inverse * points[i]);
            }

            var size = localBounds.size;
            var diagonal = Mathf.Max(0.001f, size.magnitude);
            var minThickness = Mathf.Max(0.0005f, diagonal * 0.003f);
            size.x = Mathf.Max(size.x, minThickness);
            size.y = Mathf.Max(size.y, minThickness);
            size.z = Mathf.Max(size.z, minThickness);
            var center = localBounds.center;
            var half = size * 0.5f;
            var localVertices = new[]
            {
                center + new Vector3(-half.x, -half.y, -half.z),
                center + new Vector3(half.x, -half.y, -half.z),
                center + new Vector3(half.x, -half.y, half.z),
                center + new Vector3(-half.x, -half.y, half.z),
                center + new Vector3(-half.x, half.y, -half.z),
                center + new Vector3(half.x, half.y, -half.z),
                center + new Vector3(half.x, half.y, half.z),
                center + new Vector3(-half.x, half.y, half.z)
            };
            var vertices = localVertices.Select(vertex => rotation * vertex).ToList();

            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.None
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(new[]
            {
                0, 1, 2, 0, 2, 3,
                7, 6, 5, 7, 5, 4,
                4, 5, 1, 4, 1, 0,
                5, 6, 2, 5, 2, 1,
                6, 7, 3, 6, 3, 2,
                7, 4, 0, 7, 0, 3
            }, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return AcceptHullMesh(ref mesh);
        }

        private static bool AcceptHullMesh(ref Mesh mesh)
        {
            if (IsUnitySafeHullMesh(mesh))
            {
                return true;
            }

            if (mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
            }

            return false;
        }

        private static bool IsUnitySafeHullMesh(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount < 4)
            {
                return false;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || triangles == null)
            {
                return false;
            }

            var triangleCount = triangles.Length / 3;
            if (triangleCount < 4 || triangleCount > UnityConvexTriangleLimit)
            {
                return false;
            }

            var scale = Mathf.Max(0.000001f, mesh.bounds.size.sqrMagnitude);
            var minAreaVectorSqr = Mathf.Max(0.0000000000000001f, scale * scale * 0.000000000001f);
            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var ia = triangles[i];
                var ib = triangles[i + 1];
                var ic = triangles[i + 2];
                if (ia < 0 || ia >= vertices.Length
                    || ib < 0 || ib >= vertices.Length
                    || ic < 0 || ic >= vertices.Length
                    || ia == ib
                    || ib == ic
                    || ia == ic)
                {
                    return false;
                }

                var areaVector = Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]);
                if (areaVector.sqrMagnitude <= minAreaVectorSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryFindPlanarFrame(
            List<Vector3> points,
            out Vector3 center,
            out Vector3 normal,
            out Vector3 axisX,
            out Vector3 axisY,
            out float maxPlaneDistance,
            out float diagonal)
        {
            center = Vector3.zero;
            normal = Vector3.up;
            axisX = Vector3.right;
            axisY = Vector3.forward;
            maxPlaneDistance = 0f;
            diagonal = 0f;
            if (points == null || points.Count < 3)
            {
                return false;
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
            {
                center += point;
                bounds.Encapsulate(point);
            }

            center /= points.Count;
            diagonal = Mathf.Max(0.0001f, bounds.size.magnitude);

            var bestArea = 0f;
            for (var i = 0; i < points.Count - 2; i++)
            {
                for (var j = i + 1; j < points.Count - 1; j++)
                {
                    for (var k = j + 1; k < points.Count; k++)
                    {
                        var candidate = Vector3.Cross(points[j] - points[i], points[k] - points[i]);
                        var area = candidate.sqrMagnitude;
                        if (area <= bestArea)
                        {
                            continue;
                        }

                        bestArea = area;
                        normal = candidate.normalized;
                    }
                }
            }

            if (bestArea <= HullEpsilon * HullEpsilon)
            {
                return false;
            }

            var farthest = points[0];
            var farthestDistance = 0f;
            foreach (var point in points)
            {
                var distance = (point - center).sqrMagnitude;
                if (distance <= farthestDistance)
                {
                    continue;
                }

                farthestDistance = distance;
                farthest = point;
            }

            axisX = Vector3.ProjectOnPlane(farthest - center, normal);
            if (axisX.sqrMagnitude <= HullEpsilon * HullEpsilon)
            {
                axisX = MostOrthogonalAxis(normal);
            }

            axisX.Normalize();
            axisY = Vector3.Cross(normal, axisX).normalized;
            foreach (var point in points)
            {
                maxPlaneDistance = Mathf.Max(maxPlaneDistance, Mathf.Abs(Vector3.Dot(point - center, normal)));
            }

            return true;
        }

        private static List<Vector2> BuildConvexHull2D(List<ProjectedPoint> points)
        {
            var sorted = points
                .Select(point => point.Point)
                .OrderBy(point => point.x)
                .ThenBy(point => point.y)
                .ToList();
            var unique = new List<Vector2>(sorted.Count);
            foreach (var point in sorted)
            {
                if (unique.Count > 0 && (unique[unique.Count - 1] - point).sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    continue;
                }

                unique.Add(point);
            }

            if (unique.Count <= 2)
            {
                return unique;
            }

            var lower = new List<Vector2>();
            foreach (var point in unique)
            {
                while (lower.Count >= 2 && Cross2D(lower[lower.Count - 2], lower[lower.Count - 1], point) <= HullEpsilon)
                {
                    lower.RemoveAt(lower.Count - 1);
                }

                lower.Add(point);
            }

            var upper = new List<Vector2>();
            for (var i = unique.Count - 1; i >= 0; i--)
            {
                var point = unique[i];
                while (upper.Count >= 2 && Cross2D(upper[upper.Count - 2], upper[upper.Count - 1], point) <= HullEpsilon)
                {
                    upper.RemoveAt(upper.Count - 1);
                }

                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static List<Vector2> ReduceConvexLoop(List<Vector2> loop, int maxCount)
        {
            if (loop == null || loop.Count <= maxCount)
            {
                return loop;
            }

            var reduced = new List<Vector2>(maxCount);
            for (var i = 0; i < maxCount; i++)
            {
                var index = Mathf.FloorToInt(i * loop.Count / (float)maxCount);
                reduced.Add(loop[Mathf.Clamp(index, 0, loop.Count - 1)]);
            }

            return reduced;
        }

        private static float Cross2D(Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = b - a;
            var ac = c - a;
            return ab.x * ac.y - ab.y * ac.x;
        }

        private static List<int> BuildConvexHullFaces(List<Vector3> points)
        {
            var triangles = new List<int>();
            var planes = new List<HullPlane>();
            var center = Vector3.zero;
            foreach (var point in points)
            {
                center += point;
            }

            center /= points.Count;
            for (var i = 0; i < points.Count - 2; i++)
            {
                for (var j = i + 1; j < points.Count - 1; j++)
                {
                    for (var k = j + 1; k < points.Count; k++)
                    {
                        var normal = Vector3.Cross(points[j] - points[i], points[k] - points[i]);
                        if (normal.sqrMagnitude <= HullEpsilon * HullEpsilon)
                        {
                            continue;
                        }

                        normal.Normalize();
                        if (!IsHullPlane(points, points[i], normal))
                        {
                            continue;
                        }

                        var distance = Vector3.Dot(normal, points[i]);
                        if (Vector3.Dot(normal, points[i] - center) < 0f)
                        {
                            normal = -normal;
                            distance = -distance;
                        }

                        if (planes.Any(plane => Vector3.Dot(plane.Normal, normal) > 0.999f
                            && Mathf.Abs(plane.Distance - distance) <= HullEpsilon * 20f))
                        {
                            continue;
                        }

                        planes.Add(new HullPlane(normal, distance));
                        AddHullPlaneTriangles(points, normal, distance, center, triangles);
                        if (triangles.Count / 3 > UnityConvexTriangleLimit)
                        {
                            return new List<int>();
                        }
                    }
                }
            }

            return triangles;
        }

        private static List<int> BuildQuickConvexHullFaces(List<Vector3> points)
        {
            if (!TryBuildInitialQuickHull(points, out var faces, out var insidePoint, out var tolerance))
            {
                return new List<int>();
            }

            // This is a clean-room incremental QuickHull-style pass. It finds the
            // most exposed point, removes faces visible from that point, and
            // stitches the horizon. The exhaustive hull builder below remains as
            // a fallback for numerical edge cases.
            var maxIterations = Mathf.Clamp(points.Count * 3, 12, 512);
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                if (!TryFindQuickHullOutsidePoint(points, faces, tolerance, out var pointIndex))
                {
                    break;
                }

                var visibleFaces = new List<int>();
                for (var faceIndex = 0; faceIndex < faces.Count; faceIndex++)
                {
                    if (faces[faceIndex].SignedDistance(points[pointIndex]) > tolerance)
                    {
                        visibleFaces.Add(faceIndex);
                    }
                }

                if (visibleFaces.Count == 0)
                {
                    break;
                }

                var horizonEdges = new Dictionary<ulong, QuickHullEdge>();
                foreach (var faceIndex in visibleFaces)
                {
                    var face = faces[faceIndex];
                    AddQuickHullEdge(horizonEdges, face.A, face.B);
                    AddQuickHullEdge(horizonEdges, face.B, face.C);
                    AddQuickHullEdge(horizonEdges, face.C, face.A);
                }

                for (var i = visibleFaces.Count - 1; i >= 0; i--)
                {
                    faces.RemoveAt(visibleFaces[i]);
                }

                var addedFaces = 0;
                foreach (var edge in horizonEdges.Values)
                {
                    if (edge.Count != 1 || edge.A == pointIndex || edge.B == pointIndex)
                    {
                        continue;
                    }

                    if (AddQuickHullFace(faces, points, edge.A, edge.B, pointIndex, insidePoint, tolerance))
                    {
                        addedFaces++;
                    }
                }

                if (addedFaces == 0 || faces.Count > UnityConvexTriangleLimit)
                {
                    return new List<int>();
                }
            }

            var triangles = new List<int>(faces.Count * 3);
            foreach (var face in faces)
            {
                triangles.Add(face.A);
                triangles.Add(face.B);
                triangles.Add(face.C);
            }

            return triangles;
        }

        private static bool TryBuildInitialQuickHull(
            List<Vector3> points,
            out List<QuickHullFace> faces,
            out Vector3 insidePoint,
            out float tolerance)
        {
            faces = new List<QuickHullFace>(32);
            insidePoint = Vector3.zero;
            tolerance = HullEpsilon * 20f;
            if (points == null || points.Count < 4)
            {
                return false;
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            tolerance = Mathf.Max(tolerance, bounds.size.magnitude * 0.00005f);
            if (!TryFindInitialHullPair(points, bounds, out var a, out var b))
            {
                return false;
            }

            var line = points[b] - points[a];
            var lineLengthSqr = line.sqrMagnitude;
            if (lineLengthSqr <= tolerance * tolerance)
            {
                return false;
            }

            var c = -1;
            var bestLineDistance = tolerance * tolerance;
            for (var i = 0; i < points.Count; i++)
            {
                if (i == a || i == b)
                {
                    continue;
                }

                var distance = Vector3.Cross(line, points[i] - points[a]).sqrMagnitude / lineLengthSqr;
                if (distance <= bestLineDistance)
                {
                    continue;
                }

                bestLineDistance = distance;
                c = i;
            }

            if (c < 0)
            {
                return false;
            }

            var planeNormal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            if (planeNormal.sqrMagnitude <= tolerance * tolerance)
            {
                return false;
            }

            planeNormal.Normalize();
            var d = -1;
            var bestPlaneDistance = tolerance;
            for (var i = 0; i < points.Count; i++)
            {
                if (i == a || i == b || i == c)
                {
                    continue;
                }

                var distance = Mathf.Abs(Vector3.Dot(planeNormal, points[i] - points[a]));
                if (distance <= bestPlaneDistance)
                {
                    continue;
                }

                bestPlaneDistance = distance;
                d = i;
            }

            if (d < 0)
            {
                return false;
            }

            insidePoint = (points[a] + points[b] + points[c] + points[d]) * 0.25f;
            return AddQuickHullFace(faces, points, a, b, c, insidePoint, tolerance)
                && AddQuickHullFace(faces, points, a, d, b, insidePoint, tolerance)
                && AddQuickHullFace(faces, points, b, d, c, insidePoint, tolerance)
                && AddQuickHullFace(faces, points, c, d, a, insidePoint, tolerance);
        }

        private static bool TryFindInitialHullPair(List<Vector3> points, Bounds bounds, out int a, out int b)
        {
            a = 0;
            b = 0;
            var minX = 0;
            var maxX = 0;
            var minY = 0;
            var maxY = 0;
            var minZ = 0;
            var maxZ = 0;
            for (var i = 1; i < points.Count; i++)
            {
                var point = points[i];
                if (point.x < points[minX].x) minX = i;
                if (point.x > points[maxX].x) maxX = i;
                if (point.y < points[minY].y) minY = i;
                if (point.y > points[maxY].y) maxY = i;
                if (point.z < points[minZ].z) minZ = i;
                if (point.z > points[maxZ].z) maxZ = i;
            }

            var candidates = new[]
            {
                (minX, maxX),
                (minY, maxY),
                (minZ, maxZ)
            };

            var bestDistance = Mathf.Max(HullEpsilon * HullEpsilon, bounds.size.sqrMagnitude * 0.00000001f);
            foreach (var candidate in candidates)
            {
                var distance = (points[candidate.Item2] - points[candidate.Item1]).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                a = candidate.Item1;
                b = candidate.Item2;
            }

            return a != b;
        }

        private static bool TryFindQuickHullOutsidePoint(
            List<Vector3> points,
            List<QuickHullFace> faces,
            float tolerance,
            out int pointIndex)
        {
            pointIndex = -1;
            var bestDistance = tolerance;
            for (var i = 0; i < points.Count; i++)
            {
                for (var faceIndex = 0; faceIndex < faces.Count; faceIndex++)
                {
                    var distance = faces[faceIndex].SignedDistance(points[i]);
                    if (distance <= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    pointIndex = i;
                }
            }

            return pointIndex >= 0;
        }

        private static bool AddQuickHullFace(
            List<QuickHullFace> faces,
            List<Vector3> points,
            int a,
            int b,
            int c,
            Vector3 insidePoint,
            float tolerance)
        {
            if (a == b || b == c || c == a)
            {
                return false;
            }

            if (QuickHullFaceExists(faces, a, b, c))
            {
                return false;
            }

            var normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            if (normal.sqrMagnitude <= tolerance * tolerance)
            {
                return false;
            }

            normal.Normalize();
            if (Vector3.Dot(normal, insidePoint - points[a]) > 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            faces.Add(new QuickHullFace(a, b, c, normal, Vector3.Dot(normal, points[a])));
            return true;
        }

        private static bool QuickHullFaceExists(List<QuickHullFace> faces, int a, int b, int c)
        {
            var min = Mathf.Min(a, Mathf.Min(b, c));
            var max = Mathf.Max(a, Mathf.Max(b, c));
            var sum = a + b + c;
            foreach (var face in faces)
            {
                if (Mathf.Min(face.A, Mathf.Min(face.B, face.C)) == min
                    && Mathf.Max(face.A, Mathf.Max(face.B, face.C)) == max
                    && face.A + face.B + face.C == sum)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddQuickHullEdge(Dictionary<ulong, QuickHullEdge> edges, int a, int b)
        {
            var key = BuildQuickHullEdgeKey(a, b);
            if (edges.TryGetValue(key, out var edge))
            {
                edge.Count++;
                edges[key] = edge;
                return;
            }

            edges.Add(key, new QuickHullEdge(a, b));
        }

        private static ulong BuildQuickHullEdgeKey(int a, int b)
        {
            var min = (uint)Mathf.Min(a, b);
            var max = (uint)Mathf.Max(a, b);
            return ((ulong)min << 32) | max;
        }

        private static bool IsHullPlane(List<Vector3> points, Vector3 planePoint, Vector3 normal)
        {
            var hasPositive = false;
            var hasNegative = false;
            foreach (var point in points)
            {
                var distance = Vector3.Dot(normal, point - planePoint);
                if (distance > HullEpsilon)
                {
                    hasPositive = true;
                }
                else if (distance < -HullEpsilon)
                {
                    hasNegative = true;
                }

                if (hasPositive && hasNegative)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddHullPlaneTriangles(List<Vector3> points, Vector3 normal, float distance, Vector3 center, List<int> triangles)
        {
            var face = new List<int>();
            for (var i = 0; i < points.Count; i++)
            {
                if (Mathf.Abs(Vector3.Dot(normal, points[i]) - distance) <= HullEpsilon * 20f)
                {
                    face.Add(i);
                }
            }

            if (face.Count < 3)
            {
                return;
            }

            var faceCenter = Vector3.zero;
            foreach (var index in face)
            {
                faceCenter += points[index];
            }

            faceCenter /= face.Count;
            var axisX = (points[face[0]] - faceCenter).normalized;
            if (axisX.sqrMagnitude <= HullEpsilon)
            {
                axisX = MostOrthogonalAxis(normal);
            }

            var axisY = Vector3.Cross(normal, axisX).normalized;
            face = BuildFaceOutline(points, face, faceCenter, axisX, axisY);
            if (face.Count < 3)
            {
                return;
            }

            for (var i = 1; i < face.Count - 1; i++)
            {
                var a = face[0];
                var b = face[i];
                var c = face[i + 1];
                var triangleNormal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                if (Vector3.Dot(triangleNormal, normal) < 0f)
                {
                    (b, c) = (c, b);
                }

                if (Vector3.Dot(Vector3.Cross(points[b] - points[a], points[c] - points[a]), faceCenter - center) < 0f)
                {
                    (b, c) = (c, b);
                }

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
        }

        private static List<int> BuildFaceOutline(
            List<Vector3> points,
            List<int> face,
            Vector3 faceCenter,
            Vector3 axisX,
            Vector3 axisY)
        {
            var projected = new List<FacePoint>(face.Count);
            foreach (var index in face)
            {
                var offset = points[index] - faceCenter;
                projected.Add(new FacePoint(index, new Vector2(
                    Vector3.Dot(offset, axisX),
                    Vector3.Dot(offset, axisY))));
            }

            projected = projected
                .OrderBy(point => point.Point.x)
                .ThenBy(point => point.Point.y)
                .ToList();

            var unique = new List<FacePoint>(projected.Count);
            foreach (var point in projected)
            {
                if (unique.Count > 0 && (unique[unique.Count - 1].Point - point.Point).sqrMagnitude <= HullEpsilon * HullEpsilon)
                {
                    continue;
                }

                unique.Add(point);
            }

            if (unique.Count <= 3)
            {
                return unique.Select(point => point.Index).ToList();
            }

            var lower = new List<FacePoint>();
            foreach (var point in unique)
            {
                while (lower.Count >= 2 && Cross2D(lower[lower.Count - 2].Point, lower[lower.Count - 1].Point, point.Point) <= HullEpsilon)
                {
                    lower.RemoveAt(lower.Count - 1);
                }

                lower.Add(point);
            }

            var upper = new List<FacePoint>();
            for (var i = unique.Count - 1; i >= 0; i--)
            {
                var point = unique[i];
                while (upper.Count >= 2 && Cross2D(upper[upper.Count - 2].Point, upper[upper.Count - 1].Point, point.Point) <= HullEpsilon)
                {
                    upper.RemoveAt(upper.Count - 1);
                }

                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower.Select(point => point.Index).ToList();
        }

        private static Quaternion EstimateOrientation(List<Vector3> points)
        {
            if (points.Count < 3)
            {
                return Quaternion.identity;
            }

            var mean = Vector3.zero;
            foreach (var point in points)
            {
                mean += point;
            }

            mean /= points.Count;
            var covariance = new SymmetricMatrix3();
            foreach (var point in points)
            {
                var offset = point - mean;
                covariance.XX += offset.x * offset.x;
                covariance.XY += offset.x * offset.y;
                covariance.XZ += offset.x * offset.z;
                covariance.YY += offset.y * offset.y;
                covariance.YZ += offset.y * offset.z;
                covariance.ZZ += offset.z * offset.z;
            }

            var axisX = PowerIterate(covariance, Vector3.right, Vector3.zero);
            if (axisX.sqrMagnitude <= 0.5f)
            {
                return Quaternion.identity;
            }

            var axisY = PowerIterate(covariance, MostOrthogonalAxis(axisX), axisX);
            if (axisY.sqrMagnitude <= 0.5f)
            {
                axisY = MostOrthogonalAxis(axisX);
            }

            var axisZ = Vector3.Cross(axisX, axisY);
            if (axisZ.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            axisZ.Normalize();
            axisY = Vector3.Cross(axisZ, axisX).normalized;
            return Quaternion.LookRotation(axisZ, axisY);
        }

        private static Vector3 PowerIterate(SymmetricMatrix3 matrix, Vector3 seed, Vector3 rejectAxis)
        {
            var value = seed.sqrMagnitude <= 0.0001f ? Vector3.right : seed.normalized;
            for (var i = 0; i < 12; i++)
            {
                value = matrix.Multiply(value);
                if (rejectAxis.sqrMagnitude > 0.5f)
                {
                    value -= rejectAxis * Vector3.Dot(value, rejectAxis);
                }

                if (value.sqrMagnitude <= 0.0001f)
                {
                    return Vector3.zero;
                }

                value.Normalize();
            }

            return value;
        }

        private static Vector3 MostOrthogonalAxis(Vector3 axis)
        {
            axis = axis.sqrMagnitude <= 0.0001f ? Vector3.right : axis.normalized;
            var absX = Mathf.Abs(axis.x);
            var absY = Mathf.Abs(axis.y);
            var absZ = Mathf.Abs(axis.z);
            var seed = absX <= absY && absX <= absZ
                ? Vector3.right
                : absY <= absZ ? Vector3.up : Vector3.forward;
            return (seed - axis * Vector3.Dot(seed, axis)).normalized;
        }

        private static List<Vector3> CollectPoints(Vector3[] vertices, int[] triangles, List<int> group)
        {
            var indices = new HashSet<int>();
            foreach (var triangle in group)
            {
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                indices.Add(triangles[triangle]);
                indices.Add(triangles[triangle + 1]);
                indices.Add(triangles[triangle + 2]);
            }

            return indices
                .Where(index => index >= 0 && index < vertices.Length)
                .Select(index => vertices[index])
                .ToList();
        }

        private static Bounds CalculateBounds(Vector3[] vertices, int[] triangles, List<int> group)
        {
            var initialized = false;
            var bounds = new Bounds();
            foreach (var triangle in group)
            {
                if (triangle < 0 || triangle + 2 >= triangles.Length)
                {
                    continue;
                }

                for (var corner = 0; corner < 3; corner++)
                {
                    var index = triangles[triangle + corner];
                    if (index < 0 || index >= vertices.Length)
                    {
                        continue;
                    }

                    if (!initialized)
                    {
                        bounds = new Bounds(vertices[index], Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(vertices[index]);
                    }
                }
            }

            return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.001f);
        }

        private static Vector3 TriangleCenter(Vector3[] vertices, int[] triangles, int triangle)
        {
            if (!TryGetTriangleVertices(vertices, triangles, triangle, out var a, out var b, out var c))
            {
                return Vector3.zero;
            }

            return (a + b + c) / 3f;
        }

        private static bool TryGetTriangleVertices(Vector3[] vertices, int[] triangles, int triangle, out Vector3 a, out Vector3 b, out Vector3 c)
        {
            a = Vector3.zero;
            b = Vector3.zero;
            c = Vector3.zero;
            if (vertices == null || triangles == null || triangle < 0 || triangle + 2 >= triangles.Length)
            {
                return false;
            }

            var ia = triangles[triangle];
            var ib = triangles[triangle + 1];
            var ic = triangles[triangle + 2];
            if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length)
            {
                return false;
            }

            a = vertices[ia];
            b = vertices[ib];
            c = vertices[ic];
            return true;
        }

        private static int LongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private struct SymmetricMatrix3
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
