using UnityEngine;
using Dungeon.Sim;

/// <summary>
/// A move you author in the Unity editor. Right-click in the Project window:
/// Create > Dungeon > Move. Then add it to an ActorAsset's move list.
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Move", fileName = "New Move")]
public sealed class MoveAsset : ScriptableObject
{
    [Tooltip("Shown in the HUD. Leave empty to use the asset's file name.")]
    public string displayName = "";

    [Tooltip("Damage hurts enemies. Heal restores Power HP to allies instead (AllAround also heals the user).")]
    public MoveEffect effect = MoveEffect.Damage;

    [Tooltip("100 = one normal turn. Cheaper moves let the user act again sooner.")]
    [Min(1)] public int energyCost = 100;

    [Tooltip("Line: straight ahead, stops at the first actor or wall. AllAround: all 8 tiles around the user.")]
    public MoveShape shape = MoveShape.Line;

    [Tooltip("Line moves only: how many tiles ahead it reaches. 1 = the tile right in front.")]
    [Min(1)] public int range = 1;

    [Tooltip("Base damage. The user's Attack is added on top (never below 1). Only enemies are hit.")]
    public int power = 4;

    /// <summary>Converts this asset into the plain data the sim uses.</summary>
    public MoveDef ToDef() => new MoveDef
    {
        Name = string.IsNullOrEmpty(displayName) ? name : displayName,
        Effect = effect,
        EnergyCost = energyCost,
        Shape = shape,
        Range = range,
        Power = power
    };
}
