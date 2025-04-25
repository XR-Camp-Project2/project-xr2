using UnityEngine;
using UnityEngine.XR.Hands.Samples.GestureSample;
using Stateless;
using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.XR.CoreUtils;

public class Pet : MonoBehaviour
{
    public enum State
    {
        Idle,
        Following,
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

    // TODO: DI?
    public StaticHandGesture followGesture;
    public StaticHandGesture teleportGesture;
    public StaticHandGesture pointAtGesture;

    private StateMachine<State, Trigger> stateMachine;
    private CancellationTokenSource stateTransitionTokenSource;
    private CancellationToken stateTransitionToken => this.stateTransitionTokenSource.Token;
    private PetNav petNav;
    private XROrigin xrOrigin;

    private async UniTaskVoid Start()
    {
        this.petNav = GetComponent<PetNav>();
        this.xrOrigin = FindFirstObjectByType<XROrigin>();
        this.setupStateMachine();
        this.subscribeHandGestureEvents();
        await this.stateMachineLoop();
    }

    private void setupStateMachine()
    {
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
    }

    private void subscribeHandGestureEvents()
    {
        if(this.followGesture == null)
        {
            Debug.LogWarning("Follow gesture is not assigned. Skip subscribing.");
        }
        else
        {
            this.followGesture.gesturePerformed
                .AddListener(() =>
                {
                    this.stateTransitionTokenSource.Cancel();
                    this.stateMachine.Fire(Trigger.Follow);
                });
        }
    }

    private async UniTask stateMachineLoop()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();

        while (!destroyToken.IsCancellationRequested)
        {
            this.stateTransitionTokenSource  = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);

            switch (this.stateMachine.State)
            {
                case State.Idle:
                    await this.inIdle();
                    break;
                case State.Following:
                    await this.inFollowing();
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

    private async UniTask inFollowing()
    {
        await this.petNav.MoveTo(this.xrOrigin.transform, this.stateTransitionToken);
        this.stateMachine.Fire(Trigger.StopFollowing);
    }
}
