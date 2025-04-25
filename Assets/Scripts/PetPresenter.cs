using Cysharp.Threading.Tasks;
using UnityEngine;

public class PetPresenter : MonoBehaviour
{
    private Pet pet;
    private PetNav petNav;
    private Animator animator;

    void Start()
    {
        this.pet = GetComponent<Pet>();
        if (this.pet == null)
        {
            Debug.LogError("Pet component not found on the GameObject.");
            return;
        }
        this.petNav = GetComponent<PetNav>();
        if (this.petNav == null)
        {
            Debug.LogError("PetNav component not found on the GameObject.");
            return;
        }
        this.animator = GetComponentInChildren<Animator>();
        if (this.animator == null)
        {
            Debug.LogError("Animator component not found in children of the GameObject.");
            return;
        }

        this.randomlyEnterCrawlingState().Forget();
    }

    void Update()
    {
        var isMoving = this.petNav.Velocity.magnitude > 0.1f;
        this.animator.SetBool("isWalking", isMoving);
        this.animator.SetBool("isCrawlingIdle", !isMoving);
    }

    private async UniTaskVoid randomlyEnterCrawlingState()
    {
        var token = this.GetCancellationTokenOnDestroy();
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: token);
            if (this.pet.StateMachine.State == Pet.State.Idle)
            {
                if (Random.Range(0f, 1f) < 0.1f)
                    this.animator.SetBool("isCrawling", true);
            }
            else
            {
                this.animator.SetBool("isCrawling", false);
            }
        }
    }
}
