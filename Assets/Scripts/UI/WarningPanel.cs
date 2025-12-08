using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WarningPanel : BasePanel
{
    public override string PanelName => "WarningPanel";

    [SerializeField] private GameObject _arrows;
    
    [Header("动画配置")]
    [Tooltip("箭头移动幅度（像素）")]
    [SerializeField] private float _arrowMoveRange = 20f;
    
    [Tooltip("箭头移动速度（每秒循环次数）")]
    [SerializeField] private float _arrowMoveSpeed = 2f;
    
    [Header("自动关闭配置")]
    [Tooltip("面板自动关闭时间（秒）")]
    [SerializeField] private float _autoCloseDuration = 3f;

    private Coroutine _arrowAnimationCoroutine;
    private Coroutine _autoCloseCoroutine;
    private RectTransform _arrowsRectTransform;
    private Vector2 _arrowsOriginalPosition;

    protected override void Awake()
    {
        base.Awake();
        
        // 获取箭头的RectTransform组件
        if (_arrows != null)
        {
            _arrowsRectTransform = _arrows.GetComponent<RectTransform>();
            if (_arrowsRectTransform != null)
            {
                _arrowsOriginalPosition = _arrowsRectTransform.anchoredPosition;
            }
        }
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        
        // 重置箭头位置
        if (_arrowsRectTransform != null)
        {
            _arrowsRectTransform.anchoredPosition = _arrowsOriginalPosition;
        }
        
        // 启动箭头动画
        if (_arrowsRectTransform != null)
        {
            StopArrowAnimation();
            _arrowAnimationCoroutine = StartCoroutine(ArrowMoveAnimation());
        }
        
        // 启动自动关闭协程
        StopAutoClose();
        _autoCloseCoroutine = StartCoroutine(AutoCloseCoroutine());
    }

    protected override void OnClose()
    {
        // 停止所有协程
        StopArrowAnimation();
        StopAutoClose();
        
        // 重置箭头位置
        if (_arrowsRectTransform != null)
        {
            _arrowsRectTransform.anchoredPosition = _arrowsOriginalPosition;
        }
        
        base.OnClose();
    }

    /// <summary>
    /// 箭头上下移动动画协程
    /// </summary>
    private IEnumerator ArrowMoveAnimation()
    {
        if (_arrowsRectTransform == null) yield break;
        
        float time = 0f;
        
        while (true)
        {
            time += Time.unscaledDeltaTime * _arrowMoveSpeed;
            
            // 使用sin函数实现平滑的上下移动
            float offset = Mathf.Sin(time * Mathf.PI * 2f) * _arrowMoveRange;
            _arrowsRectTransform.anchoredPosition = _arrowsOriginalPosition + Vector2.up * offset;
            
            yield return null;
        }
    }

    /// <summary>
    /// 自动关闭协程
    /// </summary>
    private IEnumerator AutoCloseCoroutine()
    {
        yield return new WaitForSecondsRealtime(_autoCloseDuration);
        
        // 自动关闭面板
        Close();
    }

    /// <summary>
    /// 停止箭头动画
    /// </summary>
    private void StopArrowAnimation()
    {
        if (_arrowAnimationCoroutine != null)
        {
            StopCoroutine(_arrowAnimationCoroutine);
            _arrowAnimationCoroutine = null;
        }
    }

    /// <summary>
    /// 停止自动关闭协程
    /// </summary>
    private void StopAutoClose()
    {
        if (_autoCloseCoroutine != null)
        {
            StopCoroutine(_autoCloseCoroutine);
            _autoCloseCoroutine = null;
        }
    }
}