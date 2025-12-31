using UnityEngine;

/// <summary>
/// ScriptableObject containing a single question with multiple choice answers.
/// </summary>
[CreateAssetMenu(fileName = "New Question", menuName = "Mysterious Labyrinth/Question Data")]
public class QuestionData : ScriptableObject
{
    [Header("Dialogue - Approach")]
    [Tooltip("Approach dialogue messages array. Each element is shown sequentially (Continue to next).")]
    [TextArea(2, 4)]
    public string[] approachDialogueMessages;
    
    [Header("Dialogue - Success")]
    [Tooltip("Success dialogue messages array. Each element is shown sequentially (Continue to next).")]
    [TextArea(2, 4)]
    public string[] successDialogueMessages;
    
    [Header("Dialogue - Legacy (Deprecated)")]
    [Tooltip("Legacy single dialogue fields (for backward compatibility). Use approachDialogueMessages instead.")]
    [TextArea(2, 4)]
    public string approachDialogue;
    [TextArea(2, 4)]
    public string successDialogue;

    [Header("Question Settings")]
    [TextArea(2, 4)]
    public string questionText;

    [Header("Answer Options")]
    public string[] answers = new string[3];
    
    [Tooltip("Index of the correct answer (0-based)")]
    [Range(0, 2)]
    public int correctAnswerIndex;
    
    [Header("Question Type")]
    public QuestionType questionType = QuestionType.Obstacle;
    
    [Header("Feedback")]
    [TextArea(1, 2)]
    public string correctFeedback = "Doğru cevap!";
    [TextArea(1, 2)]
    public string incorrectFeedback = "Yanlış cevap!";
    
    /// <summary>
    /// Validates if the given answer index is correct.
    /// </summary>
    public bool IsCorrectAnswer(int answerIndex)
    {
        return answerIndex == correctAnswerIndex;
    }
    
    /// <summary>
    /// Gets the correct answer text.
    /// </summary>
    public string GetCorrectAnswer()
    {
        if (correctAnswerIndex >= 0 && correctAnswerIndex < answers.Length)
        {
            return answers[correctAnswerIndex];
        }
        return string.Empty;
    }
    
    /// <summary>
    /// Gets approach dialogue messages array. Returns array if set, otherwise falls back to legacy approachDialogue.
    /// </summary>
    public string[] GetApproachDialogueMessages()
    {
        // Use new array system if available
        if (approachDialogueMessages != null && approachDialogueMessages.Length > 0)
        {
            // Filter out empty strings
            System.Collections.Generic.List<string> validMessages = new System.Collections.Generic.List<string>();
            foreach (string msg in approachDialogueMessages)
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    validMessages.Add(msg);
                }
            }
            if (validMessages.Count > 0)
            {
                return validMessages.ToArray();
            }
        }
        
        // Fallback to legacy single dialogue for backward compatibility
        if (!string.IsNullOrEmpty(approachDialogue))
        {
            return new string[] { approachDialogue };
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets success dialogue messages array. Returns array if set, otherwise falls back to legacy successDialogue.
    /// </summary>
    public string[] GetSuccessDialogueMessages()
    {
        // Use new array system if available
        if (successDialogueMessages != null && successDialogueMessages.Length > 0)
        {
            // Filter out empty strings
            System.Collections.Generic.List<string> validMessages = new System.Collections.Generic.List<string>();
            foreach (string msg in successDialogueMessages)
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    validMessages.Add(msg);
                }
            }
            if (validMessages.Count > 0)
            {
                return validMessages.ToArray();
            }
        }
        
        // Fallback to legacy single dialogue for backward compatibility
        if (!string.IsNullOrEmpty(successDialogue))
        {
            return new string[] { successDialogue };
        }
        
        return null;
    }
}

/// <summary>
/// Types of questions in the game.
/// </summary>
public enum QuestionType
{
    Gate,       // Gate obstacle questions (Besher Tree, Library, etc.)
    Obstacle,   // Main obstacle questions (Pharaoh, Evil King, etc.)
    Junction,   // Compass/path selection questions
    Chest       // Chest code questions for book collection
}


