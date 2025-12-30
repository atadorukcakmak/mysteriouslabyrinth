using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Kamera geçişlerini yöneten singleton manager.
/// Trigger kameraları ile oyuncu kamerası arasında geçiş yapar.
/// </summary>
public class CameraManager : MonoBehaviour
{
    #region Singleton
    public static CameraManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion
    
    #region Serialized Fields
    [Header("Player Camera")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Oyuncu kamerasının parent'ı (CameraHolder veya Player)")]
    [SerializeField] private Transform playerCameraHolder;
    
    [Header("Transition Settings")]
    [SerializeField] private float defaultTransitionDuration = 1.0f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    #endregion
    
    #region Properties
    public bool IsTransitioning { get; private set; }
    public Camera CurrentActiveCamera { get; private set; }
    #endregion
    
    #region Private Fields
    private Coroutine transitionCoroutine;
    private Vector3 originalPlayerCameraPosition;
    private Quaternion originalPlayerCameraRotation;
    #endregion
    
    #region Initialization
    private void Start()
    {
        // Oyuncu kamerasını otomatik bul
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        
        if (playerCamera != null)
        {
            CurrentActiveCamera = playerCamera;
            
            // CameraHolder'ı PlayerController'dan al
            if (playerCameraHolder == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    playerCameraHolder = player.GetCameraHolder();
                }
                
                // Hala null ise parent'ı kullan
                if (playerCameraHolder == null)
                {
                    playerCameraHolder = playerCamera.transform.parent;
                }
            }
        }
        
        Debug.Log($"[CameraManager] Initialized. Player camera: {(playerCamera != null ? playerCamera.name : "NULL")}, " +
            $"CameraHolder: {(playerCameraHolder != null ? playerCameraHolder.name : "NULL")}");
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Belirtilen trigger kamerasına geçiş yapar.
    /// </summary>
    /// <param name="targetCamera">Hedef kamera</param>
    /// <param name="duration">Geçiş süresi (varsayılan kullanılır eğer 0 veya negatif)</param>
    /// <param name="onComplete">Geçiş tamamlandığında çağrılacak callback</param>
    public void TransitionToCamera(Camera targetCamera, float duration = -1f, Action onComplete = null)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("[CameraManager] Target camera is null, skipping transition");
            onComplete?.Invoke();
            return;
        }
        
        if (duration <= 0) duration = defaultTransitionDuration;
        
