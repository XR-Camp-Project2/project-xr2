using UnityEngine;
using UnityEngine.XR.Hands.Samples.GestureSample;
using Stateless;
using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.XR.CoreUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine.Animations.Rigging;

public class Pet : MonoBehaviour
{
    const string FOOD_TAG = "Food";

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
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                    float elapsed = Time.time - this.startsAt;
                    // Increase escape probability over time
                    float escapeProbability = Mathf.Clamp01(elapsed * elapsed / 100f);
                    if (Random.Range(0f, 1f) < escapeProbability)
                    {
                        break;
                    }
                }
            }
            finally
            {
                this.tokenSource.Cancel();
            }
        }

        public bool IsCancelled => this.tokenSource.IsCancellationRequested;
        public CancellationToken Token => this.tokenSource.Token;
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
            if (this.pet.flip(0.1f))
            {
                return 300;
            }
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

    private class SquatAction : PetAction
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
            this.pet.stateMachine.Fire(Trigger.Squat);
            await UniTask.WaitUntil(() => src.IsCancelled, cancellationToken: token);
            this.pet.stateMachine.Fire(Trigger.Standup);
            this.pet.petStats.Happiness += 5;
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
            if (this.pet.bed == null)
            {
                return 0;
            }
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
            Debug.Log("Exiting WanderAction");
        }
    }

    private class StretchAction : PetAction
    {
        private Pet pet;

        public void Setup(Pet pet)
        {
            this.pet = pet;
        }

        public float CalculateUtility()
        {
            return 100 - this.pet.petStats.Health;
        }

        public async UniTask Execute(CancellationToken token)
        {
            var src = new TimeBasedEscapeTokenSource(token);

        }
    }

    public enum State
    {
        Idle,
        Following,
        Grabbed,
        Eating,
        Sleeping,
        Upset,
        Squatting,
        Leaping,
        SearchingFood,
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
        Upset,
        Recover,
        Squat,
        Standup,
        Jump,
        Land,
        StartSearchingFood,
        GiveUpSearchingFood,
    }

    public PetStats petStats;

    // TODO: DI?
    public StaticHandGesture followGesture;
    public StaticHandGesture teleportGesture;
    public StaticHandGesture pointAtGesture;
    public Transform bed;


    [SerializeField]
    private Transform rightHandTarget;
    [SerializeField]
    private Rig rightHandRig;

    public StateMachine<State, Trigger> StateMachine => this.stateMachine;
    private StateMachine<State, Trigger> stateMachine;
    private CancellationTokenSource stateTransitionTokenSource;
    private CancellationToken stateTransitionToken => this.stateTransitionTokenSource.Token;
    private PetNav petNav;
    private XROrigin xrOrigin;
    private List<PetAction> actions = new List<PetAction>();

    private async UniTaskVoid Start()
    {
        Debug.Assert(this.rightHandTarget != null, "Right hand target is not assigned.");
        Debug.Assert(this.rightHandRig != null, "Right hand rig is not assigned.");
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
        var squatAction = new SquatAction();
        squatAction.Setup(this);
        this.actions.Add(squatAction);

        if (TableBedLocator.Instance != null)
        {
            TableBedLocator.Instance.OnBedFound += this.setupBed;
        }
        else
        {
            Debug.LogWarning("TableBedLocator instance is null. Bed will not be set.");
        }

        await UniTask.WhenAll(
            this.stateMachineLoop(),
            this.updatePetStats(),
            this.fall()
        );
    }

    private void setupBed(Transform bedTransform)
    {
        this.bed = bedTransform;
        Debug.Log($"Bed set to: {this.bed.name}");
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
            .Permit(Trigger.GotoSleep, State.Sleeping)
            .Permit(Trigger.Upset, State.Upset)
            .Permit(Trigger.Squat, State.Squatting)
            .Permit(Trigger.Jump, State.Leaping)
            .Permit(Trigger.StartSearchingFood, State.SearchingFood);

        this.stateMachine.Configure(State.Sleeping)
            .OnEntry(() => { Debug.Log("Entering Sleep state"); })
            .SubstateOf(State.Idle)
            .Permit(Trigger.WakeUp, State.Idle);
        this.stateMachine.Configure(State.Upset)
            .OnEntry(() => Debug.Log("Entering Upset state"))
            .SubstateOf(State.Idle)
            .Permit(Trigger.Recover, State.Idle);
        this.stateMachine.Configure(State.Squatting)
            .OnEntry(() => { Debug.Log("Entering Squatting state"); })
            .SubstateOf(State.Idle)
            .Permit(Trigger.Standup, State.Idle);

        this.stateMachine.Configure(State.Following)
            .OnEntry(() => { Debug.Log("Entering Following state"); })
            .Permit(Trigger.StopFollowing, State.Idle)
            .Permit(Trigger.Grab, State.Grabbed)
            .Permit(Trigger.Upset, State.Upset);

        this.stateMachine.Configure(State.Eating)
            .OnEntry(() => { Debug.Log("Entering Eating state"); })
            .Permit(Trigger.FinishEating, State.Idle)
            .Permit(Trigger.Upset, State.Upset);

        this.stateMachine.Configure(State.Leaping)
            .OnEntry(() => { Debug.Log("Entering Jumping state"); })
            .Permit(Trigger.Land, State.Idle);

        this.stateMachine.Configure(State.SearchingFood)
            .OnEntry(() => { Debug.Log("Entering SearchingFood state"); })
            .Permit(Trigger.Eat, State.Eating)
            .Permit(Trigger.GiveUpSearchingFood, State.Idle);
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
                try
                {
                    await this.inIdle();
                }
                catch (System.OperationCanceledException e)
                {
                }
                continue;
            }

            try
            {
                switch (this.stateMachine.State)
                {
                    case State.Following:
                        await this.inFollowing();
                        break;
                    case State.Grabbed:
                        // Handle grabbed state
                        break;
                    case State.Eating:
                        await this.inEating();
                        break;
                    case State.SearchingFood:
                        await this.inSearchingFood();
                        break;
                    default:
                        Debug.LogWarning($"Unhandled state: {this.stateMachine.State}");
                        break;
                }
            }
            catch (System.OperationCanceledException e)
            {
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
            try
            {
                await pickedAction.Execute(this.stateTransitionToken);
            }
            catch (System.OperationCanceledException e)
            {
                Debug.Log($"Action {pickedAction.GetType().Name} was cancelled: {e.Message}");
            }
        }
    }

    private async UniTask inFollowing()
    {
        await this.petNav.MoveTo(this.xrOrigin.transform, this.stateTransitionToken);
        this.stateMachine.Fire(Trigger.StopFollowing);
    }

    private async UniTask inSearchingFood()
    {
        float distanceFromFood = 0.5f;
        while (!this.stateTransitionToken.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: this.stateTransitionToken);
            var food = GameObject.FindGameObjectsWithTag(FOOD_TAG).FirstOrDefault();
            if (food != null)
            {
                var targetPosition = food.transform.position;
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, distanceFromFood, NavMesh.AllAreas))
                {
                    Debug.Log("Found food, moving to it.");
                    await this.petNav.MoveTo(hit.position, this.stateTransitionToken);
                    this.stateMachine.Fire(Trigger.Eat);
                    return;
                }
                else
                {
                    Debug.LogWarning($"No valid NavMesh position found near {targetPosition}, fallback to Idle state.");
                    this.stateMachine.Fire(Trigger.GiveUpSearchingFood);
                    return;
                }
            }
            else
            {
                Debug.Log("No food found, wandering around...");
                await this.petNav.moveToRandomPoint(this.stateTransitionToken);
            }
        }
    }

    private async UniTask inEating()
    {
        var food = GameObject.FindGameObjectsWithTag(FOOD_TAG)
            .OrderBy((go) => Vector3.Distance(go.transform.position, this.transform.position))
            .FirstOrDefault();
        if (food == null)
        {
            Debug.LogWarning("No food found, returning to Idle state.");
            this.stateMachine.Fire(Trigger.GiveUpSearchingFood);
            return;
        }

        if (Vector3.Distance(this.transform.position, food.transform.position) > 0.5f)
        {
            var targetPosition = food.transform.position + (food.transform.position - this.transform.position).normalized * 0.3f;
            await this.petNav.MoveTo(targetPosition, this.stateTransitionToken);
        }

        this.rightHandRig.weight = 1f;
        var rightHandOriginalPosition = this.rightHandTarget.position;
        try
        {
            await LMotion.Create(this.rightHandTarget.position, food.transform.position, 0.5f)
                .WithEase(Ease.InBack)
                .BindToPosition(this.rightHandTarget)
                .AddTo(gameObject);

            // TODO: play eating animation?
            await UniTask.Delay(2000, cancellationToken: this.stateTransitionToken);
            Destroy(food);

        }
        finally
        {
            this.rightHandRig.weight = 0f;
            this.rightHandTarget.position = rightHandOriginalPosition;
            this.petStats.Hunger -= 30;
            this.petStats.Happiness += 10;
            if (this.petStats.Hunger < 0)
            {
                this.petStats.Hunger = 0;
            }
            this.stateMachine.Fire(Trigger.FinishEating);
        }
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

            // TODO: use function to calculate probability of being hungry?
            if (this.petStats.Hunger > 10 && this.stateMachine.CanFire(Trigger.StartSearchingFood))
            {
                Debug.Log("So hungry, searching for food...");
                this.stateTransitionTokenSource.Cancel();
                this.stateMachine.Fire(Trigger.StartSearchingFood);
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

    private async UniTask fall()
    {
        var token = this.GetCancellationTokenOnDestroy();
        var agent = GetComponent<NavMeshAgent>();
        var animator = GetComponentInChildren<Animator>();
        while (!token.IsCancellationRequested)
        {
            if (!agent.isOnNavMesh)
            {
                animator.SetBool("isLeaping", true);
                var g = Mathf.Abs(Physics.gravity.y); // Use global gravity setting
                var velocity = Vector3.zero;
                while (true)
                {
                    velocity += Vector3.down * (g * Time.deltaTime);
                    transform.position += velocity * Time.deltaTime;
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.1f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                        break;
                    }
                    await UniTask.Yield(token);
                }
                animator.SetBool("isLeaping", false);
            }
            await UniTask.Yield();
        }
    }
}
