using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 会在黄色和黑色之间不断切换颜色的青蛙（通过动画控制）。
/// 通过 Inspector 中的参数可以调整黄色/黑色各自持续时间的随机范围。
/// </summary>
public class YellowBlackFrog : FrogBase
{
    [Header("黑色持续时间范围（秒）")]
    [Min(0f)]
    [SerializeField] private float blackDurationMin = 0.3f;

    [Min(0f)]
    [SerializeField] private float blackDurationMax = 0.8f;

    [Header("黄色持续时间范围（秒）")]
    [Min(0f)]
    [SerializeField] private float yellowDurationMin = 0.3f;

    [Min(0f)]
    [SerializeField] private float yellowDurationMax = 1.0f;

    [Header("爆炸设置")]
    [Tooltip("黄色青蛙被触发时的爆炸半径（世界单位），会消除该半径内的所有青蛙")]
    [Min(0f)]
    [SerializeField] private float explosionRadius = 2.0f;

    [Header("死亡粒子效果")]
    [Tooltip("黄色青蛙死亡特效")]
    [SerializeField] private ParticleSystem yellowFrogDeathParticle;
    
    [Tooltip("黑色青蛙死亡特效")]
    [SerializeField] private ParticleSystem blackFrogDeathParticle;

    // Animator 参数名称常量
    private const string PARAM_IS_BLACK = "IsBlack";

    // 当前是否为黑色
    private bool isBlack;

    // 是否已经初始化过颜色（避免OnEnable覆盖初始化设置）
    private bool colorInitialized = false;

    // 记录青蛙类型，用于判断是否应该切换颜色
    private FrogColorType currentColorType = FrogColorType.Black;

    // 是否有待处理的颜色切换（当 Jump 动画播放时收到变色要求，延后执行）
    // 字段定义在 FrogBase 中，这里保留注释说明

    // 是否已经触发过爆炸（用于防止递归/重复触发）
    private bool hasExploded = false;

    /// <summary>
    /// 当前是否处于黑色状态（只读）。
    /// </summary>
    public bool IsBlack => isBlack;

    /// <summary>
    /// 当前是否处于黄色状态（只读）。
    /// </summary>
    public bool IsYellow => !isBlack;

    /// <summary>
    /// 获取当前青蛙的颜色类型（只读）
    /// </summary>
    public FrogColorType ColorType => currentColorType;

    /// <summary>
    /// 青蛙类型枚举，用于初始化时设置颜色（两种类型都会黄黑来回切，只是初始颜色不同）
    /// </summary>
    public enum FrogColorType
    {
        Yellow,  // 初始为黄色，之后在黄色/黑色之间来回切换
        Black    // 初始为黑色，之后在黄色/黑色之间来回切换
    }

    /// <summary>
    /// 对外只读访问爆炸半径
    /// </summary>
    public float ExplosionRadius => explosionRadius;

    /// <summary>
    /// 初始化青蛙颜色类型。
    /// 在生成后立即调用此方法，设置青蛙的初始颜色行为。
    /// </summary>
    /// <param name="colorType">颜色类型：Yellow=初始黄色，Black=初始黑色（两者都会变色）</param>
    public void InitializeColor(FrogColorType colorType)
    {
        colorInitialized = true;
        currentColorType = colorType;

        if (colorType == FrogColorType.Yellow)
        {
            // 黄色青蛙：初始为黄色
            isBlack = false;
        }
        else if (colorType == FrogColorType.Black)
        {
            // 黑色青蛙：初始为黑色
            isBlack = true;
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
            // 开启时随机决定初始颜色：50% 概率为黑色，50% 概率为黄色
            isBlack = Random.value < 0.5f;
        }

        ApplyAnimationState();
        StopAllCoroutines();
        StartCoroutine(ColorSwitchLoop());
        StartCoroutine(JumpLoop());
    }

    /// <summary>
    /// 青蛙被销毁时调用，用于播放死亡粒子效果。
    /// 根据当前实际颜色状态（黄色/黑色）播放不同的特效。
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

        // 根据当前实际颜色状态播放不同的死亡特效
        // 当前是黑色状态时使用黑色特效，当前是黄色状态时使用黄色特效
        if (isBlack && blackFrogDeathParticle != null)
        {
            // 当前是黑色状态：使用黑色青蛙死亡特效（保持特效原本的旋转）
            Instantiate(blackFrogDeathParticle, transform.position, blackFrogDeathParticle.transform.rotation);
        }
        else if (!isBlack && yellowFrogDeathParticle != null)
        {
            // 当前是黄色状态：使用黄色青蛙死亡特效（保持特效原本的旋转）
            Instantiate(yellowFrogDeathParticle, transform.position, yellowFrogDeathParticle.transform.rotation);
        }