        // Mevcut geçişi durdur
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        
        transitionCoroutine = StartCoroutine(TransitionCoroutine(targetCamera, duration, onComplete));
    }
    
    /// <summary>
    /// Oyuncu kamerasına geri döner.
    /// </summary>
    /// <param name="duration">Geçiş süresi</param>
    /// <param name="onComplete">Geçiş tamamlandığında çağrılacak callback</param>
    public void TransitionToPlayerCamera(float duration = -1f, Action onComplete = null)
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("[CameraManager] Player camera is null!");
            onComplete?.Invoke();
            return;
        }
        
        if (duration <= 0) duration = defaultTransitionDuration;
        
        // Mevcut geçişi durdur
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        
        transitionCoroutine = StartCoroutine(ReturnToPlayerCoroutine(duration, onComplete));
    }
    
    /// <summary>
    /// Anında hedef kameraya geçiş yapar (animasyonsuz).
    /// </summary>
    public void SetCameraImmediate(Camera targetCamera)
    {
        if (targetCamera == null) return;
        
        // Oyuncu kamerasını deaktif et
        if (playerCamera != null && playerCamera != targetCamera)
        {
            playerCamera.enabled = false;
        }
        
        // Hedef kamerayı aktif et
        targetCamera.enabled = true;
        CurrentActiveCamera = targetCamera;
        
        Debug.Log($"[CameraManager] Immediate switch to: {targetCamera.name}");
    }
    
    /// <summary>
    /// Anında oyuncu kamerasına döner.
    /// </summary>
    public void ReturnToPlayerImmediate()
    {
        if (playerCamera == null) return;
        
        // Diğer kameraları kapat
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam != playerCamera)
            {
                cam.enabled = false;
            }
        }
        
        playerCamera.enabled = true;
        CurrentActiveCamera = playerCamera;
        
        Debug.Log("[CameraManager] Immediate return to player camera");
    }
    #endregion
    
    #region Transition Coroutines
    private IEnumerator TransitionCoroutine(Camera targetCamera, float duration, Action onComplete)
    {
        IsTransitioning = true;
        
        Debug.Log($"[CameraManager] Starting transition to {targetCamera.name} over {duration}s");
        
        // Kaynak pozisyon ve rotasyonu kaydet
        Vector3 startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;
        
        // Hedef pozisyon ve rotasyon
        Vector3 endPos = targetCamera.transform.position;
        Quaternion endRot = targetCamera.transform.rotation;
        
        // Oyuncu kamera kontrolünü devre dışı bırak
        DisablePlayerCameraControl();
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / duration);
            
            // Kamerayı hareket ettir
            playerCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            
            yield return null;
        }
        
        // Son pozisyona yerleştir
        playerCamera.transform.position = endPos;
        playerCamera.transform.rotation = endRot;
        
        CurrentActiveCamera = targetCamera;
        IsTransitioning = false;
        
        Debug.Log($"[CameraManager] Transition to {targetCamera.name} complete");
        
        onComplete?.Invoke();
    }
    
    private IEnumerator ReturnToPlayerCoroutine(float duration, Action onComplete)
    {
        IsTransitioning = true;
        
        Debug.Log($"[CameraManager] Returning to player camera over {duration}s");
        
        // Şu anki pozisyon
        Vector3 startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;
        
        // Oyuncu kamerasının olması gereken pozisyon
        // PlayerCameraHolder varsa onun pozisyonuna dön
        Vector3 endPos;
        Quaternion endRot;
        
        if (playerCameraHolder != null)
        {
            // CameraHolder'ın local pozisyonuna göre hesapla
            endPos = playerCameraHolder.position;
            endRot = playerCameraHolder.rotation;
        }
        else
        {
            // Oyuncu pozisyonunu bul
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                endPos = player.transform.position + Vector3.up * 1.6f; // Göz hizası
                endRot = player.transform.rotation;
            }
            else
            {
                endPos = startPos;
                endRot = startRot;
            }
        }
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / duration);
            
            // Hedef pozisyonu güncelle (oyuncu hareket edebilir)
            if (playerCameraHolder != null)
            {
                endPos = playerCameraHolder.position;
                endRot = playerCameraHolder.rotation;
            }
            
            // Kamerayı hareket ettir
            playerCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            
            yield return null;
        }
        
        // Oyuncu kamera kontrolünü geri aç
        EnablePlayerCameraControl();
        
        CurrentActiveCamera = playerCamera;
        IsTransitioning = false;
        
        Debug.Log("[CameraManager] Return to player camera complete");
        
        onComplete?.Invoke();
    }
    #endregion
    
    #region Camera Control
    private void DisablePlayerCameraControl()
    {
        // Oyuncunun kamera kontrolünü kapat
        // Cinemachine veya custom camera controller varsa burada devre dışı bırak
        
        // Örnek: PlayerController'daki look kontrolünü kapat
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.SetLookEnabled(false);
        }
    }
    
    private void EnablePlayerCameraControl()
    {
        // Oyuncunun kamera kontrolünü aç
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.SetLookEnabled(true);
        }
        
        // Kamerayı parent'a geri bağla
        if (playerCameraHolder != null && playerCamera != null)
        {
            playerCamera.transform.SetParent(playerCameraHolder);
            playerCamera.transform.localPosition = Vector3.zero;
            playerCamera.transform.localRotation = Quaternion.identity;
        }
    }
    #endregion
}

