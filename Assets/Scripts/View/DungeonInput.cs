using System;
using UnityEngine;
using Dungeon.Sim;
using Dungeon.Api;

/// <summary>
/// Turns keys into <see cref="DungeonGame"/> API calls. Nothing else.
///
/// <para>Runs before other scripts each frame (see DefaultExecutionOrder), so a key pressed this
/// frame reaches the game before DungeonRunner calls game.Update().</para>
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class DungeonInput : MonoBehaviour
{
    const int MaxMoveKeys = 4;     // keys 1-4
    const int MaxPartyKeys = 4;    // F1-F4

    DungeonGame _game;

    public void Bind(DungeonGame game) => _game = game;

    void Update()
    {
        if (_game == null) return;

        _game.FastForward = KeyInput.Held(K.Ctrl);
        HandleFreeCommands();
        if (!_game.IsGameOver) HandleTurnActions();
    }

    /// <summary>No turn used: the M toggle, switching leader (Tab, F1-F4), party orders (F/H/T).</summary>
    void HandleFreeCommands()
    {
        if (KeyInput.Down(K.M)) _game.MovesPlayOneAtATime = !_game.MovesPlayOneAtATime;

        // Switching only works on your turn with nothing animating (game.CanSwitchLeader).
        if (KeyInput.Down(K.Tab)) _game.SwitchToNextMember();
        for (int i = 0; i < _game.Party.Count && i < MaxPartyKeys; i++)
            if (KeyInput.Down((K)((int)K.F1 + i))) _game.SetLeader(_game.Party[i]);

        // Orders go to the whole party, so they still apply after you switch leader.
        if (KeyInput.Down(K.F)) _game.OrderParty(OrderType.Follow);
        if (KeyInput.Down(K.H)) _game.OrderParty(OrderType.Hold);
        if (KeyInput.Down(K.T)) _game.OrderPartyToAttack(_game.NearestEnemyToLeader());
    }

    /// <summary>Actions that use the leader's turn: step, use a move, wait. Plus turning in place.</summary>
    void HandleTurnActions()
    {
        // Shift + direction: turn without moving (free).
        if (KeyInput.Held(K.Shift))
        {
            if (ReadDirection(KeyInput.Down, out int faceX, out int faceY)) _game.Face(faceX, faceY);
            return;
        }

        // When the player can act, holding a direction walks continuously.
        // While animations play, only a fresh press counts (and the game queues it).
        Func<K, bool> isPressed;
        if (_game.CanPlayerAct) isPressed = KeyInput.Held;
        else isPressed = KeyInput.Down;

        if (ReadDirection(isPressed, out int dx, out int dy)) { _game.Step(dx, dy); return; }

        for (int slot = 0; slot < MaxMoveKeys; slot++)
            if (KeyInput.Down((K)((int)K.Alpha1 + slot))) { _game.UseMove(slot); return; }

        if (KeyInput.Down(K.Space)) { _game.UseMove(0); return; }
        if (KeyInput.Down(K.R)) { _game.Wait(); return; }
    }

    /// <summary>Q/E/Z/C are diagonals. WASD and arrows can combine (W + D = up-right). +y is up.</summary>
    static bool ReadDirection(Func<K, bool> isPressed, out int dx, out int dy)
    {
        dx = 0; dy = 0;
        if (isPressed(K.Q)) { dx = -1; dy = 1; return true; }
        if (isPressed(K.E)) { dx = 1; dy = 1; return true; }
        if (isPressed(K.Z)) { dx = -1; dy = -1; return true; }
        if (isPressed(K.C)) { dx = 1; dy = -1; return true; }
        if (isPressed(K.W) || isPressed(K.Up)) dy += 1;
        if (isPressed(K.S) || isPressed(K.Down)) dy -= 1;
        if (isPressed(K.D) || isPressed(K.Right)) dx += 1;
        if (isPressed(K.A) || isPressed(K.Left)) dx -= 1;
        return dx != 0 || dy != 0;
    }
}
