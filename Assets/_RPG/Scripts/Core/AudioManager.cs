using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;

    [Header("Music per Scene")]
    [SerializeField] AudioClip villageMusic;
    [SerializeField] AudioClip dungeonMusic;
    [SerializeField] AudioClip combatMusic;

    [Header("Settings")]
    [Range(0,1)] public float MusicVolume = 0.5f;
    [Range(0,1)] public float SFXVolume = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        GameManager.OnGameStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameManager.OnGameStateChanged -= OnStateChanged;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioClip clip = scene.buildIndex == 0 ? villageMusic : dungeonMusic;
        PlayMusic(clip);
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Combat && combatMusic != null)
            PlayMusic(combatMusic);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip) return;
        musicSource.clip = clip;
        musicSource.volume = MusicVolume;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume * SFXVolume);
    }

    public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume * SFXVolume);
    }
}
