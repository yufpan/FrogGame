using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景切换管理器（单例）。
/// 提供黑屏淡入淡出的场景切换接口。
/// 使用方式：
/// 1. 在一个常驻场景中挂一个空物体，添加本脚本。
/// 2. 准备一个全黑的 Panel Prefab（挂在 Canvas 下时全屏），继承自 BasePanel，并挂上 FadePanel 脚本。
/// 3. 在本脚本中拖拽该 Prefab 到 _fadePanelPrefab。
/// 4. 其他脚本中调用：SwitchSceneManager.Instance.SwitchSceneWithFade("GameScene");
/// </summary>
public class SwitchSceneManager : MonoBehaviour
{
    /// <summary>
    /// 全局单例
    /// </summary>
    public static SwitchSceneManager Instance { get; private set; }

    [Header("淡入淡出配置")]
    [Tooltip("场景中已经存在的全屏黑色 FadePanel。不要使用 Prefab 实例化，否则会和 UIManager 的激活逻辑冲突。")]
    [SerializeField] private FadePanel _fadePanelInstance; // 直接引用场景里的 Panel 实例
    [SerializeField] private string _fadePanelName = "FadePanel"; // 备用：通过 UIManager 名字查找
    [SerializeField] private float _fadeDuration = 0.5f; // 淡入/淡出时间
    
    private Image _fadeImage;             // 从 Panel 中获取的 Image
    private bool _isSwitching = false;
    private bool _isFading = false;       // 是否正在执行淡入淡出（不切换场景）

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 不在 Awake 就实例化 Panel，延迟到第一次切换时再创建
        
        // 监听场景加载事件，确保所有场景切换都能播放BGM
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // 取消订阅场景加载事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 对外暴露的场景切换接口：黑屏淡出 -> 关闭指定面板(可选) -> 加载场景 -> 淡入
    /// </summary>
    /// <param name="sceneName">目标场景名称</param>
    /// <param name="panelNameToClose">
    /// 在当前场景中希望在"完全黑屏后"再关闭的面板名称（可选）。
    /// 例如传入 "MenuPanel"，可以做到：先黑屏完全挡住，再把菜单面板关掉，避免玩家看到 UI 突然消失。
    /// </param>
    public void SwitchSceneWithFade(string sceneName, string panelNameToClose = null)
    {
        // 转换为列表形式，保持向后兼容
        List<string> panelsToClose = null;
        if (!string.IsNullOrEmpty(panelNameToClose))
        {
            panelsToClose = new List<string> { panelNameToClose };
        }
        
        SwitchSceneWithFade(sceneName, panelsToClose, null);
    }

    /// <summary>
    /// 扩展的场景切换接口：支持多个面板的关闭和打开
    /// </summary>
    /// <param name="sceneName">目标场景名称</param>
    /// <param name="panelsToClose">在黑屏时关闭的面板名称列表（可选）</param>
    /// <param name="panelsToOpen">场景切换后要打开的面板名称列表（可选）</param>
    public void SwitchSceneWithFade(string sceneName, List<string> panelsToClose = null, List<string> panelsToOpen = null)
    {
        if (_isSwitching)
        {
            // 正在切换中，避免重复调用
            return;
        }
        
        // 确保淡入淡出 Panel 已经实例化
        EnsureFadePanelInstance();
        if (_fadeImage == null)
        {
            Debug.LogError("[SwitchSceneManager] 没有找到用于淡入淡出的 Image，将直接切换场景。");
            SceneManager.LoadScene(sceneName);
            // 直接加载场景时，OnSceneLoaded事件会自动处理BGM播放
            return;
        }

        StartCoroutine(SwitchSceneCoroutine(sceneName, panelsToClose, panelsToOpen));
    }

