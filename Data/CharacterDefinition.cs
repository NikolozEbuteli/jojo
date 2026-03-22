using UnityEngine;

[CreateAssetMenu(menuName = "JoJo/Character Definition", fileName = "CharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Character";

    [Header("Stats")]
    [Min(1f)] public float maxHealth = 100f;
    [Min(0f)] public float speed = 5f;

    [Header("Visuals")]
    public RuntimeAnimatorController animator;
}