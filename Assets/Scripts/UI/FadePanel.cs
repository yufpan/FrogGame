using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏黑色淡入淡出 Panel，继承自 BasePanel。
/// 只负责持有 Image 等组件，不包含具体的场景切换逻辑。
/// </summary>
public class FadePanel : BasePanel
{
    [SerializeField] private Image _fadeImage;

    /// <summary>
    /// 对外提供要被控制透明度的 Image
    /// </summary>
    public Image FadeImage => _fadeImage;
}


