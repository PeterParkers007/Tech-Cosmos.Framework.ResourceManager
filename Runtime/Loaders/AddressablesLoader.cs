#if ADDRESSABLES_ENABLED
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 基于 Unity Addressables 的加载器
    /// 适用于中大型项目，支持热更新
    /// </summary>
    public class AddressablesLoader : IResourceLoader
    {
        public bool SupportsHotUpdate => true;
        
        // 记录句柄，用于释放
        private readonly Dictionary<string, AsyncOperationHandle> _handles = 
            new Dictionary<string, AsyncOperationHandle>();
        
        public async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) 
            where T : UnityEngine.Object
        {
            var tcs = new TaskCompletionSource<T>();
            
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(path);
                
                // 记录句柄
                lock (_handles)
                {
                    _handles[path] = handle;
                }
                
                // 注册取消回调
                cancellationToken.Register(() =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        Addressables.Release(handle);
                        tcs.TrySetCanceled(cancellationToken);
                    }
                });
                
                // 等待完成
                handle.Completed += op =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded)
                    {
                        tcs.TrySetResult(op.Result);
                    }
                    else
                    {
                        tcs.TrySetException(new Exception($"Addressables 加载失败: {path}, 错误: {op.OperationException}"));
                    }
                };
                
                return await tcs.Task;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                tcs.TrySetException(ex);
                return await tcs.Task;
            }
        }
        
        public void Release(string path)
        {
            lock (_handles)
            {
                if (_handles.TryGetValue(path, out var handle))
                {
                    Addressables.Release(handle);
                    _handles.Remove(path);
                }
            }
        }
    }
}
#endif