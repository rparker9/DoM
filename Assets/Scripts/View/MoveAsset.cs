using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Dungeon.Sim;

/// <summary>
/// A move you author in the Unity editor. Right-click in the Project window:
/// Create > Dungeon > Move. Then add it to an ActorAsset's move list.
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Move", fileName = "New Move")]
public sealed class MoveAsset : ScriptableObject
{
    /// <summary>One line in the Effects list.</summary>
    [Serializable]
    public sealed class Effect
    {
        public EffectKind kind = EffectKind.Damage;

        [Tooltip("Damage/Heal: HP. Knockback: tiles. Drain/Recoil: % of the damage this hit dealt.")]
        public int amount = 4;

        [Tooltip("% chance per target. 0 counts as 100.")]
        [Range(0, 100)] public int chance = 100;
    }

    [Tooltip("Shown in the HUD. Leave empty to use the asset's file name.")]
    public string displayName = "";

    [Tooltip("100 = one normal turn. Cheaper moves let the user act again sooner.")]
    [Min(1)] public int energyCost = 100;

    [Tooltip("Optional. Empty = gray-box placeholder visuals.")]
    public MoveVisual visual;

    [Header("Who it reaches")]
    [Tooltip("Line: straight ahead. Around: every tile within Range. Self: the user. Team: the user's whole team.")]
    [FormerlySerializedAs("shape")] public MoveArea area = MoveArea.Line;

    [Tooltip("Line: tiles ahead. Around: radius.")]
    [Min(1)] public int range = 1;

    [Tooltip("Line: keep going past the first actor.")]
    public bool pierce;

    [Tooltip("Line: > 0 also affects everyone this close to where it lands.")]
    [Min(0)] public int burstRadius;

    public TargetFilter filter = TargetFilter.Enemies;

    [Header("How it gets there")]
    public Delivery delivery = Delivery.Contact;

    [Tooltip("Everything below repeats this many times.")]
    [Min(1)] public int hits = 1;

    [Header("What it does to each target")]
    public List<Effect> effects = new List<Effect>();

    /// <summary>Converts this asset into the plain data the sim uses.</summary>
    public MoveDef ToDef()
    {
        if (visual != null) visual.Register();

        var def = new MoveDef
        {
            Name = string.IsNullOrEmpty(displayName) ? name : displayName,
            VisualId = visual != null ? visual.name : "",
            EnergyCost = energyCost,
            Area = area,
            Range = range,
            Pierce = pierce,
            BurstRadius = burstRadius,
            Filter = filter,
            Delivery = delivery,
            Hits = hits
        };

        foreach (var e in effects)
            def.Effects.Add(new EffectEntry { Kind = e.kind, Amount = e.amount, Chance = e.chance <= 0 ? 100 : e.chance });

        // If you make a move with no effects, it will be ignored by the sim. This is useful for placeholder moves that just show visuals, but otherwise it's probably a mistake.
        if (effects.Count == 0)
        {
            // Add a dummy effect so the sim will run it. It won't do anything, but at least it won't be ignored.
            def.Effects.Add(new EffectEntry
            {
                Kind = EffectKind.Damage,
                Amount = 0,
                Chance = 100,
            });
        }
        return def;
    }
}
