# Resource Manager

Unity 通用异步资源管理器，与项目完全解耦，导入即用。开发阶段零配置使用 Resources 快速迭代，上线一行代码切换 Addressables 控制内存与热更新。

[![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 目录

- [设计理念](#设计理念)
- [特性](#特性)
- [快速开始](#快速开始)
- [安装](#安装)
- [核心概念](#核心概念)
- [API 文档](#api-文档)
- [使用示例](#使用示例)
- [从 Resources 切换到 Addressables](#从-resources-切换到-addressables)
- [编辑器调试窗口](#编辑器调试窗口)
- [常见问题](#常见问题)
- [架构说明](#架构说明)
- [依赖](#依赖)
- [License](#license)

---

## 设计理念

### 问题

游戏开发中，资源加载方式往往因阶段而异：

| 阶段 | 需求 | 常用方案 |
|------|------|----------|
| 开发期 | 快速迭代，改了立刻看效果 | `Resources` 或 `AssetDatabase` |
| 上线期 | 控制内存、分包、热更新 | `Addressables`（底层 `AssetBundle`） |

如果在业务代码中直接调用 `Resources.Load` 或 `Addressables.LoadAssetAsync`，切换时需要改动几十上百处代码，风险极高。

### 解决方案

本库将「加载」和「释放」抽象为统一接口，上层业务只依赖接口，底层实现可随时替换。

```
你的业务代码
    ↓ 只依赖这个接口
IResourceLoader
    ├── ResourcesLoader      （开发用）
    ├── AddressablesLoader   （上线用）
    └── AssetBundleLoader    （自定义扩展）
```

**切换加载方式只需改一行初始化代码，业务代码零修改。**

---

## 特性

- **异步加载**：全异步 API，不阻塞主线程，避免加载卡顿
- **引用计数**：自动管理资源生命周期，同一资源多次请求只加载一次
- **自动清理**：可配置的定时清理机制，引用归零后自动卸载
- **常驻资源**：支持标记永不释放的全局资源
- **加载器可插拔**：接口抽象，Resources / Addressables / 自定义加载器自由切换
- **编辑器调试**：内置可视化调试窗口，实时查看缓存状态和引用计数
- **零配置启动**：不传任何参数即可用 Resources 模式运行
- **与项目完全解耦**：不依赖任何项目特定结构，导入即用

---

## 快速开始

### 第一步：初始化

在游戏入口处调用一次初始化：

```csharp
using ResourceManager.Core;

public class GameBootstrap : MonoBehaviour
{
    async void Start()
    {
        // 零配置，使用 Resources 加载器
        ResourceManager.Initialize();
        
        Debug.Log("资源管理器就绪");
    }
}
```

### 第二步：加载资源

```csharp
// 加载 Sprite（路径相对于 Resources 文件夹，不带后缀）
Sprite sprite = await ResourceManager.Default.LoadAsync<Sprite>("Icons/icon_fireball");

// 加载预制体
GameObject prefab = await ResourceManager.Default.LoadAsync<GameObject>("Prefabs/Enemy");

// 加载音频
AudioClip clip = await ResourceManager.Default.LoadAsync<AudioClip>("Audio/bgm_main");
```

### 第三步：释放资源

```csharp
// 不用时释放引用
ResourceManager.Default.Release("Icons/icon_fireball");
```

### 资源放哪里？

使用 `ResourcesLoader` 时，资源必须放在 `Resources` 文件夹下：

```
Assets/
└── Resources/
    ├── Icons/
    │   ├── icon_fireball.png
    │   └── icon_sword.png
    ├── Prefabs/
    │   └── Enemy.prefab
    └── Audio/
        └── bgm_main.wav
```

加载路径 = 相对于 `Resources` 的路径，不带文件后缀名。

---

## 安装

### 方式一：Unity Package Manager（推荐）

1. 打开 `Window → Package Manager`
2. 点击 `+` → `Add package from git URL...`
3. 输入仓库地址：
   ```
   https://github.com/你的用户名/ResourceManager.git
   ```

### 方式二：手动导入

将整个仓库文件夹复制到项目的 `Packages` 目录下。

### 方式三：直接放入 Assets

将 `Runtime` 和 `Editor` 文件夹放入 `Assets` 下的任意目录中。

---

## 核心概念

### 引用计数

每次调用 `LoadAsync` 加载同一个资源时，引用计数 +1。每次调用 `Release` 时，引用计数 -1。

当引用计数降为 0 时，资源才会被真正卸载。

```
LoadAsync("icon")  →  计数 = 1
LoadAsync("icon")  →  计数 = 2
Release("icon")    →  计数 = 1
Release("icon")    →  计数 = 0  →  卸载
```

### 加载复用

多个请求同时加载同一资源时，只会执行一次真实的加载操作，后续请求共享同一个异步任务。

```
请求1：LoadAsync("icon")  →  开始加载...
请求2：LoadAsync("icon")  →  发现正在加载，等待同一个任务
请求3：LoadAsync("icon")  →  发现正在加载，等待同一个任务
                              ↓
                         加载完成，三个请求同时返回
```

### 常驻资源

标记为常驻的资源永远不会被自动清理，即使引用计数为 0。

```csharp
// 标记常驻
ResourceManager.Default.MarkAsPermanent("UI/button_common");

// 取消常驻
ResourceManager.Default.UnmarkAsPermanent("UI/button_common");
```

### 自动清理

启用后，引用计数为 0 且超过保留时间的非驻留资源会被定时清理。配置在 `ResourceManagerSettings` 中：

- `EnableAutoCleanup`：是否启用（默认 true）
- `CleanupIntervalMs`：清理间隔毫秒（默认 30000）
- `RetentionTimeSeconds`：引用归零后保留时间秒（默认 60）

---

## API 文档

### ResourceManager

核心管理器，单例模式。

#### 初始化

```csharp
// 默认初始化（Resources 加载器）
ResourceManager.Initialize();

// 指定加载器
ResourceManager.Initialize(new AddressablesLoader());

// 创建独立实例（非单例）
var manager = ResourceManager.CreateInstance(new ResourcesLoader(), settings);
```

#### 加载

```csharp
// 异步加载
Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object

// 示例
Sprite sprite = await ResourceManager.Default.LoadAsync<Sprite>("Icons/icon");
GameObject prefab = await ResourceManager.Default.LoadAsync<GameObject>("Prefabs/Enemy");
```

#### 释放

```csharp
// 释放一个引用
void Release(string path)

// 强制卸载（忽略引用计数）
void ForceUnload(string path)

// 强制卸载所有
void ForceUnloadAll()
```

#### 常驻管理

```csharp
void MarkAsPermanent(string path)
void UnmarkAsPermanent(string path)
```

#### 清理

```csharp
// 手动清理未使用资源
void CleanupUnused()
```

#### 调试

```csharp
int CacheCount                                    // 缓存数量
int GetRefCount(string path)                       // 引用计数
IReadOnlyList<...> GetCacheSnapshot()              // 缓存快照
```

### IResourceLoader

加载器接口，实现此接口可自定义加载方式。

```csharp
public interface IResourceLoader
{
    Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
    void Release(string path);
    bool SupportsHotUpdate { get; }
}
```

### ResourceCache

单个资源的缓存信息（内部使用，不直接操作）。

| 字段 | 说明 |
|------|------|
| `Path` | 资源路径 |
| `Asset` | 加载后的资源对象 |
| `RefCount` | 当前引用计数 |
| `IsPermanent` | 是否常驻 |
| `LastAccessTime` | 最后访问时间 |

### ResourceManagerSettings

配置文件，通过 `ScriptableObject` 在 Inspector 中配置。

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `EnableAutoCleanup` | true | 启用自动清理 |
| `CleanupIntervalMs` | 30000 | 清理间隔（毫秒） |
| `RetentionTimeSeconds` | 60 | 引用归零后保留（秒） |
| `UseLoadDelaySimulation` | false | 模拟加载延迟（测试用） |
| `SimulatedDelayMs` | 500 | 模拟延迟时间（毫秒） |

---

## 使用示例

### 示例 1：UI 面板

面板打开时加载图标，关闭时释放。

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ResourceManager.Core;

public class SkillPanel : MonoBehaviour
{
    [SerializeField] private Image[] _icons;
    private List<string> _loadedPaths = new List<string>();

    public async void Show(string[] iconNames)
    {
        for (int i = 0; i < iconNames.Length && i < _icons.Length; i++)
        {
            string path = $"Icons/{iconNames[i]}";
            Sprite sprite = await ResourceManager.Default.LoadAsync<Sprite>(path);
            
            if (sprite != null)
            {
                _icons[i].sprite = sprite;
                _loadedPaths.Add(path);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var path in _loadedPaths)
        {
            ResourceManager.Default.Release(path);
        }
        _loadedPaths.Clear();
    }
}
```

### 示例 2：场景切换

进入场景时加载资源，离开时批量释放。

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ResourceManager.Core;

public class BattleSceneLoader : MonoBehaviour
{
    private List<string> _sceneResources = new List<string>();

    async void Start()
    {
        // 加载战斗场景需要的资源
        string[] resources = { "Prefabs/Player", "Prefabs/Enemy", "Audio/bgm_battle" };
        
        foreach (var path in resources)
        {
            await ResourceManager.Default.LoadAsync<GameObject>(path);
            _sceneResources.Add(path);
        }
    }

    public void ExitBattle()
    {
        foreach (var path in _sceneResources)
        {
            ResourceManager.Default.Release(path);
        }
        _sceneResources.Clear();
        
        SceneManager.LoadScene("MainMenu");
    }
}
```

### 示例 3：自动释放（ResourceHandle）

将资源生命周期绑定到 GameObject。GameObject 销毁时自动释放。

```csharp
using UnityEngine;
using ResourceManager.Core;
using ResourceManager.Components;

public class AutoReleaseExample : MonoBehaviour
{
    async void Start()
    {
        var handle = ResourceHandle.GetOrCreate(gameObject);
        
        var sprite = await ResourceManager.Default.LoadAsync<Sprite>("Icons/icon_test");
        handle.Track("Icons/icon_test");
        
        var prefab = await ResourceManager.Default.LoadAsync<GameObject>("Prefabs/Effect");
        handle.Track("Prefabs/Effect");
        
        // GameObject 销毁时，ResourceHandle 自动调用 Release
        // 无需手动写 OnDestroy
    }
}
```

### 示例 4：角色/怪物

角色创建时加载，销毁时释放。

```csharp
using UnityEngine;
using ResourceManager.Core;

public class Monster : MonoBehaviour
{
    private string _modelPath;
    private string _effectPath;

    public async void Init(string modelId, string effectId)
    {
        _modelPath = $"Models/{modelId}";
        _effectPath = $"Effects/{effectId}";
        
        await ResourceManager.Default.LoadAsync<GameObject>(_modelPath);
        await ResourceManager.Default.LoadAsync<GameObject>(_effectPath);
    }

    void OnDestroy()
    {
        ResourceManager.Default.Release(_modelPath);
        ResourceManager.Default.Release(_effectPath);
    }
}
```

### 示例 5：全局常驻资源

游戏启动时加载，永不释放。

```csharp
using UnityEngine;
using ResourceManager.Core;

public class GlobalResourceLoader : MonoBehaviour
{
    async void Start()
    {
        // 加载全局通用的 UI 素材
        await ResourceManager.Default.LoadAsync<Sprite>("UI/button_normal");
        await ResourceManager.Default.LoadAsync<Sprite>("UI/frame_common");
        await ResourceManager.Default.LoadAsync<Sprite>("UI/bg_main");
        
        // 标记为常驻
        ResourceManager.Default.MarkAsPermanent("UI/button_normal");
        ResourceManager.Default.MarkAsPermanent("UI/frame_common");
        ResourceManager.Default.MarkAsPermanent("UI/bg_main");
    }
}
```

### 示例 6：配合 CancellationToken

在对象销毁时取消正在进行的加载。

```csharp
using UnityEngine;
using System.Threading;
using ResourceManager.Core;

public class CancellableLoader : MonoBehaviour
{
    private CancellationTokenSource _cts;

    async void Start()
    {
        _cts = new CancellationTokenSource();
        
        try
        {
            var sprite = await ResourceManager.Default.LoadAsync<Sprite>(
                "Icons/huge_icon", 
                _cts.Token
            );
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("加载被取消");
        }
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

---

## 从 Resources 切换到 Addressables

### 前置条件

1. 在 Package Manager 中安装 Addressables 包
2. 在 `Edit → Project Settings → Player → Scripting Define Symbols` 中添加 `ADDRESSABLES_ENABLED`
3. 配置好 Addressables Groups，将资源标记为 Addressable

### 切换代码

只改一行初始化代码：

```csharp
// 之前（开发阶段）
ResourceManager.Initialize();  // 默认 ResourcesLoader

// 之后（上线阶段）
ResourceManager.Initialize(new AddressablesLoader());
```

**其他所有业务代码无需任何修改。** `LoadAsync` 和 `Release` 的调用方式完全相同。

### 注意事项

- `ResourcesLoader` 的路径是相对于 `Resources` 文件夹的路径
- `AddressablesLoader` 的路径是资源的 Addressable Key（你在 Addressables 中设置的地址）
- 切换时只需确保路径/Key 对应一致即可

---

## 编辑器调试窗口

菜单：`Window → Resource Manager → Debug Window`

功能：

- 查看当前缓存资源总数
- 实时显示每个资源的路径、引用计数、是否常驻、最后访问时间
- 手动触发清理未使用资源
- 手动强制卸载全部资源
- 支持自动刷新

---

## 常见问题

### Q: 单机游戏也需要异步加载吗？

需要。即使单机游戏，同步加载大资源也会导致画面卡顿。异步加载把文件读取放在后台线程，保证帧率稳定。

### Q: 必须释放资源吗？

是的。每次 `LoadAsync` 都应该有对应的 `Release`，否则资源会一直占用内存。如果不确定释放时机，可以使用 `ResourceHandle` 组件自动管理。

### Q: Resources 和 Addressables 可以混用吗？

技术上可以，但不推荐。统一用一套加载器能让资源管理更可控。如果确有需要，可以创建两个独立的 ResourceManager 实例。

### Q: 多个场景共用的资源怎么处理？

在游戏启动时加载，然后标记为常驻（`MarkAsPermanent`），这样不会在场景切换时被清理。

### Q: 引用计数为 0 了，为什么资源没立即释放？

如果启用了自动清理（`EnableAutoCleanup = true`），资源会在引用归零后保留一段时间（默认 60 秒），避免频繁加载卸载。可以通过 `CleanupUnused()` 手动立即清理。

---

## 架构说明

```
ResourceManager/
├── Runtime/
│   ├── Core/
│   │   ├── IResourceLoader.cs          # 加载器接口
│   │   ├── ResourceCache.cs            # 缓存数据结构
│   │   ├── ResourceManager.cs          # 核心管理器
│   │   └── ResourceManagerSettings.cs  # 配置文件（ScriptableObject）
│   ├── Loaders/
│   │   ├── ResourcesLoader.cs          # Resources 加载器
│   │   └── AddressablesLoader.cs       # Addressables 加载器
│   ├── Extensions/
│   │   └── ResourceManagerExtensions.cs # 便捷扩展方法
│   └── Components/
│       └── ResourceHandle.cs            # 自动释放组件
├── Editor/
│   └── ResourceManagerWindow.cs         # 调试窗口
└── package.json
```

### 调用链路

```
业务代码调用 LoadAsync<T>("path")
    ↓
ResourceManager（检查缓存、管理引用计数）
    ↓
IResourceLoader（抽象加载器）
    ↓
ResourcesLoader / AddressablesLoader（具体实现）
    ↓
Unity 底层 API（Resources.LoadAsync / Addressables.LoadAssetAsync）
```

### 设计原则

- **依赖倒置**：上层依赖接口，不依赖具体实现
- **单一职责**：管理器负责引用计数和缓存，加载器只负责加载
- **开闭原则**：新增加载方式只需实现 `IResourceLoader`，无需修改管理器代码

---

## 依赖

- Unity 2021.3+
- （可选）Addressables 1.21.0+，仅在启用 `AddressablesLoader` 时需要

---

## License

MIT License. 详见 [LICENSE](LICENSE) 文件。