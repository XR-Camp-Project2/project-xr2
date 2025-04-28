using Meta.XR.ImmersiveDebugger.UserInterface.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

public class Display : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private PanelButtons panelButtons;
    private const float distance = 0.05f;
    private Vector3 velocity = Vector3.zero;

    XRHandTrackingEvents handTrackingEvents;
    private int handedness = 0; // 0: None, 1: Left, 2: Right

    public Transform leftHandTransform;
    public Transform rightHandTransform;

    void LateUpdate()
    {   
        if (panelButtons.isPanelFixed()) return;

        Vector3 newPosition = Vector3.zero;
        Quaternion newRotation = Quaternion.identity;
        if (handedness == 1) {
            newPosition = leftHandTransform.position;
        } 
        else if (handedness == 2) {
            newPosition = rightHandTransform.position;
        } 
        else {
            return;
        }

        newPosition -= (newPosition - Camera.main.transform.position).normalized * distance;
        newRotation = Quaternion.LookRotation(Camera.main.transform.position - newPosition, Vector3.up);

        panel.transform.position = Vector3.SmoothDamp(panel.transform.position, newPosition, ref velocity, 0.1f);
        panel.transform.rotation = Quaternion.Slerp(panel.transform.rotation, newRotation, Time.deltaTime * 10f);
    }

    public void ShowPanel(int handedness) {
        if (handedness == 0) return;
        if (this.handedness > 0) return;

        panelButtons.setPanelFixed(false);
        this.handedness = handedness;

        Vector3 newPosition = Vector3.zero;
        if (handedness == 1) {
            newPosition = leftHandTransform.position;
        } 
        else if (handedness == 2) {
            newPosition = rightHandTransform.position;
        }

        panel.transform.position = newPosition - 
            (newPosition - Camera.main.transform.position).normalized * distance;
        panel.transform.LookAt(Camera.main.transform.position);
        velocity = Vector3.zero; // Reset velocity to avoid jittering
        
        panel.SetActive(true);
    }

    public void HidePanel() {
        handedness = 0;
        panel.SetActive(false);
    }
}
