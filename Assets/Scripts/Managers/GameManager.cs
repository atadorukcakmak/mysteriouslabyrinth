using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Central game manager handling state, progression, and core game flow.
/// Singleton pattern - persists across scenes.
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Initialize();
    }
    #endregion
    
    #region Events
    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnChapterChanged;
    public event Action OnGamePaused;
    public event Action OnGameResumed;
    public event Action OnGameOver;
    public event Action OnGameWon;
    #endregion
    
    #region Serialized Fields
    [Header("Chapter Data")]
    [SerializeField] private ChapterData[] chapters;
    
    [Header("Settings")]
    [SerializeField] private int startingLives = 3;
    #endregion
    
    #region Properties
    public GameState CurrentState { get; private set; } = GameState.Menu;
    public int CurrentChapter { get; private set; } = 1;
    public ChapterData CurrentChapterData => GetChapterData(CurrentChapter);
    public int StartingLives => startingLives;
    public bool IsPaused { get; private set; }
    #endregion
    
    #region Private Fields
    private int totalChapters = 4;
    #endregion
    
    #region Initialization
    private void Initialize()
    {
        CurrentChapter = 1;
        CurrentState = GameState.Menu;
        IsPaused = false;
        
        if (chapters != null)
        {
            totalChapters = chapters.Length;
        }
    }
    #endregion
    
    #region State Management
    /// <summary>
    /// Changes the current game state and notifies listeners.
    /// </summary>
    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState) return;
        
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
        
        Debug.Log($"[GameManager] State changed to: {newState}");
    }
    
    /// <summary>
    /// Pauses the game.
    /// </summary>
    public void PauseGame()
    {
        if (IsPaused) return;
        
        IsPaused = true;
        Time.timeScale = 0f;
        SetGameState(GameState.Paused);
        OnGamePaused?.Invoke();
        
        // Unlock cursor for menu interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Resumes the game.
    /// </summary>
    public void ResumeGame()
    {
        if (!IsPaused) return;
        
        IsPaused = false;
        Time.timeScale = 1f;
        SetGameState(GameState.Playing);
        OnGameResumed?.Invoke();
        
        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    #endregion
    
    #region Chapter Management
    /// <summary>
    /// Gets the chapter data for a specific chapter number.
    /// </summary>
    public ChapterData GetChapterData(int chapterNumber)
    {
        if (chapters == null || chapters.Length == 0) return null;
        
        int index = chapterNumber - 1;
        if (index >= 0 && index < chapters.Length)
        {
            return chapters[index];
        }
        return null;
    }
    
    /// <summary>
    /// Starts a specific chapter.
    /// </summary>
    public void StartChapter(int chapterNumber)
    {
        if (chapterNumber < 1 || chapterNumber > totalChapters)
        {
            Debug.LogError($"[GameManager] Invalid chapter number: {chapterNumber}");
            return;
        }
        
        CurrentChapter = chapterNumber;
        OnChapterChanged?.Invoke(CurrentChapter);
        SetGameState(GameState.Playing);
        
        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log($"[GameManager] Starting Chapter {chapterNumber}");
    }
    
    /// <summary>
    /// Advances to the next chapter or triggers game completion.
    /// </summary>
    public void CompleteChapter()
    {
        Debug.Log($"[GameManager] Chapter {CurrentChapter} completed!");
        
        if (CurrentChapter >= totalChapters)
        {
            // All chapters completed - game won!
            TriggerGameWon();
        }
        else
        {
            // Advance to next chapter
            CurrentChapter++;
            OnChapterChanged?.Invoke(CurrentChapter);
            SetGameState(GameState.ChapterTransition);
        }
    }
    
    /// <summary>
    /// Restarts the current chapter.
    /// </summary>
    public void RestartChapter()
    {
        Debug.Log($"[GameManager] Restarting Chapter {CurrentChapter}");
        
        Time.timeScale = 1f;
        IsPaused = false;
        
        // Reload current scene
        ChapterData data = CurrentChapterData;
        if (data != null && !string.IsNullOrEmpty(data.sceneName))
        {
            SceneManager.LoadScene(data.sceneName);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        
        SetGameState(GameState.Playing);
    }
    #endregion
    
    #region Game Flow
    /// <summary>
    /// Called when player loses all lives.
    /// </summary>
    public void TriggerGameOver()
    {
        SetGameState(GameState.GameOver);
        OnGameOver?.Invoke();
        
        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("[GameManager] Game Over!");
    }
    
    /// <summary>
    /// Called when player completes all chapters.
    /// </summary>
    public void TriggerGameWon()
    {
        SetGameState(GameState.Victory);
        OnGameWon?.Invoke();
        
        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("[GameManager] Victory! All chapters completed!");
    }
    
    /// <summary>
    /// Starts a new game from chapter 1.
    /// </summary>
    public void StartNewGame()
    {
        CurrentChapter = 1;
        StartChapter(1);
    }
    
    /// <summary>
    /// Returns to main menu.
    /// </summary>
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SetGameState(GameState.Menu);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        SceneManager.LoadScene("MainMenu");
    }
    
    /// <summary>
    /// Quits the application.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("[GameManager] Quitting game...");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    #endregion
    
    #region Input Handling
    private void Update()
    {
        // Handle pause input using new Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (CurrentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (CurrentState == GameState.Paused)
            {
                ResumeGame();
            }
        }
    }
    #endregion
}

/// <summary>
/// Possible game states.
/// </summary>
public enum GameState
{
    Menu,
    Playing,
    Paused,
    Question,       // Player is answering a question
    Dialogue,       // Dialogue/cutscene playing
    ChapterTransition,
    GameOver,
    Victory
}

