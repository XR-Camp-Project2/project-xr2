using UnityEngine;
using System;
using TMPro;

public class UpdateTimes : MonoBehaviour
{
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI batteryText;

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
        Debug.Log(batteryPercentage);

        if (batteryText != null)
        {
            if (batteryPercentage >= 0 && batteryPercentage <= 1)
            {
                batteryText.text = "Battery: " + Mathf.RoundToInt(batteryPercentage * 100) + "%";
            }
            else
            {
                batteryText.text = "Battery: Unknown";
            }
        }
        else
        {
            Debug.Log((batteryPercentage >= 0 && batteryPercentage <= 1 ? Mathf.RoundToInt(batteryPercentage * 100) + "%" : "Unknown"));
        }
    }
}