using Cysharp.Threading.Tasks;
using UnityEngine;

public class PatHead : MonoBehaviour
{
    [SerializeField] private PetStats petStats;
    public bool isLeftHandPatting = false;
    public bool isRightHandPatting = false;

    void Start()
    {
        PatHeadCoroutine().Forget(); // Start the coroutine
    }

    public async UniTaskVoid PatHeadCoroutine()
    {
        while (true) {
            if (isLeftHandPatting || isRightHandPatting)
            {
                petStats.Happiness += 10;
                await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            else
            {
                await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
            }
        }
    }

    public void SetLeftHandPatting(bool isPatting)
    {
        isLeftHandPatting = isPatting;
    }

    public void SetRightHandPatting(bool isPatting)
    {
        isRightHandPatting = isPatting;
    }
}
