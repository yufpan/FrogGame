using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有 UI 面板的基类。
/// 具体 Panel 请继承此类，而不要直接操作 GameObject 的显隐。
/// </summary>
public abstract class BasePanel : MonoBehaviour
{
    /// <summary>
    /// 该面板在 UIManager 中注册使用的名字。
    /// 默认使用 GameObject.name，子类可以重写提供自定义名字。
    /// 设为 public 是为了让 UIManager 在初始扫描时也能读取到。
    /// </summary>
    public virtual string PanelName => gameObject.name;

    [Header("打开动效设置")]
    [Tooltip("是否在面板打开时播放从小到大的弹出动画")]
    [SerializeField] private bool _useOpenPopupAnimation = false;

    [Tooltip("需要播放弹出动画的根物体（通常是面板的根节点或内容根节点）")]
    [SerializeField] private RectTransform _popupRoot;

    [Tooltip("弹出动画时长（秒）")]
    [SerializeField] private float _popupDuration = 0.2f;

    [Tooltip("弹出动画初始缩放（例如 0.6 比较自然，0 为完全从无到有）")]
    [SerializeField] private float _popupStartScale = 0.6f;

    [Header("面板特效设置")]
    [Tooltip("面板打开时自动实例化、关闭时自动销毁的特效预制体列表（通常是带多个子粒子系统的根对象）")]
    [SerializeField] private List<GameObject> _openEffectPrefabs = new List<GameObject>();

    [Tooltip("特效实例生成的父物体（可选，不设置则挂在当前面板对象下）")]
    [SerializeField] private Transform _openEffectParent;

    /// <summary>
    /// 记录弹出根物体的原始缩放，便于还原
    /// </summary>
    private Vector3 _popupRootOriginalScale = Vector3.one;

    /// <summary>
    /// 记录当前已生成的特效实例，便于在 Close 时统一销毁
    /// </summary>
    private readonly List<GameObject> _openEffectInstances = new List<GameObject>();

    /// <summary>
    /// 在 Awake 时自动向 UIManager 注册
    /// </summary>
    protected virtual void Awake()
    {
        // 记录原始缩放
        if (_popupRoot != null)
        {
            _popupRootOriginalScale = _popupRoot.localScale;
        }

        if (UIManager.Instance != null && !string.IsNullOrEmpty(PanelName))
        {
            UIManager.Instance.RegisterPanel(PanelName, this);
        }
    }

    protected virtual void OnDestroy()
    {
        // 保险：面板被销毁时也清理掉已经实例化的特效
        DestroyOpenEffects();
    }

    /// <summary>
    /// 是否处于打开状态（即 GameObject 是否激活）
    /// </summary>
    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// 打开面板（显示）
    /// </summary>
    public virtual void Open()
    {
        if (IsOpen) return;

        gameObject.SetActive(true);

        // 播放从小到大的简单弹出动效
        if (_useOpenPopupAnimation && _popupRoot != null)
        {
            // 停掉可能还在运行的协程，避免叠加
            StopCoroutine("PopupOpenAnimation");
            StartCoroutine("PopupOpenAnimation");
        }

        OnOpen();

        // 打开面板时生成并播放特效
        SpawnOpenEffects();
    }

    /// <summary>
    /// 关闭面板（隐藏）
    /// </summary>
    public virtual void Close()
    {
        if (!IsOpen) return;

        OnClose();

        // 关闭面板时销毁特效实例
        DestroyOpenEffects();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 打开面板时的回调，子类可重写
    /// </summary>
    protected virtual void OnOpen() { }

    /// <summary>
    /// 关闭面板时的回调，子类可重写
    /// </summary>
    protected virtual void OnClose() { }

    /// <summary>
    /// 播放按钮点击音效
    /// </summary>
    /// <param name="isCloseButton">是否为关闭按钮</param>
    protected void PlayButtonSound(bool isCloseButton = false)
    {
        if (AudioManager.Instance == null) return;
        
        if (isCloseButton)
        {
            AudioManager.Instance.PlayFX("Click_Close");
        }
        else
        {
            AudioManager.Instance.PlayFX("Click_Button");
        }
    }

    /// <summary>
    /// 简单的从小到大弹出动画
    /// </summary>
    private IEnumerator PopupOpenAnimation()
    {
        // 保护：如果根物体被删了就直接退出
        if (_popupRoot == null)
        {
            yield break;
        }

        // 动画开始前先设置为起始缩放
        Vector3 targetScale = _popupRootOriginalScale;
        Vector3 startScale = targetScale * Mathf.Clamp(_popupStartScale, 0.01f, 1.0f);

        _popupRoot.localScale = startScale;

        float duration = Mathf.Max(0.01f, _popupDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime，忽略 Time.timeScale 影响
            float t = Mathf.Clamp01(time / duration);

            // 简单的“先快后慢”插值，模拟一点弹性感觉（不使用第三方插件）
            t = 1f - Mathf.Pow(1f - t, 3f);

            _popupRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        _popupRoot.localScale = targetScale;
    }

    /// <summary>
    /// 打开面板时生成并播放配置好的特效预制体
    /// </summary>
    private void SpawnOpenEffects()
    {
        if (_openEffectPrefabs == null || _openEffectPrefabs.Count == 0)
        {
            return;
        }


        foreach (var prefab in _openEffectPrefabs)
        {
            if (prefab == null) continue;

            GameObject instance = Instantiate(prefab);
            if (instance == null) continue;

            if (!instance.activeSelf)
            {
                instance.SetActive(true);
            }

            // 播放实例及其子物体上的所有粒子系统
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;

                if (!ps.gameObject.activeSelf)
                {
                    ps.gameObject.SetActive(true);
                }

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }

            _openEffectInstances.Add(instance);
        }
    }

    /// <summary>
    /// 关闭面板时销毁之前生成的特效实例
    /// </summary>
    private void DestroyOpenEffects()
    {
        if (_openEffectInstances == null || _openEffectInstances.Count == 0)
        {
            return;
        }

        foreach (var go in _openEffectInstances)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }

        _openEffectInstances.Clear();
    }
}

