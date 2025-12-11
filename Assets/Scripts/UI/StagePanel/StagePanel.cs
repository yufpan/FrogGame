using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StagePanel : BasePanel
{

    public override string PanelName => "StagePanel";

    [SerializeField] private Button _settingButton;
    [SerializeField] private Transform _healthLayout;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private GameObject _levelInfo;
    [SerializeField] private TextMeshProUGUI _stageCountText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private Button _freezeButton;
    [SerializeField] private Button _isolateButton;
    [SerializeField] private Button _bombButton;
    [SerializeField] private TextMeshProUGUI _bombText;

    [Header("冰冻Buff配置")]
    [Tooltip("冰冻Buff激活时显示的GameObject")]
    [SerializeField] private GameObject _freezeBuffObject;
    [Tooltip("冰冻状态时间遮罩图片（填充从1变为0表示时间进度）")]
    [SerializeField] private Image _freezeMaskImage;

    [Header("隔离Buff配置")]
    [Tooltip("隔离Buff激活时显示的GameObject")]
    [SerializeField] private GameObject _isolateBuffObject;
    [Tooltip("隔离状态时间遮罩图片（填充从1变为0表示时间进度）")]
    [SerializeField] private Image _isolateMaskImage;

    [Header("胜利演出配置")]
    [Tooltip("胜利时要生成并播放的特效预制体（可配置多个，会自动播放其自身及所有子物体上的粒子系统）")]
    [SerializeField] private List<GameObject> _winEffectPrefabs = new List<GameObject>();

    [Tooltip("胜利特效实例的父物体（可选，不设置则生成在场景根节点）")]
    [SerializeField] private Transform _winEffectParent;

    [Tooltip("从胜利判定到打开 WinPanel 的延迟时间（秒）")]
    [SerializeField] private float _winPanelDelay = 1.5f;
    
    private List<GameObject> _healthIcons = new List<GameObject>();
    private int _maxHealth = 6;
    private int _totalFrogCount = 0;
    private Coroutine _initializeCoroutine;
    private Coroutine _freezeCoroutine;
    private Coroutine _isolateCoroutine;
    private bool _isFrozen = false;
    private bool _isIsolated = false;
    private List<FrogBase> _isolatedFrogs = new List<FrogBase>();
    private GameObject _iceScreenInstance = null; // 冰冻屏幕特效实例
    private GameObject _fireScreenInstance = null; // 火焰屏幕特效实例
    private bool _isBombMode = false; // 是否处于投弹模式
    private Coroutine _bombClickCoroutine = null; // 投弹模式下的点击监听协程
    private Coroutine _bombTextBlinkCoroutine = null; // 炸弹文本闪烁协程

    public override void Open()
    {
        base.Open();
        
        // 等待场景切换完成，StageManager 存在后再初始化
        if (_initializeCoroutine != null)
        {
            StopCoroutine(_initializeCoroutine);
        }
        _initializeCoroutine = StartCoroutine(WaitForStageManagerAndInitialize());
    }

    /// <summary>
    /// 等待 StageManager 存在后再初始化
    /// </summary>
    private IEnumerator WaitForStageManagerAndInitialize()
    {
        // 等待 StageManager 存在（场景切换完成后）
        float timeout = 5f; // 超时时间5秒
        float elapsed = 0f;
        
        while (StageManager.Instance == null && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (StageManager.Instance == null)
        {
            Debug.LogWarning("[StagePanel] 等待 StageManager 超时，可能不在 GameScene 中。");
            yield break;
        }

        // StageManager 存在后，再等待一帧确保完全初始化
        yield return null;
        
        InitializeHealthIcons();
        SubscribeToEvents();
        UpdateUI();
        SetupButtonListeners();
        
        // 检查是否是第一关，如果是则打开教程面板
        CheckAndOpenTutorial();
        
        _initializeCoroutine = null;
    }

    /// <summary>
    /// 设置按钮监听
    /// </summary>
    private void SetupButtonListeners()
    {
        if (_settingButton != null)
        {
            _settingButton.onClick.RemoveAllListeners();
            _settingButton.onClick.AddListener(OnSettingButtonClicked);
        }


        if (_freezeButton != null)
        {
            _freezeButton.onClick.RemoveAllListeners();
            _freezeButton.onClick.AddListener(OnFreezeButtonClicked);
        }

        if (_isolateButton != null)
        {
            _isolateButton.onClick.RemoveAllListeners();
            _isolateButton.onClick.AddListener(OnIsolateButtonClicked);
        }

        if (_bombButton != null)
        {
            _bombButton.onClick.RemoveAllListeners();
            _bombButton.onClick.AddListener(OnBombButtonClicked);
        }
    }
    
    /// <summary>
    /// 检查是否是第一关，如果是则打开教程面板
    /// </summary>
    private void CheckAndOpenTutorial()
    {
        // 只在常规模式下且是第一关时才打开教程面板
        if (GameManager.Instance != null 
            && GameManager.Instance.CurrentLevel == 1 
            && GameManager.Instance.CurrentGameMode == GameManager.GameMode.Normal)
        {
            // 延迟一帧打开教程面板，确保 StagePanel 完全初始化
            StartCoroutine(OpenTutorialDelayed());
        }
    }
    
    /// <summary>
    /// 延迟打开教程面板
    /// </summary>
    private IEnumerator OpenTutorialDelayed()
    {
        yield return null; // 等待一帧
        
        if (UIManager.Instance != null)
        {
            TutorialPanel tutorialPanel = UIManager.Instance.GetPanel<TutorialPanel>("TutorialPanel");
            if (tutorialPanel != null)
            {
                // 检查是否是初次打开（玩家还没有进行过划框操作）
                bool isFirstTime = StageManager.Instance == null || !StageManager.Instance.HasPlayerDraggedBox();
                tutorialPanel.SetIsFirstTime(isFirstTime);
                UIManager.Instance.OpenPanel("TutorialPanel");
            }
        }
    }

    public override void Close()
    {
        // 停止初始化协程
        if (_initializeCoroutine != null)
        {
            StopCoroutine(_initializeCoroutine);
            _initializeCoroutine = null;
        }

        // 停止冻结协程并解除冻结
        if (_freezeCoroutine != null)
        {
            StopCoroutine(_freezeCoroutine);
            _freezeCoroutine = null;
        }
        if (_isFrozen)
        {
            FreezeAllFrogs(false);
            _isFrozen = false;
        }
        if (_freezeBuffObject != null)
        {
            _freezeBuffObject.SetActive(false);
        }
        if (_freezeMaskImage != null)
        {
            _freezeMaskImage.fillAmount = 1f;
        }

        // 销毁冰冻屏幕特效
        if (_iceScreenInstance != null)
        {
            Destroy(_iceScreenInstance);
            _iceScreenInstance = null;
        }

        // 退出投弹模式
        if (_isBombMode)
        {
            ExitBombMode();
        }

        // 停止隔离协程并解除隔离
        if (_isolateCoroutine != null)
        {
            StopCoroutine(_isolateCoroutine);
            _isolateCoroutine = null;
        }
        if (_isIsolated)
        {
            IsolateFrogs(false);
            _isIsolated = false;
        }
        if (_isolateBuffObject != null)
        {
            _isolateBuffObject.SetActive(false);
        }
        if (_isolateMaskImage != null)
        {
            _isolateMaskImage.fillAmount = 1f;
        }
        
        UnsubscribeFromEvents();
        base.Close();
    }

    protected override void OnDestroy()
    {
        // 停止初始化协程
        if (_initializeCoroutine != null)
        {
            StopCoroutine(_initializeCoroutine);
            _initializeCoroutine = null;
        }

        // 停止冻结协程并解除冻结
        if (_freezeCoroutine != null)
        {
            StopCoroutine(_freezeCoroutine);
            _freezeCoroutine = null;
        }
        if (_isFrozen)
        {
            FreezeAllFrogs(false);
            _isFrozen = false;
        }
        if (_freezeBuffObject != null)
        {
            _freezeBuffObject.SetActive(false);
        }
        if (_freezeMaskImage != null)
        {
            _freezeMaskImage.fillAmount = 1f;
        }

        // 销毁冰冻屏幕特效
        if (_iceScreenInstance != null)
        {
            Destroy(_iceScreenInstance);
            _iceScreenInstance = null;
        }

        // 退出投弹模式
        if (_isBombMode)
        {
            ExitBombMode();
        }

        // 停止隔离协程并解除隔离
        if (_isolateCoroutine != null)
        {
            StopCoroutine(_isolateCoroutine);
            _isolateCoroutine = null;
        }
        if (_isIsolated)
        {
            IsolateFrogs(false);
            _isIsolated = false;
        }
        if (_isolateBuffObject != null)
        {
            _isolateBuffObject.SetActive(false);
        }
        if (_isolateMaskImage != null)
        {
            _isolateMaskImage.fillAmount = 1f;
        }
        
        UnsubscribeFromEvents();
        
        // 调用基类的清理逻辑
        base.OnDestroy();
    }

    /// <summary>
    /// 订阅StageManager的事件
    /// </summary>
    private void SubscribeToEvents()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnHealthChanged += OnHealthChanged;
            StageManager.Instance.OnFrogCountChanged += OnFrogCountChanged;
            StageManager.Instance.OnStageVictory += OnStageVictory;
            StageManager.Instance.OnStageFailed += OnStageFailed;
            StageManager.Instance.OnScoreChanged += OnScoreChanged;
        }

        // 订阅 GameManager 的状态变化事件，当进入 Playing 状态时刷新关卡号
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
        }
    }

    /// <summary>
    /// 取消订阅StageManager的事件
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnHealthChanged -= OnHealthChanged;
            StageManager.Instance.OnFrogCountChanged -= OnFrogCountChanged;
            StageManager.Instance.OnStageVictory -= OnStageVictory;
            StageManager.Instance.OnStageFailed -= OnStageFailed;
            StageManager.Instance.OnScoreChanged -= OnScoreChanged;
        }

        // 取消订阅 GameManager 的状态变化事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }
    }

    /// <summary>
    /// 初始化血量图标
    /// </summary>
    private void InitializeHealthIcons()
    {
        // 清除现有列表
        _healthIcons.Clear();

        if (_healthLayout == null)
        {
            Debug.LogWarning("[StagePanel] _healthLayout 未设置，无法初始化血量图标。");
            return;
        }

        // 从 _healthLayout 的子物体中获取所有 Image 组件（包括未激活的对象）
        Image[] images = _healthLayout.GetComponentsInChildren<Image>(true);
        
        if (images == null || images.Length == 0)
        {
            Debug.LogWarning("[StagePanel] 在 _healthLayout 的子物体中未找到 Image 组件。");
            return;
        }

        // 将 Image 的 GameObject 添加到列表中
        foreach (Image img in images)
        {
            if (img != null && img.gameObject != null)
            {
                _healthIcons.Add(img.gameObject);
            }
        }

        // 获取初始血量（如果StageManager已初始化，使用实际值；否则使用找到的图标数量）
        if (StageManager.Instance != null)
        {
            _maxHealth = StageManager.Instance.GetCurrentHealth();
        }
        else
        {
            _maxHealth = _healthIcons.Count; // 使用找到的图标数量作为默认值
        }

        Debug.Log($"[StagePanel] 初始化血量图标：找到 {_healthIcons.Count} 个 HeartIcon，初始血量：{_maxHealth}");
    }

    /// <summary>
    /// 更新UI
    /// </summary>
    private void UpdateUI()
    {
        if (StageManager.Instance != null)
        {
            UpdateHealthDisplay(StageManager.Instance.GetCurrentHealth());
            
            // 检查是否为每日挑战模式
            bool isDailyChallenge = StageManager.Instance.IsDailyChallengeMode();
            
            if (isDailyChallenge)
            {
                // 每日挑战模式：隐藏进度条和关卡信息，显示分数
                if (_progressSlider != null)
                {
                    _progressSlider.gameObject.SetActive(false);
                }
                if (_progressText != null)
                {
                    _progressText.gameObject.SetActive(false);
                }
                if (_levelInfo != null)
                {
                    _levelInfo.SetActive(false);
                }
                if (_scoreText != null)
                {
                    _scoreText.gameObject.SetActive(true);
                    UpdateScoreDisplay(StageManager.Instance.GetDailyChallengeScore());
                }
            }
            else
            {
                // 常规关卡模式：显示进度条和关卡信息，隐藏分数
                if (_progressSlider != null)
                {
                    _progressSlider.gameObject.SetActive(true);
                }
                if (_progressText != null)
                {
                    _progressText.gameObject.SetActive(true);
                }
                if (_levelInfo != null)
                {
                    _levelInfo.SetActive(true);
                }
                if (_scoreText != null)
                {
                    _scoreText.gameObject.SetActive(false);
                }
                
                _totalFrogCount = StageManager.Instance.GetTotalFrogCount();
                UpdateProgressBar();
            }
            
            UpdateStageCountText();
        }
    }

    /// <summary>
    /// 血量变化回调
    /// </summary>
    private void OnHealthChanged(int currentHealth)
    {
        Debug.Log($"[StagePanel] OnHealthChanged: currentHealth = {currentHealth}");
        UpdateHealthDisplay(currentHealth);
    }

    /// <summary>
    /// 更新血量显示
    /// </summary>
    private void UpdateHealthDisplay(int currentHealth)
    {
        Debug.Log($"[StagePanel] UpdateHealthDisplay: currentHealth = {currentHealth}, _healthIcons.Count = {_healthIcons.Count}");
        for (int i = 0; i < _healthIcons.Count; i++)
        {
            if (_healthIcons[i] != null)
            {
                _healthIcons[i].SetActive(i < currentHealth);
            }
        }
    }

    /// <summary>
    /// 青蛙数量变化回调
    /// </summary>
    private void OnFrogCountChanged(int remaining, int total)
    {
        _totalFrogCount = total;
        UpdateProgressBar();
    }

    /// <summary>
    /// 更新进度条
    /// </summary>
    private void UpdateProgressBar()
    {
        if (_progressSlider == null) return;

        if (StageManager.Instance != null && _totalFrogCount > 0)
        {
            int remaining = StageManager.Instance.GetRemainingFrogCount();
            float progress = 1f - (float)remaining / _totalFrogCount;
            _progressSlider.value = progress;
            
            // 更新进度文本（保留到个位的百分数，使用向下取整避免出现100%但未完成的情况）
            if (_progressText != null)
            {
                int progressPercent = Mathf.FloorToInt(progress * 100f);
                _progressText.text = $"{progressPercent}%";
            }
        }
        else
        {
            _progressSlider.value = 0f;
            if (_progressText != null)
            {
                _progressText.text = "0%";
            }
        }
    }


    /// <summary>
    /// 更新关卡号显示（使用sprite格式）
    /// </summary>
    private void UpdateStageCountText()
    {
        if (_stageCountText == null) return;

        int currentLevel = 1;
        if (GameManager.Instance != null)
        {
            currentLevel = GameManager.Instance.CurrentLevel;
        }

        // 将关卡号转换为sprite格式，例如：<sprite name=Num_1><sprite name=Num_2>
        string levelString = currentLevel.ToString();
        Debug.Log($"[StagePanel] UpdateStageCountText: levelString = {levelString}");
        string spriteText = "";
        
        foreach (char digit in levelString)
        {
            spriteText += $"<sprite name=Num_{digit}>";
        }
        
        _stageCountText.text = spriteText;
    }

    /// <summary>
    /// 公开方法：刷新关卡号显示（供外部调用，例如在连续进入下一关时）
    /// </summary>
    public void RefreshStageCount()
    {
        UpdateStageCountText();
    }

    /// <summary>
    /// GameManager 状态变化回调
    /// </summary>
    private void OnGameStateChanged(GameManager.GameState newState)
    {
        // 当进入 Playing 状态时，刷新关卡号显示
        if (newState == GameManager.GameState.Playing)
        {
            UpdateStageCountText();
        }
    }

    /// <summary>
    /// 分数变化回调（每日挑战模式）
    /// </summary>
    private void OnScoreChanged(int score)
    {
        UpdateScoreDisplay(score);
    }

    /// <summary>
    /// 更新分数显示
    /// </summary>
    private void UpdateScoreDisplay(int score)
    {
        if (_scoreText != null)
        {
            _scoreText.text = $"分数: {score}";
        }
    }

    /// <summary>
    /// 关卡胜利回调
    /// </summary>
    private void OnStageVictory()
    {
        // 每日挑战模式下不应该触发胜利
        if (StageManager.Instance != null && StageManager.Instance.IsDailyChallengeMode())
        {
            return;
        }
        
        Debug.Log("[StagePanel] 关卡胜利！开始胜利演出。");
        
        // 关卡胜利后，立即更新关卡进度并保存
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageWin();
            GameManager.Instance.EndGame();
        }
        
        StartCoroutine(PlayWinSequenceCoroutine());
    }

    /// <summary>
    /// 胜利演出：先播放粒子特效，再延迟一段时间后打开 WinPanel
    /// </summary>
    private IEnumerator PlayWinSequenceCoroutine()
    {
        // 生成并播放所有配置好的胜利特效预制体（实例及其子物体上的粒子系统）
        if (_winEffectPrefabs != null)
        {
            foreach (var prefab in _winEffectPrefabs)
            {
                if (prefab == null) continue;

                GameObject instance;
                if (_winEffectParent != null)
                {
                    instance = Instantiate(prefab, _winEffectParent);
                }
                else
                {
                    instance = Instantiate(prefab);
                }

                if (instance == null) continue;

                // 确保实例激活
                if (!instance.activeSelf)
                {
                    instance.SetActive(true);
                }

                // 获取自身及所有子物体上的粒子系统
                ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particleSystems)
                {
                    if (ps == null) continue;

                    if (!ps.gameObject.activeSelf)
                    {
                        ps.gameObject.SetActive(true);
                    }

                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play();
                }
            }
        }

        // 等待配置的时间（防止负数）
        if (_winPanelDelay > 0f)
        {
            yield return new WaitForSeconds(_winPanelDelay);
        }

        // 打开胜利面板
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel("WinPanel");
        }
    }

    /// <summary>
    /// 关卡失败回调
    /// </summary>
    private void OnStageFailed(StageManager.FailureType failureType)
    {
        Debug.Log($"[StagePanel] 关卡失败！失败类型：{failureType}");
        
        // 同步GameManager状态为Result
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndGame();
        }
        
        // 根据失败类型弹出不同的面板
        if (UIManager.Instance != null)
        {
            if (failureType == StageManager.FailureType.HealthDepleted)
            {
                // 血量归0，弹出 DeadPanel
                UIManager.Instance.OpenPanel("DeadPanel");
            }
            else if (failureType == StageManager.FailureType.TimeOut)
            {
                // 时间归零，弹出 TimeOutPanel
                UIManager.Instance.OpenPanel("TimeOutPanel");
            }
        }
    }

    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    private void OnSettingButtonClicked()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel("SettingPanel");
            Debug.Log("[StagePanel] 设置按钮被点击，打开设置面板");
        }
    }


    /// <summary>
    /// 冻结按钮点击事件
    /// </summary>
    private void OnFreezeButtonClicked()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        if (_isFrozen)
        {
            Debug.Log("[StagePanel] 冻结状态已激活，无法重复激活");
            return;
        }

        if (StageManager.Instance == null) return;

        // 检查广告是否可用
        if (ADManager.Instance == null || !ADManager.Instance.IsAdAvailable())
        {
            Debug.LogWarning("[StagePanel] 广告不可用，无法使用冰冻功能");
            return;
        }

        // 通过广告触发冰冻功能
        ADManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                // 用户看完广告，执行冰冻功能
                if (_freezeCoroutine != null)
                {
                    StopCoroutine(_freezeCoroutine);
                }
                _freezeCoroutine = StartCoroutine(FreezeFrogsCoroutine(30f));
                Debug.Log("[StagePanel] 广告观看完成，开始冻结30秒");
            },
            onFailed: (errorMsg) =>
            {
                // 用户未看完广告或广告失败，不做任何事情
                Debug.Log($"[StagePanel] 广告未完成，无法使用冰冻功能: {errorMsg}");
            }
        );
    }

    /// <summary>
    /// 冻结青蛙协程
    /// </summary>
    /// <param name="duration">冻结持续时间（秒）</param>
    private IEnumerator FreezeFrogsCoroutine(float duration)
    {
        _isFrozen = true;

        // 冻结所有会变色的青蛙
        FreezeAllFrogs(true);

        // 激活冰冻Buff GameObject
        if (_freezeBuffObject != null)
        {
            _freezeBuffObject.SetActive(true);
        }

        // 初始化遮罩填充为1
        if (_freezeMaskImage != null)
        {
            _freezeMaskImage.fillAmount = 1f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 检查是否所有绿色青蛙都被消除了
            if (StageManager.Instance != null)
            {
                int greenFrogCount = StageManager.Instance.GetRemainingGreenFrogCount();
                if (greenFrogCount == 0)
                {
                    Debug.Log("[StagePanel] 所有绿色青蛙已被消除，提前解除冻结");
                    break;
                }
            }

            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // 更新遮罩填充（从1变为0）
            if (_freezeMaskImage != null)
            {
                _freezeMaskImage.fillAmount = 1f - progress;
            }

            yield return null;
        }

        // 解除冻结
        FreezeAllFrogs(false);
        _isFrozen = false;

        // 取消激活冰冻Buff GameObject
        if (_freezeBuffObject != null)
        {
            _freezeBuffObject.SetActive(false);
        }

        // 重置遮罩填充
        if (_freezeMaskImage != null)
        {
            _freezeMaskImage.fillAmount = 1f;
        }

        // 销毁冰冻屏幕特效
        if (_iceScreenInstance != null)
        {
            Destroy(_iceScreenInstance);
            _iceScreenInstance = null;
        }

        _freezeCoroutine = null;
        Debug.Log("[StagePanel] 冻结状态已解除");
    }

    /// <summary>
    /// 冻结或解冻所有会变色的青蛙
    /// </summary>
    /// <param name="frozen">是否冻结</param>
    private void FreezeAllFrogs(bool frozen)
    {
        if (StageManager.Instance == null) return;

        // 查找所有NormalFrog
        GreenRedFrog[] normalFrogs = FindObjectsOfType<GreenRedFrog>();
        foreach (var normalFrog in normalFrogs)
        {
            if (normalFrog != null && normalFrog.ColorType == GreenRedFrog.FrogColorType.Red)
            {
                // 只冻结会变色的红色青蛙（不包括绿色青蛙）
                normalFrog.SetFrozen(frozen);
            }
        }
    }

    /// <summary>
    /// 隔离按钮点击事件
    /// </summary>
    private void OnIsolateButtonClicked()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        if (_isIsolated)
        {
            Debug.Log("[StagePanel] 隔离状态已激活，无法重复激活");
            return;
        }

        if (StageManager.Instance == null) return;

        // 检查广告是否可用
        if (ADManager.Instance == null || !ADManager.Instance.IsAdAvailable())
        {
            Debug.LogWarning("[StagePanel] 广告不可用，无法使用隔离功能");
            return;
        }

        // 通过广告触发隔离功能
        ADManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                // 用户看完广告，执行隔离功能
                if (_isolateCoroutine != null)
                {
                    StopCoroutine(_isolateCoroutine);
                }
                _isolateCoroutine = StartCoroutine(IsolateFrogsCoroutine(30f));
                Debug.Log("[StagePanel] 广告观看完成，开始隔离30秒");
            },
            onFailed: (errorMsg) =>
            {
                // 用户未看完广告或广告失败，不做任何事情
                Debug.Log($"[StagePanel] 广告未完成，无法使用隔离功能: {errorMsg}");
            }
        );
    }

    /// <summary>
    /// 隔离青蛙协程
    /// </summary>
    /// <param name="duration">隔离持续时间（秒）</param>
    private IEnumerator IsolateFrogsCoroutine(float duration)
    {
        _isIsolated = true;

        // 随机选择场上一半的青蛙进行隔离
        List<FrogBase> allFrogs = new List<FrogBase>();
        
        // 查找所有青蛙
        GreenRedFrog[] greenRedFrogs = FindObjectsOfType<GreenRedFrog>();
        YellowBlackFrog[] yellowBlackFrogs = FindObjectsOfType<YellowBlackFrog>();
        
        foreach (var frog in greenRedFrogs)
        {
            if (frog != null)
            {
                allFrogs.Add(frog);
            }
        }
        
        foreach (var frog in yellowBlackFrogs)
        {
            if (frog != null)
            {
                allFrogs.Add(frog);
            }
        }

        if (allFrogs.Count == 0)
        {
            Debug.LogWarning("[StagePanel] 场上没有青蛙，无法进行隔离");
            _isIsolated = false;
            _isolateCoroutine = null;
            yield break;
        }

        // 随机打乱数组
        for (int i = allFrogs.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            FrogBase temp = allFrogs[i];
            allFrogs[i] = allFrogs[j];
            allFrogs[j] = temp;
        }

        // 选择一半的青蛙进行隔离
        int isolateCount = Mathf.CeilToInt(allFrogs.Count * 0.5f);
        _isolatedFrogs.Clear();
        
        for (int i = 0; i < isolateCount && i < allFrogs.Count; i++)
        {
            if (allFrogs[i] != null)
            {
                allFrogs[i].SetIsolated(true);
                _isolatedFrogs.Add(allFrogs[i]);
            }
        }

        Debug.Log($"[StagePanel] 隔离了 {_isolatedFrogs.Count} 只青蛙（总共 {allFrogs.Count} 只）");

        // 激活隔离Buff GameObject
        if (_isolateBuffObject != null)
        {
            _isolateBuffObject.SetActive(true);
        }

        // 初始化遮罩填充为1
        if (_isolateMaskImage != null)
        {
            _isolateMaskImage.fillAmount = 1f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 检查是否所有非隔离的青蛙都被消灭了
            int nonIsolatedFrogCount = GetNonIsolatedFrogCount();
            if (nonIsolatedFrogCount == 0)
            {
                Debug.Log("[StagePanel] 所有非隔离青蛙已被消灭，提前解除隔离");
                break;
            }

            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // 更新遮罩填充（从1变为0）
            if (_isolateMaskImage != null)
            {
                _isolateMaskImage.fillAmount = 1f - progress;
            }

            yield return null;
        }

        // 解除隔离
        IsolateFrogs(false);
        _isIsolated = false;

        // 取消激活隔离Buff GameObject
        if (_isolateBuffObject != null)
        {
            _isolateBuffObject.SetActive(false);
        }

        // 重置遮罩填充
        if (_isolateMaskImage != null)
        {
            _isolateMaskImage.fillAmount = 1f;
        }

        _isolateCoroutine = null;
        Debug.Log("[StagePanel] 隔离状态已解除");
    }

    /// <summary>
    /// 隔离或解除隔离青蛙
    /// </summary>
    /// <param name="isolated">是否隔离</param>
    private void IsolateFrogs(bool isolated)
    {
        foreach (var frog in _isolatedFrogs)
        {
            if (frog != null)
            {
                frog.SetIsolated(isolated);
            }
        }

        if (!isolated)
        {
            _isolatedFrogs.Clear();
        }
    }

    /// <summary>
    /// 获取非隔离的青蛙数量
    /// </summary>
    private int GetNonIsolatedFrogCount()
    {
        int count = 0;
        
        // 查找所有青蛙
        GreenRedFrog[] greenRedFrogs = FindObjectsOfType<GreenRedFrog>();
        YellowBlackFrog[] yellowBlackFrogs = FindObjectsOfType<YellowBlackFrog>();
        
        // 统计非隔离的青蛙数量
        foreach (var frog in greenRedFrogs)
        {
            if (frog != null && !_isolatedFrogs.Contains(frog))
            {
                count++;
            }
        }
        
        foreach (var frog in yellowBlackFrogs)
        {
            if (frog != null && !_isolatedFrogs.Contains(frog))
            {
                count++;
            }
        }
        
        return count;
    }

    /// <summary>
    /// 炸弹按钮点击事件
    /// </summary>
    private void OnBombButtonClicked()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        if (_isBombMode)
        {
            Debug.Log("[StagePanel] 投弹模式已激活，无法重复激活");
            return;
        }

        if (StageManager.Instance == null) return;

        // 检查广告是否可用
        if (ADManager.Instance == null || !ADManager.Instance.IsAdAvailable())
        {
            Debug.LogWarning("[StagePanel] 广告不可用，无法使用炸弹功能");
            return;
        }

        // 通过广告触发炸弹功能
        ADManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                // 用户看完广告，执行炸弹功能
                EnterBombMode();
                Debug.Log("[StagePanel] 广告观看完成，进入投弹模式");
            },
            onFailed: (errorMsg) =>
            {
                // 用户未看完广告或广告失败，不做任何事情
                Debug.Log($"[StagePanel] 广告未完成，无法使用炸弹功能: {errorMsg}");
            }
        );
    }

    /// <summary>
    /// 进入投弹模式
    /// </summary>
    private void EnterBombMode()
    {
        _isBombMode = true;

        // 通知StageManager进入投弹模式
        if (StageManager.Instance != null)
        {
            StageManager.Instance.EnterBombMode();
        }

        // 禁用划框功能
        if (DragBoxManager.Instance != null)
        {
            DragBoxManager.Instance.SetDragBoxEnabled(false);
        }

        // 激活并开始闪烁炸弹文本
        if (_bombText != null)
        {
            _bombText.gameObject.SetActive(true);
            if (_bombTextBlinkCoroutine != null)
            {
                StopCoroutine(_bombTextBlinkCoroutine);
            }
            _bombTextBlinkCoroutine = StartCoroutine(BombTextBlinkCoroutine());
        }

        // 开始监听点击事件（等待一帧，避免捕获按钮点击事件）
        if (_bombClickCoroutine != null)
        {
            StopCoroutine(_bombClickCoroutine);
        }
        _bombClickCoroutine = StartCoroutine(EnterBombModeCoroutine());

        Debug.Log("[StagePanel] 进入投弹模式");
    }

    /// <summary>
    /// 退出投弹模式
    /// </summary>
    private void ExitBombMode()
    {
        _isBombMode = false;

        // 停止点击监听协程
        if (_bombClickCoroutine != null)
        {
            StopCoroutine(_bombClickCoroutine);
            _bombClickCoroutine = null;
        }

        // 停止文本闪烁协程并隐藏文本
        if (_bombTextBlinkCoroutine != null)
        {
            StopCoroutine(_bombTextBlinkCoroutine);
            _bombTextBlinkCoroutine = null;
        }
        if (_bombText != null)
        {
            _bombText.gameObject.SetActive(false);
        }

        // 通知StageManager退出投弹模式
        if (StageManager.Instance != null)
        {
            StageManager.Instance.ExitBombMode();
        }

        // 启用划框功能
        if (DragBoxManager.Instance != null)
        {
            DragBoxManager.Instance.SetDragBoxEnabled(true);
        }

        // 销毁火焰屏幕特效
        if (_fireScreenInstance != null)
        {
            Destroy(_fireScreenInstance);
            _fireScreenInstance = null;
        }

        Debug.Log("[StagePanel] 退出投弹模式");
    }

    /// <summary>
    /// 进入投弹模式的协程（等待一帧后开始监听点击，避免捕获按钮点击事件）
    /// </summary>
    private IEnumerator EnterBombModeCoroutine()
    {
        // 等待一帧，确保按钮点击事件已经处理完毕
        yield return null;
        
        // 开始监听点击事件
        yield return StartCoroutine(BombClickListenerCoroutine());
    }

    /// <summary>
    /// 投弹模式下的点击监听协程
    /// </summary>
    private IEnumerator BombClickListenerCoroutine()
    {
        // 用于接收点击事件的变量
        Vector2? clickPosition = null;
        
        // 订阅 TouchManager 的点击事件
        void OnBombClick(Vector2 screenPos)
        {
            clickPosition = screenPos;
        }
        
        if (TouchManager.Instance != null)
        {
            TouchManager.Instance.OnClick += OnBombClick;
        }
        else
        {
            Debug.LogError("[StagePanel] TouchManager.Instance 为空，无法监听点击事件");
            yield break;
        }

        try
        {
            while (_isBombMode)
            {
                // 检查游戏状态
                if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                {
                    ExitBombMode();
                    yield break;
                }

                if (StageManager.Instance != null && StageManager.Instance.IsStageEnded())
                {
                    ExitBombMode();
                    yield break;
                }

                // 检查StageManager是否已退出投弹模式（炸弹爆炸处理完成后会退出）
                if (StageManager.Instance != null && !StageManager.Instance.IsBombMode())
                {
                    ExitBombMode();
                    yield break;
                }

                // 检查是否有点击事件
                if (clickPosition.HasValue)
                {
                    Vector2 currentPos = clickPosition.Value;
                    clickPosition = null; // 重置标志

                    // 将屏幕坐标转换为世界坐标
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        float distanceToZ0 = -mainCamera.transform.position.z;
                        Vector3 screenPos = new Vector3(currentPos.x, currentPos.y, distanceToZ0);
                        Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
                        worldPos.z = 0f;

                        // 在点击位置生成炸弹
                        if (StageManager.Instance != null)
                        {
                            StageManager.Instance.SpawnBomb(worldPos);
                        }
                    }
                }

                yield return null;
            }
        }
        finally
        {
            // 取消订阅事件
            if (TouchManager.Instance != null)
            {
                TouchManager.Instance.OnClick -= OnBombClick;
            }
        }
    }

    /// <summary>
    /// 炸弹文本红白交替闪烁协程
    /// </summary>
    private IEnumerator BombTextBlinkCoroutine()
    {
        if (_bombText == null) yield break;

        Color whiteColor = Color.white;
        Color redColor = Color.red;
        float blinkInterval = 0.5f; // 闪烁间隔（秒）

        while (_isBombMode && _bombText != null)
        {
            // 切换到红色
            _bombText.color = redColor;
            yield return new WaitForSeconds(blinkInterval);

            // 切换到白色
            _bombText.color = whiteColor;
            yield return new WaitForSeconds(blinkInterval);
        }
    }
}