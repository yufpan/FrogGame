using UnityEngine;
using UnityEditor;
using System;

/// <summary>
/// 关卡设计工具窗口。
/// 用于编辑关卡配置，包括生成模式、随机数量、倒计时和金币奖励。
/// </summary>
public class LevelEditorWindow : EditorWindow
{
    // 使用 LevelSpawnInfo 中的生成模式枚举
    private LevelSpawnInfo.SpawnMode spawnMode = LevelSpawnInfo.SpawnMode.Random;

    // 当前编辑的配置
    private MobSpawnConfig mobSpawnConfig;
    private int targetLevel = 1;

    // 倒计时配置（秒）
    private float timeLimit = 300f; // 默认5分钟

    // 金币奖励配置
    private int coinReward = 10; // 默认10个

    // 随机模式下的颜色数量配置
    private int randomGreenCount = 0;
    private int randomRedCount = 0;
    private int randomYellowCount = 0;
    private int randomBlackCount = 0;

    [MenuItem("Tools/FrogGame/Level Editor")]
    public static void ShowWindow()
    {
        LevelEditorWindow window = GetWindow<LevelEditorWindow>("关卡编辑器");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        // 配置选择区域
        DrawConfigSection();
        
        EditorGUILayout.Space(10);

        // 生成模式与随机配置
        DrawSpawnModeSection();
        
        EditorGUILayout.Space(10);

        // 关卡设置（倒计时和金币）
        DrawLevelSettingsSection();
        
        EditorGUILayout.Space(10);
        
        // 操作按钮
        DrawActionButtons();
        
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制配置选择区域
    /// </summary>
    private void DrawConfigSection()
    {
        EditorGUILayout.LabelField("关卡配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        mobSpawnConfig = (MobSpawnConfig)EditorGUILayout.ObjectField(
            "MobSpawnConfig", 
            mobSpawnConfig, 
            typeof(MobSpawnConfig), 
            false
        );
        
        EditorGUILayout.EndHorizontal();
        
        if (mobSpawnConfig != null)
        {
            // 显示关卡信息
            int totalLevels = mobSpawnConfig.levelSpawnInfos != null ? mobSpawnConfig.levelSpawnInfos.Length : 0;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"总关卡数: {totalLevels}", EditorStyles.helpBox, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("编辑关卡:", GUILayout.Width(80));
            
            // 上一个关卡按钮
            bool canGoPrev = targetLevel > 1;
            EditorGUI.BeginDisabledGroup(!canGoPrev);
            if (GUILayout.Button("◀", GUILayout.Width(30)))
            {
                SwitchToLevel(targetLevel - 1);
            }
            EditorGUI.EndDisabledGroup();
            
            int newLevel = EditorGUILayout.IntField(targetLevel, GUILayout.Width(50));
            newLevel = Mathf.Max(1, newLevel);
            if (newLevel != targetLevel)
            {
                SwitchToLevel(newLevel);
            }
            
            // 下一个关卡按钮
            if (GUILayout.Button("▶", GUILayout.Width(30)))
            {
                SwitchToLevel(targetLevel + 1);
            }
            
            if (totalLevels > 0)
            {
                EditorGUILayout.LabelField($"/ {totalLevels}", GUILayout.Width(50));
            }
            
            if (GUILayout.Button("加载", GUILayout.Width(60)))
            {
                LoadLevel();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 显示当前关卡的状态
            if (totalLevels > 0 && targetLevel <= totalLevels)
            {
                LevelSpawnInfo info = mobSpawnConfig.levelSpawnInfos[targetLevel - 1];
                if (info != null)
                {
                    EditorGUILayout.HelpBox(
                        $"关卡 {targetLevel} 配置已加载", 
                        MessageType.Info
                    );
                }
            }
            else if (targetLevel > totalLevels)
            {
                EditorGUILayout.HelpBox(
                    $"关卡 {targetLevel} 不存在，保存时将自动创建", 
                    MessageType.Info
                );
            }
        }
    }

    /// <summary>
    /// 绘制生成模式和随机配置区域
    /// </summary>
    private void DrawSpawnModeSection()
    {
        EditorGUILayout.LabelField("生成模式", EditorStyles.boldLabel);

        // 生成模式枚举选择
        spawnMode = (LevelSpawnInfo.SpawnMode)EditorGUILayout.EnumPopup("模式", spawnMode);

        // 显示当前模式说明
        if (spawnMode == LevelSpawnInfo.SpawnMode.Random)
        {
            EditorGUILayout.HelpBox("当前为【随机模式】：根据下方配置的数量，在屏幕中随机生成青蛙。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("当前为【布局模式】：此模式已废弃，请使用随机模式。", MessageType.Warning);
        }

        // 随机模式下的颜色数量配置
        if (spawnMode == LevelSpawnInfo.SpawnMode.Random)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("随机模式 - 颜色数量配置", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("绿色数量:", GUILayout.Width(80));
            randomGreenCount = Mathf.Max(0, EditorGUILayout.IntField(randomGreenCount, GUILayout.Width(60)));
            EditorGUILayout.LabelField("红色数量:", GUILayout.Width(80));
            randomRedCount = Mathf.Max(0, EditorGUILayout.IntField(randomRedCount, GUILayout.Width(60)));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("黄色数量:", GUILayout.Width(80));
            randomYellowCount = Mathf.Max(0, EditorGUILayout.IntField(randomYellowCount, GUILayout.Width(60)));
            EditorGUILayout.LabelField("黑色数量:", GUILayout.Width(80));
            randomBlackCount = Mathf.Max(0, EditorGUILayout.IntField(randomBlackCount, GUILayout.Width(60)));
            EditorGUILayout.EndHorizontal();

            int totalRandom = randomGreenCount + randomRedCount + randomYellowCount + randomBlackCount;
            EditorGUILayout.LabelField($"总数：{totalRandom}", EditorStyles.helpBox);

            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 绘制关卡设置区域（倒计时和金币）
    /// </summary>
    private void DrawLevelSettingsSection()
    {
        EditorGUILayout.LabelField("关卡设置", EditorStyles.boldLabel);
        
        // 倒计时配置
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("倒计时:", GUILayout.Width(60));
        
        // 显示分钟和秒的输入
        float minutes = timeLimit / 60f;
        float newMinutes = EditorGUILayout.FloatField(minutes, GUILayout.Width(60));
        EditorGUILayout.LabelField("分钟", GUILayout.Width(40));
        
        if (newMinutes != minutes)
        {
            timeLimit = newMinutes * 60f;
            timeLimit = Mathf.Max(0f, timeLimit);
        }
        
        // 显示总秒数（只读，用于参考）
        EditorGUILayout.LabelField($"({timeLimit:F0}秒)", EditorStyles.helpBox, GUILayout.Width(80));
        
        EditorGUILayout.EndHorizontal();

        // 金币奖励配置
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("通关金币:", GUILayout.Width(60));
        coinReward = Mathf.Max(0, EditorGUILayout.IntField(coinReward, GUILayout.Width(60)));
        EditorGUILayout.LabelField("个", GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制操作按钮
    /// </summary>
    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("保存到关卡", GUILayout.Height(40)))
        {
            SaveToLevel();
        }
        
        // 保存并下一个关卡按钮（方便连续编辑）
        EditorGUI.BeginDisabledGroup(mobSpawnConfig == null);
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f); // 浅绿色
        if (GUILayout.Button("保存并下一个 ▶", GUILayout.Height(40)))
        {
            if (SaveToLevelSilent())
            {
                SwitchToLevel(targetLevel + 1);
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 切换到指定关卡（用于上一个/下一个关卡按钮）
    /// </summary>
    private void SwitchToLevel(int newLevel)
    {
        if (newLevel < 1)
        {
            return;
        }
        
        targetLevel = newLevel;
        LoadLevel();
    }

    /// <summary>
    /// 加载关卡数据
    /// </summary>
    private void LoadLevel()
    {
        if (mobSpawnConfig == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择MobSpawnConfig", "确定");
            return;
        }
        
        // 获取关卡信息（如果不存在会返回null或创建默认的）
        LevelSpawnInfo info = mobSpawnConfig.GetLevelInfo(targetLevel);
        
        // 如果关卡不存在，尝试从数组中获取
        if (info == null && mobSpawnConfig.levelSpawnInfos != null && 
            targetLevel > 0 && targetLevel <= mobSpawnConfig.levelSpawnInfos.Length)
        {
            info = mobSpawnConfig.levelSpawnInfos[targetLevel - 1];
        }
        
        // 如果还是没有，说明关卡不存在，重置为默认值
        if (info == null)
        {
            Debug.Log($"关卡 {targetLevel} 不存在，使用默认配置");
            // 重置为默认值
            timeLimit = 300f; // 默认5分钟
            coinReward = 10; // 默认10个
            spawnMode = LevelSpawnInfo.SpawnMode.Random;
            randomGreenCount = 0;
            randomRedCount = 0;
            randomYellowCount = 0;
            randomBlackCount = 0;
            Repaint();
            return;
        }
        
        // 加载倒计时配置
        timeLimit = info.timeLimit > 0 ? info.timeLimit : 300f; // 默认5分钟

        // 加载金币奖励配置
        coinReward = info.coinReward >= 0 ? info.coinReward : 10; // 默认10个

        // 加载生成模式及随机配置
        spawnMode = info.spawnMode;
        randomGreenCount = Mathf.Max(0, info.randomGreenCount);
        randomRedCount = Mathf.Max(0, info.randomRedCount);
        randomYellowCount = Mathf.Max(0, info.randomYellowCount);
        randomBlackCount = Mathf.Max(0, info.randomBlackCount);
        
        Debug.Log($"已加载关卡 {targetLevel} 的配置：倒计时 {timeLimit}秒，金币奖励 {coinReward}，模式 {spawnMode}");
        Repaint();
    }

    /// <summary>
    /// 保存到关卡配置（静默版本，不显示对话框，用于"保存并下一个"功能）
    /// </summary>
    /// <returns>是否保存成功</returns>
    private bool SaveToLevelSilent()
    {
        if (mobSpawnConfig == null)
        {
            return false;
        }
        
        // 确保关卡数组足够大（一个MobSpawnConfig包含多个关卡）
        if (mobSpawnConfig.levelSpawnInfos == null)
        {
            mobSpawnConfig.levelSpawnInfos = new LevelSpawnInfo[targetLevel];
        }
        else if (mobSpawnConfig.levelSpawnInfos.Length < targetLevel)
        {
            // 扩展数组到目标关卡大小
            Array.Resize(ref mobSpawnConfig.levelSpawnInfos, targetLevel);
        }
        
        // 确保所有关卡都有对象（填充中间的null）
        for (int i = 0; i < targetLevel; i++)
        {
            if (mobSpawnConfig.levelSpawnInfos[i] == null)
            {
                mobSpawnConfig.levelSpawnInfos[i] = new LevelSpawnInfo();
            }
        }
        
        // 获取或创建目标关卡的信息
        LevelSpawnInfo info = mobSpawnConfig.levelSpawnInfos[targetLevel - 1];
        if (info == null)
        {
            info = new LevelSpawnInfo();
            mobSpawnConfig.levelSpawnInfos[targetLevel - 1] = info;
        }

        // 保存生成模式与随机配置
        info.spawnMode = spawnMode;
        info.randomGreenCount = Mathf.Max(0, randomGreenCount);
        info.randomRedCount = Mathf.Max(0, randomRedCount);
        info.randomYellowCount = Mathf.Max(0, randomYellowCount);
        info.randomBlackCount = Mathf.Max(0, randomBlackCount);
        
        // 保存倒计时配置（如果为0或负数，使用默认值300秒）
        info.timeLimit = timeLimit > 0 ? timeLimit : 300f;
        
        // 保存金币奖励配置（如果为负数，使用默认值10）
        info.coinReward = coinReward >= 0 ? coinReward : 10;
        
        // 标记为已修改并保存
        EditorUtility.SetDirty(mobSpawnConfig);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"已保存关卡 {targetLevel} 的配置到 MobSpawnConfig：倒计时 {timeLimit}秒，金币奖励 {coinReward}，模式 {spawnMode}");
        return true;
    }

    /// <summary>
    /// 保存到关卡配置
    /// </summary>
    private void SaveToLevel()
    {
        if (mobSpawnConfig == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择MobSpawnConfig", "确定");
            return;
        }
        
        bool success = SaveToLevelSilent();
        if (success)
        {
            int totalLevels = mobSpawnConfig.levelSpawnInfos != null ? mobSpawnConfig.levelSpawnInfos.Length : 0;
            EditorUtility.DisplayDialog("成功", $"已保存到关卡 {targetLevel}\n（MobSpawnConfig 包含 {totalLevels} 个关卡）", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "保存失败，请检查配置", "确定");
        }
    }
}
