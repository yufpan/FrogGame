using UnityEngine;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// 拖框管理器：只负责监听 TouchManager 的拖拽事件。
/// 具体"框选到什么、怎么处理"的逻辑可以在之后补充。
/// </summary>
public class DragBoxManager : MonoBehaviour
{
    /// <summary>
    /// 全局访问的单例实例
    /// </summary>
    public static DragBoxManager Instance { get; private set; }

    [Header("触摸管理器（如果留空，会在场景中自动查找）")]
    [SerializeField] private TouchManager touchManager;

    [Header("世界空间拖拽矩形")]
    [Tooltip("用于显示拖拽矩形的世界物体（建议是带 SpriteRenderer 的空物体或方块），XY 平面上缩放来改变大小")]
    [SerializeField] private Transform selectionRect;

    [Tooltip("用于从屏幕坐标转换到世界坐标的相机，留空则使用 Camera.main")]
    [SerializeField] private Camera worldCamera;

    [Header("黄黑青蛙特殊机制")]
    [Tooltip("黑色青蛙被框选时，转换的绿色青蛙数量（转为可变色的红色青蛙）")]
    [Min(0)]
    [SerializeField] private int blackFrogConversionCount = 10;

    [Header("结算时间间隔配置")]
    [Tooltip("黄色爆炸后，等待多久再销毁被影响的青蛙（秒）")]
    [Min(0f)]
    [SerializeField] private float yellowExplosionDelay = 0.3f;

    [Tooltip("连锁爆炸之间的时间间隔（秒）")]
    [Min(0f)]
    [SerializeField] private float chainExplosionDelay = 0.2f;

    [Tooltip("逐个销毁剩余青蛙的间隔（秒）")]
    [Min(0f)]
    [SerializeField] private float frogDestroyInterval = 0.2f;

    [Tooltip("玩家血量归零后，等待多久再触发失败结算（秒）")]
    [Min(0f)]
    [SerializeField] private float gameOverDelay = 0.5f;

    [Header("拖拽终点调整配置（解决手指遮挡问题）")]
    [Tooltip("拖拽距离超过此阈值（屏幕像素）时，调整终点位置")]
    [Min(0f)]
    [SerializeField] private float dragDeltaThreshold = 50f;

    [Tooltip("当超过阈值时，终点向起点方向回退的距离（屏幕像素）")]
    [Min(0f)]
    [SerializeField] private float endPointRetreatDistance = 30f;

    /// <summary>
    /// 当前拖拽过程中处于选中框内、需要显示"选择中"图标的青蛙集合。
    /// </summary>
    private readonly HashSet<FrogBase> highlightedFrogs = new HashSet<FrogBase>();

    /// <summary>
    /// 当前结算过程中需要处理的青蛙列表（用于复活后解冻）
    /// </summary>
    private List<GameObject> currentSettlingFrogs = new List<GameObject>();

    // 记录起点的屏幕坐标
    private Vector2 dragStartScreenPos;

    // 是否正在结算中（结算过程中不允许新的划框）
    private bool isSettling = false;

    // 是否禁用划框功能（例如在投弹模式下）
    private bool isDragBoxDisabled = false;

    private void Awake()
    {
        // 单例初始化
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[DragBoxManager] 检测到多个 DragBoxManager 实例，销毁重复的实例。");
            Destroy(gameObject);
            return;
        }

        if (touchManager == null)
        {
            touchManager = FindObjectOfType<TouchManager>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        // 初始隐藏世界矩形
        SetSelectionRectVisible(false);
    }

    private void OnEnable()
    {
        if (touchManager != null)
        {
            touchManager.OnDragBoxStart += HandleDragBoxStart;
            touchManager.OnDragBox += HandleDragBox;
            touchManager.OnDragBoxEnd += HandleDragBoxEnd;
            touchManager.OnDragBoxCancel += HandleDragBoxCancel;
        }
    }

    private void OnDisable()
    {
        if (touchManager != null)
        {
            touchManager.OnDragBoxStart -= HandleDragBoxStart;
            touchManager.OnDragBox -= HandleDragBox;
            touchManager.OnDragBoxEnd -= HandleDragBoxEnd;
            touchManager.OnDragBoxCancel -= HandleDragBoxCancel;
        }
    }

    /// <summary>
    /// 禁用或启用划框功能
    /// </summary>
    public void SetDragBoxEnabled(bool enabled)
    {
        isDragBoxDisabled = !enabled;
        if (!enabled)
        {
            // 如果禁用划框，取消当前拖拽
            SetSelectionRectVisible(false);
            ClearAllSelectingIcons();
        }
    }

