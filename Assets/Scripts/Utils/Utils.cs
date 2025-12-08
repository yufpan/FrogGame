using UnityEngine;
#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
using WeChatWASM;
#endif

/// <summary>
/// SDK工具类，用于包装各种SDK的API调用
/// </summary>
public static class Utils
{
    /// <summary>
    /// 短震动 - 强震动（heavy）
    /// 需要基础库：2.13.0
    /// </summary>
    public static void VibrateShortHeavy()
    {
#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
        WX.VibrateShort(new VibrateShortOption() { type = "heavy" });
#endif
    }

    /// <summary>
    /// 短震动 - 中等震动（medium）
    /// 需要基础库：2.13.0
    /// </summary>
    public static void VibrateShortMedium()
    {
#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
        WX.VibrateShort(new VibrateShortOption() { type = "medium" });
#endif
    }

    /// <summary>
    /// 短震动 - 轻震动（light）
    /// 需要基础库：2.13.0
    /// </summary>
    public static void VibrateShortLight()
    {
#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
        WX.VibrateShort(new VibrateShortOption() { type = "light" });
#endif
    }
}

