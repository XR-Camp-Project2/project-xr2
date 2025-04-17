using UnityEngine;
using Stateless;
public class Pet : MonoBehaviour
{
    public enum State
    {
        Idle,
        Following,
        Hold,
        Grabbed, // New state
        Eating   // New state
    }

    public enum Trigger
    {
        Follow,
        StopFollowing,
        Grab,         // New trigger
        Eat,          // New trigger
        FinishEating  // New trigger
    }

    private StateMachine<State, Trigger> stateMachine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        this.stateMachine = new StateMachine<State, Trigger>(State.Idle);

        // Configure state transitions
        this.stateMachine.Configure(State.Idle)
            .Permit(Trigger.Follow, State.Following)
            .Permit(Trigger.Grab, State.Grabbed)       // New transition
            .Permit(Trigger.Eat, State.Eating);        // New transition

        this.stateMachine.Configure(State.Following)
            .Permit(Trigger.StopFollowing, State.Idle)
            .Permit(Trigger.Grab, State.Grabbed)       // New transition
            .Permit(Trigger.Eat, State.Eating);        // New transition

        this.stateMachine.Configure(State.Eating)
            .Permit(Trigger.FinishEating, State.Idle); // New transition
    }

    // Update is called once per frame
    void Update()
    {
        switch(this.stateMachine.State)
        {
            case State.Idle:
                // Handle idle state
                break;
            case State.Following:
                // Handle following state
                break;
            case State.Hold:
                // Handle hold state
                break;
            case State.Grabbed:
                // Handle grabbed state
                break;
            case State.Eating:
                // Handle eating state
                break;
        }
    }
}
