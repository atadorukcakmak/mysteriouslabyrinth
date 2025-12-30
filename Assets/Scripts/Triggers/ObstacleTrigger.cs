using System.Collections;
using UnityEngine;

/// <summary>
/// Obstacle that blocks player path until a question is answered correctly.
/// Implements IInteractable for raycast-based interaction.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ObstacleTrigger : MonoBehaviour, IInteractable
{
    #region Serialized Fields
    [Header("Obstacle Settings")]
    [SerializeField] private ObstacleType obstacleType = ObstacleType.Gate;
    [SerializeField] private bool isActive = true;
    [SerializeField] private bool requireInteraction = false; // If true, player must press E; if false, auto-trigger
    
    [Header("Question")]
    [SerializeField] private QuestionData customQuestion; // Override chapter question
    
    [Header("Visual Elements")]
    [SerializeField] private GameObject obstacleVisual; // The visible obstacle (Pharaoh, Tree, etc.)
    [SerializeField] private GameObject blockingCollider; // Collider that blocks path
    
    [Header("Removal Animation")]
    [SerializeField] private RemovalType removalType = RemovalType.Disappear;
    [SerializeField] private float removalDuration = 1.5f;
    [SerializeField] private Transform sinkTarget; // For sinking animation (Pharaoh into water)
    [SerializeField] private float sinkDepth = 3f;
    
    [Header("Environment")]
    [SerializeField] private int environmentZoneId = -1; // Zone to transform on success
    [SerializeField] private float transformRadius = 10f;
    
    
    #endregion
    
    #region Properties
    public bool IsCleared { get; private set; }
    public bool IsProcessing { get; private set; }
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool playerInRange;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        if (obstacleVisual == null)
        {
            obstacleVisual = gameObject;
        }
        
        if (blockingCollider == null)
        {
            // Try to find a non-trigger collider child
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                if (!col.isTrigger)
                {
                    blockingCollider = col.gameObject;
                    break;
                }
            }
        }
    }
    #endregion
    
    #region Trigger Detection
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || IsCleared || IsProcessing) return;
        
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log($"[ObstacleTrigger] Player entered {obstacleType} trigger: {gameObject.name}");
            
            StartDialogueThenQuestion();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
    #endregion
    
    #region IInteractable Implementation
    public void OnLookAt()
    {
        // Could show interaction prompt here
        Debug.Log($"[ObstacleTrigger] Looking at {gameObject.name}");
    }
    
    public void OnLookAway()
    {
        // Hide interaction prompt
    }
    
    public void Interact(GameObject interactor)
    {
        if (!isActive || IsCleared || IsProcessing) return;
        
        if (requireInteraction && playerInRange)
        {
            StartDialogueThenQuestion();
        }
    }
    #endregion
    
    #region Question Logic
    /// <summary>
    /// Önce diyalog gösterir, sonra soru sorar.
    /// </summary>
    private void StartDialogueThenQuestion()
    {
        if (IsProcessing || IsCleared) return;
        
        IsProcessing = true;

        string approachDialogue = customQuestion.approachDialogue;

        // Diyalog varsa önce onu göster
        if (!string.IsNullOrEmpty(approachDialogue) && UIManager.Instance != null)
        {
            UIManager.Instance.ShowDialogueWithCallback(approachDialogue, () =>
            {
                // Diyalog bittikten sonra soruyu göster
                ShowQuestion();
            });
        }
        else
        {
            ShowQuestion();             
        }
    }
    
    private void ShowQuestion()
    {
        QuestionData question = GetQuestion();
        
        if (question != null)
        {
            Debug.Log($"[ObstacleTrigger] Showing question: {question.questionText}");
            QuestionManager.Instance?.AskQuestion(question, OnQuestionAnswered, this);
        }
        else
        {
            Debug.LogWarning($"[ObstacleTrigger] No question available for {gameObject.name}. Clearing automatically.");
            // Soru yok - oyunu düzgün duruma döndür ve engeli otomatik temizle
            IsProcessing = false;
            
            // Game state'i düzelt
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
            }
            
            // Diyalog panelini kapat
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseDialogue();
            }
            
            // Engeli otomatik temizle (test için)
            ClearObstacle();
        }
    }
    
    private QuestionData GetQuestion()
    {
            return customQuestion;
    }


    private void OnQuestionAnswered(bool isCorrect)
    {
        if (isCorrect)
        {
            ClearObstacle();
        }
        else
        {
            // Player got it wrong - they can try again
            IsProcessing = false;
            Debug.Log($"[ObstacleTrigger] Wrong answer at {gameObject.name}");
        }
    }
    #endregion
    
    #region Obstacle Removal
    private void ClearObstacle()
    {
        IsCleared = true;
        IsProcessing = false;
        
        Debug.Log($"[ObstacleTrigger] Clearing obstacle: {gameObject.name}");
        
        // Start removal animation (dialogue shown after animation starts)
        StartCoroutine(RemovalCoroutine());
        
        // Transform environment
        TransformEnvironment();
    }
    
    private IEnumerator RemovalCoroutine()
    {
        // Bir frame bekle - soru paneli kapansın
        yield return null;

        string successDialogue = customQuestion != null ? customQuestion.successDialogue : null;

        // Show success dialogue and wait for Continue (hala UI mode'dayız)
        if (!string.IsNullOrEmpty(successDialogue) && UIManager.Instance != null)
        {
            bool dialogueClosed = false;
            UIManager.Instance.ShowDialogueWithCallback(successDialogue, () =>
            {
                dialogueClosed = true;
            });

            // Wait until user presses Continue
            yield return new WaitUntil(() => dialogueClosed);
        }
        
        // Oyun moduna dön - success diyaloğu bitti
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReturnToGameMode();
        }
        
        // Disable blocking collider
        if (blockingCollider != null)
        {
            blockingCollider.SetActive(false);
        }
        
        switch (removalType)
        {
            case RemovalType.Disappear:
                yield return StartCoroutine(FadeOutCoroutine());
                break;
                
            case RemovalType.Sink:
                yield return StartCoroutine(SinkCoroutine());
                break;
                
            case RemovalType.Explode:
                yield return StartCoroutine(ExplodeCoroutine());
                break;
                
            case RemovalType.Dissolve:
                yield return StartCoroutine(DissolveCoroutine());
                break;
        }
        
        // Disable the obstacle visual
        if (obstacleVisual != null)
        {
            obstacleVisual.SetActive(false);
        }
        
        // Disable trigger
        isActive = false;
    }
    
    private IEnumerator FadeOutCoroutine()
    {
        Renderer[] renderers = obstacleVisual.GetComponentsInChildren<Renderer>();
        float elapsed = 0f;
        
        // Store original colors
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
            {
                originalColors[i] = renderers[i].material.GetColor("_BaseColor");
            }
            else if (renderers[i].material.HasProperty("_Color"))
            {
                originalColors[i] = renderers[i].material.GetColor("_Color");
            }
        }
        
        while (elapsed < removalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / removalDuration;
            float alpha = 1f - t;
            
            for (int i = 0; i < renderers.Length; i++)
            {
                Color newColor = originalColors[i];
                newColor.a = alpha;
                
                if (renderers[i].material.HasProperty("_BaseColor"))
                {
                    renderers[i].material.SetColor("_BaseColor", newColor);
                }
                else if (renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.SetColor("_Color", newColor);
                }
            }
            
            yield return null;
        }
    }
    
    private IEnumerator SinkCoroutine()
    {
        Vector3 startPos = obstacleVisual.transform.position;
        Vector3 endPos = startPos - Vector3.up * sinkDepth;
        float elapsed = 0f;
        
        while (elapsed < removalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / removalDuration;
            
            // Ease out
            t = 1f - Mathf.Pow(1f - t, 2f);
            
            obstacleVisual.transform.position = Vector3.Lerp(startPos, endPos, t);
            
            yield return null;
        }
        
        obstacleVisual.transform.position = endPos;
    }
    
    private IEnumerator ExplodeCoroutine()
    {
        // Simple scale down + scatter effect
        Vector3 startScale = obstacleVisual.transform.localScale;
        float elapsed = 0f;
        
        while (elapsed < removalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / removalDuration;
            
            obstacleVisual.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            obstacleVisual.transform.Rotate(Vector3.up, 360f * Time.deltaTime);
            
            yield return null;
        }
    }
    
    private IEnumerator DissolveCoroutine()
    {
        // Similar to fade but with upward movement
        Vector3 startPos = obstacleVisual.transform.position;
        float elapsed = 0f;
        
        Renderer[] renderers = obstacleVisual.GetComponentsInChildren<Renderer>();
        
        while (elapsed < removalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / removalDuration;
            
            // Move up slightly
            obstacleVisual.transform.position = startPos + Vector3.up * t * 0.5f;
            
            // Fade out
            float alpha = 1f - t;
            foreach (var rend in renderers)
            {
                if (rend.material.HasProperty("_BaseColor"))
                {
                    Color c = rend.material.GetColor("_BaseColor");
                    c.a = alpha;
                    rend.material.SetColor("_BaseColor", c);
                }
            }
            
            yield return null;
        }
    }
    #endregion
    
    #region Environment
    private void TransformEnvironment()
    {
        if (EnvironmentManager.Instance == null) return;
        
        if (environmentZoneId >= 0)
        {
            EnvironmentManager.Instance.TransformZone(environmentZoneId);
        }
        else
        {
            // Transform nearby zones based on position
            EnvironmentManager.Instance.TransformNearbyZones(transform.position, transformRadius);
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Manually clear the obstacle (for testing or special cases).
    /// </summary>
    public void ForceClear()
    {
        if (!IsCleared)
        {
            ClearObstacle();
        }
    }
    
    /// <summary>
    /// Reset the obstacle to its initial state.
    /// </summary>
    public void Reset()
    {
        IsCleared = false;
        IsProcessing = false;
        isActive = true;
        
        if (obstacleVisual != null)
        {
            obstacleVisual.SetActive(true);
        }
        
        if (blockingCollider != null)
        {
            blockingCollider.SetActive(true);
        }
    }
    #endregion
}

/// <summary>
/// Types of obstacles.
/// </summary>
public enum ObstacleType
{
    Gate,           // Entry gate (Besher Tree, Library, etc.)
    MainObstacle    // Main chapter obstacle (Pharaoh, Evil King, etc.)
}

/// <summary>
/// How the obstacle is removed after correct answer.
/// </summary>
public enum RemovalType
{
    Disappear,  // Fade out
    Sink,       // Sink into ground/water (Pharaoh)
    Explode,    // Break apart
    Dissolve    // Dissolve upward
}

