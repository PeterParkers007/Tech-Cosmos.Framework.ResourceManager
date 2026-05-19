using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 单个资源的缓存信息
    /// </summary>
    internal class ResourceCache
    {
        /// <summary>资源的唯一标识路径</summary>
        public string Path { get; set; }

        /// <summary>实际加载的资源对象</summary>
        public UnityEngine.Object Asset { get; set; }

        /// <summary>当前引用计数</summary>
        public int RefCount { get; set; }

        /// <summary>是否常驻内存，永不自动卸载</summary>
        public bool IsPermanent { get; set; }

        /// <summary>最后一次访问时间，用于自动清理判断</summary>
        public float LastAccessTime { get; set; }

        /// <summary>资源类型</summary>
        public Type AssetType { get; set; }

        /// <summary>
        /// 正在进行的加载任务（非泛型 Task）
        /// 只用于判断是否完成、等待完成，不通过它获取结果
        /// </summary>
        public Task LoadingTask { get; set; }

        /// <summary>
        /// 刷新最后访问时间
        /// </summary>
        public void Touch()
        {
            LastAccessTime = Time.realtimeSinceStartup;
        }
    }
}