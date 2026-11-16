namespace Dungeon.Sim
{
    public enum ActionType { Wait, Step, UseMove }

    /// <summary>What an actor does with its turn. Make one with Wait(), Step() or UseMove().</summary>
    public struct GameAction
    {
        public ActionType Type;
        public int Dx, Dy;   // Step: direction. UseMove: direction, or (0, 0) for current facing.
        public int Slot;     // UseMove: which move, 0 = first

        public static GameAction Wait() => new GameAction { Type = ActionType.Wait };

        /// <param name="dx">Direction x (-1, 0, 1).</param>
        /// <param name="dy">Direction y (-1, 0, 1).</param>
        public static GameAction Step(int dx, int dy) =>
            new GameAction { Type = ActionType.Step, Dx = dx, Dy = dy };

        /// <summary>Use a move in the direction the actor is facing.</summary>
        /// <param name="slot">Which move, 0 = first.</param>
        public static GameAction UseMove(int slot) =>
            new GameAction { Type = ActionType.UseMove, Slot = slot };

        /// <summary>Turn to (dx, dy), then use a move.</summary>
        /// <param name="slot">Which move, 0 = first.</param>
        /// <param name="dx">Direction x (-1, 0, 1).</param>
        /// <param name="dy">Direction y (-1, 0, 1).</param>
        public static GameAction UseMove(int slot, int dx, int dy) =>
            new GameAction { Type = ActionType.UseMove, Slot = slot, Dx = dx, Dy = dy };
    }

    /// <summary>Energy costs of actions that aren't moves (a move's cost is on its MoveDef). 100 = one turn.</summary>
    public static class ActionCosts
    {
        public const int Wait = 100;
        public const int Step = 100;
    }
}
