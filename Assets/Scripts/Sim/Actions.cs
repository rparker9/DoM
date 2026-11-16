namespace Dungeon.Sim
{
    public enum ActionType { Wait, Step, UseMove }

    /// <summary>What an actor does with its turn.</summary>
    public struct GameAction
    {
        public ActionType Type;
        public int Dx, Dy;   // Step: direction. UseMove: direction, or (0,0) = current facing.
        public int Slot;     // UseMove: which of the actor's moves (0 = first)

        public static GameAction Wait() => new GameAction { Type = ActionType.Wait };
        public static GameAction Step(int dx, int dy) => new GameAction { Type = ActionType.Step, Dx = dx, Dy = dy };
        public static GameAction UseMove(int slot) => new GameAction { Type = ActionType.UseMove, Slot = slot };
        public static GameAction UseMove(int slot, int dx, int dy) =>
            new GameAction { Type = ActionType.UseMove, Slot = slot, Dx = dx, Dy = dy };
    }

    /// <summary>Energy costs for actions that aren't moves. 100 = one normal turn.
    /// A move's cost comes from its MoveDef.EnergyCost.</summary>
    public static class ActionCosts
    {
        public const int Wait = 100;
        public const int Step = 100;
    }
}
