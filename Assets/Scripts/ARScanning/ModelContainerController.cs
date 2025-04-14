using UnityEngine;
using Vuforia;

public class ModelContainerController : MonoBehaviour
{
    [SerializeField] private ObserverBehaviour imageTarget;
    [SerializeField] private bool enableModel = true; // default to true, can be set in the inspector
    
    void Start()
    {
        if (imageTarget == null)
        {
            Debug.LogError("Please assign an ImageTargetBehaviour to the ModelContainerController script.");
            return;
        }
        
        // hide the model at the start
        gameObject.SetActive(false);
        
        // register to the OnTargetStatusChanged event
        imageTarget.OnTargetStatusChanged += OnTargetStatusChanged;
    }
    
    void OnDestroy()
    {
        if (imageTarget != null)
        {
            imageTarget.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }
    
    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool isTracking = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;
        gameObject.SetActive(isTracking && enableModel); 
    }
    
    // use this method to enable or disable the model
    public void SetModelEnabled(bool enabled)
    {
        enableModel = enabled;
        if (!enableModel)
        {
            gameObject.SetActive(false); 
        }
    }
}