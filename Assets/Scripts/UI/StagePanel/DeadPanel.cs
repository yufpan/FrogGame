using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeadPanel : BasePanel
{
    public override string PanelName => "DeadPanel";

    [SerializeField] private Button _backButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private TextMeshProUGUI _progressText;

    public override void Open()
    {
        // 绑定按钮事件
        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClick);
        }
        
        if (_retryButton != null)
        {
            _retryButton.onClick.AddListener(OnRetryButtonClick);
        }

        // 更新进度文本
        UpdateProgressText();
        
        base.Open();
    }

    public override void Close()
    {
        // 解绑按钮事件
        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClick);
        }
        
        if (_retryButton != null)
        {
            _retryButton.onClick.RemoveListener(OnRetryButtonClick);
        }
        
        base.Close();
    }

    /// <summary>
    /// 返回按钮点击事件
    /// </summary>
    private void OnBackButtonClick()
    {
        PlayButtonSound(true); // 播放关闭按钮音效
        // 打开 ResultPanel
            if (UIManager.Instance != null)
            {
            UIManager.Instance.OpenPanel("ResultPanel");
            }
        
        // 关闭 DeadPanel 和 StagePanel
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClosePanel("DeadPanel");
            UIManager.Instance.ClosePanel("StagePanel");
        }
    }

    /// <summary>
    /// 重试按钮点击事件（恢复血量）
    /// </summary>
    private void OnRetryButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        Debug.Log("[DeadPanel] 重试：恢复血量");
        
        // 恢复血量到初始值
        if (StageManager.Instance != null)
        {
            StageManager.Instance.RestoreHealth();
        }
        
        // 解冻之前被冻结的青蛙
        if (DragBoxManager.Instance != null)
        {
            DragBoxManager.Instance.UnfreezeSettlingFrogs();
        }
        
        // 确保游戏状态为Playing（重试时关卡会继续运行）
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            GameManager.Instance.RestartGame();
        }
        
        // 关闭 DeadPanel
        Close();
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

}