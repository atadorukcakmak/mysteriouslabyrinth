using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages collected holy books.
/// </summary>
public class Inventory : MonoBehaviour
{
    #region Events
    public event Action<BookData> OnBookCollected;
    public event Action OnInventoryChanged;
    #endregion
    
    #region Serialized Fields
    [Header("Book Definitions")]
    [SerializeField] private BookData[] allBooks; // All 4 books in order
    #endregion
    
    #region Properties
    public List<BookData> CollectedBooks { get; private set; } = new List<BookData>();
    public int CollectedCount => CollectedBooks.Count;
    public bool HasAllBooks => CollectedBooks.Count >= 4;
    #endregion
    
    #region Initialization
    private void Start()
    {
        UpdateInventoryUI();
    }
    #endregion
    
    #region Book Collection
    /// <summary>
    /// Adds a book to the inventory.
    /// </summary>
    public void CollectBook(BookData book)
    {
        if (book == null) return;
        
        if (CollectedBooks.Contains(book))
        {
            Debug.Log($"[Inventory] Book '{book.bookName}' already collected");
            return;
        }
        
        CollectedBooks.Add(book);
        
        Debug.Log($"[Inventory] Collected: {book.bookName} ({CollectedCount}/4)");
        
        OnBookCollected?.Invoke(book);
        OnInventoryChanged?.Invoke();
        UpdateInventoryUI();
        
        // Check for game completion
        if (HasAllBooks)
        {
            Debug.Log("[Inventory] All books collected!");
        }
    }
    
    /// <summary>
    /// Checks if a specific book has been collected.
    /// </summary>
    public bool HasBook(BookData book)
    {
        return book != null && CollectedBooks.Contains(book);
    }
    
    /// <summary>
    /// Checks if a book by order number has been collected.
    /// </summary>
    public bool HasBook(int bookOrder)
    {
        return CollectedBooks.Exists(b => b.bookOrder == bookOrder);
    }
    
    /// <summary>
    /// Gets a book definition by order.
    /// </summary>
    public BookData GetBookByOrder(int order)
    {
        if (allBooks == null) return null;
        
        foreach (var book in allBooks)
        {
            if (book != null && book.bookOrder == order)
            {
                return book;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Clears all collected books (for game restart).
    /// </summary>
    public void Clear()
    {
        CollectedBooks.Clear();
        OnInventoryChanged?.Invoke();
        UpdateInventoryUI();
    }
    #endregion
    
    #region UI
    private void UpdateInventoryUI()
    {
        if (UIManager.Instance == null || allBooks == null) 
        {
            Debug.LogWarning($"[Inventory] UpdateInventoryUI skipped - UIManager: {UIManager.Instance != null}, allBooks: {allBooks != null}");
            return;
        }
        
        Debug.Log($"[Inventory] UpdateInventoryUI called. CollectedBooks count: {CollectedBooks.Count}");
        
        for (int i = 0; i < allBooks.Length; i++)
        {
            if (allBooks[i] != null)
            {
                bool collected = HasBook(allBooks[i]);
                Debug.Log($"[Inventory] Slot {i}: Book '{allBooks[i].bookName}', collected: {collected}, hasIcon: {allBooks[i].bookIcon != null}");
                UIManager.Instance.UpdateBookSlot(i, allBooks[i], collected);
            }
            else
            {
                Debug.LogWarning($"[Inventory] Slot {i}: allBooks[{i}] is null!");
            }
        }
    }
    #endregion
}


