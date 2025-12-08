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
    private const string REWARDED_AD_ID = "90e90tvhwb3cfhd4ig";

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
        try
        {
            _isAdPlaying = true;
            
            // 使用TT.CreateRewardedVideoAd创建并显示激励视频广告
            // 该方法会自动创建并显示广告，如果广告未加载会自动加载
            TT.CreateRewardedVideoAd(
                REWARDED_AD_ID,
                OnAdClosed,
                OnAdError,
                false,  // multiton: 不开启再得广告模式
                null,   // multitonRewardMsg: 不需要
                0,      // multitonRewardTime: 不需要
                false   // progressTip: 不开启进度提醒
            );
            
            Debug.Log($"[ADManager] 开始播放激励广告，广告ID: {REWARDED_AD_ID}");
        }
        catch (Exception e)
        {
            _isAdPlaying = false;
            Debug.LogError($"[ADManager] 播放广告失败: {e.Message}");
            onFailed?.Invoke(e.Message);
        }
#else
        // 编辑器模式下模拟广告播放
        Debug.Log("[ADManager] 编辑器模式：模拟播放激励广告");
        _isAdPlaying = true;
        StartCoroutine(SimulateAdPlayback());
#endif
    }

    /// <summary>
    /// 广告关闭回调（由TTSDK调用）
    /// </summary>
    /// <param name="isComplete">是否播放完成（用户看完广告）</param>
    /// <param name="errCode">错误码（如果有关闭错误）</param>
    private void OnAdClosed(bool isComplete, int errCode)
    {
        _isAdPlaying = false;
        
        if (isComplete)
        {
            // 用户看完了广告，给予奖励
            Debug.Log("[ADManager] 激励广告播放完成，用户获得奖励");
            _onRewardedCallback?.Invoke();
        }
        else
        {
            // 用户没有看完广告就关闭了，或者有错误
            if (errCode != 0)
            {
                Debug.LogWarning($"[ADManager] 激励广告关闭，错误码: {errCode}");
                _onFailedCallback?.Invoke($"广告关闭错误，错误码: {errCode}");
            }
            else
            {
                Debug.Log("[ADManager] 激励广告未播放完成，用户关闭了广告");
                _onFailedCallback?.Invoke("用户未看完广告");
            }
        }
        
        // 清理回调
        _onRewardedCallback = null;
        _onFailedCallback = null;
    }

    /// <summary>
    /// 广告错误回调（由TTSDK调用）
    /// </summary>
    /// <param name="errCode">错误码</param>
    /// <param name="errMsg">错误信息</param>
    private void OnAdError(int errCode, string errMsg)
    {
        _isAdPlaying = false;
        Debug.LogError($"[ADManager] 激励广告错误: errCode={errCode}, errMsg={errMsg}");
        _onFailedCallback?.Invoke($"广告错误: {errMsg} (错误码: {errCode})");
        
        // 清理回调
        _onRewardedCallback = null;
        _onFailedCallback = null;
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
}
