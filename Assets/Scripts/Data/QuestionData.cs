using UnityEngine;

/// <summary>
/// ScriptableObject containing a single question with multiple choice answers.
/// </summary>
[CreateAssetMenu(fileName = "New Question", menuName = "Mysterious Labyrinth/Question Data")]
public class QuestionData : ScriptableObject
{
    [Header("Dialogue")]
    [TextArea(2, 4)] public string approachDialogue;
    [TextArea(2, 4)] public string successDialogue;

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


