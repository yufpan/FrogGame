using UnityEngine;

/// <summary>
/// 每一关的怪物生成配置。
/// 支持两种生成模式：
/// 1. 使用二维数组布局（frogLayout）：按网格整齐排列
/// 2. 使用总数量（totalCount）：随机生成（旧模式，保留兼容性）
/// </summary>
[System.Serializable]
public class LevelSpawnInfo
{
    /// <summary>
    /// 生成模式：
    /// Layout  = 使用二维数组布局（按格子生成）
    /// Random  = 使用随机模式，根据每种颜色的数量在屏幕内随机生成
    /// </summary>
    public enum SpawnMode
    {
        Layout = 0,
        Random = 1
    }

    [Header("生成模式")]
    [Tooltip("本关使用的生成模式：布局模式 或 随机模式")]
    public SpawnMode spawnMode = SpawnMode.Layout;

    [Tooltip("本关需要生成的怪物总数量（旧随机模式，保留兼容性，当 frogLayout 为空且未使用按颜色随机模式时使用）")]
    public int totalCount = 10;

    [Header("青蛙布局配置")]
    [Tooltip("青蛙布局二维数组。0=绿色, 1=红色, 2=黄色, 3=黑色, -1=空位。如果设置了此数组，将优先使用布局模式生成。")]
    public int[] frogLayout;

    [Tooltip("布局的行数（只读，由frogLayout自动计算）")]
    [SerializeField] private int layoutRows;

    [Tooltip("布局的列数（只读，由frogLayout自动计算）")]
    [SerializeField] private int layoutCols;

    [Header("关卡配置")]
    [Tooltip("关卡倒计时（秒），默认5分钟（300秒）")]
    [Min(0)]
    public float timeLimit = 300f;

    [Header("随机模式配置（按颜色数量）")]
    [Min(0)]
    [Tooltip("随机模式下生成的绿色青蛙数量")]
    public int randomGreenCount = 0;

    [Min(0)]
    [Tooltip("随机模式下生成的红色青蛙数量")]
    public int randomRedCount = 0;

    [Min(0)]
    [Tooltip("随机模式下生成的黄色青蛙数量")]
    public int randomYellowCount = 0;

    [Min(0)]
    [Tooltip("随机模式下生成的黑色青蛙数量")]
    public int randomBlackCount = 0;

    /// <summary>
    /// 设置青蛙布局。将二维数组展平为一维数组存储。
    /// </summary>
    public void SetFrogLayout(int[,] layout)
    {
        if (layout == null)
        {
            frogLayout = null;
            layoutRows = 0;
            layoutCols = 0;
            return;
        }

        int rows = layout.GetLength(0);
        int cols = layout.GetLength(1);
        frogLayout = new int[rows * cols];
        layoutRows = rows;
        layoutCols = cols;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                frogLayout[i * cols + j] = layout[i, j];
            }
        }
    }

    /// <summary>
    /// 获取青蛙布局的二维数组。如果未设置，返回null。
    /// </summary>
    public int[,] GetFrogLayout()
    {
        if (frogLayout == null || frogLayout.Length == 0 || layoutRows <= 0 || layoutCols <= 0)
        {
            return null;
        }

        int[,] layout = new int[layoutRows, layoutCols];
        for (int i = 0; i < layoutRows; i++)
        {
            for (int j = 0; j < layoutCols; j++)
            {
                layout[i, j] = frogLayout[i * layoutCols + j];
            }
        }
        return layout;
    }

    /// <summary>
    /// 检查是否使用布局模式。
    /// </summary>
    public bool UseLayoutMode()
    {
        // 只有当生成模式为布局模式，且布局数据有效时，才认为使用布局模式
        return spawnMode == SpawnMode.Layout &&
               frogLayout != null && frogLayout.Length > 0 &&
               layoutRows > 0 && layoutCols > 0;
    }

    /// <summary>
    /// 检查是否使用“按颜色数量随机生成”的随机模式。
    /// </summary>
    public bool UseRandomMode()
    {
        if (spawnMode != SpawnMode.Random)
        {
            return false;
        }

        int totalRandomCount = randomGreenCount + randomRedCount + randomYellowCount + randomBlackCount;
        return totalRandomCount > 0;
    }
}

/// <summary>
/// 存放所有关卡怪物生成配置的 ScriptableObject。
/// 创建方式：在 Unity 菜单栏选择
/// Create -> FrogGame -> MobSpawnConfig
/// 然后在 Inspector 中配置每一关的参数。
/// </summary>
[CreateAssetMenu(fileName = "MobSpawnConfig", menuName = "FrogGame/MobSpawnConfig", order = 0)]
public class MobSpawnConfig : ScriptableObject
{
    [Tooltip("按关卡顺序配置，每个元素代表一关（下标 0 对应第 1 关）")]
    public LevelSpawnInfo[] levelSpawnInfos;

    /// <summary>
    /// 根据关卡数（从 1 开始）获取本关配置。
    /// 如果超出配置范围，则返回最后一关的配置。
    /// 如果完全未配置，则返回 null。
    /// </summary>
    public LevelSpawnInfo GetLevelInfo(int level)
    {
        if (levelSpawnInfos == null || levelSpawnInfos.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(level - 1, 0, levelSpawnInfos.Length - 1);
        return levelSpawnInfos[index];
    }
}


