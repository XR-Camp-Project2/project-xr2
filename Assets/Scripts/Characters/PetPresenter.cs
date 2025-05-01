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
        LoopWalkingSFX().Forget(); // Start the walking sound effect loop
    }

    void Update()
    {
        var isMoving = this.petNav.Velocity.magnitude > 0.05f;
        this.animator.SetBool("isWalking", isMoving);
        this.animator.SetBool("isCrawlingIdle", !isMoving);
        this.animator.SetBool("isSleeping", this.pet.StateMachine.State == Pet.State.Sleeping);
        this.animator.SetBool("isUpset", this.pet.StateMachine.State == Pet.State.Upset);
        this.animator.SetBool("isCrawling", this.pet.StateMachine.State == Pet.State.SearchingFood);
    }

    private async UniTaskVoid LoopWalkingSFX() {
        AudioManager audioManager = FindFirstObjectByType<AudioManager>();

        while (true) {
            if (this.petNav.Velocity.magnitude > 0.05f && this.animator.GetBool("isWalking")) {
                audioManager.Play("SFX", 2, 0.5f);
                await UniTask.Delay(500, cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            else {
                await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
            }
        }
    }
}
