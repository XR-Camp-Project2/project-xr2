using UnityEngine;
using UnityEngine.XR.Hands.Samples.GestureSample;
using Stateless;
using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.XR.CoreUtils;
using System.Collections.Generic;
using System.Linq;

public class Pet : MonoBehaviour
{
    private interface PetAction
    {
        void Setup(Pet pet);
        float CalculateUtility();
        UniTask Execute(CancellationToken token);
    }

    private class StandAction : PetAction
    {
        private Pet pet;
        private float utility = 0.5f;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return this.utility;
        }

        public async UniTask Execute(CancellationToken token)
        {
            var startsAt = Time.time;
            while (true)
            {
                await UniTask.Delay(1000, cancellationToken: token);
                float elapsed = Time.time - startsAt;
                // Increase escape probability over time
                float escapeProbability = Mathf.Clamp01(elapsed * elapsed / 100f);
                if (Random.Range(0f, 1f) < escapeProbability)
                {
                    break;
                }
            }
        }
    }

    private class SleepAction : PetAction
    {
        private Pet pet;
        private float utility = 0.5f;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return this.utility;
        }

        public async UniTask Execute(CancellationToken token)
        {
            await this.pet.petNav.MoveTo(this.pet.bed, token);
            // trigger sleep animation
            this.pet.stateMachine.Fire(Trigger.GotoSleep);

            var startsAt = Time.time;
            while (true)
            {
                await UniTask.Delay(1000, cancellationToken: token);
                float elapsed = Time.time - startsAt;
                // Increase escape probability over time
                float escapeProbability = Mathf.Clamp01(elapsed * elapsed / 100f);
                if (Random.Range(0f, 1f) < escapeProbability)
                {
                    break;
                }
            }

            this.pet.stateMachine.Fire(Trigger.WakeUp);
        }
    }

    private class WanderAction : PetAction
    {
        private Pet pet;
        private float utility = 0.5f;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return this.utility;
        }

        public async UniTask Execute(CancellationToken token)
        {
            await this.pet.petNav.moveToRandomPoint(token);
        }
    }

    public enum State
    {
        Idle,
        Following,
        Grabbed,
        Eating,
        Sleep,
    }

    public enum Trigger
    {
        Follow,
        StopFollowing,
        Grab,
        Eat,
        FinishEating,
        GotoSleep,
        WakeUp,
    }

    // TODO: DI?
    public StaticHandGesture followGesture;
    public StaticHandGesture teleportGesture;
    public StaticHandGesture pointAtGesture;
    public Transform bed;


    public StateMachine<State, Trigger> StateMachine => this.stateMachine;
    private StateMachine<State, Trigger> stateMachine;
    private CancellationTokenSource stateTransitionTokenSource;
    private CancellationToken stateTransitionToken => this.stateTransitionTokenSource.Token;
    private PetNav petNav;
    private XROrigin xrOrigin;
    private List<PetAction> actions = new List<PetAction>();

    private async UniTaskVoid Start()
    {
        this.petNav = GetComponent<PetNav>();
        this.xrOrigin = FindFirstObjectByType<XROrigin>();
        this.setupStateMachine();
        this.subscribeHandGestureEvents();

        var standAction = new StandAction();
        standAction.Setup(this);
        this.actions.Add(standAction);
        var sleepAction = new SleepAction();
        sleepAction.Setup(this);
        this.actions.Add(sleepAction);
        var wanderAction = new WanderAction();
        wanderAction.Setup(this);
        this.actions.Add(wanderAction);

        await this.stateMachineLoop();
    }

    private void setupStateMachine()
    {
        this.stateMachine = new StateMachine<State, Trigger>(State.Idle);

        // Configure state transitions
        this.stateMachine.Configure(State.Idle)
            .OnEntry(() => { Debug.Log("Entering Idle state"); })
            .Permit(Trigger.Follow, State.Following)
            .Permit(Trigger.Grab, State.Grabbed)
            .Permit(Trigger.Eat, State.Eating)
            .Permit(Trigger.GotoSleep, State.Sleep);
        this.stateMachine.Configure(State.Sleep)
            .OnEntry(() => { Debug.Log("Entering Sleep state"); })
            .SubstateOf(State.Idle)
            .Permit(Trigger.WakeUp, State.Idle);

        this.stateMachine.Configure(State.Following)
            .OnEntry(() => { Debug.Log("Entering Following state"); })
            .Permit(Trigger.StopFollowing, State.Idle)
            .Permit(Trigger.Grab, State.Grabbed)
            .Permit(Trigger.Eat, State.Eating);
        this.stateMachine.Configure(State.Eating)
            .OnEntry(() => { Debug.Log("Entering Eating state"); })
            .Permit(Trigger.FinishEating, State.Idle);
    }

    private void subscribeHandGestureEvents()
    {
        if (this.followGesture == null)
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

        if (this.pointAtGesture == null)
        {
            Debug.LogWarning("Point at gesture is not assigned. Skip subscribing.");
        }
        else
        {
            this.pointAtGesture.gesturePerformed
                .AddListener(() =>
                {
                    this.stateTransitionTokenSource.Cancel();
                    this.stateMachine.Fire(Trigger.Grab);
                });
        }
    }

    private async UniTask stateMachineLoop()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();

        while (!destroyToken.IsCancellationRequested)
        {
            this.stateTransitionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);

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
        while (!this.stateTransitionToken.IsCancellationRequested)
        {
            var utilities = this.actions.Select(a => a.CalculateUtility()).ToArray();
            var actionIndex = this.softmaxSample(utilities);
            var pickedAction = this.actions[actionIndex];
            Debug.Log($"Picked action: {pickedAction.GetType().Name} with utility: {utilities[actionIndex]}");
            await pickedAction.Execute(this.stateTransitionToken);
        }
    }

    private async UniTask inFollowing()
    {
        await this.petNav.MoveTo(this.xrOrigin.transform, this.stateTransitionToken);
        this.stateMachine.Fire(Trigger.StopFollowing);
    }

    private int softmaxSample(float[] values)
    {
        float max = values.Max();
        float sum = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Mathf.Exp(values[i] - max);
            sum += values[i];
        }
        for (int i = 0; i < values.Length; i++)
        {
            values[i] /= sum;
        }
        return sampleFromDistribution(values);
    }

    private int sampleFromDistribution(float[] values)
    {
        float randomValue = Random.Range(0f, 1f);
        float cumulativeProbability = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            cumulativeProbability += values[i];
            if (randomValue < cumulativeProbability)
            {
                return i;
            }
        }
        return values.Length - 1; // Fallback in case of rounding errors
    }
}
