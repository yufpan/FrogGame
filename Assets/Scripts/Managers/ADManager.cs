using System;
using UnityEngine;
using TTSDK;

/// <summary>
/// 广告管理器（单例）
/// 封装抖音小游戏的激励广告功能
/// </summary>
public class ADManager : MonoBehaviour
{
    /// <summary>
    /// 全局访问的单例实例
    /// </summary>
    public static ADManager Instance { get; private set; }

    /// <summary>
    /// 激励广告ID
    /// </summary>
    private const string REWARDED_AD_ID = "3q5u0po6f9i1gj01m8";

    /// <summary>
    /// 广告实例
    /// </summary>
    private TTRewardedVideoAd _rewardVideoAd;

    /// <summary>
    /// 广告加载状态
    /// </summary>
    private bool _rewardVideoLoaded = false;

    /// <summary>
    /// 广告是否正在播放
    /// </summary>
    private bool _isAdPlaying = false;

    /// <summary>
    /// 广告奖励回调
    /// </summary>
    private Action _onRewardedCallback;

    /// <summary>
    /// 广告失败回调
    /// </summary>
    private Action<string> _onFailedCallback;

    /// <summary>
    /// 弱网重试配置
    /// </summary>
    private const int MAX_RETRY_COUNT = 3;
    private const float RETRY_DELAY = 2f;
    private int _retryCount = 0;

    private void Awake()
    {
        // 单例模式：让 ADManager 在场景切换间常驻
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeRewardVideoAd();
    }

    /// <summary>
    /// 初始化激励视频广告
    /// </summary>
    private void InitializeRewardVideoAd()
    {
        if (_rewardVideoAd != null) return;

#if !UNITY_EDITOR
        _rewardVideoAd = TTAdManager.Instance.ShowVideoAdNew(REWARDED_AD_ID);
        _rewardVideoLoaded = false;
        _retryCount = 0;

        _rewardVideoAd.OnClose += (isEnded, count) =>
        {
            _isAdPlaying = false;
            _rewardVideoLoaded = false;
            
            if (isEnded)
            {
                // 用户看完了广告，给予奖励
                Debug.Log("[ADManager] 激励广告播放完成，用户获得奖励");
                _onRewardedCallback?.Invoke();
            }
            else
            {
                // 用户没有看完广告就关闭了
                Debug.Log("[ADManager] 激励广告未播放完成，用户关闭了广告");
                _onFailedCallback?.Invoke("用户未看完广告");
            }
            
            // 清理回调
            _onRewardedCallback = null;
            _onFailedCallback = null;
        };

        _rewardVideoAd.OnError += (errorCode, errorMessage) =>
        {
            _isAdPlaying = false;
            _rewardVideoLoaded = false;
            Debug.LogError($"[ADManager] 激励广告错误: errCode={errorCode}, errMsg={errorMessage}");
            _onFailedCallback?.Invoke($"广告错误: {errorMessage} (错误码: {errorCode})");
            
            // 清理回调
            _onRewardedCallback = null;
            _onFailedCallback = null;
            
            // 弱网重试逻辑
            if (_retryCount < MAX_RETRY_COUNT)
            {
                _retryCount++;
                StartCoroutine(RetryLoadRewardVideo());
            }
        };

        _rewardVideoAd.OnLoad += () =>
        {
            _rewardVideoLoaded = true;
            _retryCount = 0; // 重置重试计数
            Debug.Log($"[ADManager] 激励广告加载完成，广告ID: {REWARDED_AD_ID}");
        };

        Debug.Log($"[ADManager] 初始化激励视频广告: {REWARDED_AD_ID}");
#endif
    }

    /// <summary>
    /// 重试加载激励视频广告
    /// </summary>
    private System.Collections.IEnumerator RetryLoadRewardVideo()
    {
        yield return new WaitForSeconds(RETRY_DELAY);
        if (_rewardVideoAd != null)
        {
            Debug.Log($"[ADManager] 重试加载激励广告 (第 {_retryCount} 次)");
            _rewardVideoAd.Load();
        }
    }

    /// <summary>
    /// 播放激励广告
    /// </summary>
    /// <param name="onRewarded">观看完成获得奖励的回调</param>
    /// <param name="onFailed">播放失败的回调（参数为错误信息）</param>
    public void ShowRewardedAd(Action onRewarded = null, Action<string> onFailed = null)
    {
        if (_isAdPlaying)
        {
            Debug.LogWarning("[ADManager] 广告正在播放中，请勿重复调用");
            onFailed?.Invoke("广告正在播放中");
            return;
        }

        _onRewardedCallback = onRewarded;
        _onFailedCallback = onFailed;

#if !UNITY_EDITOR
        if (_rewardVideoAd == null)
        {
            InitializeRewardVideoAd();
        }

        _isAdPlaying = true;

        if (!_rewardVideoLoaded)
        {
            Debug.Log("[ADManager] 广告未加载，开始加载广告");
            _rewardVideoAd.Load();
        }

        Debug.Log($"[ADManager] 开始播放激励广告，广告ID: {REWARDED_AD_ID}");
        _rewardVideoAd.Show();
#else
        // 编辑器模式下模拟广告播放
        Debug.Log("[ADManager] 编辑器模式：模拟播放激励广告");
        _isAdPlaying = true;
        StartCoroutine(SimulateAdPlayback());
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器模式下模拟广告播放
    /// </summary>
    private System.Collections.IEnumerator SimulateAdPlayback()
    {
        yield return new WaitForSeconds(1f);
        _isAdPlaying = false;
        _onRewardedCallback?.Invoke();
        Debug.Log("[ADManager] 编辑器模式：模拟广告播放完成，触发奖励回调");
        _onRewardedCallback = null;
        _onFailedCallback = null;
    }
#endif

    /// <summary>
    /// 检查广告是否可用
    /// </summary>
    /// <returns>广告是否可用</returns>
    public bool IsAdAvailable()
    {
        return !_isAdPlaying;
    }

    private void OnDestroy()
    {
        // 清理广告实例
        if (_rewardVideoAd != null)
        {
            _rewardVideoAd.Destroy();
            _rewardVideoAd = null;
        }
    }
}
