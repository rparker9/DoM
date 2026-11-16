using System.Collections.Generic;

namespace Dungeon.Sim
{
    /// <summary>What area a move hits.</summary>
    public enum MoveShape
    {
        /// <summary>Straight ahead, up to Range tiles. Stops at the first entity or wall.</summary>
        Line,
        /// <summary>All 8 tiles around the user.</summary>
        AllAround
    }

    /// <summary>
    /// A move (an attack or skill) as plain data. Only enemies take damage.
    /// In Unity you author these as MoveAssets; MoveAsset.ToDef() makes one of these.
    /// </summary>
    public sealed class MoveDef
    {
        public string Name = "Tackle";

        /// <summary>Energy cost. 100 = one normal turn. Cheaper moves let the user act again sooner.</summary>
        public int EnergyCost = 100;

        public MoveShape Shape = MoveShape.Line;

        /// <summary>Line moves only: how many tiles ahead it reaches. 1 = the tile right in front.</summary>
        public int Range = 1;

        /// <summary>Base damage. The user's Attack is added on top (damage is never below 1).</summary>
        public int Power = 4;
    }

    /// <summary>
    /// A kind of actor (the leader, an ally, a slime) as plain data.
    /// In Unity you author these as ActorAssets; ActorAsset.ToDef() makes one of these.
    /// </summary>
    public sealed class ActorDef
    {
        public string Name = "Actor";
        public int MaxHp = 20;

        /// <summary>Added to the Power of every move this actor uses. Can be negative.</summary>
        public int Attack = 0;

        /// <summary>100 = normal. 200 = twice as many turns. 50 = half as many.</summary>
        public int SpeedPercent = 100;

        /// <summary>Gray-box color, as "#RRGGBB".</summary>
        public string ColorHex = "#FFFFFF";

        /// <summary>Up to 4 moves. The AI uses the first move in the list that can hit something.</summary>
        public List<MoveDef> Moves = new List<MoveDef>();
    }
}
