using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TechCosmos.ResourceManager.Runtime
{
    /// <summary>
    /// 提供针对常见 UI 组件的便捷扩展方法
    /// </summary>
    public static class ResourceManagerExtensions
    {
        /// <summary>
        /// 直接设置 Image 的 Sprite（自动处理加载与引用）
        /// </summary>
        public static async Task SetImageAsync(
            this Image image,
            string spritePath,
            CancellationToken cancellationToken = default)
        {
            var sprite = await Runtime.ResourceManager.Default.LoadAsync<Sprite>(spritePath, cancellationToken);
            image.sprite = sprite;
        }

        /// <summary>
        /// 设置 SpriteRenderer 的 Sprite
        /// </summary>
        public static async Task SetSpriteAsync(
            this SpriteRenderer renderer,
            string spritePath,
            CancellationToken cancellationToken = default)
        {
            var sprite = await Runtime.ResourceManager.Default.LoadAsync<Sprite>(spritePath, cancellationToken);
            renderer.sprite = sprite;
        }

        /// <summary>
        /// 加载并实例化 GameObject（如预制体）
        /// </summary>
        public static async Task<GameObject> InstantiateAsync(
            this string prefabPath,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            var prefab = await Runtime.ResourceManager.Default.LoadAsync<GameObject>(prefabPath, cancellationToken);
            if (prefab == null) return null;
            return UnityEngine.Object.Instantiate(prefab, parent);
        }
    }
}