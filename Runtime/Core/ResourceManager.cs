using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 通用异步资源管理器
    /// 使用方式：ResourceManager.Default.LoadAsync<Sprite>("icon_fireball")
    /// </summary>
    public class ResourceManager : IDisposable
    {
        #region 单例与创建

        /// <summary>默认实例，使用 Resources 加载器</summary>
        public static ResourceManager Default { get; private set; }

        /// <summary>
        /// 初始化默认实例（在游戏启动时调用一次）
        /// </summary>
        public static void Initialize(IResourceLoader loader = null)
        {
            if (Default != null)
            {
                Debug.LogWarning("[ResourceManager] 已经初始化过，将先释放旧实例");
                Default.Dispose();
            }

            loader ??= new ResourcesLoader();
            Default = new ResourceManager(loader, ResourceManagerSettings.Default);

            Debug.Log($"[ResourceManager] 初始化完成，加载器类型: {loader.GetType().Name}");
        }

        /// <summary>创建独立的管理器实例（用于特殊场景）</summary>
        public static ResourceManager CreateInstance(IResourceLoader loader, ResourceManagerSettings settings = null)
        {
            return new ResourceManager(loader, settings ?? ResourceManagerSettings.Default);
        }

        #endregion

        #region 私有字段

        private readonly Dictionary<string, ResourceCache> _cache =
            new Dictionary<string, ResourceCache>(StringComparer.OrdinalIgnoreCase);

        private readonly IResourceLoader _loader;
        private readonly ResourceManagerSettings _settings;
        private readonly object _lockObj = new object();
        private bool _disposed;

        #endregion

        #region 构造与析构

        private ResourceManager(IResourceLoader loader, ResourceManagerSettings settings)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _settings = settings ?? ResourceManagerSettings.Default;

            // 开启定时清理
            if (settings.EnableAutoCleanup)
            {
                StartAutoCleanupTimer();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ForceUnloadAll();
            Default = null;
        }

        #endregion

        #region 核心加载方法

        /// <summary>
        /// 异步加载资源（不存在则加载，已存在则引用计数+1）
        /// 多个请求同时加载同一资源时，共享同一个加载任务
        /// </summary>
        public async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourceManager] 加载路径不能为空");
                return null;
            }

            Task loadingTaskToWait = null;
            bool needToLoad = false;

            // ========== 第一步：在 lock 中检查缓存、增加引用计数 ==========
            lock (_lockObj)
            {
                if (_cache.TryGetValue(path, out var cache))
                {
                    // 情况1：资源已加载完成，直接返回
                    if (cache.Asset != null)
                    {
                        cache.RefCount++;
                        cache.Touch();
                        return cache.Asset as T;
                    }

                    // 情况2：正在加载中，复用正在进行的任务
                    if (cache.LoadingTask != null && !cache.LoadingTask.IsFaulted)
                    {
                        cache.RefCount++;
                        cache.Touch();
                        loadingTaskToWait = cache.LoadingTask;
                    }
                    else
                    {
                        // 情况3：之前加载失败，重新加载
                        cache.RefCount++;
                        needToLoad = true;
                    }
                }
                else
                {
                    // 情况4：全新资源，创建缓存条目
                    var newCache = new ResourceCache
                    {
                        Path = path,
                        AssetType = typeof(T),
                        RefCount = 1
                    };
                    newCache.Touch();
                    _cache[path] = newCache;
                    needToLoad = true;
                }
            }

            // ========== 第二步：如果有正在进行的加载任务，等待它完成 ==========
            if (loadingTaskToWait != null)
            {
                try
                {
                    await loadingTaskToWait;
                }
                catch
                {
                    // 加载任务失败，忽略异常，后面会返回 null
                }

                // 加载完成后，从缓存中获取结果
                lock (_lockObj)
                {
                    if (_cache.TryGetValue(path, out var cache) && cache.Asset != null)
                    {
                        return cache.Asset as T;
                    }
                }
                return null;
            }

            // ========== 第三步：自己执行加载 ==========
            if (needToLoad)
            {
                ResourceCache currentCache;
                lock (_lockObj)
                {
                    currentCache = _cache[path];
                }

                var myLoadingTask = LoadAssetAsync<T>(path, cancellationToken);
                currentCache.LoadingTask = myLoadingTask;

                try
                {
                    var asset = await myLoadingTask;

                    lock (_lockObj)
                    {
                        currentCache.Asset = asset;
                        currentCache.LoadingTask = null;
                    }

                    return asset;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ResourceManager] 加载资源失败: {path}, 错误: {ex.Message}");

                    lock (_lockObj)
                    {
                        currentCache.LoadingTask = null;
                        currentCache.RefCount--;

                        // 引用计数归零则移除缓存
                        if (currentCache.RefCount <= 0)
                        {
                            _cache.Remove(path);
                        }
                    }

                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// 实际执行底层加载（可附加模拟延迟）
        /// </summary>
        private async Task<T> LoadAssetAsync<T>(string path, CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (_settings.UseLoadDelaySimulation)
            {
                await Task.Delay(_settings.SimulatedDelayMs, cancellationToken);
            }

            return await _loader.LoadAsync<T>(path, cancellationToken);
        }

        #endregion

        #region 释放方法

        /// <summary>
        /// 释放一个资源引用（引用计数-1，归零时卸载或等待自动清理）
        /// </summary>
        public void Release(string path)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(path)) return;

            lock (_lockObj)
            {
                if (!_cache.TryGetValue(path, out var cache)) return;

                cache.RefCount--;

                if (cache.RefCount <= 0 && !cache.IsPermanent)
                {
                    // 如果启用了自动清理，则延迟处理
                    if (_settings.EnableAutoCleanup)
                    {
                        cache.Touch();
                        return;
                    }

                    // 否则即时卸载
                    UnloadCache(cache);
                    _cache.Remove(path);
                }
            }
        }

        /// <summary>
        /// 强制卸载指定资源（忽略引用计数）
        /// </summary>
        public void ForceUnload(string path)
        {
            lock (_lockObj)
            {
                if (!_cache.TryGetValue(path, out var cache)) return;
                UnloadCache(cache);
                _cache.Remove(path);
            }
        }

        /// <summary>
        /// 强制卸载所有资源
        /// </summary>
        public void ForceUnloadAll()
        {
            lock (_lockObj)
            {
                foreach (var kvp in _cache.ToList())
                {
                    UnloadCache(kvp.Value);
                }
                _cache.Clear();
            }
        }

        /// <summary>
        /// 实际执行单个缓存的卸载
        /// </summary>
        private void UnloadCache(ResourceCache cache)
        {
            if (cache.Asset != null && !cache.IsPermanent)
            {
                _loader.Release(cache.Path);

                if (!cache.Asset.Equals(null))
                {
                    Resources.UnloadAsset(cache.Asset);
                }
            }
        }

        #endregion

        #region 常驻资源管理

        /// <summary>
        /// 将指定资源标记为常驻，永不自动卸载
        /// </summary>
        public void MarkAsPermanent(string path)
        {
            lock (_lockObj)
            {
                if (_cache.TryGetValue(path, out var cache))
                {
                    cache.IsPermanent = true;
                }
            }
        }

        /// <summary>
        /// 取消常驻标记
        /// </summary>
        public void UnmarkAsPermanent(string path)
        {
            lock (_lockObj)
            {
                if (_cache.TryGetValue(path, out var cache))
                {
                    cache.IsPermanent = false;
                }
            }
        }

        #endregion

        #region 自动清理

        private async void StartAutoCleanupTimer()
        {
            while (!_disposed)
            {
                await Task.Delay(_settings.CleanupIntervalMs);
                if (!_disposed)
                {
                    CleanupUnused();
                }
            }
        }

        /// <summary>
        /// 清理引用计数为0且超过保留时间的非驻留资源
        /// </summary>
        public void CleanupUnused()
        {
            lock (_lockObj)
            {
                var now = Time.realtimeSinceStartup;
                var toRemove = _cache
                    .Where(kvp =>
                        kvp.Value.RefCount <= 0 &&
                        !kvp.Value.IsPermanent &&
                        (now - kvp.Value.LastAccessTime) > _settings.RetentionTimeSeconds)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    var cache = _cache[key];
                    UnloadCache(cache);
                    _cache.Remove(key);
                }

                if (toRemove.Count > 0)
                {
                    Debug.Log($"[ResourceManager] 自动清理了 {toRemove.Count} 个未使用资源");
                }
            }
        }

        #endregion

        #region 查询与调试

        /// <summary>获取当前缓存的资源数量</summary>
        public int CacheCount
        {
            get
            {
                lock (_lockObj) return _cache.Count;
            }
        }

        /// <summary>获取指定资源的引用计数</summary>
        public int GetRefCount(string path)
        {
            lock (_lockObj)
            {
                return _cache.TryGetValue(path, out var cache) ? cache.RefCount : 0;
            }
        }

        /// <summary>获取所有缓存的快照（用于调试）</summary>
        public IReadOnlyList<(string path, int refCount, bool isPermanent, float lastAccess)> GetCacheSnapshot()
        {
            lock (_lockObj)
            {
                return _cache.Select(kvp =>
                    (kvp.Key, kvp.Value.RefCount, kvp.Value.IsPermanent, kvp.Value.LastAccessTime)
                ).ToList();
            }
        }

        #endregion

        #region 工具方法

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ResourceManager));
        }

        #endregion
    }
}