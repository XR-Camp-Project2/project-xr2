using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class MCPObjectRead : MonoBehaviour {
    [Header("預設選取的物件名稱")]
    public string selectedObjectName = "MysticCube";

    [Header("顯示描述的 UI 元件")]
    public Text descriptionText;
    [Header("文字輸入框")]
    public InputField inputField;

    void Start() {
        StartCoroutine(RequestLLMDescription(selectedObjectName));
    }

    public void OnUpdateText(){
        if (inputField != null){
            selectedObjectName = inputField.text;
            Debug.Log("使用者輸入: " + selectedObjectName);
            StartCoroutine(RequestLLMDescription(selectedObjectName));
        }
    }

    IEnumerator RequestLLMDescription(string objectName) {
        string url = "http://localhost:3000/describe";
        WWWForm form = new WWWForm();
        form.AddField("name", objectName);

        using (UnityWebRequest req = UnityWebRequest.Post(url, form)) {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success) {
                string description = req.downloadHandler.text;
                ShowDescription(description);
                Debug.Log("LLM: " + description);
            } else {
                Debug.LogError("LLM 回應失敗: " + req.error);
                ShowDescription("無法取得描述...");
            }
        }
    }

    void ShowDescription(string text) {
        if (descriptionText != null) {
            descriptionText.text = text;
        } else {
            Debug.LogWarning("尚未指派 descriptionText");
        }
    }
}
