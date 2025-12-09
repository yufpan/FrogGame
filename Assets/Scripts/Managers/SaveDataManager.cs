using UnityEngine;

/// <summary>
/// 存档管理器（单例）
/// 使用微信小游戏SDK的PlayerPrefs来存储游戏数据
/// </summary>
public class SaveDataManager : MonoBehaviour
{
    /// <summary>
    /// 全局访问的单例实例
    /// </summary>
    public static SaveDataManager Instance { get; private set; }

    /// <summary>
    /// PlayerPrefs键名常量
    /// </summary>
    private const string KEY_CURRENT_LEVEL = "CurrentLevel";
    private const string KEY_CURRENT_ENERGY = "CurrentEnergy";
    private const string KEY_LAST_ENERGY_UPDATE_TIME = "LastEnergyUpdateTime";
    private const string KEY_DAILY_ENERGY_GET_DATE = "DailyEnergyGetDate";
    private const string KEY_DAILY_ENERGY_GET_COUNT = "DailyEnergyGetCount";
    private const string KEY_CURRENT_COINS = "CurrentCoins";

    private void Awake()
    {
        // 简单单例：让 SaveDataManager 在场景切换间常驻
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存当前关卡进度
    /// </summary>
    public void SaveLevel(int level)
    {
        PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, level);
        PlayerPrefs.Save();
        Debug.Log($"[SaveDataManager] 保存关卡进度: {level}");
    }

    /// <summary>
    /// 加载当前关卡进度
    /// </summary>
    public int LoadLevel(int defaultValue = 1)
    {
        int level = PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, defaultValue);
        Debug.Log($"[SaveDataManager] 加载关卡进度: {level}");
        return level;
    }

    /// <summary>
    /// 保存当前体力
    /// </summary>
    public void SaveEnergy(int energy)
    {
        PlayerPrefs.SetInt(KEY_CURRENT_ENERGY, energy);
        PlayerPrefs.Save();
        Debug.Log($"[SaveDataManager] 保存体力: {energy}");
    }

    /// <summary>
    /// 加载当前体力
    /// </summary>
    public int LoadEnergy(int defaultValue = 100)
    {
        int energy = PlayerPrefs.GetInt(KEY_CURRENT_ENERGY, defaultValue);
        Debug.Log($"[SaveDataManager] 加载体力: {energy}");
        return energy;
    }

    /// <summary>
    /// 保存上次体力更新时间戳
    /// </summary>
    public void SaveLastEnergyUpdateTime(long timestamp)
    {
        PlayerPrefs.SetString(KEY_LAST_ENERGY_UPDATE_TIME, timestamp.ToString());
        PlayerPrefs.Save();
        Debug.Log($"[SaveDataManager] 保存上次体力更新时间: {timestamp}");
    }

    /// <summary>
    /// 加载上次体力更新时间戳
    /// </summary>
    public long LoadLastEnergyUpdateTime(long defaultValue = 0)
    {
        string timeStr = PlayerPrefs.GetString(KEY_LAST_ENERGY_UPDATE_TIME, defaultValue.ToString());
        if (long.TryParse(timeStr, out long timestamp))
        {
            Debug.Log($"[SaveDataManager] 加载上次体力更新时间: {timestamp}");
            return timestamp;
        }
        Debug.Log($"[SaveDataManager] 加载上次体力更新时间失败，使用默认值: {defaultValue}");
        return defaultValue;
    }

    /// <summary>
    /// 保存每日获取体力的日期（格式：yyyy-MM-dd）
    /// </summary>
    public void SaveDailyEnergyGetDate(string date)
    {
        PlayerPrefs.SetString(KEY_DAILY_ENERGY_GET_DATE, date);
        PlayerPrefs.Save();
        Debug.Log($"[SaveDataManager] 保存每日获取体力日期: {date}");
    }

    /// <summary>
    /// 加载每日获取体力的日期
    /// </summary>
    public string LoadDailyEnergyGetDate(string defaultValue = "")
    {
        string date = PlayerPrefs.GetString(KEY_DAILY_ENERGY_GET_DATE, defaultValue);
        Debug.Log($"[SaveDataManager] 加载每日获取体力日期: {date}");
        return date;
    }

    /// <summary>
    /// 保存每日获取体力的剩余次数
    /// </summary>
    public void SaveDailyEnergyGetCount(int count)
    {
        PlayerPrefs.SetInt(KEY_DAILY_ENERGY_GET_COUNT, count);
        PlayerPrefs.Save();
        Debug.Log($"[SaveDataManager] 保存每日获取体力剩余次数: {count}");
    }

    /// <summary>
    /// 加载每日获取体力的剩余次数
    /// </summary>
    public int LoadDailyEnergyGetCount(int defaultValue = 5)
    {
        int count = PlayerPrefs.GetInt(KEY_DAILY_ENERGY_GET_COUNT, defaultValue);
        Debug.Log($"[SaveDataManager] 加载每日获取体力剩余次数: {count}");
        return count;
    }

    /// <summary>
    /// 保存当前金币
    /// </summary>
    public void SaveCoins(int coins)
    {
        PlayerPrefs.SetInt(KEY_CURRENT_COINS, coins);
        PlayerPrefs.Save();
        Debug.Log($"[SaveDataManager] 保存金币: {coins}");
    }

    /// <summary>
    /// 加载当前金币
    /// </summary>
    public int LoadCoins(int defaultValue = 0)
    {
        int coins = PlayerPrefs.GetInt(KEY_CURRENT_COINS, defaultValue);
        Debug.Log($"[SaveDataManager] 加载金币: {coins}");
        return coins;
    }

    /// <summary>
    /// 清除所有存档数据
    /// </summary>
    public void ClearAllData()
    {
        PlayerPrefs.DeleteKey(KEY_CURRENT_LEVEL);
        PlayerPrefs.DeleteKey(KEY_CURRENT_ENERGY);
        PlayerPrefs.DeleteKey(KEY_LAST_ENERGY_UPDATE_TIME);
        PlayerPrefs.DeleteKey(KEY_DAILY_ENERGY_GET_DATE);
        PlayerPrefs.DeleteKey(KEY_DAILY_ENERGY_GET_COUNT);
        PlayerPrefs.DeleteKey(KEY_CURRENT_COINS);
        PlayerPrefs.Save();
        Debug.Log("[SaveDataManager] 清除所有存档数据");
    }
}

