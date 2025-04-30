using TMPro;
using UnityEngine;

public class ShowPetState : MonoBehaviour
{
    private TMP_Text text;
    private Pet pet;

    void Start()
    {
        this.text = GetComponent<TMP_Text>();
        this.pet = FindFirstObjectByType<Pet>();
    }

    void Update()
    {
        this.text.text = $"Pet State: {this.pet.StateMachine.State}";
    }
}
