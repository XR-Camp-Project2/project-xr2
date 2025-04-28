using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

// 主要負責找出最大桌面並作為床的參考點
public class TableBedLocator : MonoBehaviour
{
    public static TableBedLocator Instance { get; private set; }

    // 最大桌面的Transform，供Pet使用
    public Transform largestTable { get; private set; }

    // 事件系統，通知找到新的床位置
    public delegate void BedFoundEvent(Transform bedTransform);
    public event BedFoundEvent OnBedFound;

    private MRUKRoom[] rooms;
    private bool shouldScan = true;

    private void Awake()
    {
        // 單例模式設定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (shouldScan)
        {
            ScanForLargestTable();
        }
    }

    // 掃描場景找出最大桌面
    public void ScanForLargestTable()
    {
        rooms = FindObjectsByType<MRUKRoom>(FindObjectsSortMode.None);

        if (rooms != null && rooms.Length > 0)
        {
            MRUKRoom room = rooms[0];
            List<MRUKAnchor> allAnchors = room.Anchors;

            float maxSize = 0.0f;
            MRUKAnchor largestTableAnchor = null;

            foreach (var anchor in allAnchors)
            {
                if (anchor.Label == MRUKAnchor.SceneLabels.TABLE)
                {
                    if (anchor.VolumeBounds.HasValue)
                    {
                        Bounds bound = anchor.VolumeBounds.Value;
                        float tableVolume = Mathf.Abs(bound.size.x) * Mathf.Abs(bound.size.z) * Mathf.Abs(bound.size.y);

                        if (tableVolume > maxSize)
                        {
                            largestTableAnchor = anchor;
                            maxSize = tableVolume;
                        }
                    }
                }
            }

            if (largestTableAnchor != null)
            {
                // 找到最大桌面，創建或更新參考點
                Vector3 tableCenter = largestTableAnchor.GetAnchorCenter();
                Bounds bound = largestTableAnchor.VolumeBounds.Value;
                Quaternion rot = largestTableAnchor.transform.rotation;

                Vector3 up = rot * Vector3.up;

                float halfHeight = bound.extents.y;
                Vector3 tableTop = tableCenter + up * halfHeight;

                if (largestTable == null)
                {
                    GameObject bedAnchor = new GameObject("BedAnchor");
                    largestTable = bedAnchor.transform;
                }

                largestTable.position = tableTop;
                Debug.Log("Finded largest table at " + tableTop);

                // 觸發事件通知寵物系統
                OnBedFound?.Invoke(largestTable);

                // 停止持續掃描
                shouldScan = false;
            }
        }
    }

    // 外部可調用此方法重新掃描
    public void RescanRoom()
    {
        shouldScan = true;
    }
}