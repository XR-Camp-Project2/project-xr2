using UnityEngine;
using UnityEngine.UI;

public class PanelButtons : MonoBehaviour
{
    [Header("資訊面板視窗")]
    [SerializeField] private GameObject panel;

    [Header("固定視窗")]
    [SerializeField] private GameObject fixedButton;
    [SerializeField] private Sprite fixedButtonOnSprite;
    [SerializeField] private Sprite fixedButtonOffSprite;

    [Header("餵食按鈕")]
    [SerializeField] private GameObject[] foodPrefabs;

    private GameObject petObject;
    private bool isFixed = false;

    public void Start()
    {
        petObject = GetComponent<Pet>().gameObject;    
    }

    public void onFixedButtonClicked()
    {
        // 切換固定資訊面板視窗

        Image buttonImage = fixedButton.GetComponent<Image>();
        if (!isFixed)
        {
            buttonImage.sprite = fixedButtonOnSprite;
            isFixed = true;
        }
        else
        {
            buttonImage.sprite = fixedButtonOffSprite;
            isFixed = false;
        }
    }
    
    public void onFollowButtonClicked()
    {
        // 讓寵物跟隨玩家（與招手手勢相同）
    }

    public void onFeedButtonClicked()
    {
        // 隨機挑選食物並生成食物物件

        int randomIndex = Random.Range(0, foodPrefabs.Length);
        GameObject selectedFood = foodPrefabs[randomIndex];
        // if (selectedFood != null)
        // {
        //     Instantiate(selectedFood, petObject.transform.position, Quaternion.identity);
        // }
    }

    public void onRestButtonClicked()
    {
        // 讓寵物直接消失
    }

    public void onCloseButtonClicked()
    {
        // 關閉資訊面板視窗
        
        panel.SetActive(false);
    }
}
