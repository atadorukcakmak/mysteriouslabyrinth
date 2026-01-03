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
    
    [Header("Camera")]
    [SerializeField] private Camera triggerCamera; // Bu trigger için özel kamera
    [SerializeField] private float cameraTransitionDuration = 1.0f;
    
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
    [SerializeField] private ChestAnimation chestAnimation; // Animator tabanlı animasyon
    [SerializeField] private bool useAnimator = true; // Animator mı yoksa kod animasyonu mu?
    [SerializeField] private float openDuration = 1f;
    [SerializeField] private float bookRiseDuration = 1.5f;
    [SerializeField] private float bookRiseHeight = 1.5f;
    
    [Header("Book Fly To UI")]
    [SerializeField] private float bookFlyDuration = 1.0f; // Kitabın UI'a uçma süresi
    [SerializeField] private float bookShrinkScale = 0.1f; // Kitabın küçülme oranı
    [SerializeField] private RectTransform bookSlotsContainer; // UI'daki book slots container
    
    [Header("Timing")]
    [Tooltip("Sandık açıldıktan sonra kitabın yükselmeye başlaması için bekleme süresi")]
    [SerializeField] private float delayBeforeBookRise = 0.5f;
    
    [Header("Dialogue")]
    [SerializeField] private string alreadyOpenDialogue = "You've already collected the book from this chest.";
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem unlockParticles;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private Light glowLight;
    
    [Header("Chest Audio")]
    [Tooltip("Ambient sound that plays when player is nearby (stops when chest opens)")]
    [SerializeField] private AudioClip chestAmbientSound;
    [Tooltip("Sound when chest opens")]
    [SerializeField] private AudioClip chestOpenSound;
    [Tooltip("Maximum distance to hear ambient sound")]
    [SerializeField] private float ambientSoundMaxDistance = 10f;
    [Tooltip("Volume of ambient sound")]
    [Range(0f, 1f)]
    [SerializeField] private float ambientSoundVolume = 0.5f;
    
    [Header("Book Charging Effect")]
    [Tooltip("Charging/explosion effect prefab that follows the book")]
    [SerializeField] private GameObject chargingPopPrefab;
    [Tooltip("Duration the charging effect follows the book (in seconds)")]
    [SerializeField] private float chargingPopDuration = 2f;
    [Tooltip("Scale of the charging effect")]
    [SerializeField] private float chargingPopScale = 1f;
    [Tooltip("Height offset above the book for the charging effect")]
    [SerializeField] private float chargingPopHeightOffset = 0.5f;
    #endregion
    
    #region Properties
    public bool IsOpened { get; private set; }
    public bool IsUnlocking { get; private set; }
    public BookData ContainedBook => bookReward;
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool playerInRange;
    private GameObject activeChargingEffect;
    private AudioSource ambientAudioSource;
    private Transform playerTransform;
    private bool ambientSoundPlaying;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        // Setup ambient audio source for 3D spatial sound
        SetupAmbientAudio();
        
        // ChestAnimation'ı otomatik bul (atanmamışsa)
        if (chestAnimation == null)
        {
            chestAnimation = GetComponentInChildren<ChestAnimation>();
        }
        
        // Debug: Referansları kontrol et
        Debug.Log($"[ChestTrigger] Awake - Checking references for {gameObject.name}:");
        Debug.Log($"  - chestAnimation: {(chestAnimation != null ? chestAnimation.gameObject.name : "NULL")}");
        Debug.Log($"  - bookVisual: {(bookVisual != null ? bookVisual.name : "NULL")}");
        Debug.Log($"  - bookSpawnPoint: {(bookSpawnPoint != null ? bookSpawnPoint.name : "NULL")}");
        Debug.Log($"  - useAnimator: {useAnimator}");
        
        if (bookVisual == null)
        {
            Debug.LogWarning($"[ChestTrigger] WARNING: bookVisual is not assigned in {gameObject.name}! Assign it in Inspector.");
        }
        
        // Initial state
        SetChestVisuals(false);
    }
    
    private void SetupAmbientAudio()
    {
        // Create ambient audio source for 3D spatial sound
        ambientAudioSource = gameObject.AddComponent<AudioSource>();
        ambientAudioSource.clip = chestAmbientSound;
        ambientAudioSource.loop = true;
        ambientAudioSource.playOnAwake = false;
        ambientAudioSource.spatialBlend = 1f; // Full 3D
        ambientAudioSource.rolloffMode = AudioRolloffMode.Linear;
        ambientAudioSource.minDistance = 1f;
        ambientAudioSource.maxDistance = ambientSoundMaxDistance;
        ambientAudioSource.volume = ambientSoundVolume;
        
        // Start playing ambient if assigned and chest not opened
        if (chestAmbientSound != null && !IsOpened)
        {
            ambientAudioSource.Play();
            ambientSoundPlaying = true;
            Debug.Log($"[ChestTrigger] Started ambient sound for {gameObject.name}");
        }
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
        
        // Önce kameraya geçiş yap, sonra diyalog/soru göster
        StartCoroutine(TryUnlockSequence());
    }
    
    private IEnumerator TryUnlockSequence()
    {
        // Kamera geçişi
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log("[ChestTrigger] Transitioning to chest camera...");
            bool cameraTransitionComplete = false;
            CameraManager.Instance.TransitionToCamera(triggerCamera, cameraTransitionDuration, () =>
            {
                cameraTransitionComplete = true;
            });
            yield return new WaitUntil(() => cameraTransitionComplete);
            Debug.Log("[ChestTrigger] Camera transition complete");
        }
        
        if (isLocked)
        {
            // Get approach dialogue messages (supports two-part dialogue)
            string[] lockedMessages = customQuestion != null ? customQuestion.GetApproachDialogueMessages() : null;
            
            // Show locked dialogue, then ask question when dialogue finishes
            if (lockedMessages != null && lockedMessages.Length > 0 && UIManager.Instance != null)
            {
                bool dialogueComplete = false;
                UIManager.Instance.ShowDialogueSequence(lockedMessages, () =>
                {
                    dialogueComplete = true;
                });
                yield return new WaitUntil(() => dialogueComplete);
                
                AskChestQuestion();
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
        // Bir frame bekle - soru paneli kapansın
        yield return null;
        
        // 1. Play unlock sound
        if (unlockSound != null)
        {
            AudioSource.PlayClipAtPoint(unlockSound, transform.position, 1f);
        }
        
        // 2. Play particles
        if (unlockParticles != null)
        {
            unlockParticles.Play();
        }
        
        // 3. Stop ambient sound and play chest open sound
        StopAmbientSound();
        PlayChestOpenSound();
        
        // 4. Chest açılma animasyonu
        Debug.Log("[ChestTrigger] Step 1: Playing chest open animation");
        if (useAnimator && chestAnimation != null)
        {
            bool animationComplete = false;
            chestAnimation.PlayOpenAnimation(() => animationComplete = true);
            yield return new WaitUntil(() => animationComplete);
        }
        else
        {
            yield return StartCoroutine(AnimateChestOpen());
        }
        
        // 4. Sandık tamamen açılana kadar bekle
        if (delayBeforeBookRise > 0)
        {
            Debug.Log($"[ChestTrigger] Waiting {delayBeforeBookRise}s for chest to fully open...");
            yield return new WaitForSeconds(delayBeforeBookRise);
        }
        
        // 5. Kitap dönerek yukarı çıkıyor
        Debug.Log("[ChestTrigger] Step 2: Book rising from chest");
        
        // AnimateBookRise içinde charging efekti spawn edilecek (kitap aktif olduktan sonra)
        yield return StartCoroutine(AnimateBookRise());
        
        // 5. Kitap UI'daki book slot'a uçuyor
        Debug.Log("[ChestTrigger] Step 3: Book flying to UI");
        yield return StartCoroutine(AnimateBookFlyToUI());
        
        // 6. Kitabı envantere ekle
        AwardBook();
        
        // 7. Success diyaloğunu göster
        Debug.Log("[ChestTrigger] Step 4: Showing success dialogue");
        // Get success dialogue messages (supports two-part dialogue)
        string[] successMessages = customQuestion != null ? customQuestion.GetSuccessDialogueMessages() : null;
        
        if (successMessages != null && successMessages.Length > 0 && UIManager.Instance != null)
        {
            bool dialogueClosed = false;
            UIManager.Instance.ShowDialogueSequence(successMessages, () =>
            {
                dialogueClosed = true;
            });
            
            yield return new WaitUntil(() => dialogueClosed);
        }
        
        // 8. Continue'a basıldı - debug log
        Debug.Log("elhamdulillah");
        
        // 9. Oyuncu kamerasına geri dön
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log("[ChestTrigger] Returning to player camera...");
            bool cameraReturnComplete = false;
            CameraManager.Instance.TransitionToPlayerCamera(cameraTransitionDuration, () =>
            {
                cameraReturnComplete = true;
            });
            yield return new WaitUntil(() => cameraReturnComplete);
            Debug.Log("[ChestTrigger] Returned to player camera");
        }
        
        // 10. Oyun moduna dön
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReturnToGameMode();
        }
        
        // 11. Bölüm tamamlama kontrolü
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
        // Null kontrolü ve debug
        if (bookVisual == null)
        {
            Debug.LogError("[ChestTrigger] ERROR: bookVisual is NULL! Assign it in Inspector.");
            yield break;
        }
        
        Debug.Log($"[ChestTrigger] BookVisual found: {bookVisual.name}, activeSelf: {bookVisual.activeSelf}");
        
        // Kitabın renderer'larını kontrol et
        Renderer[] renderers = bookVisual.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"[ChestTrigger] Book has {renderers.Length} renderers");
        foreach (var rend in renderers)
        {
            Debug.Log($"[ChestTrigger]   - Renderer: {rend.gameObject.name}, enabled: {rend.enabled}, material: {(rend.material != null ? rend.material.name : "NULL")}");
        }
        
        // Position book at spawn point or chest center
        Vector3 startPos = bookSpawnPoint != null ? bookSpawnPoint.position : transform.position;
        Vector3 endPos = startPos + Vector3.up * bookRiseHeight;
        
        Debug.Log($"[ChestTrigger] Book will rise from {startPos} to {endPos}");
        Debug.Log($"[ChestTrigger] Camera position: {(Camera.main != null ? Camera.main.transform.position.ToString() : "NO CAMERA")}");
        
        // Kitabı pozisyonla ve aktif et
        bookVisual.transform.position = startPos;
        //bookVisual.transform.localScale = Vector3.one;
        bookVisual.SetActive(true);
        
        // Tüm child'ları da aktif et
        foreach (Transform child in bookVisual.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.SetActive(true);
        }
        
        // Renderer'ları aktif et
        foreach (var rend in renderers)
        {
            rend.enabled = true;
        }
        
        Debug.Log($"[ChestTrigger] Book activated at position: {bookVisual.transform.position}, scale: {bookVisual.transform.localScale}");
        
        // Kitap aktif oldu, şimdi charging efektini spawn et
        SpawnChargingEffect();
        
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
        
        // Kitap yukarıda bekliyor - artık UI'a uçacak
        Debug.Log($"[ChestTrigger] Book rise complete at position: {bookVisual.transform.position}");
    }
    
    private IEnumerator AnimateBookFlyToUI()
    {
        if (bookVisual == null) yield break;
        
        // UI hedef pozisyonunu bul
        RectTransform targetSlot = null;
        
        // Önce Inspector'dan atanan container'ı dene
        if (bookSlotsContainer != null)
        {
            targetSlot = bookSlotsContainer;
        }
        // Sonra UIManager'dan dinamik olarak al
        else if (UIManager.Instance != null)
        {
            targetSlot = UIManager.Instance.GetNextEmptyBookSlot();
        }
        
        // Ana kamerayı al
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[ChestTrigger] Main camera not found!");
            bookVisual.SetActive(false);
            yield break;
        }
        
        // Hedef ekran pozisyonunu hesapla
        Vector3 targetScreenPos;
        if (targetSlot != null)
        {
            // UI element'in ekran pozisyonunu al
            targetScreenPos = RectTransformUtility.WorldToScreenPoint(null, targetSlot.position);
        }
        else
        {
            // Fallback: Ekranın sağ üst köşesi
            targetScreenPos = new Vector3(Screen.width - 100f, Screen.height - 100f, 0f);
        }
        
        // Kitabın başlangıç değerleri
        Vector3 startWorldPos = bookVisual.transform.position;
        Vector3 startScale = bookVisual.transform.localScale;
        Vector3 endScale = startScale * bookShrinkScale;
        
        float elapsed = 0f;
        
        while (elapsed < bookFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bookFlyDuration;
            
            // Ease in-out (smooth)
            float easedT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            
            // Hedef world pozisyonunu hesapla
            // Kitabı kameraya doğru çekiyoruz ama çok yakın değil
            float distanceFromCamera = Mathf.Lerp(
                Vector3.Distance(startWorldPos, mainCamera.transform.position),
                2f, // Son mesafe
                easedT
            );
            
            Vector3 directionToTarget = (mainCamera.ScreenToWorldPoint(new Vector3(
                targetScreenPos.x,
                targetScreenPos.y,
                distanceFromCamera
            )) - startWorldPos).normalized;
            
            Vector3 targetWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(
                targetScreenPos.x,
                targetScreenPos.y,
                distanceFromCamera
            ));
            
            // Pozisyonu lerp et (eğrisel hareket için bezier kullanabiliriz)
            // Basit versiyon: düz lerp
            bookVisual.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, easedT);
            
            // Scale'i küçült
            bookVisual.transform.localScale = Vector3.Lerp(startScale, endScale, easedT);
            
            // Döndürmeye devam et (hızlanarak)
            float rotateSpeed = Mathf.Lerp(180f, 720f, easedT);
            bookVisual.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
            
            yield return null;
        }
        
        // Kitap UI'a ulaştı - gizle
        bookVisual.SetActive(false);
        bookVisual.transform.localScale = startScale; // Scale'i resetle
        
        Debug.Log("[ChestTrigger] Book fly to UI complete");
    }
    #endregion
    
    #region Charging Effect
    /// <summary>
    /// Spawns the charging effect and starts following the book.
    /// </summary>
    private void SpawnChargingEffect()
    {
        Debug.Log($"[ChestTrigger] SpawnChargingEffect called. Prefab: {(chargingPopPrefab != null ? chargingPopPrefab.name : "NULL")}");
        
        if (chargingPopPrefab == null)
        {
            Debug.LogWarning("[ChestTrigger] No charging pop prefab assigned, skipping effect");
            return;
        }
        
        if (bookVisual == null)
        {
            Debug.LogError("[ChestTrigger] Book visual is null, cannot spawn charging effect");
            return;
        }
        
        // Spawn effect at book's current position + height offset
        Vector3 spawnPos = bookVisual.transform.position + Vector3.up * chargingPopHeightOffset;
        Debug.Log($"[ChestTrigger] Spawning charging effect at position: {spawnPos} (offset: {chargingPopHeightOffset})");
        
        activeChargingEffect = Instantiate(chargingPopPrefab, spawnPos, Quaternion.identity);
        
        if (activeChargingEffect == null)
        {
            Debug.LogError("[ChestTrigger] Failed to instantiate charging effect!");
            return;
        }
        
        // Apply scale
        activeChargingEffect.transform.localScale = Vector3.one * chargingPopScale;
        
        // Name it for easy identification in hierarchy
        activeChargingEffect.name = "ChargingPop_BookEffect";
        
        Debug.Log($"[ChestTrigger] Spawned charging effect '{activeChargingEffect.name}' at {spawnPos}, scale: {chargingPopScale}, duration: {chargingPopDuration}s");
        
        // Start following the book
        StartCoroutine(ChargingEffectFollowCoroutine());
    }
    
    /// <summary>
    /// Coroutine that makes the charging effect follow the book for a duration.
    /// </summary>
    private IEnumerator ChargingEffectFollowCoroutine()
    {
        if (activeChargingEffect == null || bookVisual == null)
        {
            yield break;
        }
        
        float elapsed = 0f;
        
        while (elapsed < chargingPopDuration)
        {
            elapsed += Time.deltaTime;
            
            // Follow book position with height offset
            if (activeChargingEffect != null && bookVisual != null && bookVisual.activeInHierarchy)
            {
                activeChargingEffect.transform.position = bookVisual.transform.position + Vector3.up * chargingPopHeightOffset;
            }
            else
            {
                // Book is no longer active, stop following
                break;
            }
            
            yield return null;
        }
        
        // Destroy effect after duration
        DestroyChargingEffect();
    }
    
    /// <summary>
    /// Stops the ambient sound when chest is opened.
    /// </summary>
    private void StopAmbientSound()
    {
        if (ambientAudioSource != null && ambientSoundPlaying)
        {
            ambientAudioSource.Stop();
            ambientSoundPlaying = false;
            Debug.Log($"[ChestTrigger] Stopped ambient sound for {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Plays the chest opening sound.
    /// </summary>
    private void PlayChestOpenSound()
    {
        if (chestOpenSound != null)
        {
            // Play at position for 3D effect
            AudioSource.PlayClipAtPoint(chestOpenSound, transform.position, 1f);
            Debug.Log($"[ChestTrigger] Playing chest open sound");
        }
        
        // Also play book collected sound via AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBookCollectedSound();
        }
    }
    
    /// <summary>
    /// Destroys the active charging effect.
    /// </summary>
    private void DestroyChargingEffect()
    {
        if (activeChargingEffect != null)
        {
            Debug.Log("[ChestTrigger] Destroying charging effect");
            Destroy(activeChargingEffect);
            activeChargingEffect = null;
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
        
        // Animator'ı da sıfırla
        if (chestAnimation != null)
        {
            chestAnimation.SetClosedImmediate();
        }
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