    /// <summary>
    /// 玩家开始拖框，screenPos 为起点屏幕坐标
    /// </summary>
    private void HandleDragBoxStart(Vector2 screenPos)
    {
        // 如果游戏已结束或正在结算中，或划框功能被禁用，不处理拖拽
        if (!IsGamePlaying() || isSettling || isDragBoxDisabled)
        {
            return;
        }
        
        dragStartScreenPos = screenPos;
        UpdateSelectionRect(dragStartScreenPos, dragStartScreenPos);
        SetSelectionRectVisible(true);

        // 初始时更新一次头顶“选择中”图标
        UpdateFrogSelectingIcons();
    }

    /// <summary>
    /// 玩家正在拖框，start 为起点，current 为当前点（都是屏幕坐标）
    /// </summary>
    private void HandleDragBox(Vector2 start, Vector2 current)
    {
        // 如果游戏已结束或正在结算中，或划框功能被禁用，不处理拖拽
        if (!IsGamePlaying() || isSettling || isDragBoxDisabled)
        {
            return;
        }
        
        // 调整终点位置以解决手指遮挡问题
        Vector2 adjustedCurrent = AdjustEndPointForFingerOcclusion(start, current);
        UpdateSelectionRect(start, adjustedCurrent);
        // 拖拽过程中动态更新头顶"选择中"图标
        UpdateFrogSelectingIcons();
    }

    /// <summary>
    /// 玩家结束拖框，start 为起点，end 为终点（都是屏幕坐标）
    /// </summary>
    private void HandleDragBoxEnd(Vector2 start, Vector2 end)
    {
        // 如果游戏已结束或正在结算中，或划框功能被禁用，不处理拖拽
        if (!IsGamePlaying() || isSettling || isDragBoxDisabled)
        {
            SetSelectionRectVisible(false);
            ClearAllSelectingIcons();
            return;
        }
        
        // 调整终点位置以解决手指遮挡问题
        Vector2 adjustedEnd = AdjustEndPointForFingerOcclusion(start, end);
        UpdateSelectionRect(start, adjustedEnd);

        // 结束拖拽，进入结算前，先清除"选择中"图标
        ClearAllSelectingIcons();

        // 在当前矩形范围内尝试消除青蛙（使用协程分阶段处理）
        StartCoroutine(EliminateFrogsInSelectionCoroutine());

        SetSelectionRectVisible(false);
    }

    /// <summary>
    /// 拖拽被取消（例如拖到 UI 上或游戏状态变为不可交互）
    /// 只需要把当前的选择框隐藏，不做任何结算。
    /// </summary>
    private void HandleDragBoxCancel()
    {
        SetSelectionRectVisible(false);
        ClearAllSelectingIcons();
    }
    
    /// <summary>
    /// 检查游戏是否正在进行中
    /// </summary>
    private bool IsGamePlaying()
    {
        // 检查 GameManager 状态
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            {
                return false;
            }
        }
        
