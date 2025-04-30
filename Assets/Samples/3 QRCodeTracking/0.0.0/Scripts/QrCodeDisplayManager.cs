using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Meta.XR;
using PassthroughCameraSamples;

/// <summary>
/// 增強型 QR 碼識別與角色顯示管理系統
/// 處理 Quest Passthrough Camera API 的 QR 碼追蹤、空間定位與角色生成管線
/// </summary>
public class QrCodeDisplayManager : MonoBehaviour
{
#if ZXING_ENABLED
    [Header("核心系統引用")]
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private QrCodeScanner scanner;
    [SerializeField] private EnvironmentRaycastManager envRaycastManager;
    [SerializeField] private WebCamTextureManager passthroughCameraManager;

    [Header("QR 碼檢測與穩定性參數")]
    [SerializeField] private float qrDetectionStabilityThreshold = 0.5f; // 穩定性閾值，秒
    [SerializeField] private float markerVisibilityTimeout = 2.0f; // 標記可見性超時，秒
    [SerializeField] private float animationCompletionTimeout = 5.0f; // 動畫完成超時保護，秒

    [Header("進階調試選項")]
    [SerializeField] private bool enableDiagnostics = true;
    [SerializeField] private bool simulationModeInEditor = true;

    /// <summary>
    /// 角色狀態追蹤枚舉，實現精確的生命週期管理
    /// </summary>
    private enum CharacterState
    {
        NotPresent,    // 角色不存在
        Spawning,      // 角色正在生成/播放出場動畫
        Active,        // 角色已完全生成且處於活躍狀態
        Despawning     // 角色正在消失
    }

    // 系統狀態與追蹤變數
    private GameObject _spawnedCharacter;
    private CharacterState _characterState = CharacterState.NotPresent;
    private bool _isWaitingForAnimation = false;
    private string _lastDetectedQrCode = null;
    private float _qrStabilityTimer = 0f;
    private Stopwatch _animationStopwatch = new Stopwatch();

    // 系統引用與組件緩存
    private readonly Dictionary<string, MarkerController> _activeMarkers = new();
    private PassthroughCameraEye _passthroughCameraEye;

    /// <summary>
    /// 初始化系統引用與狀態追蹤
    /// </summary>
    private void Awake()
    {
        if (passthroughCameraManager == null)
        {
            LogError("Passthrough Camera Manager reference missing。請在 Inspector 中設置。");
            enabled = false;
            return;
        }

        if (scanner == null)
        {
            LogError("QR Code Scanner 引用缺失。請在 Inspector 中設置。");
            enabled = false;
            return;
        }

        if (envRaycastManager == null)
        {
            LogError("Environment Raycast Manager 引用缺失。請在 Inspector 中設置。");
            enabled = false;
            return;
        }

        if (characterPrefab == null)
        {
            LogError("Character Prefab 引用缺失。請在 Inspector 中設置。");
            enabled = false;
            return;
        }

        _passthroughCameraEye = passthroughCameraManager.Eye;
        LogInfo("QR code display manager initialized。");

        // 編輯器模式檢測
        #if UNITY_EDITOR
        if (simulationModeInEditor)
        {
            LogWarning("Simulation mode enabled。Passthrough Camera API just for reference。");
        }
        #endif
    }

    /// <summary>
    /// 主要更新循環，處理 QR 碼檢測與角色生命週期管理
    /// </summary>
    private void Update()
    {
        // 編輯器模式下的模擬輸入處理
        #if UNITY_EDITOR
        if (simulationModeInEditor && Input.GetKeyDown(KeyCode.Space))
        {
            SimulateQrCodeDetection();
            return;
        }
        #endif

        // 在裝置上正常執行 QR 碼更新邏輯
        UpdateMarkers();

        // 動畫超時保護機制
        if (_isWaitingForAnimation && _animationStopwatch.IsRunning && 
            _animationStopwatch.ElapsedMilliseconds > animationCompletionTimeout * 1000)
        {
            LogWarning($"Animation timed out ({animationCompletionTimeout} seconds) Forcing character activation。");
            _animationStopwatch.Stop();
            _isWaitingForAnimation = false;
            _characterState = CharacterState.Active;
        }
    }

    /// <summary>
    /// 主要 QR 碼檢測與處理管線
    /// 實現完整的 QR 碼空間追蹤、穩定性判斷與角色生成管理
    /// </summary>
    private async void UpdateMarkers()
    {
        // 獲取 QR 碼掃描結果
        var qrResults = await scanner.ScanFrameAsync();
        if (qrResults == null || qrResults.Length == 0)
        {
            return;
        }

        // QR 碼穩定性追蹤與角色生成管理
        ProcessQrCodeResults(qrResults);

        // 處理標記系統更新
        ProcessMarkerUpdates(qrResults);

        // 處理無 QR 碼情況
        if (qrResults.Length == 0)
        {
            HandleNoQrCodesDetected();
        }

        // 清理無效標記
        CleanupInactiveMarkers();
    }

