#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TechCosmos.ResourceManager.Editor
{
    /// <summary>
    /// 资源管理器调试窗口
    /// 菜单: Window -> Resource Manager -> Debug Window
    /// </summary>
    public class ResourceManagerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;

        [MenuItem("Tech-Cosmos/Resource Manager/Debug Window")]
        public static void ShowWindow()
        {
            GetWindow<ResourceManagerWindow>("资源管理器调试");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("资源管理器状态", EditorStyles.boldLabel);

            var manager = Runtime.ResourceManager.Default;
            if (manager == null)
            {
                EditorGUILayout.HelpBox("ResourceManager 尚未初始化。请在游戏启动时调用 ResourceManager.Initialize()", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"缓存资源总数: {manager.CacheCount}");

            _autoRefresh = EditorGUILayout.Toggle("自动刷新", _autoRefresh);
            if (GUILayout.Button("手动刷新"))
            {
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("清理未使用资源"))
            {
                manager.CleanupUnused();
                Repaint();
            }

            if (GUILayout.Button("强制卸载全部"))
            {
                manager.ForceUnloadAll();
                Repaint();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("缓存资源列表", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var snapshot = manager.GetCacheSnapshot();
            if (snapshot.Count == 0)
            {
                EditorGUILayout.LabelField("暂无缓存资源");
            }
            else
            {
                foreach (var item in snapshot.OrderByDescending(x => x.refCount))
                {
                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.LabelField($"路径: {item.path}", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField($"引用计数: {item.refCount}  |  常驻: {item.isPermanent}  |  最后访问: {item.lastAccess:F1}s");

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.EndScrollView();

            if (_autoRefresh)
            {
                Repaint();
            }
        }
    }
}
#endif