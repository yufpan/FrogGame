using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

public class SideBarPanel : BasePanel
{
    public override string PanelName => "SideBarPanel";
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _goToSideBarButton;
    [SerializeField] private Button _bonusButton;

    /// <summary>
    /// 是否从侧边栏启动
    /// </summary>
    private bool _isFromSidebar = false;

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // 订阅OnShow事件，监听是否从侧边栏进入
        TT.GetAppLifeCycle().OnShow += OnAppShow;
#endif
    }

    public override void Open()
    {
        base.Open();

        // 设置按钮监听
        SetupButtonListeners();

        // 根据是否从侧边栏启动来显示不同的按钮
        UpdateButtonVisibility();
    }

    public override void Close()
    {
        // 移除按钮监听
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
        }
        if (_goToSideBarButton != null)
        {
            _goToSideBarButton.onClick.RemoveAllListeners();
        }
        if (_bonusButton != null)
        {
            _bonusButton.onClick.RemoveAllListeners();
        }

        base.Close();
    }

    protected override void OnDestroy()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // 取消订阅OnShow事件
        TT.GetAppLifeCycle().OnShow -= OnAppShow;
#endif
        base.OnDestroy();
    }

    /// <summary>
    /// 设置按钮监听
    /// </summary>
    private void SetupButtonListeners()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(OnCloseButtonClick);
        }

        if (_goToSideBarButton != null)
        {
            _goToSideBarButton.onClick.RemoveAllListeners();
            _goToSideBarButton.onClick.AddListener(OnGoToSideBarButtonClick);
        }

        if (_bonusButton != null)
        {
            _bonusButton.onClick.RemoveAllListeners();
            _bonusButton.onClick.AddListener(OnBonusButtonClick);
        }
    }

    /// <summary>
    /// 根据是否从侧边栏启动来更新按钮显示状态
    /// </summary>
    private void UpdateButtonVisibility()
    {
        // 如果从侧边栏启动，显示奖励按钮；否则显示跳转按钮
        if (_goToSideBarButton != null)
        {
            _goToSideBarButton.gameObject.SetActive(!_isFromSidebar);
        }

        if (_bonusButton != null)
        {
            _bonusButton.gameObject.SetActive(_isFromSidebar);
        }

        Debug.Log($"[SideBarPanel] 是否从侧边栏启动：{_isFromSidebar}，显示跳转按钮：{!_isFromSidebar}，显示奖励按钮：{_isFromSidebar}");
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// 游戏切到前台事件回调
    /// </summary>
    private void OnAppShow(Dictionary<string, object> param)
    {
        // 检查是否从侧边栏进入
        if (param != null && 
            param.ContainsKey("launchFrom") && param["launchFrom"].ToString() == "homepage" &&
            param.ContainsKey("location") && param["location"].ToString() == "sidebar_card")
        {
            _isFromSidebar = true;
            Debug.Log("[SideBarPanel] 用户从侧边栏进入游戏");
        }
        else
        {
            _isFromSidebar = false;
        }
    }
#endif

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void OnCloseButtonClick()
    {
        PlayButtonSound(true); // 播放关闭按钮音效
        Close();
    }

    /// <summary>
    /// 跳转到侧边栏按钮点击事件
    /// </summary>
    private void OnGoToSideBarButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效

#if UNITY_WEBGL && !UNITY_EDITOR
        // 跳转到侧边栏
        var data = new JsonData
        {
            ["scene"] = "sidebar"
        };

        TT.NavigateToScene(data,
            () =>
            {
                Debug.Log("[SideBarPanel] 跳转到侧边栏成功");
            },
            () =>
            {
                Debug.Log("[SideBarPanel] 跳转到侧边栏完成");
            },
            (errCode, errMsg) =>
            {
                Debug.LogError($"[SideBarPanel] 跳转到侧边栏失败，错误码：{errCode}，错误信息：{errMsg}");
                
                // 显示错误提示
                if (UIManager.Instance != null)
                {
                    ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
                    if (toastPanel != null)
                    {
                        toastPanel.ShowToast($"跳转失败：{errMsg}");
                    }
                }
            });
#else
        // 编辑器模式下模拟
        Debug.Log("[SideBarPanel] 编辑器模式：模拟跳转到侧边栏");
        if (UIManager.Instance != null)
        {
            ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
            if (toastPanel != null)
            {
                toastPanel.ShowToast("编辑器模式：模拟跳转到侧边栏");
            }
        }
#endif
    }

    /// <summary>
    /// 奖励按钮点击事件（从侧边栏进入时显示）
    /// </summary>
    private void OnBonusButtonClick()
    {
        PlayButtonSound(false); // 播放普通按钮音效
        
        // 检查今日是否已领取
        if (IsRewardClaimedToday())
        {
            // 今日已领取，显示提示
            if (UIManager.Instance != null)
            {
                ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
                if (toastPanel != null)
                {
                    toastPanel.ShowToast("今日已领取");
                }
            }
            Debug.Log("[SideBarPanel] 今日已领取过侧边栏奖励");
            return;
        }

        // 今日未领取，发放奖励
        ClaimReward();
    }

    /// <summary>
    /// 检查今日是否已领取奖励
    /// </summary>
    private bool IsRewardClaimedToday()
    {
        if (SaveDataManager.Instance == null)
        {
            return false;
        }

        string savedDate = SaveDataManager.Instance.LoadSidebarRewardDate("");
        string currentDate = GetCurrentDateString();

        // 如果日期相同，说明今日已领取
        return savedDate == currentDate;
    }

    /// <summary>
    /// 领取奖励
    /// </summary>
    private void ClaimReward()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[SideBarPanel] GameManager.Instance 为 null，无法发放奖励");
            return;
        }

        // 增加100金币
        GameManager.Instance.AddCoins(100);

        // 保存领取日期
        if (SaveDataManager.Instance != null)
        {
            string currentDate = GetCurrentDateString();
            SaveDataManager.Instance.SaveSidebarRewardDate(currentDate);
        }

        // 显示成功提示
        if (UIManager.Instance != null)
        {
            ToastPanel toastPanel = UIManager.Instance.GetPanel<ToastPanel>("ToastPanel");
            if (toastPanel != null)
            {
                toastPanel.ShowToast("成功领取");
            }
        }

        Debug.Log("[SideBarPanel] 成功发放侧边栏奖励：100金币");
    }

    /// <summary>
    /// 获取当前日期字符串（格式：yyyy-MM-dd）
    /// </summary>
    private string GetCurrentDateString()
    {
        return System.DateTime.Now.ToString("yyyy-MM-dd");
    }
}

