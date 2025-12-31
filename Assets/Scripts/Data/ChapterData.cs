using UnityEngine;

/// <summary>
/// ScriptableObject containing all data for a single chapter.
/// </summary>
[CreateAssetMenu(fileName = "New Chapter", menuName = "Mysterious Labyrinth/Chapter Data")]
public class ChapterData : ScriptableObject
{
    [Header("Chapter Info")]
    public int chapterNumber = 1;
    public string chapterName = "Chapter 1";
    [TextArea(2, 4)]
    public string chapterDescription;
    
    [Header("Questions")]
    [Tooltip("Question for the gate obstacle")]
    public QuestionData gateQuestion;
    
    [Tooltip("Question for the main obstacle")]
    public QuestionData mainObstacleQuestion;
    
    [Tooltip("Question for the chest code")]
    public QuestionData chestQuestion;
    
    [Tooltip("Questions for junction compass selections")]
    public QuestionData[] junctionQuestions;
    
    [Header("Rewards")]
    [Tooltip("Book awarded upon chapter completion")]
    public BookData bookReward;
    
    [Header("Story - Intro Dialogue")]
    [Tooltip("Intro dialogue messages array. Each element is shown sequentially (Continue to next).")]
    [TextArea(2, 4)]
    public string[] introDialogueMessages;
    
    [Header("Story - Completion Dialogue")]
    [Tooltip("Completion dialogue messages array. Each element is shown sequentially (Continue to next).")]
    [TextArea(2, 4)]
    public string[] completionDialogueMessages;
    
    [Header("Story - Legacy (Deprecated)")]
    [Tooltip("Legacy single dialogue fields (for backward compatibility). Use introDialogueMessages instead.")]
    [TextArea(2, 4)]
    public string introDialogue;
    [TextArea(2, 4)]
    public string completionDialogue;
    [Header("Scene")]
    public string sceneName;
    
    /// <summary>
    /// Gets intro dialogue messages array. Returns array if set, otherwise falls back to legacy introDialogue.
    /// </summary>
    public string[] GetIntroDialogueMessages()
    {
        // Use new array system if available
        if (introDialogueMessages != null && introDialogueMessages.Length > 0)
        {
            // Filter out empty strings
            System.Collections.Generic.List<string> validMessages = new System.Collections.Generic.List<string>();
            foreach (string msg in introDialogueMessages)
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
        if (!string.IsNullOrEmpty(introDialogue))
        {
            return new string[] { introDialogue };
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets completion dialogue messages array. Returns array if set, otherwise falls back to legacy completionDialogue.
    /// </summary>
    public string[] GetCompletionDialogueMessages()
    {
        // Use new array system if available
        if (completionDialogueMessages != null && completionDialogueMessages.Length > 0)
        {
            // Filter out empty strings
            System.Collections.Generic.List<string> validMessages = new System.Collections.Generic.List<string>();
            foreach (string msg in completionDialogueMessages)
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
        if (!string.IsNullOrEmpty(completionDialogue))
        {
            return new string[] { completionDialogue };
        }
        
        return null;
    }
}


