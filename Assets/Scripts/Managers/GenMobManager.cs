using UnityEngine;

/// <summary>
/// 怪物生成管理器（单例）。
/// 负责根据 GameManager 当前关卡，从 MobSpawnConfig 中读取配置，
/// 支持两种生成模式：
/// 1. 布局模式：根据二维数组在屏幕中心生成整齐的青蛙矩阵
/// 2. 随机模式：在屏幕范围内随机生成（旧模式，保留兼容性）
/// </summary>
public class GenMobManager : MonoBehaviour
{
    public static GenMobManager Instance { get; private set; }

    [Header("生成配置")]
    [Tooltip("怪物生成配置 ScriptableObject")]
    public MobSpawnConfig mobSpawnConfig;

    [Header("每日挑战配置")]
    [Tooltip("每日挑战配置 ScriptableObject（可选，也可以通过参数传入）")]
    public DailyChallengeConfig dailyChallengeConfig;

    [Header("怪物 Prefab")]
    [Tooltip("红绿青蛙 Prefab（NormalFrog），引用 Assets/Prefabs/Mob/Frog.prefab")]
    public GameObject frogPrefab;
    
    [Tooltip("黄黑青蛙 Prefab（暂未实现，可为空）")]
    public GameObject specialFrogPrefab;

    [Header("生成范围设置")]
    [Tooltip("用于计算屏幕范围的相机。如果留空，将使用 Camera.main")]
    public Camera targetCamera;

    [Tooltip("生成位置距离屏幕边缘的内缩量，避免生成在完全边缘（随机模式使用）")]
    public float screenPadding = 0.5f;
    
