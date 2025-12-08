using UnityEngine;
using UnityEditor;
using System;

/// <summary>
/// 关卡设计工具窗口。
/// 用于可视化编辑关卡中的青蛙布局，支持4种颜色（绿、红、黄、黑）绘制网格。
/// </summary>
public class LevelEditorWindow : EditorWindow
{
    // 使用 LevelSpawnInfo 中的生成模式枚举
    private LevelSpawnInfo.SpawnMode spawnMode = LevelSpawnInfo.SpawnMode.Layout;

    // 青蛙类型枚举：0=绿色, 1=红色, 2=黄色, 3=黑色, -1=空位
    private enum FrogType
    {
        Empty = -1,
        Green = 0,
        Red = 1,
        Yellow = 2,
        Black = 3
    }

    // 颜色映射
    private static readonly Color[] FrogColors = new Color[]
    {
        Color.green,    // 0: 绿色
        Color.red,      // 1: 红色
        Color.yellow,   // 2: 黄色
        Color.black     // 3: 黑色
    };

    // 当前编辑的配置
    private MobSpawnConfig mobSpawnConfig;
    private int targetLevel = 1;
    private int gridWidth = 10;
    private int gridHeight = 10;
    
    // 网格数据（二维数组）
    private int[,] gridData;
    
    // 倒计时配置（秒）
    private float timeLimit = 300f; // 默认5分钟

    // 随机模式下的颜色数量配置
    private int randomGreenCount = 0;
    private int randomRedCount = 0;
    private int randomYellowCount = 0;
    private int randomBlackCount = 0;
    
    // 当前选中的青蛙类型
    private FrogType selectedFrogType = FrogType.Green;
    
    // UI布局参数
    private Vector2 scrollPosition;
    private float cellSize = 20f;
    private bool isDragging = false;
    
    // 滚动视图区域
    private Rect gridRect;
    private Vector2 gridScrollPosition;

    [MenuItem("Tools/FrogGame/Level Editor")]
    public static void ShowWindow()
    {
        LevelEditorWindow window = GetWindow<LevelEditorWindow>("关卡编辑器");
        window.minSize = new Vector2(600, 500);
        window.Show();
    }

