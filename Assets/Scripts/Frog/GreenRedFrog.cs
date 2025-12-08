using UnityEngine;
using System.Collections;

/// <summary>
/// 会在绿色和红色之间不断切换颜色的青蛙（通过动画控制）。
/// 通过 Inspector 中的参数可以调整红色/绿色各自持续时间的随机范围。
/// </summary>
public class GreenRedFrog : FrogBase
{
    [Header("红色持续时间范围（秒）")]
    [Min(0f)]
    [SerializeField] private float redDurationMin = 0.3f;

    [Min(0f)]
    [SerializeField] private float redDurationMax = 0.8f;

    [Header("绿色持续时间范围（秒）")]
    [Min(0f)]
    [SerializeField] private float greenDurationMin = 0.3f;

    [Min(0f)]
    [SerializeField] private float greenDurationMax = 1.0f;

    [Header("死亡粒子效果")]
    [Tooltip("绿色青蛙死亡特效")]
    [SerializeField] private ParticleSystem deathParticle;
    
    [Tooltip("红色青蛙死亡特效")]
    [SerializeField] private ParticleSystem redFrogDeathParticle;
    // Animator 参数名称常量
    private const string PARAM_IS_RED = "IsRed";

    // 当前是否为红色
    private bool isRed;

    // 是否已经初始化过颜色（避免OnEnable覆盖初始化设置）
    private bool colorInitialized = false;

    // 记录青蛙类型，用于判断是否应该切换颜色
    private FrogColorType currentColorType = FrogColorType.Red;

    // 是否有待处理的颜色切换（当 Jump 动画播放时收到变色要求，延后执行）
    // 字段定义在 FrogBase 中，这里保留注释说明

    /// <summary>
    /// 当前是否处于红色状态（只读）。
    /// </summary>
    public bool IsRed => isRed;

    /// <summary>
    /// 当前是否处于绿色状态（只读）。
    /// </summary>
    public bool IsGreen => !isRed;

    /// <summary>
    /// 获取当前青蛙的颜色类型（只读）
    /// </summary>
    public FrogColorType ColorType => currentColorType;

    /// <summary>
    /// 青蛙类型枚举，用于初始化时设置颜色
    /// </summary>
    public enum FrogColorType
    {
        Green,  // 绿色：保持绿色，不切换为红色
        Red     // 红色：正常红绿切换
    }

