using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AI;

// Modified from AgentLinkMover.cs in Unity AI Navigation Samples 
public class PetLinkMover : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        agent.autoTraverseOffMeshLink = false;
        Animator animator = GetComponentInChildren<Animator>();
        var token = this.GetCancellationTokenOnDestroy();
        while (!token.IsCancellationRequested)
        {
            if (agent.isOnOffMeshLink)
            {
                Debug.Log("Set isLeaping to true");
                animator.SetBool("isLeaping", true);
                OffMeshLinkData data = agent.currentOffMeshLinkData;
                Vector3 startPos = agent.transform.position;
                Vector3 endPos = data.endPos + Vector3.up * agent.baseOffset;
                transform.LookAt(endPos);
                float deltaY = endPos.y - startPos.y;
                float height = Mathf.Abs(deltaY);
                float duration = (endPos - startPos).magnitude / agent.speed;
                float normalizedTime = 0.0f;
                while (normalizedTime < 1.0f)
                {
                    float yOffset = height * 4.0f * (normalizedTime - normalizedTime * normalizedTime);
                    agent.transform.position = Vector3.Lerp(startPos, endPos, normalizedTime) + yOffset * Vector3.up;
                    normalizedTime += Time.deltaTime / duration;
                    await UniTask.Yield(token);
                }
                agent.Warp(endPos);
                agent.CompleteOffMeshLink();
                Debug.Log("Set isLeaping to false");
                animator.SetBool("isLeaping", false);
            }
            await UniTask.Yield(token);
        }
    }
}
