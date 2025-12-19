using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class GetEnergyPanel : BasePanel
{
    public override string PanelName => "GetEnergyPanel";
    [SerializeField] private Button _closeButton;
    [SerializeField] private TextMeshProUGUI _remainText;
    [SerializeField] private Button _getEnergyButton;

    public override void Open()
    {
        // 绑定按钮事件
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseButtonClick);
        }

        if (_getEnergyButton != null)
        {
            _getEnergyButton.onClick.AddListener(OnGetEnergyButtonClick);
        }

        // 更新剩余次数显示
        UpdateRemainText();

        // 订阅每日获取次数变化事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDailyEnergyGetCountChanged += OnDailyEnergyGetCountChanged;
        }

        base.Open();
    }

    public override void Close()
    {
        // 解绑按钮事件
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(OnCloseButtonClick);
        }

        if (_getEnergyButton != null)
        {
            _getEnergyButton.onClick.RemoveListener(OnGetEnergyButtonClick);
        }

        // 取消订阅事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDailyEnergyGetCountChanged -= OnDailyEnergyGetCountChanged;
        }

        base.Close();
    }

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void OnCloseButtonClick()
    {
        PlayButtonSound(true); // 播放关闭按钮音效
        Close();
    }

    /// <summary>
    /// 获取能量按钮点击事件
    /// </summary>
    private void OnGetEnergyButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效

        // 检查广告是否可用
        if (ADManager.Instance == null || !ADManager.Instance.IsAdAvailable())
        {
            Debug.LogWarning("[GetEnergyPanel] 广告不可用，无法获取体力");
            if (UIManager.Instance != null)
            {
                ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
                if (toastPanel != null)
                {
                    toastPanel.ShowToast("广告暂时不可用，请稍后再试");
                }
            }
            return;
        }

        // 播放激励广告，观看完成后执行获取体力逻辑
        ADManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                // 广告观看完成，执行获取体力逻辑
                Debug.Log("[GetEnergyPanel] 广告观看完成，执行获取体力");
                ExecuteGetEnergy();
            },
            onFailed: (errorMsg) =>
            {
                Debug.LogWarning($"[GetEnergyPanel] 广告播放失败: {errorMsg}");
                if (UIManager.Instance != null)
                {
                    ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
                    if (toastPanel != null)
                    {
                        toastPanel.ShowToast($"广告播放失败：{errorMsg}");
                    }
                }
            }
        );
    }

    /// <summary>
    /// 执行获取体力逻辑
    /// </summary>
    private void ExecuteGetEnergy()
    {
        if (GameManager.Instance != null)
        {
            // 尝试获取能量
            bool success = GameManager.Instance.TryGetEnergyFromPanel();
            if (success)
            {
                // 更新剩余次数显示
                UpdateRemainText();
                Close();
            }
            else
            {
                Debug.LogWarning("[GetEnergyPanel] 获取能量失败，今日次数已用完");
                if (UIManager.Instance != null)
                {
                    ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
                    if (toastPanel != null)
                    {
                        toastPanel.ShowToast("今日获取次数已用完");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 更新剩余次数文本
    /// </summary>
    private void UpdateRemainText()
    {
        if (_remainText != null && GameManager.Instance != null)
        {
            int remainCount = GameManager.Instance.DailyEnergyGetRemainCount;
            _remainText.text = $"今日剩余{remainCount}次";
        }
    }

    /// <summary>
    /// 每日获取次数变化事件回调
    /// </summary>
    private void OnDailyEnergyGetCountChanged(int newCount)
    {
        UpdateRemainText();
    }
}