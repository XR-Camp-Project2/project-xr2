using UnityEngine;

public class PetPresenter : MonoBehaviour
{
    private PetNav petNav;
    private Animator animator;

    void Start()
    {
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
        this.animator.SetBool("isWalking", this.petNav.Velocity.magnitude > 0.1f);
    }
}
