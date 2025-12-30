using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages environment transformation: material swapping, vegetation growth, etc.
/// Handles the "revitalization" effect when players answer correctly.
/// Singleton pattern.
/// </summary>
public class EnvironmentManager : MonoBehaviour
{
    #region Singleton
    public static EnvironmentManager Instance { get; private set; }
    
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
    [Header("Default Materials (Cold/Lifeless)")]
    [SerializeField] private Material wallGreyMaterial;
    [SerializeField] private Material floorDirtMaterial;
    [SerializeField] private Material treeBarkMaterial;
    
    [Header("Revitalized Materials (Success)")]
    [SerializeField] private Material wallLeafyMaterial;
    [SerializeField] private Material floorGrassMaterial;
    [SerializeField] private Material treeLeavesMaterial;
    
    [Header("Final Celebration Materials")]
    [SerializeField] private Material flowerMaterial;
    
    [Header("Transformation Settings")]
    [SerializeField] private float transformationDuration = 1.5f;
    [SerializeField] private float zoneTransformDelay = 0.2f;
    
    [Header("Tags")]
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private string floorTag = "Floor";
    [SerializeField] private string treeTag = "Tree";
    [SerializeField] private string zoneTag = "EnvironmentZone";
    #endregion
    
    #region Private Fields
    private Dictionary<int, bool> transformedZones = new Dictionary<int, bool>();
    private List<EnvironmentZone> allZones = new List<EnvironmentZone>();
    private int totalTransformedCount = 0;
    #endregion
    
    #region Initialization
    private void Start()
    {
        // Find all environment zones in the scene
        RefreshZonesList();
    }
    
    /// <summary>
    /// Refreshes the list of environment zones in the scene.
    /// Call this after loading a new scene.
    /// </summary>
    public void RefreshZonesList()
    {
        allZones.Clear();
        transformedZones.Clear();
        totalTransformedCount = 0;
        
        EnvironmentZone[] zones = FindObjectsByType<EnvironmentZone>(FindObjectsSortMode.None);
        allZones.AddRange(zones);
        
        Debug.Log($"[EnvironmentManager] Found {allZones.Count} environment zones");
    }
    #endregion
    
    #region Zone Transformation
    /// <summary>
    /// Transforms a specific environment zone to the revitalized state.
    /// </summary>
    public void TransformZone(int zoneId)
    {
        if (transformedZones.ContainsKey(zoneId) && transformedZones[zoneId])
        {
            Debug.Log($"[EnvironmentManager] Zone {zoneId} already transformed");
            return;
        }
        
        EnvironmentZone zone = allZones.Find(z => z.ZoneId == zoneId);
        if (zone != null)
        {
            StartCoroutine(TransformZoneCoroutine(zone));
            transformedZones[zoneId] = true;
            totalTransformedCount++;
        }
        else
        {
            Debug.LogWarning($"[EnvironmentManager] Zone {zoneId} not found");
        }
    }
    
    /// <summary>
    /// Transforms all zones connected to a trigger point.
    /// </summary>
    public void TransformNearbyZones(Vector3 position, float radius = 10f)
    {
        foreach (var zone in allZones)
        {
            if (zone == null) continue;
            
            float distance = Vector3.Distance(position, zone.transform.position);
            if (distance <= radius && !IsZoneTransformed(zone.ZoneId))
            {
                TransformZone(zone.ZoneId);
            }
        }
    }
    
    private IEnumerator TransformZoneCoroutine(EnvironmentZone zone)
    {
        if (zone == null) yield break;
        
        Debug.Log($"[EnvironmentManager] Transforming zone {zone.ZoneId}");
        
        // Get all renderers in the zone
        Renderer[] renderers = zone.GetComponentsInChildren<Renderer>();
        
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            
            // Determine material swap based on tag
            Material newMaterial = GetRevitalizedMaterial(renderer.gameObject);
            if (newMaterial != null)
            {
                StartCoroutine(LerpMaterial(renderer, newMaterial, transformationDuration));
            }
            
            yield return new WaitForSeconds(zoneTransformDelay);
        }
        
