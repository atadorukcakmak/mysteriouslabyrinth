using System;
using UnityEngine;

/// <summary>
/// Manages player health (lives) with damage, death, and respawn logic.
/// </summary>
public class HealthSystem : MonoBehaviour
{
    #region Events
    public event Action<int, int> OnHealthChanged; // currentLives, maxLives
    public event Action OnDeath;
    public event Action OnDamaged;
    #endregion
    
    #region Serialized Fields
    [Header("Health Settings")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float invincibilityDuration = 1.5f;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private int flashCount = 3;
    #endregion
    
    #region Properties
    public int CurrentLives { get; private set; }
    public int MaxLives => maxLives;
    public bool IsAlive => CurrentLives > 0;
    public bool IsInvincible { get; private set; }
    #endregion
    
    #region Private Fields
    private Renderer[] playerRenderers;
    private float invincibilityTimer;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        // Get max lives from GameManager if available
        if (GameManager.Instance != null)
        {
            maxLives = GameManager.Instance.StartingLives;
        }
        
        CurrentLives = maxLives;
        playerRenderers = GetComponentsInChildren<Renderer>();
    }
    
    private void Start()
    {
        // Update UI
        UpdateHealthUI();
    }
    #endregion
    
    #region Update
    private void Update()
    {
        // Handle invincibility timer
        if (IsInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                IsInvincible = false;
            }
        }
    }
    #endregion
    
    #region Health Management
    /// <summary>
    /// Deals damage to the player.
    /// </summary>
    public void TakeDamage(int amount = 1)
    {
        if (!IsAlive || IsInvincible) return;
        
        CurrentLives = Mathf.Max(0, CurrentLives - amount);
        
        Debug.Log($"[HealthSystem] Took {amount} damage. Lives remaining: {CurrentLives}");
        
        OnDamaged?.Invoke();
        OnHealthChanged?.Invoke(CurrentLives, maxLives);
        UpdateHealthUI();
        
        if (flashOnDamage)
        {
            StartCoroutine(DamageFlashCoroutine());
        }
        
        // Start invincibility
        IsInvincible = true;
        invincibilityTimer = invincibilityDuration;
        
        if (CurrentLives <= 0)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Heals the player.
    /// </summary>
    public void Heal(int amount = 1)
    {
        if (!IsAlive) return;
        
        CurrentLives = Mathf.Min(maxLives, CurrentLives + amount);
        
        Debug.Log($"[HealthSystem] Healed {amount}. Lives: {CurrentLives}");
        
        OnHealthChanged?.Invoke(CurrentLives, maxLives);
        UpdateHealthUI();
    }
    
    /// <summary>
    /// Fully restores health.
    /// </summary>
    public void FullHeal()
    {
        CurrentLives = maxLives;
        OnHealthChanged?.Invoke(CurrentLives, maxLives);
        UpdateHealthUI();
    }
    
    /// <summary>
    /// Resets health for chapter restart.
    /// </summary>
    public void Reset()
    {
        CurrentLives = maxLives;
        IsInvincible = false;
        invincibilityTimer = 0;
        OnHealthChanged?.Invoke(CurrentLives, maxLives);
        UpdateHealthUI();
    }
    
    private void Die()
    {
        Debug.Log("[HealthSystem] Player died!");
        
        OnDeath?.Invoke();
        
        // Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
    }
    #endregion
    
    #region Visual Feedback
    private System.Collections.IEnumerator DamageFlashCoroutine()
    {
        if (playerRenderers == null || playerRenderers.Length == 0) yield break;
        
        for (int i = 0; i < flashCount; i++)
        {
            // Hide
            SetRenderersEnabled(false);
            yield return new WaitForSeconds(flashDuration);
            
            // Show
            SetRenderersEnabled(true);
            yield return new WaitForSeconds(flashDuration);
        }
    }
    
    private void SetRenderersEnabled(bool enabled)
    {
        foreach (var renderer in playerRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }
    #endregion
    
    #region UI
    private void UpdateHealthUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHearts(CurrentLives, maxLives);
        }
    }
    #endregion
}


