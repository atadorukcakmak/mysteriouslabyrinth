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
    
    [Header("Ufyo Appearance")]
    [SerializeField] private GameObject ufyoVisual; // Ufyo character/sprite at junction
    [SerializeField] private Transform ufyoSpawnPoint;
    [SerializeField] private bool showUfyoOnEnter = true;
    
    [Header("Paths")]
    [SerializeField] private Transform correctPath; // Reference to correct path direction
    [SerializeField] private Transform[] wrongPaths; // References to dead-end paths
    
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
            PlayerInJunction = false;
            OnPlayerExitJunction();
        }
    }
    #endregion
    
    #region Junction Logic
    private void OnPlayerEnterJunction()
    {
        Debug.Log($"[JunctionTrigger] Player entered junction {junctionIndex}");
        
        // Show Ufyo
        if (showUfyoOnEnter && ufyoVisual != null)
        {
            ShowUfyo();
        }

        string junctionDialogue = customQuestion.approachDialogue;

        // Show junction dialogue, then enable compass
        if (!string.IsNullOrEmpty(junctionDialogue) && UIManager.Instance != null)
        {
            Debug.Log($"[JunctionTrigger] Showing dialogue: {junctionDialogue}");
            UIManager.Instance.ShowDialogueWithCallback(junctionDialogue, () =>
            {
                // Diyalog bittikten sonra pusula butonunu aktif et
                Debug.Log("[JunctionTrigger] Dialogue complete, enabling compass");
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetCompassEnabled(true);
                }
            });
        }
        else
        {
            // Diyalog yoksa direkt soruyu sor (compass'a gerek yok)
            Debug.Log("[JunctionTrigger] No dialogue, asking question directly");
            AskJunctionQuestion();
        }
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
        // Only respond if player is in this junction
        if (!PlayerInJunction || IsResolved || compassUsed) return;
        
        Debug.Log($"[JunctionTrigger] Compass clicked at junction {junctionIndex}");
        
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
            Debug.LogWarning($"[JunctionTrigger] No question available for junction {junctionIndex}. Revealing path automatically.");
            // Soru yok - direkt yolu göster
            // Önce compass'ı kapat ve normal duruma geç
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetCompassEnabled(false);
            }
            
            // Reveal path
            RevealCorrectPath();
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
            RevealCorrectPath();
        }
        else
        {
            // Wrong answer - can try again later or proceed with risk
            compassUsed = false; // Allow retry
            
            if (UIManager.Instance != null)
            {
               // UIManager.Instance.ShowDialogue(wrongPathDialogue);
                UIManager.Instance.SetCompassEnabled(true);
            }
        }
    }
    #endregion
    
    #region Path Reveal
    private void RevealCorrectPath()
    {
        IsResolved = true;
        
        Debug.Log($"[JunctionTrigger] Revealing correct path at junction {junctionIndex}");
        
        // Delay dialogue to let question panel close
        StartCoroutine(RevealPathSequence());
    }
    
    private System.Collections.IEnumerator RevealPathSequence()
    {
        // Wait for question panel to close
        yield return new UnityEngine.WaitForSeconds(0.5f);

        string correctPathDialogue = customQuestion.successDialogue;

        // Show correct path dialogue and wait for Continue
        if (!string.IsNullOrEmpty(correctPathDialogue) && UIManager.Instance != null)
        {
            bool dialogueClosed = false;
            UIManager.Instance.ShowDialogueWithCallback(correctPathDialogue, () =>
            {
                dialogueClosed = true;
            });
            
            // Wait until user presses Continue
            yield return new UnityEngine.WaitUntil(() => dialogueClosed);
        }
        
        // Transform environment to show correct path
        if (EnvironmentManager.Instance != null)
        {
            if (correctPathZoneId >= 0)
            {
                EnvironmentManager.Instance.TransformZone(correctPathZoneId);
            }
            else if (correctPath != null)
            {
                EnvironmentManager.Instance.TransformNearbyZones(correctPath.position, 5f);
            }
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
            RevealCorrectPath();
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
        
        HideUfyo();
        HidePathRevealObjects();
    }
    
    /// <summary>
    /// Gets the direction to the correct path.
    /// </summary>
    public Vector3 GetCorrectPathDirection()
    {
        if (correctPath != null)
        {
            return (correctPath.position - transform.position).normalized;
        }
        return Vector3.forward;
    }
    #endregion
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        // Draw junction area
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(5f, 2f, 5f));
        
        // Draw correct path direction
        if (correctPath != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, correctPath.position);
            Gizmos.DrawSphere(correctPath.position, 0.5f);
        }
        
        // Draw wrong path directions
        if (wrongPaths != null)
        {
            Gizmos.color = Color.red;
            foreach (var wrongPath in wrongPaths)
            {
                if (wrongPath != null)
                {
                    Gizmos.DrawLine(transform.position, wrongPath.position);
                    Gizmos.DrawSphere(wrongPath.position, 0.3f);
                }
            }
        }
        
        // Draw Ufyo spawn point
        if (ufyoSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(ufyoSpawnPoint.position, 0.5f);
        }
    }
    #endregion
}

