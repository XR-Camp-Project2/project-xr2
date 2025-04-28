using UnityEngine;

public class AnimationSFX : StateMachineBehaviour
{
    public AudioClip clip;
    private AudioSource audioSource;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (clip != null)
        {
            if (audioSource == null)
            {
                audioSource = animator.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = animator.gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
