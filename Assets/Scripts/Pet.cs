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
    private class TimeBasedEscapeTokenSource
    {
        private CancellationTokenSource tokenSource;
        private float startsAt;

        public TimeBasedEscapeTokenSource(CancellationToken token)
        {
            this.startsAt = Time.time;
            this.tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            this.checkCancel(token).Forget();
        }

        private async UniTaskVoid checkCancel(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(1000, cancellationToken: token);
                float elapsed = Time.time - this.startsAt;
                // Increase escape probability over time
                float escapeProbability = Mathf.Clamp01(elapsed * elapsed / 100f);
                if (Random.Range(0f, 1f) < escapeProbability)
                {
                    this.tokenSource.Cancel();
                    break;
                }
            }
        }

        public bool IsCancelled => this.tokenSource.IsCancellationRequested;
    }

    private interface PetAction
    {
        void Setup(Pet pet);
        float CalculateUtility();
        UniTask Execute(CancellationToken token);
    }

    private class StandAction : PetAction
    {
        private Pet pet;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return this.pet.petStats.Hunger;
        }

        public async UniTask Execute(CancellationToken token)
        {
            var src = new TimeBasedEscapeTokenSource(token);
            await UniTask.WaitUntil(() => src.IsCancelled, cancellationToken: token);
        }
    }

    private class UpsetAction : PetAction
    {
        private Pet pet;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return 100 - this.pet.petStats.Happiness;
        }

        public async UniTask Execute(CancellationToken token)
        {
            var src = new TimeBasedEscapeTokenSource(token);
            this.pet.stateMachine.Fire(Trigger.Upset);
            while (!src.IsCancelled)
            {
                await UniTask.Delay(1000, cancellationToken: token);
                this.pet.petStats.Happiness += 2;
            }
            this.pet.stateMachine.Fire(Trigger.Recover);
        }
    }

    private class SleepAction : PetAction
    {
        private Pet pet;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return (100 - this.pet.petStats.Health) + this.pet.petStats.Hunger;
        }

        public async UniTask Execute(CancellationToken token)
        {
            await this.pet.petNav.MoveTo(this.pet.bed, token);
            // trigger sleep animation
            this.pet.stateMachine.Fire(Trigger.GotoSleep);

            var src = new TimeBasedEscapeTokenSource(token);
            while (!src.IsCancelled)
            {
                await UniTask.Delay(1000, cancellationToken: token);
                // health is increased more when sleeping
                this.pet.petStats.Health += 5;
            }

            this.pet.stateMachine.Fire(Trigger.WakeUp);
        }
    }

    private class WanderAction : PetAction
    {
        private Pet pet;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return this.pet.petStats.Health + (50 - this.pet.petStats.Hunger);
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
        Upset    // new
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
        Upset,   // new
        Recover  // new
    }

    public PetStats petStats;

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
        this.setupStats();
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
        var upsetAction = new UpsetAction();
        upsetAction.Setup(this);
        this.actions.Add(upsetAction);

        await UniTask.WhenAll(
            this.stateMachineLoop(),
            this.updatePetStats()
        );
    }

    private void setupStats()
    {
        this.petStats.Health = 100;
        this.petStats.Hunger = 0;
        this.petStats.Happiness = 100;
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
            .Permit(Trigger.GotoSleep, State.Sleep)
            .Permit(Trigger.Upset, State.Upset);

        this.stateMachine.Configure(State.Sleep)
            .OnEntry(() => { Debug.Log("Entering Sleep state"); })
            .SubstateOf(State.Idle)
            .Permit(Trigger.WakeUp, State.Idle)
            .Permit(Trigger.Upset, State.Upset);
        this.stateMachine.Configure(State.Upset)
            .OnEntry(() => Debug.Log("Entering Upset state"))
            .SubstateOf(State.Idle)
            .Permit(Trigger.Recover, State.Idle);

        this.stateMachine.Configure(State.Following)
            .OnEntry(() => { Debug.Log("Entering Following state"); })
            .Permit(Trigger.StopFollowing, State.Idle)
            .Permit(Trigger.Grab, State.Grabbed)
            .Permit(Trigger.Eat, State.Eating)
            .Permit(Trigger.Upset, State.Upset);

        this.stateMachine.Configure(State.Eating)
            .OnEntry(() => { Debug.Log("Entering Eating state"); })
            .Permit(Trigger.FinishEating, State.Idle)
            .Permit(Trigger.Upset, State.Upset);

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

            if (this.stateMachine.IsInState(State.Idle))
            {
                await this.inIdle();
                continue;
            }

            switch (this.stateMachine.State)
            {
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
            var actionIndex = this.softmaxSample(utilities, 3);
            var pickedAction = this.actions[actionIndex];
            for (int i = 0; i < utilities.Length; i++)
            {
                Debug.Log($"Action {i}: {this.actions[i].GetType().Name} with utility: {utilities[i]}");
            }
            Debug.Log($"Picked action: {pickedAction.GetType().Name}");
            await pickedAction.Execute(this.stateTransitionToken);
        }
    }

    private async UniTask inFollowing()
    {
        await this.petNav.MoveTo(this.xrOrigin.transform, this.stateTransitionToken);
        this.stateMachine.Fire(Trigger.StopFollowing);
    }

    private async UniTask updatePetStats()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();
        while (!destroyToken.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: destroyToken);
            this.petStats.Hunger += 1;
            this.petStats.Health -= 1;
            if (this.petStats.Hunger > 100)
            {
                this.petStats.Hunger = 100;
            }
            if (this.petStats.Health < 0)
            {
                this.petStats.Health = 0;
            }

            if (this.flip(0.1f))
            {
                this.petStats.Happiness -= 1;
            }
        }
    }

    // Adds a temperature parameter to control "softness" of the softmax.
    // Lower temperature (<1) makes choices sharper, higher (>1) makes them softer.
    private int softmaxSample(float[] values, float temperature = 1.0f)
    {
        float max = values.Max();
        float sum = 0f;
        float[] expValues = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            expValues[i] = Mathf.Exp((values[i] - max) / Mathf.Max(temperature, 1e-6f));
            sum += expValues[i];
        }
        for (int i = 0; i < expValues.Length; i++)
        {
            expValues[i] /= sum;
        }
        return sampleFromDistribution(expValues);
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

    private bool flip(float probability)
    {
        return Random.Range(0f, 1f) < probability;
    }
}
