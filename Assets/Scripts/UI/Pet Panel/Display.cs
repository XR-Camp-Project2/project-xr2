using Meta.XR.ImmersiveDebugger.UserInterface.Generic;
using UnityEngine;

public class Display : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private PanelButtons panelButtons;
    private const float distance = 0.5f;


    void LateUpdate()
    {   
        if (!panelButtons.isPanelFixed()) {
            panel.transform.position = Camera.main.transform.position + Camera.main.transform.forward * distance;
            panel.transform.LookAt(Camera.main.transform.position);
        }
    }

    public void showPanel() {
        panel.transform.position = Camera.main.transform.position + Camera.main.transform.forward * distance;
        panel.transform.LookAt(Camera.main.transform.position);
        panel.SetActive(true);
    }

    public void hidePanel() {
        panel.SetActive(false);
    }
}
