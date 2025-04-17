using System.Collections;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;

public class PetNav : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float wanderTimer = 2f;

    private NavMeshAgent agent;
    private Animator animator;
    private float timer;

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;
        animator = transform.GetChild(0).gameObject.GetComponent<Animator>();
    }

    private void Start()
    {
        //var surfaces = GameObject.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        //foreach (var surface in surfaces)
        //{
        //    surface.BuildNavMesh();
        //}
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            animator.SetBool("isWalking", true);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }

        if (timer >= wanderTimer)
        {
            Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, -1);
            agent.SetDestination(newPos);
            timer = 0;
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
