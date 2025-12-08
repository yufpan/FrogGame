using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡管理器（单例）。
/// 负责管理关卡信息（包括青蛙网格、血量等）。
/// 维护一个二维数组来快速定位和操作青蛙。
/// </summary>
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    /// <summary>
    /// 玩家初始血量（可配置）
    /// </summary>
    [Header("玩家配置")]
    [Tooltip("玩家初始血量")]
    [Min(1)]
    [SerializeField] private int initialHealth = 6;

    /// <summary>
    /// 青蛙类型枚举
    /// </summary>
    public enum FrogType
    {
        Empty = -1,              // 空位
        GreenStatic = 0,         // 不变色的绿红青蛙（绿色）
        RedDynamic = 1,          // 会变色的绿红青蛙（红色）
        YellowStatic = 2,        // 不变色的黄黑青蛙（黄色）
        BlackDynamic = 3         // 会变色的黄黑青蛙（黑色）
    }


    /// <summary>
    /// 当前玩家血量
    /// </summary>
    private int currentHealth;

    /// <summary>
    /// 青蛙总数
    /// </summary>
    private int totalFrogCount;

    /// <summary>
    /// 当前剩余青蛙数量
    /// </summary>
    private int remainingFrogCount;

    /// <summary>
    /// 关卡倒计时（秒）
    /// </summary>
    private float timeRemaining;

    /// <summary>
    /// 关卡默认时间限制（用于重置时间）
    /// </summary>
    private float defaultTimeLimit;

    /// <summary>
    /// 是否正在倒计时
    /// </summary>
    private bool isTimerRunning = false;

    /// <summary>
    /// 关卡是否已结束
    /// </summary>
    private bool isStageEnded = false;

    /// <summary>
    /// 是否正在清理/重置关卡（用于临时禁用胜利检查）
    /// </summary>
    private bool isClearingStage = false;

    /// <summary>
    /// 是否处于投弹模式
    /// </summary>
    private bool isBombMode = false;
    
    /// <summary>
    /// 玩家是否已经进行过划框操作（用于教程面板）
    /// </summary>
    private bool hasPlayerDraggedBox = false;
    
    /// <summary>
    /// 是否已经打开过第二次教程面板（确保只打开一次）
    /// </summary>
    private bool hasOpenedSecondTutorial = false;

    /// <summary>
    /// 当前炸弹实例（用于监听爆炸事件）
    /// </summary>
    private Bomb currentBomb = null;

    /// <summary>
    /// 关卡开始时间（用于计算通关时间）
    /// </summary>
    private float stageStartTime;

    /// <summary>
    /// 通关时间（秒）
    /// </summary>
    private float finishTime;

    /// <summary>
    /// 血量变化事件
    /// </summary>
    public event Action<int> OnHealthChanged;

    /// <summary>
    /// 青蛙数量变化事件
    /// </summary>
    public event Action<int, int> OnFrogCountChanged; // (剩余数量, 总数量)

    /// <summary>
    /// 倒计时变化事件
    /// </summary>
    public event Action<float> OnTimerChanged;

    /// <summary>
    /// 关卡胜利事件
    /// </summary>
    public event Action OnStageVictory;

    /// <summary>
    /// 失败类型枚举
    /// </summary>
    public enum FailureType
    {
        HealthDepleted,  // 血量归0
        TimeOut          // 时间归零
    }

    /// <summary>
    /// 关卡失败事件（传递失败类型）
    /// </summary>
    public event Action<FailureType> OnStageFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }


    /// <summary>
    /// 初始化关卡（在生成青蛙后调用）
    /// </summary>
    /// <param name="totalFrogs">青蛙总数</param>
    /// <param name="timeLimit">倒计时（秒）</param>
    public void InitializeStage(int totalFrogs, float timeLimit)
    {
        totalFrogCount = totalFrogs;
        remainingFrogCount = totalFrogs;
        currentHealth = initialHealth;
        timeRemaining = timeLimit;
        defaultTimeLimit = timeLimit; // 保存默认时间限制
        isTimerRunning = true;
        isStageEnded = false;
        isClearingStage = false; // 重置清理标志
        stageStartTime = Time.time; // 记录关卡开始时间
        finishTime = 0f; // 重置通关时间
        hasPlayerDraggedBox = false; // 重置划框操作标志
        hasOpenedSecondTutorial = false; // 重置第二次教程面板打开标志

        OnHealthChanged?.Invoke(currentHealth);
        OnFrogCountChanged?.Invoke(remainingFrogCount, totalFrogCount);
        OnTimerChanged?.Invoke(timeRemaining);

        Debug.Log($"[StageManager] 关卡初始化：总青蛙数={totalFrogs}，倒计时={timeLimit}秒，初始血量={initialHealth}");
        
        // 如果是第二关，显示警告面板
        if (GameManager.Instance != null && GameManager.Instance.CurrentLevel == 2 && UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel("WarningPanel");
        }
    }

    private void Update()
    {
        if (isTimerRunning && !isStageEnded)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining < 0)
            {
                timeRemaining = 0;
                isTimerRunning = false;
                OnTimerChanged?.Invoke(timeRemaining);
                CheckStageFailed(FailureType.TimeOut);
            }
            else
            {
                OnTimerChanged?.Invoke(timeRemaining);
            }
        }
    }

    /// <summary>
    /// 当青蛙被销毁时调用（由FrogDestroyListener调用）
    /// </summary>
    public void OnFrogDestroyed()
    {
        // 如果关卡已结束或正在清理关卡，不触发胜利检查
        if (isStageEnded || isClearingStage) return;

        remainingFrogCount = Mathf.Max(0, remainingFrogCount - 1);
        OnFrogCountChanged?.Invoke(remainingFrogCount, totalFrogCount);

        // 检查是否胜利
        if (remainingFrogCount == 0)
        {
            CheckStageVictory();
        }
    }
    
    /// <summary>
    /// 通知玩家进行了划框操作（由DragBoxManager调用）
    /// </summary>
    public void OnPlayerDraggedBox()
    {
        if (isStageEnded || isClearingStage) return;
        
        hasPlayerDraggedBox = true;
        
        // 如果是第一关且还有剩余青蛙，且还没有打开过第二次教程面板，则打开教程面板（显示第二次提示）
        if (GameManager.Instance != null && GameManager.Instance.CurrentLevel == 1 && remainingFrogCount > 0 && !hasOpenedSecondTutorial)
        {
            if (UIManager.Instance != null)
            {
                TutorialPanel tutorialPanel = UIManager.Instance.GetPanel<TutorialPanel>("TutorialPanel");
                if (tutorialPanel != null)
                {
                    tutorialPanel.SetIsFirstTime(false); // 设置为第二次打开
                    UIManager.Instance.OpenPanel("TutorialPanel");
                    hasOpenedSecondTutorial = true; // 标记已经打开过第二次
                }
            }
        }
    }
    
    /// <summary>
    /// 检查玩家是否已经进行过划框操作
    /// </summary>
    public bool HasPlayerDraggedBox()
    {
        return hasPlayerDraggedBox;
    }

    /// <summary>
    /// 扣血（当框到红色青蛙时调用）
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        if (isStageEnded) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth);

        Debug.Log($"[StageManager] 玩家受到 {damage} 点伤害，当前血量：{currentHealth}");

        
    }

    /// <summary>
    /// 检查关卡胜利
    /// </summary>
    private void CheckStageVictory()
    {
        if (isStageEnded) return;
        if (isClearingStage) return;
        
        isStageEnded = true;
        isTimerRunning = false;
        // 计算通关时间（从关卡开始到胜利的时间）
        finishTime = Time.time - stageStartTime;
        Debug.Log($"[StageManager] 关卡胜利！所有青蛙已消除。通关时间：{finishTime:F2}秒");
        OnStageVictory?.Invoke();
    }

    /// <summary>
    /// 检查关卡失败
    /// </summary>
    public void CheckStageFailed(FailureType failureType)
    {
        if (isStageEnded) return;

        isStageEnded = true;
        isTimerRunning = false;
        string reason = failureType == FailureType.HealthDepleted ? "血量为0" : "时间归零";
        Debug.Log($"[StageManager] 关卡失败！原因：{reason}");
        OnStageFailed?.Invoke(failureType);
    }

    /// <summary>
    /// 恢复血量到初始值（用于重试）
    /// </summary>
    public void RestoreHealth()
    {
        currentHealth = initialHealth;
        isStageEnded = false;
        isTimerRunning = true;
        OnHealthChanged?.Invoke(currentHealth);
        Debug.Log($"[StageManager] 恢复血量到初始值：{initialHealth}");
    }

    /// <summary>
    /// 重置时间到默认值（用于重试）
    /// </summary>
    public void ResetTimer()
    {
        timeRemaining = defaultTimeLimit;
        isStageEnded = false;
        isTimerRunning = true;
        OnTimerChanged?.Invoke(timeRemaining);
        Debug.Log($"[StageManager] 重置时间到默认值：{defaultTimeLimit}秒");
    }

    /// <summary>
    /// 获取初始血量
    /// </summary>
    public int GetInitialHealth()
    {
        return initialHealth;
    }

    /// <summary>
    /// 获取当前血量
    /// </summary>
    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    /// <summary>
    /// 获取剩余青蛙数量
    /// </summary>
    public int GetRemainingFrogCount()
    {
        return remainingFrogCount;
    }

    /// <summary>
    /// 获取总青蛙数量
    /// </summary>
    public int GetTotalFrogCount()
    {
        return totalFrogCount;
    }

    /// <summary>
    /// 获取剩余时间
    /// </summary>
    public float GetTimeRemaining()
    {
        return timeRemaining;
    }

    /// <summary>
    /// 检查关卡是否已结束
    /// </summary>
    public bool IsStageEnded()
    {
        return isStageEnded;
    }

    /// <summary>
    /// 回满血量
    /// </summary>
    public void HealToFull()
    {
        if (isStageEnded) return;
        
        currentHealth = initialHealth;
        OnHealthChanged?.Invoke(currentHealth);
        Debug.Log($"[StageManager] 回满血量：{initialHealth}");
    }

    /// <summary>
    /// 增加时间
    /// </summary>
    /// <param name="seconds">要增加的秒数</param>
    public void AddTime(float seconds)
    {
        if (isStageEnded) return;
        
        timeRemaining += seconds;
        OnTimerChanged?.Invoke(timeRemaining);
        Debug.Log($"[StageManager] 增加时间 {seconds} 秒，当前剩余时间：{timeRemaining}秒");
    }

    /// <summary>
    /// 获取剩余绿色青蛙数量
    /// </summary>
    public int GetRemainingGreenFrogCount()
    {
        int count = 0;
        
        // 查找所有NormalFrog
        GreenRedFrog[] normalFrogs = FindObjectsOfType<GreenRedFrog>();
        foreach (var frog in normalFrogs)
        {
            if (frog != null && frog.ColorType == GreenRedFrog.FrogColorType.Green)
            {
                count++;
            }
        }
        
        return count;
    }

    /// <summary>
    /// 进入投弹模式
    /// </summary>
    public void EnterBombMode()
    {
        if (isStageEnded) return;
        
        isBombMode = true;
        Debug.Log("[StageManager] 进入投弹模式");
    }

    /// <summary>
    /// 退出投弹模式
    /// </summary>
    public void ExitBombMode()
    {
        isBombMode = false;
        currentBomb = null;
        Debug.Log("[StageManager] 退出投弹模式");
    }

    /// <summary>
    /// 检查是否处于投弹模式
    /// </summary>
    public bool IsBombMode()
    {
        return isBombMode;
    }

    /// <summary>
    /// 在指定位置生成炸弹
    /// </summary>
    /// <param name="worldPosition">世界坐标位置</param>
    public void SpawnBomb(Vector3 worldPosition)
    {
        if (!isBombMode || isStageEnded) return;

        // 加载炸弹预制体
        GameObject bombPrefab = Resources.Load<GameObject>("Item/Bomb");
        if (bombPrefab == null)
        {
            Debug.LogError("[StageManager] 未找到炸弹预制体 Resources/Item/Bomb");
            return;
        }

        // 生成炸弹
        GameObject bombInstance = Instantiate(bombPrefab, worldPosition, Quaternion.identity);
        if (bombInstance == null)
        {
            Debug.LogError("[StageManager] 生成炸弹失败");
            return;
        }

        // 获取Bomb组件并订阅爆炸事件
        currentBomb = bombInstance.GetComponent<Bomb>();
        if (currentBomb != null)
        {
            currentBomb.OnExplode += HandleBombExplosion;
            Debug.Log($"[StageManager] 在位置 {worldPosition} 生成炸弹");
        }
        else
        {
            Debug.LogError("[StageManager] 炸弹预制体上没有Bomb组件");
        }
    }

    /// <summary>
    /// 处理炸弹爆炸
    /// </summary>
    /// <param name="explosionPosition">爆炸位置</param>
    /// <param name="explosionRadius">爆炸半径</param>
    private void HandleBombExplosion(Vector3 explosionPosition, float explosionRadius)
    {
        if (!isBombMode) return;

        Debug.Log($"[StageManager] 炸弹爆炸：位置={explosionPosition}，半径={explosionRadius}");

        // 取消订阅当前炸弹的事件
        if (currentBomb != null)
        {
            currentBomb.OnExplode -= HandleBombExplosion;
            currentBomb = null;
        }

        // 开始处理爆炸逻辑
        StartCoroutine(ProcessBombExplosionCoroutine(explosionPosition, explosionRadius));
    }

    /// <summary>
    /// 处理炸弹爆炸的协程
    /// </summary>
    private IEnumerator ProcessBombExplosionCoroutine(Vector3 explosionPosition, float explosionRadius)
    {
        // 使用 2D 物理在爆炸范围内查找所有碰撞体
        Collider2D[] hits = Physics2D.OverlapCircleAll(explosionPosition, explosionRadius);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[StageManager] 炸弹爆炸范围内没有找到任何物体");
            ExitBombMode();
            yield break;
        }

        // 收集爆炸范围内的所有青蛙
        HashSet<GameObject> affectedFrogs = new HashSet<GameObject>();
        List<GameObject> yellowFrogs = new List<GameObject>();

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            GreenRedFrog normalFrog = hit.GetComponentInParent<GreenRedFrog>();
            YellowBlackFrog yellowBlackFrog = hit.GetComponentInParent<YellowBlackFrog>();

            GameObject frogGo = null;
            if (normalFrog != null)
            {
                frogGo = normalFrog.gameObject;
            }
            else if (yellowBlackFrog != null)
            {
                frogGo = yellowBlackFrog.gameObject;
                
                // 如果是黄色青蛙，单独记录用于连锁爆炸
                if (yellowBlackFrog.IsYellow)
                {
                    yellowFrogs.Add(frogGo);
                }
            }

            if (frogGo == null) continue;

            // 跳过被隔离的青蛙
            FrogBase frogBase = frogGo.GetComponent<FrogBase>();
            if (frogBase != null && frogBase.IsIsolated)
            {
                continue;
            }

            if (affectedFrogs.Add(frogGo))
            {
                // 标记为被爆炸影响，这样销毁时不会播放特效和声音
                if (frogBase != null)
                {
                    frogBase.SetExplosionAffected(true);
                }
            }
        }

        Debug.Log($"[StageManager] 炸弹爆炸影响 {affectedFrogs.Count} 只青蛙，其中黄色青蛙 {yellowFrogs.Count} 只");

        // 处理黄色青蛙的连锁爆炸
        HashSet<GameObject> processedYellowFrogs = new HashSet<GameObject>();
        Queue<GameObject> yellowFrogsToProcess = new Queue<GameObject>(yellowFrogs);

        while (yellowFrogsToProcess.Count > 0)
        {
            List<GameObject> currentBatch = new List<GameObject>();
            while (yellowFrogsToProcess.Count > 0)
            {
                var frog = yellowFrogsToProcess.Dequeue();
                if (frog != null && !processedYellowFrogs.Contains(frog))
                {
                    currentBatch.Add(frog);
                    processedYellowFrogs.Add(frog);
                }
            }

            if (currentBatch.Count == 0)
            {
                break;
            }

            List<GameObject> newYellowFrogs = new List<GameObject>();

            foreach (var frog in currentBatch)
            {
                if (frog == null) continue;

                YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();
                if (yellowBlackFrog != null && yellowBlackFrog.IsYellow)
                {
                    // 触发黄色青蛙的爆炸（不立即销毁，只收集影响范围）
                    var chainAffected = yellowBlackFrog.TriggerExplosion(false);
                    foreach (var affectedFrog in chainAffected)
                    {
                        if (affectedFrog != null && affectedFrog != frog)
                        {
                            // 标记为被爆炸影响
                            FrogBase affectedFrogBase = affectedFrog.GetComponent<FrogBase>();
                            if (affectedFrogBase != null)
                            {
                                affectedFrogBase.SetExplosionAffected(true);
                            }

                            // 检查是否是黄色青蛙，如果是则加入下一批处理队列
                            YellowBlackFrog affectedYellowBlackFrog = affectedFrog.GetComponent<YellowBlackFrog>();
                            if (affectedYellowBlackFrog != null && affectedYellowBlackFrog.IsYellow)
                            {
                                if (!processedYellowFrogs.Contains(affectedFrog))
                                {
                                    newYellowFrogs.Add(affectedFrog);
                                }
                            }

                            affectedFrogs.Add(affectedFrog);
                        }
                    }
                    // 销毁黄色青蛙自身
                    Destroy(frog);
                }
            }

            if (newYellowFrogs.Count > 0)
            {
                foreach (var newYellowFrog in newYellowFrogs)
                {
                    if (newYellowFrog != null)
                    {
                        yellowFrogsToProcess.Enqueue(newYellowFrog);
                    }
                }
                yield return new WaitForSeconds(0.2f); // 连锁爆炸间隔
            }
        }

        // 等待一帧确保连锁爆炸完成
        yield return null;

        // 销毁所有被爆炸影响的青蛙（不包括已处理的黄色青蛙）
        foreach (var frog in affectedFrogs)
        {
            if (frog == null) continue;
            // 跳过已处理的黄色青蛙
            if (processedYellowFrogs.Contains(frog)) continue;

            Destroy(frog);
        }

        Debug.Log($"[StageManager] 炸弹爆炸处理完成，共消除 {affectedFrogs.Count} 只青蛙");

        // 退出投弹模式
        ExitBombMode();
    }

    /// <summary>
    /// 获取通关时间（秒）
    /// </summary>
    public float GetFinishTime()
    {
        return finishTime;
    }


    /// <summary>
    /// 仅为青蛙对象添加销毁监听（不必登记到网格），用于随机模式或无法确定行列时。
    /// </summary>
    /// <param name="frog">青蛙 GameObject</param>
    /// <param name="row">行索引（未知时可为 -1）</param>
    /// <param name="col">列索引（未知时可为 -1）</param>
    public void AttachDestroyListener(GameObject frog, int row = -1, int col = -1)
    {
        if (frog == null) return;

        var destroyListener = frog.GetComponent<FrogDestroyListener>();
        if (destroyListener == null)
        {
            destroyListener = frog.AddComponent<FrogDestroyListener>();
        }
        destroyListener.Initialize(row, col);
    }



    /// <summary>
    /// 清除关卡数据（关卡结束时调用）
    /// </summary>
    public void ClearGrid()
    {
        // 先设置关卡已结束和清理标志，避免清理过程中触发胜利事件
        // 这样在清理青蛙时，OnFrogDestroyed 会直接返回，不会触发胜利
        isStageEnded = true;
        isClearingStage = true;
        isTimerRunning = false;
        
        // 退出投弹模式（如果处于投弹模式）
        if (isBombMode)
        {
            ExitBombMode();
        }
        
        // 注意：保持 isStageEnded = true，避免清理青蛙时触发胜利事件
        // 当需要重新开始游戏时，会在 InitializeStage 中重置状态
    }

    /// <summary>
    /// 重开当前关卡
    /// </summary>
    public void RestartStage()
    {
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

        UIManager.Instance.ClosePanel("ResultPanel");

        // 读取当前关卡数
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;

        // 使用淡入淡出效果重新开始关卡
        if (SwitchSceneManager.Instance != null)
        {
            // 准备要关闭的面板列表（如果有打开的结果面板等）
            var panelsToClose = new List<string>();
            
            // 检查是否有打开的结果面板或胜利面板，需要关闭
            if (UIManager.Instance != null)
            {
                if (UIManager.Instance.GetPanel("ResultPanel") != null && UIManager.Instance.GetPanel("ResultPanel").IsOpen)
                {
                    panelsToClose.Add("ResultPanel");
                }
                if (UIManager.Instance.GetPanel("WinPanel") != null && UIManager.Instance.GetPanel("WinPanel").IsOpen)
                {
                    panelsToClose.Add("WinPanel");
                }
                if (UIManager.Instance.GetPanel("SettingPanel") != null && UIManager.Instance.GetPanel("SettingPanel").IsOpen)
                {
                    panelsToClose.Add("SettingPanel");
                }
            }

            // 执行淡入淡出，在黑屏时执行重新开始的逻辑
            SwitchSceneManager.Instance.FadeInOut(() => {
                // 先设置清理标志，避免清理过程中触发胜利检查
                isClearingStage = true;
                isStageEnded = true;

                // 先清除网格数据，设置 isStageEnded = true
                // 这样在清理青蛙时，OnDestroy 中检查 IsStageEnded() 会返回 true，不会触发死亡特效
                ClearGrid();

                // 清理当前关卡的所有青蛙（此时 isClearingStage 和 isStageEnded 都已经是 true，不会触发胜利检查）
                ClearAllFrogs();

                // 先结束当前游戏（设置状态为 Result），以便可以重新开始
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EndGame();
                }

                // 使用协程等待一帧，确保所有旧青蛙都被销毁后再生成新青蛙
                // 这样可以避免旧青蛙的 OnDestroy 在新关卡初始化后触发胜利检查
                StartCoroutine(RestartStageCoroutine(currentLevel));
            }, panelsToClose, null);
        }
        else
        {
            // 如果没有 SwitchSceneManager，使用原来的逻辑（无淡入淡出）
            Debug.LogWarning("[StageManager] SwitchSceneManager.Instance 为 null，将不使用淡入淡出效果。");
            
            // 先设置清理标志，避免清理过程中触发胜利检查
            isClearingStage = true;
            isStageEnded = true;
            ClearGrid();
            ClearAllFrogs();
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndGame();
            }
            
            StartCoroutine(RestartStageCoroutine(currentLevel));
        }
    }

    /// <summary>
    /// 重开关卡的协程，等待旧青蛙销毁完成后再生成新青蛙
    /// </summary>
    private IEnumerator RestartStageCoroutine(int currentLevel)
    {
        // 等待一帧，确保所有 Destroy() 调用都完成，旧青蛙的 OnDestroy 都已执行
        yield return null;

        // 重新开始当前关卡
        // 注意：由于 StartGame 会检查状态，我们需要先 EndGame 才能再次 StartGame
        if (GameManager.Instance != null)
        {
            bool success = GameManager.Instance.StartGame(currentLevel);
            if (!success)
            {
                // 如果StartGame失败（体力不足），重置清理标志并结束协程
                isClearingStage = false;
                yield break;
            }
        }

        // 重新生成当前关卡的青蛙
        // 注意：GenMobManager.Start 只会在场景加载时自动调用
        // 在同一个场景中重新开始时，需要手动调用 SpawnLevelMobs
        if (GenMobManager.Instance != null)
        {
            GenMobManager.Instance.SpawnLevelMobs(currentLevel);
        }
        else
        {
            Debug.LogWarning("[StageManager] GenMobManager.Instance 为 null，无法重新生成关卡。");
            isClearingStage = false;
        }

        // 等待一帧，确保所有青蛙都已生成完成
        yield return null;

        // 重新统计场上青蛙数量并更新剩余青蛙数（解决结算时玩家死亡导致计数不准确的问题）
        RefreshRemainingFrogCount();

        // 刷新 StagePanel 的关卡号显示
        if (UIManager.Instance != null)
        {
            BasePanel stagePanel = UIManager.Instance.GetPanel("StagePanel");
            if (stagePanel is StagePanel panel)
            {
                panel.RefreshStageCount();
            }
        }
        
        // 如果是第二关，显示警告面板
        if (currentLevel == 2 && UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel("WarningPanel");
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
            Debug.Log($"[StageManager] 清理 {normalFrogs.Length} 只 NormalFrog");
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
            Debug.Log($"[StageManager] 清理 {yellowBlackFrogs.Length} 只 YellowBlackFrog");
            foreach (var frog in yellowBlackFrogs)
            {
                if (frog != null && frog.gameObject != null)
                {
                    Destroy(frog.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// 重新统计场上青蛙数量并更新剩余青蛙数
    /// 用于解决结算时玩家死亡导致计数不准确的问题
    /// </summary>
    private void RefreshRemainingFrogCount()
    {
        int count = 0;

        // 统计所有 FrogBase（所有青蛙）
        FrogBase[] normalFrogs = FindObjectsOfType<FrogBase>();
        if (normalFrogs != null)
        {
            foreach (var frog in normalFrogs)
            {
                if (frog != null && frog.gameObject != null)
                {
                    count++;
                }
            }
        }

        // 更新剩余青蛙数和总数
        remainingFrogCount = count;

        // 触发事件通知UI更新
        OnFrogCountChanged?.Invoke(remainingFrogCount, totalFrogCount);

        Debug.Log($"[StageManager] 重新统计场上青蛙数量：{count} 只");
    }
}

/// <summary>
/// 用于监听青蛙销毁事件的组件
/// </summary>
public class FrogDestroyListener : MonoBehaviour
{
    private bool initialized = false;

    public void Initialize(int row = -1, int col = -1)
    {
        // row和col参数保留以保持兼容性，但不再使用
        initialized = true;
    }

    private void OnDestroy()
    {
        if (initialized && StageManager.Instance != null)
        {
            // 如果关卡已经结束（重开或关卡结束后的清理阶段），就不要触发销毁逻辑，避免播放特效和判断胜利
            if (StageManager.Instance.IsStageEnded())
            {
                return;
            }
            
            // 通知StageManager有青蛙被销毁
            StageManager.Instance.OnFrogDestroyed();
        }
    }
}

