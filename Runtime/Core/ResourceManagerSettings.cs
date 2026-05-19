using UnityEngine;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 资源管理器配置，可通过 ScriptableObject 在 Inspector 中配置
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceManagerSettings", menuName = "Resource Manager/Settings")]
    public class ResourceManagerSettings : ScriptableObject
    {
        private static ResourceManagerSettings _default;
        public static ResourceManagerSettings Default
        {
            get
            {
                if (_default == null)
                {
                    _default = Resources.Load<ResourceManagerSettings>("ResourceManagerSettings");
                    if (_default == null)
                    {
                        _default = CreateInstance<ResourceManagerSettings>();
                    }
                }
                return _default;
            }
        }

        [Header("自动清理")]
        [Tooltip("是否启用定时自动清理未使用资源")]
        public bool EnableAutoCleanup = true;

        [Tooltip("清理间隔（毫秒）")]
        public int CleanupIntervalMs = 30000;

        [Tooltip("资源在引用归零后保留时间（秒）")]
        public float RetentionTimeSeconds = 60f;

        [Header("调试")]
        [Tooltip("启用加载延迟模拟（仅用于测试异步加载表现）")]
        public bool UseLoadDelaySimulation = false;

        [Tooltip("模拟延迟时间（毫秒）")]
        public int SimulatedDelayMs = 500;
    }
}