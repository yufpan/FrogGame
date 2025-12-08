using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
#endif

/// <summary>
/// SDK工具类，用于包装各种SDK的API调用
/// </summary>
public static class Utils
{
    /// <summary>
    /// 短震动 - 强震动（heavy）
    /// 使用抖音SDK的长震动（400ms）来模拟强震动效果
    /// </summary>
    public static void VibrateShortHeavy()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TT.VibrateLong(new VibrateLongParam());
#elif UNITY_EDITOR
        // 编辑器模式下不执行震动
        Debug.Log("[Utils] 编辑器模式：模拟强震动（长震动）");
#endif
    }

    /// <summary>
    /// 短震动 - 中等震动（medium）
    /// 使用抖音SDK的短震动（Android 30ms，iOS 15ms）
    /// </summary>
    public static void VibrateShortMedium()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TT.VibrateShort(new VibrateShortParam());
#elif UNITY_EDITOR
        // 编辑器模式下不执行震动
        Debug.Log("[Utils] 编辑器模式：模拟中等震动（短震动）");
#endif
    }

    /// <summary>
    /// 短震动 - 轻震动（light）
    /// 使用抖音SDK的短震动（Android 30ms，iOS 15ms）
    /// </summary>
    public static void VibrateShortLight()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TT.VibrateShort(new VibrateShortParam());
#elif UNITY_EDITOR
        // 编辑器模式下不执行震动
        Debug.Log("[Utils] 编辑器模式：模拟轻震动（短震动）");
#endif
    }
}

