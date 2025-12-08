using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 简单的 UI 管理器（单例）。
/// 负责当前场景中各个面板的注册、打开、关闭。
/// 动态创建和管理 Canvas，避免场景切换时的重复问题。
/// </summary>
public class UIManager : MonoBehaviour
{
    /// <summary>
    /// 全局访问的单例实例
    /// </summary>
    public static UIManager Instance { get; private set; }

    /// <summary>
    /// 当前已注册的面板列表（按名字）
    /// 一般用 GameObject 名称或自定义字符串作为 key
    /// </summary>
    private readonly Dictionary<string, BasePanel> _panels =
        new Dictionary<string, BasePanel>();

    /// <summary>
    /// 常驻的 Canvas 对象（动态创建）
    /// </summary>
    private GameObject _persistentCanvas;

    /// <summary>
    /// 常驻的 EventSystem 对象（用于跟踪我们创建的 EventSystem）
    /// </summary>
    private GameObject _persistentEventSystem;

    /// <summary>
    /// 背景Canvas的名称列表（这些Canvas不会被清理）
    /// </summary>
    private readonly string[] _backgroundCanvasNames = { "BGCanvas", "BackgroundCanvas", "BgCanvas" };

    private void Awake()
    {
        // 简单单例实现：场景中只保留一个 UIManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 动态创建 Canvas（在游戏开始时创建，常驻）
        EnsureCanvas();

        // 确保 EventSystem 存在（场景切换时 EventSystem 会被销毁，需要重新创建）
        EnsureEventSystem();

        // 场景一开始时，主动扫描场景中已有的 BasePanel 并注册
        // 这样即使 BasePanel.Awake 比 UIManager.Awake 早，也不会漏注册
        var panels = FindObjectsOfType<BasePanel>(true); // true: 包含未激活对象
        foreach (var panel in panels)
        {
            if (panel != null && !string.IsNullOrEmpty(panel.PanelName))
            {
                RegisterPanel(panel.PanelName, panel);
            }
        }
    }

