using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SettingPanel : BasePanel
{
    public override string PanelName => "SettingPanel";

    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _bgmButton;
    [SerializeField] private Button _fxButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private Button _replayButton;
    [SerializeField] private Sprite _toggleOnSprite;
    [SerializeField] private Sprite _toggleOffSprite;
    
    // BGM和FX按钮的Image组件，用于切换sprite
    private Image _bgmButtonImage;
    private Image _fxButtonImage;
    
    public override void Open()
    {
        base.Open();
        
        // 获取按钮的Image组件
        if (_bgmButton != null)
        {
            _bgmButtonImage = _bgmButton.GetComponent<Image>();
            _bgmButton.onClick.AddListener(OnBGMButtonClick);
        }
        
        if (_fxButton != null)
        {
            _fxButtonImage = _fxButton.GetComponent<Image>();
            _fxButton.onClick.AddListener(OnFXButtonClick);
        }
        
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseButtonClick);
        }
        
        if (_menuButton != null)
        {
            _menuButton.onClick.AddListener(OnMenuButtonClick);
        }
        
        if (_replayButton != null)
        {
            _replayButton.onClick.AddListener(OnReplayButtonClick);
        }
        
        // 判断当前是在menu还是game中
        bool isInMenu = GameManager.Instance != null && 
                        GameManager.Instance.CurrentState == GameManager.GameState.Start;
        
        // 如果在menu中，隐藏menuButton和replayButton
        if (_menuButton != null)
        {
            _menuButton.gameObject.SetActive(!isInMenu);
        }
        
        if (_replayButton != null)
        {
            _replayButton.gameObject.SetActive(!isInMenu);
        }
        
        // 更新BGM和FX按钮的sprite状态
        UpdateBGMButtonSprite();
        UpdateFXButtonSprite();
    }
    
    public override void Close()
    {
        // 解绑按钮事件
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(OnCloseButtonClick);
        }
        
        if (_bgmButton != null)
        {
            _bgmButton.onClick.RemoveListener(OnBGMButtonClick);
        }
        
        if (_fxButton != null)
        {
            _fxButton.onClick.RemoveListener(OnFXButtonClick);
        }
        
        if (_menuButton != null)
        {
            _menuButton.onClick.RemoveListener(OnMenuButtonClick);
        }
        
        if (_replayButton != null)
        {
            _replayButton.onClick.RemoveListener(OnReplayButtonClick);
        }
        
        base.Close();
    }
    
    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void OnCloseButtonClick()
    {
        PlayButtonSound(true); // 播放关闭按钮音效
        Close();
    }
    
    /// <summary>
    /// BGM按钮点击事件（toggle）
    /// </summary>
    private void OnBGMButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        if (AudioManager.Instance != null)
        {
            bool isEnabled = AudioManager.Instance.ToggleBGM();
            UpdateBGMButtonSprite();
            Debug.Log($"[SettingPanel] BGM切换为：{(isEnabled ? "开启" : "关闭")}");
        }
    }
    
    /// <summary>
    /// 音效按钮点击事件（toggle）
    /// </summary>
    private void OnFXButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        if (AudioManager.Instance != null)
        {
            bool isEnabled = AudioManager.Instance.ToggleFX();
            UpdateFXButtonSprite();
            Debug.Log($"[SettingPanel] 音效切换为：{(isEnabled ? "开启" : "关闭")}");
        }
    }
    
    /// <summary>
    /// 菜单按钮点击事件
    /// </summary>
    private void OnMenuButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        // 清理当前关卡的所有青蛙
        ClearAllFrogs();
        
        // 清除 StageManager 的网格数据
        if (StageManager.Instance != null)
        {
            StageManager.Instance.ClearGrid();
        }
        
        // 同步GameManager状态为Start
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMenu();
        }
        
        // 关闭SettingPanel
        Close();
        
        // 切换到 Menu 场景
        // 黑屏时关闭：SettingPanel 和 StagePanel
        // 场景切换后打开：MenuPanel
        if (SwitchSceneManager.Instance != null)
        {
            var panelsToClose = new List<string> { "SettingPanel", "StagePanel" };
            var panelsToOpen = new List<string> { "MenuPanel" };
            SwitchSceneManager.Instance.SwitchSceneWithFade("Menu", panelsToClose, panelsToOpen);
        }
        else
        {
            // 如果没有 SwitchSceneManager，直接关闭面板并切换场景
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClosePanel("SettingPanel");
                UIManager.Instance.ClosePanel("StagePanel");
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            // 等待场景加载后打开MenuPanel
            StartCoroutine(OpenMenuPanelAfterSceneLoad());
        }
    }
    
    /// <summary>
    /// 场景加载后打开MenuPanel（用于没有SwitchSceneManager的情况）
    /// </summary>
    private IEnumerator OpenMenuPanelAfterSceneLoad()
    {
        yield return new WaitForEndOfFrame();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel("MenuPanel");
        }
    }
    
    /// <summary>
    /// 重开关卡按钮点击事件
    /// </summary>
    private void OnReplayButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        // 关闭SettingPanel
        Close();
        
        // 调用StageManager的重开关卡方法
        if (StageManager.Instance != null)
        {
            StageManager.Instance.RestartStage();
        }
        else
        {
            Debug.LogWarning("[SettingPanel] StageManager.Instance 为 null，无法重开关卡。");
        }
    }
    
    /// <summary>
    /// 更新BGM按钮的sprite
    /// </summary>
    private void UpdateBGMButtonSprite()
    {
        if (_bgmButtonImage == null || _toggleOnSprite == null || _toggleOffSprite == null)
        {
            return;
        }
        
        bool isEnabled = AudioManager.Instance != null && AudioManager.Instance.IsBGMEnabled();
        _bgmButtonImage.sprite = isEnabled ? _toggleOnSprite : _toggleOffSprite;
    }
    
    /// <summary>
    /// 更新音效按钮的sprite
    /// </summary>
    private void UpdateFXButtonSprite()
    {
        if (_fxButtonImage == null || _toggleOnSprite == null || _toggleOffSprite == null)
        {
            return;
        }
        
        bool isEnabled = AudioManager.Instance != null && AudioManager.Instance.IsFXEnabled();
        _fxButtonImage.sprite = isEnabled ? _toggleOnSprite : _toggleOffSprite;
    }
    
    /// <summary>
    /// 清理场景中所有的青蛙对象
    /// </summary>
    private void ClearAllFrogs()
    {
        // 清理所有 NormalFrog（红绿青蛙）
        GreenRedFrog[] normalFrogs = FindObjectsOfType<GreenRedFrog>();
        if (normalFrogs != null && normalFrogs.Length > 0)
        {
            Debug.Log($"[SettingPanel] 清理 {normalFrogs.Length} 只 NormalFrog");
            foreach (var frog in normalFrogs)
            {
                if (frog != null && frog.gameObject != null)
                {
                    Destroy(frog.gameObject);
                }
            }
        }

        // 清理所有 YellowBlackFrog（黄黑青蛙）
        YellowBlackFrog[] yellowBlackFrogs = FindObjectsOfType<YellowBlackFrog>();
        if (yellowBlackFrogs != null && yellowBlackFrogs.Length > 0)
        {
            Debug.Log($"[SettingPanel] 清理 {yellowBlackFrogs.Length} 只 YellowBlackFrog");
            foreach (var frog in yellowBlackFrogs)
            {
                if (frog != null && frog.gameObject != null)
                {
                    Destroy(frog.gameObject);
                }
            }
        }
    }
}