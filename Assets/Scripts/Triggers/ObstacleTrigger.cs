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
    
    [Header("Camera")]
    [SerializeField] private Camera triggerCamera; // Bu trigger için özel kamera
    [SerializeField] private float cameraTransitionDuration = 1.0f;
    
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
    
    [Header("Tree Leaves (Gate Only)")]
    [Tooltip("TreeLeaf objects to activate when Gate obstacle is cleared. Leave empty to search by tag.")]
    [SerializeField] private GameObject[] treeLeafObjects;
    
    [Header("Door (Gate Only)")]
    [Tooltip("Door GameObject to open when Gate obstacle is cleared")]
    [SerializeField] private GameObject doorObject;
    
    [Tooltip("Target Y rotation for the door when opened (in degrees)")]
    [SerializeField] private float doorOpenRotationY = 140f;
    
    [Tooltip("Duration of door opening animation (in seconds)")]
    [SerializeField] private float doorOpenDuration = 2f;
    
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
        Debug.Log($"[ObstacleTrigger] OnTriggerEnter called on {gameObject.name}, tag: {other.tag}");
        Debug.Log($"[ObstacleTrigger] State: isActive={isActive}, IsCleared={IsCleared}, IsProcessing={IsProcessing}");
        
        if (!isActive)
        {
            Debug.LogWarning($"[ObstacleTrigger] Ignored - not active");
            return;
        }
        if (IsCleared)
        {
            Debug.LogWarning($"[ObstacleTrigger] Ignored - already cleared");
            return;
        }
        if (IsProcessing)
        {
            Debug.LogWarning($"[ObstacleTrigger] Ignored - already processing");
            return;
        }
        
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log($"[ObstacleTrigger] Player entered {obstacleType} trigger: {gameObject.name}");
            
            StartDialogueThenQuestion();
        }
        else
        {
            Debug.Log($"[ObstacleTrigger] Non-player object entered: {other.gameObject.name}");
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
        
        // Kamera geçişi ile başla
        StartCoroutine(DialogueQuestionSequence());
    }
    
    private System.Collections.IEnumerator DialogueQuestionSequence()
    {
        Debug.Log($"[ObstacleTrigger] DialogueQuestionSequence started");
        
        // Kamera geçişi
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log($"[ObstacleTrigger] Transitioning to {obstacleType} camera...");
            bool cameraTransitionComplete = false;
            CameraManager.Instance.TransitionToCamera(triggerCamera, cameraTransitionDuration, () =>
            {
                cameraTransitionComplete = true;
            });
            yield return new WaitUntil(() => cameraTransitionComplete);
            Debug.Log($"[ObstacleTrigger] Camera transition complete");
        }
        else
        {
            Debug.Log($"[ObstacleTrigger] No camera or CameraManager - triggerCamera: {(triggerCamera != null ? triggerCamera.name : "NULL")}, CameraManager: {(CameraManager.Instance != null ? "EXISTS" : "NULL")}");
        }

        // Get approach dialogue messages (supports two-part dialogue)
        string[] approachMessages = customQuestion != null ? customQuestion.GetApproachDialogueMessages() : null;
        Debug.Log($"[ObstacleTrigger] Approach dialogue: {(approachMessages != null && approachMessages.Length > 0 ? $"{approachMessages.Length} part(s)" : "EMPTY")}");

        // Diyalog varsa önce onu göster
        if (approachMessages != null && approachMessages.Length > 0 && UIManager.Instance != null)
        {
            Debug.Log($"[ObstacleTrigger] Showing approach dialogue ({approachMessages.Length} part(s))...");
            bool dialogueComplete = false;
            UIManager.Instance.ShowDialogueSequence(approachMessages, () =>
            {
                dialogueComplete = true;
            });
            yield return new WaitUntil(() => dialogueComplete);
            Debug.Log($"[ObstacleTrigger] Approach dialogue complete");
        }
        
        // Soruyu göster
        Debug.Log($"[ObstacleTrigger] Calling ShowQuestion...");
        ShowQuestion();
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

        // Get success dialogue messages (supports two-part dialogue)
        string[] successMessages = customQuestion != null ? customQuestion.GetSuccessDialogueMessages() : null;

        // Show success dialogue and wait for Continue (hala UI mode'dayız)
        if (successMessages != null && successMessages.Length > 0 && UIManager.Instance != null)
        {
            bool dialogueClosed = false;
            UIManager.Instance.ShowDialogueSequence(successMessages, () =>
            {
                dialogueClosed = true;
            });

            // Wait until user presses Continue
            yield return new WaitUntil(() => dialogueClosed);
        }
        
        // Oyuncu kamerasına geri dön
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log($"[ObstacleTrigger] Returning to player camera...");
            bool cameraReturnComplete = false;
            CameraManager.Instance.TransitionToPlayerCamera(cameraTransitionDuration, () =>
            {
                cameraReturnComplete = true;
            });
            yield return new WaitUntil(() => cameraReturnComplete);
            Debug.Log($"[ObstacleTrigger] Returned to player camera");
        }
        
        // Oyun moduna dön
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReturnToGameMode();
        }
        
        // Disable blocking collider
        if (blockingCollider != null)
        {
            blockingCollider.SetActive(false);
        }
        
        Debug.Log($"[ObstacleTrigger] Checking obstacle type: {obstacleType}, IsGate: {obstacleType == ObstacleType.Gate}");
        
        // If this is a Gate obstacle, activate TreeLeaf objects and open door (no removal animation)
        if (obstacleType == ObstacleType.Gate)
        {
            Debug.Log("[ObstacleTrigger] Gate obstacle detected, calling ActivateTreeLeaves()...");
            ActivateTreeLeaves();
            
            // Open door if door object is set
            if (doorObject != null)
            {
                Debug.Log("[ObstacleTrigger] Opening door...");
                yield return StartCoroutine(OpenDoorCoroutine());
            }
            else
            {
                Debug.LogWarning("[ObstacleTrigger] Door object is not assigned!");
            }
            
            Debug.Log("[ObstacleTrigger] Gate obstacle cleared - TreeLeaf objects activated and door opened, obstacle remains visible");
            
            // Disable trigger but keep obstacle visible
            isActive = false;
            yield break; // Exit early, don't run removal animations
        }
        else
        {
            Debug.Log($"[ObstacleTrigger] Non-Gate obstacle ({obstacleType}), proceeding with removal animation");
        }
        
        // For non-Gate obstacles, run removal animations
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
        
        // Disable the obstacle visual (only for non-Gate obstacles)
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
    
    /// <summary>
    /// Activates all TreeLeaf objects when Gate obstacle is cleared.
    /// First tries to use serialized treeLeafObjects array, then falls back to tag search.
    /// </summary>
    private void ActivateTreeLeaves()
    {
        Debug.Log("[ObstacleTrigger] ActivateTreeLeaves() called - searching for TreeLeaf objects...");
        
        System.Collections.Generic.List<GameObject> treeLeaves = new System.Collections.Generic.List<GameObject>();
        
        // First, use serialized array if available
        if (treeLeafObjects != null && treeLeafObjects.Length > 0)
        {
            Debug.Log($"[ObstacleTrigger] Using serialized treeLeafObjects array ({treeLeafObjects.Length} object(s))");
            foreach (GameObject leaf in treeLeafObjects)
            {
                if (leaf != null)
                {
                    treeLeaves.Add(leaf);
                }
            }
        }
        
        // If no objects found in serialized array, search by tag (including inactive objects)
        if (treeLeaves.Count == 0)
        {
            Debug.Log("[ObstacleTrigger] No serialized objects found, searching by 'TreeLeaf' tag...");
            
            // Find all objects with TreeLeaf tag (including inactive ones)
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.CompareTag("TreeLeaf"))
                {
                    treeLeaves.Add(obj);
                }
            }
            
            // Also try FindGameObjectsWithTag for active objects
            GameObject[] activeTreeLeaves = GameObject.FindGameObjectsWithTag("TreeLeaf");
            foreach (GameObject leaf in activeTreeLeaves)
            {
                if (!treeLeaves.Contains(leaf))
                {
                    treeLeaves.Add(leaf);
                }
            }
        }
        
        Debug.Log($"[ObstacleTrigger] Found {treeLeaves.Count} TreeLeaf object(s) total");
        
        if (treeLeaves.Count > 0)
        {
            Debug.Log($"[ObstacleTrigger] Activating {treeLeaves.Count} TreeLeaf object(s)");
            foreach (GameObject leaf in treeLeaves)
            {
                if (leaf != null)
                {
                    Debug.Log($"[ObstacleTrigger] Activating TreeLeaf: {leaf.name}");
                    leaf.SetActive(true);
                }
                else
                {
                    Debug.LogWarning("[ObstacleTrigger] Found null TreeLeaf object in list");
                }
            }
            Debug.Log("[ObstacleTrigger] All TreeLeaf objects activated");
        }
        else
        {
            Debug.LogWarning("[ObstacleTrigger] No TreeLeaf objects found! Make sure to either:");
            Debug.LogWarning("  1. Assign TreeLeaf objects to 'Tree Leaf Objects' array in Inspector, OR");
            Debug.LogWarning("  2. Tag your TreeLeaf objects with 'TreeLeaf' tag in Unity");
        }
    }
    
    /// <summary>
    /// Smoothly opens the door by rotating it to the target Y rotation.
    /// </summary>
    private IEnumerator OpenDoorCoroutine()
    {
        if (doorObject == null)
        {
            Debug.LogWarning("[ObstacleTrigger] Door object is null, cannot open door");
            yield break;
        }
        
        Vector3 startRotation = doorObject.transform.localEulerAngles;
        Vector3 targetRotation = new Vector3(startRotation.x, doorOpenRotationY, startRotation.z);
        
        Debug.Log($"[ObstacleTrigger] Opening door from Y={startRotation.y}° to Y={doorOpenRotationY}° over {doorOpenDuration}s");
        
        float elapsed = 0f;
        
        while (elapsed < doorOpenDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / doorOpenDuration;
            
            // Smooth ease-out curve
            t = 1f - Mathf.Pow(1f - t, 3f);
            
            // Interpolate rotation
            Vector3 currentRotation = Vector3.Lerp(startRotation, targetRotation, t);
            doorObject.transform.localEulerAngles = currentRotation;
            
            yield return null;
        }
        
        // Ensure final rotation is exact
        doorObject.transform.localEulerAngles = targetRotation;
        Debug.Log($"[ObstacleTrigger] Door opened to Y={doorOpenRotationY}°");
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

