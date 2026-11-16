using System.Collections.Generic;
using UnityEngine;
using Dungeon.Sim;

/// <summary>
/// An actor you author in the Unity editor: the leader, an ally or a monster.
/// Right-click in the Project window: Create > Dungeon > Actor.
/// Then drag it into the DungeonRunner's Party or Monsters list.
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Actor", fileName = "New Actor")]
public sealed class ActorAsset : ScriptableObject
{
    [Tooltip("Shown in the HUD. Leave empty to use the asset's file name.")]
    public string displayName = "";

    [Min(1)] public int maxHp = 20;

    [Tooltip("Added to the Power of every move this actor uses. Can be negative.")]
    public int attack = 0;

    [Tooltip("100 = normal. 200 = twice as many turns. 50 = half as many.")]
    [Range(10, 400)] public int speedPercent = 100;

    [Tooltip("Gray-box color until there's real art.")]
    public Color color = Color.white;

    [Tooltip("Up to 4 moves (keys 1-4). The AI uses the first move in the list that can hit something.")]
    public List<MoveAsset> moves = new List<MoveAsset>();

    /// <summary>Converts this asset into the plain data the sim uses.</summary>
    public ActorDef ToDef()
    {
        var def = new ActorDef
        {
            Name = string.IsNullOrEmpty(displayName) ? name : displayName,
            MaxHp = maxHp,
            Attack = attack,
            SpeedPercent = speedPercent,
            ColorHex = "#" + ColorUtility.ToHtmlStringRGB(color)
        };
        foreach (var move in moves)
            if (move != null) def.Moves.Add(move.ToDef());
        return def;
    }
}
