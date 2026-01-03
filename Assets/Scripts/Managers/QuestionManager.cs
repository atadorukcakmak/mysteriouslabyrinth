using System;
using UnityEngine;

/// <summary>
/// Manages question flow, answer validation, and callbacks.
/// Singleton pattern.
/// </summary>
public class QuestionManager : MonoBehaviour
{
    #region Singleton
    public static QuestionManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion
    
    #region Events
    public event Action<bool> OnQuestionAnswered; // bool = isCorrect
    public event Action<QuestionData> OnQuestionStarted;
    public event Action OnQuestionCompleted;
    #endregion
    
    #region Properties
    public QuestionData CurrentQuestion { get; private set; }
    public bool IsQuestionActive { get; private set; }
    #endregion
    
    #region Private Fields
    private Action<bool> currentCallback;
    private object currentTriggerSource;
    #endregion
    
    #region Initialization
    private void Start()
    {
        // Subscribe to UI events
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnAnswerSelected += HandleAnswerSelected;
        }
    }
    
    private void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnAnswerSelected -= HandleAnswerSelected;
        }
    }
    #endregion
    
    #region Question Flow
    /// <summary>
    /// Starts a question with a callback for the result.
    /// </summary>
    /// <param name="question">The question data to display.</param>
    /// <param name="onComplete">Callback with true if correct, false if wrong.</param>
    /// <param name="triggerSource">Optional reference to the trigger that started this question.</param>
    public void AskQuestion(QuestionData question, Action<bool> onComplete = null, object triggerSource = null)
    {
        if (question == null)
        {
            Debug.LogError("[QuestionManager] Cannot ask null question!");
            onComplete?.Invoke(false);
            return;
        }
        
        if (IsQuestionActive)
        {
            Debug.LogWarning("[QuestionManager] Question already active, ignoring new question");
            return;
        }
        
        CurrentQuestion = question;
        currentCallback = onComplete;
        currentTriggerSource = triggerSource;
        IsQuestionActive = true;
        
        OnQuestionStarted?.Invoke(question);
        
        // Show question UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowQuestion(question);
        }
        
        Debug.Log($"[QuestionManager] Asking question: {question.questionText}");
    }
    
    /// <summary>
    /// Asks a gate question from current chapter.
    /// </summary>
    public void AskGateQuestion(Action<bool> onComplete = null, object triggerSource = null)
    {
        if (GameManager.Instance?.CurrentChapterData?.gateQuestion != null)
        {
            AskQuestion(GameManager.Instance.CurrentChapterData.gateQuestion, onComplete, triggerSource);
        }
        else
        {
            Debug.LogError("[QuestionManager] No gate question available for current chapter");
            onComplete?.Invoke(false);
        }
    }
    
    /// <summary>
    /// Asks the main obstacle question from current chapter.
    /// </summary>
    public void AskObstacleQuestion(Action<bool> onComplete = null, object triggerSource = null)
    {
        if (GameManager.Instance?.CurrentChapterData?.mainObstacleQuestion != null)
        {
            AskQuestion(GameManager.Instance.CurrentChapterData.mainObstacleQuestion, onComplete, triggerSource);
        }
        else
        {
            Debug.LogError("[QuestionManager] No obstacle question available for current chapter");
            onComplete?.Invoke(false);
        }
    }
    
    /// <summary>
    /// Asks a junction/compass question from current chapter.
    /// </summary>
    public void AskJunctionQuestion(int junctionIndex, Action<bool> onComplete = null, object triggerSource = null)
    {
        var junctionQuestions = GameManager.Instance?.CurrentChapterData?.junctionQuestions;
        
        if (junctionQuestions != null && junctionIndex >= 0 && junctionIndex < junctionQuestions.Length)
        {
            AskQuestion(junctionQuestions[junctionIndex], onComplete, triggerSource);
        }
        else
        {
            Debug.LogError($"[QuestionManager] No junction question available at index {junctionIndex}");
            onComplete?.Invoke(false);
        }
    }
    
    /// <summary>
    /// Asks the chest code question from current chapter.
    /// </summary>
    public void AskChestQuestion(Action<bool> onComplete = null, object triggerSource = null)
    {
        if (GameManager.Instance?.CurrentChapterData?.chestQuestion != null)
        {
            AskQuestion(GameManager.Instance.CurrentChapterData.chestQuestion, onComplete, triggerSource);
        }
        else
        {
            Debug.LogError("[QuestionManager] No chest question available for current chapter");
            onComplete?.Invoke(false);
        }
    }
    #endregion
    
    #region Answer Handling
    private void HandleAnswerSelected(int answerIndex)
    {
        if (!IsQuestionActive || CurrentQuestion == null)
        {
            Debug.LogWarning("[QuestionManager] No active question to answer");
            return;
        }
        
        bool isCorrect = CurrentQuestion.IsCorrectAnswer(answerIndex);
        string feedback = isCorrect ? CurrentQuestion.correctFeedback : CurrentQuestion.incorrectFeedback;
        
        Debug.Log($"[QuestionManager] Answer {answerIndex} selected. Correct: {isCorrect}");
        
        // Play answer sound
        if (AudioManager.Instance != null)
        {
            if (isCorrect)
            {
                AudioManager.Instance.PlayCorrectAnswerSound();
            }
            else
            {
                AudioManager.Instance.PlayWrongAnswerSound();
            }
        }
        
        if (isCorrect)
        {
            // CORRECT ANSWER - Mark correct, show success dialogue with animation
            if (UIManager.Instance != null)
            {
                // Önce doğru şıkkı yeşil yap
                UIManager.Instance.MarkCorrectAnswer(answerIndex);
                
                // Gate soruları için özel akış - anahtar animasyonu göster
                if (CurrentQuestion.questionType == QuestionType.Gate)
                {
                    Debug.Log("[QuestionManager] Gate question - showing key animation before proceeding");
                    
                    // Anahtar animasyonu göster, bitince devam et (Continue bekleme)
                    UIManager.Instance.ShowKeyAnimation(() =>
                    {
                        // Anahtar animasyonu bitti, success dialogue'u atla ve direkt devam et
                        Debug.Log("[QuestionManager] Key animation complete, closing question");
                        
                        if (UIManager.Instance != null)
                        {
                            UIManager.Instance.CloseQuestion(stayInUIMode: true);
                        }
                        
                        OnQuestionAnswered?.Invoke(true);
                        currentCallback?.Invoke(true);
                        CompleteQuestion();
                    });
                }
                else
                {
                    // Normal sorular için eski akış - success dialogue göster
                    UIManager.Instance.ShowSuccessDialogue(feedback, () =>
                    {
                        // Continue'a basıldığında burası çalışır
                        // Soru panelini kapat ama UI mode'da kal - trigger success diyaloğunu gösterecek
                        if (UIManager.Instance != null)
                        {
                            UIManager.Instance.CloseQuestion(stayInUIMode: true);
                        }
                        
                        OnQuestionAnswered?.Invoke(true);
                        currentCallback?.Invoke(true);
                        CompleteQuestion();
                    });
                }
            }
        }
        else
        {
            // WRONG ANSWER - Mark as wrong, keep question open
            if (UIManager.Instance != null)
            {
                UIManager.Instance.MarkWrongAnswer(answerIndex, feedback);
            }
            
            // Damage player
            var healthSystem = FindFirstObjectByType<HealthSystem>();
            if (healthSystem != null)
            {
                healthSystem.TakeDamage(1);
                
                // Check if player died - close question if no lives left
                if (!healthSystem.IsAlive)
                {
                    Debug.Log("[QuestionManager] Player died - closing question");
                    OnQuestionAnswered?.Invoke(false);
                    currentCallback?.Invoke(false);
                    CompleteQuestion();
                    
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.CloseQuestion();
                    }
                }
            }
            
            // Don't close question - player can try again
        }
    }
    
    private void CompleteQuestion()
    {
        IsQuestionActive = false;
        CurrentQuestion = null;
        currentCallback = null;
        currentTriggerSource = null;
        
        OnQuestionCompleted?.Invoke();
    }
    
    /// <summary>
    /// Cancels the current question without triggering callbacks.
    /// </summary>
    public void CancelQuestion()
    {
        if (!IsQuestionActive) return;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseQuestion();
        }
        
        IsQuestionActive = false;
        CurrentQuestion = null;
        currentCallback = null;
        currentTriggerSource = null;
        
        Debug.Log("[QuestionManager] Question cancelled");
    }
    #endregion
    
    #region Utility
    /// <summary>
    /// Gets the trigger source that started the current question.
    /// </summary>
    public T GetCurrentTriggerSource<T>() where T : class
    {
        return currentTriggerSource as T;
    }
    #endregion
}

