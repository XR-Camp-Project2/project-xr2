using UnityEngine;

public class Display : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    public void showPanel() {
        panel.SetActive(true);
    }

    public void hidePanel() {
        panel.SetActive(false);
    }
}
