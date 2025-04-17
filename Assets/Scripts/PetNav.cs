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
    private Animator animator;

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = transform.GetChild(0).gameObject.GetComponent<Animator>();
    }

    private void Start()
    {
        var surfaces = GameObject.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (var surface in surfaces)
        {
            surface.BuildNavMesh();
        }
    }

    private void Update()
    {
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            animator.SetBool("isWalking", true);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }
    }

    public Vector3 findNextInstrestdPoint()
    {
        return RandomNavSphere(transform.position, wanderRadius, -1);
    }

    public async UniTask moveToRandomPoint(CancellationToken ct)
    {
        var newPos = this.findNextInstrestdPoint();
        agent.SetDestination(newPos);
        await UniTask.WaitUntil(
            () => agent.pathPending || agent.remainingDistance > 0.1f,
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
