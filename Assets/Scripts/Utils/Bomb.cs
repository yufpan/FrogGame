using UnityEngine;
using System.Collections;
using System;

public class Bomb : MonoBehaviour
{
    [Header("爆炸设置")]
    [SerializeField] private float explosionRadius = 5f; // 爆炸半径
    
    [Header("动画设置")]
    [SerializeField] private float scaleAnimationDuration = 1f; // 缩放动画时长（秒）
    
    private Vector3 originalScale; // 原始缩放值
    
    /// <summary>
    /// 炸弹爆炸事件，在缩放动画完成后触发
    /// 参数：爆炸位置、爆炸半径
    /// </summary>
    public event Action<Vector3, float> OnExplode;
    
    /// <summary>
    /// 获取爆炸半径
    /// </summary>
    public float ExplosionRadius => explosionRadius;
    
    void Start()
    {
        originalScale = transform.localScale;
        transform.localScale = originalScale * 0.1f; // 初始缩放为0.1
        StartCoroutine(ScaleAnimation());
    }
    
    /// <summary>
    /// 缩放动画协程：从0.1缩放到1
    /// </summary>
    private IEnumerator ScaleAnimation()
    {
        Vector3 startScale = originalScale * 0.1f;
        Vector3 targetScale = originalScale;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < scaleAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / scaleAnimationDuration);
            
            // 使用平滑插值
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); // 缓出效果
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            yield return null;
        }
        
        transform.localScale = targetScale; // 确保最终缩放值准确
        
        // 触发爆炸事件
        OnExplode?.Invoke(transform.position, explosionRadius);
        
        // 播放爆炸音效
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFX("Dead_Boom");
        }
        
        // 生成爆炸特效
        GameObject bombFXPrefab = Resources.Load<GameObject>("FX/BombFX");
        if (bombFXPrefab != null)
        {
            GameObject bombFXInstance = Instantiate(bombFXPrefab, transform.position, bombFXPrefab.transform.rotation);
            if (bombFXInstance != null)
            {
                // 确保特效激活
                if (!bombFXInstance.activeSelf)
                {
                    bombFXInstance.SetActive(true);
                }
                
                // 播放特效上的粒子系统
                ParticleSystem[] particleSystems = bombFXInstance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particleSystems)
                {
                    if (ps != null)
                    {
                        if (!ps.gameObject.activeSelf)
                        {
                            ps.gameObject.SetActive(true);
                        }
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Play();
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[Bomb] 未找到爆炸特效预制体 Resources/FX/BombFX");
        }
        
        // 销毁炸弹自身
        Destroy(gameObject);
    }
    
    /// <summary>
    /// 在Scene视图中绘制Gizmo，显示爆炸半径
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
    
    /// <summary>
    /// 当选中时绘制Gizmo，颜色更明显
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // 半透明红色
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}