using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToastPanel : BasePanel
{
    public override string PanelName => "ToastPanel";

    [SerializeField] private TextMeshProUGUI _toastText;
    [SerializeField] private GameObject _toast;
    
    [Header("Toast动画配置")]
    [Tooltip("默认显示时长（秒）")]
    [SerializeField] private float _defaultDisplayDuration = 2f;
    
    [Tooltip("淡出动画时长（秒）")]
    [SerializeField] private float _fadeOutDuration = 0.5f;
    
    [Tooltip("向上移动的距离（像素）")]
    [SerializeField] private float _moveUpDistance = 50f;
    
    private Coroutine _toastCoroutine;
    private RectTransform _toastRectTransform;
    private CanvasGroup _toastCanvasGroup;
    private Vector3 _originalPosition;
    private Color _originalTextColor;
    
    protected override void Awake()
    {
        base.Awake();
        
        // 获取或添加必要的组件
        if (_toast != null)
        {
            _toastRectTransform = _toast.GetComponent<RectTransform>();
            if (_toastRectTransform == null)
            {
                _toastRectTransform = _toast.AddComponent<RectTransform>();
            }
            
            _toastCanvasGroup = _toast.GetComponent<CanvasGroup>();
            if (_toastCanvasGroup == null)
            {
                _toastCanvasGroup = _toast.AddComponent<CanvasGroup>();
            }
            
            // 记录原始位置和颜色
            if (_toastRectTransform != null)
            {
                _originalPosition = _toastRectTransform.anchoredPosition;
            }
            
            if (_toastText != null)
            {
                _originalTextColor = _toastText.color;
            }
        }
    }
    
    public override void Open()
    {
        base.Open();
    }

    public override void Close()
    {
        // 停止正在运行的toast协程
        if (_toastCoroutine != null)
        {
            StopCoroutine(_toastCoroutine);
            _toastCoroutine = null;
        }
        
        // 重置toast状态
        ResetToastState();
        
        base.Close();
    }
    
    /// <summary>
    /// 显示toast提示
    /// </summary>
    /// <param name="message">要显示的文本</param>
    /// <param name="displayDuration">显示时长（秒），默认使用配置的值</param>
    public void ShowToast(string message, float displayDuration = -1f)
    {
        if (_toast == null || _toastText == null)
        {
            Debug.LogWarning("[ToastPanel] Toast组件未配置，无法显示提示");
            return;
        }
        
        // 如果传入了-1，使用默认值
        if (displayDuration < 0)
        {
            displayDuration = _defaultDisplayDuration;
        }
        
        // 如果已经有toast正在显示，停止它
        if (_toastCoroutine != null)
        {
            StopCoroutine(_toastCoroutine);
            _toastCoroutine = null;
        }
        
        // 重置toast状态
        ResetToastState();
        
        // 设置文本
        _toastText.text = message;
        
        // 确保面板是打开的
        if (!IsOpen)
        {
            Open();
        }
        
        // 显示toast并启动协程
        _toast.SetActive(true);
        _toastCoroutine = StartCoroutine(ShowToastCoroutine(displayDuration));
    }
    
    /// <summary>
    /// Toast显示协程
    /// </summary>
    private IEnumerator ShowToastCoroutine(float displayDuration)
    {
        // 等待显示时长
        yield return new WaitForSeconds(displayDuration);
        
        // 开始淡出和向上移动动画
        float timer = 0f;
        Vector3 startPosition = _toastRectTransform.anchoredPosition;
        Vector3 targetPosition = startPosition + Vector3.up * _moveUpDistance;
        float startAlpha = _toastCanvasGroup.alpha;
        Color startTextColor = _toastText.color;
        Color targetTextColor = new Color(startTextColor.r, startTextColor.g, startTextColor.b, 0f);
        
        while (timer < _fadeOutDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _fadeOutDuration);
            
            // 向上移动
            if (_toastRectTransform != null)
            {
                _toastRectTransform.anchoredPosition = Vector3.Lerp(startPosition, targetPosition, t);
            }
            
            // 淡出（使用CanvasGroup）
            if (_toastCanvasGroup != null)
            {
                _toastCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }
            
            // 文本颜色淡出（备用方案，如果CanvasGroup不生效）
            if (_toastText != null)
            {
                _toastText.color = Color.Lerp(startTextColor, targetTextColor, t);
            }
            
            yield return null;
        }
        
        // 确保最终状态
        if (_toastRectTransform != null)
        {
            _toastRectTransform.anchoredPosition = targetPosition;
        }
        if (_toastCanvasGroup != null)
        {
            _toastCanvasGroup.alpha = 0f;
        }
        if (_toastText != null)
        {
            _toastText.color = targetTextColor;
        }
        
        // 隐藏toast
        _toast.SetActive(false);
        
        // 重置状态
        ResetToastState();
        
        _toastCoroutine = null;
    }
    
    /// <summary>
    /// 重置toast到初始状态
    /// </summary>
    private void ResetToastState()
    {
        if (_toastRectTransform != null)
        {
            _toastRectTransform.anchoredPosition = _originalPosition;
        }
        
        if (_toastCanvasGroup != null)
        {
            _toastCanvasGroup.alpha = 1f;
        }
        
        if (_toastText != null)
        {
            _toastText.color = _originalTextColor;
        }
    }
}