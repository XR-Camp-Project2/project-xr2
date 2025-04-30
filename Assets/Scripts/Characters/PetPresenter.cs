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
    }

    void Update()
    {
        var isMoving = this.petNav.Velocity.magnitude > 0.1f;
        this.animator.SetBool("isWalking", isMoving);
        this.animator.SetBool("isCrawlingIdle", !isMoving);
        this.animator.SetBool("isSleeping", this.pet.StateMachine.State == Pet.State.Sleeping);
        this.animator.SetBool("isUpset", this.pet.StateMachine.State == Pet.State.Upset);
    }

    private async UniTaskVoid randomlyEnterCrawlingState()
    {
        var token = this.GetCancellationTokenOnDestroy();
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: token);
            if (this.pet.StateMachine.IsInState(Pet.State.Idle))
            {
                this.animator.SetBool("isCrawling", Random.Range(0f, 1f) < 0.1f);
            }
            else
            {
                this.animator.SetBool("isCrawling", false);
            }
        }
    }

    private async UniTaskVoid LoopWalkingSFX() {
        AudioManager audioManager = FindFirstObjectByType<AudioManager>();

        while (true) {
            await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (this.petNav.Velocity.magnitude > 0.1f && !this.animator.GetBool("isLeaping")) {
                audioManager.Play("SFX", 2, 0.5f);
            }
            else {
                break;
            }
        }
    }
}
