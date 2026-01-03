using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the main menu UI and functionality.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Text versionText;
    
    [Header("Version")]
    [SerializeField] private string versionNumber = "1.0.0";
    #endregion
    
    #region Unity Callbacks
    private void Start()
    {
        // Setup button listener
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }
        
        // Set version text
        UpdateVersionText();
        
        // Ensure cursor is visible in menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Set game state to Menu if GameManager exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Menu);
        }
        
        // Play menu music if AudioManager exists
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuMusic();
        }
        
        Debug.Log("[MainMenuManager] Main menu initialized");
    }
    
    private void UpdateVersionText()
    {
        if (versionText != null)
        {
            versionText.text = $"v{versionNumber}";
        }
    }
    #endregion
    
    #region Button Handlers
    public void OnPlayButtonClicked()
    {
        Debug.Log("[MainMenuManager] Play button clicked - loading SampleScene...");
        
        // Play button click sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
        
        // Load SampleScene
        SceneManager.LoadScene("SampleScene");
    }
    
    /// <summary>
    /// Called by UI button to quit the game.
    /// </summary>
    public void OnQuitButtonClicked()
    {
        Debug.Log("[MainMenuManager] Quit button clicked");
        
        // Play button click sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    #endregion
}

