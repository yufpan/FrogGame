using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class MenuPanel : BasePanel
{
    public override string PanelName => "MenuPanel";
    [SerializeField] private Button _settingButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _stageCountText;
    [SerializeField] private TextMeshProUGUI _energyText;
    [SerializeField] private TextMeshProUGUI _coinText;
    [SerializeField] private GameObject _startText;
    
    [Header("开始文本动画配置")]
    [Tooltip("缩放动画持续时间（秒）")]
    [SerializeField] private float _scaleAnimationDuration = 1f;
    
    private Coroutine _startTextScaleCoroutine;
    public override void Open()
    {
        base.Open();

        _startButton.onClick.AddListener(OnStartButtonClick);
        _settingButton.onClick.AddListener(OnSettingButtonClick);
        // 显示当前关卡进度
        int stageCount = GameManager.Instance.CurrentLevel;
        _stageCountText.text = $"第 {stageCount} 关";

        // 更新体力显示
        UpdateEnergyText();

        // 更新金币显示
        UpdateCoinText();

        // 订阅体力变化事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnEnergyChanged += OnEnergyChanged;
            GameManager.Instance.OnCoinsChanged += OnCoinsChanged;
        }
        
        // 启动开始文本缩放动画
        if (_startText != null)
        {
            _startText.transform.localScale = Vector3.one;
            if (_startTextScaleCoroutine != null)
            {
                StopCoroutine(_startTextScaleCoroutine);
            }
            _startTextScaleCoroutine = StartCoroutine(StartTextScaleAnimation());
        }
    }

    public override void Close()
    {
        _startButton.onClick.RemoveListener(OnStartButtonClick);
        _settingButton.onClick.RemoveListener(OnSettingButtonClick);
        
        // 取消订阅体力变化事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnEnergyChanged -= OnEnergyChanged;
            GameManager.Instance.OnCoinsChanged -= OnCoinsChanged;
        }
        
        // 停止开始文本缩放动画
        if (_startTextScaleCoroutine != null)
        {
            StopCoroutine(_startTextScaleCoroutine);
            _startTextScaleCoroutine = null;
        }
        if (_startText != null)
        {
            _startText.transform.localScale = Vector3.one;
        }
        
        base.Close();
    }
    private void OnStartButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        // 不再在这里检查体力，而是交给 StartGame 内部统一检查
        // StartGame 内部会检查体力，如果不足会自动打开 GetEnergyPanel 并返回 false
        // 只有在 StartGame 返回 true 时才切换场景，避免场景切换后弹出 GetEnergyPanel
        if (GameManager.Instance != null)
        {
            bool success = GameManager.Instance.StartGame();
            if (success)
            {
                // 不再在这里立即关闭菜单面板，而是交给 SwitchSceneManager
                // 在黑屏完全盖住之后再关闭，避免 UI 突然消失的视觉突兀
                SwitchSceneManager.Instance.SwitchSceneWithFade("GameScene", new List<string> { "MenuPanel" }, new List<string> { "StagePanel" });
            }
            // 如果 StartGame 返回 false（体力不足），GetEnergyPanel 已经在 StartGame 内部打开了，这里不需要做任何操作
        }
    }
    
    /// <summary>
    /// 更新体力文本显示
    /// </summary>
    private void UpdateEnergyText()
    {
        if (_energyText != null && GameManager.Instance != null)
        {
            int currentEnergy = GameManager.Instance.CurrentEnergy;
            int maxEnergy = GameManager.Instance.MaxEnergy;
            _energyText.text = $"{currentEnergy}/{maxEnergy}";
        }
    }
    
    /// <summary>
    /// 体力变化事件回调
    /// </summary>
    private void OnEnergyChanged(int newEnergy)
    {
        UpdateEnergyText();
    }

    /// <summary>
    /// 更新金币文本显示
    /// </summary>
    private void UpdateCoinText()
    {
        if (_coinText != null && GameManager.Instance != null)
        {
            int currentCoins = GameManager.Instance.CurrentCoins;
            _coinText.text = currentCoins.ToString();
        }
    }

    /// <summary>
    /// 金币变化事件回调
    /// </summary>
    private void OnCoinsChanged(int newCoins)
    {
        UpdateCoinText();
    }
    private void OnSettingButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        UIManager.Instance.OpenPanel("SettingPanel");
    }
    
    /// <summary>
    /// 开始文本持续缩放动画协程（scale 从 1 到 1.2，循环）
    /// </summary>
    private IEnumerator StartTextScaleAnimation()
    {
        if (_startText == null) yield break;
        
        Vector3 minScale = Vector3.one;
        Vector3 maxScale = Vector3.one * 1.2f;
        float duration = Mathf.Max(0.01f, _scaleAnimationDuration);
        
        while (true)
        {
            // 从 1 缩放到 1.2
            float time = 0f;
            while (time < duration)
            {
                if (_startText == null) yield break;
                
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                // 使用平滑的插值曲线（ease in-out）
                t = t * t * (3f - 2f * t);
                _startText.transform.localScale = Vector3.Lerp(minScale, maxScale, t);
                yield return null;
            }
            
            // 从 1.2 缩放回 1
            time = 0f;
            while (time < duration)
            {
                if (_startText == null) yield break;
                
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                // 使用平滑的插值曲线（ease in-out）
                t = t * t * (3f - 2f * t);
                _startText.transform.localScale = Vector3.Lerp(maxScale, minScale, t);
                yield return null;
            }
        }
    }
}