        // 检查 StageManager 是否已结束
        if (StageManager.Instance != null)
        {
            if (StageManager.Instance.IsStageEnded())
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// 控制世界矩形显示/隐藏
    /// </summary>
    private void SetSelectionRectVisible(bool visible)
    {
        if (selectionRect != null)
        {
            selectionRect.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 获取当前选择框的大小（世界单位）
    /// 优先使用 SpriteRenderer.size，如果没有则回退到 localScale
    /// </summary>
    private Vector2 GetSelectionRectSize()
    {
        if (selectionRect == null)
        {
            return Vector2.zero;
        }

        SpriteRenderer spriteRenderer = selectionRect.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            return spriteRenderer.size;
        }

        // 回退到使用 localScale（兼容旧代码）
        return new Vector2(
            Mathf.Abs(selectionRect.localScale.x),
            Mathf.Abs(selectionRect.localScale.y)
        );
    }

    /// <summary>
    /// 根据拖拽距离调整终点坐标，解决手指遮挡问题
    /// 当拖拽距离超过阈值时，将终点向起点方向回退指定距离
    /// </summary>
    private Vector2 AdjustEndPointForFingerOcclusion(Vector2 startScreenPos, Vector2 endScreenPos)
    {
        // 计算拖拽距离
        float dragDelta = Vector2.Distance(startScreenPos, endScreenPos);
        
        // 如果距离未超过阈值，直接返回原终点
        if (dragDelta <= dragDeltaThreshold)
        {
            return endScreenPos;
        }
        
        // 计算起点到终点的方向向量
        Vector2 direction = (endScreenPos - startScreenPos).normalized;
        
        // 将终点向起点方向回退指定距离
        Vector2 adjustedEnd = endScreenPos - direction * endPointRetreatDistance;
        
        return adjustedEnd;
    }

    /// <summary>
    /// 根据起点和终点屏幕坐标，更新世界空间矩形的位置和大小
    /// </summary>
    private void UpdateSelectionRect(Vector2 startScreenPos, Vector2 endScreenPos)
    {
        if (selectionRect == null || worldCamera == null)
        {
            return;
        }

        // 目标矩形所在平面的 Z（保持不变，只在 XY 平面上变化）
        float targetZ = selectionRect.position.z;

        // 计算相机到该平面的深度（ScreenToWorldPoint 的 z 是“到相机的距离”）
        float depth = targetZ - worldCamera.transform.position.z;

        Vector3 startWorld = worldCamera.ScreenToWorldPoint(
            new Vector3(startScreenPos.x, startScreenPos.y, depth));
        Vector3 endWorld = worldCamera.ScreenToWorldPoint(
            new Vector3(endScreenPos.x, endScreenPos.y, depth));

        // 只在 XY 平面上计算矩形
        float minX = Mathf.Min(startWorld.x, endWorld.x);
        float maxX = Mathf.Max(startWorld.x, endWorld.x);
        float minY = Mathf.Min(startWorld.y, endWorld.y);
        float maxY = Mathf.Max(startWorld.y, endWorld.y);

        Vector2 size = new Vector2(maxX - minX, maxY - minY);
        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        // 更新矩形世界位置（保持原来的 Z）
        selectionRect.position = new Vector3(center.x, center.y, targetZ);

        // 使用 SpriteRenderer.size 来改变框的大小（适用于 sliced sprite，保持边框粗细不变）
        SpriteRenderer spriteRenderer = selectionRect.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        }
        else
        {
            Debug.LogWarning("[DragBoxManager] UpdateSelectionRect：selectionRect 上没有找到 SpriteRenderer 组件，无法更新大小。");
        }
    }

    /// <summary>
    /// 使用协程分阶段消除青蛙
    /// 第一阶段：处理黄色青蛙的爆炸，同时销毁
    /// 第二阶段：处理红色青蛙，一个一个销毁，每销毁一个扣1血，如果没血了则停止结算
    /// 第三阶段：处理剩余其他青蛙，同时销毁
    /// </summary>
    private IEnumerator EliminateFrogsInSelectionCoroutine()
    {
        // 设置结算标志，防止新的划框
        isSettling = true;

        if (selectionRect == null)
        {
            Debug.LogWarning("[DragBoxManager] EliminateFrogsInSelectionCoroutine：selectionRect 为空，无法结算。");
            isSettling = false;
            yield break;
        }

        // ---------- 第 0 步：通过碰撞体查找当前矩形范围内的所有青蛙 ----------
        Vector3 center = selectionRect.position;
        Vector2 rectSize = GetSelectionRectSize();
        Vector3 halfSize = new Vector3(rectSize.x * 0.5f, rectSize.y * 0.5f, 0f);
        Vector2 boxSize = new Vector2(rectSize.x, rectSize.y);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, boxSize, 0f);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[DragBoxManager] EliminateFrogsInSelectionCoroutine：OverlapBox 命中 0 个碰撞体，本次不结算。");
            isSettling = false;
            yield break;
        }

        // 使用 HashSet 去重（同一只青蛙可能有多个碰撞体）
        HashSet<GameObject> frogSet = new HashSet<GameObject>();
        List<GameObject> frogsInRect = new List<GameObject>();

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            GreenRedFrog normal = hit.GetComponentInParent<GreenRedFrog>();
            YellowBlackFrog yb = hit.GetComponentInParent<YellowBlackFrog>();

            GameObject frogGo = null;
            if (normal != null)
            {
                frogGo = normal.gameObject;
            }
            else if (yb != null)
            {
                frogGo = yb.gameObject;
            }

            // 只关心挂了这两种脚本的对象
            if (frogGo == null) continue;
            
            // 跳过被隔离的青蛙（无法被选中）
            FrogBase frogBase = frogGo.GetComponent<FrogBase>();
            if (frogBase != null && frogBase.IsIsolated)
            {
                continue;
            }
            
