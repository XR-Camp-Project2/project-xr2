using UnityEngine;
using System;
using TMPro;

public class UpdateTimes : MonoBehaviour
{
    [Header("時間顯示文字")]
    [SerializeField] private TextMeshProUGUI timeText;
    [Header("電量顯示文字與物件")]
    [SerializeField] private TextMeshProUGUI batteryText;
    [SerializeField] private GameObject batteryObjectCharged;
    [SerializeField] private GameObject[] batteryMasks = new GameObject[5];

    void Start()
    {
        if (timeText == null)
        {
            Debug.LogError("timeText TextMeshProUGUI is not assigned in the Inspector!");
        }
        if (batteryText == null)
        {
            Debug.LogError("batteryText TextMeshProUGUI is not assigned in the Inspector!");
        }
    }

    void Update()
    {
        UpdateTimeDisplay();
        UpdateBatteryDisplay();
    }

    void UpdateTimeDisplay()
    {
        if (timeText != null)
        {
            timeText.text = DateTime.Now.ToString("HH:mm");
        }
    }

    void UpdateBatteryDisplay()
    {
        float batteryPercentage = SystemInfo.batteryLevel;


        if (batteryText != null)
        {
            if (batteryPercentage >= 0 && batteryPercentage <= 1)
            {
                batteryText.text = Mathf.RoundToInt(batteryPercentage * 100) + "%";
                UpdateBatteryIcon((int)(batteryPercentage*100));
            }
            else
            {
                batteryText.text = "0%";
            }
        }
        else
        {
            Debug.Log((batteryPercentage >= 0 && batteryPercentage <= 1 ? Mathf.RoundToInt(batteryPercentage * 100) + "%" : "Unknown"));
        }
    }

    void UpdateBatteryIcon(int batteryValue)
    {
        for (int i = 0; i < batteryMasks.Length; i++)
        {
            var mask = batteryMasks[i];
            if (batteryValue - (i+1)*20 >= 0)
            {
                mask.SetActive(false);
            }
            else
            {
                mask.SetActive(true);
            }
        }
    }
}