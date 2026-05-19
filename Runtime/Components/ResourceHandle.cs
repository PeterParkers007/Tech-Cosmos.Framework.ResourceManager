using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 附加到 GameObject 上，自动跟踪该对象加载的所有资源
    /// 对象销毁时自动释放所有资源引用
    /// </summary>
    public class ResourceHandle : MonoBehaviour
    {
        /// <summary>该对象持有的资源路径列表</summary>
        [SerializeField]
        private List<string> _trackedResources = new List<string>();

        /// <summary>
        /// 记录一个资源引用
        /// </summary>
        public void Track(string resourcePath)
        {
            if (!_trackedResources.Contains(resourcePath))
            {
                _trackedResources.Add(resourcePath);
            }
        }

        /// <summary>
        /// 停止追踪并释放特定资源
        /// </summary>
        public void Untrack(string resourcePath)
        {
            if (_trackedResources.Remove(resourcePath))
            {
                Runtime.ResourceManager.Default?.Release(resourcePath);
            }
        }

        private void OnDestroy()
        {
            if (Runtime.ResourceManager.Default == null) return;

            foreach (var path in _trackedResources)
            {
                Runtime.ResourceManager.Default.Release(path);
            }

            _trackedResources.Clear();
        }

        #region 静态辅助方法

        /// <summary>
        /// 获取或创建 GameObject 上的 ResourceHandle
        /// </summary>
        public static ResourceHandle GetOrCreate(GameObject go)
        {
            var handle = go.GetComponent<ResourceHandle>();
            if (handle == null)
            {
                handle = go.AddComponent<ResourceHandle>();
            }
            return handle;
        }

        #endregion
    }
}