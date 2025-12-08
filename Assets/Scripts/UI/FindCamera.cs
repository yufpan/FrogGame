using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Canvas))]
public class FindCamera : MonoBehaviour
{
    public Camera mainCam;
    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        StartCoroutine(FindMainCameraCoroutine());
    }

    private void OnEnable()
    {
        // 订阅场景加载事件，场景切换后重新寻找主相机
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // 取消订阅，避免重复订阅或引用泄漏
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 新场景加载完成后，清空旧引用并重新寻找主相机
        mainCam = null;
        StartCoroutine(FindMainCameraCoroutine());
    }

    private IEnumerator FindMainCameraCoroutine()
    {
        while (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam != null)
            {
                break;
            }
            yield return null;
        }

        // 找到主相机后，自动配置 Canvas
        if (canvas != null && mainCam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCam;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 0;
        }

        Debug.Log("[FindCamera] Main Camera found: " + mainCam.name + " for Canvas: " + canvas.name);
    }
}