using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Dungeon.Sim;
using Dungeon.Api;

/// <summary>
/// The debug panel in the top-left corner. Only reads from the API.
/// Note that it shows the SIM's state, which runs ahead of what's on screen.
/// </summary>
public sealed class DungeonHud : MonoBehaviour
{
    const int MaxMovesShown = 4;
    const int MaxTurnsShown = 8;

    DungeonGame _game;
    readonly List<TurnInfo> _upcomingTurns = new List<TurnInfo>();
    readonly StringBuilder _text = new StringBuilder();

    public void Bind(DungeonGame game) => _game = game;

    void OnGUI()
    {
        if (_game == null) return;
        _text.Clear();

        AppendStatus();
        AppendMoves();
        AppendParty();
        AppendTurnOrder();
        AppendControls();

        var rect = new Rect(10, 10, 400, 470);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), _text.ToString());
    }

    void AppendStatus()
    {
        if (_game.IsGameOver) _text.AppendLine("GAME OVER");
        else
        {
            string state = _game.CanSwitchLeader ? "  [READY, can switch]"
                         : _game.CanPlayerAct ? "  [READY]"
                         : "  [playing...]";
            _text.Append("Turn: ").Append(_game.GetActor(_game.WhoseTurn).Name).Append(state)
                 .Append("   tick ").Append(_game.Tick).AppendLine();
        }
        _text.Append("Playback ").Append(_game.PlaybackTime.ToString("0.00"))
             .Append("s  left to play ").Append(_game.TimeLeftToPlay.ToString("0.00"))
             .Append("s  speed x").Append(_game.PlaybackSpeed.ToString("0.0")).AppendLine();
        _text.Append("Moves one at a time (M): ").Append(_game.MovesPlayOneAtATime ? "ON" : "OFF").AppendLine();
    }

    /// <summary>The moves of whoever's turn it is, with their key.</summary>
    void AppendMoves()
    {
        if (_game.IsGameOver) return;
        var def = _game.GetActor(_game.WhoseTurn).Def;
        if (def == null) return;

        _text.AppendLine().AppendLine("Moves:");
        for (int i = 0; i < def.Moves.Count && i < MaxMovesShown; i++)
        {
            var m = def.Moves[i];
            _text.Append(' ').Append(i + 1).Append("  ").Append(m.Name)
                 .Append("  power ").Append(m.Power)
                 .Append("  cost ").Append(m.EnergyCost)
                 .Append(m.Shape == MoveShape.AllAround ? "  all around" : "  range " + m.Range)
                 .AppendLine();
        }
    }

    /// <summary>Every party member, with "LEADER" next to the one you control.</summary>
    void AppendParty()
    {
        _text.AppendLine().AppendLine("Party:");
        for (int i = 0; i < _game.Party.Count; i++)
        {
            var a = _game.GetActor(_game.Party[i]);
            _text.Append(" F").Append(i + 1).Append("  ").Append(a.Name);
            if (!a.IsAlive) { _text.AppendLine("  (down)"); continue; }
            _text.Append("  ").Append(a.IsLeader ? "LEADER" : "orders: " + a.Orders)
                 .Append("  hp ").Append(a.Hp).Append('/').Append(a.MaxHp).AppendLine();
        }
    }

    void AppendTurnOrder()
    {
        _text.AppendLine().AppendLine("Turn order (sim):");
        if (!_game.WhoseTurn.IsNone)
            _text.Append(" > ").Append(_game.GetActor(_game.WhoseTurn).Name).AppendLine("  (now)");

        _game.GetUpcomingTurns(_upcomingTurns, MaxTurnsShown);
        foreach (var turn in _upcomingTurns)
            _text.Append("   ").Append(turn.Name).Append("  @").Append(turn.Tick).AppendLine();
    }

    void AppendControls()
    {
        _text.AppendLine()
             .AppendLine("WASD/arrows + QEZC diagonals: step")
             .AppendLine("1-4 or Space: use move   Shift+dir: turn   R: wait")
             .AppendLine("Tab / F1-F4: switch leader (your turn, nothing moving)")
             .AppendLine("Party orders: F follow leader  H hold  T attack nearest enemy")
             .AppendLine("Hold Ctrl: fast-forward");
    }
}
