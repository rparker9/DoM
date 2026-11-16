using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>Where a move reaches.</summary>
    public enum MoveArea
    {
        Line,     // straight ahead up to Range tiles
        Around,   // every tile within Range of the user (Range 1 = the 8 tiles around)
        Self,     // only the user
        Team      // every living member of the user's team, anywhere
    }

    /// <summary>Who in the area is affected.</summary>
    public enum TargetFilter
    {
        Enemies,
        Allies,          // not the user
        AlliesAndSelf,
        Everyone         // not the user
    }

    /// <summary>How a move gets from the user to its impact point. Changes timing and visuals only.</summary>
    public enum Delivery
    {
        Contact,      // no travel: the impact follows the wind-up
        Projectile,   // something flies to the impact tile
        Beam          // an instant line to the impact tile
    }

    public enum EffectKind
    {
        Damage,      // Amount + user's Attack, at least 1
        Heal,        // restores Amount HP, never above max
        Knockback,   // pushes the target Amount tiles away from the user
        Drain,       // the user heals Amount % of the damage this hit dealt
        Recoil       // the user takes Amount % of the damage this hit dealt
    }

    /// <summary>One thing a move does to each target.</summary>
    public sealed class EffectEntry
    {
        public EffectKind Kind;
        public int Amount;
        public int Chance = 100;   // % chance per target
    }

    /// <summary>A move as plain data. Made from a MoveAsset in Unity.</summary>
    public sealed class MoveDef
    {
        public string Name = "Tackle";
        public string VisualId = "";               // which MoveVisual draws it; "" = gray-box fallback
        public int EnergyCost = 100;               // 100 = one turn

        public MoveArea Area = MoveArea.Line;
        public int Range = 1;                      // Line: tiles ahead. Around: radius.
        public bool Pierce;                        // Line: keep going past the first actor
        public int BurstRadius;                    // > 0: also affects everyone this close to the impact tile
        public TargetFilter Filter = TargetFilter.Enemies;

        public Delivery Delivery = Delivery.Contact;
        public int Hits = 1;                       // the whole delivery and effects repeat this many times
        public List<EffectEntry> Effects = new List<EffectEntry>();
    }

    /// <summary>A kind of actor (party member or monster) as plain data. Made from an ActorAsset in Unity.</summary>
    public sealed class ActorDef
    {
        public string Name = "Actor";
        public int MaxHp = 20;
        public int Attack = 0;                     // added to every Damage effect; can be negative
        public int SpeedPercent = 100;             // 100 = normal, 200 = twice as many turns
        public string ColorHex = "#FFFFFF";        // gray-box color, "#RRGGBB"
        public List<MoveDef> Moves = new List<MoveDef>();   // up to 4
    }
}
