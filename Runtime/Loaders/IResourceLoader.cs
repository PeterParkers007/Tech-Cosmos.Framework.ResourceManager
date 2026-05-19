using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 资源加载器接口，隔离底层加载方式
    /// 你可以实现 Addressables、AssetBundle、Resources 等任意加载方式
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径/地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载完成的资源</returns>
        Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 释放单个资源
        /// </summary>
        /// <param name="path">资源路径/地址</param>
        void Release(string path);

        /// <summary>
        /// 该加载器是否支持热更新资源（比如Addressables）
        /// </summary>
        bool SupportsHotUpdate { get; }
    }
}