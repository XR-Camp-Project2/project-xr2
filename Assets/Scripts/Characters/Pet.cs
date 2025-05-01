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
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Pet : MonoBehaviour
{
    const string FOOD_TAG = "Food";
    const int STRETCH_AUDIO_INDEX = 4;

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
            try {
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
            } finally {
                if(this.pet.stateMachine.CanFire(Trigger.WakeUp))
                    this.pet.stateMachine.Fire(Trigger.WakeUp);
                await UniTask.Delay(1500, cancellationToken: token);
            }
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
            return this.pet.petStats.Health + (40 - this.pet.petStats.Hunger);
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
            if (this.pet.petStats.Health < 50) {
                return 100;
            }
            if (Random.Range(0f, 1f) < 0.1f) {
                return 100;
            }
            return 100 - this.pet.petStats.Health;
        }

        public async UniTask Execute(CancellationToken token)
        {
            // FIXME: don't call animator directly here
            var animator = this.pet.GetComponentInChildren<Animator>();
            var audioManager = FindFirstObjectByType<AudioManager>();
            animator.SetBool("isStrech", true);
            try {
                await UniTask.WaitUntil(
                    () => animator.GetCurrentAnimatorStateInfo(0).IsName("Strech"),
                    cancellationToken: token
                );
                audioManager.Play("TALK", STRETCH_AUDIO_INDEX, 0.5f);
                this.pet.petStats.Happiness += 5;
                this.pet.petStats.Health += 20;
            } finally {
                animator.SetBool("isStrech", false);
                audioManager.Stop("TALK", STRETCH_AUDIO_INDEX);
                await UniTask.WaitUntil(
                    () => !animator.GetCurrentAnimatorStateInfo(0).IsName("Strech"),
                    cancellationToken: token
                );
            }
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
    public StaticHandGesture teleportGesture;
    public StaticHandGesture pointAtGesture;
    public Transform bed;


    [SerializeField]
    private Transform rightHandTarget;
    [SerializeField]
    private Rig rightHandRig;
    [SerializeField]
    private Transform head;
    [SerializeField]
    private Transform headTarget;
    [SerializeField]
    private Rig headRig;

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
        Debug.Assert(this.head != null, "Head is not assigned.");
        Debug.Assert(this.headTarget != null, "Head target is not assigned.");
        Debug.Assert(this.headRig != null, "Head rig is not assigned.");

        this.petNav = GetComponent<PetNav>();
        this.xrOrigin = FindFirstObjectByType<XROrigin>();
        this.setupStats();
        this.setupStateMachine();
        this.subscribeHandGestureEvents();

        this.actions.Add(new StandAction());
        this.actions.Add(new SleepAction());
        this.actions.Add(new WanderAction());
        this.actions.Add(new UpsetAction());
        this.actions.Add(new SquatAction());
        this.actions.Add(new StretchAction());
        foreach (var action in this.actions)
        {
            action.Setup(this);
        }

        if (TableBedLocator.Instance != null)
        {
            TableBedLocator.Instance.OnBedFound += this.setupBed;
            TableBedLocator.Instance.RescanRoom();
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
            .Permit(Trigger.Grab, State.Grabbed);

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
            .Permit(Trigger.GiveUpSearchingFood, State.Idle)
            .Permit(Trigger.Follow, State.Following);
    }

    private void subscribeHandGestureEvents()
    {
        var followGestureGo = FindFirstObjectByType<FollowGestureTag>()?.gameObject;
        if (followGestureGo == null)
        {
            Debug.LogWarning("Follow gesture is not assigned. Skip subscribing.");
        }
        else
        {
            var gestures = followGestureGo.GetComponents<StaticHandGesture>();
            if (gestures.Length == 0)
            {
                Debug.LogWarning("No gestures found on FollowGestureTag. Skip subscribing.");
                return;
            }
            foreach (var gesture in gestures)
            {
                gesture.gesturePerformed
                    .AddListener(() =>
                    {
                        this.stateTransitionTokenSource.Cancel();
                        this.stateMachine.Fire(Trigger.Follow);
                    });
            }
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
        while(!this.stateTransitionToken.IsCancellationRequested)
        {
            await UniTask.WhenAny(
                this.moveTowardsPlayer(),
                this.lookAtPlayer()
            );
        }
    }

    private async UniTask lookAtPlayer()
    {
        this.headRig.weight = 1f;
        var target =  Camera.main.transform;
        try
        {
            while (!this.stateTransitionToken.IsCancellationRequested)
            {
                await UniTask.Yield(this.stateTransitionToken);
                this.headTarget.position = Vector3.Lerp(
                    this.headTarget.position,
                    target.position,
                    2f * Time.deltaTime
                );
            }
        }
        finally
        {
            this.headRig.weight = 0f;
        }
    }

    private async UniTask moveTowardsPlayer()
    {
        var target = Camera.main;

        while(!this.stateTransitionToken.IsCancellationRequested)
        {
            System.Func<Vector3> targetPos = () => {
                var ret = target.transform.position;
                ret.y = transform.position.y;
                return ret; 
            };
            // wait until the player is far enough
            await UniTask.WaitUntil(
                () => Vector3.Distance(transform.position, targetPos()) > 0.6f,
                cancellationToken: this.stateTransitionToken
            );

            try
            {
                var moveToPos = targetPos();
                if(NavMesh.SamplePosition(moveToPos, out NavMeshHit hit, 0.35f, -1)) {
                    moveToPos = hit.position;
                }
                // move towards the player at most 1 second
                await UniTask.WhenAny(
                    this.petNav.MoveTo(moveToPos, this.stateTransitionToken, 0.4f),
                    UniTask.Delay(3000, cancellationToken: this.stateTransitionToken)
                );
            }
            catch (System.OperationCanceledException e)
            {
                if(this.stateMachine.State == State.Following)
                {
                    Debug.Log("Keep following player...");
                    continue;
                }
                Debug.Log($"Move towards player action was cancelled: {e.Message}");
            }       
        }
    }

    private async UniTask inSearchingFood()
    {
        float distanceFromFood = 0.2f;
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

        if (Vector3.Distance(this.transform.position, food.transform.position) > 0.2f)
        {
            var targetPosition = food.transform.position + (food.transform.position - this.transform.position).normalized * 0.1f;
            await this.petNav.MoveTo(targetPosition, this.stateTransitionToken);
        }

        this.rightHandRig.weight = 1f;
        var rightHandOriginalLocalPosition = this.rightHandTarget.localPosition;
        try
        {
            // Turn to food
            var targetRotation = Quaternion.LookRotation(food.transform.position - this.transform.position);
            await LMotion.Create(this.transform.rotation, targetRotation, 0.5f)
                .WithEase(Ease.OutBack)
                .BindToRotation(this.transform)
                .AddTo(gameObject);
            await UniTask.Delay(3000, cancellationToken: this.stateTransitionToken);
            // Try grab food
            await LMotion.Create(this.rightHandTarget.position, food.transform.position, 0.5f)
                .WithEase(Ease.InBack)
                .BindToPosition(this.rightHandTarget)
                .AddTo(gameObject);
            food.transform.parent = this.rightHandTarget;
            food.transform.localPosition = Vector3.zero;
            if(food.TryGetComponent<Rigidbody>(out var rb))
            {
                if(food.TryGetComponent<XRGrabInteractable>(out var interactable))
                {
                    Destroy(interactable);
                    await UniTask.Yield(this.stateTransitionToken);
                }
                Destroy(rb);
            }
            await UniTask.Delay(2000, cancellationToken: this.stateTransitionToken);
            // Move food to mouth
            await LMotion.Create(
                    this.rightHandTarget.position,
                    this.head.position + transform.forward * 0.1f,
                    0.5f)
                .WithEase(Ease.OutBack)
                .BindToPosition(this.rightHandTarget)
                .AddTo(gameObject);
            AudioManager audioManager = FindFirstObjectByType<AudioManager>();
            audioManager.Play("SFX", 0, 0.5f);
            await UniTask.Delay(3000, cancellationToken: this.stateTransitionToken);
            Destroy(food);
            // Move hand back to original position
            await LMotion.Create(this.rightHandTarget.localPosition, rightHandOriginalLocalPosition, 0.5f)
                .WithEase(Ease.OutBack)
                .BindToLocalPosition(this.rightHandTarget)
                .AddTo(gameObject);
        }
        finally
        {
            this.rightHandRig.weight = 0f;
            this.rightHandTarget.localPosition = rightHandOriginalLocalPosition;
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
            if (this.petStats.Hunger > 70 && this.stateMachine.CanFire(Trigger.StartSearchingFood))
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
                var rb = gameObject.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                while (true)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                    {
                        agent.Warp(hit.position);
                        break;
                    }
                    await UniTask.Yield(token);
                }
                Destroy(rb);
                animator.SetBool("isLeaping", false);
            }
            await UniTask.Yield(token);
        }
    }

    public void TriggerFollow()
    {
        if(this.stateMachine.State == State.Following)
        {
            this.stateTransitionTokenSource.Cancel();
            this.stateMachine.Fire(Trigger.StopFollowing);
        }
        else if(this.stateMachine.CanFire(Trigger.Follow))
        {
            this.stateTransitionTokenSource.Cancel();
            this.stateMachine.Fire(Trigger.Follow);
        }
        else
        {
            Debug.LogWarning($"Cannot trigger follow from state {this.stateMachine.State}");
        }
    }
}
