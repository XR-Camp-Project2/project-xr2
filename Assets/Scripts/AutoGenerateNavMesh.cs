using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class AutoGenerateNavMesh : MonoBehaviour
{
    public ARPlaneManager arPlaneManager;

    void Start()
    {
        this.arPlaneManager.trackablesChanged.AddListener((_) => this.generateMeshes());
        this.generateMeshes();
    }

    private void generateMeshes()
    {
        foreach (var plane in this.arPlaneManager.trackables)
        {
            var surface = plane.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
        }

        // FIXME: workaround to create list from TrackableCollection
        var planes = new List<ARPlane>();
        foreach (var plane in this.arPlaneManager.trackables)
        {
            planes.Add(plane);
        }

        for(int i=0; i < planes.Count; i++)
        {
            var boundaryI = planes[i].boundary
                .Select(b => planes[i].gameObject.transform.position + new Vector3(b.x, 0, b.y))
                .ToArray();
            for(int j = 0; j < i; j++)
            {
                foreach (var b in planes[j].boundary)
                {
                    for(int k = 0; k < boundaryI.Length; k++)
                    {
                        var boundaryJ = planes[j].gameObject.transform.position + new Vector3(b.x, 0, b.y);
                        if (Vector3.Distance(boundaryI[k], boundaryJ) < 10)
                        {
                            var go = new GameObject($"NavMeshLink ({i}-{j})");
                            var link = go.AddComponent<NavMeshLink>();
                            link.startPoint = boundaryI[k];
                            link.endPoint = boundaryJ;
                            link.width = 4f;
                            link.bidirectional = true;
                            link.UpdateLink();
                        }
                    }
                }
            }
        }
    }
}
