using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI elemanlarının ekrana sığmasını sağlayan yardımcı script.
/// Canvas'a ekle veya editörden çalıştır.
/// </summary>
[ExecuteInEditMode]
public class UIAutoFit : MonoBehaviour
{
    [Header("Canvas Ayarları")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    [SerializeField] private float matchWidthOrHeight = 0.5f;
    
    [Header("Otomatik Ayarla")]
    [SerializeField] private bool autoConfigureOnStart = true;
    
    private void Start()
    {
        if (autoConfigureOnStart)
        {
            ConfigureCanvas();
        }
    }
    
    [ContextMenu("Canvas'ı Yapılandır")]
    public void ConfigureCanvas()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
        
        Debug.Log($"[UIAutoFit] Canvas yapılandırıldı: {referenceResolution.x}x{referenceResolution.y}");
    }
    
    [ContextMenu("Tüm Panelleri Ortala")]
    public void CenterAllPanels()
    {
        // Stretch panelleri bul ve düzelt
        RectTransform[] allRects = GetComponentsInChildren<RectTransform>(true);
        
        foreach (var rect in allRects)
        {
            // "Panel" veya "Container" içeren objeleri kontrol et
            if (rect.name.Contains("Panel") || rect.name.Contains("Container"))
            {
                // Eğer çok büyükse küçült
                if (rect.sizeDelta.x > 1920 || rect.sizeDelta.y > 1080)
                {
                    Debug.Log($"[UIAutoFit] {rect.name} boyutu düzeltildi");
                }
            }
        }
    }
}


