using UnityEngine;

/// <summary>
/// ScriptableObject containing data for a holy book collectible.
/// </summary>
[CreateAssetMenu(fileName = "New Book", menuName = "Mysterious Labyrinth/Book Data")]
public class BookData : ScriptableObject
{
    [Header("Book Info")]
    public string bookName = "Torah";
    public int bookOrder = 1; // 1=Torah, 2=Psalms, 3=Gospel, 4=Quran
    
    [TextArea(2, 4)]
    public string bookDescription;
    
    [Header("Visuals")]
    public Sprite bookIcon;
    public Color bookColor = Color.white;
    
    [Tooltip("True for Quran (glowing effect), false for others (matte)")]
    public bool isGlowing = false;
    
    [Header("Collection")]
    public int requiredChapter = 1;
}


