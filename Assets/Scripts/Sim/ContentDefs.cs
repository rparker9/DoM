using System.Collections.Generic;

namespace Dungeon.Sim
{
    public enum MoveShape
    {
        Line,       // straight ahead up to Range tiles; stops at the first actor or wall
        AllAround   // all 8 tiles around the user
    }

    public enum MoveEffect
    {
        Damage,   // hurts enemies
        Heal      // restores HP to allies
    }

    /// <summary>A move (attack or skill) as plain data. Made from a MoveAsset in Unity. Only enemies are hit.</summary>
    public sealed class MoveDef
    {
        public string Name = "Tackle";
        public MoveEffect Effect = MoveEffect.Damage;   // Heal: Power = HP restored, hits allies instead
        public int EnergyCost = 100;           // 100 = one turn; cheaper moves let the user act again sooner
        public MoveShape Shape = MoveShape.Line;
        public int Range = 1;                  // Line only: tiles ahead, 1 = adjacent
        public int Power = 4;                  // damage = Power + user's Attack, at least 1
    }

    /// <summary>A kind of actor (party member or monster) as plain data. Made from an ActorAsset in Unity.</summary>
    public sealed class ActorDef
    {
        public string Name = "Actor";
        public int MaxHp = 20;
        public int Attack = 0;                 // added to every move's Power; can be negative
        public int SpeedPercent = 100;         // 100 = normal, 200 = twice as many turns
        public string ColorHex = "#FFFFFF";    // gray-box color, "#RRGGBB"
        public List<MoveDef> Moves = new List<MoveDef>();   // up to 4; the AI prefers earlier ones
    }
}
