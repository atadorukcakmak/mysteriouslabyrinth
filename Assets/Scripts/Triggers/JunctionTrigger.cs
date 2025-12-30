using UnityEngine;

/// <summary>
/// Junction point where Ufyo appears to guide the player.
/// Activates compass button for path-revealing questions.
/// </summary>
[RequireComponent(typeof(Collider))]
public class JunctionTrigger : MonoBehaviour
{
    #region Serialized Fields
    [Header("Junction Settings")]
    [SerializeField] private int junctionIndex = 0; // Index for junction questions array
    [SerializeField] private bool isActive = true;
    
    [Header("Camera")]
    [SerializeField] private Camera triggerCamera; // Bu trigger için özel kamera
    [SerializeField] private float cameraTransitionDuration = 1.0f;
    
    [Header("Ufyo Appearance")]
    [SerializeField] private GameObject ufyoVisual; // Ufyo character/sprite at junction
    [SerializeField] private Transform ufyoSpawnPoint;
    [SerializeField] private bool showUfyoOnEnter = true;
    
    [Header("Environment Zones")]
    [SerializeField] private int correctPathZoneId = -1; // Zone to transform when correct
    [SerializeField] private GameObject[] pathRevealObjects; // Objects to activate on correct answer
    
    [Header("Question")]
    [SerializeField] private QuestionData customQuestion; // Override chapter question
    #endregion
    
    #region Properties
    public bool IsResolved { get; private set; }
    public bool PlayerInJunction { get; private set; }
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool compassUsed;
    private bool isInSequence; // Kamera geçişi veya diyalog sırasında true
    #endregion
    
    #region Initialization
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        // Hide Ufyo initially
        if (ufyoVisual != null)
        {
            ufyoVisual.SetActive(false);
        }
        
