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
    [SerializeField] private TextMeshProUGUI _unlockItemText;
    [SerializeField] private Image _unlockItemIcon;
    [SerializeField] private GameObject _normalStageObject;
    [SerializeField] private TextMeshProUGUI _normalStageCoinText;
    [SerializeField] private GameObject _dailyStageObject;
    [SerializeField] private TextMeshProUGUI _dailyStageCoinText;
    [SerializeField] private TextMeshProUGUI _dailyStageScoreText;
    [SerializeField] private TextMeshProUGUI _dailyStageHistoryScoreText;

    /// <summary>
    /// 是否已经发放过金币（避免重复发放）
    /// </summary>
    private bool _hasRewardedCoins = false;




    public override void Open()
    {
        // 重置金币发放标志
        _hasRewardedCoins = false;

        // 绑定按钮事件
        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClick);
        }

        if (_nextStageButton != null)
        {
            _nextStageButton.onClick.AddListener(OnNextStageButtonClick);
        }

        // 根据游戏模式显示不同的面板
        UpdatePanelDisplay();

        // 更新解锁物品信息显示（仅常规关卡显示）
        if (GameManager.Instance != null && GameManager.Instance.CurrentGameMode == GameManager.GameMode.Normal)
        {
            UpdateUnlockItemInfo();
        }

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
    /// 根据游戏模式更新面板显示
    /// </summary>
    private void UpdatePanelDisplay()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[WinPanel] GameManager.Instance 为 null，无法判断游戏模式。");
            return;
        }

        bool isDailyChallenge = GameManager.Instance.CurrentGameMode == GameManager.GameMode.DailyChallenge;

        // 显示/隐藏对应的面板
        if (_normalStageObject != null)
        {
            _normalStageObject.SetActive(!isDailyChallenge);
        }

        if (_dailyStageObject != null)
        {
            _dailyStageObject.SetActive(isDailyChallenge);
        }

        // 在每日挑战模式下隐藏"下一关"按钮（因为每日挑战失败后应该返回菜单）
        if (_nextStageButton != null)
        {
            _nextStageButton.gameObject.SetActive(!isDailyChallenge);
        }

        // 根据模式更新显示内容
        if (isDailyChallenge)
        {
            UpdateDailyChallengeDisplay();
        }
        else
        {
            UpdateNormalStageDisplay();
        }
    }

    /// <summary>
    /// 更新常规关卡显示
    /// </summary>
    private void UpdateNormalStageDisplay()
    {
        if (_normalStageCoinText == null) return;

        // 读取关卡信息中的金币
        // 注意：WinPanel 打开时，关卡已经自动+1了，所以需要读取上一关（CurrentLevel - 1）的金币奖励
        int coinReward = 10; // 默认值
        if (GenMobManager.Instance != null && GenMobManager.Instance.mobSpawnConfig != null)
        {
            int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;
            int completedLevel = Mathf.Max(1, currentLevel - 1); // 刚刚完成的关卡
            LevelSpawnInfo levelInfo = GenMobManager.Instance.mobSpawnConfig.GetLevelInfo(completedLevel);
            if (levelInfo != null)
            {
                coinReward = levelInfo.coinReward;
            }
        }

        _normalStageCoinText.text = $"x{coinReward}";

        // 发放金币奖励（只发放一次）
        if (!_hasRewardedCoins && GameManager.Instance != null && coinReward > 0)
        {
            GameManager.Instance.AddCoins(coinReward);
            _hasRewardedCoins = true;
            Debug.Log($"[WinPanel] 常规关卡胜利，发放金币奖励: +{coinReward}");
        }
    }

    /// <summary>
    /// 更新每日挑战显示
    /// </summary>
    private void UpdateDailyChallengeDisplay()
    {
        if (StageManager.Instance == null)
        {
            Debug.LogWarning("[WinPanel] StageManager.Instance 为 null，无法获取每日挑战分数。");
            return;
        }

        // 获取当前得分
        int currentScore = StageManager.Instance.GetDailyChallengeScore();

        // 获取历史最高分
        int historyHighestScore = 0;
        if (SaveDataManager.Instance != null)
        {
            historyHighestScore = SaveDataManager.Instance.LoadDailyChallengeHighestScore(0);
        }

        // 检查是否打破记录
        bool isNewRecord = currentScore > historyHighestScore;
        if (isNewRecord)
        {
            // 保存新记录
            if (SaveDataManager.Instance != null)
            {
                SaveDataManager.Instance.SaveDailyChallengeHighestScore(currentScore);
            }
            historyHighestScore = currentScore; // 两个分数都显示新分数
        }

        // 更新当前得分显示
        if (_dailyStageScoreText != null)
        {
            _dailyStageScoreText.text = $"本轮挑战积分：{currentScore}";
        }

        // 更新历史最高分显示
        if (_dailyStageHistoryScoreText != null)
        {
            _dailyStageHistoryScoreText.text = $"历史最高分：{historyHighestScore}";
        }

        // 计算金币奖励：基础10金币，每200分额外获得20金币
        int coinReward = 10;
        if (currentScore >= 200)
        {
            int bonusMultiplier = currentScore / 200;
            coinReward += bonusMultiplier * 20;
        }

        // 更新金币显示
        if (_dailyStageCoinText != null)
        {
            _dailyStageCoinText.text = $"x{coinReward}";
        }

        // 发放金币奖励（只发放一次）
        if (!_hasRewardedCoins && GameManager.Instance != null && coinReward > 0)
        {
            GameManager.Instance.AddCoins(coinReward);
            _hasRewardedCoins = true;
            Debug.Log($"[WinPanel] 每日挑战结算，发放金币奖励: +{coinReward} (得分: {currentScore})");
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