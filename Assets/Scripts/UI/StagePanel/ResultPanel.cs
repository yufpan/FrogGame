using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ResultPanel : BasePanel
{
    public override string PanelName => "ResultPanel";

    [SerializeField] private Button _menuButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private Image _frogImage;
    [SerializeField] private Slider _progressSlider;
    
    [Header("进度条动画配置")]
    [Tooltip("进度条动画持续时间（秒）")]
    [SerializeField] private float _progressAnimationDuration = 1f;
    [SerializeField] private GameObject _frogStartPos;
    [SerializeField] private GameObject _frogEndPos;

    private Coroutine _progressAnimationCoroutine;

    public override void Open()
    {
        // 先调用 base.Open() 激活 GameObject
        base.Open();

        // 绑定按钮事件
        if (_menuButton != null)
        {
            _menuButton.onClick.AddListener(OnMenuButtonClick);
        }

        if (_retryButton != null)
        {
            _retryButton.onClick.AddListener(OnRetryButtonClick);
        }

        // 更新进度文本
        UpdateProgressText();

        // 播放失败音效
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PauseBGMPlayFXAndResume("Lose");
        }

        // 开始进度条动画（此时 GameObject 已经激活，可以启动协程）
        StartProgressAnimation();
    }

    public override void Close()
    {
        // 解绑按钮事件
        if (_menuButton != null)
        {
            _menuButton.onClick.RemoveListener(OnMenuButtonClick);
        }

        if (_retryButton != null)
        {
            _retryButton.onClick.RemoveListener(OnRetryButtonClick);
        }

        // 停止进度条动画
        if (_progressAnimationCoroutine != null)
        {
            StopCoroutine(_progressAnimationCoroutine);
            _progressAnimationCoroutine = null;
        }

        // 隐藏青蛙图片
        if (_frogImage != null)
        {
            _frogImage.gameObject.SetActive(false);
        }

        base.Close();
    }

    /// <summary>
    /// 菜单按钮点击事件
    /// </summary>
    private void OnMenuButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        // 清理当前关卡的所有青蛙
        ClearAllFrogs();

        // 清除 StageManager 的网格数据
        if (StageManager.Instance != null)
        {
            StageManager.Instance.ClearGrid();
        }

        // 同步GameManager状态为Start
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMenu();
        }

        // 切换到 Menu 场景
        // 黑屏时关闭：ResultPanel 和 StagePanel
        // 场景切换后打开：MenuPanel
        if (SwitchSceneManager.Instance != null)
        {
            var panelsToClose = new List<string> { "ResultPanel", "StagePanel" };
            var panelsToOpen = new List<string> { "MenuPanel" };
            SwitchSceneManager.Instance.SwitchSceneWithFade("Menu", panelsToClose, panelsToOpen);
        }
        else
        {
            // 如果没有 SwitchSceneManager，直接关闭面板并切换场景
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClosePanel("ResultPanel");
                UIManager.Instance.ClosePanel("StagePanel");
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            // 等待场景加载后打开MenuPanel
            StartCoroutine(OpenMenuPanelAfterSceneLoad());
        }
    }

    /// <summary>
    /// 场景加载后打开MenuPanel（用于没有SwitchSceneManager的情况）
    /// </summary>
    private IEnumerator OpenMenuPanelAfterSceneLoad()
    {
        yield return new WaitForEndOfFrame();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel("MenuPanel");
        }
    }

    /// <summary>
    /// 重新开始按钮点击事件
    /// </summary>
    private void OnRetryButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        Debug.Log("[ResultPanel] 重新开始当前关卡");

        // 调用StageManager的重开关卡方法
        if (StageManager.Instance != null)
        {
            StageManager.Instance.RestartStage();
            UIManager.Instance.OpenPanel("StagePanel");
        }
        else
        {
            Debug.LogWarning("[ResultPanel] StageManager.Instance 为 null，无法重开关卡。");
        }
    }

    /// <summary>
    /// 更新进度文本
    /// </summary>
    private void UpdateProgressText()
    {
        if (_progressText == null) return;

        if (StageManager.Instance != null)
        {
            int totalFrogs = StageManager.Instance.GetTotalFrogCount();
            int remainingFrogs = StageManager.Instance.GetRemainingFrogCount();

            if (totalFrogs > 0)
            {
                float progress = 1f - (float)remainingFrogs / totalFrogs;
                int progressPercent = Mathf.RoundToInt(progress * 100f);
                _progressText.text = $"您已经完成进度<color=#FFF100>{progressPercent}%</color>";
            }
            else
            {
                _progressText.text = "您已经完成进度<color=#FFF100>0%</color>";
            }
        }
        else
        {
            _progressText.text = "您已经完成进度<color=#FFF100>0%</color>";
        }
    }

    /// <summary>
    /// 开始进度条动画
    /// </summary>
    private void StartProgressAnimation()
    {
        if (_progressSlider == null) return;

        // 停止之前的动画
        if (_progressAnimationCoroutine != null)
        {
            StopCoroutine(_progressAnimationCoroutine);
        }

        // 获取目标进度值
        float targetProgress = 0f;
        if (StageManager.Instance != null)
        {
            int totalFrogs = StageManager.Instance.GetTotalFrogCount();
            int remainingFrogs = StageManager.Instance.GetRemainingFrogCount();

            if (totalFrogs > 0)
            {
                targetProgress = 1f - (float)remainingFrogs / totalFrogs;
            }
        }

        // 激活青蛙图片
        if (_frogImage != null)
        {
            _frogImage.gameObject.SetActive(true);
        }

        // 重置进度条为0
        _progressSlider.value = 0f;

        // 开始动画
        _progressAnimationCoroutine = StartCoroutine(AnimateProgress(targetProgress));
    }

    /// <summary>
    /// 进度条动画协程
    /// </summary>
    private IEnumerator AnimateProgress(float targetProgress)
    {
        if (_progressSlider == null) yield break;

        float elapsedTime = 0f;
        float startProgress = 0f;

        while (elapsedTime < _progressAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / _progressAnimationDuration);
            
            // 使用平滑插值
            float currentProgress = Mathf.Lerp(startProgress, targetProgress, t);
            _progressSlider.value = currentProgress;

            // 更新青蛙图片位置
            UpdateFrogImagePosition(currentProgress);

            yield return null;
        }

        // 确保最终值准确
        _progressSlider.value = targetProgress;
        UpdateFrogImagePosition(targetProgress);

        _progressAnimationCoroutine = null;
    }

    /// <summary>
    /// 更新青蛙图片位置，使其跟随进度条
    /// </summary>
    private void UpdateFrogImagePosition(float progress)
    {
        if (_frogImage == null || _frogStartPos == null || _frogEndPos == null) return;

        // 获取起始和结束位置的 RectTransform
        RectTransform startRect = _frogStartPos.GetComponent<RectTransform>();
        RectTransform endRect = _frogEndPos.GetComponent<RectTransform>();
        RectTransform frogRect = _frogImage.GetComponent<RectTransform>();

        if (startRect == null || endRect == null || frogRect == null) return;

        // 获取起始和结束位置的世界位置（考虑锚定点不同）
        Vector3 startWorldPos = startRect.position;
        Vector3 endWorldPos = endRect.position;

        // 根据进度值在两个世界位置之间插值
        Vector3 targetWorldPos = Vector3.Lerp(startWorldPos, endWorldPos, progress);

        // 将目标世界位置转换到 frogImage 的父容器的本地坐标系
        // 如果 frogImage 和 startPos/endPos 在同一个父容器下，可以直接使用
        if (frogRect.parent != null)
        {
            // 将世界位置转换为父容器的本地位置
            RectTransform parentRect = frogRect.parent as RectTransform;
            if (parentRect != null)
            {
                Vector3 targetLocalPos = parentRect.InverseTransformPoint(targetWorldPos);
                frogRect.localPosition = targetLocalPos;
            }
            else
            {
                // 如果父容器不是 RectTransform，直接使用世界位置
                frogRect.position = targetWorldPos;
            }
        }
        else
        {
            // 如果没有父容器，直接使用世界位置
            frogRect.position = targetWorldPos;
        }
    }

    /// <summary>
    /// 清理场景中所有的青蛙对象
    /// </summary>
    private void ClearAllFrogs()
    {
        // 清理所有 NormalFrog（红绿青蛙）
        GreenRedFrog[] normalFrogs = FindObjectsOfType<GreenRedFrog>();
        if (normalFrogs != null && normalFrogs.Length > 0)
        {
            Debug.Log($"[ResultPanel] 清理 {normalFrogs.Length} 只 NormalFrog");
            foreach (var frog in normalFrogs)
            {
                if (frog != null && frog.gameObject != null)
                {
                    Destroy(frog.gameObject);
                }
            }
        }

        // 清理所有 YellowBlackFrog（黄黑青蛙）
        YellowBlackFrog[] yellowBlackFrogs = FindObjectsOfType<YellowBlackFrog>();
        if (yellowBlackFrogs != null && yellowBlackFrogs.Length > 0)
        {
            Debug.Log($"[ResultPanel] 清理 {yellowBlackFrogs.Length} 只 YellowBlackFrog");
            foreach (var frog in yellowBlackFrogs)
            {
                if (frog != null && frog.gameObject != null)
                {
                    Destroy(frog.gameObject);
                }
            }
        }
    }
}