    /// <summary>
    /// 协程：先淡出到黑，再（可选）关闭指定面板，然后加载场景，最后淡入显示新场景
    /// </summary>
    private IEnumerator SwitchSceneCoroutine(string sceneName, List<string> panelsToClose = null, List<string> panelsToOpen = null)
    {
        _isSwitching = true;

        // 确保 Panel 处于打开状态并阻止点击
        if (_fadePanelInstance != null && !_fadePanelInstance.IsOpen)
        {
            _fadePanelInstance.Open();
        }

        _fadeImage.raycastTarget = true; // 阻止切换过程中的点击

        // 1. 从透明 -> 黑色（淡出当前场景）
        yield return Fade(0f, 1f);

        // 1.5. 黑屏已经完全挡住，此时再关闭指定的面板列表（如果有传）
        if (panelsToClose != null && panelsToClose.Count > 0 && UIManager.Instance != null)
        {
            foreach (string panelName in panelsToClose)
            {
                if (!string.IsNullOrEmpty(panelName))
                {
                    UIManager.Instance.ClosePanel(panelName);
                }
            }
        }

        // 2. 同步加载新场景
        SceneManager.LoadScene(sceneName);

        // 等一帧，确保场景切换完成、UI 刷新
        yield return null;

        // 注意：BGM播放由OnSceneLoaded事件处理，避免重复播放

        // 2.5. 场景切换后，打开指定的面板列表（如果有传）
        if (panelsToOpen != null && panelsToOpen.Count > 0 && UIManager.Instance != null)
        {
            foreach (string panelName in panelsToOpen)
            {
                if (!string.IsNullOrEmpty(panelName))
                {
                    UIManager.Instance.OpenPanel(panelName);
                    Debug.Log($"[SwitchSceneManager] 场景切换后打开面板：{panelName}");
                }
            }
        }

        // 3. 从黑色 -> 透明（淡入新场景）
        yield return Fade(1f, 0f);

        _fadeImage.raycastTarget = false;

        // 完成后隐藏 Panel
        if (_fadePanelInstance != null && _fadePanelInstance.IsOpen)
        {
            _fadePanelInstance.Close();
        }

        _isSwitching = false;
    }

    /// <summary>
    /// 在当前场景中执行淡入淡出效果（不切换场景）
    /// 用于重新开始等场景内操作
    /// </summary>
    /// <param name="onFadeComplete">黑屏时执行的回调函数（用于执行重新开始等操作）</param>
    /// <param name="panelsToClose">在黑屏时关闭的面板名称列表（可选）</param>
    /// <param name="panelsToOpen">淡入后要打开的面板名称列表（可选）</param>
    public void FadeInOut(System.Action onFadeComplete = null, List<string> panelsToClose = null, List<string> panelsToOpen = null)
    {
        if (_isFading || _isSwitching)
        {
            // 正在淡入淡出或切换场景中，避免重复调用
            return;
        }
        
        // 确保淡入淡出 Panel 已经实例化
        EnsureFadePanelInstance();
        if (_fadeImage == null)
        {
            Debug.LogError("[SwitchSceneManager] 没有找到用于淡入淡出的 Image，将直接执行回调。");
            onFadeComplete?.Invoke();
            return;
        }

        StartCoroutine(FadeInOutCoroutine(onFadeComplete, panelsToClose, panelsToOpen));
    }