        // Activate any hidden vegetation/decorations
        zone.ActivateVegetation();
    }
    
    private Material GetRevitalizedMaterial(GameObject obj)
    {
        if (obj.CompareTag(wallTag) || obj.name.Contains("Wall"))
        {
            return wallLeafyMaterial;
        }
        else if (obj.CompareTag(floorTag) || obj.name.Contains("Floor"))
        {
            return floorGrassMaterial;
        }
        else if (obj.CompareTag(treeTag) || obj.name.Contains("Tree"))
        {
            return treeLeavesMaterial;
        }
        
        return null;
    }
    
    private IEnumerator LerpMaterial(Renderer renderer, Material targetMaterial, float duration)
    {
        if (renderer == null || targetMaterial == null) yield break;
        
        Material startMaterial = renderer.material;
        float elapsed = 0f;
        
        // Create a temporary material for lerping
        Material tempMaterial = new Material(startMaterial);
        renderer.material = tempMaterial;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Lerp color
            if (tempMaterial.HasProperty("_BaseColor") && targetMaterial.HasProperty("_BaseColor"))
            {
                Color startColor = startMaterial.GetColor("_BaseColor");
                Color endColor = targetMaterial.GetColor("_BaseColor");
                tempMaterial.SetColor("_BaseColor", Color.Lerp(startColor, endColor, t));
            }
            else if (tempMaterial.HasProperty("_Color") && targetMaterial.HasProperty("_Color"))
            {
                Color startColor = startMaterial.GetColor("_Color");
                Color endColor = targetMaterial.GetColor("_Color");
                tempMaterial.SetColor("_Color", Color.Lerp(startColor, endColor, t));
            }
            
            yield return null;
        }
        
        // Apply final material
        renderer.material = targetMaterial;
    }
    
    public bool IsZoneTransformed(int zoneId)
    {
        return transformedZones.ContainsKey(zoneId) && transformedZones[zoneId];
    }
    #endregion
    
    #region Direct Object Transformation
    /// <summary>
    /// Transforms a specific object's material to revitalized state.
    /// </summary>
    public void TransformObject(GameObject obj, bool animate = true)
    {
        if (obj == null) return;
        
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        
        Material newMaterial = GetRevitalizedMaterial(obj);
        if (newMaterial == null) return;
        
        if (animate)
        {
            StartCoroutine(LerpMaterial(renderer, newMaterial, transformationDuration));
        }
        else
        {
            renderer.material = newMaterial;
        }
    }
    
    /// <summary>
    /// Resets an object to its default lifeless material.
    /// </summary>
    public void ResetObject(GameObject obj)
    {
        if (obj == null) return;
        
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        
        Material defaultMaterial = GetDefaultMaterial(obj);
        if (defaultMaterial != null)
        {
            renderer.material = defaultMaterial;
        }
    }
    
    private Material GetDefaultMaterial(GameObject obj)
    {
        if (obj.CompareTag(wallTag) || obj.name.Contains("Wall"))
        {
            return wallGreyMaterial;
        }
        else if (obj.CompareTag(floorTag) || obj.name.Contains("Floor"))
        {
            return floorDirtMaterial;
        }
        else if (obj.CompareTag(treeTag) || obj.name.Contains("Tree"))
        {
            return treeBarkMaterial;
        }
        
        return null;
    }
    #endregion
    
    #region Final Celebration
    /// <summary>
    /// Triggers the final celebration effect - transforms entire labyrinth.
    /// </summary>
    public void TriggerFinalCelebration()
    {
        StartCoroutine(FinalCelebrationCoroutine());
    }
    
    private IEnumerator FinalCelebrationCoroutine()
    {
        Debug.Log("[EnvironmentManager] Final Celebration - Transforming entire labyrinth!");
        
        // Transform all remaining zones
        foreach (var zone in allZones)
        {
            if (zone != null && !IsZoneTransformed(zone.ZoneId))
            {
                StartCoroutine(TransformZoneCoroutine(zone));
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        yield return new WaitForSeconds(1f);
        
        // Spawn flowers and birds (placeholder - activate pre-placed objects)
        SpawnCelebrationEffects();
    }
    
    private void SpawnCelebrationEffects()
    {
        // Find and activate celebration objects
        GameObject[] celebrationObjects = GameObject.FindGameObjectsWithTag("Celebration");
        foreach (var obj in celebrationObjects)
        {
            obj.SetActive(true);
        }
        
        Debug.Log("[EnvironmentManager] Celebration effects activated!");
    }
    #endregion
    
    #region Utility
    /// <summary>
    /// Resets all zones to default state.
    /// </summary>
    public void ResetAllZones()
    {
        transformedZones.Clear();
        totalTransformedCount = 0;
        
        foreach (var zone in allZones)
        {
            if (zone != null)
            {
                zone.ResetZone();
            }
        }
    }
    
    public int GetTransformedZoneCount()
    {
        return totalTransformedCount;
    }
    
    public int GetTotalZoneCount()
    {
        return allZones.Count;
    }
    #endregion
}

/// <summary>
/// Component attached to environment zone objects.
/// </summary>
public class EnvironmentZone : MonoBehaviour
{
    [SerializeField] private int zoneId;
    [SerializeField] private GameObject[] hiddenVegetation;
    
    public int ZoneId => zoneId;
    
    /// <summary>
    /// Activates hidden vegetation when zone is transformed.
    /// </summary>
    public void ActivateVegetation()
    {
        if (hiddenVegetation == null) return;
        
        foreach (var obj in hiddenVegetation)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }
    
    /// <summary>
    /// Resets the zone to default state.
    /// </summary>
    public void ResetZone()
    {
        if (hiddenVegetation == null) return;
        
        foreach (var obj in hiddenVegetation)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
}


