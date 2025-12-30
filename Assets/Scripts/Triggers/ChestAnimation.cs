using System;
using UnityEngine;

/// <summary>
/// Animator tabanlı sandık açılma animasyonu kontrolcüsü.
/// ChestTrigger tarafından tetiklenir.
/// </summary>
public class ChestAnimation : MonoBehaviour
{
    #region Serialized Fields
    [Header("Animator Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openTriggerName = "Open";
    
    [Header("Timing")]
    [Tooltip("Açılma animasyonunun süresi (saniye)")]
    [SerializeField] private float openAnimationDuration = 1.5f;
    #endregion
    
    #region Properties
    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }
    #endregion
    
    #region Events
    /// <summary>
    /// Açılma animasyonu tamamlandığında tetiklenir.
    /// </summary>
    public event Action OnOpenComplete;
    #endregion
    
    #region Private Fields
    private int openTriggerHash;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        // Animator'ı otomatik bul (atanmamışsa)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Hash'i önceden hesapla (performans için)
        openTriggerHash = Animator.StringToHash(openTriggerName);
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Sandık açılma animasyonunu başlatır.
    /// </summary>
    /// <param name="onComplete">Animasyon tamamlandığında çağrılacak callback (opsiyonel)</param>
    public void PlayOpenAnimation(Action onComplete = null)
    {
        if (IsOpen || IsAnimating)
        {
            Debug.LogWarning("[ChestAnimation] Chest is already open or animating!");
            onComplete?.Invoke(); // Yine de callback'i çağır
            return;
        }
        
        Debug.Log("[ChestAnimation] Playing open animation");
        
        IsAnimating = true;
        
        // Animator'ı tetikle
        if (animator != null)
        {
            // Parametrenin var olup olmadığını ve tipini kontrol et
            AnimatorControllerParameterType? paramType = null;
            foreach (var param in animator.parameters)
            {
                if (param.name == openTriggerName)
                {
                    paramType = param.type;
                    break;
                }
            }
            
            if (paramType.HasValue)
            {
                // Parametre bulundu - tipine göre ayarla
                switch (paramType.Value)
                {
                    case AnimatorControllerParameterType.Trigger:
                        animator.SetTrigger(openTriggerHash);
                        Debug.Log($"[ChestAnimation] Trigger '{openTriggerName}' set successfully");
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(openTriggerName, true);
                        Debug.Log($"[ChestAnimation] Bool '{openTriggerName}' set to true");
                        break;
                    default:
                        Debug.LogWarning($"[ChestAnimation] Parameter '{openTriggerName}' is type {paramType.Value}, expected Trigger or Bool");
                        animator.SetTrigger(openTriggerHash); // Yine de dene
                        break;
                }
            }
            else
            {
                Debug.LogError($"[ChestAnimation] ERROR: Animator parameter '{openTriggerName}' does not exist! " +
                    $"Available parameters: {string.Join(", ", System.Array.ConvertAll(animator.parameters, p => $"{p.name}({p.type})"))}");
            }
        }
        else
        {
            Debug.LogError("[ChestAnimation] ERROR: No Animator component found! Add Animator to this GameObject.");
        }
        
        // Animasyon bitimini bekle
        StartCoroutine(WaitForOpenAnimation(onComplete));
    }
    
    /// <summary>
    /// Animasyon olmadan direkt açık duruma geçir.
    /// </summary>
    public void SetOpenImmediate()
    {
        IsOpen = true;
        IsAnimating = false;
        
        if (animator != null)
        {
            // Açık state'ine direkt atla
            animator.Play("Open", 0, 1f);
        }
    }
    
    /// <summary>
    /// Animasyon olmadan direkt kapalı duruma geçir (reset için).
    /// </summary>
    public void SetClosedImmediate()
    {
        IsOpen = false;
        IsAnimating = false;
        
        if (animator != null)
        {
            // Kapalı state'ine direkt atla
            animator.Play("Closed", 0, 0f);
        }
    }
    #endregion
    
    #region Animation Coroutines
    private System.Collections.IEnumerator WaitForOpenAnimation(Action onComplete)
    {
        // Animasyon süresini bekle
        yield return new WaitForSeconds(openAnimationDuration);
        
        IsOpen = true;
        IsAnimating = false;
        
        Debug.Log("[ChestAnimation] Open animation complete");
        
        // Event ve callback'i çağır
        OnOpenComplete?.Invoke();
        onComplete?.Invoke();
    }
    #endregion
    
    #region Animation Events (Animator'dan çağrılabilir - opsiyonel)
    /// <summary>
    /// Animator'daki Animation Event'ten çağrılabilir.
    /// Açılma animasyonu tamamlandığında.
    /// </summary>
    public void OnOpenAnimationEvent()
    {
        IsOpen = true;
        IsAnimating = false;
        OnOpenComplete?.Invoke();
        Debug.Log("[ChestAnimation] Animation event received - Open complete");
    }
    #endregion
}
