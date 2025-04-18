using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class SpawnPet : MonoBehaviour
{
    [SerializeField]
    private Pet petPrefab;

    private Pet petInstance;

    void Start()
    {
        
    }

    void Update()
    {
        var planes = FindObjectsByType<ARPlane>(FindObjectsSortMode.None);
        foreach (var plane in planes)
        {
            if(plane.gameObject.GetComponent<NavMeshModifier>() == null)
            {
                var go = plane.gameObject;
                var surface =  go.AddComponent<NavMeshSurface>();
                var modifier = go.AddComponent<NavMeshModifier>();
                surface.BuildNavMesh();

                if(this.petInstance == null)
                {
                    this.petInstance = Instantiate(this.petPrefab, go.transform.position, Quaternion.identity);
                }
            }
        }
    }
}
