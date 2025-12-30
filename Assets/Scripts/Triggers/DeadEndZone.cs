using System.Collections;
using UnityEngine;

/// <summary>
/// Dead-end zone that damages the player after spending too much time inside.
/// Encourages players to use the compass at junctions.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeadEndZone : MonoBehaviour
{
    #region Serialized Fields
    [Header("Timing")]
    [SerializeField] private float warningTime = 3f;      // Time before warning
    [SerializeField] private float damageTime = 6f;       // Time before taking damage
    [SerializeField] private float damageInterval = 3f;   // Time between damage ticks
    
    [Header("Damage")]
    [SerializeField] private int damageAmount = 1;
    
    [Header("Feedback")]
    [SerializeField] private string warningMessage = "This path feels wrong... You should turn back!";
    [SerializeField] private string damageMessage = "You've wasted precious time in a dead end!";
    
    [Header("Visual Effects")]
    [SerializeField] private Color fogColor = new Color(0.3f, 0.1f, 0.1f);
    [SerializeField] private float fogDensity = 0.05f;
    [SerializeField] private bool enableFogEffect = true;
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool playerInside;
    private float timeInZone;
    private bool warningShown;
    private bool isDamaging;
    private Coroutine damageCoroutine;
    
    // Original fog settings
    private bool originalFogEnabled;
    private Color originalFogColor;
    private float originalFogDensity;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        // Store original fog settings
        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
    }
    #endregion
    
    #region Trigger Detection
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            timeInZone = 0f;
            warningShown = false;
            isDamaging = false;
            
            Debug.Log($"[DeadEndZone] Player entered dead end: {gameObject.name}");
            
            if (enableFogEffect)
            {
                StartCoroutine(TransitionFog(true));
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            timeInZone = 0f;
            warningShown = false;
            
            if (damageCoroutine != null)
            {
                StopCoroutine(damageCoroutine);
                damageCoroutine = null;
            }
            isDamaging = false;
            
            Debug.Log($"[DeadEndZone] Player exited dead end: {gameObject.name}");
            
            if (enableFogEffect)
            {
                StartCoroutine(TransitionFog(false));
            }
        }
    }
    #endregion
    
    #region Update
    private void Update()
    {
        if (!playerInside) return;
        
        // Don't count time during questions/dialogue
        if (GameManager.Instance != null && 
            GameManager.Instance.CurrentState != GameState.Playing)
        {
            return;
        }
        
        timeInZone += Time.deltaTime;
        
        // Show warning
        if (!warningShown && timeInZone >= warningTime)
        {
            ShowWarning();
            warningShown = true;
        }
        
        // Start damaging
        if (!isDamaging && timeInZone >= damageTime)
        {
            isDamaging = true;
            damageCoroutine = StartCoroutine(DamageOverTime());
        }
    }
    #endregion
    
    #region Damage
    private void ShowWarning()
    {
        Debug.Log($"[DeadEndZone] Warning player in dead end: {gameObject.name}");
        
        if (UIManager.Instance != null && !string.IsNullOrEmpty(warningMessage))
        {
            UIManager.Instance.ShowDialogue(warningMessage);
            StartCoroutine(AutoCloseDialogue(2f));
        }
    }
    
    private IEnumerator AutoCloseDialogue(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseDialogue();
        }
    }
    
    private IEnumerator DamageOverTime()
    {
        while (playerInside && isDamaging)
        {
            // Deal damage
            DealDamage();
            
            // Wait for interval
            yield return new WaitForSeconds(damageInterval);
        }
    }
    
    private void DealDamage()
    {
        Debug.Log($"[DeadEndZone] Damaging player in dead end: {gameObject.name}");
        
        // Show damage message
        if (UIManager.Instance != null && !string.IsNullOrEmpty(damageMessage))
        {
            UIManager.Instance.ShowDialogue(damageMessage);
            StartCoroutine(AutoCloseDialogue(1.5f));
        }
        
        // Apply damage
        HealthSystem health = FindFirstObjectByType<HealthSystem>();
        if (health != null)
        {
            health.TakeDamage(damageAmount);
        }
    }
    #endregion
    
    #region Visual Effects
    private IEnumerator TransitionFog(bool entering)
    {
        float duration = 1f;
        float elapsed = 0f;
        
        Color startColor = entering ? originalFogColor : fogColor;
        Color endColor = entering ? fogColor : originalFogColor;
        float startDensity = entering ? originalFogDensity : fogDensity;
        float endDensity = entering ? fogDensity : originalFogDensity;
        
        RenderSettings.fog = true;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            RenderSettings.fogColor = Color.Lerp(startColor, endColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(startDensity, endDensity, t);
            
            yield return null;
        }
        
        if (!entering)
        {
            RenderSettings.fog = originalFogEnabled;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
        }
    }
    #endregion
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 2f);
        }
    }
    #endregion
}