    /// <summary>
    /// 协程：在当前场景中执行淡入淡出效果
    /// </summary>
    private IEnumerator FadeInOutCoroutine(System.Action onFadeComplete = null, List<string> panelsToClose = null, List<string> panelsToOpen = null)
    {
        _isFading = true;

        // 确保 Panel 处于打开状态并阻止点击
        if (_fadePanelInstance != null && !_fadePanelInstance.IsOpen)
        {
            _fadePanelInstance.Open();
        }

        _fadeImage.raycastTarget = true; // 阻止淡入淡出过程中的点击

        // 1. 从透明 -> 黑色（淡出当前场景）
        yield return Fade(0f, 1f);

        // 1.5. 黑屏已经完全挡住，此时再关闭指定的面板列表（如果有传）
        if (panelsToClose != null && panelsToClose.Count > 0 && UIManager.Instance != null)
        {
            foreach (string panelName in panelsToClose)
            {
                if (!string.IsNullOrEmpty(panelName))
                {
                    UIManager.Instance.ClosePanel(panelName);
                }
            }
        }

        // 2. 执行回调函数（例如：重新开始关卡）
        onFadeComplete?.Invoke();

        // 等待一帧，确保回调中的操作完成
        yield return null;

        // 2.5. 淡入前，打开指定的面板列表（如果有传）
        if (panelsToOpen != null && panelsToOpen.Count > 0 && UIManager.Instance != null)
        {
            foreach (string panelName in panelsToOpen)
            {
                if (!string.IsNullOrEmpty(panelName))
                {
                    UIManager.Instance.OpenPanel(panelName);
                }
            }
        }

        // 3. 从黑色 -> 透明（淡入显示新内容）
        yield return Fade(1f, 0f);

        _fadeImage.raycastTarget = false;

        // 完成后隐藏 Panel
        if (_fadePanelInstance != null && _fadePanelInstance.IsOpen)
        {
            _fadePanelInstance.Close();
        }

        _isFading = false;
    }

    /// <summary>
    /// 通用淡入淡出函数
    /// </summary>
    private IEnumerator Fade(float from, float to)
    {
        if (_fadeDuration <= 0f)
        {
            var cInstant = _fadeImage.color;
            cInstant.a = to;
            _fadeImage.color = cInstant;
            yield break;
        }

        float timer = 0f;
        while (timer < _fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _fadeDuration);
            float alpha = Mathf.Lerp(from, to, t);

            var c = _fadeImage.color;
            c.a = alpha;
            _fadeImage.color = c;

            yield return null;
        }

        // 保证最终值精准
        var cFinal = _fadeImage.color;
        cFinal.a = to;
        _fadeImage.color = cFinal;
    }

    /// <summary>
    /// 确保有一个淡入淡出用的 Panel 实例，并拿到其中的 Image。
    /// </summary>
    private void EnsureFadePanelInstance()
    {
        // 1. 先尝试使用在 Inspector 中直接拖过来的 FadePanel 引用
        if (_fadePanelInstance == null)
        {
            // 2. 如果没有拖引用，则通过 UIManager 用名字查找已经注册的 FadePanel
            if (UIManager.Instance != null && !string.IsNullOrEmpty(_fadePanelName))
            {
                _fadePanelInstance = UIManager.Instance.GetPanel<FadePanel>(_fadePanelName);
            }

            if (_fadePanelInstance == null)
            {
                Debug.LogError("[SwitchSceneManager] 没有找到场景中的 FadePanel（既没有拖引用，也没有通过 UIManager 找到）。");
                return;
            }
        }

        if (_fadeImage == null)
        {
            _fadeImage = _fadePanelInstance.FadeImage;
            if (_fadeImage == null)
            {
                Debug.LogError("[SwitchSceneManager] FadePanel 上没有设置 FadeImage。");
                return;
            }

            // 初始化为完全透明（不遮挡画面）
            var c = _fadeImage.color;
            c.a = 0f;
            _fadeImage.color = c;
            _fadeImage.raycastTarget = false;
        }
    }

    /// <summary>
    /// 场景加载完成时的回调，用于播放对应的BGM和同步游戏状态
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayBGMForScene(scene.name);
        
        // 同步游戏状态：切换到Menu场景时，确保状态为Start
        if (scene.name == "Menu" && GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMenu();
        }
    }

    /// <summary>
    /// 根据场景名称播放对应的BGM
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    private void PlayBGMForScene(string sceneName)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[SwitchSceneManager] AudioManager 实例不存在，无法播放BGM");
            return;
        }

        // 根据场景名称播放对应的BGM
        if (sceneName == "Menu")
        {
            AudioManager.Instance.PlayBGM("Menu");
        }
        else if (sceneName == "GameScene")
        {
            AudioManager.Instance.PlayBGM("Game");
        }
        // 可以根据需要添加更多场景的BGM映射
    }
}


