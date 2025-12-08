using UnityEngine;

/// <summary>
/// 粒子特效屏幕适配器
/// 根据当前屏幕分辨率调整粒子特效的缩放，使其适应不同屏幕尺寸
/// 设计分辨率为1284x2778
/// </summary>
public class PSAdaptor : MonoBehaviour
{
    // 设计分辨率
    private const float DESIGN_WIDTH = 1284f;
    private const float DESIGN_HEIGHT = 2778f;
    
    // 保存预制体的原始scale
    private Vector3 _originalScale;
    
    // 粒子系统组件
    private ParticleSystem _particleSystem;

    private void Awake()
    {
        // 在Awake中保存原始的scale值（此时还未被其他代码修改）
        _originalScale = transform.localScale;
        
        // 获取粒子系统组件
        _particleSystem = GetComponent<ParticleSystem>();
        
        Debug.Log($"[PSAdaptor] 保存原始scale: {_originalScale}");
    }

    private void Start()
    {
        AdaptToScreen();
    }

    /// <summary>
    /// 根据当前屏幕分辨率适配缩放
    /// </summary>
    private void AdaptToScreen()
    {
        // 获取当前屏幕分辨率
        float currentWidth = Screen.width;
        float currentHeight = Screen.height;

        // 分别计算宽度和高度的缩放比例
        float scaleX = currentWidth / DESIGN_WIDTH;
        float scaleY = currentHeight / DESIGN_HEIGHT;

        // 应用Transform缩放（基于原始scale，X和Y分别独立缩放，Z保持原值）
        Vector3 newScale = new Vector3(
            _originalScale.x * scaleX,
            _originalScale.y * scaleY,
            _originalScale.z
        );
        transform.localScale = newScale;
        
        // 如果粒子系统存在，同时调整ShapeModule的scale（如果启用）
        if (_particleSystem != null)
        {
            var shape = _particleSystem.shape;
            if (shape.enabled)
            {
                // 保存原始shape scale
                Vector3 originalShapeScale = shape.scale;
                
                // 应用新的scale（X和Y分别独立缩放）
                shape.scale = new Vector3(
                    originalShapeScale.x * scaleX,
                    originalShapeScale.y * scaleY,
                    originalShapeScale.z
                );
                
                Debug.Log($"[PSAdaptor] 调整ShapeModule scale: {shape.scale}");
            }
        }
        
        Debug.Log($"[PSAdaptor] 屏幕尺寸: {currentWidth}x{currentHeight}, 缩放比例: X={scaleX:F3}, Y={scaleY:F3}, Transform scale: {newScale}");
    }
}