    private void OnEnable()
    {
        // 初始化网格数据
        InitializeGrid();
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

        // 当为随机模式时，仅编辑随机数量；网格编辑相关 UI 置灰
        EditorGUI.BeginDisabledGroup(spawnMode == LevelSpawnInfo.SpawnMode.Random);
        
        // 网格大小设置
        DrawGridSizeSection();
        
        EditorGUILayout.Space(10);
        
        // 工具选择区域
        DrawToolSection();
        
        EditorGUILayout.Space(10);
        
        // 网格绘制区域
        DrawGridSection();

        EditorGUI.EndDisabledGroup();
        
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
            bool canGoNext = true; // 总是可以切换到下一个关卡（即使不存在也会创建）
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
                if (info != null && info.UseLayoutMode())
                {
                    int[,] layout = info.GetFrogLayout();
                    if (layout != null)
                    {
                        EditorGUILayout.HelpBox(
                            $"关卡 {targetLevel} 已有布局: {layout.GetLength(0)}x{layout.GetLength(1)}", 
                            MessageType.Info
                        );
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"关卡 {targetLevel} 暂无布局数据（使用随机模式）", 
                        MessageType.Warning
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
        if (spawnMode == LevelSpawnInfo.SpawnMode.Layout)
        {
            EditorGUILayout.HelpBox("当前为【布局模式】：使用下方网格编辑本关的青蛙排列。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("当前为【随机模式】：根据下方配置的数量，在屏幕中随机生成青蛙，不再按格子排列。", MessageType.Info);
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
    /// 绘制网格大小设置区域
    /// </summary>
    private void DrawGridSizeSection()
    {
        EditorGUILayout.LabelField("网格设置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField("宽度:", GUILayout.Width(50));
        int newWidth = EditorGUILayout.IntField(gridWidth, GUILayout.Width(50));
        newWidth = Mathf.Clamp(newWidth, 1, 50);
        
        EditorGUILayout.LabelField("高度:", GUILayout.Width(50));
        int newHeight = EditorGUILayout.IntField(gridHeight, GUILayout.Width(50));
        newHeight = Mathf.Clamp(newHeight, 1, 50);
        
        if (newWidth != gridWidth || newHeight != gridHeight)
        {
            gridWidth = newWidth;
            gridHeight = newHeight;
            ResizeGrid();
        }
        
        EditorGUILayout.LabelField("单元格大小:", GUILayout.Width(80));
        cellSize = EditorGUILayout.Slider(cellSize, 15f, 40f);
        
        EditorGUILayout.EndHorizontal();
        
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
    }

    /// <summary>
    /// 绘制工具选择区域（颜色选择）
    /// </summary>
    private void DrawToolSection()
    {
        EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        // 空位按钮
        GUI.backgroundColor = selectedFrogType == FrogType.Empty ? Color.gray : Color.white;
        if (GUILayout.Button("空位", GUILayout.Height(30)))
        {
            selectedFrogType = FrogType.Empty;
        }
        
        // 绿色按钮
        GUI.backgroundColor = selectedFrogType == FrogType.Green ? Color.green : Color.white;
        if (GUILayout.Button("绿色", GUILayout.Height(30)))
        {
            selectedFrogType = FrogType.Green;
        }
        
        // 红色按钮
        GUI.backgroundColor = selectedFrogType == FrogType.Red ? Color.red : Color.white;
        if (GUILayout.Button("红色", GUILayout.Height(30)))
        {
            selectedFrogType = FrogType.Red;
        }
        
        // 黄色按钮
        GUI.backgroundColor = selectedFrogType == FrogType.Yellow ? Color.yellow : Color.white;
        if (GUILayout.Button("黄色", GUILayout.Height(30)))
        {
            selectedFrogType = FrogType.Yellow;
        }
        
        // 黑色按钮
        GUI.backgroundColor = selectedFrogType == FrogType.Black ? Color.gray : Color.white;
        if (GUILayout.Button("黑色", GUILayout.Height(30)))
        {
            selectedFrogType = FrogType.Black;
        }
        
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制网格区域
    /// </summary>
    private void DrawGridSection()
    {
        EditorGUILayout.LabelField("网格编辑区域", EditorStyles.boldLabel);
        
        // 计算网格区域大小
        float gridDisplayWidth = gridWidth * cellSize;
        float gridDisplayHeight = gridHeight * cellSize;
        
        // 创建滚动视图
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // 计算网格绘制区域
        gridRect = GUILayoutUtility.GetRect(gridDisplayWidth, gridDisplayHeight);
        
        // 绘制网格背景
        EditorGUI.DrawRect(gridRect, new Color(0.2f, 0.2f, 0.2f));
        
        // 绘制网格线和单元格
        DrawGridCells();
        
        // 处理鼠标输入
        HandleMouseInput();
        
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 绘制网格单元格
    /// </summary>
    private void DrawGridCells()
    {
        if (gridData == null) return;
        
        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                float x = gridRect.x + col * cellSize;
                float y = gridRect.y + row * cellSize;
                Rect cellRect = new Rect(x, y, cellSize, cellSize);
                
                // 绘制单元格背景
                int frogType = gridData[row, col];
                Color cellColor;
                
                if (frogType == -1)
                {
                    // 空位：深灰色
                    cellColor = new Color(0.3f, 0.3f, 0.3f);
                }
                else if (frogType >= 0 && frogType < FrogColors.Length)
                {
                    // 有青蛙：使用对应颜色
                    cellColor = FrogColors[frogType];
                    cellColor.a = 0.7f; // 半透明
                }
                else
                {
                    cellColor = new Color(0.3f, 0.3f, 0.3f);
                }
                
                EditorGUI.DrawRect(cellRect, cellColor);
                
                // 绘制边框
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1), Color.black);
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1, cellRect.height), Color.black);
            }
        }
        
        // 绘制最后一行和最后一列的边框
        EditorGUI.DrawRect(
            new Rect(gridRect.x, gridRect.y + gridHeight * cellSize, gridWidth * cellSize, 1), 
            Color.black
        );
        EditorGUI.DrawRect(
            new Rect(gridRect.x + gridWidth * cellSize, gridRect.y, 1, gridHeight * cellSize), 
            Color.black
        );
    }

    /// <summary>
    /// 处理鼠标输入
    /// </summary>
    private void HandleMouseInput()
    {
        Event currentEvent = Event.current;
        
        if (gridRect.Contains(currentEvent.mousePosition))
        {
            // 计算点击的单元格坐标
            int col = Mathf.FloorToInt((currentEvent.mousePosition.x - gridRect.x) / cellSize);
            int row = Mathf.FloorToInt((currentEvent.mousePosition.y - gridRect.y) / cellSize);
            
            col = Mathf.Clamp(col, 0, gridWidth - 1);
            row = Mathf.Clamp(row, 0, gridHeight - 1);
            
            if (currentEvent.type == EventType.MouseDown)
            {
                isDragging = true;
                SetCell(row, col);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && isDragging)
            {
                SetCell(row, col);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                isDragging = false;
                currentEvent.Use();
            }
        }
    }

    /// <summary>
    /// 设置单元格的值
    /// </summary>
    private void SetCell(int row, int col)
    {
        if (gridData == null) return;
        if (row < 0 || row >= gridHeight || col < 0 || col >= gridWidth) return;
        
        gridData[row, col] = (int)selectedFrogType;
        Repaint();
    }

    /// <summary>
    /// 绘制操作按钮
    /// </summary>
    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("清空网格", GUILayout.Height(30)))
        {
            ClearGrid();
        }
        
        if (GUILayout.Button("填充当前颜色", GUILayout.Height(30)))
        {
            FillGrid();
        }
        
        EditorGUILayout.EndHorizontal();
        
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
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("导出为代码", GUILayout.Height(30)))
        {
            ExportAsCode();
        }
        
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 初始化网格
    /// </summary>
    private void InitializeGrid()
    {
        if (gridData == null || gridData.GetLength(0) != gridHeight || gridData.GetLength(1) != gridWidth)
        {
            gridData = new int[gridHeight, gridWidth];
            // 初始化为空位
            for (int i = 0; i < gridHeight; i++)
            {
                for (int j = 0; j < gridWidth; j++)
                {
                    gridData[i, j] = -1;
                }
            }
        }
    }