    /// <summary>
    /// 處理 QR 碼檢測結果的穩定性追蹤與角色生成邏輯
    /// </summary>
    private void ProcessQrCodeResults(QrCodeResult[] qrResults)
    {
        if(qrResults == null || qrResults.Length == 0)
        {
            _lastDetectedQrCode = null;
            _qrStabilityTimer = 0f;
            return;
        }

        string currentQrText = qrResults[0].text;
        LogDebug($"QR code detected: {currentQrText}");
        LogDebug(_lastDetectedQrCode != null ? $"Last QR code: {_lastDetectedQrCode}" : "No previous QR code detected.");


        if(_lastDetectedQrCode == null)
        {
            _lastDetectedQrCode = currentQrText;
            _qrStabilityTimer = 0f;
            LogDebug($"First QR detection: {currentQrText}");
            return;
        }

        if (currentQrText != _lastDetectedQrCode)
        {
            LogDebug($"New QR detected. Old: '{_lastDetectedQrCode}', New: '{currentQrText}'");
            _lastDetectedQrCode = currentQrText;
            _qrStabilityTimer = 0f;
            return;
        }


        _qrStabilityTimer += Time.deltaTime;
        LogDebug($"Same QR detected. Timer: {_qrStabilityTimer:F2}s");
        
        if (_qrStabilityTimer >= qrDetectionStabilityThreshold && 
            _characterState == CharacterState.NotPresent)
        {
            TriggerCharacterSpawn(qrResults[0]);
        }
        
    }

    /// <summary>
    /// 處理標記系統的更新與空間映射
    /// </summary>
    private void ProcessMarkerUpdates(QrCodeResult[] qrResults)
    {
        foreach (var qrResult in qrResults)
        {
            if (qrResult?.corners == null || qrResult.corners.Length < 4)
            {
                continue;
            }

            // 計算 QR 碼在相機圖像中的紋理坐標
            var uvs = CalculateTextureCoordinates(qrResult.corners);
            var centerUV = CalculateCenterPoint(uvs);

            // 計算 QR 碼在 3D 空間中的位置與方向
            if (!CalculateMarkerTransform(centerUV, uvs, out Vector3 center, out Quaternion poseRot, out Vector3 scale))
            {
                continue;
            }

            // 更新或創建對應的標記
            UpdateOrCreateMarker(qrResult.text, center, poseRot, scale);
        }
    }

    /// <summary>
    /// 處理未檢測到 QR 碼的情況
    /// </summary>
    private void HandleNoQrCodesDetected()
    {
        // 如果角色正在生成但尚未完成動畫且 QR 碼消失，考慮取消角色生成
        if (_characterState == CharacterState.Spawning && _spawnedCharacter != null && !_isWaitingForAnimation)
        {
            LogInfo("QR code disappeared, canceling spawning animation.");
            CancelSpawningAnimation();
        }
    }

