using UnityEngine;

[CreateAssetMenu(fileName = "PetStats", menuName = "Scriptable Objects/PetStats")]
public class PetStats : ScriptableObject
{
    public string PetName;
    public string PetLabel;
    public string PetRace;
    public int Health;
    public int Hunger;
    public int Happiness;

    public float Favorability;
}
