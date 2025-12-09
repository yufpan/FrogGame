using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 简单的游戏流程管理器（单例）。
/// 负责控制小游戏的几个阶段：开始、游戏中、结算中。
/// 其他脚本可以通过 GameManager.Instance 访问。
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>
    /// 游戏阶段枚举
    /// </summary>
    public enum GameState
    {
        Start,      // 开始阶段（一般显示主菜单）
        Playing,    // 游戏中
        Result      // 结算中 / 结算界面
    }

    /// <summary>
    /// 全局访问的单例实例
    /// </summary>
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// 当前游戏阶段
    /// </summary>
    public GameState CurrentState { get; private set; } = GameState.Start;

    /// <summary>
    /// 当前关卡数（从 1 开始）
    /// </summary>
    public int CurrentLevel { get; private set; } = 1;

    /// <summary>
    /// 状态切换事件（可选）：其他脚本可以订阅，做 UI 或逻辑响应
    /// </summary>
    public event Action<GameState> OnGameStateChanged;

    public UnlockItemConfig UnlockItemConfig;

    /// <summary>
    /// 是否强制使用 Inspector 设置的关卡数（覆盖存档）
    /// </summary>
    [SerializeField] private bool forceLevel = false;

    /// <summary>
    /// Inspector 中设置的关卡数（仅在 forceLevel 为 true 时生效）
    /// </summary>
    [SerializeField] private int inspectorLevel = 1;

    /// <summary>
    /// 体力上限
    /// </summary>
    private const int MAX_ENERGY = 100;

    /// <summary>
    /// 开始游戏需要的体力
    /// </summary>
    private const int ENERGY_COST_PER_GAME = 20;

    /// <summary>
    /// 体力恢复间隔（秒）- 5分钟
    /// </summary>
    private const float ENERGY_RECOVER_INTERVAL = 300f; // 5分钟 = 300秒

    /// <summary>
    /// 每次恢复的体力值
    /// </summary>
    private const int ENERGY_RECOVER_AMOUNT = 1;

    /// <summary>
    /// 每日获取体力的最大次数
    /// </summary>
    private const int MAX_DAILY_ENERGY_GET_COUNT = 5;

    /// <summary>
    /// 每次通过GetEnergyPanel获取的体力值
    /// </summary>
    private const int ENERGY_GET_AMOUNT = 80;

    /// <summary>
    /// 当前体力
    /// </summary>
    private int _currentEnergy = MAX_ENERGY;

    /// <summary>
    /// 每日获取体力的剩余次数
    /// </summary>
    private int _dailyEnergyGetRemainCount = MAX_DAILY_ENERGY_GET_COUNT;

    /// <summary>
    /// 上次体力更新时间戳（Unix时间戳，秒）
    /// </summary>
    private long _lastEnergyUpdateTime = 0;

    /// <summary>
    /// 体力恢复协程
    /// </summary>
    private Coroutine _energyRecoverCoroutine = null;

    /// <summary>
    /// 当前体力（公开属性）
    /// </summary>
    public int CurrentEnergy => _currentEnergy;

    /// <summary>
    /// 体力上限（公开属性）
    /// </summary>
    public int MaxEnergy => MAX_ENERGY;

    /// <summary>
    /// 体力变化事件
    /// </summary>
    public event Action<int> OnEnergyChanged;

    /// <summary>
    /// 每日获取体力剩余次数变化事件
    /// </summary>
    public event Action<int> OnDailyEnergyGetCountChanged;

    /// <summary>
    /// 每日获取体力剩余次数（公开属性）
    /// </summary>
    public int DailyEnergyGetRemainCount => _dailyEnergyGetRemainCount;

    /// <summary>
    /// 当前金币
    /// </summary>
    private int _currentCoins = 0;

    /// <summary>
    /// 当前金币（公开属性）
    /// </summary>
    public int CurrentCoins => _currentCoins;

    /// <summary>
    /// 金币变化事件
    /// </summary>
    public event Action<int> OnCoinsChanged;

    /// <summary>
    /// 是否已经成功从 BGCanvas/CanGenArea 读取到生成区域
    /// </summary>
    public bool HasGenArea { get; private set; }

    /// <summary>
    /// 生成区域左下角世界坐标
    /// </summary>
    public Vector2 GenAreaMin { get; private set; }

    /// <summary>
    /// 生成区域右上角世界坐标
    /// </summary>
    public Vector2 GenAreaMax { get; private set; }

    private void Awake()
    {
        // 简单单例：让 GameManager 在场景切换间常驻
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 加载存档数据
        LoadSaveData();
        
        // 如果启用了强制关卡设置，则覆盖当前关卡
        if (forceLevel && inspectorLevel > 0)
        {
            CurrentLevel = inspectorLevel;
            Debug.Log($"[GameManager] 使用 Inspector 强制设置的关卡数: {CurrentLevel}");
        }
        
        // 检查并刷新每日获取次数
        CheckAndRefreshDailyEnergyGetCount();
        
        // 计算并恢复离线体力
        RecoverOfflineEnergy();
        
        // 启动体力恢复协程
        StartEnergyRecoverCoroutine();
        
        UIManager.Instance.OpenPanel("MenuPanel");
        SetGameState(GameState.Start, CurrentLevel);
    }

    private void OnDestroy()
    {
        // 停止体力恢复协程
        if (_energyRecoverCoroutine != null)
        {
            StopCoroutine(_energyRecoverCoroutine);
            _energyRecoverCoroutine = null;
        }
    }

    /// <summary>
    /// 加载存档数据
    /// </summary>
    private void LoadSaveData()
    {
        if (SaveDataManager.Instance != null)
        {
            // 加载关卡进度
            CurrentLevel = SaveDataManager.Instance.LoadLevel(1);
            // 加载体力
            _currentEnergy = SaveDataManager.Instance.LoadEnergy(MAX_ENERGY);
            // 加载上次体力更新时间
            _lastEnergyUpdateTime = SaveDataManager.Instance.LoadLastEnergyUpdateTime(GetCurrentTimestamp());
            // 加载每日获取次数
            _dailyEnergyGetRemainCount = SaveDataManager.Instance.LoadDailyEnergyGetCount(MAX_DAILY_ENERGY_GET_COUNT);
            // 加载金币
            _currentCoins = SaveDataManager.Instance.LoadCoins(0);
        }
        else
        {
            // 如果没有存档管理器，使用默认值
            CurrentLevel = 1;
            _currentEnergy = MAX_ENERGY;
            _lastEnergyUpdateTime = GetCurrentTimestamp();
            _dailyEnergyGetRemainCount = MAX_DAILY_ENERGY_GET_COUNT;
            _currentCoins = 0;
        }
    }

    /// <summary>
    /// 获取当前日期字符串（格式：yyyy-MM-dd）
    /// </summary>
    private string GetCurrentDateString()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// 检查并刷新每日获取次数（如果跨天则重置）
    /// </summary>
    private void CheckAndRefreshDailyEnergyGetCount()
    {
        if (SaveDataManager.Instance == null)
        {
            _dailyEnergyGetRemainCount = MAX_DAILY_ENERGY_GET_COUNT;
            return;
        }

        string savedDate = SaveDataManager.Instance.LoadDailyEnergyGetDate("");
        string currentDate = GetCurrentDateString();

        // 如果日期不同或者是第一次，重置次数
        if (string.IsNullOrEmpty(savedDate) || savedDate != currentDate)
        {
            _dailyEnergyGetRemainCount = MAX_DAILY_ENERGY_GET_COUNT;
            SaveDataManager.Instance.SaveDailyEnergyGetDate(currentDate);
            SaveDataManager.Instance.SaveDailyEnergyGetCount(_dailyEnergyGetRemainCount);
            Debug.Log($"[GameManager] 新的一天，重置每日获取次数为: {_dailyEnergyGetRemainCount}");
            OnDailyEnergyGetCountChanged?.Invoke(_dailyEnergyGetRemainCount);
        }
        else
        {
            // 日期相同，加载保存的次数
            _dailyEnergyGetRemainCount = SaveDataManager.Instance.LoadDailyEnergyGetCount(MAX_DAILY_ENERGY_GET_COUNT);
            Debug.Log($"[GameManager] 加载每日获取剩余次数: {_dailyEnergyGetRemainCount}");
        }
    }

    /// <summary>
    /// 获取当前Unix时间戳（秒）
    /// </summary>
    private long GetCurrentTimestamp()
    {
        return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    /// <summary>
    /// 恢复离线期间的体力
    /// </summary>
    private void RecoverOfflineEnergy()
    {
        if (_currentEnergy >= MAX_ENERGY)
        {
            // 体力已满，更新时间戳但不恢复
            _lastEnergyUpdateTime = GetCurrentTimestamp();
            SaveLastEnergyUpdateTime();
            return;
        }

        long currentTime = GetCurrentTimestamp();
        long timePassed = currentTime - _lastEnergyUpdateTime;

        if (timePassed >= ENERGY_RECOVER_INTERVAL)
        {
            // 计算可以恢复多少体力
            int recoverCount = (int)(timePassed / ENERGY_RECOVER_INTERVAL);
            
            // 恢复体力（不超过上限）
            int oldEnergy = _currentEnergy;
            _currentEnergy = Mathf.Min(_currentEnergy + recoverCount * ENERGY_RECOVER_AMOUNT, MAX_ENERGY);
            
            // 更新时间戳（减去已恢复的时间）
            long remainingTime = timePassed % (long)ENERGY_RECOVER_INTERVAL;
            _lastEnergyUpdateTime = currentTime - remainingTime;
            
            if (_currentEnergy != oldEnergy)
            {
                Debug.Log($"[GameManager] 离线恢复体力: {oldEnergy} -> {_currentEnergy} (恢复 {_currentEnergy - oldEnergy} 点)");
                OnEnergyChanged?.Invoke(_currentEnergy);
                SaveEnergy();
                SaveLastEnergyUpdateTime();
            }
        }
    }

    /// <summary>
    /// 启动体力恢复协程
    /// </summary>
    private void StartEnergyRecoverCoroutine()
    {
        if (_energyRecoverCoroutine != null)
        {
            StopCoroutine(_energyRecoverCoroutine);
        }
        _energyRecoverCoroutine = StartCoroutine(EnergyRecoverCoroutine());
    }

    /// <summary>
    /// 体力恢复协程
    /// </summary>
    private IEnumerator EnergyRecoverCoroutine()
    {
        while (true)
        {
            // 如果体力已满，停止计时
            if (_currentEnergy >= MAX_ENERGY)
            {
                // 更新时间戳但不恢复体力
                _lastEnergyUpdateTime = GetCurrentTimestamp();
                SaveLastEnergyUpdateTime();
                yield return new WaitForSeconds(ENERGY_RECOVER_INTERVAL);
                continue;
            }

            // 等待恢复间隔
            yield return new WaitForSeconds(ENERGY_RECOVER_INTERVAL);

            // 检查体力是否仍然未满
            if (_currentEnergy < MAX_ENERGY)
            {
                // 恢复1点体力
                _currentEnergy = Mathf.Min(_currentEnergy + ENERGY_RECOVER_AMOUNT, MAX_ENERGY);
                _lastEnergyUpdateTime = GetCurrentTimestamp();
                
                Debug.Log($"[GameManager] 体力自动恢复: {_currentEnergy - ENERGY_RECOVER_AMOUNT} -> {_currentEnergy}");
                OnEnergyChanged?.Invoke(_currentEnergy);
                SaveEnergy();
                SaveLastEnergyUpdateTime();
            }
        }
    }

    /// <summary>
    /// 保存上次体力更新时间戳
    /// </summary>
    private void SaveLastEnergyUpdateTime()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.SaveLastEnergyUpdateTime(_lastEnergyUpdateTime);
        }
    }

    /// <summary>
    /// 尝试从场景中的 BGCanvas/CanGenArea（UI 坐标）读取生成青蛙的世界范围。
    /// 约定：
    /// - BGCanvas 下一定有名为 CanGenArea 的 UI 物体（RectTransform）
    /// - Canvas 使用 ScreenSpaceCamera / ScreenSpaceOverlay，RectTransform.GetWorldCorners
    ///   得到的 XY 即为世界坐标系下的位置（Z 忽略）。
    /// 此方法是幂等的，可以在场景切换后多次调用。
    /// </summary>
    public void UpdateGenAreaFromUI()
    {
        HasGenArea = false;

        GameObject canvasGo = GameObject.Find("BGCanvas");
        if (canvasGo == null)
        {
            Debug.LogWarning("[GameManager] 未找到名为 BGCanvas 的物体，无法读取生成区域。");
            return;
        }

        Transform areaTrans = canvasGo.transform.Find("CanGenArea");
        if (areaTrans == null)
        {
            Debug.LogWarning("[GameManager] 在 BGCanvas 下未找到 CanGenArea，无法读取生成区域。");
            return;
        }

        RectTransform rt = areaTrans as RectTransform;
        if (rt == null)
        {
            Debug.LogWarning("[GameManager] CanGenArea 不是 RectTransform，无法读取 UI 区域。");
            return;
        }

        // 获取四个世界空间顶点（左下、左上、右上、右下）
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        GenAreaMin = new Vector2(minX, minY);
        GenAreaMax = new Vector2(maxX, maxY);
        HasGenArea = true;

        Debug.Log($"[GameManager] 成功从 BGCanvas/CanGenArea 记录生成区域，Min={GenAreaMin}, Max={GenAreaMax}");
    }

    /// <summary>
    /// 检查是否有足够的体力开始游戏
    /// </summary>
    public bool HasEnoughEnergy()
    {
        return _currentEnergy >= ENERGY_COST_PER_GAME;
    }

    /// <summary>
    /// 消耗体力
    /// </summary>
    private bool ConsumeEnergy(int amount)
    {
        if (_currentEnergy < amount)
        {
            return false;
        }
        _currentEnergy -= amount;
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0, MAX_ENERGY);
        _lastEnergyUpdateTime = GetCurrentTimestamp();
        OnEnergyChanged?.Invoke(_currentEnergy);
        // 保存体力变化和时间戳
        SaveEnergy();
        SaveLastEnergyUpdateTime();
        return true;
    }

    /// <summary>
    /// 增加体力（用于GetEnergyPanel等）
    /// </summary>
    public void AddEnergy(int amount)
    {
        _currentEnergy += amount;
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0, MAX_ENERGY);
        _lastEnergyUpdateTime = GetCurrentTimestamp();
        OnEnergyChanged?.Invoke(_currentEnergy);
        // 保存体力变化和时间戳
        SaveEnergy();
        SaveLastEnergyUpdateTime();
    }

    /// <summary>
    /// 尝试通过GetEnergyPanel获取体力
    /// 返回是否成功获取
    /// </summary>
    public bool TryGetEnergyFromPanel()
    {
        // 检查是否还有剩余次数
        if (_dailyEnergyGetRemainCount <= 0)
        {
            Debug.Log("[GameManager] 今日获取体力次数已用完");
            return false;
        }

        // 检查是否跨天，如果跨天则重置次数
        CheckAndRefreshDailyEnergyGetCount();

        // 如果重置后还是没有次数，说明今天已经用完了
        if (_dailyEnergyGetRemainCount <= 0)
        {
            Debug.Log("[GameManager] 今日获取体力次数已用完");
            return false;
        }

        // 减少剩余次数
        _dailyEnergyGetRemainCount--;
        SaveDailyEnergyGetCount();

        // 增加体力
        AddEnergy(ENERGY_GET_AMOUNT);

        Debug.Log($"[GameManager] 通过GetEnergyPanel获取体力: +{ENERGY_GET_AMOUNT}，剩余次数: {_dailyEnergyGetRemainCount}");
        OnDailyEnergyGetCountChanged?.Invoke(_dailyEnergyGetRemainCount);

        return true;
    }

    /// <summary>
    /// 保存每日获取次数
    /// </summary>
    private void SaveDailyEnergyGetCount()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.SaveDailyEnergyGetCount(_dailyEnergyGetRemainCount);
        }
    }

    /// <summary>
    /// 保存体力
    /// </summary>
    private void SaveEnergy()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.SaveEnergy(_currentEnergy);
        }
    }

    /// <summary>
    /// 保存关卡进度
    /// </summary>
    private void SaveLevel()
    {
        if (SaveDataManager.Instance != null)
        {
            SaveDataManager.Instance.SaveLevel(CurrentLevel);
        }
    }

    /// <summary>
    /// 外部调用：开始游戏（从开始阶段 -> 游戏中）
    /// 可指定关卡数，传0或负数则使用当前已加载的关卡（从存档加载的关卡）
    /// </summary>
    public bool StartGame(int level = 0)
    {
        if (CurrentState == GameState.Playing) return false;

        // 检查体力
        if (!HasEnoughEnergy())
        {
            // 打开GetEnergyPanel
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenPanel("GetEnergyPanel");
            }
            return false;
        }

        // 消耗体力
        if (!ConsumeEnergy(ENERGY_COST_PER_GAME))
        {
            // 如果消耗失败，打开GetEnergyPanel
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenPanel("GetEnergyPanel");
            }
            return false;
        }

        // 进入游戏时更新当前关卡：如果指定了有效关卡（>0）则使用指定值，否则使用当前已加载的关卡
        if (level > 0)
        {
            CurrentLevel = level;
        }
        // 如果 level <= 0，CurrentLevel 保持从存档加载的值不变
        SetGameState(GameState.Playing, CurrentLevel);

        // 这里可以做一些开始游戏的初始化：
        // 比如重置分数、生成关卡等
        // ResetScore();
        // SpawnLevel();
        return true;
    }

    /// <summary>
    /// 外部调用：结束当前局游戏，进入结算阶段
    /// </summary>
    public void EndGame()
    {
        if (CurrentState != GameState.Playing) return;

        SetGameState(GameState.Result);

        // 这里可以做结算逻辑，比如计算得分、显示结算面板等
        // ShowResultPanel();
    }

    /// <summary>
    /// 外部调用：重新开始一局游戏（从结算阶段 -> 游戏中）
    /// 也可以根据实际需求，从任意阶段强制切到 Playing
    /// </summary>
    public void RestartGame()
    {
        // 根据需要，你也可以限制只能在结算阶段重开：
        // if (CurrentState != GameState.Result) return;

        SetGameState(GameState.Playing, CurrentLevel);

        // 做一次重开初始化
        // ResetScore();
        // ReloadLevel();
    }

    /// <summary>
    /// 外部调用：返回主菜单（从任意阶段 -> 开始阶段）
    /// </summary>
    public void ReturnToMenu()
    {
        SetGameState(GameState.Start);
    }

    /// <summary>
    /// 外部调用：关卡胜利时更新关卡进度
    /// </summary>
    public void OnStageWin()
    {
        // 关卡胜利后，当前关卡自动+1
        CurrentLevel += 1;
        // 保存关卡进度
        SaveLevel();
        Debug.Log($"[GameManager] 关卡胜利！当前关卡更新为：{CurrentLevel}");
    }

    /// <summary>
    /// 外部调用：强制设置当前关卡（运行时调用）
    /// </summary>
    /// <param name="level">要设置的关卡数（必须大于0）</param>
    public void SetLevel(int level)
    {
        if (level > 0)
        {
            CurrentLevel = level;
            SaveLevel();
            Debug.Log($"[GameManager] 强制设置关卡为：{CurrentLevel}");
        }
        else
        {
            Debug.LogWarning($"[GameManager] 无效的关卡数：{level}，关卡数必须大于0");
        }
    }

    /// <summary>
    /// 内部统一状态切换接口，触发事件/处理 UI 等
    /// </summary>
    private void SetGameState(GameState newState, int level = 1)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        if (newState == GameState.Playing)
        {
            CurrentLevel = Mathf.Max(1, level);

            // 刚进入游戏阶段时，尝试根据 BGCanvas/CanGenArea 记录生成区域。
            // 注意：如果此时游戏场景尚未加载完，后续也可以再次调用 UpdateGenAreaFromUI() 进行刷新。
            UpdateGenAreaFromUI();
        }

        Debug.Log($"[GameManager] 状态切换为：{newState}, Level = {CurrentLevel}");

        switch (newState)
        {
            case GameState.Start:
                UIManager.Instance.OpenPanel("MenuPanel");
                break;
            case GameState.Playing:
                // 这里只负责切状态和记录关卡。
                // 真正生成怪物放在 GameScene 中的 GenMobManager.Start 里，
                // 确保场景和对象都加载完成后再生成。
                Debug.Log("GameManager SetGameState: Playing");
                break;
            case GameState.Result:
                break;
        }

        // 通知订阅者
        OnGameStateChanged?.Invoke(CurrentState);
    }

}


