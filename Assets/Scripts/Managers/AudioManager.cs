using System.Collections;
using UnityEngine;

/// <summary>
/// Manages all audio in the game including background music, ambient sounds, and SFX.
/// Singleton pattern.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton
    public static AudioManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeAudioSources();
    }
    #endregion
    
    #region Serialized Fields - Music
    [Header("Background Music")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField] private AudioClip victoryMusic;
    [SerializeField] private AudioClip gameOverMusic;
    [SerializeField] private AudioClip chapterTransitionMusic;
    
    [Header("Music Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.3f;
    [SerializeField] private float musicFadeDuration = 1.5f;
    #endregion
    
    #region Serialized Fields - UI Sounds
    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip dialogueAppearSound;
    [SerializeField] private AudioClip dialogueCharacterSound; // Her harf için
    [SerializeField] private AudioClip continueSound;
    
    [Header("UI Sound Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float uiVolume = 0.5f;
    #endregion
    
    #region Serialized Fields - Question Sounds
    [Header("Question/Answer Sounds")]
    [SerializeField] private AudioClip correctAnswerSound;
    [SerializeField] private AudioClip wrongAnswerSound;
    [SerializeField] private AudioClip questionAppearSound;
    
    [Header("Question Sound Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float questionVolume = 0.6f;
    #endregion
    
    #region Serialized Fields - Game Events
    [Header("Game Event Sounds")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip chapterCompleteSound;
    [SerializeField] private AudioClip bookCollectedSound;
    [SerializeField] private AudioClip keyUnlockSound; // Gate anahtar animasyonu için
    
    [Header("Game Event Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float eventVolume = 0.7f;
    #endregion
    
    #region Serialized Fields - Ambient
    [Header("Ambient Sounds")]
    [SerializeField] private AudioClip forestAmbient;
    [SerializeField] private AudioClip caveAmbient;
    [SerializeField] private AudioClip windAmbient;
    
    [Header("Ambient Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float ambientVolume = 0.2f;
    #endregion
    
    #region Private Fields
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource ambientSource;
    private AudioSource uiSource;
    
    private Coroutine musicFadeCoroutine;
    private bool isMusicMuted;
    private bool isSFXMuted;
    #endregion
    
    #region Initialization
    private void InitializeAudioSources()
    {
        // Music source - looping background music
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;
        
        // SFX source - one-shot sound effects
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        
        // Ambient source - looping ambient sounds
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.volume = ambientVolume;
        
        // UI source - UI sound effects
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.loop = false;
        uiSource.playOnAwake = false;
        uiSource.volume = uiVolume;
    }
    
    private void Start()
    {
        // Subscribe to game state changes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }
    
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }
    #endregion
    
    #region Music Control
    /// <summary>
    /// Plays background music with optional fade.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool fade = true)
    {
        if (clip == null || isMusicMuted) return;
        
        if (fade && musicSource.isPlaying)
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }
            musicFadeCoroutine = StartCoroutine(CrossfadeMusic(clip));
        }
        else
        {
            musicSource.clip = clip;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }
    
    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        // Fade out current
        float elapsed = 0f;
        float startVolume = musicSource.volume;
        
        while (elapsed < musicFadeDuration / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration / 2f));
            yield return null;
        }
        
        // Switch clip
        musicSource.clip = newClip;
        musicSource.Play();
        
        // Fade in new
        elapsed = 0f;
        while (elapsed < musicFadeDuration / 2f)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsed / (musicFadeDuration / 2f));
            yield return null;
        }
        
        musicSource.volume = musicVolume;
    }
    
    public void PlayGameplayMusic()
    {
        PlayMusic(gameplayMusic);
    }
    
    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }
    
    public void PlayVictoryMusic()
    {
        PlayMusic(victoryMusic, false);
    }
    
    public void PlayGameOverMusic()
    {
        PlayMusic(gameOverMusic, false);
    }
    
    public void StopMusic(bool fade = true)
    {
        if (fade)
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }
            musicFadeCoroutine = StartCoroutine(FadeOutMusic());
        }
        else
        {
            musicSource.Stop();
        }
    }
    
    private IEnumerator FadeOutMusic()
    {
        float elapsed = 0f;
        float startVolume = musicSource.volume;
        
        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
            yield return null;
        }
        
        musicSource.Stop();
        musicSource.volume = musicVolume;
    }
    
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }
    #endregion
    
    #region SFX Control
    /// <summary>
    /// Plays a one-shot sound effect.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null || isSFXMuted) return;
        sfxSource.PlayOneShot(clip, volumeMultiplier);
    }
    
    /// <summary>
    /// Plays a sound at a specific world position (3D audio).
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null || isSFXMuted) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
    #endregion
    
    #region Question Sounds
    public void PlayCorrectAnswerSound()
    {
        PlaySFX(correctAnswerSound, questionVolume);
        Debug.Log("[AudioManager] Playing correct answer sound");
    }
    
    public void PlayWrongAnswerSound()
    {
        PlaySFX(wrongAnswerSound, questionVolume);
        Debug.Log("[AudioManager] Playing wrong answer sound");
    }
    
    public void PlayQuestionAppearSound()
    {
        PlaySFX(questionAppearSound, questionVolume);
    }
    #endregion
    
    #region UI Sounds
    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound, uiVolume);
    }
    
    public void PlayButtonHover()
    {
        PlaySFX(buttonHoverSound, uiVolume * 0.5f);
    }
    
    public void PlayDialogueAppear()
    {
        PlaySFX(dialogueAppearSound, uiVolume);
    }
    
    public void PlayDialogueCharacter()
    {
        if (dialogueCharacterSound != null && uiSource != null)
        {
            // Use uiSource for character sounds to avoid overlapping
            uiSource.pitch = Random.Range(0.9f, 1.1f); // Slight variation
            uiSource.PlayOneShot(dialogueCharacterSound, uiVolume * 0.3f);
        }
    }
    
    public void PlayContinueSound()
    {
        PlaySFX(continueSound, uiVolume);
    }
    #endregion
    
    #region Game Event Sounds
    public void PlayVictorySound()
    {
        PlaySFX(victorySound, eventVolume);
    }
    
    public void PlayGameOverSound()
    {
        PlaySFX(gameOverSound, eventVolume);
    }
    
    public void PlayChapterCompleteSound()
    {
        PlaySFX(chapterCompleteSound, eventVolume);
    }
    
    public void PlayBookCollectedSound()
    {
        PlaySFX(bookCollectedSound, eventVolume);
        Debug.Log("[AudioManager] Playing book collected sound");
    }
    
    public void PlayKeyUnlockSound()
    {
        PlaySFX(keyUnlockSound, eventVolume);
        Debug.Log("[AudioManager] Playing key unlock sound");
    }
    #endregion
    
    #region Ambient Control
    public void PlayAmbient(AudioClip clip)
    {
        if (clip == null) return;
        
        ambientSource.clip = clip;
        ambientSource.volume = ambientVolume;
        ambientSource.Play();
    }
    
    public void StopAmbient()
    {
        ambientSource.Stop();
    }
    
    public void SetAmbientVolume(float volume)
    {
        ambientVolume = Mathf.Clamp01(volume);
        if (ambientSource != null)
        {
            ambientSource.volume = ambientVolume;
        }
    }
    #endregion
    
    #region Mute Control
    public void ToggleMusicMute()
    {
        isMusicMuted = !isMusicMuted;
        musicSource.mute = isMusicMuted;
    }
    
    public void ToggleSFXMute()
    {
        isSFXMuted = !isSFXMuted;
    }
    
    public void SetMusicMuted(bool muted)
    {
        isMusicMuted = muted;
        musicSource.mute = muted;
    }
    
    public void SetSFXMuted(bool muted)
    {
        isSFXMuted = muted;
    }
    #endregion
    
    #region Game State Handling
    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Menu:
                PlayMenuMusic();
                break;
            case GameState.Playing:
                if (!musicSource.isPlaying || musicSource.clip != gameplayMusic)
                {
                    PlayGameplayMusic();
                }
                break;
            case GameState.Victory:
                PlayVictorySound();
                PlayVictoryMusic();
                break;
            case GameState.GameOver:
                PlayGameOverSound();
                PlayGameOverMusic();
                break;
            case GameState.ChapterTransition:
                PlayChapterCompleteSound();
                break;
        }
    }
    #endregion
}

