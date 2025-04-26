using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class UpdateTimes : MonoBehaviour
{
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI batteryText;

    private InputDevice headDevice;

    void Start()
    {
        headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
    }

    void Update()
    {
        UpdateTime();
        UpdateBattery();
    }

    void UpdateTime()
    {
        if (timeText != null){
            timeText.text = DateTime.Now.ToString("HH:mm");
        }
    }

    void UpdateBattery()
    {
        if (batteryText != null){
            if (!headDevice.isValid){
                headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            }

            if (headDevice.TryGetFeatureValue(CommonUsages.batteryLevel, out float batteryLevel)){
                batteryText.text = $"{(batteryLevel * 100f):F0}%";
            } else {
                batteryText.text = "N/A";
            }
        }
    }
}
