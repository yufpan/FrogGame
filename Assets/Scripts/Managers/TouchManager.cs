using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class TouchManager : MonoBehaviour
{
    /// <summary>
    /// 全局访问的单例实例
    /// </summary>
    public static TouchManager Instance { get; private set; }

    /// <summary>
    /// 拖框开始事件：参数为拖拽起点（屏幕坐标）
    /// </summary>
    public event Action<Vector2> OnDragBoxStart;

    /// <summary>
    /// 拖框中事件：参数为起点和当前点（屏幕坐标）
    /// </summary>
    public event Action<Vector2, Vector2> OnDragBox;

    /// <summary>
    /// 拖框结束事件：参数为起点和终点（屏幕坐标）
    /// </summary>
    public event Action<Vector2, Vector2> OnDragBoxEnd;

    /// <summary>
    /// 拖框被取消事件（例如拖拽过程中移到 UI 上、或游戏状态变为不可交互时）
    /// 仅用于让表现层（如 DragBoxManager）隐藏框，不做结算逻辑。
    /// </summary>
    public event Action OnDragBoxCancel;

    /// <summary>
    /// 指针按下事件：参数为按下位置（屏幕坐标）
    /// </summary>
    public event Action<Vector2> OnPointerDown;

    /// <summary>
    /// 指针抬起事件：参数为抬起位置（屏幕坐标）
    /// </summary>
    public event Action<Vector2> OnPointerUp;

    /// <summary>
    /// 点击事件：参数为点击位置（屏幕坐标）
    /// 只有在按下和抬起之间没有发生拖拽时才会触发
    /// </summary>
    public event Action<Vector2> OnClick;

    /// <summary>
    /// 判定拖拽的最小像素距离，避免轻触就当成拖拽
    /// </summary>
    [SerializeField]
    private float dragThreshold = 10f;

    private bool isDragging = false;
    private Vector2 dragStartPos;
    
    // 用于跟踪点击状态
    private bool isPointerDown = false;
    private Vector2 pointerDownPos;
    
    // 用于跟踪上次的状态，避免重复打印日志
    private bool lastShouldProcessInput = true;
    private GameManager.GameState? lastGameState = null;
    private bool? lastStageEnded = null;

    /// <summary>
    /// 检查是否应该处理输入（游戏进行中且不在UI上）
    /// </summary>
    private bool ShouldProcessInput(bool checkUI = true)
    {
        // 检查 GameManager 状态
        if (GameManager.Instance != null)
        {
            var currentState = GameManager.Instance.CurrentState;
            if (currentState != GameManager.GameState.Playing)
            {
                // 只在状态变化时打印日志
                if (lastGameState != currentState)
                {
                    Debug.Log($"[TouchManager] ShouldProcessInput: GameManager状态不是Playing，当前状态={currentState}");
                    lastGameState = currentState;
                }
                return false;
            }
            lastGameState = currentState;
        }
        
        // 检查 StageManager 是否已结束
        if (StageManager.Instance != null)
        {
            bool stageEnded = StageManager.Instance.IsStageEnded();
            if (stageEnded)
            {
                // 只在状态变化时打印日志
                if (lastStageEnded != stageEnded)
                {
                    Debug.Log($"[TouchManager] ShouldProcessInput: StageManager已结束，不处理输入");
                    lastStageEnded = stageEnded;
                }
                return false;
            }
            lastStageEnded = stageEnded;
        }
        
        // 如果指定要检查UI，且点击在UI上，不处理输入（让UI系统处理）
        // 注意：这里不打印日志，因为UI检测会在有实际输入时进行
        if (checkUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 检查指定屏幕位置是否在UI上
    /// </summary>
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null)
        {
            return false;
        }
        
        // 使用 PointerEventData 来检查指定位置是否在UI上
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPos;
        
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        bool isOverUI = results.Count > 0;
        // 移除频繁的日志，只在必要时打印（例如调试时）
        // if (isOverUI)
        // {
        //     Debug.Log($"[TouchManager] IsPointerOverUI: 位置{screenPos}在UI上，命中{results.Count}个UI元素");
        // }
        
        return isOverUI;
    }

    private void Awake()
    {
        // 单例初始化
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[TouchManager] 检测到多个 TouchManager 实例，销毁重复的实例。");
            Destroy(gameObject);
            return;
        }
    }

    void Update()
    {
        // 检查游戏状态（不检查UI，因为UI检查应该在有实际输入时进行）
        bool shouldProcess = ShouldProcessInput(checkUI: false);
        
        // 只在状态变化时打印日志
        if (shouldProcess != lastShouldProcessInput)
        {
            if (!shouldProcess)
            {
                Debug.Log($"[TouchManager] Update: 游戏状态不允许处理输入");
            }
            else
            {
                Debug.Log($"[TouchManager] Update: 游戏状态允许处理输入");
            }
            lastShouldProcessInput = shouldProcess;
        }
        
        if (!shouldProcess)
        {
            // 如果正在拖拽，需要结束它
            if (isDragging)
            {
                Debug.Log($"[TouchManager] Update: 游戏状态不允许处理输入，取消拖拽。isDragging={isDragging}");
                // 通知外部：本次拖拽被取消（不触发结算）
                OnDragBoxCancel?.Invoke();
                isDragging = false;
            }
            return;
        }
        
        // 在 Editor 或 PC 上用鼠标模拟，方便调试
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0)
        {
            // 如果手指抬起，结束拖拽
            bool wasDraggingBeforeEnd = isDragging;
            bool isOverUI = IsPointerOverUI(dragStartPos);
            
            if (isDragging)
            {
                Debug.Log($"[TouchManager] HandleTouchInput: touchCount=0，处理拖拽结束。起点={dragStartPos}");
                // 注意：这里 touchCount 已经是 0，无法获取最后位置
                // 使用 dragStartPos 作为终点位置进行检查
                // 如果起点在UI上，取消拖拽；否则正常结束
                if (isOverUI)
                {
                    Debug.Log($"[TouchManager] HandleTouchInput: 起点在UI上，取消拖拽");
                    OnDragBoxCancel?.Invoke();
                }
                else
                {
                    Debug.Log($"[TouchManager] HandleTouchInput: 起点不在UI上，正常结束拖拽");
                    EndDrag(dragStartPos);
                }
            }
            
            // 处理指针抬起事件
            if (isPointerDown)
            {
                OnPointerUp?.Invoke(dragStartPos);
                
                // 如果没有发生拖拽，且不在UI上，触发点击事件
                if (!wasDraggingBeforeEnd && !isOverUI)
                {
                    Debug.Log($"[TouchManager] HandleTouchInput: touchCount=0，触发点击事件，位置={dragStartPos}");
                    OnClick?.Invoke(dragStartPos);
                }
                
                isPointerDown = false;
            }
            
            // 重置拖拽状态
            isDragging = false;
            return;
        }

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
            {
                // 重置拖拽状态，确保每次新的触摸开始时状态干净
                bool wasDragging = isDragging;
                isDragging = false;
                if (wasDragging)
                {
                    Debug.Log($"[TouchManager] TouchPhase.Began: 检测到新的触摸开始，重置之前的拖拽状态");
                }
                
                // 重置指针状态
                if (isPointerDown)
                {
                    // 如果之前有未完成的指针按下，先触发抬起事件
                    OnPointerUp?.Invoke(pointerDownPos);
                    isPointerDown = false;
                }
                
                // 检查是否在UI上
                bool isOverUI = IsPointerOverUI(touch.position);
                
                // 再次检查是否应该处理（防止在按下和移动之间状态改变）
                if (!ShouldProcessInput(checkUI: false))
                {
                    Debug.Log($"[TouchManager] TouchPhase.Began: 游戏状态不允许处理输入");
                    return;
                }
                
                // 如果在UI上，不处理输入
                if (isOverUI)
                {
                    Debug.Log($"[TouchManager] TouchPhase.Began: 触摸在UI上，不处理输入");
                    return;
                }
                
                dragStartPos = touch.position;
                pointerDownPos = touch.position;
                isPointerDown = true;
                Debug.Log($"[TouchManager] TouchPhase.Began: 记录起点位置={dragStartPos}");
                OnPointerDown?.Invoke(touch.position);
                break;
            }

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
            {
                // 如果当前点在UI上，取消拖拽
                if (IsPointerOverUI(touch.position))
                {
                    if (isDragging)
                    {
                        Debug.Log($"[TouchManager] TouchPhase.Moved/Stationary: 当前点在UI上({touch.position})，取消拖拽");
                        // 通知外部：本次拖拽被取消（不触发结算）
                        OnDragBoxCancel?.Invoke();
                        isDragging = false;
                    }
                    return;
                }
                
                Vector2 currentPos = touch.position;
                float distance = Vector2.Distance(dragStartPos, currentPos);

                if (!isDragging && distance >= dragThreshold)
                {
                    // 触发开始拖拽
                    Debug.Log($"[TouchManager] TouchPhase.Moved: 距离({distance:F2}) >= 阈值({dragThreshold})，开始拖拽。起点={dragStartPos}, 当前点={currentPos}");
                    isDragging = true;
                    OnDragBoxStart?.Invoke(dragStartPos);
                }

                if (isDragging)
                {
                    OnDragBox?.Invoke(dragStartPos, currentPos);
                }
                break;
            }

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
            {
                bool wasDraggingBeforeEnd = isDragging;
                bool isOverUI = IsPointerOverUI(touch.position);
                
                if (isDragging)
                {
                    Debug.Log($"[TouchManager] TouchPhase.Ended/Canceled: 拖拽结束。终点={touch.position}, 在UI上={isOverUI}");
                    // 如果终点在UI上，取消拖拽而不是结束拖拽
                    if (isOverUI)
                    {
                        Debug.Log($"[TouchManager] TouchPhase.Ended/Canceled: 终点在UI上，取消拖拽");
                        OnDragBoxCancel?.Invoke();
                    }
                    else
                    {
                        Debug.Log($"[TouchManager] TouchPhase.Ended/Canceled: 终点不在UI上，正常结束拖拽");
                        EndDrag(touch.position);
                    }
                }
                else
                {
                    Debug.Log($"[TouchManager] TouchPhase.Ended/Canceled: 未在拖拽状态，仅重置标志");
                }
                
                // 处理指针抬起事件
                if (isPointerDown)
                {
                    OnPointerUp?.Invoke(touch.position);
                    
                    // 如果没有发生拖拽，且不在UI上，触发点击事件
                    if (!wasDraggingBeforeEnd && !isOverUI)
                    {
                        Debug.Log($"[TouchManager] TouchPhase.Ended/Canceled: 触发点击事件，位置={touch.position}");
                        OnClick?.Invoke(touch.position);
                    }
                    
                    isPointerDown = false;
                }
                
                isDragging = false;
                break;
            }
        }
    }

    private void HandleMouseInput()
    {
        // 鼠标左键按下
        if (Input.GetMouseButtonDown(0))
        {
            // 重置拖拽状态，确保每次新的点击开始时状态干净
            bool wasDragging = isDragging;
            isDragging = false;
            if (wasDragging)
            {
                Debug.Log($"[TouchManager] GetMouseButtonDown: 检测到新的鼠标按下，重置之前的拖拽状态");
            }
            
            // 重置指针状态
            if (isPointerDown)
            {
                // 如果之前有未完成的指针按下，先触发抬起事件
                OnPointerUp?.Invoke(pointerDownPos);
                isPointerDown = false;
            }
            
            // 检查是否在UI上
            bool isOverUI = IsPointerOverUI(Input.mousePosition);
            
            // 再次检查是否应该处理（防止在按下和移动之间状态改变）
            if (!ShouldProcessInput(checkUI: false))
            {
                Debug.Log($"[TouchManager] GetMouseButtonDown: 游戏状态不允许处理输入");
                return;
            }
            
            // 如果在UI上，不处理输入
            if (isOverUI)
            {
                Debug.Log($"[TouchManager] GetMouseButtonDown: 鼠标在UI上，不处理输入");
                return;
            }
            
            dragStartPos = Input.mousePosition;
            pointerDownPos = Input.mousePosition;
            isPointerDown = true;
            Debug.Log($"[TouchManager] GetMouseButtonDown: 记录起点位置={dragStartPos}");
            OnPointerDown?.Invoke(Input.mousePosition);
        }

        // 鼠标移动或按住
        if (Input.GetMouseButton(0))
        {
            // 如果当前点在UI上，取消拖拽
            if (IsPointerOverUI(Input.mousePosition))
            {
                if (isDragging)
                {
                    Debug.Log($"[TouchManager] GetMouseButton: 当前点在UI上({Input.mousePosition})，取消拖拽");
                    // 通知外部：本次拖拽被取消（不触发结算）
                    OnDragBoxCancel?.Invoke();
                    isDragging = false;
                }
                return;
            }
            
            Vector2 currentPos = Input.mousePosition;
            float distance = Vector2.Distance(dragStartPos, currentPos);

            if (!isDragging && distance >= dragThreshold)
            {
                Debug.Log($"[TouchManager] GetMouseButton: 距离({distance:F2}) >= 阈值({dragThreshold})，开始拖拽。起点={dragStartPos}, 当前点={currentPos}");
                isDragging = true;
                OnDragBoxStart?.Invoke(dragStartPos);
            }

            if (isDragging)
            {
                OnDragBox?.Invoke(dragStartPos, currentPos);
            }
        }

        // 鼠标抬起
        if (Input.GetMouseButtonUp(0))
        {
            bool wasDraggingBeforeEnd = isDragging;
            bool isOverUI = IsPointerOverUI(Input.mousePosition);
            
            if (isDragging)
            {
                Debug.Log($"[TouchManager] GetMouseButtonUp: 拖拽结束。终点={Input.mousePosition}, 在UI上={isOverUI}");
                // 如果终点在UI上，取消拖拽而不是结束拖拽
                if (isOverUI)
                {
                    Debug.Log($"[TouchManager] GetMouseButtonUp: 终点在UI上，取消拖拽");
                    OnDragBoxCancel?.Invoke();
                }
                else
                {
                    Debug.Log($"[TouchManager] GetMouseButtonUp: 终点不在UI上，正常结束拖拽");
                    EndDrag(Input.mousePosition);
                }
            }
            else
            {
                Debug.Log($"[TouchManager] GetMouseButtonUp: 未在拖拽状态，仅重置标志");
            }
            
            // 处理指针抬起事件
            if (isPointerDown)
            {
                OnPointerUp?.Invoke(Input.mousePosition);
                
                // 如果没有发生拖拽，且不在UI上，触发点击事件
                if (!wasDraggingBeforeEnd && !isOverUI)
                {
                    Debug.Log($"[TouchManager] GetMouseButtonUp: 触发点击事件，位置={Input.mousePosition}");
                    OnClick?.Invoke(Input.mousePosition);
                }
                
                isPointerDown = false;
            }
            
            isDragging = false;
        }
    }

    private void EndDrag(Vector2 endPos)
    {
        Debug.Log($"[TouchManager] EndDrag: 触发OnDragBoxEnd事件。起点={dragStartPos}, 终点={endPos}");
        OnDragBoxEnd?.Invoke(dragStartPos, endPos);
    }
}