    [Header("布局模式设置")]
    [Tooltip("青蛙矩阵中每个青蛙之间的间距（世界单位）")]
    public float frogSpacing = 1.0f;

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
    /// 当 GameScene 加载完成并且 GenMobManager 出现在场景中时，
    /// 如果当前 GameManager 的状态是 Playing，就根据当前关卡生成怪物。
    /// 这样可以保证是在切换到 GameScene 之后才开始生成。
    /// </summary>
    private void Start()
    {
            // 先尝试让 GameManager 根据当前场景中的 BGCanvas/CanGenArea 刷新一次生成区域
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateGenAreaFromUI();
            }

        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameManager.GameState.Playing)
        {
            // 检查是否为每日挑战模式
            if (GameManager.Instance.CurrentGameMode == GameManager.GameMode.DailyChallenge)
            {
                // 每日挑战模式：使用 DailyChallengeConfig
                if (dailyChallengeConfig != null)
                {
                    SpawnDailyChallengeFrogs(dailyChallengeConfig);
                }
                else
                {
                    Debug.LogWarning("[GenMobManager] 每日挑战模式但 dailyChallengeConfig 未设置。");
                }
            }
            else
            {
                // 常规关卡模式
                SpawnLevelMobs(GameManager.Instance.CurrentLevel);
            }
        }
        else    
        {
            if (GameManager.Instance.CurrentState == GameManager.GameState.Start)
            {
                Debug.Log("GenMobManager Start: GameManager is in Start state");
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.Result)
            {
                Debug.Log("GenMobManager Start: GameManager is in Result state");
            }
            else
            {
                Debug.Log("GenMobManager Start: GameManager is in unknown state");
            }
        }
    }

    /// <summary>
    /// 由 GameManager 在进入 Playing 状态时调用。
    /// 根据传入的关卡数，读取对应配置并生成怪物。
    /// 优先使用布局模式，如果未配置布局则使用随机模式。
    /// </summary>
    public void SpawnLevelMobs(int level)
    {
        if (mobSpawnConfig == null)
        {
            Debug.LogWarning("[GenMobManager] mobSpawnConfig 未设置，无法生成怪物。");
            return;
        }

        LevelSpawnInfo info = mobSpawnConfig.GetLevelInfo(level);
        if (info == null)
        {
            Debug.LogWarning($"[GenMobManager] 未找到关卡 {level} 的生成配置。");
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[GenMobManager] 找不到相机，无法计算屏幕范围。");
            return;
        }

        // 优先使用布局模式
        if (info.UseLayoutMode())
        {
            SpawnByLayout(info, cam, level);
        }
        // 其次使用"按颜色数量随机生成"的随机模式
        else if (info.UseRandomMode())
        {
            SpawnRandomByColor(info, cam, level);
        }
        else
        {
            // 使用旧的随机模式（只根据 totalCount 生成，不区分颜色，保留兼容性）
            SpawnRandomLegacy(info, cam, level);
        }
    }

    /// <summary>
    /// 尝试从 GameManager 获取由 BGCanvas/CanGenArea 计算出的世界空间生成区域。
    /// 返回 true 表示成功拿到区域，min 为左下角，max 为右上角。
    /// </summary>
    private bool TryGetGenWorldArea(out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (GameManager.Instance == null || !GameManager.Instance.HasGenArea)
        {
            return false;
        }

        min = GameManager.Instance.GenAreaMin;
        max = GameManager.Instance.GenAreaMax;
        return true;
    }

    /// <summary>
    /// 根据布局配置，在 BGCanvas/CanGenArea 指定的世界范围内生成青蛙矩阵。
    /// 如果未能从 GameManager 获取到该范围，将直接报错并放弃生成。
    /// </summary>
    private void SpawnByLayout(LevelSpawnInfo info, Camera cam, int level)
    {
        int[,] layout = info.GetFrogLayout();
        if (layout == null)
        {
            Debug.LogWarning($"[GenMobManager] 关卡 {level} 的布局数据无效，回退到随机模式。");
            SpawnRandomByColor(info, cam, level);
            return;
        }

        int rows = layout.GetLength(0);
        int cols = layout.GetLength(1);

        if (rows == 0 || cols == 0)
        {
            Debug.LogWarning($"[GenMobManager] 关卡 {level} 的布局大小为0，不生成怪物。");
            return;
        }

        // 必须从 GameManager 的 BGCanvas/CanGenArea 获取生成区域
        Vector2 areaMin, areaMax;
        if (!TryGetGenWorldArea(out areaMin, out areaMax))
        {
            Debug.LogError("[GenMobManager] SpawnByLayout：未能从 GameManager 获取生成区域（BGCanvas/CanGenArea），放弃生成。");
            return;
        }

        // 计算矩阵的总尺寸
        float totalWidth = (cols - 1) * frogSpacing;
        float totalHeight = (rows - 1) * frogSpacing;

        // 优先使用 BGCanvas/CanGenArea 提供的生成范围，将矩阵居中放置在该区域内
        Vector3 startPos;
        Vector3 centerPos;

        centerPos = new Vector3(
            (areaMin.x + areaMax.x) * 0.5f,
            (areaMin.y + areaMax.y) * 0.5f,
            0f);

        // 计算矩阵的起始位置（左上角），使其整体居中
        startPos = centerPos;
        startPos.x -= totalWidth * 0.5f;
        startPos.y += totalHeight * 0.5f;

        Debug.Log($"[GenMobManager] 开始生成关卡 {level} 的布局模式，矩阵大小：{rows}x{cols}");

        int spawnCount = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int frogType = layout[row, col];
                
                // -1 表示空位，跳过
                if (frogType == -1)
                {
                    continue;
                }

                // 计算当前青蛙的位置
                Vector3 spawnPos = startPos;
                spawnPos.x += col * frogSpacing;
                spawnPos.y -= row * frogSpacing;

                // 根据类型选择预制体并生成
                GameObject prefabToSpawn = GetFrogPrefab(frogType);
                if (prefabToSpawn != null)
                {
                    GameObject frog = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                    
                    // 如果是红绿青蛙（NormalFrog），需要设置颜色
                    if (frogType == 0 || frogType == 1)
                    {
                        SetFrogColor(frog, frogType);
                    }
                    // 如果是黄黑青蛙（YellowBlackFrog），需要设置颜色
                    else if (frogType == 2 || frogType == 3)
                    {
                        SetYellowBlackFrogColor(frog, frogType);
                    }
                    
                    // 添加销毁监听
                    if (StageManager.Instance != null)
                    {
                        StageManager.Instance.AttachDestroyListener(frog);
                    }
                    
                    spawnCount++;
                }
                else
                {
                    Debug.LogWarning($"[GenMobManager] 青蛙类型 {frogType} 的预制体未设置，跳过生成。");
                }
            }
        }

        Debug.Log($"[GenMobManager] 布局模式生成完成，共生成 {spawnCount} 只青蛙");

        // 初始化关卡（设置青蛙总数和倒计时）
        if (StageManager.Instance != null)
        {
            float timeLimit = info.timeLimit > 0 ? info.timeLimit : 300f; // 默认5分钟
            StageManager.Instance.InitializeStage(spawnCount, timeLimit);
        }
    }

    /// <summary>
    /// “按颜色数量随机生成”的随机模式：
    /// 根据 LevelSpawnInfo 中配置的绿/红/黄/黑数量，在 BGCanvas/CanGenArea 范围内随机生成青蛙。
    /// 如果未能从 GameManager 获取到该范围，将直接报错并放弃生成。
    /// </summary>
    private void SpawnRandomByColor(LevelSpawnInfo info, Camera cam, int level)
    {
        if (frogPrefab == null && specialFrogPrefab == null)
        {
            Debug.LogWarning("[GenMobManager] frogPrefab 与 specialFrogPrefab 都未设置，无法生成怪物。");
            return;
        }

        int greenCount = Mathf.Max(0, info.randomGreenCount);
        int redCount = Mathf.Max(0, info.randomRedCount);
        int yellowCount = Mathf.Max(0, info.randomYellowCount);
        int blackCount = Mathf.Max(0, info.randomBlackCount);

        int totalCount = greenCount + redCount + yellowCount + blackCount;
        if (totalCount == 0)
        {
            Debug.Log($"[GenMobManager] 关卡 {level} 的随机模式总数量为 0，不生成怪物。");
            return;
        }

        // 必须从 GameManager 的 BGCanvas/CanGenArea 获取生成区域
        Vector2 areaMin, areaMax;
        if (!TryGetGenWorldArea(out areaMin, out areaMax))
        {
            Debug.LogError("[GenMobManager] SpawnRandomByColor：未能从 GameManager 获取生成区域（BGCanvas/CanGenArea），放弃生成。");
            return;
        }

        Debug.Log($"[GenMobManager] 开始生成关卡 {level} 的【随机模式（按颜色数量）】，" +
                  $"绿色={greenCount}, 红色={redCount}, 黄色={yellowCount}, 黑色={blackCount}, 总数={totalCount}");

        int spawned = 0;

        // 按颜色分别生成，位置完全随机
        spawned += SpawnRandomColorFrogs(cam, 0, greenCount);  // 0=绿色
        spawned += SpawnRandomColorFrogs(cam, 1, redCount);    // 1=红色
        spawned += SpawnRandomColorFrogs(cam, 2, yellowCount); // 2=黄色
        spawned += SpawnRandomColorFrogs(cam, 3, blackCount);  // 3=黑色

        Debug.Log($"[GenMobManager] 随机模式生成完成，实际生成数量：{spawned}");

        // 初始化关卡（设置青蛙总数和倒计时）
        if (StageManager.Instance != null)
        {
            float timeLimit = info.timeLimit > 0 ? info.timeLimit : 300f; // 默认5分钟
            StageManager.Instance.InitializeStage(spawned, timeLimit);
        }
    }

    /// <summary>
    /// 旧的随机模式生成（只根据 totalCount 生成，不区分颜色，保留兼容性）
    /// 生成范围同样严格限定在 BGCanvas/CanGenArea 内，如果该范围不存在则直接报错并放弃生成。
    /// </summary>
    private void SpawnRandomLegacy(LevelSpawnInfo info, Camera cam, int level)
    {
        if (frogPrefab == null)
        {
            Debug.LogWarning("[GenMobManager] frogPrefab 未设置，无法生成怪物。");
            return;
        }

        int count = Mathf.Max(0, info.totalCount);
        if (count == 0)
        {
            Debug.Log($"[GenMobManager] 关卡 {level} 的生成数量为 0，不生成怪物。");
            return;
        }

        // 必须从 GameManager 的 BGCanvas/CanGenArea 获取生成区域
        Vector2 areaMin, areaMax;
        if (!TryGetGenWorldArea(out areaMin, out areaMax))
        {
            Debug.LogError("[GenMobManager] SpawnRandomLegacy：未能从 GameManager 获取生成区域（BGCanvas/CanGenArea），放弃生成。");
            return;
        }

        Debug.Log($"[GenMobManager] 开始生成关卡 {level} 的旧随机模式，总数量：{count}");

        int spawned = 0;

        // 旧模式：在屏幕范围内随机生成，但仍然为每只青蛙添加销毁监听，保证 StageManager 的计数正确
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = GetRandomPositionInScreen(cam);
            GameObject frog = Instantiate(frogPrefab, spawnPos, Quaternion.identity);

            if (frog != null && StageManager.Instance != null)
            {
                // 添加销毁监听，保证 OnFrogDestroyed 会被调用
                StageManager.Instance.AttachDestroyListener(frog);
            }

            spawned++;
        }

        // 初始化关卡（设置青蛙总数和倒计时）
        if (StageManager.Instance != null)
        {
            float timeLimit = info.timeLimit > 0 ? info.timeLimit : 300f; // 默认5分钟
            StageManager.Instance.InitializeStage(spawned, timeLimit);
        }
    }

    /// <summary>
    /// 根据青蛙类型获取对应的预制体
    /// 0=绿色, 1=红色 -> frogPrefab (NormalFrog)
    /// 2=黄色, 3=黑色 -> specialFrogPrefab (暂未实现)
    /// </summary>
    private GameObject GetFrogPrefab(int frogType)
    {
        if (frogType == 0 || frogType == 1)
        {
            return frogPrefab;
        }
        else if (frogType == 2 || frogType == 3)
        {
            return specialFrogPrefab; // 可能为null，暂时留空
        }
        return null;
    }

    /// <summary>
    /// 设置青蛙颜色（仅用于红绿青蛙）
    /// 0=绿色, 1=红色
    /// </summary>
    private void SetFrogColor(GameObject frog, int frogType)
    {
        GreenRedFrog normalFrog = frog.GetComponent<GreenRedFrog>();
        if (normalFrog == null)
        {
            Debug.LogWarning($"[GenMobManager] 青蛙对象 {frog.name} 没有 NormalFrog 组件，无法设置颜色。");
            return;
        }

        // 根据类型设置颜色
        if (frogType == 0)
        {
            // 绿色青蛙
            normalFrog.InitializeColor(GreenRedFrog.FrogColorType.Green);
        }
        else if (frogType == 1)
        {
            // 红色青蛙
            normalFrog.InitializeColor(GreenRedFrog.FrogColorType.Red);
        }
    }

    /// <summary>
    /// 设置黄黑青蛙颜色（仅用于黄黑青蛙）
    /// 2=黄色, 3=黑色
    /// </summary>
    private void SetYellowBlackFrogColor(GameObject frog, int frogType)
    {
        YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();
        if (yellowBlackFrog == null)
        {
            Debug.LogWarning($"[GenMobManager] 青蛙对象 {frog.name} 没有 YellowBlackFrog 组件，无法设置颜色。");
            return;
        }

        // 根据类型设置颜色
        if (frogType == 2)
        {
            // 黄色青蛙
            yellowBlackFrog.InitializeColor(YellowBlackFrog.FrogColorType.Yellow);
        }
        else if (frogType == 3)
        {
            // 黑色青蛙
            yellowBlackFrog.InitializeColor(YellowBlackFrog.FrogColorType.Black);
        }
    }

    /// <summary>
    /// 在当前 BGCanvas/CanGenArea 定义的世界范围内随机生成一个世界坐标位置。
    /// 如果 GameManager 中没有有效的生成区域，将报错并返回 (0,0,0)（理论上不会被调用到，
    /// 因为调用方在进入生成前已经做过检查）。
    /// </summary>
    private Vector3 GetRandomPositionInScreen(Camera cam)
    {
        Vector2 areaMin, areaMax;
        if (TryGetGenWorldArea(out areaMin, out areaMax))
        {
            // 给世界范围也留一点 padding，避免贴边
            float padX = screenPadding;
            float padY = screenPadding;

            float minWX = areaMin.x + padX;
            float maxWX = areaMax.x - padX;
            float minWY = areaMin.y + padY;
            float maxWY = areaMax.y - padY;

            // 防止 padding 过大导致反转
            if (minWX > maxWX) (minWX, maxWX) = (maxWX, minWX);
            if (minWY > maxWY) (minWY, maxWY) = (maxWY, minWY);

            float rx = Random.Range(minWX, maxWX);
            float ry = Random.Range(minWY, maxWY);
            return new Vector3(rx, ry, 0f);
        }

        Debug.LogError("[GenMobManager] GetRandomPositionInScreen：未能从 GameManager 获取生成区域（BGCanvas/CanGenArea），返回 (0,0,0)。");
        return Vector3.zero;
    }


    /// <summary>
    /// 在屏幕内随机生成指定数量的某种颜色青蛙。
    /// </summary>
    /// <param name="cam">相机</param>
    /// <param name="frogType">0=绿色,1=红色,2=黄色,3=黑色</param>
    /// <param name="count">要生成的数量</param>
    /// <returns>实际生成数量</returns>
    private int SpawnRandomColorFrogs(Camera cam, int frogType, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        GameObject prefabToSpawn = GetFrogPrefab(frogType);
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[GenMobManager] 随机模式：青蛙类型 {frogType} 的预制体未设置，跳过生成。");
            return 0;
        }

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = GetRandomPositionInScreen(cam);
            GameObject frog = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            // 根据类型设置颜色
            if (frogType == 0 || frogType == 1)
            {
                SetFrogColor(frog, frogType);
            }
            else if (frogType == 2 || frogType == 3)
            {
                SetYellowBlackFrogColor(frog, frogType);
            }

            // 添加销毁监听
            if (StageManager.Instance != null)
            {
                StageManager.Instance.AttachDestroyListener(frog);
            }

            spawned++;
        }

        return spawned;
    }

    /// <summary>
    /// 根据 DailyChallengeConfig 生成每日挑战青蛙
    /// </summary>
    /// <param name="config">每日挑战配置</param>
    /// <param name="initializeStage">是否初始化关卡（重新生成时设为false，只生成青蛙不重置状态）</param>
    public void SpawnDailyChallengeFrogs(DailyChallengeConfig config, bool initializeStage = true)
    {
        if (config == null)
        {
            Debug.LogWarning("[GenMobManager] DailyChallengeConfig 为空，无法生成每日挑战青蛙。");
            return;
        }
        
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[GenMobManager] 找不到相机，无法计算屏幕范围。");
            return;
        }
        
        int greenCount = config.randomGreenCount;
        int redCount = config.randomRedCount;
        int yellowCount = config.randomYellowCount;
        int blackCount = config.randomBlackCount;
        
        int totalCount = greenCount + redCount + yellowCount + blackCount;
        if (totalCount == 0)
        {
            Debug.Log("[GenMobManager] 每日挑战配置的总数量为 0，不生成怪物。");
            return;
        }
        
        Debug.Log($"[GenMobManager] 开始生成每日挑战青蛙，" +
                  $"绿色={greenCount}, 红色={redCount}, 黄色={yellowCount}, 黑色={blackCount}, 总数={totalCount}");
        
        int spawned = 0;
        
        // 按颜色分别生成
        spawned += SpawnRandomColorFrogs(cam, 0, greenCount);
        spawned += SpawnRandomColorFrogs(cam, 1, redCount);
        spawned += SpawnRandomColorFrogs(cam, 2, yellowCount);
        spawned += SpawnRandomColorFrogs(cam, 3, blackCount);
        
        Debug.Log($"[GenMobManager] 每日挑战生成完成，实际生成数量：{spawned}");
        
        // 初始化关卡（每日挑战模式，时间限制设为9999秒，实际不会使用）
        if (StageManager.Instance != null)
        {
            if (initializeStage)
            {
                // 首次生成：初始化关卡
                StageManager.Instance.InitializeStage(spawned, 9999f, true, config);
            }
            else
            {
                // 重新生成：只更新青蛙计数，不重置血量等其他状态
                StageManager.Instance.UpdateFrogCountOnly(spawned);
            }
        }
    }
}
