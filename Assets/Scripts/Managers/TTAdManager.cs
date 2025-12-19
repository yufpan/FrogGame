using UnityEngine;
using TTSDK;

/// <summary>
/// 抖音广告管理器（单例）
/// 封装抖音小游戏的广告功能
/// </summary>
public class TTAdManager : MonoBehaviour
{
    private static TTAdManager _instance;

    public static TTAdManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameObject("TTAdManager").AddComponent<TTAdManager>();
                DontDestroyOnLoad(_instance.gameObject);
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(_instance.gameObject);
        
#if UNITY_EDITOR
        // 编辑器模式下开启Mock模块
        MockSetting.OpenAllMockModule();
#endif
    }

    /// <summary>
    /// 创建Banner广告
    /// </summary>
    /// <param name="adId">广告位id</param>
    /// <param name="bS">样式</param>
    /// <param name="intervalTime">间隔时间必须大于等于30</param>
    /// <returns>Banner广告实例</returns>
    public TTBannerAd CreateBanner(string adId, TTBannerStyle bS, int intervalTime)
    {
        return TT.CreateBannerAd(new CreateBannerAdParam() 
        { 
            BannerAdId = adId, 
            Style = bS, 
            AdIntervals = intervalTime 
        });
    }

    /// <summary>
    /// 播放激励视频广告
    /// </summary>
    /// <param name="adId">广告位id</param>
    /// <param name="closeCallBack">关闭回调，参数：是否播放完成，错误码</param>
    /// <param name="errorCallBack">错误回调，参数：错误码，错误信息</param>
    /// <returns>激励视频广告实例</returns>
    public TTRewardedVideoAd ShowVideoAd(string adId, System.Action<bool, int> closeCallBack, System.Action<int, string> errorCallBack)
    {
        return TT.CreateRewardedVideoAd(adId, closeCallBack, errorCallBack);
    }

    /// <summary>
    /// 播放激励视频广告（新版本）
    /// </summary>
    /// <param name="adId">广告位id</param>
    /// <returns>激励视频广告实例</returns>
    public TTRewardedVideoAd ShowVideoAdNew(string adId)
    {
        return TT.CreateRewardedVideoAd(new CreateRewardedVideoAdParam() { AdUnitId = adId });
    }

    /// <summary>
    /// 播放插屏广告
    /// </summary>
    /// <param name="adId">广告位id</param>
    /// <param name="errorCallBack">错误回调，参数：错误码，错误信息</param>
    /// <param name="closeCallBack">关闭回调</param>
    public void ShowInterstitialAd(string adId, System.Action<int, string> errorCallBack, System.Action closeCallBack)
    {
        TT.CreateInterstitialAd(adId, errorCallBack, closeCallBack);
    }
}

