using System.Collections;
using UnityEngine;

/// <summary>
/// Simple trigger that shows dialogue when player enters.
/// Dialogue is shown, and game continues after Continue is pressed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DialogueTrigger : MonoBehaviour
{
    #region Serialized Fields
    [Header("Dialogue Settings")]
    [Tooltip("Use two-part dialogue (first part, then second part after Continue)")]
    [SerializeField] private bool useTwoPartDialogue = false;
    
    [Header("Two-Part Dialogue")]
    [Tooltip("First part of dialogue - shown first, player presses Continue")]
    [TextArea(3, 6)]
    [SerializeField] private string firstPartDialogue;
    
    [Tooltip("Second part of dialogue - shown after Continue is pressed on first part")]
    [TextArea(3, 6)]
    [SerializeField] private string secondPartDialogue;
    
    [Header("Single/Multiple Dialogue (Alternative)")]
    [Tooltip("Single dialogue text to show (used if useTwoPartDialogue is false)")]
    [TextArea(2, 4)]
    [SerializeField] private string dialogueText;
    
    [Tooltip("Multiple dialogue messages (if set, will be used instead of dialogueText)")]
    [TextArea(2, 4)]
    [SerializeField] private string[] dialogueMessages;
    
    [Header("Trigger Settings")]
    [SerializeField] private bool triggerOnce = true; // Only trigger once
    [SerializeField] private bool isActive = true;
    
    [Header("Camera")]
    [Tooltip("Optional camera to transition to during dialogue")]
    [SerializeField] private Camera triggerCamera;
    [SerializeField] private float cameraTransitionDuration = 1.0f;
    
    [Header("Player Control")]
    [Tooltip("If true, player movement is disabled during dialogue")]
    [SerializeField] private bool disablePlayerMovement = true;
    #endregion
    
    #region Private Fields
    private Collider triggerCollider;
    private bool hasTriggered;
    private bool isProcessing;
    #endregion
    
    #region Initialization
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
    #endregion
    
    #region Trigger Detection
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (hasTriggered && triggerOnce) return;
        if (isProcessing) return;
        
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[DialogueTrigger] Player entered trigger: {gameObject.name}");
            StartCoroutine(ShowDialogueSequence());
        }
    }
    #endregion
    
    #region Dialogue Sequence
    private IEnumerator ShowDialogueSequence()
    {
        isProcessing = true;
        hasTriggered = true;
        
        // Disable player movement if needed
        if (disablePlayerMovement && GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Dialogue);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        // Camera transition if camera is set
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log($"[DialogueTrigger] Transitioning to trigger camera...");
            bool cameraTransitionComplete = false;
            CameraManager.Instance.TransitionToCamera(triggerCamera, cameraTransitionDuration, () =>
            {
                cameraTransitionComplete = true;
            });
            yield return new WaitUntil(() => cameraTransitionComplete);
        }
        
        // Prepare dialogue messages
        string[] messagesToShow = null;
        
        if (useTwoPartDialogue)
        {
            // Use two-part dialogue
            if (!string.IsNullOrEmpty(firstPartDialogue) && !string.IsNullOrEmpty(secondPartDialogue))
            {
                messagesToShow = new string[] { firstPartDialogue, secondPartDialogue };
                Debug.Log("[DialogueTrigger] Using two-part dialogue");
            }
            else
            {
                Debug.LogWarning($"[DialogueTrigger] useTwoPartDialogue is true but firstPartDialogue or secondPartDialogue is empty on {gameObject.name}");
            }
        }
        else if (dialogueMessages != null && dialogueMessages.Length > 0)
        {
            // Use dialogue messages array
            messagesToShow = dialogueMessages;
        }
        else if (!string.IsNullOrEmpty(dialogueText))
        {
            // Use single dialogue text
            messagesToShow = new string[] { dialogueText };
        }
        
        // Show dialogue if we have messages
        if (messagesToShow != null && messagesToShow.Length > 0 && UIManager.Instance != null)
        {
            Debug.Log($"[DialogueTrigger] Showing {messagesToShow.Length} dialogue message(s)");
            
            bool dialogueComplete = false;
            
            // Show dialogue sequence
            UIManager.Instance.ShowDialogueSequence(messagesToShow, () =>
            {
                dialogueComplete = true;
                Debug.Log("[DialogueTrigger] Dialogue sequence complete");
            });
            
            // Wait until dialogue is complete
            yield return new WaitUntil(() => dialogueComplete);
        }
        else
        {
            Debug.LogWarning($"[DialogueTrigger] No dialogue text or messages set on {gameObject.name}");
        }
        
        // Return camera to player if we transitioned
        if (triggerCamera != null && CameraManager.Instance != null)
        {
            Debug.Log("[DialogueTrigger] Returning to player camera...");
            bool cameraReturnComplete = false;
            CameraManager.Instance.TransitionToPlayerCamera(cameraTransitionDuration, () =>
            {
                cameraReturnComplete = true;
            });
            yield return new WaitUntil(() => cameraReturnComplete);
        }
        
        // Re-enable player movement
        if (disablePlayerMovement && GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Playing);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        isProcessing = false;
        Debug.Log("[DialogueTrigger] Dialogue sequence finished, player can continue");
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Manually trigger the dialogue (useful for external calls).
    /// </summary>
    public void TriggerDialogue()
    {
        if (!isActive) return;
        if (hasTriggered && triggerOnce) return;
        if (isProcessing) return;
        
        StartCoroutine(ShowDialogueSequence());
    }
    
    /// <summary>
    /// Reset the trigger so it can be triggered again.
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
        isProcessing = false;
    }
    
    /// <summary>
    /// Set the dialogue text programmatically.
    /// </summary>
    public void SetDialogueText(string text)
    {
        dialogueText = text;
        dialogueMessages = null;
        useTwoPartDialogue = false;
    }
    
    /// <summary>
    /// Set the dialogue messages array programmatically.
    /// </summary>
    public void SetDialogueMessages(string[] messages)
    {
        dialogueMessages = messages;
        dialogueText = null;
        useTwoPartDialogue = false;
    }
    
    /// <summary>
    /// Set two-part dialogue programmatically.
    /// </summary>
    public void SetTwoPartDialogue(string firstPart, string secondPart)
    {
        firstPartDialogue = firstPart;
        secondPartDialogue = secondPart;
        useTwoPartDialogue = true;
        dialogueText = null;
        dialogueMessages = null;
    }
    #endregion
    
    #region Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Green with transparency
        if (triggerCollider != null)
        {
            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Brighter green when selected
        if (triggerCollider != null)
        {
            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
    #endregion
}