    /// <summary>
    /// 初始化青蛙颜色类型。
    /// 在生成后立即调用此方法，设置青蛙的初始颜色行为。
    /// </summary>
    /// <param name="colorType">颜色类型：Green=绿色（保持绿色），Red=红色（正常切换）</param>
    public void InitializeColor(FrogColorType colorType)
    {
        colorInitialized = true;
        currentColorType = colorType;

        if (colorType == FrogColorType.Green)
        {
            // 绿色青蛙：始终保持绿色，不切换为红色
            isRed = false;
        }
        else if (colorType == FrogColorType.Red)
        {
            // 红色青蛙：保持默认的红绿切换行为
            // isRed 会在 OnEnable 时随机设置（如果还没初始化）
        }

        // 如果已经激活，立即应用动画状态并重启协程
        if (gameObject.activeInHierarchy)
        {
            ApplyAnimationState();
            StopAllCoroutines();
            StartCoroutine(ColorSwitchLoop());
            StartCoroutine(JumpLoop());
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // 如果还没有初始化过颜色，则使用默认的随机行为
        // 这样可以保持向后兼容（如果外部没有调用 InitializeColor）
        if (!colorInitialized)
        {
            // 开启时随机决定初始颜色：50% 概率为红色，50% 概率为绿色
            isRed = Random.value < 0.5f;
        }

        ApplyAnimationState();
        StopAllCoroutines();
        StartCoroutine(ColorSwitchLoop());
        StartCoroutine(JumpLoop());
    }

    /// <summary>
    /// 青蛙被销毁时调用，用于播放死亡粒子效果。
    /// 根据青蛙类型（红色/绿色）播放不同的特效。
    /// </summary>
    private void OnDestroy()
    {
        // 如果关卡已经结束（胜利/失败后的统一清理阶段），就不要播放死亡特效，避免满屏粒子。
        if (StageManager.Instance != null && StageManager.Instance.IsStageEnded())
        {
            return;
        }

        // 如果被黄色爆炸影响，不播放特效和声音，直接销毁
        if (isExplosionAffected)
        {
            return;
        }

        // 根据当前实际颜色状态播放不同的死亡特效和音效
        // 当前是红色状态时使用红色特效，当前是绿色状态时使用绿色特效
        if (isRed && redFrogDeathParticle != null)
        {
            // 当前是红色状态：使用红色青蛙死亡特效（保持特效原本的旋转）
            Instantiate(redFrogDeathParticle, transform.position, redFrogDeathParticle.transform.rotation);
        }
        else if (deathParticle != null)
        {
            // 当前是绿色状态：使用绿色青蛙死亡特效（默认，保持特效原本的旋转）
            Instantiate(deathParticle, transform.position, deathParticle.transform.rotation);
        }

        // 播放死亡音效
        if (AudioManager.Instance != null)
        {
            if (isRed)
            {
                // 红青蛙死亡时播放 Dead_Error
                AudioManager.Instance.PlayFX("Dead_Error");
            }
            else
            {
                // 绿青蛙死亡时播放 Dead_Normal
                AudioManager.Instance.PlayFX("Dead_Normal");
            }
        }
    }

    private IEnumerator ColorSwitchLoop()
    {
        // 绿色青蛙不需要切换颜色，直接退出协程
        if (currentColorType == FrogColorType.Green)
        {
            // 确保绿色青蛙始终保持 isRed = false
            isRed = false;
            ApplyAnimationState();
            yield break;
        }

        while (true)
        {
            // 如果处于冻结状态或隔离状态，等待直到解除
            while (isFrozen || isIsolated)
            {
                yield return null;
            }

            float duration;

            if (isRed)
            {
                // 当前是红色，下一个阶段切回绿色（不需要预警）
                duration = GetRandomDuration(redDurationMin, redDurationMax);
            }
            else
            {
                // 当前是绿色，下一个阶段切到红色（需要预警）
                duration = GetRandomDuration(greenDurationMin, greenDurationMax);
            }

            yield return new WaitForSeconds(duration);

            // 再次检查是否处于冻结状态或隔离状态
            if (isFrozen || isIsolated)
            {
                continue;
            }

            // 如果正在播放 Jump 动画，标记为待处理，延后变色
            if (isJumping)
            {
                pendingColorChange = true;
                continue;
            }

            // 检查是否需要预警（绿色->红色需要预警）
            if (WillChangeToWorseColor())
            {
                // 开始预警协程
                yield return StartCoroutine(StartWarning());
                
                // 预警期间如果被冻结或隔离，不执行颜色切换
                if (isFrozen || isIsolated)
                {
                    continue;
                }
            }

            // 切换颜色（只有红色类型的青蛙才会执行到这里）
            isRed = !isRed;
            ApplyAnimationState();
        }
    }

    /// <summary>
    /// 应用动画状态到 Animator
    /// </summary>
    protected override void ApplyAnimationState()
    {
        if (animator == null) return;
        animator.SetBool(PARAM_IS_RED, isRed);
    }

    /// <summary>
    /// 当有待处理的颜色切换时，执行一次红/绿互换。
    /// 由 FrogBase.JumpEnd 调用。
    /// </summary>
    protected override void PerformPendingColorChange()
    {
        // 检查是否需要预警（绿色->红色需要预警）
        if (WillChangeToWorseColor())
        {
            // 启动预警协程，预警结束后再切换颜色
            StartCoroutine(PerformPendingColorChangeWithWarning());
        }
        else
        {
            // 直接切换颜色（红色->绿色不需要预警）
            isRed = !isRed;
            ApplyAnimationState();
        }
    }

    /// <summary>
    /// 执行待处理的颜色切换（带预警）
    /// </summary>
    private IEnumerator PerformPendingColorChangeWithWarning()
    {
        // 开始预警
        yield return StartCoroutine(StartWarning());
        
        // 预警期间如果被冻结或隔离，不执行颜色切换
        if (!isFrozen && !isIsolated)
        {
            isRed = !isRed;
            ApplyAnimationState();
        }
    }

    /// <summary>
    /// 检查是否要变为"更坏颜色"（绿色->红色需要预警）
    /// </summary>
    protected override bool WillChangeToWorseColor()
    {
        // 当前是绿色，要变为红色时需要预警
        return !isRed;
    }

    /// <summary>
    /// 重启协程（解冻时调用）
    /// </summary>
    protected override void RestartCoroutines()
    {
        base.RestartCoroutines(); // 重启 JumpLoop
        StartCoroutine(ColorSwitchLoop()); // 重启颜色切换协程
    }
}


