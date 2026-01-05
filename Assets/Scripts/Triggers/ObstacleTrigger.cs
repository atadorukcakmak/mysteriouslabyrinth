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
    
    [Header("Dramatic Drowning Settings")]
    [Tooltip("Water plane that becomes visible during drowning")]
    [SerializeField] private GameObject waterPlane;
    
    [Tooltip("Total duration of the drowning sequence")]
    [SerializeField] private float drowningDuration = 3.5f;
    
    [Header("Drowning - Shake Phase")]
    [Tooltip("How long the statue shakes before sinking")]
    [SerializeField] private float shakeDuration = 0.8f;
    [Tooltip("Intensity of the shake")]
    [SerializeField] private float shakeIntensity = 0.05f;
    
    [Header("Drowning - Tilt Phase")]
    [Tooltip("Maximum tilt angle on X axis")]
    [SerializeField] private float maxTiltX = 15f;
    [Tooltip("Maximum tilt angle on Z axis")]
    [SerializeField] private float maxTiltZ = 20f;
    
    [Header("Drowning - Wobble Phase")]
    [Tooltip("Horizontal wobble frequency during sink")]
    [SerializeField] private float wobbleFrequency = 3f;
    [Tooltip("Horizontal wobble amplitude")]
    [SerializeField] private float wobbleAmplitude = 0.1f;
    
    [Header("Drowning - Final Spin")]
    [Tooltip("Y rotation during final phase")]
    [SerializeField] private float finalSpinDegrees = 60f;
    
    [Header("Water Rise (Pharaoh Only)")]
    [Tooltip("Water object that rises from the ground when Pharaoh obstacle is cleared")]
    [SerializeField] private GameObject waterRiseObject;
    
    [Tooltip("How high the water rises from its starting position")]
    [SerializeField] private float waterRiseHeight = 2f;
    
    [Tooltip("Duration of water rising animation")]
    [SerializeField] private float waterRiseDuration = 2f;
    
    [Tooltip("Delay before water starts rising (after question answered)")]
    [SerializeField] private float waterRiseDelay = 0.5f;
    
    [Tooltip("Sound played when water starts rising")]
    [SerializeField] private AudioClip waterRiseSound;
    
    [Tooltip("Volume of water rising sound")]
    [Range(0f, 1f)]
    [SerializeField] private float waterRiseSoundVolume = 0.8f;
    
    [Header("Environment")]
    [SerializeField] private int environmentZoneId = -1; // Zone to transform on success
    [SerializeField] private float transformRadius = 10f;
    
    [Header("Tree Leaves (Gate Only)")]
    [Tooltip("TreeLeaf objects to activate when Gate obstacle is cleared. Leave empty to search by tag.")]
    [SerializeField] private GameObject[] treeLeafObjects;
    
    [Tooltip("Delay between each TreeLeaf activation (in seconds)")]
    [SerializeField] private float treeLeafActivationDelay = 0.2f;
    
    [Header("Door (Gate Only)")]
    [Tooltip("Door GameObject to open when Gate obstacle is cleared")]
    [SerializeField] private GameObject doorObject;
    
    [Tooltip("Target Y rotation for the door when opened (in degrees)")]
    [SerializeField] private float doorOpenRotationY = 140f;
    
    [Tooltip("Duration of door opening animation (in seconds)")]
    [SerializeField] private float doorOpenDuration = 2f;
    
    [Header("Audio (Gate Only)")]
    [Tooltip("Sound played when door opens")]
    [SerializeField] private AudioClip doorOpenSound;
    [Tooltip("Volume of door opening sound")]
    [Range(0f, 1f)]
    [SerializeField] private float doorSoundVolume = 0.7f;
    
    #endregion
    
    #region Properties
    public bool IsCleared { get; private set; }
    public bool IsProcessing { get; private set; }
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool playerInRange;
    private bool doorAnimationComplete;
    private bool treeLeavesActivationComplete;
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

        // Gate soruları için success dialogue atlanır - anahtar animasyonu yeterli
        // Diğer sorular için success dialogue göster
        if (obstacleType != ObstacleType.Gate)
        {
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
        }
        else
        {
            Debug.Log("[ObstacleTrigger] Gate obstacle - skipping success dialogue, key animation was already shown");
        }
        
        // Disable blocking collider
        if (blockingCollider != null)
        {
            blockingCollider.SetActive(false);
        }
        
        Debug.Log($"[ObstacleTrigger] Checking obstacle type: {obstacleType}, IsGate: {obstacleType == ObstacleType.Gate}");
        
        // If this is a Gate obstacle, show success text first, then open door and activate TreeLeaves
        if (obstacleType == ObstacleType.Gate)
        {
            Debug.Log("[ObstacleTrigger] Gate obstacle detected");
            
            // Get success dialogue for Gate
            string[] successMessages = customQuestion != null ? customQuestion.GetSuccessDialogueMessages() : null;
            
            // Step 1: Show success dialogue FIRST and wait for it to complete
            if (successMessages != null && successMessages.Length > 0 && UIManager.Instance != null)
            {
                Debug.Log("[ObstacleTrigger] Showing success text...");
                bool dialogueComplete = false;
                UIManager.Instance.ShowDialogueSequence(successMessages, () =>
                {
                    dialogueComplete = true;
                    Debug.Log("[ObstacleTrigger] Success dialogue completed (Continue pressed)");
                });
                
                // Wait for player to press Continue
                yield return new WaitUntil(() => dialogueComplete);
            }
            
            Debug.Log("[ObstacleTrigger] Success text completed, now starting door animation and activating TreeLeaves...");
            
            // Step 2: AFTER success text is complete, start activating TreeLeaves one by one (parallel with door)
            Debug.Log("[ObstacleTrigger] Starting TreeLeaves activation...");
            StartCoroutine(ActivateTreeLeavesCoroutine());
            
            // Step 3: Start door opening animation AFTER success text is complete (parallel with TreeLeaves)
            if (doorObject != null)
            {
                Debug.Log("[ObstacleTrigger] Opening door...");
                StartCoroutine(OpenDoorCoroutine());
            }
            else
            {
                Debug.LogWarning("[ObstacleTrigger] Door object is not assigned!");
                doorAnimationComplete = true; // No door to animate
            }
            
            // Step 4: Wait for BOTH door animation AND TreeLeaves activation to complete
            Debug.Log("[ObstacleTrigger] Waiting for door animation and TreeLeaves activation to complete...");
            yield return new WaitUntil(() => doorAnimationComplete && treeLeavesActivationComplete);
            Debug.Log("[ObstacleTrigger] Both door animation and TreeLeaves activation completed");
            
            // Step 5: AFTER both animations are complete, return camera to player
            if (triggerCamera != null && CameraManager.Instance != null)
            {
                Debug.Log("[ObstacleTrigger] Returning to player camera...");
                bool cameraReturnComplete = false;
                CameraManager.Instance.TransitionToPlayerCamera(cameraTransitionDuration, () =>
                {
                    cameraReturnComplete = true;
                });
                yield return new WaitUntil(() => cameraReturnComplete);
                Debug.Log("[ObstacleTrigger] Returned to player camera");
            }
            
            // Return to game mode
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ReturnToGameMode();
            }
            
            Debug.Log("[ObstacleTrigger] Gate obstacle cleared - TreeLeaf objects activated and door opened");
            
            // Disable trigger but keep obstacle visible
            isActive = false;
            yield break; // Exit early, don't run removal animations
        }
        else
        {
            Debug.Log($"[ObstacleTrigger] Non-Gate obstacle ({obstacleType}), proceeding with removal animation");
        }
        
        // For non-Gate obstacles, run removal animations FIRST
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
        
        // For non-Gate obstacles, return camera to player AFTER removal animation
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
        
        // Return to game mode
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReturnToGameMode();
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
        // Use the new dramatic drowning sequence
        yield return StartCoroutine(DramaticDrowningCoroutine());
    }
    
    /// <summary>
    /// Dramatic 5-phase drowning sequence:
    /// Phase 1: SHAKE (0.0 - 0.8s) - Statue trembles as if struggling
    /// Phase 2: TIP (0.3 - 1.0s) - Statue tilts to one side
    /// Phase 3: SINK (0.8 - 3.5s) - Accelerating descent (Ease-In-Quad)
    /// Phase 4: WOBBLE (1.0 - 3.0s) - Horizontal oscillation during sink
    /// Phase 5: FINAL SPIN (2.5 - 3.5s) - Dramatic Y rotation
    /// </summary>
    private IEnumerator DramaticDrowningCoroutine()
    {
        Debug.Log($"[ObstacleTrigger] Starting dramatic drowning sequence for {gameObject.name}");
        
        // Activate water plane
        if (waterPlane != null)
        {
            waterPlane.SetActive(true);
            Debug.Log("[ObstacleTrigger] Water plane activated");
        }
        
        // Start water rise animation for Pharaoh obstacles (parallel with sinking)
        if (obstacleType == ObstacleType.MainObstacle && gameObject.CompareTag("Pharaoh"))
        {
            StartCoroutine(WaterRiseCoroutine());
        }
        
        // Store initial transform values
        Vector3 startPos = obstacleVisual.transform.position;
        Quaternion startRot = obstacleVisual.transform.rotation;
        Vector3 startEuler = startRot.eulerAngles;
        
        // Calculate end position (use sinkTarget if available, otherwise use sinkDepth)
        Vector3 endPos;
        if (sinkTarget != null)
        {
            endPos = sinkTarget.position;
        }
        else
        {
            endPos = startPos - Vector3.up * sinkDepth;
        }
        
        // Randomize tilt direction for organic feel
        float tiltDirectionX = Random.Range(-1f, 1f) > 0 ? 1f : -1f;
        float tiltDirectionZ = Random.Range(-1f, 1f) > 0 ? 1f : -1f;
        float targetTiltX = maxTiltX * tiltDirectionX * Random.Range(0.7f, 1f);
        float targetTiltZ = maxTiltZ * tiltDirectionZ * Random.Range(0.7f, 1f);
        float targetSpinY = finalSpinDegrees * (Random.Range(-1f, 1f) > 0 ? 1f : -1f);
        
        Debug.Log($"[ObstacleTrigger] Drowning params - TiltX: {targetTiltX:F1}°, TiltZ: {targetTiltZ:F1}°, SpinY: {targetSpinY:F1}°");
        Debug.Log($"[ObstacleTrigger] Sink from {startPos} to {endPos}");
        
        float elapsed = 0f;
        float totalDuration = drowningDuration;
        
        // Phase timing (normalized 0-1)
        float shakeEnd = shakeDuration / totalDuration;           // ~0.23
        float tipStart = 0.08f;                                    // Start tipping early
        float tipEnd = 0.35f;                                      // Finish tipping
        float sinkStart = 0.2f;                                    // Start sinking
        float wobbleStart = 0.25f;                                 // Start wobbling
        float wobbleEnd = 0.85f;                                   // End wobbling
        float spinStart = 0.7f;                                    // Start final spin
        
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);  // Normalized time 0-1
            
            Vector3 currentPos = startPos;
            Vector3 currentEuler = startEuler;
            
            // ═══════════════════════════════════════════════════════════
            // PHASE 1: SHAKE (0.0 - shakeEnd)
            // ═══════════════════════════════════════════════════════════
            if (t < shakeEnd)
            {
                float shakeT = t / shakeEnd;  // 0-1 within shake phase
                float shakeFade = 1f - shakeT;  // Fade out shake as we progress
                
                float shakeX = (Mathf.PerlinNoise(Time.time * 50f, 0f) - 0.5f) * 2f * shakeIntensity * shakeFade;
                float shakeZ = (Mathf.PerlinNoise(0f, Time.time * 50f) - 0.5f) * 2f * shakeIntensity * shakeFade;
                
                currentPos.x += shakeX;
                currentPos.z += shakeZ;
            }
            
            // ═══════════════════════════════════════════════════════════
            // PHASE 2: TIP (tipStart - tipEnd)
            // ═══════════════════════════════════════════════════════════
            if (t >= tipStart && t <= 1f)
            {
                float tipT;
                if (t <= tipEnd)
                {
                    // Ramping up the tilt
                    tipT = (t - tipStart) / (tipEnd - tipStart);
                    tipT = EaseOutQuad(tipT);  // Smooth ease-out for natural feel
                }
                else
                {
                    // Hold the tilt
                    tipT = 1f;
                }
                
                currentEuler.x = startEuler.x + targetTiltX * tipT;
                currentEuler.z = startEuler.z + targetTiltZ * tipT;
            }
            
            // ═══════════════════════════════════════════════════════════
            // PHASE 3: SINK (sinkStart - 1.0) with Ease-In-Quad
            // ═══════════════════════════════════════════════════════════
            if (t >= sinkStart)
            {
                float sinkT = (t - sinkStart) / (1f - sinkStart);  // 0-1 within sink phase
                sinkT = EaseInQuad(sinkT);  // Accelerating descent - key for realism!
                
                currentPos.y = Mathf.Lerp(startPos.y, endPos.y, sinkT);
            }
            
            // ═══════════════════════════════════════════════════════════
            // PHASE 4: WOBBLE (wobbleStart - wobbleEnd)
            // ═══════════════════════════════════════════════════════════
            if (t >= wobbleStart && t <= wobbleEnd)
            {
                float wobbleT = (t - wobbleStart) / (wobbleEnd - wobbleStart);
                float wobbleFade = 1f - wobbleT;  // Fade out wobble as it sinks deeper
                
                float wobbleOffset = Mathf.Sin(elapsed * wobbleFrequency * Mathf.PI * 2f) * wobbleAmplitude * wobbleFade;
                currentPos.x += wobbleOffset;
            }
            
            // ═══════════════════════════════════════════════════════════
            // PHASE 5: FINAL SPIN (spinStart - 1.0)
            // ═══════════════════════════════════════════════════════════
            if (t >= spinStart)
            {
                float spinT = (t - spinStart) / (1f - spinStart);
                spinT = EaseInOutQuad(spinT);  // Smooth spin
                
                currentEuler.y = startEuler.y + targetSpinY * spinT;
            }
            
            // Apply the calculated transform
            obstacleVisual.transform.position = currentPos;
            obstacleVisual.transform.rotation = Quaternion.Euler(currentEuler);
            
            yield return null;
        }
        
        // Ensure final position
        obstacleVisual.transform.position = endPos;
        Debug.Log($"[ObstacleTrigger] Drowning sequence complete. Final position: {endPos}");
    }
    
    #region Easing Functions
    /// <summary>
    /// Ease-In Quadratic: Starts slow, accelerates. Perfect for sinking physics.
    /// </summary>
    private float EaseInQuad(float t)
    {
        return t * t;
    }
    
    /// <summary>
    /// Ease-Out Quadratic: Starts fast, decelerates. Good for initial tilt.
    /// </summary>
    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
    
    /// <summary>
    /// Ease-In-Out Quadratic: Smooth start and end. Good for spins.
    /// </summary>
    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
    #endregion
    
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
    
    /// <summary>
    /// Raises water from ground when Pharaoh obstacle is cleared.
    /// </summary>
    private IEnumerator WaterRiseCoroutine()
    {
        if (waterRiseObject == null)
        {
            Debug.LogWarning("[ObstacleTrigger] Water rise object is not assigned for Pharaoh obstacle");
            yield break;
        }
        
        Debug.Log($"[ObstacleTrigger] Starting water rise animation for {gameObject.name}");
        
        // Wait for delay before starting
        if (waterRiseDelay > 0f)
        {
            yield return new WaitForSeconds(waterRiseDelay);
        }
        
        // Activate water object if not already active
        waterRiseObject.SetActive(true);
        
        // Play water rise sound
        if (waterRiseSound != null)
        {
            AudioSource.PlayClipAtPoint(waterRiseSound, waterRiseObject.transform.position, waterRiseSoundVolume);
            Debug.Log("[ObstacleTrigger] Playing water rise sound");
        }
        
        // Store initial position
        Vector3 startPos = waterRiseObject.transform.position;
        Vector3 targetPos = startPos + Vector3.up * waterRiseHeight;
        
        Debug.Log($"[ObstacleTrigger] Water rising from {startPos.y} to {targetPos.y}");
        
        float elapsed = 0f;
        
        while (elapsed < waterRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / waterRiseDuration;
            
            // Smooth ease-out curve for natural water rise
            t = EaseOutQuad(t);
            
            // Interpolate position
            waterRiseObject.transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            yield return null;
        }
        
        // Ensure final position is exact
        waterRiseObject.transform.position = targetPos;
        Debug.Log($"[ObstacleTrigger] Water rise complete. Final height: {targetPos.y}");
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
    /// Activates all TreeLeaf objects one by one with a delay when Gate obstacle is cleared.
    /// First tries to use serialized treeLeafObjects array, then falls back to tag search.
    /// </summary>
    private IEnumerator ActivateTreeLeavesCoroutine()
    {
        treeLeavesActivationComplete = false;
        Debug.Log("[ObstacleTrigger] ActivateTreeLeavesCoroutine() called - searching for TreeLeaf objects...");
        
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
            Debug.Log($"[ObstacleTrigger] Activating {treeLeaves.Count} TreeLeaf object(s) one by one with {treeLeafActivationDelay}s delay");
            
            for (int i = 0; i < treeLeaves.Count; i++)
            {
                GameObject leaf = treeLeaves[i];
                if (leaf != null)
                {
                    Debug.Log($"[ObstacleTrigger] Activating TreeLeaf {i + 1}/{treeLeaves.Count}: {leaf.name}");
                    leaf.SetActive(true);
                    
                    // Wait before activating next leaf (except for the last one)
                    if (i < treeLeaves.Count - 1)
                    {
                        yield return new WaitForSeconds(treeLeafActivationDelay);
                    }
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
        
        treeLeavesActivationComplete = true;
        Debug.Log("[ObstacleTrigger] TreeLeaves activation complete");
    }
    
    /// <summary>
    /// Smoothly opens the door by rotating it to the target Y rotation.
    /// </summary>
    private IEnumerator OpenDoorCoroutine()
    {
        doorAnimationComplete = false;
        
        if (doorObject == null)
        {
            Debug.LogWarning("[ObstacleTrigger] Door object is null, cannot open door");
            doorAnimationComplete = true;
            yield break;
        }
        
        Vector3 startRotation = doorObject.transform.localEulerAngles;
        Vector3 targetRotation = new Vector3(startRotation.x, startRotation.y + doorOpenRotationY, startRotation.z);
        
        Debug.Log($"[ObstacleTrigger] Opening door from Y={startRotation.y}° to Y={doorOpenRotationY}° over {doorOpenDuration}s");
        
        // Play door opening sound
        if (doorOpenSound != null)
        {
            AudioSource.PlayClipAtPoint(doorOpenSound, doorObject.transform.position, doorSoundVolume);
            Debug.Log("[ObstacleTrigger] Playing door open sound");
        }
        
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
        
        // Mark animation as complete
        doorAnimationComplete = true;
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