    /// <summary>
    /// 调整网格大小
    /// </summary>
    private void ResizeGrid()
    {
        int[,] oldData = gridData;
        gridData = new int[gridHeight, gridWidth];
        
        // 初始化为空位
        for (int i = 0; i < gridHeight; i++)
        {
            for (int j = 0; j < gridWidth; j++)
            {
                gridData[i, j] = -1;
            }
        }
        
        // 如果有旧数据，复制重叠部分
        if (oldData != null)
        {
            int oldHeight = oldData.GetLength(0);
            int oldWidth = oldData.GetLength(1);
            
            for (int i = 0; i < Mathf.Min(gridHeight, oldHeight); i++)
            {
                for (int j = 0; j < Mathf.Min(gridWidth, oldWidth); j++)
                {
                    gridData[i, j] = oldData[i, j];
                }
            }
        }
        
        Repaint();
    }

    /// <summary>
    /// 清空网格数据（用于Editor工具编辑布局）
    /// </summary>
    private void ClearGrid()
    {
        if (EditorUtility.DisplayDialog("确认", "确定要清空整个网格吗？", "确定", "取消"))
        {
            for (int i = 0; i < gridHeight; i++)
            {
                for (int j = 0; j < gridWidth; j++)
                {
                    gridData[i, j] = -1;
                }
            }
            Repaint();
        }
    }

    /// <summary>
    /// 用当前选中的颜色填充整个网格
    /// </summary>
    private void FillGrid()
    {
        if (EditorUtility.DisplayDialog("确认", "确定要用当前颜色填充整个网格吗？", "确定", "取消"))
        {
            int value = (int)selectedFrogType;
            for (int i = 0; i < gridHeight; i++)
            {
                for (int j = 0; j < gridWidth; j++)
                {
                    gridData[i, j] = value;
                }
            }
            Repaint();
        }
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
        
        // 如果还是没有，说明关卡不存在，创建新网格
        if (info == null)
        {
            Debug.Log($"关卡 {targetLevel} 不存在，创建新网格");
            InitializeGrid();
            Repaint();
            return;
        }
        
        // 加载倒计时配置
        timeLimit = info.timeLimit > 0 ? info.timeLimit : 300f; // 默认5分钟

        // 加载生成模式及随机配置
        spawnMode = info.spawnMode;
        randomGreenCount = Mathf.Max(0, info.randomGreenCount);
        randomRedCount = Mathf.Max(0, info.randomRedCount);
        randomYellowCount = Mathf.Max(0, info.randomYellowCount);
        randomBlackCount = Mathf.Max(0, info.randomBlackCount);
        
        // 尝试加载布局数据
        int[,] layout = info.GetFrogLayout();
        if (layout != null && layout.GetLength(0) > 0 && layout.GetLength(1) > 0)
        {
            gridHeight = layout.GetLength(0);
            gridWidth = layout.GetLength(1);
            gridData = layout;
            Repaint();
            Debug.Log($"已加载关卡 {targetLevel} 的布局：{gridWidth}x{gridHeight}，倒计时：{timeLimit}秒");
        }
        else
        {
            Debug.Log($"关卡 {targetLevel} 还没有布局数据，创建新网格");
            InitializeGrid();
            Repaint();
        }
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
        
        if (gridData == null)
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
        
        // 保存布局数据
        info.SetFrogLayout(gridData);
        
        // 保存倒计时配置（如果为0或负数，使用默认值300秒）
        info.timeLimit = timeLimit > 0 ? timeLimit : 300f;
        
        // 标记为已修改并保存
        EditorUtility.SetDirty(mobSpawnConfig);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"已保存关卡 {targetLevel} 的布局到 MobSpawnConfig：{gridWidth}x{gridHeight}，倒计时：{timeLimit}秒");
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
        
        if (gridData == null)
        {
            EditorUtility.DisplayDialog("错误", "网格数据为空，无法保存", "确定");
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

    /// <summary>
    /// 导出为代码格式（用于调试）
    /// </summary>
    private void ExportAsCode()
    {
        if (gridData == null)
        {
            EditorUtility.DisplayDialog("错误", "网格数据为空", "确定");
            return;
        }
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"// 网格大小: {gridHeight}x{gridWidth}");
        sb.AppendLine("int[,] layout = new int[,]");
        sb.AppendLine("{");
        
        for (int i = 0; i < gridHeight; i++)
        {
            sb.Append("    { ");
            for (int j = 0; j < gridWidth; j++)
            {
                sb.Append(gridData[i, j]);
                if (j < gridWidth - 1) sb.Append(", ");
            }
            sb.Append(" }");
            if (i < gridHeight - 1) sb.Append(",");
            sb.AppendLine();
        }
        
        sb.AppendLine("};");
        
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        EditorUtility.DisplayDialog("成功", "代码已复制到剪贴板", "确定");
        Debug.Log(sb.ToString());
    }
}

