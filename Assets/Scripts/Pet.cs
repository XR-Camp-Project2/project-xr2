using UnityEngine;
using Stateless;
using Cysharp.Threading.Tasks;
using System.Threading;

public class Pet : MonoBehaviour
{
    public enum State
    {
        Idle,
        Following,
        Hold,
        Grabbed, 
        Eating   
    }

    public enum Trigger
    {
        Follow,
        StopFollowing,
        Grab,         
        Eat,          
        FinishEating  
    }

    private StateMachine<State, Trigger> stateMachine;
    private CancellationToken stateTransitionToken;
    private PetNav petNav;

    private async UniTaskVoid Start()
    {
        this.petNav = GetComponent<PetNav>();
        this.stateMachine = new StateMachine<State, Trigger>(State.Idle);

        // Configure state transitions
        this.stateMachine.Configure(State.Idle)
            .Permit(Trigger.Follow, State.Following)
            .Permit(Trigger.Grab, State.Grabbed)
            .Permit(Trigger.Eat, State.Eating);

        this.stateMachine.Configure(State.Following)
            .Permit(Trigger.StopFollowing, State.Idle)
            .Permit(Trigger.Grab, State.Grabbed)
            .Permit(Trigger.Eat, State.Eating);

        this.stateMachine.Configure(State.Eating)
            .Permit(Trigger.FinishEating, State.Idle);

        await this.stateMachineLoop();
    }

    private async UniTask stateMachineLoop()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();

        while (!destroyToken.IsCancellationRequested)
        {
            var stateTransitionTokenSource  = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            this.stateTransitionToken = stateTransitionTokenSource.Token;

            switch (this.stateMachine.State)
            {
                case State.Idle:
                    await this.inIdle();
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

    private async UniTask inIdle()
    {
        while(!this.stateTransitionToken.IsCancellationRequested)
        {
            await this.petNav.moveToRandomPoint(this.stateTransitionToken);
            int idleMilliSeconds = Random.Range(500, 1500);
            await UniTask.Delay(idleMilliSeconds, cancellationToken: this.stateTransitionToken);
        }
    }
}
