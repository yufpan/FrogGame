using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class TutorialPanel : BasePanel
{
    public override string PanelName => "TutorialPanel";
    [SerializeField] private GameObject _box;
    [SerializeField] private GameObject _finger;
    [SerializeField] private GameObject _start;
    [SerializeField] private GameObject _end;
    [SerializeField] private TextMeshProUGUI _text;
    private string firstText = "画框消除选中的青蛙，不过要注意如果消掉粉色青蛙是会扣血的哦";
    private string lastText = "干得好！消灭所有青蛙过关！";
    
    [Header("动画设置")]
    [Tooltip("拖框动画时长（秒）")]
    [SerializeField] private float _animationDuration = 0.5f;
    
    private RectTransform _boxRectTransform;
    private RectTransform _fingerRectTransform;
    private RectTransform _startRectTransform;
    private RectTransform _endRectTransform;
    
    // Box 起点和终点配置
    private Vector2 _boxStartPosition; // 左上角位置（保持不变）
    private Vector2 _boxStartSize; // 初始大小 (0, 0)
    private Vector2 _boxEndSize; // 终点大小
    
    // Finger 起点和终点配置
    private Vector2 _fingerStartPosition;
    private Vector2 _fingerEndPosition;
    private Coroutine _animationCoroutine;
    private EventTrigger _clickTrigger;
    private bool _isFirstTime = true; // 是否是初次打开
    
    protected override void Awake()
    {
        base.Awake();
        
        // 获取 RectTransform 组件
        if (_box != null)
        {
            _boxRectTransform = _box.GetComponent<RectTransform>();
        }
        
        if (_finger != null)
        {
            _fingerRectTransform = _finger.GetComponent<RectTransform>();
        }
        
        if (_start != null)
        {
            _startRectTransform = _start.GetComponent<RectTransform>();
        }
        
        if (_end != null)
        {
            _endRectTransform = _end.GetComponent<RectTransform>();
        }
        
        // 设置点击监听（任何点击都关闭面板）
        SetupClickToClose();
    }
    
    /// <summary>
    /// 设置点击关闭功能
    /// </summary>
    private void SetupClickToClose()
    {
        // 确保面板有 Image 组件用于接收点击事件
        Image backgroundImage = gameObject.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0); // 透明背景
        }
        backgroundImage.raycastTarget = true;
        
        // 获取或添加 EventTrigger 组件
        _clickTrigger = gameObject.GetComponent<EventTrigger>();
        if (_clickTrigger == null)
        {
            _clickTrigger = gameObject.AddComponent<EventTrigger>();
        }
        
        // 清除旧的监听
        _clickTrigger.triggers.Clear();
        
        // 添加点击事件
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => { OnAnyClick(); });
        _clickTrigger.triggers.Add(entry);
    }
    
    public override void Open()
    {
        base.Open();
        
        // 设置文本内容
        if (_text != null)
        {
            _text.text = _isFirstTime ? firstText : lastText;
        }
        
        // 如果是第二次打开，隐藏box和finger
        if (!_isFirstTime)
        {
            if (_box != null)
            {
                _box.SetActive(false);
            }
            if (_finger != null)
            {
                _finger.SetActive(false);
            }
            // 停止动画协程（如果正在运行）
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }
        else
        {
            // 初次打开，显示box和finger
            if (_box != null)
            {
                _box.SetActive(true);
            }
            if (_finger != null)
            {
                _finger.SetActive(true);
            }
            
            // 初始化动画参数
            InitializeAnimation();
            
            // 开始播放拖框动画
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(PlayDragBoxAnimation());
        }
    }
    
    /// <summary>
    /// 设置是否是初次打开
    /// </summary>
    /// <param name="isFirstTime">是否是初次打开</param>
    public void SetIsFirstTime(bool isFirstTime)
    {
        _isFirstTime = isFirstTime;
    }
    
    public override void Close()
    {
        // 停止动画协程
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        
        base.Close();
    }
    
    /// <summary>
    /// 初始化动画参数
    /// </summary>
    private void InitializeAnimation()
    {
        if (_boxRectTransform == null || _fingerRectTransform == null)
        {
            Debug.LogWarning("[TutorialPanel] Box 或 Finger 的 RectTransform 未找到！");
            return;
        }
        
        if (_startRectTransform == null || _endRectTransform == null)
        {
            Debug.LogWarning("[TutorialPanel] Start 或 End GameObject 的 RectTransform 未找到！");
            return;
        }
        
        // box 初始大小为 (0, 0)，左上角位置不变
        // 起点 GameObject 的位置 = box 的左上角位置（初始大小为 0，所以右下角就是左上角）
        // 终点 GameObject 的位置 = box 的终点右下角位置
        // 终点 GameObject 的大小 = box 的终点大小
        
        // box 的左上角位置（在整个动画中保持不变）
        _boxStartPosition = _startRectTransform.anchoredPosition;
        
        // box 的初始大小
        _boxStartSize = Vector2.zero;
        
        // box 的终点大小（从终点 GameObject 读取）
        _boxEndSize = _endRectTransform.sizeDelta;
        
        // 如果终点大小是 (0, 0)，则根据起点和终点位置计算大小
        if (_boxEndSize.x <= 0 || _boxEndSize.y <= 0)
        {
            Vector2 endBottomRight = _endRectTransform.anchoredPosition;
            Vector2 startTopLeft = _startRectTransform.anchoredPosition;
            // 计算大小：终点右下角 - 起点左上角
            _boxEndSize = new Vector2(
                endBottomRight.x - startTopLeft.x,
                startTopLeft.y - endBottomRight.y // Y 轴向下为负，所以用起点 Y - 终点 Y
            );
        }
        
        // finger 的位置直接从起点和终点 GameObject 读取
        _fingerStartPosition = _startRectTransform.anchoredPosition;
        _fingerEndPosition = _endRectTransform.anchoredPosition;
        
        // 调试信息
        Debug.Log($"[TutorialPanel] Box 起点位置: {_boxStartPosition}, 起点大小: {_boxStartSize}, 终点大小: {_boxEndSize}");
        Debug.Log($"[TutorialPanel] Finger 起点位置: {_fingerStartPosition}, 终点位置: {_fingerEndPosition}");
        
        // 设置 box 和 finger 的初始状态
        _boxRectTransform.anchoredPosition = _boxStartPosition;
        _boxRectTransform.sizeDelta = _boxStartSize;
        _fingerRectTransform.anchoredPosition = _fingerStartPosition;
    }
    
    /// <summary>
    /// 播放拖框动画（循环播放）
    /// </summary>
    private IEnumerator PlayDragBoxAnimation()
    {
        if (_boxRectTransform == null || _fingerRectTransform == null)
        {
            yield break;
        }
        
        float duration = Mathf.Max(0.01f, _animationDuration);
        
        // 循环播放动画
        while (true)
        {
            float elapsed = 0f;
            
            // 从起点到终点的动画
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime，忽略 Time.timeScale 影响
                float t = Mathf.Clamp01(elapsed / duration);
                
                // 线性插值
                // box 的左上角位置保持不变，只改变大小
                _boxRectTransform.anchoredPosition = _boxStartPosition;
                _boxRectTransform.sizeDelta = Vector2.Lerp(_boxStartSize, _boxEndSize, t);
                
                // 更新 finger 的位置
                _fingerRectTransform.anchoredPosition = Vector2.Lerp(_fingerStartPosition, _fingerEndPosition, t);
                
                yield return null;
            }
            
            // 确保到达终点状态
            _boxRectTransform.anchoredPosition = _boxStartPosition; // 左上角位置保持不变
            _boxRectTransform.sizeDelta = _boxEndSize;
            _fingerRectTransform.anchoredPosition = _fingerEndPosition;
            
            // 立即重置到起点，开始下一次循环
            _boxRectTransform.anchoredPosition = _boxStartPosition;
            _boxRectTransform.sizeDelta = _boxStartSize;
            _fingerRectTransform.anchoredPosition = _fingerStartPosition;
        }
    }
    
    /// <summary>
    /// 任何点击都会关闭面板
    /// </summary>
    private void OnAnyClick()
    {
        Close();
    }
}