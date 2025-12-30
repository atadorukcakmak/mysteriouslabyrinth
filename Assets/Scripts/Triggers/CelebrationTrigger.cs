using System.Collections;
using UnityEngine;

/// <summary>
/// Triggers final celebration effects when the game is completed.
/// Attach to a trigger at the end or call manually from GameManager.
/// </summary>
public class CelebrationTrigger : MonoBehaviour
{
    #region Serialized Fields
    [Header("Celebration Objects")]
    [SerializeField] private GameObject[] flowerObjects;
    [SerializeField] private GameObject[] birdObjects;
    [SerializeField] private GameObject[] lightObjects;
    [SerializeField] private ParticleSystem[] celebrationParticles;
    
    [Header("Timing")]
    [SerializeField] private float spawnDelay = 0.2f;
    [SerializeField] private float celebrationDuration = 10f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip celebrationMusic;
    [SerializeField] private AudioClip victorySound;
    
    [Header("Camera Effects")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeIntensity = 0.1f;
    [SerializeField] private float shakeDuration = 0.5f;
    #endregion
    
    #region Private Fields
    private bool hasTriggered;
    private AudioSource audioSource;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Hide all celebration objects initially
        HideAllCelebrationObjects();
    }
    
    private void Start()
    {
        // Subscribe to game won event
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameWon += TriggerCelebration;
        }
    }
    
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameWon -= TriggerCelebration;
        }
    }
    
    private void HideAllCelebrationObjects()
    {
        SetObjectsActive(flowerObjects, false);
        SetObjectsActive(birdObjects, false);
        SetObjectsActive(lightObjects, false);
    }
    
    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
    #endregion
    
    #region Trigger
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            TriggerCelebration();
        }
    }
    
    /// <summary>
    /// Manually trigger the celebration (called by GameManager on victory).
    /// </summary>
    public void TriggerCelebration()
    {
        if (hasTriggered) return;
        hasTriggered = true;
        
        Debug.Log("[CelebrationTrigger] Starting celebration!");
        
        StartCoroutine(CelebrationSequence());
    }
    #endregion
    
    #region Celebration Sequence
    private IEnumerator CelebrationSequence()
    {
        // Play victory sound
        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound);
        }
        
        // Camera shake
        if (enableCameraShake)
        {
            StartCoroutine(CameraShake());
        }
        
        // Transform entire environment
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.TriggerFinalCelebration();
        }
        
        yield return new WaitForSeconds(1f);
        
        // Spawn flowers with delay
        yield return StartCoroutine(SpawnObjectsSequentially(flowerObjects));
        
        // Spawn birds
        yield return StartCoroutine(SpawnObjectsSequentially(birdObjects));
        
        // Activate lights
        SetObjectsActive(lightObjects, true);
        
        // Play particles
        PlayAllParticles();
        
        // Play celebration music
        if (celebrationMusic != null && audioSource != null)
        {
            audioSource.clip = celebrationMusic;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        // Show victory message
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowDialogue("Congratulations! You have collected all the holy books and completed the Mysterious Labyrinth!");
        }
        
        yield return new WaitForSeconds(celebrationDuration);
        
        // Celebration complete - game will show victory screen via GameManager
    }
    
    private IEnumerator SpawnObjectsSequentially(GameObject[] objects)
    {
        if (objects == null) yield break;
        
        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                
                // Optional: Add spawn animation
                StartCoroutine(SpawnAnimation(obj));
                
                yield return new WaitForSeconds(spawnDelay);
            }
        }
    }
    
    private IEnumerator SpawnAnimation(GameObject obj)
    {
        Vector3 targetScale = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;
        
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Elastic ease out
            float scale = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 2f);
            obj.transform.localScale = targetScale * scale;
            
            yield return null;
        }
        
        obj.transform.localScale = targetScale;
    }
    
    private void PlayAllParticles()
    {
        if (celebrationParticles == null) return;
        
        foreach (var ps in celebrationParticles)
        {
            if (ps != null) ps.Play();
        }
    }
    
    private IEnumerator CameraShake()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        
        Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float strength = shakeIntensity * (1f - elapsed / shakeDuration);
            
            cam.transform.localPosition = originalPos + Random.insideUnitSphere * strength;
            
            yield return null;
        }
        
        cam.transform.localPosition = originalPos;
    }
    #endregion
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.84f, 0f, 0.5f); // Gold
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
    #endregion
}