            if (frogSet.Add(frogGo))
            {
                frogsInRect.Add(frogGo);
            }
        }

        Debug.Log($"[DragBoxManager] EliminateFrogsInSelectionCoroutine：OverlapBox 命中碰撞体 {hits.Length} 个，识别出青蛙对象 {frogsInRect.Count} 个。");

        if (frogsInRect.Count == 0)
        {
            // 框里没有青蛙，就什么都不做
            currentSettlingFrogs.Clear();
            isSettling = false;
            yield break;
        }

        // 保存当前结算的青蛙列表（用于复活后解冻）
        currentSettlingFrogs = new List<GameObject>(frogsInRect);

        // 结算阶段：先为所有被框选的青蛙显示"确认"图标（跳过被隔离的青蛙）
        foreach (var frog in frogsInRect)
        {
            if (frog == null) continue;
            var frogBase = frog.GetComponent<FrogBase>();
            if (frogBase != null && !frogBase.IsIsolated)
            {
                frogBase.SetConfirmIcon(true);
            }
        }

        // 冻结所有被框选的青蛙（禁止变色和动画）
        foreach (var frog in frogsInRect)
        {
            if (frog == null) continue;

            GreenRedFrog normalFrog = frog.GetComponent<GreenRedFrog>();
            YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();

            if (normalFrog != null)
            {
                normalFrog.SetFrozen(true);
            }
            else if (yellowBlackFrog != null)
            {
                yellowBlackFrog.SetFrozen(true);
            }
        }

        // 分类青蛙
        List<GameObject> yellowFrogs = new List<GameObject>();
        List<GameObject> redFrogs = new List<GameObject>();
        List<GameObject> otherFrogs = new List<GameObject>();

        foreach (var frog in frogsInRect)
        {
            if (frog == null) continue;

            YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();
            GreenRedFrog normalFrog = frog.GetComponent<GreenRedFrog>();

            if (yellowBlackFrog != null && yellowBlackFrog.IsYellow)
            {
                yellowFrogs.Add(frog);
            }
            else if (normalFrog != null && normalFrog.IsRed)
            {
                redFrogs.Add(frog);
            }
            else
            {
                otherFrogs.Add(frog);
            }
        }

        // ---------- 第一阶段：销毁黄色青蛙（触发连锁爆炸，但不立即销毁被影响的） ----------
        HashSet<GameObject> explosionAffectedFrogs = new HashSet<GameObject>();
        HashSet<GameObject> processedYellowFrogs = new HashSet<GameObject>(); // 记录已处理过的黄色青蛙
        
        // 初始化待处理的黄色青蛙队列（从框选中的黄色青蛙开始）
        Queue<GameObject> yellowFrogsToProcess = new Queue<GameObject>(yellowFrogs);
        int totalYellowFrogsDestroyed = 0;
        bool isFirstYellowBatch = true; // 标记是否是第一批黄色青蛙
        
        // 连锁爆炸循环：处理所有黄色青蛙及其连锁爆炸
        while (yellowFrogsToProcess.Count > 0)
        {
            // 收集当前批次要处理的黄色青蛙
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
            
            // 处理当前批次的黄色青蛙
            List<GameObject> newYellowFrogs = new List<GameObject>(); // 本次爆炸新影响到的黄色青蛙
            
            foreach (var frog in currentBatch)
            {
                if (frog == null) continue;

                YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();
                if (yellowBlackFrog != null && yellowBlackFrog.IsYellow)
                {
                    // 黄色青蛙：触发爆炸，但不立即销毁被影响的青蛙（immediateDestroy = false）
                    var affected = yellowBlackFrog.TriggerExplosion(false);
                    foreach (var affectedFrog in affected)
                    {
                        if (affectedFrog != null && affectedFrog != frog) // 排除黄色青蛙自身
                        {
                            explosionAffectedFrogs.Add(affectedFrog);
                            
                            // 检查被影响的是否是黄色青蛙，如果是则加入下一批处理队列
                            YellowBlackFrog affectedYellowBlackFrog = affectedFrog.GetComponent<YellowBlackFrog>();
                            if (affectedYellowBlackFrog != null && affectedYellowBlackFrog.IsYellow)
                            {
                                // 如果这个黄色青蛙还没有被处理过，加入下一批
                                if (!processedYellowFrogs.Contains(affectedFrog))
                                {
                                    newYellowFrogs.Add(affectedFrog);
                                }
                            }
                        }
                    }
                    // 黄色青蛙自身立即销毁
                    Destroy(frog);
                    totalYellowFrogsDestroyed++;
                }
            }
            
            // 第一批黄色青蛙销毁时触发长震动
            if (isFirstYellowBatch && currentBatch.Count > 0)
            {
                Utils.VibrateShortHeavy();
                isFirstYellowBatch = false;
            }
            
            Debug.Log($"[DragBoxManager] 第一阶段（连锁爆炸）：当前批次销毁了 {currentBatch.Count} 只黄色青蛙，发现 {newYellowFrogs.Count} 只新的黄色青蛙将被连锁触发。");
            
            // 如果有新的黄色青蛙被影响，等待间隔后继续处理
            if (newYellowFrogs.Count > 0)
            {
                // 将新发现的黄色青蛙加入队列
                foreach (var newYellowFrog in newYellowFrogs)
                {
                    if (newYellowFrog != null)
                    {
                        yellowFrogsToProcess.Enqueue(newYellowFrog);
                    }
                }
                
                // 等待连锁爆炸间隔
                if (chainExplosionDelay > 0f)
                {
                    yield return new WaitForSeconds(chainExplosionDelay);
                }
            }
        }
        
        Debug.Log($"[DragBoxManager] 第一阶段：连锁爆炸完成，总共销毁了 {totalYellowFrogsDestroyed} 只黄色青蛙，爆炸影响 {explosionAffectedFrogs.Count} 只其他青蛙。");

        // 等待间隔时间
        if (yellowExplosionDelay > 0f)
        {
            yield return new WaitForSeconds(yellowExplosionDelay);
        }

        // ---------- 第二阶段：销毁被黄色爆炸影响的所有青蛙 ----------
        List<GameObject> explosionAffectedList = new List<GameObject>(explosionAffectedFrogs);
        int destroyedCount = 0;
        foreach (var frog in explosionAffectedList)
        {
            // 跳过已经在第一阶段被销毁的黄色青蛙自身
            if (frog == null) continue;
            // 跳过所有已处理过的黄色青蛙（包括连锁爆炸中处理过的）
            if (processedYellowFrogs.Contains(frog)) continue;

            // 标记为被爆炸影响，这样销毁时不会播放特效和声音
            var frogBase = frog.GetComponent<FrogBase>();
            if (frogBase != null)
            {
                frogBase.SetExplosionAffected(true);
            }

            Destroy(frog);
            destroyedCount++;
        }
        Debug.Log($"[DragBoxManager] 第二阶段：销毁了 {destroyedCount} 只被黄色爆炸影响的青蛙（共 {explosionAffectedList.Count} 只被影响，其中 {explosionAffectedList.Count - destroyedCount} 只已在连锁爆炸中处理）。");

        // 等待一帧，确保销毁完成
        yield return null;

        // ---------- 第三阶段：逐个销毁剩余的所有青蛙（绿色、红色、黑色） ----------
        int blackCount = 0;
        List<GameObject> remainingFrogs = new List<GameObject>();

        // 收集剩余仍存活的青蛙（排除已被爆炸影响的）
        foreach (var frog in frogsInRect)
        {
            if (frog == null) continue;
            // 跳过已被爆炸影响的青蛙
            if (explosionAffectedFrogs.Contains(frog)) continue;
            // 跳过所有已处理过的黄色青蛙（包括连锁爆炸中处理过的）
            if (processedYellowFrogs.Contains(frog)) continue;

            remainingFrogs.Add(frog);
        }

        // 逐个销毁剩余青蛙
        foreach (var frog in remainingFrogs)
        {
            if (frog == null) continue;

            // 检查关卡是否已结束
            if (StageManager.Instance != null && StageManager.Instance.IsStageEnded())
            {
                Debug.Log("[DragBoxManager] 第三阶段：关卡已结束，停止结算。");
                break;
            }

            GreenRedFrog normalFrog = frog.GetComponent<GreenRedFrog>();
            YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();

            // 处理红色青蛙：销毁并扣血
            if (normalFrog != null && normalFrog.IsRed)
            {
                Utils.VibrateShortMedium();
                Destroy(frog);

                // 扣血
                if (StageManager.Instance != null)
                {
                    StageManager.Instance.TakeDamage(1);
                }

                // 检查血量，如果归零则等待间隔后触发失败
                if (StageManager.Instance != null && StageManager.Instance.GetCurrentHealth() <= 0)
                {
                    Debug.Log("[DragBoxManager] 第三阶段：玩家血量归零，等待间隔后触发失败。");
                    yield return new WaitForSeconds(gameOverDelay);
        
                    StageManager.Instance.CheckStageFailed(StageManager.FailureType.HealthDepleted);
        
                    isSettling = false;
                    yield break;
                }

                // 等待销毁间隔
                if (frogDestroyInterval > 0f)
                {
                    yield return new WaitForSeconds(frogDestroyInterval);
                }
                continue;
            }

            // 处理黑色黄黑青蛙
            if (yellowBlackFrog != null && yellowBlackFrog.IsBlack)
            {
                Utils.VibrateShortMedium();
                Destroy(frog);
                blackCount++;

                // 等待销毁间隔
                if (frogDestroyInterval > 0f)
                {
                    yield return new WaitForSeconds(frogDestroyInterval);
                }
                continue;
            }

            // 处理绿色普通青蛙
            if (normalFrog != null && normalFrog.IsGreen)
            {
                Utils.VibrateShortMedium();
                Destroy(frog);

                // 等待销毁间隔
                if (frogDestroyInterval > 0f)
                {
                    yield return new WaitForSeconds(frogDestroyInterval);
                }
                continue;
            }
        }
        Debug.Log($"[DragBoxManager] 第三阶段：逐个销毁了 {remainingFrogs.Count} 只剩余青蛙，其中黑色={blackCount}。");

        // 根据黑色数量触发"增加变色"机制：
        // 每消除一只黑色青蛙，就触发一次 ConvertGreenFrogsToRed(blackFrogConversionCount)
        if (blackCount > 0)
        {
            int totalConvertCount = blackFrogConversionCount * blackCount;
            if (totalConvertCount > 0)
            {
                ConvertGreenFrogsToRed(totalConvertCount);
            }
        }

        int totalEliminated = yellowFrogs.Count + redFrogs.Count + otherFrogs.Count;
        Debug.Log($"[DragBoxManager] 框选结算完成：总共消除 {totalEliminated} 只青蛙。剩余青蛙数量：{StageManager.Instance?.GetRemainingFrogCount() ?? 0}");

        // 如果成功消除了青蛙，通知 StageManager 玩家进行了划框操作
        if (totalEliminated > 0 && StageManager.Instance != null)
        {
            StageManager.Instance.OnPlayerDraggedBox();
        }

        // 解冻所有仍存活的被框选青蛙（如果还有的话）
        UnfreezeSettlingFrogs();

        // 清空当前结算列表
        currentSettlingFrogs.Clear();

        // 结算完成，允许新的划框
        isSettling = false;
    }

    /// <summary>
    /// 销毁红色青蛙（特效会在 NormalFrog 的 OnDestroy 中自动播放）
    /// </summary>
    private void DestroyRedFrogWithEffect(GameObject frog)
    {
        if (frog == null) return;

        // 销毁青蛙（OnDestroy 会自动根据青蛙类型播放对应的特效）
        Destroy(frog);
    }

    /// <summary>
    /// 使用当前 selectionRect 作为范围，尝试消除其中的青蛙（基于碰撞体检测）。
    /// 结算规则：
    /// 1. 每次所有被框到的青蛙最终都会被消除。
    /// 2. 先结算黄色黄黑青蛙的爆炸（可连锁），爆炸波及到的其他青蛙：
    ///    - 如果是黄色黄黑青蛙 => 继续触发其爆炸；
    ///    - 其它颜色青蛙 => 直接被炸死，不触发任何额外能力。
    /// 3. 在黄色爆炸之后，再结算"剩余仍存活且在本次框选列表中的青蛙"：
    ///    - 绿色 NormalFrog：正常消除；
    ///    - 红色 NormalFrog：消除且每消除 1 只红色，扣 1 滴血；
    ///    - 黑色 YellowBlackFrog：消除并触发布局中既有的"增加变色"机制。
    ///
    /// 选中判定通过 Physics2D.OverlapBoxAll 对 selectionRect 覆盖范围进行检测，
    /// 再从碰撞体上查找 NormalFrog / YellowBlackFrog 脚本。
    /// 为了调试方便，这里会输出若干 Debug 日志，帮助观察每一步实际结算到的青蛙数量和类型。
    /// </summary>
    private void TryEliminateFrogsInSelection()
    {
        if (selectionRect == null)
        {
            Debug.LogWarning("[DragBoxManager] TryEliminateFrogsInSelection：selectionRect 为空，无法结算。");
            return;
        }

        // ---------- 第 0 步：通过碰撞体查找当前矩形范围内的所有青蛙 ----------
        Vector3 center = selectionRect.position;
        Vector2 rectSize = GetSelectionRectSize();
        Vector3 halfSize = new Vector3(rectSize.x * 0.5f, rectSize.y * 0.5f, 0f);
        Vector2 boxSize = new Vector2(rectSize.x, rectSize.y);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, boxSize, 0f);
        if (hits == null || hits.Length == 0)
        {
            Debug.Log("[DragBoxManager] TryEliminateFrogsInSelection：OverlapBox 命中 0 个碰撞体，本次不结算。");
            return;
        }

        // 使用 HashSet 去重（同一只青蛙可能有多个碰撞体）
        HashSet<GameObject> frogSet = new HashSet<GameObject>();
        List<GameObject> frogsInRect = new List<GameObject>();

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            GreenRedFrog normal = hit.GetComponentInParent<GreenRedFrog>();
            YellowBlackFrog yb = hit.GetComponentInParent<YellowBlackFrog>();

            GameObject frogGo = null;
            if (normal != null)
            {
                frogGo = normal.gameObject;
            }
            else if (yb != null)
            {
                frogGo = yb.gameObject;
            }

            // 只关心挂了这两种脚本的对象
            if (frogGo == null) continue;
            
            // 跳过被隔离的青蛙（无法被选中）
            FrogBase frogBase = frogGo.GetComponent<FrogBase>();
            if (frogBase != null && frogBase.IsIsolated)
            {
                continue;
            }
            
            if (frogSet.Add(frogGo))
            {
                frogsInRect.Add(frogGo);
            }
        }

        Debug.Log($"[DragBoxManager] TryEliminateFrogsInSelection：OverlapBox 命中碰撞体 {hits.Length} 个，识别出青蛙对象 {frogsInRect.Count} 个。");

        if (frogsInRect.Count == 0)
        {
            // 框里没有青蛙，就什么都不做
            return;
        }

        // ---------- 第 1 步：结算黄色爆炸（可连锁，仅对黄黑青蛙生效） ----------
        foreach (var frog in frogsInRect)
        {
            if (frog == null) continue;

            YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();
            if (yellowBlackFrog != null && yellowBlackFrog.IsYellow)
            {
                // 黄色青蛙：触发其爆炸效果（在 YellowBlackFrog 上配置爆炸半径）
                yellowBlackFrog.TriggerExplosion();
            }
        }
        Debug.Log($"[DragBoxManager] TryEliminateFrogsInSelection：第 1 步黄色爆炸阶段结束。");

        // ---------- 第 2 步：结算其余被框到的青蛙 ----------
        int eliminatedCount = 0;
        int redHitCount = 0;
        int blackCount = 0;

        foreach (var frog in frogsInRect)
        {
            // 注意：在第 1 步中，部分青蛙可能已经被爆炸销毁，此处需要跳过
            if (frog == null) continue;

            GreenRedFrog normalFrog = frog.GetComponent<GreenRedFrog>();
            YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();

            // 优先处理黑色黄黑青蛙（只处理仍然存活且为黑色的）
            if (yellowBlackFrog != null && yellowBlackFrog.IsBlack)
            {
                Destroy(frog);
                eliminatedCount++;
                blackCount++;
                continue;
            }

            // 处理普通绿红青蛙
            if (normalFrog != null)
            {
                if (normalFrog.IsRed)
                {
                    // 红色：消除并累计扣血次数
                    Destroy(frog);
                    eliminatedCount++;
                    redHitCount++;
                }
                else
                {
                    // 绿色：正常消除
                    Destroy(frog);
                    eliminatedCount++;
                }

                continue;
            }

            // 其他类型的对象（比如特殊测试物体），目前仅打印日志做调试
            Debug.LogWarning($"[DragBoxManager] TryEliminateFrogsInSelection：命中未知类型对象 {frog.name}，无 NormalFrog / YellowBlackFrog 组件，本次未销毁。");
        }

        // 根据红色数量扣血：每消除一只红色青蛙扣 1 滴血
        if (redHitCount > 0 && StageManager.Instance != null)
        {
            StageManager.Instance.TakeDamage(redHitCount);
        }

        // 根据黑色数量触发“增加变色”机制：
        // 每消除一只黑色青蛙，就触发一次 ConvertGreenFrogsToRed(blackFrogConversionCount)
        if (blackCount > 0)
        {
            int totalConvertCount = blackFrogConversionCount * blackCount;
            if (totalConvertCount > 0)
            {
                ConvertGreenFrogsToRed(totalConvertCount);
            }
        }

        Debug.Log($"[DragBoxManager] 框选结算完成：总共消除 {eliminatedCount} 只青蛙，其中红色(扣血)={redHitCount}，黑色(触发变色)={blackCount}。剩余青蛙数量：{StageManager.Instance.GetRemainingFrogCount()}");
    }

    /// <summary>
    /// 旧版本的消除方法（当 StageManager 不可用时使用）
    /// </summary>
    private void TryEliminateFrogsInSelectionLegacy()
    {
        // 找到场景中所有 NormalFrog
        GreenRedFrog[] allFrogs = FindObjectsOfType<GreenRedFrog>();
        if (allFrogs == null || allFrogs.Length == 0)
        {
            return;
        }

        // 收集矩形内的青蛙
        List<GreenRedFrog> frogsInRect = new List<GreenRedFrog>();

        foreach (var frog in allFrogs)
        {
            if (frog == null) continue;

            Vector3 pos = frog.transform.position;
            if (IsPointInsideSelectionRect(pos))
            {
                frogsInRect.Add(frog);
            }
        }

        if (frogsInRect.Count == 0)
        {
            return;
        }

        // 如果有任意一只青蛙是红色，则本次无事发生
        bool hasRed = false;
        foreach (var frog in frogsInRect)
        {
            if (frog != null && frog.IsRed)
            {
                hasRed = true;
                break;
            }
        }

        if (hasRed)
        {
            Debug.Log("[DragBoxManager] 选择区域内存在红色青蛙，本次不消除。");
            return;
        }

        // 否则：全部都是白色青蛙 => 消除这些青蛙
        foreach (var frog in frogsInRect)
        {
            if (frog != null)
            {
                Destroy(frog.gameObject);
            }
        }

        Debug.Log($"[DragBoxManager] 成功消除 {frogsInRect.Count} 只绿色青蛙。");
    }

    /// <summary>
    /// 将指定数量的绿色青蛙（不变色的）转换为红色青蛙（可变色的）
    /// 由于无法直接判断青蛙的类型，我们采用随机选择的方式：
    /// 找到所有 NormalFrog，随机选择指定数量，将它们转为红色类型
    /// </summary>
    private void ConvertGreenFrogsToRed(int count)
    {
        // 找到所有 NormalFrog
        GreenRedFrog[] allFrogs = FindObjectsOfType<GreenRedFrog>();
        if (allFrogs == null || allFrogs.Length == 0)
        {
            Debug.LogWarning("[DragBoxManager] 场景中没有找到 NormalFrog，无法转换。");
            return;
        }

        // 随机打乱数组
        List<GreenRedFrog> shuffledFrogs = new List<GreenRedFrog>(allFrogs);
        
        // Fisher-Yates 洗牌算法
        for (int i = shuffledFrogs.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            GreenRedFrog temp = shuffledFrogs[i];
            shuffledFrogs[i] = shuffledFrogs[j];
            shuffledFrogs[j] = temp;
        }

        // 选择前 count 个青蛙，将它们转为红色类型（跳过被隔离的青蛙）
        int convertedCount = 0;
        for (int i = 0; i < shuffledFrogs.Count && convertedCount < count; i++)
        {
            if (shuffledFrogs[i] != null)
            {
                // 跳过被隔离的青蛙（黑色转换无法影响被隔离的青蛙）
                FrogBase frogBase = shuffledFrogs[i].GetComponent<FrogBase>();
                if (frogBase != null && frogBase.IsIsolated)
                {
                    continue;
                }
                
                shuffledFrogs[i].InitializeColor(GreenRedFrog.FrogColorType.Red);
                convertedCount++;
            }
        }

        Debug.Log($"[DragBoxManager] 成功转换 {convertedCount} 只青蛙为红色类型。");
    }

    /// <summary>
    /// 判断某个世界坐标点是否在当前 selectionRect 矩形范围内（仅考虑 X/Y）。
    /// </summary>
    private bool IsPointInsideSelectionRect(Vector3 worldPos)
    {
        if (selectionRect == null)
        {
            return false;
        }

        Vector3 center = selectionRect.position;
        Vector2 rectSize = GetSelectionRectSize();
        Vector3 halfSize = new Vector3(rectSize.x * 0.5f, rectSize.y * 0.5f, 0f);

        float minX = center.x - halfSize.x;
        float maxX = center.x + halfSize.x;
        float minY = center.y - halfSize.y;
        float maxY = center.y + halfSize.y;

        return worldPos.x >= minX && worldPos.x <= maxX &&
               worldPos.y >= minY && worldPos.y <= maxY;
    }

    /// <summary>
    /// 根据当前 selectionRect 范围更新青蛙头顶“选择中”图标。
    /// </summary>
    private void UpdateFrogSelectingIcons()
    {
        if (selectionRect == null || worldCamera == null)
        {
            return;
        }

        Vector3 center = selectionRect.position;
        Vector2 rectSize = GetSelectionRectSize();
        Vector3 halfSize = new Vector3(rectSize.x * 0.5f, rectSize.y * 0.5f, 0f);
        Vector2 boxSize = new Vector2(rectSize.x, rectSize.y);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, boxSize, 0f);

        HashSet<FrogBase> newSet = new HashSet<FrogBase>();

        if (hits != null && hits.Length > 0)
        {
            foreach (var hit in hits)
            {
                if (hit == null) continue;

                // 只要是继承自 FrogBase 的青蛙都可以显示头顶图标
                FrogBase frog = hit.GetComponentInParent<FrogBase>();
                if (frog == null) continue;

                // 跳过被隔离的青蛙（不显示头顶图标）
                if (frog.IsIsolated) continue;

                newSet.Add(frog);
            }
        }

        // 处理离开框的青蛙：关闭“选择中”图标
        foreach (var frog in highlightedFrogs)
        {
            if (frog == null) continue;
            if (!newSet.Contains(frog))
            {
                frog.SetSelectingIcon(false);
            }
        }

        // 处理新进入框的青蛙：打开“选择中”图标
        foreach (var frog in newSet)
        {
            if (frog == null) continue;
            if (!highlightedFrogs.Contains(frog))
            {
                frog.SetSelectingIcon(true);
            }
        }

        highlightedFrogs.Clear();
        foreach (var frog in newSet)
        {
            highlightedFrogs.Add(frog);
        }
    }

    /// <summary>
    /// 清空所有当前高亮青蛙的"选择中"图标。
    /// </summary>
    private void ClearAllSelectingIcons()
    {
        foreach (var frog in highlightedFrogs)
        {
            if (frog == null) continue;
            frog.SetSelectingIcon(false);
        }
        highlightedFrogs.Clear();
    }

    /// <summary>
    /// 解冻当前结算列表中的所有青蛙（公共方法，供外部调用，如复活时）
    /// </summary>
    public void UnfreezeSettlingFrogs()
    {
        if (currentSettlingFrogs == null || currentSettlingFrogs.Count == 0)
        {
            return;
        }

        Debug.Log($"[DragBoxManager] 解冻 {currentSettlingFrogs.Count} 只青蛙。");

        foreach (var frog in currentSettlingFrogs)
        {
            if (frog == null) continue;

            GreenRedFrog normalFrog = frog.GetComponent<GreenRedFrog>();
            YellowBlackFrog yellowBlackFrog = frog.GetComponent<YellowBlackFrog>();

            if (normalFrog != null)
            {
                normalFrog.SetFrozen(false);
            }
            else if (yellowBlackFrog != null)
            {
                yellowBlackFrog.SetFrozen(false);
            }

            // 清理头顶图标
            var frogBase = frog.GetComponent<FrogBase>();
            if (frogBase != null)
            {
                frogBase.ClearTopIcon();
            }
        }
    }
}