        // 播放死亡音效
        if (AudioManager.Instance != null)
        {
            if (isBlack)
            {
                // 黑青蛙死亡时播放 Dead_Normal
                AudioManager.Instance.PlayFX("Dead_Normal");
            }
            else
            {
                // 黄青蛙死亡时播放 Dead_Boom
                AudioManager.Instance.PlayFX("Dead_Boom");
            }
        }
    }

    private IEnumerator ColorSwitchLoop()
    {
        while (true)
        {
            // 如果处于冻结状态，等待直到解除冻结
            while (isFrozen)
            {
                yield return null;
            }

            float duration;

            if (isBlack)
            {
                // 当前是黑色，下一个阶段切回黄色
                duration = GetRandomDuration(blackDurationMin, blackDurationMax);
            }
            else
            {
                // 当前是黄色，下一个阶段切到黑色
                duration = GetRandomDuration(yellowDurationMin, yellowDurationMax);
            }

            yield return new WaitForSeconds(duration);

            // 再次检查是否处于冻结状态
            if (isFrozen)
            {
                continue;
            }

            // 如果正在播放 Jump 动画，标记为待处理，延后变色
            if (isJumping)
            {
                pendingColorChange = true;
                continue;
            }

            // 切换颜色（只有黑色类型的青蛙才会执行到这里）
            isBlack = !isBlack;
            ApplyAnimationState();
        }
    }

    /// <summary>
    /// 应用动画状态到 Animator
    /// </summary>
    protected override void ApplyAnimationState()
    {
        if (animator == null) return;
        animator.SetBool(PARAM_IS_BLACK, isBlack);
    }

    /// <summary>
    /// 触发黄色青蛙的爆炸效果：查找一定半径内的所有青蛙（包括自身）。
    /// 半径由 explosionRadius 配置。
    /// </summary>
    /// <param name="immediateDestroy">是否立即销毁被影响的青蛙。如果为 false，只返回列表，不销毁。</param>
    /// <returns>被爆炸影响的青蛙列表（包括自身）</returns>
    public List<GameObject> TriggerExplosion(bool immediateDestroy = true)
    {
        List<GameObject> affectedFrogs = new List<GameObject>();

        // 防止同一只黄色青蛙多次触发爆炸（包括递归链）
        if (hasExploded)
        {
            return affectedFrogs;
        }
        hasExploded = true;

        float radius = Mathf.Max(0f, explosionRadius);
        if (radius <= 0f)
        {
            // 半径为 0，相当于只影响自己
            affectedFrogs.Add(gameObject);
            if (immediateDestroy)
            {
                Destroy(gameObject);
            }
            return affectedFrogs;
        }

        // 使用 2D 物理在周围查找所有碰撞体
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        if (hits == null || hits.Length == 0)
        {
            affectedFrogs.Add(gameObject);
            if (immediateDestroy)
            {
                Destroy(gameObject);
            }
            return affectedFrogs;
        }

        HashSet<GameObject> affectedSet = new HashSet<GameObject>();
        affectedSet.Add(gameObject); // 自身总是被影响

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            // 优先找到挂在对象或其父物体上的青蛙脚本
            GreenRedFrog normal = hit.GetComponentInParent<GreenRedFrog>();
            YellowBlackFrog yb = hit.GetComponentInParent<YellowBlackFrog>();

            GameObject target = null;
            if (normal != null)
            {
                // 普通绿红青蛙：被炸到
                target = normal.gameObject;
            }
            else if (yb != null)
            {
                // 如果波及到的是"其他黄色青蛙"，则继续触发它的爆炸（链式效果）
                if (yb != this && yb.IsYellow && immediateDestroy)
                {
                    // 递归调用，但只收集列表，不重复销毁
                    var chainAffected = yb.TriggerExplosion(immediateDestroy);
                    foreach (var chainFrog in chainAffected)
                    {
                        if (chainFrog != null && affectedSet.Add(chainFrog))
                        {
                            affectedFrogs.Add(chainFrog);
                        }
                    }
                    continue;
                }

                // 黑色或自身（this）的 YellowBlackFrog
                target = yb.gameObject;
            }

            if (target == null) continue;

            // 跳过被隔离的青蛙（黄色爆炸无法影响被隔离的青蛙）
            FrogBase targetFrogBase = target.GetComponent<FrogBase>();
            if (targetFrogBase != null && targetFrogBase.IsIsolated)
            {
                continue;
            }

            if (affectedSet.Add(target))
            {
                affectedFrogs.Add(target);
            }

            if (immediateDestroy)
            {
                Destroy(target);
            }
        }

        Debug.Log($"[YellowBlackFrog] 触发爆炸，半径={radius}，影响青蛙数量={affectedFrogs.Count}");
        return affectedFrogs;
    }

    /// <summary>
    /// 当有待处理的颜色切换时，执行一次黄/黑互换。
    /// 由 FrogBase.JumpEnd 调用。
    /// </summary>
    protected override void PerformPendingColorChange()
    {
        isBlack = !isBlack;
        ApplyAnimationState();
    }

    /// <summary>
    /// 重启协程（解冻时调用）
    /// </summary>
    protected override void RestartCoroutines()
    {
        base.RestartCoroutines(); // 重启 JumpLoop
        StartCoroutine(ColorSwitchLoop()); // 重启颜色切换协程
    }

    /// <summary>
    /// 在 Unity 编辑器中绘制 Gizmo，显示黄色青蛙的爆炸范围
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 只在选中时显示，且只显示黄色青蛙的爆炸范围
        if (!isBlack && explosionRadius > 0f)
        {
            // 设置 Gizmo 颜色为黄色（半透明）
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            // 绘制实心球体表示爆炸范围
            Gizmos.DrawSphere(transform.position, explosionRadius);
            
            // 设置 Gizmo 颜色为黄色（不透明）用于边框
            Gizmos.color = new Color(1f, 1f, 0f, 1f);
            // 绘制线框球体表示爆炸范围边界
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}

