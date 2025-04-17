using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class PetNav : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float wanderTimer = 5f;

    private NavMeshAgent agent;
    private Animator animator;
    private float timer;
    private Pet pet;

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;
        animator = transform.GetChild(0).gameObject.GetComponent<Animator>();
        this.pet = GetComponent<Pet>();
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
        while (agent.pathPending || agent.remainingDistance > 0.1f)
        {
            await UniTask.Yield(ct);
        }
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
