using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 封面青蛙脚本，会在idle和jump两个sprite之间循环切换。
/// 每隔一个可配置的时间区间[t1, t2]切换到jumpSprite，保持t3秒后切回idleSprite。
/// </summary>
public class MenuFrog : MonoBehaviour
{
    [Header("Sprite引用")]
    [SerializeField] private Sprite _idleSprite;
    [SerializeField] private Sprite _jumpSprite;

    [Header("切换时间配置（秒）")]
    [Tooltip("切换到jumpSprite的等待时间最小值")]
    [Min(0f)]
    [SerializeField] private float _waitTimeMin = 2.0f;

    [Tooltip("切换到jumpSprite的等待时间最大值")]
    [Min(0f)]
    [SerializeField] private float _waitTimeMax = 4.0f;

    [Tooltip("jumpSprite保持的时间")]
    [Min(0f)]
    [SerializeField] private float _jumpDuration = 0.5f;

    [Header("组件引用")]
    [Tooltip("Image组件；如果留空则会在Awake里自动获取。")]
    [SerializeField] private Image _image;

    private void Reset()
    {
        // 在拖到对象上时，自动尝试获取Image组件
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }
    }

    private void Awake()
    {
        // 自动获取Image组件
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }

        if (_image == null)
        {
            Debug.LogWarning("[MenuFrog] 未找到Image组件，sprite切换将不会生效。", this);
        }
    }

    private void OnEnable()
    {
        // 初始化sprite为idle状态
        if (_image != null && _idleSprite != null)
        {
            _image.sprite = _idleSprite;
        }

        // 启动切换循环
        StopAllCoroutines();
        StartCoroutine(SpriteSwitchLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>
    /// Sprite切换循环协程
    /// </summary>
    private IEnumerator SpriteSwitchLoop()
    {
        // 启动时先等待一小段随机时间，避免所有MenuFrog同步切换
        float startupDelay = Random.Range(0f, 1f);
        yield return new WaitForSeconds(startupDelay);

        while (true)
        {
            // 等待随机时间区间[t1, t2]
            float waitTime = GetRandomDuration(_waitTimeMin, _waitTimeMax);
            yield return new WaitForSeconds(waitTime);

            // 切换到jumpSprite
            if (_image != null && _jumpSprite != null)
            {
                _image.sprite = _jumpSprite;
            }

            // 保持jumpSprite t3秒
            yield return new WaitForSeconds(_jumpDuration);

            // 切回idleSprite
            if (_image != null && _idleSprite != null)
            {
                _image.sprite = _idleSprite;
            }
        }
    }

    /// <summary>
    /// 获取随机时间值，带防御性处理
    /// </summary>
    private float GetRandomDuration(float min, float max)
    {
        // 防御性处理，避免出现max < min的情况
        if (max < min)
        {
            float tmp = min;
            min = max;
            max = tmp;
        }

        // 当两者相等时也能正常返回一个值
        return Random.Range(min, max);
    }
}