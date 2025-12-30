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
    
    [Header("Story")]
    [TextArea(2, 4)]
    public string introDialogue;
    [TextArea(2, 4)]
    public string gateDialogue;
    [TextArea(2, 4)]
    public string obstacleDialogue;
    [TextArea(2, 4)]
    public string chestDialogue;
    [TextArea(2, 4)]
    public string completionDialogue;
    
    [Header("Scene")]
    public string sceneName;
}


