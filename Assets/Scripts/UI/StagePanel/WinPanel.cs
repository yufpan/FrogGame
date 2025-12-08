using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WinPanel : BasePanel
{
    public override string PanelName => "WinPanel";

    [SerializeField] private Button _backButton;
    [SerializeField] private Button _nextStageButton;
    [SerializeField] private TextMeshProUGUI _finishTimerText;
    [SerializeField] private TextMeshProUGUI _unlockItemText;
    [SerializeField] private Image _unlockItemIcon;



    public override void Open()
    {
        // 绑定按钮事件
        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClick);
        }

        if (_nextStageButton != null)
        {
            _nextStageButton.onClick.AddListener(OnNextStageButtonClick);
        }
        
        // 更新通关时间显示
        UpdateFinishTimer();
        
        // 更新解锁物品信息显示
        UpdateUnlockItemInfo();

        // 播放胜利音效
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PauseBGMPlayFXAndResume("Win");
        }
        
        base.Open();

    }

    public override void Close()
    {
        // 解绑按钮事件
        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClick);
        }
        
        if (_nextStageButton != null)
        {
            _nextStageButton.onClick.RemoveListener(OnNextStageButtonClick);
        }
        
        base.Close();
    }

    /// <summary>
    /// 返回大厅按钮点击事件
    /// </summary>
    private void OnBackButtonClick()
    {
        PlayButtonSound(true); // 播放关闭按钮音效
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
        
        // 切换到 Menu 场景
        // 黑屏时关闭：WinPanel 和 StagePanel
        // 场景切换后打开：MenuPanel
        if (SwitchSceneManager.Instance != null)
        {
            var panelsToClose = new List<string> { "WinPanel", "StagePanel" };
            var panelsToOpen = new List<string> { "MenuPanel" };
            SwitchSceneManager.Instance.SwitchSceneWithFade("Menu", panelsToClose, panelsToOpen);
        }
        else
        {
            // 如果没有 SwitchSceneManager，直接关闭面板并切换场景
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClosePanel("WinPanel");
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
    /// 下一关按钮点击事件
    /// </summary>
    private void OnNextStageButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        // 检查体力
        if (GameManager.Instance != null && !GameManager.Instance.HasEnoughEnergy())
        {
            // 打开GetEnergyPanel
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenPanel("GetEnergyPanel");
            }
            return;
        }
        
        // 读取当前关卡数（胜利时已经自动更新了）
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;
        
        Debug.Log($"[WinPanel] 准备进入下一关：关卡 {currentLevel}");
        
        // 使用淡入淡出效果进入下一关
        if (SwitchSceneManager.Instance != null)
        {
            // 准备要关闭的面板列表
            var panelsToClose = new List<string> { "WinPanel" };
            
            // 执行淡入淡出，在黑屏时执行进入下一关的逻辑
            SwitchSceneManager.Instance.FadeInOut(() => {
                // 清理当前关卡的所有青蛙
                ClearAllFrogs();
                
                // 清除 StageManager 的网格数据
                if (StageManager.Instance != null)
                {
                    StageManager.Instance.ClearGrid();
                }
                
                // 先结束当前游戏（设置状态为 Result），以便可以重新开始
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EndGame();
                }
                
                // 开始下一关（关卡已经在胜利时自动更新了）
                // 注意：由于 StartGame 会检查状态，我们需要先 EndGame 才能再次 StartGame
                if (GameManager.Instance != null)
                {
                    bool success = GameManager.Instance.StartGame(currentLevel);
                    if (!success)
                    {
                        // 如果StartGame失败（体力不足），直接返回
                        return;
                    }
                }
                
                // 重新生成下一关的青蛙
                // 注意：GenMobManager.Start 只会在场景加载时自动调用
                // 在同一个场景中切换关卡时，需要手动调用 SpawnLevelMobs
                if (GenMobManager.Instance != null)
                {
                    GenMobManager.Instance.SpawnLevelMobs(currentLevel);
                }
                else
                {
                    Debug.LogWarning("[WinPanel] GenMobManager.Instance 为 null，无法生成下一关。");
                }

                // 刷新 StagePanel 的关卡号显示
                if (UIManager.Instance != null)
                {
                    BasePanel stagePanel = UIManager.Instance.GetPanel("StagePanel");
                    if (stagePanel is StagePanel panel)
                    {
                        panel.RefreshStageCount();
                    }
                }
            }, panelsToClose, null);
        }
        else
        {
            // 如果没有 SwitchSceneManager，使用原来的逻辑（无淡入淡出）
            Debug.LogWarning("[WinPanel] SwitchSceneManager.Instance 为 null，将不使用淡入淡出效果。");
            
            // 清理当前关卡的所有青蛙
            ClearAllFrogs();
            
            // 清除 StageManager 的网格数据
            if (StageManager.Instance != null)
            {
                StageManager.Instance.ClearGrid();
            }
            
            // 关闭 WinPanel
            Close();
            
            // 先结束当前游戏（设置状态为 Result），以便可以重新开始
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndGame();
            }
            
            // 开始下一关（关卡已经在胜利时自动更新了）
            // 注意：由于 StartGame 会检查状态，我们需要先 EndGame 才能再次 StartGame
            if (GameManager.Instance != null)
            {
                bool success = GameManager.Instance.StartGame(currentLevel);
                if (!success)
                {
                    // 如果StartGame失败（体力不足），直接返回
                    return;
                }
            }
            
            // 重新生成下一关的青蛙
            // 注意：GenMobManager.Start 只会在场景加载时自动调用
            // 在同一个场景中切换关卡时，需要手动调用 SpawnLevelMobs
            if (GenMobManager.Instance != null)
            {
                GenMobManager.Instance.SpawnLevelMobs(currentLevel);
            }
            else
            {
                Debug.LogWarning("[WinPanel] GenMobManager.Instance 为 null，无法生成下一关。");
            }

            // 刷新 StagePanel 的关卡号显示
            if (UIManager.Instance != null)
            {
                BasePanel stagePanel = UIManager.Instance.GetPanel("StagePanel");
                if (stagePanel is StagePanel panel)
                {
                    panel.RefreshStageCount();
                }
            }
        }
    }

    /// <summary>
    /// 更新通关时间显示
    /// </summary>
    private void UpdateFinishTimer()
    {
        if (_finishTimerText == null) return;
        
        if (StageManager.Instance != null)
        {
            float finishTime = StageManager.Instance.GetFinishTime();
            // 格式化为 "00:00" 格式（分:秒）
            int minutes = Mathf.FloorToInt(finishTime / 60f);
            int seconds = Mathf.FloorToInt(finishTime % 60f);
            _finishTimerText.text = $"{minutes:D2}:{seconds:D2}";
        }
        else
        {
            _finishTimerText.text = "00:00";
        }
    }

    /// <summary>
    /// 更新解锁物品信息显示
    /// </summary>
    private void UpdateUnlockItemInfo()
    {
        if (GameManager.Instance == null || GameManager.Instance.UnlockItemConfig == null)
        {
            // 如果没有配置，隐藏或清空显示
            if (_unlockItemText != null)
            {
                _unlockItemText.text = "";
            }
            if (_unlockItemIcon != null)
            {
                _unlockItemIcon.gameObject.SetActive(false);
            }
            return;
        }

        // 获取当前关卡（胜利后已经自动+1了，所以这里获取的是下一关的关卡数）
        int currentLevel = GameManager.Instance.CurrentLevel;
        
        // 首先查找当前关卡正好等于解锁关卡的物品（已解锁的物品）
        UnlockItem unlockedItem = null;
        foreach (var item in GameManager.Instance.UnlockItemConfig.unlockItems)
        {
            if (item != null && item.unlockLevel == currentLevel)
            {
                unlockedItem = item;
                break;
            }
        }

        if (unlockedItem != null)
        {
            // 当前关卡正好解锁了这个物品，显示"已解锁"
            if (_unlockItemText != null)
            {
                _unlockItemText.text = $"已解锁<color=#E15904>{unlockedItem.itemName}</color>";
            }
            
            // 更新图标显示
            if (_unlockItemIcon != null)
            {
                if (unlockedItem.itemIcon != null)
                {
                    _unlockItemIcon.sprite = unlockedItem.itemIcon;
                    _unlockItemIcon.gameObject.SetActive(true);
                }
                else
                {
                    _unlockItemIcon.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // 没有正好解锁的物品，查找大于当前关卡的最接近的解锁物品
            UnlockItem nextUnlockItem = null;
            int minUnlockLevel = int.MaxValue;
            
            foreach (var item in GameManager.Instance.UnlockItemConfig.unlockItems)
            {
                if (item != null && item.unlockLevel > currentLevel && item.unlockLevel < minUnlockLevel)
                {
                    minUnlockLevel = item.unlockLevel;
                    nextUnlockItem = item;
                }
            }

            if (nextUnlockItem != null)
            {
                // 计算剩余关卡数
                int remainingLevels = nextUnlockItem.unlockLevel - currentLevel;
                
                // 更新文本显示
                if (_unlockItemText != null)
                {
                    _unlockItemText.text = $"再玩<color=#E15904>{remainingLevels}</color>关解锁<color=#E15904>{nextUnlockItem.itemName}</color>";
                }
                
                // 更新图标显示
                if (_unlockItemIcon != null)
                {
                    if (nextUnlockItem.itemIcon != null)
                    {
                        _unlockItemIcon.sprite = nextUnlockItem.itemIcon;
                        _unlockItemIcon.gameObject.SetActive(true);
                    }
                    else
                    {
                        _unlockItemIcon.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // 没有找到下一个解锁物品，隐藏或清空显示
                if (_unlockItemText != null)
                {
                    _unlockItemText.text = "";
                }
                if (_unlockItemIcon != null)
                {
                    _unlockItemIcon.gameObject.SetActive(false);
                }
            }
        }
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
            Debug.Log($"[WinPanel] 清理 {normalFrogs.Length} 只 NormalFrog");
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
            Debug.Log($"[WinPanel] 清理 {yellowBlackFrogs.Length} 只 YellowBlackFrog");
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