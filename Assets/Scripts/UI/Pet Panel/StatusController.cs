using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;

public class StatusController : MonoBehaviour
{
    [Header("寵物狀態數值")]
    [SerializeField] private PetStats petStats;

    [Header("狀態條物件遮罩")]
    [SerializeField] private GameObject happinessBarMask;
    [SerializeField] private GameObject hungerBarMask;
    [SerializeField] private GameObject energyBarMask;

    [Header("名稱、標籤顯示文字")]
    [SerializeField] private TMP_Text petNameText;
    [SerializeField] private TMP_Text petLabelText;

    [Header("種族顯示文字")]
    [SerializeField] private TMP_Text petRaceText;

    [Header("狀態顯示文字")]
    [SerializeField] private TMP_Text petMoodText;
    [Header("關係顯示文字")]
    [SerializeField] private TMP_Text petRelationshipText;
    [Header("狀態標籤")]
    [SerializeField] private TMP_Text petStateLabelText;


    public enum RelationshipStatus{
        Friendly,
        Stranger,
        Hostile,
        Neutral,
        Ally,
        Rival
    }

    public enum MoodStatus{
        Happy,
        Hungry,
        Tired,
        Angry,
        Bored,
        Excited,
        Sad,
        Neutral
    }
    
    private float startPositionX = 6.56f;
    private float endPositionX = 0.45f;

    private void Start()
    {
        UniTask.Void(async () => {
            var token = this.GetCancellationTokenOnDestroy();
            while(!token.IsCancellationRequested) {
                await UniTask.Delay(100, cancellationToken: token);
                this.updateStateLabel();
           }
        });
    }

    void Update()
    {
        UpdatePercentageWithBar(happinessBarMask, petStats.Happiness);
        UpdatePercentageWithBar(hungerBarMask, petStats.Hunger);
        UpdatePercentageWithBar(energyBarMask, petStats.Health);

        updatePetName(petStats.PetName);
        updatePetLabel(petStats.PetLabel);
        updatePetRace(petStats.PetRace);

        MoodStatus nowMood = EvaluateNowMood(petStats.Happiness, petStats.Hunger, petStats.Health);
        petMoodText.text = getMoodLabel(nowMood);

        RelationshipStatus nowRelationship = EvaluateRelationship(petStats.Favorability);
        petRelationshipText.text = getRelationshipLabel(nowRelationship);
    }

    private void UpdatePercentageWithBar(GameObject barMask, int value)
    {
        barMask.transform.localPosition = new Vector3(Mathf.Lerp(endPositionX, startPositionX, value / 100f), barMask.transform.localPosition.y, barMask.transform.localPosition.z);
    }

    private void updatePetName(string name){
        if (name != null && name != ""){
            petNameText.text = name;
        }
    }

    private void updatePetLabel(string label){
        if (label != null && label != ""){
            petLabelText.text = label;
        }
    }

    private void updatePetRace(string race){
        if (race != null && race != ""){
            petRaceText.text = race;
        }
    }

    private string getRelationshipLabel(RelationshipStatus relationship){
        switch (relationship){
            case RelationshipStatus.Friendly: return "友好";
            case RelationshipStatus.Stranger: return "陌生";
            case RelationshipStatus.Hostile: return "敵對";
            case RelationshipStatus.Neutral: return "普通";
            case RelationshipStatus.Ally: return "一生的夥伴";
            case RelationshipStatus.Rival: return "宿敵";
            default: return "未知";
        }
    }

    private string getMoodLabel(MoodStatus mood){
        switch (mood){
            case MoodStatus.Happy: return "開心";
            case MoodStatus.Hungry: return "肚子餓";
            case MoodStatus.Tired: return "疲累";
            case MoodStatus.Angry: return "生氣";
            case MoodStatus.Bored: return "無聊";
            case MoodStatus.Excited: return "興奮";
            case MoodStatus.Sad: return "難過";
            case MoodStatus.Neutral: return "平靜";
            default: return "未知";
        }
    }

    public MoodStatus EvaluateNowMood(float happiness, float hunger, float energy){
        if (hunger > 80f){
            return MoodStatus.Hungry;
        }
        if (energy < 20f){
            return MoodStatus.Tired;
        }
        if (happiness > 70f){
            return MoodStatus.Happy;
        }
        if (happiness < 20f){
            return MoodStatus.Sad;
        }
        return MoodStatus.Neutral;
    }

    public RelationshipStatus EvaluateRelationship(float favorability){
    if (favorability >= 80f){
        return RelationshipStatus.Ally;
    }
    if (favorability >= 60f){
        return RelationshipStatus.Friendly;
    }
    if (favorability >= 40f){
        return RelationshipStatus.Neutral;
    }
    if (favorability >= 20f){
        return RelationshipStatus.Stranger;
    }
    return RelationshipStatus.Hostile;
}
    private void updateStateLabel() {
        var pet = FindFirstObjectByType<Pet>();
        if (pet != null) {
            Debug.LogWarning("Cannot find pet object in scene.");
        }

        this.petStateLabelText.text = pet.StateMachine.State.ToString();
    }
}
