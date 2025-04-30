using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class PetNav : MonoBehaviour
{
    [SerializeField]
    private float wanderRadius = 10f;

    private NavMeshAgent agent;
    public Vector3 Velocity => this.agent.velocity;

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        var surfaces = GameObject.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (var surface in surfaces)
        {
            surface.BuildNavMesh();
        }
    }
    private Vector3 findNextInterestedPoint()
    {
        return RandomNavSphere(transform.position, wanderRadius, -1);
    }

    public async UniTask moveToRandomPoint(CancellationToken ct)
    {
        var newPos = this.findNextInterestedPoint();
        agent.SetDestination(newPos);

        // FIXME: not sure whether WaitUntil does not work, use workaround for now
        await UniTask.Delay(5000, cancellationToken: ct);

        // await UniTask.WaitUntil(
        //     () => agent.pathPending || agent.remainingDistance < 0.1f,
        //     cancellationToken: ct
        // );
    }

    public async UniTask MoveTo(Transform target, CancellationToken ct)
    {
        await this.MoveTo(target.position, ct);
    }

    public async UniTask MoveTo(Vector3 target, CancellationToken ct)
    {
        agent.SetDestination(target);
        await UniTask.WaitUntil(
            () => agent.pathPending || agent.remainingDistance < 0.1f,
            cancellationToken: ct
        );
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randomDirection = Random.insideUnitSphere * dist;
        randomDirection += origin;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randomDirection, out navHit, dist, layermask))
        {
            return navHit.position;
        }
        else
        {
            return origin;
        }
    }
}