    /// <summary>
    /// 检查Canvas是否是背景Canvas
    /// </summary>
    private bool IsBackgroundCanvas(Canvas canvas)
    {
        if (canvas == null || canvas.gameObject == null) return false;
        
        string canvasName = canvas.gameObject.name;
        foreach (string bgName in _backgroundCanvasNames)
        {
            if (canvasName.Contains(bgName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 确保 Canvas 存在，如果不存在则创建一个并设置为 DontDestroyOnLoad
    /// 注意：背景Canvas不会被管理，它们由场景自己管理
    /// </summary>
    private void EnsureCanvas()
    {
        // 如果已经有常驻的 Canvas，检查它是否仍然存在
        if (_persistentCanvas != null && _persistentCanvas.activeInHierarchy)
        {
            return;
        }

        // 检查场景中是否已经有 Canvas（可能是场景自带的）
        Canvas[] existingCanvases = FindObjectsOfType<Canvas>();
        
        // 过滤掉背景Canvas，只处理UI Canvas
        List<Canvas> uiCanvases = new List<Canvas>();
        foreach (var canvas in existingCanvases)
        {
            if (canvas != null && !IsBackgroundCanvas(canvas))
            {
                uiCanvases.Add(canvas);
            }
        }
        
        // 如果已经存在UI Canvas，使用第一个并设置为 DontDestroyOnLoad
        if (uiCanvases.Count > 0 && uiCanvases[0] != null)
        {
            _persistentCanvas = uiCanvases[0].gameObject;
            DontDestroyOnLoad(_persistentCanvas);
            
            // 添加 DontDestroyOnLoad 脚本以确保后续场景切换时正确处理
            if (_persistentCanvas.GetComponent<DontDestroyOnLoad>() == null)
            {
                _persistentCanvas.AddComponent<DontDestroyOnLoad>();
            }
            
            Debug.Log("[UIManager] 找到已存在的 UI Canvas 并设置为 DontDestroyOnLoad");
            
            // 销毁其他多余的UI Canvas（不包括背景Canvas）
            for (int i = 1; i < uiCanvases.Count; i++)
            {
                if (uiCanvases[i] != null && uiCanvases[i].gameObject != null)
                {
                    Debug.Log($"[UIManager] 销毁多余的 UI Canvas：{uiCanvases[i].gameObject.name}");
                    Destroy(uiCanvases[i].gameObject);
                }
            }
            return;
        }

        // 如果不存在UI Canvas，创建一个新的 Canvas
        _persistentCanvas = Resources.Load<GameObject>("UI/Canvas");
        Instantiate(_persistentCanvas);

        Debug.Log("[UIManager] 创建了新的 UI Canvas 并设置为 DontDestroyOnLoad");
    }

    /// <summary>
    /// 确保场景中存在 EventSystem，如果不存在则创建一个并设置为 DontDestroyOnLoad
    /// </summary>
    private void EnsureEventSystem()
    {
        // 如果已经有常驻的 EventSystem，检查它是否仍然存在
        if (_persistentEventSystem != null && _persistentEventSystem.activeInHierarchy)
        {
            return;
        }

        // 检查场景中是否已经有 EventSystem
        EventSystem[] existingEventSystems = FindObjectsOfType<EventSystem>();
        
        // 如果有多个 EventSystem，保留第一个，销毁其他的
        if (existingEventSystems != null && existingEventSystems.Length > 1)
        {
            for (int i = 1; i < existingEventSystems.Length; i++)
            {
                if (existingEventSystems[i] != null && existingEventSystems[i].gameObject != null)
                {
                    Debug.Log($"[UIManager] 发现多余的 EventSystem，销毁：{existingEventSystems[i].gameObject.name}");
                    Destroy(existingEventSystems[i].gameObject);
                }
            }
        }

        // 如果已经存在 EventSystem，使用它
        if (existingEventSystems != null && existingEventSystems.Length > 0 && existingEventSystems[0] != null)
        {
            _persistentEventSystem = existingEventSystems[0].gameObject;
            DontDestroyOnLoad(_persistentEventSystem);
            Debug.Log("[UIManager] 找到已存在的 EventSystem 并设置为 DontDestroyOnLoad");
            return;
        }

        // 如果不存在，创建一个新的 EventSystem
        _persistentEventSystem = new GameObject("EventSystem");
        EventSystem eventSystem = _persistentEventSystem.AddComponent<EventSystem>();
        _persistentEventSystem.AddComponent<StandaloneInputModule>();
        
        // 设置为 DontDestroyOnLoad，确保场景切换时不被销毁
        DontDestroyOnLoad(_persistentEventSystem);
        
        Debug.Log("[UIManager] 创建了新的 EventSystem 并设置为 DontDestroyOnLoad");
    }
    
    /// <summary>
    /// 场景加载后调用，确保 EventSystem 仍然存在
    /// </summary>
    private void OnEnable()
    {
        // 订阅场景加载事件
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    /// <summary>
    /// 取消订阅场景加载事件
    /// </summary>
    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    /// <summary>
    /// 场景加载完成后的回调
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 场景切换后，确保 Canvas 仍然存在
        EnsureCanvas();
        
        // 清理新场景中可能存在的UI Canvas（我们已经有了常驻的）
        // 注意：背景Canvas不会被清理，它们由场景自己管理
        if (_persistentCanvas != null)
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in allCanvases)
            {
                if (canvas != null && canvas.gameObject != null && 
                    canvas.gameObject != _persistentCanvas && 
                    !IsBackgroundCanvas(canvas))
                {
                    Debug.Log($"[UIManager] 清理新场景中的 UI Canvas：{canvas.gameObject.name}");
                    Destroy(canvas.gameObject);
                }
            }
        }
        
        // 场景切换后，确保 EventSystem 仍然存在
        EnsureEventSystem();
        
        // 清理新场景中可能存在的 EventSystem（我们已经有了常驻的）
        // 注意：如果 EventSystem 使用了 DontDestroyOnLoad 脚本，它会自动处理重复问题
        if (_persistentEventSystem != null)
        {
            EventSystem[] allEventSystems = FindObjectsOfType<EventSystem>();
            foreach (var es in allEventSystems)
            {
                if (es != null && es.gameObject != null && es.gameObject != _persistentEventSystem)
                {
                    // 检查是否已经有 DontDestroyOnLoad 脚本处理
                    if (es.gameObject.GetComponent<DontDestroyOnLoad>() == null)
                    {
                        Debug.Log($"[UIManager] 清理新场景中的 EventSystem：{es.gameObject.name}");
                        Destroy(es.gameObject);
                    }
                }
            }
        }
        
        // 重新扫描新场景中的面板并注册
        // 注意：面板应该作为 Canvas 的子对象，或者通过其他方式管理
        var panels = FindObjectsOfType<BasePanel>(true);
        foreach (var panel in panels)
        {
            if (panel != null && !string.IsNullOrEmpty(panel.PanelName))
            {
                RegisterPanel(panel.PanelName, panel);
                
                // 如果面板不是 UI Canvas 的子对象，将其移动到 UI Canvas 下
                // 注意：如果面板在背景Canvas下，不会移动它
                if (_persistentCanvas != null && panel.transform.parent != _persistentCanvas.transform)
                {
                    // 检查面板是否在背景Canvas下
                    Transform parent = panel.transform.parent;
                    bool isInBackgroundCanvas = false;
                    while (parent != null)
                    {
                        Canvas canvas = parent.GetComponent<Canvas>();
                        if (canvas != null && IsBackgroundCanvas(canvas))
                        {
                            isInBackgroundCanvas = true;
                            break;
                        }
                        parent = parent.parent;
                    }
                    
                    // 如果不在背景Canvas下，才移动到UI Canvas
                    if (!isInBackgroundCanvas)
                    {
                        panel.transform.SetParent(_persistentCanvas.transform, false);
                        Debug.Log($"[UIManager] 将面板 {panel.PanelName} 移动到常驻 UI Canvas 下");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 获取常驻的 Canvas 对象
    /// </summary>
    public GameObject GetPersistentCanvas()
    {
        return _persistentCanvas;
    }

    /// <summary>
    /// 注册一个面板，通常在 Panel 自己的 Awake/Start 里调用
    /// </summary>
    public void RegisterPanel(string name, BasePanel panel)
    {
        if (string.IsNullOrEmpty(name) || panel == null) return;

        _panels[name] = panel;
    }

    /// <summary>
    /// 通过名字打开面板
    /// </summary>
    public void OpenPanel(string name)
    {
        if (_panels.TryGetValue(name, out var panel))
        {
            panel.Open();
        }
        else
        {
            Debug.LogWarning($"[UIManager] 未找到名为 {name} 的面板，是否忘记注册？");
        }
    }

    /// <summary>
    /// 通过名字关闭面板
    /// </summary>
    public void ClosePanel(string name)
    {
        if (_panels.TryGetValue(name, out var panel))
        {
            panel.Close();
        }
    }

    /// <summary>
    /// 获取已注册的面板
    /// </summary>
    public BasePanel GetPanel(string name)
    {
        _panels.TryGetValue(name, out var panel);
        return panel;
    }

    /// <summary>
    /// 使用泛型获取指定类型的面板（需事先注册）。
    /// 通常配合面板类名作为 key 使用。
    /// </summary>
    public T GetPanel<T>(string name) where T : BasePanel
    {
        return GetPanel(name) as T;
    }

    /// <summary>
    /// 关闭所有已注册的面板
    /// </summary>
    public void CloseAllPanels()
    {
        foreach (var kv in _panels)
        {
            if (kv.Value != null && kv.Value.IsOpen)
            {
                kv.Value.Close();
            }
        }
    }
}