    /// <summary>
    /// 清理不再活躍的標記
    /// </summary>
    private void CleanupInactiveMarkers()
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _activeMarkers)
        {
            if (!kvp.Value.gameObject.activeSelf)
                keysToRemove.Add(kvp.Key);
        }
        
        foreach (var key in keysToRemove)
        {
            _activeMarkers.Remove(key);
        }
    }

    /// <summary>
    /// 根據 QR 碼結果觸發角色生成
    /// </summary>
    private void TriggerCharacterSpawn(QrCodeResult qrResult)
    {
        if (_characterState != CharacterState.NotPresent || _isWaitingForAnimation)
        {
            LogWarning($"DC. Cannot spawn character. Current state: {_characterState}, waiting for animation: {_isWaitingForAnimation}");
            return;
        }

        // 計算 QR 碼在 3D 空間中的位置與方向
        var uvs = CalculateTextureCoordinates(qrResult.corners);
        var centerUV = CalculateCenterPoint(uvs);

        if (!CalculateMarkerTransform(centerUV, uvs, out Vector3 center, out Quaternion poseRot, out Vector3 _))
        {
            LogError("Cannot calculate marker transform. QR code may be out of view.");
            return;
        }

        LogInfo($"Spawning character at {center} generate character for QR code '{qrResult.text}'");
        _characterState = CharacterState.Spawning;
        
        // 實例化角色
        _spawnedCharacter = Instantiate(characterPrefab, center, Quaternion.identity);
        
        // 獲取動畫器並開始播放動畫
        var animator = _spawnedCharacter.GetComponent<Animator>();
        if (animator != null)
        {
            LogInfo("starting character spawn animation");
            _isWaitingForAnimation = true;
            _animationStopwatch.Restart();
            StartCoroutine(WaitForAnimationToComplete(animator, "Appear"));
        }
        else
        {
            LogWarning("character prefab missing Animator component");
            _characterState = CharacterState.Active;
        }
    }

    /// <summary>
    /// 等待動畫完成的協程
    /// 實現穩健的動畫狀態檢測與完成通知
    /// </summary>
    private IEnumerator WaitForAnimationToComplete(Animator animator, string animationName)
    {
        LogInfo($"starting to wait for '{animationName}' animation to complete");
        
        // 等待幾幀讓動畫有時間初始化
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
        
        // 等待動畫進入正確狀態
        float timeoutCounter = 0;
        bool animationFound = false;
        
        while (timeoutCounter < 1f && !animationFound) // 1秒超時
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(animationName))
            {
                animationFound = true;
                LogInfo($"finded animation state '{animationName}'");
            }
            else
            {
                timeoutCounter += Time.deltaTime;
                yield return null;
            }
        }
        
        if (!animationFound)
        {
            LogWarning($"timeout waiting for '{animationName}' pretend the animation completed");
            _characterState = CharacterState.Active;
            _isWaitingForAnimation = false;
            _animationStopwatch.Stop();
            yield break;
        }
        
        // 等待動畫完成
        bool completed = false;
        while (!completed)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(animationName))
            {
                // 如果已經轉換到其他狀態，認為動畫已完成
                completed = true;
                LogInfo("animation state changed, animation completed");
            }
            else if (stateInfo.normalizedTime >= 0.95f)
            {
                // 或者如果播放進度接近結束
                completed = true;
                LogInfo($"animation {stateInfo.normalizedTime:F2} almost completed");
            }
            
            if (!completed)
            {
                yield return null;
            }
        }
        
        // 動畫完成，更新狀態
        LogInfo("animation completed, updating character state");
        _characterState = CharacterState.Active;
        _isWaitingForAnimation = false;
        _animationStopwatch.Stop();
        
        // 設置後續動畫狀態
        animator.ResetTrigger("stopApeear");
        animator.SetTrigger("stopApeear");
    }

    /// <summary>
    /// 取消正在進行的角色生成動畫
    /// </summary>
    private void CancelSpawningAnimation()
    {
        if (_spawnedCharacter != null && _characterState == CharacterState.Spawning)
        {
            LogInfo("cancelling spawning animation");
            
            var animator = _spawnedCharacter.GetComponent<Animator>();
            if (animator != null)
            {
                // 可以選擇播放退場動畫
                // animator.SetTrigger("disappear");
                // StartCoroutine(PlayDespawnAnimationAndDestroy(animator));
                
                // 或直接銷毀
                Destroy(_spawnedCharacter);
            }
            else
            {
                Destroy(_spawnedCharacter);
            }
            
            _spawnedCharacter = null;
            _characterState = CharacterState.NotPresent;
            _isWaitingForAnimation = false;
            _animationStopwatch.Stop();
        }
    }

    /// <summary>
    /// 計算 QR 碼角點的紋理坐標
    /// </summary>
    private Vector2[] CalculateTextureCoordinates(Vector3[] corners)
    {
        int count = corners.Length;
        var uvs = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            uvs[i] = new Vector2(corners[i].x, corners[i].y);
        }
        return uvs;
    }

    /// <summary>
    /// 計算多個點的中心點
    /// </summary>
    private Vector2 CalculateCenterPoint(Vector2[] points)
    {
        Vector2 center = Vector2.zero;
        foreach (var point in points)
        {
            center += point;
        }
        return center / points.Length;
    }

    /// <summary>
    /// 計算標記在 3D 空間中的變換
    /// 實現從 2D 圖像坐標到 3D 世界坐標的精確映射
    /// </summary>
    private bool CalculateMarkerTransform(Vector2 centerUV, Vector2[] uvs, out Vector3 center, out Quaternion rotation, out Vector3 scale)
    {
        center = Vector3.zero;
        rotation = Quaternion.identity;
        scale = Vector3.one;
        
        var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(_passthroughCameraEye);
        var centerPixel = new Vector2Int(
            Mathf.RoundToInt(centerUV.x * intrinsics.Resolution.x),
            Mathf.RoundToInt(centerUV.y * intrinsics.Resolution.y)
        );
        
        var centerRay = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, centerPixel);
        if (!envRaycastManager || !envRaycastManager.Raycast(centerRay, out var hitInfo))
        {
            return false;
        }

        center = hitInfo.point;
        var distance = Vector3.Distance(centerRay.origin, hitInfo.point);

        var count = uvs.Length;
        var tempCorners = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var pixelCoord = new Vector2Int(
                Mathf.RoundToInt(uvs[i].x * intrinsics.Resolution.x),
                Mathf.RoundToInt(uvs[i].y * intrinsics.Resolution.y)
            );
            
            var r = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, pixelCoord);
            tempCorners[i] = r.origin + r.direction * distance;
        }

        var up = (tempCorners[1] - tempCorners[0]).normalized;
        var right = (tempCorners[2] - tempCorners[1]).normalized;
        var normal = -Vector3.Cross(up, right).normalized;
        var qrPlane = new Plane(normal, center);
        var worldCorners = new Vector3[count];
        
        for (var i = 0; i < count; i++)
        {
            var pixelCoord = new Vector2Int(
                Mathf.RoundToInt(uvs[i].x * intrinsics.Resolution.x),
                Mathf.RoundToInt(uvs[i].y * intrinsics.Resolution.y)
            );
            
            var r = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, pixelCoord);
            if (qrPlane.Raycast(r, out var enter))
            {
                worldCorners[i] = r.GetPoint(enter);
            }
            else
            {
                worldCorners[i] = tempCorners[i];
            }
        }

        center = Vector3.zero;
        foreach (var corner in worldCorners)
        {
            center += corner;
        }
        
        center /= count;
        up = (worldCorners[1] - worldCorners[0]).normalized;
        right = (worldCorners[2] - worldCorners[1]).normalized;
        normal = -Vector3.Cross(up, right).normalized;
        
        rotation = Quaternion.LookRotation(normal, up);
        var width = Vector3.Distance(worldCorners[0], worldCorners[1]);
        var height = Vector3.Distance(worldCorners[0], worldCorners[3]);
        var scaleFactor = 1.5f;
        scale = new Vector3(width * scaleFactor, height * scaleFactor, 1f);
        
        return true;
    }

    /// <summary>
    /// 更新或創建 QR 碼標記
    /// </summary>
    private void UpdateOrCreateMarker(string qrText, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (_activeMarkers.TryGetValue(qrText, out var marker))
        {
            marker.UpdateMarker(position, rotation, scale);
        }
        else
        {
            var markerGo = MarkerPool.Instance.GetMarker();
            if (!markerGo)
            {
                LogWarning("cannnot get marker from pool, creating new one");
                return;
            }
            
            marker = markerGo.GetComponent<MarkerController>();
            if (!marker)
            {
                LogError("marker prefab missing MarkerController component");
                return;
            }
            
            marker.UpdateMarker(position, rotation, scale);
            _activeMarkers[qrText] = marker;
        }
    }

    /// <summary>
    /// 模擬 QR 碼檢測（編輯器模式下使用）
    /// </summary>
    private void SimulateQrCodeDetection()
    {
        LogInfo("模擬 QR 碼檢測");
        
        if (_characterState != CharacterState.NotPresent)
        {
            LogInfo($"角色已存在，當前狀態: {_characterState}");
            return;
        }
        
        // 在相機前方生成角色
        Vector3 position = Camera.main.transform.position + Camera.main.transform.forward * 2.0f;
        Quaternion rotation = Quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up);
        
        _characterState = CharacterState.Spawning;
        _spawnedCharacter = Instantiate(characterPrefab, position, rotation);
        
        var animator = _spawnedCharacter.GetComponent<Animator>();
        if (animator != null)
        {
            LogInfo("開始播放模擬角色出現動畫");
            _isWaitingForAnimation = true;
            _animationStopwatch.Restart();
            StartCoroutine(WaitForAnimationToComplete(animator, "Appear"));
        }
        else
        {
            LogWarning("模擬角色預製件缺少 Animator 組件");
            _characterState = CharacterState.Active;
        }
    }

    /// <summary>
    /// 調試日誌輸出：資訊級別
    /// </summary>
    private void LogInfo(string message)
    {
        if (enableDiagnostics)
        {
            UnityEngine.Debug.Log($"DC[QrManager] {message}");
        }
    }

    /// <summary>
    /// 調試日誌輸出：除錯級別
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDiagnostics)
        {
            UnityEngine.Debug.Log($"DC[QrManager:Debug] {message}");
        }
    }

    /// <summary>
    /// 調試日誌輸出：警告級別
    /// </summary>
    private void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning($"DC[QrManager] {message}");
    }

    /// <summary>
    /// 調試日誌輸出：錯誤級別
    /// </summary>
    private void LogError(string message)
    {
        UnityEngine.Debug.LogError($"DC[QrManager] {message}");
    }
#endif
}