using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all UI elements including HUD, dialogue, questions, and overlays.
/// Singleton pattern.
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton
    public static UIManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Kitap slot'larını en başta boş olarak başlat (diğer script'lerden önce)
        InitializeBookSlots();
    }
    #endregion
    
    #region Events
    public event Action<int> OnAnswerSelected;
    public event Action OnDialogueClosed;
    public event Action OnDialogueQueueComplete; // Tüm diyaloglar bittiğinde
    public event Action OnCompassClicked;
    #endregion
    
    #region Serialized Fields - Panels
    [Header("Main Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject chapterTransitionPanel;
    
    [Header("HUD Elements")]
    [SerializeField] private Image[] heartImages;
    [SerializeField] private Image[] bookSlots;
    [SerializeField] private Sprite emptyBookSlotSprite; // Boş slot için gri placeholder sprite
    [SerializeField] private Button compassButton;
    
    [Header("Dialogue Elements")]
    [SerializeField] private Image ufyoPortrait;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button continueButton;
    
    [Header("Question Elements")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TMP_Text[] answerTexts;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Image[] answerKeyIcons; // Gate soruları için anahtar ikonları
    [SerializeField] private Sprite keySprite; // Anahtar sprite'ı (Assets/Sprites/anahtar.png)
    
    [Header("Chapter Transition")]
    [SerializeField] private TMP_Text chapterTitleText;
    [SerializeField] private TMP_Text chapterDescriptionText;
    
    [Header("Visual Settings")]
    [SerializeField] private Sprite heartFull;
    [SerializeField] private Sprite heartEmpty;
    [SerializeField] private Color correctAnswerColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color wrongAnswerColor = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private float feedbackDisplayTime = 2f;
    
    [Header("Dialogue Animation")]
    [SerializeField] private float charAppearDelay = 0.03f;  // Her harf arası bekleme
    [SerializeField] private float charDropDuration = 0.15f; // Harfin düşme süresi
    [SerializeField] private float charDropHeight = 20f;     // Harfin düştüğü yükseklik
    [SerializeField] private bool useDropAnimation = true;   // Düşme animasyonu kullan
    
    [Header("Success Feedback")]
    [SerializeField] private float successShowDelay = 1.0f;  // Doğru cevap sonrası bekleme süresi
    #endregion
    
    #region Private Fields
    private Coroutine dialogueCoroutine;
    private Coroutine feedbackCoroutine;
    private bool isShowingQuestion;
    
    // Dialogue queue system
    private System.Collections.Generic.Queue<string> dialogueQueue = new System.Collections.Generic.Queue<string>();
    private bool isDialogueAnimating;
    private System.Action onDialogueQueueCompleteCallback;
    private bool stayInUIAfterDialogue; // Diyalog bitince UI mode'da kal
    
    // Success continue system
    private System.Action successContinueCallback;
    private bool isWaitingForSuccessContinue;
    
    // Animation skip system
    private bool isAnimatingText;
    private string currentAnimatingFullText;
    private bool skipAnimationRequested;
    #endregion
    
    #region Initialization
    private void Start()
    {
        // Subscribe to GameManager events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
        
        // Setup button listeners
        SetupButtonListeners();
        
        // Initialize UI state
        HideAllPanels();
        ShowHUD();
    }
    
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }
    
    private void Update()
    {
        // Input kontrolü - sadece diyalog/soru durumundayken çalışsın
        if (GameManager.Instance == null) return;
        
        GameState state = GameManager.Instance.CurrentState;
        if (state != GameState.Dialogue && state != GameState.Question) return;
        
        // Continue tuşları kontrolü
        bool continuePressed = false;
        
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
             UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame))
        {
            continuePressed = true;
        }
        
        // Mouse tıklaması - ama cevap butonlarına tıklamamışsa
        if (!continuePressed && 
            UnityEngine.InputSystem.Mouse.current != null && 
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Sadece success bekliyorsa mouse tıklaması kabul et
            if (isWaitingForSuccessContinue && !isAnimatingText)
            {
                continuePressed = true;
            }
        }
        
        if (continuePressed)
        {
            HandleContinueInput();
        }
    }
    
    private void HandleContinueInput()
    {
        // Öncelik sırası:
        // 1. Animasyon devam ediyorsa → tamamla
        // 2. Success bekleniyorsa → devam et
        // 3. Diyalog animasyonu devam ediyorsa → tamamla (OnContinueDialogue bunu handle eder)
        
        if (isAnimatingText)
        {
            // Animasyon devam ediyor - tamamla
            skipAnimationRequested = true;
            return;
        }
        
        if (isWaitingForSuccessContinue)
        {
            // Success mesajı gösterildi, kullanıcı devam etmek istiyor
            OnSuccessContinueClicked();
            return;
        }
    }
    
    private void OnSuccessContinueClicked()
    {
        if (!isWaitingForSuccessContinue) return;
        
        isWaitingForSuccessContinue = false;
        var callback = successContinueCallback;
        successContinueCallback = null;
        
        // Feedback'i gizle
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
        
        callback?.Invoke();
    }
    
    private void SetupButtonListeners()
    {
        // Compass button - initially hidden
        if (compassButton != null)
        {
            compassButton.onClick.AddListener(() => OnCompassClicked?.Invoke());
            compassButton.gameObject.SetActive(false); // Default olarak gizli
        }
        
        // Continue button for dialogue
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueDialogue);
        }
        
        // Answer buttons
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i; // Capture for closure
            if (answerButtons[i] != null)
            {
                answerButtons[i].onClick.AddListener(() => SelectAnswer(index));
            }
        }
    }
    #endregion
    
    #region Panel Management
    private void HideAllPanels()
    {
        SetPanelActive(dialoguePanel, false);
        SetPanelActive(questionPanel, false);
        SetPanelActive(pausePanel, false);
        SetPanelActive(gameOverPanel, false);
        SetPanelActive(victoryPanel, false);
        SetPanelActive(chapterTransitionPanel, false);
    }
    
    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
    
    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Playing:
                HideAllPanels();
                ShowHUD();
                break;
            case GameState.Paused:
                ShowPauseMenu();
                break;
            case GameState.GameOver:
                ShowGameOver();
                break;
            case GameState.Victory:
                ShowVictory();
                break;
            case GameState.ChapterTransition:
                ShowChapterTransition();
                break;
        }
    }
    #endregion
    
    #region HUD
    public void ShowHUD()
    {
        SetPanelActive(hudPanel, true);
    }
    
    public void HideHUD()
    {
        SetPanelActive(hudPanel, false);
    }
    
    /// <summary>
    /// Updates the heart display based on current lives.
    /// </summary>
    public void UpdateHearts(int currentLives, int maxLives)
    {
        if (heartImages == null) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] != null)
            {
                if (i < currentLives)
                {
                    heartImages[i].gameObject.SetActive(true);
                    heartImages[i].sprite = i < currentLives ? heartFull : heartEmpty;
                }
                else
                {
                    heartImages[i].gameObject.SetActive(false);
                }
            }
        }
    }
    
    /// <summary>
    /// Tüm kitap slot'larını boş/gri olarak başlat.
    /// Oyun başlangıcında çağrılmalı.
    /// </summary>
    public void InitializeBookSlots()
    {
        if (bookSlots == null)
        {
            Debug.LogWarning("[UIManager] bookSlots array is null!");
            return;
        }
        
        Debug.Log($"[UIManager] Initializing {bookSlots.Length} book slots. EmptySprite assigned: {emptyBookSlotSprite != null}" + 
                  (emptyBookSlotSprite != null ? $" (name: {emptyBookSlotSprite.name})" : ""));
        
        for (int i = 0; i < bookSlots.Length; i++)
        {
            if (bookSlots[i] != null)
            {
                // Önce mevcut sprite'ı temizle
                string previousSprite = bookSlots[i].sprite != null ? bookSlots[i].sprite.name : "null";
                
                // Placeholder sprite varsa onu kullan, yoksa gri renk
                if (emptyBookSlotSprite != null)
                {
                    bookSlots[i].sprite = emptyBookSlotSprite;
                    bookSlots[i].color = Color.white;
                    Debug.Log($"[UIManager] Slot {i}: Changed from '{previousSprite}' to '{emptyBookSlotSprite.name}'");
                }
                else
                {
                    bookSlots[i].sprite = null;
                    bookSlots[i].color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Boş slot - gri
                    Debug.Log($"[UIManager] Slot {i}: Changed from '{previousSprite}' to null (gray)");
                }
            }
            else
            {
                Debug.LogWarning($"[UIManager] Slot {i} is null!");
            }
        }
    }
    
    /// <summary>
    /// Updates a book slot in the inventory display.
    /// </summary>
    public void UpdateBookSlot(int slotIndex, BookData book, bool collected)
    {
        if (bookSlots == null || slotIndex < 0 || slotIndex >= bookSlots.Length) return;
        
        Image slot = bookSlots[slotIndex];
        if (slot == null) return;
        
        Debug.Log($"[UIManager] UpdateBookSlot called - Slot {slotIndex}, collected: {collected}, emptySprite: {emptyBookSlotSprite != null}");
        
        if (collected && book != null)
        {
            if (book.bookIcon != null)
            {
                slot.sprite = book.bookIcon;
                slot.color = Color.white; // Tam görünür
                Debug.Log($"[UIManager] Slot {slotIndex}: SET to collected book '{book.bookName}' icon");
            }
            else
            {
                Debug.LogWarning($"[UIManager] Slot {slotIndex}: Book '{book.bookName}' has no icon assigned!");
                // İkon yoksa placeholder kullan ama farklı renkte göster
                slot.sprite = emptyBookSlotSprite;
                slot.color = new Color(0.5f, 1f, 0.5f, 1f); // Yeşilimsi - toplandı ama ikon yok
            }
        }
        else
        {
            // Placeholder sprite varsa onu kullan
            if (emptyBookSlotSprite != null)
            {
                slot.sprite = emptyBookSlotSprite;
                slot.color = Color.white;
                Debug.Log($"[UIManager] Slot {slotIndex}: SET to empty placeholder (bookLocked)");
            }
            else
            {
                slot.sprite = null;
                slot.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Empty slot - gri
                Debug.Log($"[UIManager] Slot {slotIndex}: SET to null (gray) - no empty sprite");
            }
        }
    }
    
    /// <summary>
    /// Belirtilen indeksteki book slot'un RectTransform'unu döndürür.
    /// ChestTrigger'daki kitap uçuşu animasyonu için kullanılır.
    /// </summary>
    public RectTransform GetBookSlotTransform(int slotIndex)
    {
        if (bookSlots == null || slotIndex < 0 || slotIndex >= bookSlots.Length) return null;
        
        if (bookSlots[slotIndex] != null)
        {
            return bookSlots[slotIndex].rectTransform;
        }
        return null;
    }
    
    /// <summary>
    /// İlk boş book slot'un RectTransform'unu döndürür.
    /// </summary>
    public RectTransform GetNextEmptyBookSlot()
    {
        if (bookSlots == null) return null;
        
        for (int i = 0; i < bookSlots.Length; i++)
        {
            if (bookSlots[i] != null && bookSlots[i].sprite == null)
            {
                return bookSlots[i].rectTransform;
            }
        }
        
        // Boş slot yoksa ilk slot'u döndür
        if (bookSlots.Length > 0 && bookSlots[0] != null)
        {
            return bookSlots[0].rectTransform;
        }
        
        return null;
    }
    
    /// <summary>
    /// Enables or disables the compass button.
    /// </summary>
    public void SetCompassEnabled(bool enabled)
    {
        if (compassButton != null)
        {
            compassButton.gameObject.SetActive(enabled);
            compassButton.interactable = enabled;
            Debug.Log($"[UIManager] Compass button set to: {(enabled ? "ENABLED" : "DISABLED")}");
        }
        else
        {
            Debug.LogWarning("[UIManager] Compass button is null!");
        }
    }
    #endregion
    
    #region Dialogue
    /// <summary>
    /// Birden fazla diyalog gösterir. Hepsi bitince callback çağrılır.
    /// </summary>
    public void ShowDialogueSequence(string[] messages, System.Action onComplete = null)
    {
        if (messages == null || messages.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }
        
        // Kuyruğu temizle ve yeni mesajları ekle
        dialogueQueue.Clear();
        foreach (string msg in messages)
        {
            if (!string.IsNullOrEmpty(msg))
            {
                dialogueQueue.Enqueue(msg);
            }
        }
        
        onDialogueQueueCompleteCallback = onComplete;
        
        // İlk mesajı göster
        ShowNextDialogue();
    }
    
    /// <summary>
    /// Tek bir diyalog gösterir (eski sistem uyumluluğu için).
    /// </summary>
    public void ShowDialogue(string text, Sprite portrait = null)
    {
        ShowDialogueSequence(new string[] { text }, null);
        
        if (ufyoPortrait != null && portrait != null)
        {
            ufyoPortrait.sprite = portrait;
        }
    }
    
    /// <summary>
    /// Tek diyalog gösterir ve bitince callback çağırır.
    /// </summary>
    public void ShowDialogueWithCallback(string text, System.Action onComplete)
    {
        stayInUIAfterDialogue = false;
        ShowDialogueSequence(new string[] { text }, onComplete);
    }
    
    /// <summary>
    /// Tek diyalog gösterir, bitince callback çağırır ve UI mode'da kalır (oyuncu hareket edemez, cursor açık).
    /// </summary>
    public void ShowDialogueWithCallbackStayInUI(string text, System.Action onComplete)
    {
        stayInUIAfterDialogue = true;
        ShowDialogueSequence(new string[] { text }, onComplete);
    }
    
    private void ShowNextDialogue()
    {
        if (dialogueQueue.Count == 0)
        {
            // Tüm diyaloglar bitti
            FinishDialogueSequence();
            return;
        }
        
        string nextMessage = dialogueQueue.Dequeue();
        
        SetPanelActive(dialoguePanel, true);
        
        // Cursor'u aç
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
        }
        
        // Animasyonlu göster
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }
        dialogueCoroutine = StartCoroutine(AnimateDialogueText(nextMessage));
    }
    
    private IEnumerator AnimateDialogueText(string text)
    {
        if (dialogueText == null) yield break;
        
        isDialogueAnimating = true;
        isAnimatingText = true;
        currentAnimatingFullText = text;
        skipAnimationRequested = false;
        dialogueText.text = "";
        
        if (useDropAnimation)
        {
            // Düşen harfler animasyonu
            yield return StartCoroutine(DropTextAnimation(text));
        }
        else
        {
            // Basit typewriter
            foreach (char c in text)
            {
                if (skipAnimationRequested)
                {
                    dialogueText.text = text;
                    break;
                }
                dialogueText.text += c;
                yield return new WaitForSecondsRealtime(charAppearDelay);
            }
        }
        
        // Animasyon tamamlandı - tam metni göster
        dialogueText.text = text;
        isDialogueAnimating = false;
        isAnimatingText = false;
        skipAnimationRequested = false;
    }
    
    private IEnumerator DropTextAnimation(string text)
    {
        // TMP için rich text kullanarak her harfi ayrı animate ediyoruz
        System.Text.StringBuilder visibleText = new System.Text.StringBuilder();
        
        for (int i = 0; i < text.Length; i++)
        {
            // Skip isteği geldiyse direkt tamamla
            if (skipAnimationRequested)
            {
                dialogueText.text = text;
                yield break;
            }
            
            char c = text[i];
            visibleText.Append(c);
            dialogueText.text = visibleText.ToString();
            
            // Karakter görünür olduktan sonra kısa animasyon
            // TMP'nin vertex animasyonu için ForceMeshUpdate gerekli
            dialogueText.ForceMeshUpdate();
            
            // Düşme efekti için son karakteri animate et
            if (c != ' ' && c != '\n')
            {
                yield return StartCoroutine(AnimateLastChar(i));
            }
            
            yield return new WaitForSecondsRealtime(charAppearDelay);
        }
    }
    
    private IEnumerator AnimateLastChar(int charIndex)
    {
        if (dialogueText == null) yield break;
        
        TMP_TextInfo textInfo = dialogueText.textInfo;
        
        if (charIndex >= textInfo.characterCount) yield break;
        
        TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
        if (!charInfo.isVisible) yield break;
        
        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;
        
        Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
        
        // Orijinal pozisyonları kaydet
        Vector3[] originalPos = new Vector3[4];
        for (int j = 0; j < 4; j++)
        {
            originalPos[j] = vertices[vertexIndex + j];
        }
        
        // Yukarıdan düşür
        float elapsed = 0f;
        while (elapsed < charDropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / charDropDuration;
            
            // Ease out bounce benzeri
            float offset = charDropHeight * (1f - t * t);
            
            for (int j = 0; j < 4; j++)
            {
                vertices[vertexIndex + j] = originalPos[j] + Vector3.up * offset;
            }
            
            // Mesh'i güncelle
            textInfo.meshInfo[materialIndex].mesh.vertices = vertices;
            dialogueText.UpdateGeometry(textInfo.meshInfo[materialIndex].mesh, materialIndex);
            
            yield return null;
        }
        
        // Son pozisyona yerleştir
        for (int j = 0; j < 4; j++)
        {
            vertices[vertexIndex + j] = originalPos[j];
        }
        textInfo.meshInfo[materialIndex].mesh.vertices = vertices;
        dialogueText.UpdateGeometry(textInfo.meshInfo[materialIndex].mesh, materialIndex);
    }
    
    /// <summary>
    /// Continue butonuna basıldığında çağrılır.
    /// </summary>
    public void OnContinueDialogue()
    {
        if (isDialogueAnimating || isAnimatingText)
        {
            // Animasyon devam ediyorsa - tamamla
            SkipDialogueAnimation();
            return;
        }
        
        // Sonraki diyaloğa geç veya bitir
        ShowNextDialogue();
    }
    
    private void SkipDialogueAnimation()
    {
        // Skip flag'i set et - coroutine kendisi tamamlayacak
        skipAnimationRequested = true;
        
        // Ayrıca direkt metni göster
        if (!string.IsNullOrEmpty(currentAnimatingFullText))
        {
            if (dialogueText != null && isDialogueAnimating)
            {
                dialogueText.text = currentAnimatingFullText;
            }
        }
        
        // Coroutine'i durdur
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }
        
        isDialogueAnimating = false;
        isAnimatingText = false;
    }
    
    private void FinishDialogueSequence()
    {
        SetPanelActive(dialoguePanel, false);
        
        // Callback'i çağır
        var callback = onDialogueQueueCompleteCallback;
        onDialogueQueueCompleteCallback = null;
        
        OnDialogueQueueComplete?.Invoke();
        OnDialogueClosed?.Invoke();
        
        callback?.Invoke();
        
        // UI mode'da kalma isteği varsa Playing'e dönme
        if (stayInUIAfterDialogue)
        {
            // Cursor açık kalsın, hareket edilemesin ama Playing'e dönme
            Debug.Log("[UIManager] Staying in UI mode after dialogue");
            stayInUIAfterDialogue = false; // Flag'i sıfırla
            return;
        }
        
        // Eğer soru beklemiyorsa Playing'e dön
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Dialogue)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    public void CloseDialogue()
    {
        // Kuyruğu temizle
        dialogueQueue.Clear();
        onDialogueQueueCompleteCallback = null;
        stayInUIAfterDialogue = false;
        
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }
        isDialogueAnimating = false;
        
        SetPanelActive(dialoguePanel, false);
        OnDialogueClosed?.Invoke();
    }
    
    /// <summary>
    /// UI mode'dan çıkıp oyun moduna döner (cursor kilitlenir, hareket açılır).
    /// </summary>
    public void ReturnToGameMode()
    {
        stayInUIAfterDialogue = false;
        SetCompassEnabled(false);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("[UIManager] Returned to game mode");
        
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Dialogue)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    #endregion
    
    #region Question System
    /// <summary>
    /// Shows a question overlay with answer options.
    /// </summary>
    public void ShowQuestion(QuestionData question)
    {
        if (question == null) return;
        
        isShowingQuestion = true;
        SetPanelActive(questionPanel, true);
        
        // Set question text
        if (questionText != null)
        {
            questionText.text = question.questionText;
        }
        
        // Gate soruları için anahtar ikonu göster
        bool isGateQuestion = question.questionType == QuestionType.Gate;
        
        // Set answer buttons
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < question.answers.Length && !string.IsNullOrEmpty(question.answers[i]))
            {
                answerButtons[i].gameObject.SetActive(true);
                answerButtons[i].interactable = true;
                
                // Reset ALL button colors to default (white)
                ColorBlock colors = answerButtons[i].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Slight gray on hover
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f); // Gray when disabled
                answerButtons[i].colors = colors;
                
                if (answerTexts[i] != null)
                {
                    answerTexts[i].text = question.answers[i];
                }
                
                // Anahtar ikonunu göster/gizle (sadece Gate soruları için)
                if (answerKeyIcons != null && i < answerKeyIcons.Length && answerKeyIcons[i] != null)
                {
                    answerKeyIcons[i].gameObject.SetActive(isGateQuestion);
                    if (isGateQuestion && keySprite != null)
                    {
                        answerKeyIcons[i].sprite = keySprite;
                    }
                }
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
                
                // Bu şık gizliyse anahtar ikonunu da gizle
                if (answerKeyIcons != null && i < answerKeyIcons.Length && answerKeyIcons[i] != null)
                {
                    answerKeyIcons[i].gameObject.SetActive(false);
                }
            }
        }
        
        // Hide feedback
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
        
        // Update game state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Question);
        }
        
        // Show cursor for interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private void SelectAnswer(int index)
    {
        if (!isShowingQuestion) return;
        
        // Don't disable buttons here - let QuestionManager decide
        OnAnswerSelected?.Invoke(index);
    }
    
    /// <summary>
    /// Marks a wrong answer as red but keeps the question open.
    /// </summary>
    public void MarkWrongAnswer(int selectedIndex, string feedbackMessage)
    {
        // Highlight wrong button as red and disable it
        if (selectedIndex >= 0 && selectedIndex < answerButtons.Length)
        {
            ColorBlock colors = answerButtons[selectedIndex].colors;
            colors.normalColor = wrongAnswerColor;
            colors.disabledColor = wrongAnswerColor;
            colors.selectedColor = wrongAnswerColor;
            answerButtons[selectedIndex].colors = colors;
            
            // Disable only this wrong button so player can't click it again
            answerButtons[selectedIndex].interactable = false;
        }
        
        // Show feedback text temporarily with animation
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(true);
            feedbackText.text = "";
            feedbackText.color = wrongAnswerColor;
            
            // Animate the feedback text
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            feedbackCoroutine = StartCoroutine(AnimateFeedbackThenHide(feedbackMessage, wrongAnswerColor));
        }
    }
    
    /// <summary>
    /// Marks correct answer as green.
    /// </summary>
    public void MarkCorrectAnswer(int selectedIndex)
    {
        // Disable all buttons
        foreach (var btn in answerButtons)
        {
            if (btn != null) btn.interactable = false;
        }
        
        // Highlight correct button as green
        if (selectedIndex >= 0 && selectedIndex < answerButtons.Length)
        {
            ColorBlock colors = answerButtons[selectedIndex].colors;
            colors.normalColor = correctAnswerColor;
            colors.disabledColor = correctAnswerColor;
            colors.selectedColor = correctAnswerColor;
            answerButtons[selectedIndex].colors = colors;
        }
    }
    
    /// <summary>
    /// Shows success message with falling animation, waits for Continue.
    /// Önce belli bir süre bekler, sonra animasyonlu gösterir.
    /// </summary>
    public void ShowSuccessDialogue(string message, System.Action onContinue)
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }
        feedbackCoroutine = StartCoroutine(ShowSuccessSequence(message, onContinue));
    }
    
    private IEnumerator ShowSuccessSequence(string message, System.Action onContinue)
    {
        // Önce kısa bir bekleme - doğru cevabın yeşil olduğunu görsün
        yield return new WaitForSecondsRealtime(successShowDelay);
        
        // Success mesajını feedback text'te göster
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(true);
            feedbackText.text = "";
            feedbackText.color = correctAnswerColor;
            
            // Animasyonlu göster
            yield return StartCoroutine(AnimateFeedbackText(message));
            
            // "Devam etmek için tıkla" mesajı ekle
            feedbackText.text += "\n\n<size=70%><i>(Devam etmek için SPACE veya ENTER tuşuna basın…)</i></size>";
            
            // Kullanıcının devam etmesini bekle
            successContinueCallback = onContinue;
            isWaitingForSuccessContinue = true;
            
            // NOT: Buradan sonra Update() içindeki HandleContinueInput devam edecek
        }
        else
        {
            // Feedback text yoksa direkt callback
            onContinue?.Invoke();
        }
    }
    
    private IEnumerator AnimateFeedbackThenHide(string message, Color color)
    {
        // Düşen harfler animasyonu
        yield return StartCoroutine(AnimateFeedbackText(message));
        
        // Yanlış cevap için 2 saniye göster sonra gizle
        // (Oyuncu tekrar deneyebilsin diye soru açık kalıyor)
        yield return new WaitForSecondsRealtime(2f);
        
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }
    
    
    private IEnumerator AnimateFeedbackText(string message)
    {
        if (feedbackText == null) yield break;
        
        isAnimatingText = true;
        currentAnimatingFullText = message;
        skipAnimationRequested = false;
        feedbackText.text = "";
        
        // Basit typewriter (TMP vertex animasyonu feedback için karmaşık olabilir)
        foreach (char c in message)
        {
            // Skip isteği geldiyse direkt tamamla
            if (skipAnimationRequested)
            {
                feedbackText.text = message;
                break;
            }
            
            feedbackText.text += c;
            yield return new WaitForSecondsRealtime(charAppearDelay);
        }
        
        // Animasyon tamamlandı
        feedbackText.text = message;
        isAnimatingText = false;
        skipAnimationRequested = false;
    }
    
    /// <summary>
    /// Soru ekranında herhangi bir yere tıklandığında (success sonrası devam için).
    /// </summary>
    public void OnQuestionPanelClicked()
    {
        if (isWaitingForSuccessContinue)
        {
            isWaitingForSuccessContinue = false;
            var callback = successContinueCallback;
            successContinueCallback = null;
            callback?.Invoke();
        }
    }
    
    // Eski method - artık kullanılmıyor ama uyumluluk için bırakıyoruz
    public void ShowAnswerFeedback(int selectedIndex, bool isCorrect, string feedbackMessage)
    {
        if (isCorrect)
        {
            MarkCorrectAnswer(selectedIndex);
            ShowSuccessDialogue(feedbackMessage, () => CloseQuestion());
        }
        else
        {
            MarkWrongAnswer(selectedIndex, feedbackMessage);
        }
    }
    
    /// <summary>
    /// Soru panelini kapatır.
    /// </summary>
    /// <param name="stayInUIMode">True ise UI mode'da kalır, false ise Playing'e döner.</param>
    public void CloseQuestion(bool stayInUIMode = false)
    {
        isShowingQuestion = false;
        SetPanelActive(questionPanel, false);
        
        // UI mode'da kalma isteği varsa sadece paneli kapat
        if (stayInUIMode)
        {
            Debug.Log("[UIManager] Question closed, staying in UI mode");
            // Cursor açık kalsın
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        
        // Always restore to Playing state after question (unless in special states)
        if (GameManager.Instance != null)
        {
            GameState currentState = GameManager.Instance.CurrentState;
            if (currentState == GameState.Question || currentState == GameState.Dialogue)
            {
                // Close any open dialogue first
                SetPanelActive(dialoguePanel, false);
                
                GameManager.Instance.SetGameState(GameState.Playing);
            }
            
            // Re-lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    #endregion
    
    #region Menu Screens
    public void ShowPauseMenu()
    {
        SetPanelActive(pausePanel, true);
    }
    
    public void HidePauseMenu()
    {
        SetPanelActive(pausePanel, false);
    }
    
    public void ShowGameOver()
    {
        HideAllPanels();
        SetPanelActive(gameOverPanel, true);
    }
    
    public void ShowVictory()
    {
        HideAllPanels();
        SetPanelActive(victoryPanel, true);
    }
    
    public void ShowChapterTransition()
    {
        SetPanelActive(chapterTransitionPanel, true);
        
        if (GameManager.Instance != null)
        {
            ChapterData chapter = GameManager.Instance.CurrentChapterData;
            if (chapter != null)
            {
                if (chapterTitleText != null)
                {
                    chapterTitleText.text = chapter.chapterName;
                }
                if (chapterDescriptionText != null)
                {
                    chapterDescriptionText.text = chapter.chapterDescription;
                }
            }
        }
    }
    
    /// <summary>
    /// Called by UI button to proceed to next chapter.
    /// </summary>
    public void OnContinueToNextChapter()
    {
        SetPanelActive(chapterTransitionPanel, false);
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartChapter(GameManager.Instance.CurrentChapter);
        }
    }
    
    /// <summary>
    /// Called by UI button to restart chapter.
    /// </summary>
    public void OnRestartChapter()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartChapter();
        }
    }
    
    /// <summary>
    /// Called by UI button to return to menu.
    /// </summary>
    public void OnReturnToMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMenu();
        }
    }
    
    /// <summary>
    /// Called by UI button to resume game.
    /// </summary>
    public void OnResumeGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }
    }
    #endregion
}

