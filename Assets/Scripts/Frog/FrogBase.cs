using UnityEngine;
using System.Collections;

/// <summary>
/// 青蛙基类：封装通用的 Animator / 冻结 / Jump 动画循环逻辑。
/// 不同颜色规则、死亡特效等由子类实现。
/// </summary>
public abstract class FrogBase : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("Animator 组件；如果留空则会在 Awake 里自动获取。")]
    [SerializeField] protected Animator animator;

    [Header("Jump 动画播放间隔（秒）")]
    [Min(0f)]
    [SerializeField] protected float jumpIntervalMin = 2.0f;

    [Min(0f)]
    [SerializeField] protected float jumpIntervalMax = 3.0f;

    [Header("头顶图标")]
    [SerializeField] protected GameObject _topIcon;
    [SerializeField] protected Sprite _selectingIcon;
    [SerializeField] protected Sprite _confirmIcon;

    [Header("预警设置")]
    [Tooltip("预警持续时间（秒）")]
    [Min(0f)]
    [SerializeField] protected float warningDuration = 2.0f;

    [Tooltip("每秒闪烁次数（例如：3表示每秒闪烁3次）")]
    [Min(0.1f)]
    [SerializeField] protected float warningFlashFrequency = 3.0f;

    [Tooltip("预警闪烁材质（用于高亮显示）")]
    [SerializeField] protected Material warningMaterial;

    protected SpriteRenderer _topIconRenderer;
    protected SpriteRenderer spriteRenderer;
    protected Material originalMaterial;

    // Animator 参数名称常量（所有青蛙共用）
    protected const string PARAM_PLAY_JUMP = "PlayJump";
    protected const string PARAM_IS_JUMPING = "IsJumping";

    // 是否正在播放 Jump 动画
    protected bool isJumping = false;

    // 是否有待处理的颜色切换（当 Jump 动画播放时收到变色要求，延后执行）
    protected bool pendingColorChange = false;

    // 是否处于冻结状态（冻结时暂停颜色切换和动画）
    protected bool isFrozen = false;

    // 是否被黄色爆炸影响（被爆炸影响的青蛙销毁时不播放特效和声音）
    protected bool isExplosionAffected = false;

    // 是否处于隔离状态（隔离时无法被选中，透明度变为50%）
    protected bool isIsolated = false;

    // 保存原始透明度，用于隔离解除后恢复
    private float originalAlpha = 1f;

    // 是否正在预警（预警期间保持安全颜色，预警结束后才变色）
    protected bool isWarning = false;

    /// <summary>
    /// 子类必须实现：把当前状态应用到 Animator（例如设置颜色相关的参数）。
    /// </summary>
    protected abstract void ApplyAnimationState();

    /// <summary>
    /// 子类必须实现：当有延迟处理的颜色切换时，真正执行一次颜色切换并更新动画。
    /// 由基类的 JumpEnd 调用。
    /// </summary>
    protected abstract void PerformPendingColorChange();

    /// <summary>
    /// 子类必须实现：检查是否要变为"更坏颜色"（需要预警）。
    /// 绿红青蛙：绿色->红色需要预警
    /// 黄黑青蛙：黄色->黑色需要预警
    /// </summary>
    /// <returns>如果要变为更坏颜色返回true，否则返回false</returns>
    protected abstract bool WillChangeToWorseColor();

    /// <summary>
    /// Reset 时自动尝试获取 Animator。
    /// </summary>
    protected virtual void Reset()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        CacheTopIconRenderer();
    }

    protected virtual void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 未找到 Animator，动画控制将不会生效。", this);
        }

        // 获取SpriteRenderer组件并保存原始材质
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalMaterial = spriteRenderer.material;
        }

        CacheTopIconRenderer();
        // 默认隐藏头顶图标
        SetTopIconActive(false);
    }

    protected virtual void OnEnable()
    {
        // 基类只重置通用状态；颜色初始化由子类负责
        isJumping = false;
        pendingColorChange = false;
        isWarning = false;

        // 恢复原始材质（如果之前使用了预警材质）
        if (spriteRenderer != null && originalMaterial != null)
        {
            spriteRenderer.material = originalMaterial;
        }

        // 确保进入可交互时不残留图标状态
        SetTopIconActive(false);
    }

    protected virtual void OnDisable()
    {
        StopAllCoroutines();
        // 禁用时也顺便隐藏图标
        SetTopIconActive(false);
    }

    /// <summary>
    /// Jump 动画播放循环协程（所有青蛙通用）。
    /// 子类在 OnEnable 中调用 StartCoroutine(JumpLoop()) 即可。
    /// </summary>
    protected IEnumerator JumpLoop()
    {
        // 启动时先等待一小段随机时间，避免所有青蛙同步跳跃
        float startupDelay = Random.Range(0f, 1f);
        yield return new WaitForSeconds(startupDelay);

        while (true)
        {
            // 如果处于冻结状态，等待直到解除冻结
            while (isFrozen)
            {
                yield return null;
            }

            // 等待 Jump 间隔时间
            float jumpInterval = GetRandomDuration(jumpIntervalMin, jumpIntervalMax);
            yield return new WaitForSeconds(jumpInterval);

            // 再次检查是否处于冻结状态
            if (isFrozen)
            {
                continue;
            }

            // 如果正在播放 Jump 动画，跳过本次
            if (isJumping)
            {
                continue;
            }

            // 触发 Jump 动画
            if (animator != null)
            {
                animator.SetTrigger(PARAM_PLAY_JUMP);
            }
        }
    }

    /// <summary>
    /// 设置冻结状态（暂停颜色切换和动画）
    /// </summary>
    /// <param name="frozen">是否冻结</param>
    public virtual void SetFrozen(bool frozen)
    {
        isFrozen = frozen;

        if (frozen)
        {
            // 冻结时：停止所有协程，禁用 Animator
            StopAllCoroutines();
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
        else
        {
            // 解冻时：重新启用 Animator，并重启协程
            if (animator != null)
            {
                animator.enabled = true;
            }
            // 重启协程（由子类实现具体逻辑）
            RestartCoroutines();
        }
    }

    /// <summary>
    /// 重启协程（解冻时调用）
    /// 子类应该重写此方法来重启自己的协程（如 ColorSwitchLoop、JumpLoop 等）
    /// </summary>
    protected virtual void RestartCoroutines()
    {
        // 基类默认只重启 JumpLoop
        StartCoroutine(JumpLoop());
    }

    /// <summary>
    /// 动画事件调用：Jump 动画开始
    /// </summary>
    public virtual void JumpStart()
    {
        isJumping = true;
        if (animator != null)
        {
            animator.SetBool(PARAM_IS_JUMPING, true);
        }
    }

    /// <summary>
    /// 动画事件调用：Jump 动画结束
    /// </summary>
    public virtual void JumpEnd()
    {
        isJumping = false;
        if (animator != null)
        {
            animator.SetBool(PARAM_IS_JUMPING, false);
        }

        // 如果有待处理的颜色切换，现在执行（只有在未冻结且未隔离时）
        if (pendingColorChange && !isFrozen && !isIsolated)
        {
            pendingColorChange = false;
            PerformPendingColorChange();
        }
    }

    /// <summary>
    /// 返回一个 [min, max] 范围内的随机时长，带防御性检查。
    /// </summary>
    protected float GetRandomDuration(float min, float max)
    {
        // 防御性处理，避免出现 max < min 的情况
        if (max < min)
        {
            float tmp = min;
            min = max;
            max = tmp;
        }

        // 当两者相等时也能正常返回一个值
        return Random.Range(min, max);
    }

    /// <summary>
    /// 拖拽选中阶段：显示“选择中”图标。
    /// </summary>
    public virtual void SetSelectingIcon(bool active)
    {
        if (!EnsureTopIconReady()) return;

        if (active)
        {
            if (_selectingIcon != null)
            {
                _topIconRenderer.sprite = _selectingIcon;
            }
            SetTopIconActive(true);
        }
        else
        {
            // 只有在不是确认阶段时才直接关掉；确认阶段由 SetConfirmIcon 控制
            if (_topIconRenderer != null && _topIconRenderer.sprite == _selectingIcon)
            {
                SetTopIconActive(false);
            }
        }
    }

    /// <summary>
    /// 结算阶段：显示“确认”图标。
    /// </summary>
    public virtual void SetConfirmIcon(bool active)
    {
        if (!EnsureTopIconReady()) return;

        if (active)
        {
            if (_confirmIcon != null)
            {
                _topIconRenderer.sprite = _confirmIcon;
            }
            SetTopIconActive(true);
        }
        else
        {
            if (_topIconRenderer != null && _topIconRenderer.sprite == _confirmIcon)
            {
                SetTopIconActive(false);
            }
        }
    }

    /// <summary>
    /// 清理所有头顶图标显示状态。
    /// </summary>
    public virtual void ClearTopIcon()
    {
        SetTopIconActive(false);
    }

    /// <summary>
    /// 设置是否被黄色爆炸影响（被爆炸影响的青蛙销毁时不播放特效和声音）
    /// </summary>
    /// <param name="affected">是否被爆炸影响</param>
    public virtual void SetExplosionAffected(bool affected)
    {
        isExplosionAffected = affected;
    }

    /// <summary>
    /// 设置隔离状态（隔离时无法被选中，透明度变为50%）
    /// </summary>
    /// <param name="isolated">是否隔离</param>
    public virtual void SetIsolated(bool isolated)
    {
        isIsolated = isolated;
        UpdateIsolationVisual();
    }

    /// <summary>
    /// 获取是否处于隔离状态（只读）
    /// </summary>
    public bool IsIsolated => isIsolated;

    /// <summary>
    /// 更新隔离视觉效果（设置透明度）
    /// </summary>
    private void UpdateIsolationVisual()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            
            if (isIsolated)
            {
                // 保存原始透明度
                originalAlpha = color.a;
                // 设置为50%透明度
                color.a = 0.3f;
            }
            else
            {
                // 恢复原始透明度
                color.a = originalAlpha;
            }
            
            spriteRenderer.color = color;
        }
    }

    protected void CacheTopIconRenderer()
    {
        if (_topIcon != null && _topIconRenderer == null)
        {
            _topIconRenderer = _topIcon.GetComponent<SpriteRenderer>();
        }
    }

    protected bool EnsureTopIconReady()
    {
        if (_topIcon == null)
        {
            return false;
        }

        if (_topIconRenderer == null)
        {
            _topIconRenderer = _topIcon.GetComponent<SpriteRenderer>();
        }

        return _topIconRenderer != null;
    }

    protected void SetTopIconActive(bool active)
    {
        if (_topIcon != null)
        {
            _topIcon.SetActive(active);
        }
    }

    /// <summary>
    /// 开始预警：闪烁高亮本体
    /// </summary>
    protected IEnumerator StartWarning()
    {
        if (warningMaterial == null || spriteRenderer == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 预警材质或SpriteRenderer未设置，无法显示预警效果。", this);
            yield break;
        }

        isWarning = true;

        // 切换到预警材质并创建实例（避免影响共享材质）
        Material warningMatInstance = new Material(warningMaterial);
        spriteRenderer.material = warningMatInstance;
        
        // 设置闪烁频率（Shader中的_FlashSpeed参数）
        // 每秒闪烁N次，需要每秒完成N个完整周期（sin的周期是2π）
        // 所以FlashSpeed = 2π * 每秒闪烁次数
        float flashSpeed = warningFlashFrequency * 2f * Mathf.PI;
        warningMatInstance.SetFloat("_FlashSpeed", flashSpeed);

        // 等待预警持续时间
        yield return new WaitForSeconds(warningDuration);

        // 恢复原始材质
        if (spriteRenderer != null && originalMaterial != null)
        {
            spriteRenderer.material = originalMaterial;
        }

        // 清理临时材质实例
        if (warningMatInstance != null)
        {
            Destroy(warningMatInstance);
        }

        isWarning = false;
    }

    /// <summary>
    /// 获取是否正在预警（只读）
    /// </summary>
    public bool IsWarning => isWarning;
}


