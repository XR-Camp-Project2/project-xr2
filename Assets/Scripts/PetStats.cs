using UnityEngine;

[CreateAssetMenu(fileName = "PetStats", menuName = "Scriptable Objects/PetStats")]
public class PetStats : ScriptableObject
{
    public string PetName;
    public int Health;
    public int Hunger;
}