        // Hide path reveal objects
        HidePathRevealObjects();
    }
    
    private void Start()
    {
        // Subscribe to compass button
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnCompassClicked += OnCompassClicked;
        }
    }
    
    private void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnCompassClicked -= OnCompassClicked;
        }
    }
    #endregion
    
    #region Trigger Detection
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || IsResolved) return;
        
        if (other.CompareTag("Player"))
        {
            PlayerInJunction = true;
            OnPlayerEnterJunction();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Eğer sequence içindeysek (kamera geçişi, diyalog vs.) çıkışı yoksay
            if (isInSequence)
            {
                Debug.Log("[JunctionTrigger] Player exited trigger but sequence is active, ignoring");
                return;
            }
            
            PlayerInJunction = false;
            OnPlayerExitJunction();
        }
    }
    #endregion
    
    #region Junction Logic
    private void OnPlayerEnterJunction()
    {
        Debug.Log($"[JunctionTrigger] Player entered junction {junctionIndex}");
        
        // Kamera geçişi ile başla
        StartCoroutine(JunctionEnterSequence());
    }
    
    private System.Collections.IEnumerator JunctionEnterSequence()
    {
        isInSequence = true;
        
        // Kamera geçişi
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log($"[JunctionTrigger] Transitioning to junction camera...");
            bool cameraTransitionComplete = false;
            CameraManager.Instance.TransitionToCamera(triggerCamera, cameraTransitionDuration, () =>
            {
                cameraTransitionComplete = true;
            });
            yield return new UnityEngine.WaitUntil(() => cameraTransitionComplete);
            Debug.Log($"[JunctionTrigger] Camera transition complete");
        }
        
        // Show Ufyo
        if (showUfyoOnEnter && ufyoVisual != null)
        {
            ShowUfyo();
        }

        string junctionDialogue = customQuestion != null ? customQuestion.approachDialogue : null;

        // Show Ufyo dialogue, then enable compass on Continue (stay in UI mode)
        if (!string.IsNullOrEmpty(junctionDialogue) && UIManager.Instance != null)
        {
            Debug.Log($"[JunctionTrigger] Showing Ufyo dialogue: {junctionDialogue}");
            // UI mode'da kal - oyuncu hareket edemez, cursor açık
            UIManager.Instance.ShowDialogueWithCallbackStayInUI(junctionDialogue, () =>
            {
                // Continue'a basılınca compass butonunu aktif et (hala UI mode'da)
                Debug.Log("[JunctionTrigger] Dialogue complete, enabling compass button (staying in UI mode)");
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetCompassEnabled(true);
                }
            });
        }
        else
        {
            // Diyalog yoksa UI mode'a geç ve compass'ı aktif et
            Debug.Log("[JunctionTrigger] No dialogue, entering UI mode and enabling compass");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Dialogue);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetCompassEnabled(true);
            }
        }
        
        // NOT: isInSequence burada false yapmıyoruz çünkü compass tıklanana kadar devam ediyor
    }
    
    private void OnPlayerExitJunction()
    {
        Debug.Log($"[JunctionTrigger] Player exited junction {junctionIndex}");
        
        // Hide Ufyo if not resolved
        if (!IsResolved && ufyoVisual != null)
        {
            HideUfyo();
        }
        
        // Disable compass button if not used
        if (!compassUsed && UIManager.Instance != null)
        {
            UIManager.Instance.SetCompassEnabled(false);
        }
    }
    #endregion
    
    #region Compass Logic
    private void OnCompassClicked()
    {
        Debug.Log($"[JunctionTrigger] Compass clicked! PlayerInJunction={PlayerInJunction}, IsResolved={IsResolved}, compassUsed={compassUsed}");
        
        // Only respond if player is in this junction
        if (!PlayerInJunction)
        {
            Debug.LogWarning("[JunctionTrigger] Compass ignored - player not in junction");
            return;
        }
        if (IsResolved)
        {
            Debug.LogWarning("[JunctionTrigger] Compass ignored - already resolved");
            return;
        }
        if (compassUsed)
        {
            Debug.LogWarning("[JunctionTrigger] Compass ignored - already used");
            return;
        }
        
        Debug.Log($"[JunctionTrigger] Compass accepted at junction {junctionIndex}");
        
        compassUsed = true;
        
        // Disable compass button
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetCompassEnabled(false);
        }
        
        // Ask junction question
        AskJunctionQuestion();
    }
    
    private void AskJunctionQuestion()
    {
        QuestionData question = GetQuestion();
        
        if (question != null)
        {
            Debug.Log($"[JunctionTrigger] Asking question: {question.questionText}");
            QuestionManager.Instance?.AskQuestion(question, OnQuestionAnswered, this);
        }
        else
        {
            Debug.LogWarning($"[JunctionTrigger] No question available for junction {junctionIndex}. Resolving automatically.");
            // Soru yok - direkt çöz
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetCompassEnabled(false);
            }
            
            OnJunctionResolved();
        }
    }
    
    private QuestionData GetQuestion()
    {
        // Use custom question if assigned
        if (customQuestion != null)
        {
            return customQuestion;
        }
        
        // Get from chapter data
        ChapterData chapter = GameManager.Instance?.CurrentChapterData;
        if (chapter?.junctionQuestions != null && 
            junctionIndex >= 0 && 
            junctionIndex < chapter.junctionQuestions.Length)
        {
            return chapter.junctionQuestions[junctionIndex];
        }
        
        return null;
    }
    
    private void OnQuestionAnswered(bool isCorrect)
    {
        if (isCorrect)
        {
            // Doğru cevap - başarı diyaloğunu göster
            OnJunctionResolved();
        }
        else
        {
            // Wrong answer - can try again later
            compassUsed = false; // Allow retry
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetCompassEnabled(true);
            }
        }
    }
    #endregion
    
    #region Junction Resolution
    private void OnJunctionResolved()
    {
        IsResolved = true;
        
        Debug.Log($"[JunctionTrigger] Junction {junctionIndex} resolved");
        
        // Delay to let question panel close
        StartCoroutine(ResolutionSequence());
    }
    
    private System.Collections.IEnumerator ResolutionSequence()
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
            yield return new UnityEngine.WaitUntil(() => dialogueClosed);
        }
        
        // Başarı diyaloğu Continue'a basıldıktan sonra
        Debug.Log("Bismillahirahmanirahim");
        
        // Transform environment zone if specified
        if (EnvironmentManager.Instance != null && correctPathZoneId >= 0)
        {
            EnvironmentManager.Instance.TransformZone(correctPathZoneId);
        }
        
        // Activate path reveal objects (vegetation, markers, etc.)
        ShowPathRevealObjects();
        
        // Keep Ufyo visible briefly then hide
        if (ufyoVisual != null)
        {
            Invoke(nameof(HideUfyo), 3f);
        }
        
        // Disable this junction
        isActive = false;
        
        // Oyuncu kamerasına geri dön
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log("[JunctionTrigger] Returning to player camera...");
            bool cameraReturnComplete = false;
            CameraManager.Instance.TransitionToPlayerCamera(cameraTransitionDuration, () =>
            {
                cameraReturnComplete = true;
            });
            yield return new UnityEngine.WaitUntil(() => cameraReturnComplete);
            Debug.Log("[JunctionTrigger] Returned to player camera");
        }
        
        // Oyun moduna dön - oyuncu artık hareket edebilir
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReturnToGameMode();
        }
        
        // Sequence bitti
        isInSequence = false;
    }
    
    private void ShowPathRevealObjects()
    {
        if (pathRevealObjects == null) return;
        
        foreach (var obj in pathRevealObjects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }
    
    private void HidePathRevealObjects()
    {
        if (pathRevealObjects == null) return;
        
        foreach (var obj in pathRevealObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
    #endregion
    
    #region Ufyo
    private void ShowUfyo()
    {
        if (ufyoVisual == null) return;
        
        // Position Ufyo at spawn point if available
        if (ufyoSpawnPoint != null)
        {
            ufyoVisual.transform.position = ufyoSpawnPoint.position;
            ufyoVisual.transform.rotation = ufyoSpawnPoint.rotation;
        }
        
        ufyoVisual.SetActive(true);
        
        // Face player
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 lookDir = player.transform.position - ufyoVisual.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                ufyoVisual.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }
    
    private void HideUfyo()
    {
        if (ufyoVisual != null)
        {
            ufyoVisual.SetActive(false);
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Manually resolve this junction (for testing).
    /// </summary>
    public void ForceResolve()
    {
        if (!IsResolved)
        {
            OnJunctionResolved();
        }
    }
    
    /// <summary>
    /// Reset the junction to initial state.
    /// </summary>
    public void Reset()
    {
        IsResolved = false;
        compassUsed = false;
        isActive = true;
        PlayerInJunction = false;
        isInSequence = false;
        
        HideUfyo();
        HidePathRevealObjects();
    }
    
    #endregion
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        // Draw junction area
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(5f, 2f, 5f));
        
        // Draw Ufyo spawn point
        if (ufyoSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(ufyoSpawnPoint.position, 0.5f);
        }
    }
    #endregion
}

