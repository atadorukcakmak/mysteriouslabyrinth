using System.Collections;
using UnityEngine;

/// <summary>
/// Secret chest containing a holy book.
/// Requires answering a code question to unlock.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChestTrigger : MonoBehaviour, IInteractable
{
    #region Serialized Fields
    [Header("Chest Settings")]
    [SerializeField] private bool isLocked = true;
    [SerializeField] private bool requireInteraction = true; // Must press E to interact
    
    [Header("Book Reward")]
    [SerializeField] private BookData bookReward;
    [SerializeField] private int bookOrderOverride = -1; // Use chapter data if -1
    
    [Header("Question")]
    [SerializeField] private QuestionData customQuestion;
    
    [Header("Visuals")]
    [SerializeField] private GameObject chestClosed;
    [SerializeField] private GameObject chestOpen;
    [SerializeField] private GameObject bookVisual; // Book floating above open chest
    [SerializeField] private Transform bookSpawnPoint;
    
    [Header("Animation")]
    [SerializeField] private float openDuration = 1f;
    [SerializeField] private float bookRiseDuration = 1.5f;
    [SerializeField] private float bookRiseHeight = 1.5f;
    
    [Header("Dialogue")]
    [SerializeField] private string alreadyOpenDialogue = "You've already collected the book from this chest.";
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem unlockParticles;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private Light glowLight;
    #endregion
    
    #region Properties
    public bool IsOpened { get; private set; }
    public bool IsUnlocking { get; private set; }
    public BookData ContainedBook => bookReward;
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool playerInRange;
    private AudioSource audioSource;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && unlockSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initial state
        SetChestVisuals(false);
    }
    
    private void Start()
    {
        // Get book reward from chapter data if not assigned
        if (bookReward == null && GameManager.Instance?.CurrentChapterData?.bookReward != null)
        {
            bookReward = GameManager.Instance.CurrentChapterData.bookReward;
        }
    }
    
    private void SetChestVisuals(bool opened)
    {
        if (chestClosed != null) chestClosed.SetActive(!opened);
        if (chestOpen != null) chestOpen.SetActive(opened);
        if (bookVisual != null) bookVisual.SetActive(false);
        if (glowLight != null) glowLight.enabled = opened;
    }
    #endregion
    
    #region Trigger Detection
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            
            if (IsOpened)
            {
                ShowDialogue(alreadyOpenDialogue);
            }
            else
            {
                // Her zaman otomatik tetikle (E tuşu sorunlu olabilir)
                TryUnlock();
            }
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
        // Show interaction prompt
        Debug.Log($"[ChestTrigger] Looking at chest: {gameObject.name}");
    }
    
    public void OnLookAway()
    {
        // Hide interaction prompt
    }
    
    public void Interact(GameObject interactor)
    {
        if (!playerInRange) return;
        
        if (IsOpened)
        {
            ShowDialogue(alreadyOpenDialogue);
            return;
        }
        
        if (IsUnlocking) return;
        
        TryUnlock();
    }
    #endregion
    
    #region Unlock Logic
    private void TryUnlock()
    {
        if (IsOpened || IsUnlocking) return;
        
        if (isLocked)
        {
            string lockedDialogue = customQuestion.approachDialogue;
            // Show locked dialogue, then ask question when dialogue finishes
            if (!string.IsNullOrEmpty(lockedDialogue) && UIManager.Instance != null)
            {
                UIManager.Instance.ShowDialogueWithCallback(lockedDialogue, () =>
                {
                    AskChestQuestion();
                });
            }
            else
            {
                AskChestQuestion();
            }
        }
        else
        {
            // Not locked - open directly
            OpenChest();
        }
    }
    
    private void AskChestQuestion()
    {
        IsUnlocking = true;
        
        QuestionData question = GetQuestion();
        
        if (question != null)
        {
            Debug.Log($"[ChestTrigger] Showing question: {question.questionText}");
            QuestionManager.Instance?.AskQuestion(question, OnQuestionAnswered, this);
        }
        else
        {
            Debug.LogWarning($"[ChestTrigger] No question available for chest: {gameObject.name}. Opening directly.");
            // Soru yok - oyunu düzgün duruma döndür
            isLocked = false;
            IsUnlocking = false;
            
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
            
            // Sandığı aç
            OpenChest();
        }
    }
    
    private QuestionData GetQuestion()
    {
        if (customQuestion != null)
        {
            return customQuestion;
        }
        
        return GameManager.Instance?.CurrentChapterData?.chestQuestion;
    }
    
    private void OnQuestionAnswered(bool isCorrect)
    {
        IsUnlocking = false;
        
        if (isCorrect)
        {
            isLocked = false;
            OpenChest();
        }
        else
        {
            // Wrong answer - can try again
            Debug.Log($"[ChestTrigger] Wrong answer for chest: {gameObject.name}");
        }
    }
    #endregion
    
    #region Open Chest
    private void OpenChest()
    {
        if (IsOpened) return;
        
        IsOpened = true;
        
        Debug.Log($"[ChestTrigger] Opening chest: {gameObject.name}");
        
        StartCoroutine(OpenChestSequence());
    }
    
    private IEnumerator OpenChestSequence()
    {
        // Wait for question panel to fully close
        yield return new WaitForSeconds(0.5f);
        
        // Play unlock sound
        if (audioSource != null && unlockSound != null)
        {
            audioSource.PlayOneShot(unlockSound);
        }
        
        // Play particles
        if (unlockParticles != null)
        {
            unlockParticles.Play();
        }
        
        // Animate chest opening
        yield return StartCoroutine(AnimateChestOpen());
        
        // Show book rising
        yield return StartCoroutine(AnimateBookRise());
        
        // Award book to player
        AwardBook();
        string unlockDialogue = customQuestion.successDialogue;
        // Show dialogue and wait for Continue
        if (!string.IsNullOrEmpty(unlockDialogue) && UIManager.Instance != null)
        {
            bool dialogueClosed = false;
            UIManager.Instance.ShowDialogueWithCallback(unlockDialogue, () =>
            {
                dialogueClosed = true;
            });
            
            // Wait until user presses Continue
            yield return new WaitUntil(() => dialogueClosed);
        }
        
        // Check for chapter completion
        CheckChapterCompletion();
    }
    
    private IEnumerator AnimateChestOpen()
    {
        float elapsed = 0f;
        
        // Simple cross-fade between closed and open states
        if (chestClosed != null && chestOpen != null)
        {
            chestOpen.SetActive(true);
            chestOpen.transform.localScale = Vector3.zero;
            
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / openDuration;
                
                // Scale up open chest
                chestOpen.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                
                // Scale down closed chest
                chestClosed.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                
                yield return null;
            }
            
            chestClosed.SetActive(false);
            chestOpen.transform.localScale = Vector3.one;
        }
        else
        {
            // Fallback - just swap
            SetChestVisuals(true);
            yield return new WaitForSeconds(openDuration);
        }
        
        // Enable glow
        if (glowLight != null)
        {
            glowLight.enabled = true;
        }
    }
    
    private IEnumerator AnimateBookRise()
    {
        if (bookVisual == null) yield break;
        
        // Position book at spawn point or chest center
        Vector3 startPos = bookSpawnPoint != null ? bookSpawnPoint.position : transform.position;
        Vector3 endPos = startPos + Vector3.up * bookRiseHeight;
        
        bookVisual.transform.position = startPos;
        bookVisual.SetActive(true);
        
        float elapsed = 0f;
        
        while (elapsed < bookRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bookRiseDuration;
            
            // Ease out
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            
            // Rise up
            bookVisual.transform.position = Vector3.Lerp(startPos, endPos, easedT);
            
            // Rotate
            bookVisual.transform.Rotate(Vector3.up, 180f * Time.deltaTime);
            
            yield return null;
        }
        
        // Continue floating and rotating
        StartCoroutine(FloatAndRotate());
    }
    
    private IEnumerator FloatAndRotate()
    {
        if (bookVisual == null) yield break;
        
        Vector3 basePos = bookVisual.transform.position;
        float floatAmplitude = 0.2f;
        float floatSpeed = 2f;
        float rotateSpeed = 45f;
        
        while (bookVisual.activeSelf)
        {
            // Float up and down
            float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            bookVisual.transform.position = basePos + Vector3.up * yOffset;
            
            // Rotate
            bookVisual.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
            
            yield return null;
        }
    }
    #endregion
    
    #region Book Award
    private void AwardBook()
    {
        if (bookReward == null)
        {
            Debug.LogWarning($"[ChestTrigger] No book to award from chest: {gameObject.name}");
            return;
        }
        
        // Find player inventory
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory != null)
        {
            inventory.CollectBook(bookReward);
            Debug.Log($"[ChestTrigger] Awarded book: {bookReward.bookName}");
        }
        else
        {
            Debug.LogError("[ChestTrigger] Could not find player Inventory!");
        }
    }
    
    private void CheckChapterCompletion()
    {
        // Check if this was the last book needed for the chapter
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;
        
        // For now, completing a chest triggers chapter completion
        // In a full game, you might check other conditions
        if (GameManager.Instance != null)
        {
            // Small delay before chapter completion
            StartCoroutine(CompleteChapterDelayed(3f));
        }
    }
    
    private IEnumerator CompleteChapterDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CompleteChapter();
        }
    }
    #endregion
    
    #region Utility
    private void ShowDialogue(string text)
    {
        if (UIManager.Instance != null && !string.IsNullOrEmpty(text))
        {
            UIManager.Instance.ShowDialogue(text);
        }
    }
    
    /// <summary>
    /// Force open the chest (for testing).
    /// </summary>
    public void ForceOpen()
    {
        isLocked = false;
        OpenChest();
    }
    
    /// <summary>
    /// Reset the chest to locked state.
    /// </summary>
    public void Reset()
    {
        IsOpened = false;
        IsUnlocking = false;
        isLocked = true;
        SetChestVisuals(false);
        
        if (chestClosed != null) chestClosed.transform.localScale = Vector3.one;
    }
    #endregion
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = IsOpened ? Color.green : (isLocked ? Color.red : Color.yellow);
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 1.5f, 2f));
        
        if (bookSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(bookSpawnPoint.position, 0.3f);
        }
    }
    #endregion
}

