using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// DontDestroyOnLoad 组件。
/// 对于相同类型的对象，第一个被标记为 DontDestroyOnLoad 的会一直保留，
/// 后续出现的同类型对象会被删除。
/// 例如：如果 Menu 场景初始就有一个带此脚本的 Canvas，从其他场景切换回来时，
/// 会保留这个旧的 Canvas，删除新场景创建的 Canvas。
/// </summary>
public class DontDestroyOnLoad : MonoBehaviour
{
    /// <summary>
    /// 跟踪已经存在的同类型对象
    /// Key: 对象类型标识（如 "Canvas", "EventSystem"）
    /// Value: 第一个被标记为 DontDestroyOnLoad 的对象
    /// </summary>
    private static Dictionary<string, GameObject> _persistentObjects = new Dictionary<string, GameObject>();

    void Awake()
    {
        // 确定当前对象的类型标识
        string objectType = GetObjectType();
        
        // 如果已经存在同类型的对象，销毁当前对象
        if (_persistentObjects.ContainsKey(objectType))
        {
            GameObject existingObject = _persistentObjects[objectType];
            if (existingObject != null && existingObject != gameObject)
            {
                Debug.Log($"[DontDestroyOnLoad] 发现已存在的 {objectType} 对象：{existingObject.name}，销毁新对象：{gameObject.name}");
                Destroy(gameObject);
                return;
            }
        }
        
        // 如果不存在同类型对象，保留当前对象并记录
        DontDestroyOnLoad(gameObject);
        _persistentObjects[objectType] = gameObject;
        Debug.Log($"[DontDestroyOnLoad] 保留 {objectType} 对象：{gameObject.name}");
    }

    /// <summary>
    /// 获取对象的类型标识
    /// 根据对象上的关键组件来判断类型
    /// </summary>
    private string GetObjectType()
    {
        // 检查常见的组件类型
        if (GetComponent<Canvas>() != null)
        {
            return "Canvas";
        }
        
        if (GetComponent<EventSystem>() != null)
        {
            return "EventSystem";
        }
        
        // 如果没有匹配的组件，使用 GameObject 名称作为类型标识
        // 这样可以处理其他类型的对象
        return gameObject.name;
    }

    /// <summary>
    /// 清理已销毁对象的记录（可选，用于内存管理）
    /// </summary>
    private void OnDestroy()
    {
        // 如果当前对象被销毁，从记录中移除（如果它是被记录的对象）
        string objectType = GetObjectType();
        if (_persistentObjects.ContainsKey(objectType) && _persistentObjects[objectType] == gameObject)
        {
            _persistentObjects.Remove(objectType);
        }
    }
}
