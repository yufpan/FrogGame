using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音频组件")]
    public AudioMixer audioMixer;
    public AudioSource bgmSource;  // BGM音频源
    public AudioSource fxSource;   // 音效音频源
    
    [Header("音频设置")]
    public float bgmVolume = 1f;
    public float fxVolume = 1f;
    
    // 保存原始音量，用于mute/unmute
    private float savedBGMVolume = 1f;
    private float savedFXVolume = 1f;
    
    // mute状态
    private bool isBGMMuted = false;
    private bool isFXMuted = false;
    
    [Header("音频资源")]
    [SerializeField] private List<AudioClip> bgmClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> fxClips = new List<AudioClip>();
    
    // 音频资源字典
    private Dictionary<string, AudioClip> bgmDict = new Dictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> fxDict = new Dictionary<string, AudioClip>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 初始化保存的音量值
        savedBGMVolume = bgmVolume;
        savedFXVolume = fxVolume;
        
        // 如果没有音频源，自动创建
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;  // BGM循环播放（无论是否自动创建都设置）
        
        if (fxSource == null)
        {
            fxSource = gameObject.AddComponent<AudioSource>();
        }
        fxSource.loop = false;  // 音效不循环（无论是否自动创建都设置）
        
        // 初始化音频资源字典
        InitializeAudioClips();
    }
    
    /// <summary>
    /// 初始化音频资源字典
    /// </summary>
    private void InitializeAudioClips()
    {
        bgmDict.Clear();
        fxDict.Clear();
        
        // 将Inspector中设置的BGM添加到字典
        foreach (var clip in bgmClips)
        {
            if (clip != null)
            {
                bgmDict[clip.name] = clip;
            }
        }

		// 从 Resources/Audio/BGM 动态加载所有BGM并合并到列表与字典
		var loadedBgmClips = Resources.LoadAll<AudioClip>("Audio/BGM");
		foreach (var clip in loadedBgmClips)
		{
			if (clip == null) continue;
			if (!bgmDict.ContainsKey(clip.name))
			{
				bgmClips.Add(clip);
				bgmDict[clip.name] = clip;
			}
		}
        
        // 将Inspector中设置的FX添加到字典
        foreach (var clip in fxClips)
        {
            if (clip != null)
            {
                fxDict[clip.name] = clip;
            }
        }

		// 从 Resources/Audio/SFX 动态加载所有音效并合并到列表与字典
		var loadedFxClips = Resources.LoadAll<AudioClip>("Audio/SFX");
		foreach (var clip in loadedFxClips)
		{
			if (clip == null) continue;
			if (!fxDict.ContainsKey(clip.name))
			{
				fxClips.Add(clip);
				fxDict[clip.name] = clip;
			}
		}
    }
    
    /// <summary>
    /// 播放BGM（通过名称）
    /// </summary>
    /// <param name="bgmName">BGM名称</param>
    public void PlayBGM(string bgmName)
    {
        if (bgmDict.TryGetValue(bgmName, out AudioClip clip))
        {
            PlayBGM(clip);
        }
        else
        {
            Debug.LogWarning($"未找到BGM: {bgmName}");
        }
    }
    
    /// <summary>
    /// 播放BGM
    /// </summary>
    /// <param name="clip">音频片段</param>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        
        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.loop = true;  // 确保BGM循环播放
        bgmSource.Play();
    }
    
    /// <summary>
    /// 播放音效（通过名称）
    /// </summary>
    /// <param name="fxName">音效名称</param>
    public void PlayFX(string fxName)
    {
        if (fxDict.TryGetValue(fxName, out AudioClip clip))
        {
            PlayFX(clip);
        }
        else
        {
            Debug.LogWarning($"未找到音效: {fxName}");
        }
    }
    
    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="clip">音频片段</param>
    public void PlayFX(AudioClip clip)
    {
        if (clip == null) return;
        
        fxSource.PlayOneShot(clip, fxVolume);
    }
    
    /// <summary>
    /// 停止BGM
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
    }
    
    /// <summary>
    /// 暂停BGM
    /// </summary>
    public void PauseBGM()
    {
        bgmSource.Pause();
    }
    
    /// <summary>
    /// 恢复BGM播放
    /// </summary>
    public void ResumeBGM()
    {
        bgmSource.UnPause();
    }
    
    /// <summary>
    /// 设置BGM音量
    /// </summary>
    /// <param name="volume">音量值 (0-1)</param>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume;
    }
    
    /// <summary>
    /// 设置音效音量
    /// </summary>
    /// <param name="volume">音量值 (0-1)</param>
    public void SetFXVolume(float volume)
    {
        fxVolume = Mathf.Clamp01(volume);
    }
    
    /// <summary>
    /// 添加BGM音频片段
    /// </summary>
    /// <param name="clip">音频片段</param>
    public void AddBGMClip(AudioClip clip)
    {
        if (clip != null && !bgmDict.ContainsKey(clip.name))
        {
            bgmClips.Add(clip);
            bgmDict[clip.name] = clip;
        }
    }
    
    /// <summary>
    /// 添加音效音频片段
    /// </summary>
    /// <param name="clip">音频片段</param>
    public void AddFXClip(AudioClip clip)
    {
        if (clip != null && !fxDict.ContainsKey(clip.name))
        {
            fxClips.Add(clip);
            fxDict[clip.name] = clip;
        }
    }
    
    /// <summary>
    /// 切换BGM的开关状态
    /// </summary>
    /// <returns>切换后的状态（true表示开启，false表示关闭）</returns>
    public bool ToggleBGM()
    {
        isBGMMuted = !isBGMMuted;
        
        if (isBGMMuted)
        {
            // 保存当前音量并设置为0
            savedBGMVolume = bgmVolume;
            SetBGMVolume(0f);
        }
        else
        {
            // 恢复保存的音量
            SetBGMVolume(savedBGMVolume);
        }
        
        return !isBGMMuted;
    }
    
    /// <summary>
    /// 切换音效的开关状态
    /// </summary>
    /// <returns>切换后的状态（true表示开启，false表示关闭）</returns>
    public bool ToggleFX()
    {
        isFXMuted = !isFXMuted;
        
        if (isFXMuted)
        {
            // 保存当前音量并设置为0
            savedFXVolume = fxVolume;
            SetFXVolume(0f);
        }
        else
        {
            // 恢复保存的音量
            SetFXVolume(savedFXVolume);
        }
        
        return !isFXMuted;
    }
    
    /// <summary>
    /// 获取BGM是否开启
    /// </summary>
    public bool IsBGMEnabled()
    {
        return !isBGMMuted;
    }
    
    /// <summary>
    /// 获取音效是否开启
    /// </summary>
    public bool IsFXEnabled()
    {
        return !isFXMuted;
    }

    /// <summary>
    /// 暂停BGM -> 播放FX -> 等待指定秒数 -> 恢复BGM
    /// </summary>
    /// <param name="fxName">要播放的音效名称</param>
    /// <param name="waitSeconds">等待的秒数（默认使用音效的时长）</param>
    public void PauseBGMPlayFXAndResume(string fxName, float waitSeconds = -1f)
    {
        if (fxDict.TryGetValue(fxName, out AudioClip clip))
        {
            PauseBGMPlayFXAndResume(clip, waitSeconds);
        }
        else
        {
            Debug.LogWarning($"未找到音效: {fxName}，无法执行暂停BGM播放FX操作");
        }
    }

    /// <summary>
    /// 暂停BGM -> 播放FX -> 等待指定秒数 -> 恢复BGM
    /// </summary>
    /// <param name="fxClip">要播放的音效片段</param>
    /// <param name="waitSeconds">等待的秒数（如果为-1，则使用音效的时长）</param>
    public void PauseBGMPlayFXAndResume(AudioClip fxClip, float waitSeconds = -1f)
    {
        if (fxClip == null)
        {
            Debug.LogWarning("音效片段为空，无法执行暂停BGM播放FX操作");
            return;
        }

        // 如果BGM没有在播放，直接播放FX即可
        if (!bgmSource.isPlaying)
        {
            PlayFX(fxClip);
            return;
        }

        // 启动协程执行暂停->播放->等待->恢复的流程
        StartCoroutine(PauseBGMPlayFXAndResumeCoroutine(fxClip, waitSeconds));
    }

    /// <summary>
    /// 协程：暂停BGM -> 播放FX -> 等待指定秒数 -> 恢复BGM
    /// </summary>
    private IEnumerator PauseBGMPlayFXAndResumeCoroutine(AudioClip fxClip, float waitSeconds)
    {
        // 1. 暂停BGM
        bool wasPlaying = bgmSource.isPlaying;
        if (wasPlaying)
        {
            PauseBGM();
        }

        // 2. 播放FX
        PlayFX(fxClip);

        // 3. 确定等待时间
        float waitTime = waitSeconds;
        if (waitTime < 0f)
        {
            // 如果未指定等待时间，使用音效的时长
            waitTime = fxClip.length;
        }

        // 4. 等待指定秒数
        yield return new WaitForSeconds(waitTime);

        // 5. 恢复BGM（如果之前正在播放）
        if (wasPlaying)
        {
            ResumeBGM();
        }
    }
}