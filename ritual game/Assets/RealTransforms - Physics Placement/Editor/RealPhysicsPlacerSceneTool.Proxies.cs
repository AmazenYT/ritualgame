using System;
using System.Collections.Generic;
using System.Linq;
using RealPhysicsPlacer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealPhysicsPlacer.EditorTools
{
    public static partial class RealPhysicsPlacerSceneTool
    {
        // Scene proxies are temporary editor-only colliders generated from renderers.
        // They let render-only environments participate in placement, but scans are
        // cached and throttled because large scenes can be expensive.
        private static readonly Dictionary<EntityId, SceneProxySnapshot> SceneProxySnapshots = new Dictionary<EntityId, SceneProxySnapshot>();

        internal static void MarkSceneProxyScanDirty()
        {
            if (!EditorPhysicsAllowed)
            {
                return;
            }

            proxyScanDirty = true;
            RepaintActiveSceneView();
        }

        private static void RefreshSceneProxiesIfNeeded(Transform[] roots)
        {
            if (ShouldRefreshSceneProxies(roots))
            {
                RebuildSceneProxies();
            }
        }

        private static bool ShouldRefreshSceneProxies(Transform[] roots)
        {
            if (!EditorPhysicsAllowed)
            {
                return false;
            }

            var activeSettings = GetSettings();
            if (!SceneProxyFeatureEnabled(activeSettings))
            {
                if (!HasSceneProxyState())
                {
                    proxyScanDirty = false;
                    return false;
                }

                return proxyScanDirty && SceneProxyRebuildAllowedNow();
            }

            if (proxyScanDirty)
            {
                return SceneProxyRebuildAllowedNow();
            }

            if (!activeSettings.useLocalSceneProxyCache)
            {
                return false;
            }

            if (SceneProxySnapshots.Count > 0 && ShouldCheckSceneProxySnapshotsNow())
            {
                var activeScene = SceneManager.GetActiveScene();
                foreach (var snapshot in SceneProxySnapshots.Values)
                {
                    if (snapshot.IsStale(activeScene))
                    {
                        return SceneProxyRebuildAllowedNow();
                    }
                }
            }

            if (!TryGetSceneProxyCacheFrame(roots, activeSettings, out var center, out _))
            {
                return false;
            }

            var refreshDistance = Mathf.Max(0.1f, activeSettings.sceneProxyCacheRefreshDistance);
            var movedFarEnough = !hasSceneProxyCacheCenter
                || (center - sceneProxyCacheCenter).sqrMagnitude >= refreshDistance * refreshDistance;
            // Local cache refreshes are distance-based so dragging an object does
            // not force a full renderer scan every editor frame.
            return movedFarEnough && SceneProxyRebuildAllowedNow();
        }

        private static bool SceneProxyRebuildAllowedNow()
        {
            if (!leftMouseButtonDown && !physXSessionActive && !placementOverlayPullActive && !handlePullActive)
            {
                return true;
            }

            if (lastSceneProxyRebuildTime < 0d)
            {
                return true;
            }

            return EditorApplication.timeSinceStartup - lastSceneProxyRebuildTime >= ActiveSceneProxyRebuildIntervalSeconds;
        }

        private static bool ShouldCheckSceneProxySnapshotsNow()
        {
            if (leftMouseButtonDown || HasActiveContinuousMode())
            {
                return false;
            }

            var now = EditorApplication.timeSinceStartup;
            if (lastSceneProxySnapshotCheckTime > 0d
                && now - lastSceneProxySnapshotCheckTime < SceneProxySnapshotCheckIntervalSeconds)
            {
                return false;
            }

            lastSceneProxySnapshotCheckTime = now;
            return true;
        }

        private static bool SceneProxyFeatureEnabled()
        {
            return SceneProxyFeatureEnabled(GetSettings());
        }

        private static bool SceneProxyFeatureEnabled(RealPhysicsPlacerSettings activeSettings)
        {
            return Enabled
                && activeSettings != null
                && activeSettings.generateSceneProxyColliders
                && (activeSettings.useRendererBoundsWhenNoCollider || activeSettings.useSceneProxyCollidersForAllObjects);
        }

        private static bool HasSceneProxyState()
        {
            return proxyRoot != null
                || Proxies.Count > 0
                || SceneProxyCache.Count > 0
                || SceneProxySnapshots.Count > 0
                || SceneCollidersShadowedByProxies.Count > 0;
        }

        internal static void RebuildSceneProxies()
        {
            if (!EditorPhysicsAllowed)
            {
                ClearSceneProxies(true);
                proxyScanDirty = false;
                return;
            }

            suppressSceneProxyHierarchyDirty = true;
            var activeSettings = GetSettings();
            try
            {
                var proxyAllSceneObjects = activeSettings.useSceneProxyCollidersForAllObjects;
                var roots = GetLiveSelectionRoots();
                var cacheCenter = Vector3.zero;
                var cacheRadius = 0f;
                var useLocalCache = activeSettings.useLocalSceneProxyCache
                    && TryGetSceneProxyCacheFrame(roots, activeSettings, out cacheCenter, out cacheRadius);
                var meshProxyRadius = useLocalCache
                    ? Mathf.Min(cacheRadius, GetSceneProxyMeshRadius(roots, activeSettings))
                    : float.PositiveInfinity;
                if (!Enabled
                    || !activeSettings.generateSceneProxyColliders
                    || (!activeSettings.useRendererBoundsWhenNoCollider && !proxyAllSceneObjects))
                {
                    ClearSceneProxies(true);
                    proxyScanDirty = false;
                    return;
                }

                BeginSceneProxyRebuild();
                var ignoredTags = activeSettings.BuildIgnoredTagSet();
                var activeScene = SceneManager.GetActiveScene();

                var renderers = GetCachedSceneRenderers(activeScene);
                foreach (var renderer in renderers)
                {
                    if (renderer == null
                        || !renderer.enabled
                        || !renderer.gameObject.activeInHierarchy
                        || renderer.gameObject.scene != activeScene
                        || renderer.gameObject == proxyRoot)
                    {
                        continue;
                    }

                    if (IsUnderAnyRoot(renderer.transform, roots))
                    {
                        continue;
                    }

                    var metadata = GetSceneRendererProxyMetadata(renderer);
                    if (metadata.HasBakedColliderRoot
                        || metadata.IsNonPrimaryLod
                        || metadata.BelongsToRuntimePhysicsActor)
                    {
                        continue;
                    }

                    var rendererHasSceneCollider = metadata.HasSceneCollider;
                    if (!proxyAllSceneObjects && rendererHasSceneCollider)
                    {
                        continue;
                    }

                    if (!LayerIsIncluded(renderer.gameObject.layer, activeSettings.collisionMask))
                    {
                        continue;
                    }

                    if (ignoredTags.Contains(renderer.gameObject.tag))
                    {
                        continue;
                    }

                    var distanceFromCache = 0f;
                    if (useLocalCache)
                    {
                        if (!TryGetFastRendererBroadBounds(renderer, out var broadBounds)
                            || broadBounds.size.sqrMagnitude <= Epsilon)
                        {
                            continue;
                        }

                        distanceFromCache = broadBounds.SqrDistance(cacheCenter);
                    }

                    if (useLocalCache && distanceFromCache > cacheRadius * cacheRadius)
                    {
                        continue;
                    }

                    var bounds = renderer.bounds;
                    if (bounds.size.sqrMagnitude <= Epsilon)
                    {
                        continue;
                    }

                    if (useLocalCache)
                    {
                        distanceFromCache = bounds.SqrDistance(cacheCenter);
                        if (distanceFromCache > cacheRadius * cacheRadius)
                        {
                            continue;
                        }
                    }

                    var useMeshProxy = !useLocalCache || distanceFromCache <= meshProxyRadius * meshProxyRadius;
                    if (proxyAllSceneObjects && rendererHasSceneCollider && !useMeshProxy)
                    {
                        continue;
                    }

                    if (!useMeshProxy && useLocalCache)
                    {
                        continue;
                    }

                    if (!TryAcquireSceneProxy(renderer, bounds, useMeshProxy, activeSettings, out var proxy))
                    {
                        if (proxyAllSceneObjects && rendererHasSceneCollider)
                        {
                            continue;
                        }

                        if (!TryAcquireSceneProxy(renderer, bounds, false, activeSettings, out proxy))
                        {
                            continue;
                        }
                    }

                    Proxies.Add(proxy);
                    SceneProxySnapshots[renderer.GetEntityId()] = SceneProxySnapshot.Capture(renderer);
                    if (proxyAllSceneObjects)
                    {
                        TrackSceneCollidersShadowedByProxy(renderer);
                    }
                }

                DeactivateUnusedSceneProxyCacheEntries();
                TrimSceneProxyCache();
                Physics.SyncTransforms();
                hasSceneProxyCacheCenter = useLocalCache;
                sceneProxyCacheCenter = useLocalCache ? cacheCenter : Vector3.zero;
                sceneProxyCacheRadius = useLocalCache ? cacheRadius : 0f;
                proxyScanDirty = false;
                lastSceneProxyRebuildTime = EditorApplication.timeSinceStartup;
            }
            finally
            {
                suppressSceneProxyHierarchyDirtyUntil = EditorApplication.timeSinceStartup + 0.25d;
                suppressSceneProxyHierarchyDirty = false;
            }
        }

        private static void BeginSceneProxyRebuild()
        {
            sceneProxyBuildStamp++;
            Proxies.Clear();
            SceneProxySnapshots.Clear();
            SceneCollidersShadowedByProxies.Clear();
            EnsureSceneProxyRoot();

            var staleKeys = new List<EntityId>();
            foreach (var pair in SceneProxyCache)
            {
                var entry = pair.Value;
                if (entry == null || !entry.IsValid)
                {
                    staleKeys.Add(pair.Key);
                    entry?.Destroy();
                    continue;
                }
            }

            foreach (var key in staleKeys)
            {
                SceneProxyCache.Remove(key);
            }
        }

        private static void EnsureSceneProxyRoot()
        {
            if (proxyRoot != null)
            {
                return;
            }

            proxyRoot = new GameObject(ProxyRootName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static Renderer[] GetCachedSceneRenderers(Scene activeScene)
        {
            if (!sceneRendererCacheDirty
                && sceneRendererCache != null
                && sceneRendererCacheScene == activeScene)
            {
                return sceneRendererCache;
            }

            sceneRendererCache = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(renderer => renderer != null
                    && renderer.gameObject != null
                    && renderer.gameObject.scene == activeScene
                    && (renderer.hideFlags & HideFlags.HideAndDontSave) == 0)
                .ToArray();
            SceneRendererProxyMetadataCache.Clear();
            sceneRendererCacheScene = activeScene;
            sceneRendererCacheDirty = false;
            return sceneRendererCache;
        }

        private static SceneRendererProxyMetadata GetSceneRendererProxyMetadata(Renderer renderer)
        {
            if (renderer == null)
            {
                return default;
            }

            var rendererId = renderer.GetEntityId();
            if (SceneRendererProxyMetadataCache.TryGetValue(rendererId, out var metadata))
            {
                return metadata;
            }

            metadata = new SceneRendererProxyMetadata(
                RendererHasBakedColliderRoot(renderer),
                IsNonPrimaryLodRenderer(renderer),
                RendererBelongsToRuntimePhysicsActor(renderer),
                RendererHasSceneCollider(renderer));
            SceneRendererProxyMetadataCache[rendererId] = metadata;
            return metadata;
        }

        private static bool TryGetFastRendererBroadBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = default;
            if (renderer == null)
            {
                return false;
            }

            if (renderer is MeshRenderer)
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (mesh != null)
                {
                    var localBounds = mesh.bounds;
                    var center = meshFilter.transform.TransformPoint(localBounds.center);
                    var scaledExtents = Vector3.Scale(localBounds.extents, Abs(meshFilter.transform.lossyScale));
                    var radius = Mathf.Max(0.001f, scaledExtents.magnitude);
                    bounds = new Bounds(center, Vector3.one * radius * 2f);
                    return true;
                }
            }

            bounds = renderer.bounds;
            return bounds.size.sqrMagnitude > Epsilon;
        }

        private static bool TryGetSceneProxyCacheFrame(Transform[] roots, RealPhysicsPlacerSettings activeSettings, out Vector3 center, out float radius)
        {
            center = handlePosition;
            radius = Mathf.Max(
                1f,
                activeSettings.sceneProxyCacheRadius,
                activeSettings.move.physicsAssistDistance * 8f,
                activeSettings.skinWidth * 16f);

            if (roots == null || roots.Length == 0)
            {
                return hasHandle;
            }

            if (TryGetCombinedBounds(roots, out var bounds))
            {
                center = bounds.center;
                radius += bounds.extents.magnitude;
                return true;
            }

            center = AveragePosition(roots);
            return true;
        }

        private static float GetSceneProxyMeshRadius(Transform[] roots, RealPhysicsPlacerSettings activeSettings)
        {
            var radius = Mathf.Max(
                0.5f,
                activeSettings.sceneProxyMeshRadius,
                activeSettings.move.physicsAssistDistance * 4f,
                activeSettings.skinWidth * 10f);

            if (roots != null && roots.Length > 0 && TryGetCombinedBounds(roots, out var bounds))
            {
                radius += bounds.extents.magnitude;
            }

            return radius;
        }

        private static bool RendererHasSceneCollider(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            foreach (var collider in renderer.GetComponentsInParent<Collider>(false))
            {
                if (collider != null && !IsSceneProxyObject(collider.gameObject))
                {
                    return true;
                }
            }

            foreach (var collider in renderer.GetComponentsInChildren<Collider>(false))
            {
                if (collider != null && !IsSceneProxyObject(collider.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RendererBelongsToRuntimePhysicsActor(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            return renderer.GetComponentInParent<Rigidbody>() != null
                || renderer.GetComponentInChildren<Rigidbody>(false) != null
                || renderer.GetComponentInParent<CharacterController>() != null
                || renderer.GetComponentInChildren<CharacterController>(false) != null;
        }

        private static bool RendererHasBakedColliderRoot(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var current = renderer.transform;
            while (current != null)
            {
                for (var i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    if (string.Equals(child.name, RealPhysicsPlacerColliderGenerator.GeneratedRootName, StringComparison.Ordinal)
                        && child.GetComponent<RealPhysicsPlacerBakedCollider>() != null)
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsNonPrimaryLodRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var lodGroup = renderer.GetComponentInParent<LODGroup>();
            if (lodGroup == null)
            {
                return false;
            }

            var lods = lodGroup.GetLODs();
            if (lods == null || lods.Length == 0)
            {
                return false;
            }

            var listedInAnyLod = false;
            for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                var lodRenderers = lods[lodIndex].renderers;
                if (lodRenderers == null)
                {
                    continue;
                }

                foreach (var lodRenderer in lodRenderers)
                {
                    if (lodRenderer != renderer)
                    {
                        continue;
                    }

                    listedInAnyLod = true;
                    return lodIndex > 0;
                }
            }

            return listedInAnyLod && lods[0].renderers.All(lodRenderer => lodRenderer != renderer);
        }

        private static void TrackSceneCollidersShadowedByProxy(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            foreach (var collider in renderer.GetComponentsInParent<Collider>(false))
            {
                if (collider != null && !IsSceneProxyObject(collider.gameObject))
                {
                    SceneCollidersShadowedByProxies[collider.GetEntityId()] = collider;
                }
            }

            foreach (var collider in renderer.GetComponentsInChildren<Collider>(false))
            {
                if (collider != null && !IsSceneProxyObject(collider.gameObject))
                {
                    SceneCollidersShadowedByProxies[collider.GetEntityId()] = collider;
                }
            }
        }

        private static void ClearSceneProxies(bool cleanupOrphans = false)
        {
            foreach (var entry in SceneProxyCache.Values)
            {
                entry?.Destroy();
            }

            SceneProxyCache.Clear();

            for (var i = Proxies.Count - 1; i >= 0; i--)
            {
                if (Proxies[i] != null)
                {
                    Object.DestroyImmediate(Proxies[i]);
                }
            }

            Proxies.Clear();
            SceneProxySnapshots.Clear();
            SceneCollidersShadowedByProxies.Clear();
            hasSceneProxyCacheCenter = false;
            sceneProxyCacheCenter = Vector3.zero;
            sceneProxyCacheRadius = 0f;
            if (proxyRoot != null)
            {
                Object.DestroyImmediate(proxyRoot);
                proxyRoot = null;
            }

            if (!cleanupOrphans)
            {
                proxyScanDirty = true;
                return;
            }

            var orphanedProxies = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(gameObject => gameObject != null)
                .Where(gameObject => (gameObject.hideFlags & HideFlags.HideAndDontSave) != 0)
                .Where(gameObject => gameObject.name == ProxyRootName || gameObject.name.StartsWith("Proxy_", StringComparison.Ordinal))
                .ToArray();

            foreach (var orphanedProxy in orphanedProxies)
            {
                Object.DestroyImmediate(orphanedProxy);
            }

            proxyScanDirty = true;
        }

        private static bool TryAcquireSceneProxy(
            Renderer renderer,
            Bounds bounds,
            bool useMeshProxy,
            RealPhysicsPlacerSettings activeSettings,
            out GameObject proxy)
        {
            proxy = null;
            if (renderer == null)
            {
                return false;
            }

            MeshFilter meshFilter = null;
            Mesh mesh = null;
            var convex = false;
            if (useMeshProxy)
            {
                if (renderer is not MeshRenderer)
                {
                    return false;
                }

                meshFilter = renderer.GetComponent<MeshFilter>();
                mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (mesh == null)
                {
                    return false;
                }

                convex = activeSettings.useConvexStaticMeshProxies;
            }

            var rendererId = renderer.GetEntityId();
            if (SceneProxyCache.TryGetValue(rendererId, out var existing)
                && (existing == null || !existing.IsCompatible(useMeshProxy, mesh, convex)))
            {
                existing?.Destroy();
                SceneProxyCache.Remove(rendererId);
                existing = null;
            }

            if (existing == null)
            {
                existing = SceneProxyCacheEntry.Create(renderer, useMeshProxy, mesh, convex);
                if (existing == null || !existing.IsValid)
                {
                    existing?.Destroy();
                    return false;
                }

                SceneProxyCache[rendererId] = existing;
            }

            existing.Activate(renderer, bounds, meshFilter, mesh, convex, sceneProxyBuildStamp);
            proxy = existing.Proxy;
            return true;
        }

        private static void TrimSceneProxyCache()
        {
            if (SceneProxyCache.Count <= SceneProxyCacheLimit)
            {
                return;
            }

            var overflow = SceneProxyCache.Count - SceneProxyCacheLimit;
            var inactiveKeys = SceneProxyCache
                .Where(pair => pair.Value == null || pair.Value.LastUsedStamp != sceneProxyBuildStamp)
                .OrderBy(pair => pair.Value != null ? pair.Value.LastUsedStamp : int.MinValue)
                .Take(overflow)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var key in inactiveKeys)
            {
                if (SceneProxyCache.TryGetValue(key, out var entry))
                {
                    entry?.Destroy();
                    SceneProxyCache.Remove(key);
                }
            }
        }

        private static void DeactivateUnusedSceneProxyCacheEntries()
        {
            foreach (var entry in SceneProxyCache.Values)
            {
                if (entry != null && entry.IsValid && entry.LastUsedStamp != sceneProxyBuildStamp)
                {
                    entry.SetActive(false);
                }
            }
        }

        private sealed class SceneProxyCacheEntry
        {
            private readonly bool meshProxy;
            private readonly Mesh mesh;
            private readonly bool convex;
            private Collider collider;

            internal GameObject Proxy { get; private set; }
            internal int LastUsedStamp { get; private set; }

            internal bool IsValid => Proxy != null && collider != null;

            private SceneProxyCacheEntry(GameObject proxy, Collider collider, bool meshProxy, Mesh mesh, bool convex)
            {
                Proxy = proxy;
                this.collider = collider;
                this.meshProxy = meshProxy;
                this.mesh = mesh;
                this.convex = convex;
            }

            internal static SceneProxyCacheEntry Create(Renderer renderer, bool meshProxy, Mesh mesh, bool convex)
            {
                if (renderer == null)
                {
                    return null;
                }

                EnsureSceneProxyRoot();
                var proxy = new GameObject("Proxy_" + renderer.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = renderer.gameObject.layer,
                    isStatic = true
                };
                proxy.transform.SetParent(proxyRoot.transform, false);

                Collider collider;
                if (meshProxy)
                {
                    if (mesh == null)
                    {
                        Object.DestroyImmediate(proxy);
                        return null;
                    }

                    var meshCollider = proxy.AddComponent<MeshCollider>();
                    ConfigureMeshProxyCollider(meshCollider, mesh, convex);
                    collider = meshCollider;
                }
                else
                {
                    collider = proxy.AddComponent<BoxCollider>();
                }

                collider.hideFlags = HideFlags.HideAndDontSave;
                return new SceneProxyCacheEntry(proxy, collider, meshProxy, mesh, convex);
            }

            internal bool IsCompatible(bool requestedMeshProxy, Mesh requestedMesh, bool requestedConvex)
            {
                if (!IsValid || meshProxy != requestedMeshProxy)
                {
                    return false;
                }

                return !meshProxy || (mesh == requestedMesh && convex == requestedConvex && collider is MeshCollider);
            }

            internal void Activate(Renderer renderer, Bounds bounds, MeshFilter meshFilter, Mesh requestedMesh, bool requestedConvex, int buildStamp)
            {
                if (!IsValid || renderer == null)
                {
                    return;
                }

                Proxy.name = "Proxy_" + renderer.name;
                Proxy.layer = renderer.gameObject.layer;
                Proxy.hideFlags = HideFlags.HideAndDontSave;
                Proxy.isStatic = true;
                if (Proxy.transform.parent != proxyRoot.transform)
                {
                    Proxy.transform.SetParent(proxyRoot.transform, false);
                }

                if (meshProxy && collider is MeshCollider meshCollider)
                {
                    var sourceTransform = meshFilter != null ? meshFilter.transform : renderer.transform;
                    Proxy.transform.position = sourceTransform.position;
                    Proxy.transform.rotation = sourceTransform.rotation;
                    Proxy.transform.localScale = sourceTransform.lossyScale;
                    if (meshCollider.sharedMesh != requestedMesh || meshCollider.convex != requestedConvex)
                    {
                        ConfigureMeshProxyCollider(meshCollider, requestedMesh, requestedConvex);
                    }
                }
                else if (collider is BoxCollider boxCollider)
                {
                    Proxy.transform.position = bounds.center;
                    Proxy.transform.rotation = Quaternion.identity;
                    Proxy.transform.localScale = Vector3.one;
                    boxCollider.center = Vector3.zero;
                    boxCollider.size = Vector3.Max(bounds.size, Vector3.one * 0.001f);
                }

                collider.enabled = true;
                Proxy.SetActive(true);
                LastUsedStamp = buildStamp;
            }

            internal void SetActive(bool active)
            {
                if (!IsValid)
                {
                    return;
                }

                collider.enabled = active;
                Proxy.SetActive(active);
            }

            internal void Destroy()
            {
                if (Proxy != null)
                {
                    Object.DestroyImmediate(Proxy);
                }

                Proxy = null;
                collider = null;
            }

            private static void ConfigureMeshProxyCollider(MeshCollider meshCollider, Mesh mesh, bool convex)
            {
                if (meshCollider == null)
                {
                    return;
                }

                meshCollider.enabled = false;
                if (meshCollider.sharedMesh != mesh)
                {
                    meshCollider.sharedMesh = null;
                }

                meshCollider.convex = convex;
                if (mesh != null && mesh.isReadable)
                {
                    meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                        | MeshColliderCookingOptions.EnableMeshCleaning
                        | MeshColliderCookingOptions.WeldColocatedVertices
                        | MeshColliderCookingOptions.UseFastMidphase;
                }

                meshCollider.sharedMesh = mesh;
                meshCollider.enabled = true;
            }
        }

        private readonly struct SceneProxySnapshot
        {
            private readonly Renderer renderer;
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;
            private readonly Vector3 boundsCenter;
            private readonly Vector3 boundsSize;
            private readonly int layer;
            private readonly bool enabled;
            private readonly bool activeInHierarchy;

            private SceneProxySnapshot(Renderer renderer)
            {
                this.renderer = renderer;
                var transform = renderer.transform;
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.lossyScale;
                var bounds = renderer.bounds;
                boundsCenter = bounds.center;
                boundsSize = bounds.size;
                layer = renderer.gameObject.layer;
                enabled = renderer.enabled;
                activeInHierarchy = renderer.gameObject.activeInHierarchy;
            }

            internal static SceneProxySnapshot Capture(Renderer renderer)
            {
                return new SceneProxySnapshot(renderer);
            }

            internal bool IsStale(Scene activeScene)
            {
                if (renderer == null || renderer.gameObject == null || renderer.gameObject.scene != activeScene)
                {
                    return true;
                }

                if (renderer.enabled != enabled
                    || renderer.gameObject.activeInHierarchy != activeInHierarchy
                    || renderer.gameObject.layer != layer)
                {
                    return true;
                }

                var transform = renderer.transform;
                if ((transform.position - position).sqrMagnitude > 0.000001f
                    || Quaternion.Angle(transform.rotation, rotation) > 0.01f
                    || (transform.lossyScale - scale).sqrMagnitude > 0.000001f)
                {
                    return true;
                }

                var bounds = renderer.bounds;
                return (bounds.center - boundsCenter).sqrMagnitude > 0.000001f
                    || (bounds.size - boundsSize).sqrMagnitude > 0.000001f;
            }
        }

        private readonly struct SceneRendererProxyMetadata
        {
            internal readonly bool HasBakedColliderRoot;
            internal readonly bool IsNonPrimaryLod;
            internal readonly bool BelongsToRuntimePhysicsActor;
            internal readonly bool HasSceneCollider;

            internal SceneRendererProxyMetadata(
                bool hasBakedColliderRoot,
                bool isNonPrimaryLod,
                bool belongsToRuntimePhysicsActor,
                bool hasSceneCollider)
            {
                HasBakedColliderRoot = hasBakedColliderRoot;
                IsNonPrimaryLod = isNonPrimaryLod;
                BelongsToRuntimePhysicsActor = belongsToRuntimePhysicsActor;
                HasSceneCollider = hasSceneCollider;
            }
        }

    }
}
