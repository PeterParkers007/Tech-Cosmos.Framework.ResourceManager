using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 基于 Unity Resources API 的加载器
    /// 适用于小型项目或不需要 AssetBundle 的场景
    /// </summary>
    public class ResourcesLoader : IResourceLoader
    {
        public bool SupportsHotUpdate => false;

        public async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            // Resources.LoadAsync 本身返回 ResourceRequest，用 TaskCompletionSource 包装
            var tcs = new TaskCompletionSource<T>();

            try
            {
                var request = Resources.LoadAsync<T>(path);

                // 注册取消回调
                cancellationToken.Register(() =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                });

                // 等待异步加载完成
                while (!request.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return await tcs.Task;
                    }
                    await Task.Yield();
                }

                if (request.asset == null)
                {
                    throw new Exception($"Resources 中未找到资源: {path}");
                }

                tcs.TrySetResult(request.asset as T);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                tcs.TrySetException(ex);
            }

            return await tcs.Task;
        }

        public void Release(string path)
        {
            // Resources 没有单独的释放接口，统一由 ResourceManager 通过 Resources.UnloadAsset 处理
        }
    }
}