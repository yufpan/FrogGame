using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingScene : MonoBehaviour
{
    public Image loadingBar;
    public TextMeshProUGUI loadingText;
    public float loadingTime = 2f;

    private void Start()
    {
        StartCoroutine(Loading());
    }

    private IEnumerator Loading()
    {
        float time = 0;
        while (time < loadingTime)
        {
            loadingBar.fillAmount = time / loadingTime;
            loadingText.text = $"{(int)(time / loadingTime * 100f)}%";
            time += Time.deltaTime;
            yield return null;
        }
        
        SceneManager.LoadScene("Menu");
    }
}