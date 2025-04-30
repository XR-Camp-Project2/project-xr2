using Oculus.Platform;
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
        var pet = GameObject.FindGameObjectWithTag("Pet")?.GetComponent<Pet>();
        if(pet == null)
        {
            Debug.LogError("Pet not found in the scene.");
            return;
        }
        pet.TriggerFollow();
    }

    public void onFeedButtonClicked()
    {
        // 隨機挑選食物並生成食物物件

        int randomIndex = Random.Range(0, foodPrefabs.Length);
        GameObject selectedFood = foodPrefabs[randomIndex];
        if (selectedFood != null)
        {
            Transform panelTransform = transform.parent.gameObject.transform;
            GameObject food = Instantiate(selectedFood, panelTransform.position, Quaternion.identity);
        }
    }

    public void onRestButtonClicked()
    {
        // 播退場動畫
        

        // 刪除寵物物件
        if (GameObject.FindGameObjectWithTag("Pet") != null)
        {
            petObject = GameObject.FindGameObjectWithTag("Pet");
        }
        else
        {
            return;
        }

        Destroy(petObject);
    }

    public void onCloseButtonClicked()
    {
        // 關閉資訊面板視窗
        panel.SetActive(false);
    }

    public bool isPanelFixed() {
        return isFixed;
    }

    public void setPanelFixed(bool fixedState) {
        isFixed = fixedState;
        Image buttonImage = fixedButton.GetComponent<Image>();
        if (fixedState) {
            buttonImage.sprite = fixedButtonOnSprite;
        } else {
            buttonImage.sprite = fixedButtonOffSprite;
        }
    }
}
