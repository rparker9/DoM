using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Dungeon.Sim;
using Dungeon.Playback;
using Dungeon.Api;

/// <summary>
/// Runs a dungeon floor in Unity, using only the <see cref="DungeonGame"/> API.
/// Put it on an empty GameObject in an empty scene and press Play.
///
/// <para><b>What this script does, in order</b></para>
/// <list type="number">
///   <item><b>Start:</b> creates a <see cref="DungeonGame"/>, builds the floor, spawns actors, creates a gray box
///         for everything, then calls <c>game.Begin()</c>.</item>
///   <item><b>Every frame:</b> reads keys and calls <c>game.Step</c>, <c>game.UseMove</c> and so on, then
///         <c>game.Update(Time.deltaTime)</c>.</item>
///   <item><b>Animation callbacks:</b> the game calls <see cref="StartAnim"/>, <see cref="UpdateAnim"/> and
///         <see cref="FinishAnim"/> on this script to move the gray boxes.</item>
/// </list>
///
/// <para>Content: drag ActorAssets into the slots below. Empty lists use <see cref="DefaultContent"/>.</para>
/// <para>The party is a team of equals. The leader is just whichever member you control right now.</para>
/// </summary>
public sealed class DungeonRunner : MonoBehaviour, IEventAnimator
{
    // =====================================================================================
    //  Inspector settings
    // =====================================================================================

    [Header("Content (empty = built-in defaults)")]
    [Tooltip("Up to 4 equal members. The first starts as leader. Tab or F1-F4 switches who you control.")]
    [SerializeField] List<ActorAsset> party = new List<ActorAsset>();
    [Tooltip("Monsters are picked from this list in turn until Monster Count is reached.")]
    [SerializeField] List<ActorAsset> monsters = new List<ActorAsset>();
    [SerializeField] int monsterCount = 12;

    [Header("Floor")]
    [Tooltip("Same seed = same floor and same monster behavior every run.")]
    [SerializeField] int seed = 12345;
    [SerializeField] int width = 26;
    [SerializeField] int height = 18;
    [SerializeField] int randomWalls = 18;

    [Header("Playback")]
    [Tooltip("Moves play one at a time, even between unrelated actors. Toggle with M.")]
    [SerializeField] bool movesPlayOneAtATime = true;
    [Tooltip("Events farther than this many tiles from the leader play instantly.")]
    [SerializeField] int viewRadius = 6;
    [SerializeField] float fastForwardSpeed = 4f;

    [Header("Animation durations (seconds at normal speed)")]
    [SerializeField] float stepDuration = 0.16f;
    [SerializeField] float moveDuration = 0.22f;
    [SerializeField] float hurtDuration = 0.18f;
    [SerializeField] float dieDuration = 0.25f;

    // =====================================================================================
    //  Layout constants
    // =====================================================================================

    const float CameraZ = -10f;
    const float CameraSize = 7f;
    const float CameraFollowSharpness = 8f;
    const float TileZ = 0.5f;     // floor and walls behind the actors (actors are at z = 0)
    const float MarkerZ = 0.05f;
    const int StartX = 4, StartY = 4;   // where the party starts
    const int MaxMoveKeys = 4;          // keys 1-4
    const int MaxPartySize = 4;         // F1-F4

    // =====================================================================================
    //  State
    // =====================================================================================

    DungeonGame _game;
    Shader _shader;
    Camera _cam;
    Transform _marker;

    /// <summary>One gray box per actor. A box is removed when its death finishes playing.</summary>
    readonly Dictionary<Entity, EntityView> _views = new Dictionary<Entity, EntityView>();
    readonly List<TurnInfo> _upcomingTurns = new List<TurnInfo>();
    readonly StringBuilder _hudText = new StringBuilder();

    // =====================================================================================
    //  Start: build everything
    // =====================================================================================

    void Start()
    {
        _shader = Shader.Find("Sprites/Default");
        if (_shader == null) _shader = Shader.Find("Unlit/Color");

        _game = new DungeonGame(seed, width, height, animator: this);
        ApplySettings();

        BuildFloor();
        SpawnActors();

        BuildMapViews();
        foreach (var e in _game.AllActors) SpawnView(e);
        _marker = EntityView.MakeQuad(null, "TurnMarker", new Vector3(0f, 0f, MarkerZ), Vector3.one,
                                      new Color(1f, 1f, 1f, 0.25f), _shader).transform;
        SetUpCamera();

        _game.Begin();
    }

    void BuildFloor()
    {
        _game.SurroundWithWalls();
        _game.AddRandomWalls(randomWalls, keepClearX: StartX, keepClearY: StartY);
    }

    void SpawnActors()
    {
        // Check that the party and monster lists aren't empty and aren't unpopulated. If so, end the game immediately.
        if (party.Count == 0 || monsters.Count == 0)
        {
            Debug.LogWarning("Party or monster list is empty. Ending game.");
            _game.EndGame();
            return;
        }

        if (party.TrueForAll(a => a == null) || monsters.TrueForAll(a => a == null))
        {
            Debug.LogWarning("Party or monster list is unpopulated. Ending game.");
            _game.EndGame();
            return;
        }

        Debug.Log($"Spawning {party.Count} party members and {monsterCount} monsters on a {width}x{height} floor.");

        // The party starts bunched together. The first member spawned starts as leader.
        int[,] partySpots = { { 0, 0 }, { -1, 0 }, { 0, -1 }, { -1, -1 } };
        int partySize = Mathf.Min(party.Count, MaxPartySize);
        for (int i = 0; i < partySize; i++)
        {
            var def = party[i].ToDef();
            Debug.Log($"Spawning party member {def.Name} at ({StartX + partySpots[i, 0]}, {StartY + partySpots[i, 1]})");
            int x = StartX + partySpots[i, 0], y = StartY + partySpots[i, 1];
            if (_game.IsFree(x, y)) _game.SpawnPartyMember(def, x, y);
        }

        // Monsters go on random free tiles, away from the party.
        for (int i = 0; i < monsterCount; i++)
        {
            ActorDef def = monsters[i % monsters.Count].ToDef();

            if (_game.TryFindFreeTile(StartX, StartY, minDistance: 6, out int x, out int y))
                _game.SpawnMonster(def, x, y);
        }
    }

    void BuildMapViews()
    {
        var root = new GameObject("Map").transform;
        var floorColor = new Color(0.16f, 0.16f, 0.2f, 1f);
        var wallColor = new Color(0.45f, 0.42f, 0.38f, 1f);

        for (int y = 0; y < _game.Height; y++)
        for (int x = 0; x < _game.Width; x++)
        {
            var color = _game.IsWall(x, y) ? wallColor : floorColor;
            var tile = EntityView.MakeQuad(root, $"Tile {x},{y}", new Vector3(x, y, TileZ),
                                           new Vector3(0.96f, 0.96f, 1f), color, _shader);
            tile.gameObject.isStatic = true;
        }
    }

    void SpawnView(Entity e)
    {
        var a = _game.GetActor(e);
        if (!ColorUtility.TryParseHtmlString(a.Def.ColorHex, out var color)) color = Color.white;
        _views[e] = EntityView.Create(e, a.Name, color, a.X, a.Y, a.Hp, a.MaxHp, a.FacingX, a.FacingY, _shader);
    }

    void SetUpCamera()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            _cam = go.AddComponent<Camera>();
        }
        _cam.orthographic = true;
        _cam.orthographicSize = CameraSize;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        _cam.transform.rotation = Quaternion.identity;
        FollowCamera(snap: true);
    }

    // =====================================================================================
    //  Every frame
    // =====================================================================================

    void Update()
    {
        if (_game == null) return;

        ApplySettings();                  // so Inspector changes work while playing
        HandleInput();                    // may resolve turns and add new events
        _game.Update(Time.deltaTime);     // plays animations; runs a queued action when ready

        UpdateMarker();
        FollowCamera(snap: false);
    }

    void ApplySettings()
    {
        _game.MovesPlayOneAtATime = movesPlayOneAtATime;
        _game.ViewRadius = viewRadius;
        _game.FastForwardSpeed = fastForwardSpeed;
        _game.FastForward = KeyInput.Held(K.Ctrl);
    }

    // =====================================================================================
    //  Input
    // =====================================================================================

    void HandleInput()
    {
        HandleFreeCommands();
        if (_game.IsGameOver) return;

        // Shift + direction: turn without moving.
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

    /// <summary>No turn used: switching leader (Tab, F1-F4), party orders (F/H/T), the M toggle.</summary>
    void HandleFreeCommands()
    {
        if (KeyInput.Down(K.M)) movesPlayOneAtATime = !movesPlayOneAtATime;

        // Switch who you control. Only works on your turn with nothing animating (game.CanSwitchLeader).
        // The new leader acts right away; everyone else keeps following the party's orders.
        if (KeyInput.Down(K.Tab)) _game.SwitchToNextMember();
        for (int i = 0; i < _game.Party.Count && i < MaxPartySize; i++)
            if (KeyInput.Down((K)((int)K.F1 + i))) _game.SetLeader(_game.Party[i]);

        // Orders go to the whole party, so they still apply after you switch leader.
        if (KeyInput.Down(K.F)) _game.OrderParty(OrderType.Follow);
        if (KeyInput.Down(K.H)) _game.OrderParty(OrderType.Hold);
        if (KeyInput.Down(K.T)) _game.OrderPartyToAttack(_game.NearestEnemyToLeader());
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

    // =====================================================================================
    //  IEventAnimator: the game calls these to animate each event
    //
    //  Each event gets StartAnim once, UpdateAnim with t going from 0 to 1, then FinishAnim.
    //  Events the leader can't see get all of these in the same frame.
    // =====================================================================================

    public double DurationOf(SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped: return stepDuration;
            case SimEventType.UsedMove: return moveDuration;
            case SimEventType.Damaged: return hurtDuration;
            case SimEventType.Died: return dieDuration;
            default: return 0;   // Faced: turning is instant
        }
    }

    public void StartAnim(SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped:
                if (TryView(e.Actor, out var walker)) walker.BeginStep(e);
                break;
            case SimEventType.Faced:
                if (TryView(e.Actor, out var turner)) turner.SetFacing(e.DirX, e.DirY);
                break;
            case SimEventType.UsedMove:
                if (TryView(e.Actor, out var user)) user.BeginUseMove(e);
                break;
            case SimEventType.Damaged:
                if (TryView(e.Target, out var victim)) victim.BeginHurt(e.HpAfter);
                break;
        }
    }

    public void UpdateAnim(SimEvent e, float t)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped:
                if (TryView(e.Actor, out var walker)) walker.TickStep(t);
                break;
            case SimEventType.UsedMove:
                if (TryView(e.Actor, out var user)) user.TickUseMove(t);
                break;
            case SimEventType.Damaged:
                if (TryView(e.Target, out var victim)) victim.TickHurt(t);
                break;
            case SimEventType.Died:
                if (TryView(e.Actor, out var dying)) dying.TickDie(t);
                break;
        }
    }

    public void FinishAnim(SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped:
                if (TryView(e.Actor, out var walker)) walker.EndStep();
                break;
            case SimEventType.UsedMove:
                if (TryView(e.Actor, out var user)) user.EndUseMove();
                break;
            case SimEventType.Damaged:
                if (TryView(e.Target, out var victim)) victim.EndHurt();
                break;
            case SimEventType.Died:
                if (TryView(e.Actor, out var dying))
                {
                    dying.EndDie();
                    _views.Remove(e.Actor);
                }
                break;
        }
    }

    bool TryView(Entity e, out EntityView view) => _views.TryGetValue(e, out view) && view != null;

    // =====================================================================================
    //  Turn marker, camera, HUD
    // =====================================================================================

    void UpdateMarker()
    {
        bool show = _game.CanPlayerAct && TryView(_game.WhoseTurn, out _);
        _marker.gameObject.SetActive(show);
        if (!show) return;

        TryView(_game.WhoseTurn, out var view);
        var p = view.transform.position;
        _marker.position = new Vector3(p.x, p.y, MarkerZ);
    }

    void FollowCamera(bool snap)
    {
        Vector3 target = TryView(_game.Leader, out var leaderView)
            ? leaderView.transform.position
            : _cam.transform.position;
        target.z = CameraZ;

        if (snap) { _cam.transform.position = target; return; }
        float blend = 1f - Mathf.Exp(-CameraFollowSharpness * Time.deltaTime);
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, blend);
    }

    void OnGUI()
    {
        if (_game == null) return;
        _hudText.Clear();

        AppendStatus();
        AppendMoves();
        AppendParty();
        AppendTurnOrder();
        AppendControls();

        var rect = new Rect(10, 10, 400, 470);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), _hudText.ToString());
    }

    void AppendStatus()
    {
        if (_game.IsGameOver) _hudText.AppendLine("GAME OVER");
        else
        {
            _hudText.Append("Turn: ").Append(_game.GetActor(_game.WhoseTurn).Name)
                    .Append(_game.CanSwitchLeader ? "  [READY, can switch]" : _game.CanPlayerAct ? "  [READY]" : "  [playing...]")
                    .Append("   tick ").Append(_game.Tick).AppendLine();
        }
        _hudText.Append("Playback ").Append(_game.PlaybackTime.ToString("0.00"))
                .Append("s  left to play ").Append(_game.TimeLeftToPlay.ToString("0.00"))
                .Append("s  speed x").Append(_game.PlaybackSpeed.ToString("0.0")).AppendLine();
        _hudText.Append("Moves one at a time (M): ").Append(movesPlayOneAtATime ? "ON" : "OFF").AppendLine();
    }

    /// <summary>The moves of whoever's turn it is, with their key.</summary>
    void AppendMoves()
    {
        if (_game.IsGameOver) return;
        var def = _game.GetActor(_game.WhoseTurn).Def;
        if (def == null) return;

        _hudText.AppendLine().AppendLine("Moves:");
        for (int i = 0; i < def.Moves.Count && i < MaxMoveKeys; i++)
        {
            var m = def.Moves[i];
            _hudText.Append(' ').Append(i + 1).Append("  ").Append(m.Name)
                    .Append("  power ").Append(m.Power)
                    .Append("  cost ").Append(m.EnergyCost)
                    .Append(m.Shape == MoveShape.AllAround ? "  all around" : "  range " + m.Range)
                    .AppendLine();
        }
    }

    /// <summary>Every party member, with "LEADER" next to the one you control.</summary>
    void AppendParty()
    {
        _hudText.AppendLine().AppendLine("Party:");
        for (int i = 0; i < _game.Party.Count; i++)
        {
            var a = _game.GetActor(_game.Party[i]);
            _hudText.Append(" F").Append(i + 1).Append("  ").Append(a.Name);
            if (!a.IsAlive) { _hudText.AppendLine("  (down)"); continue; }
            _hudText.Append("  ").Append(a.IsLeader ? "LEADER" : "orders: " + a.Orders)
                    .Append("  hp ").Append(a.Hp).Append('/').Append(a.MaxHp).AppendLine();
        }
    }

    void AppendTurnOrder()
    {
        _hudText.AppendLine().AppendLine("Turn order (sim):");
        if (!_game.WhoseTurn.IsNone)
            _hudText.Append(" > ").Append(_game.GetActor(_game.WhoseTurn).Name).AppendLine("  (now)");

        _game.GetUpcomingTurns(_upcomingTurns, 8);
        foreach (var turn in _upcomingTurns)
            _hudText.Append("   ").Append(turn.Name).Append("  @").Append(turn.Tick).AppendLine();
    }

    void AppendControls()
    {
        _hudText.AppendLine()
                .AppendLine("WASD/arrows + QEZC diagonals: step")
                .AppendLine("1-4 or Space: use move   Shift+dir: turn   R: wait")
                .AppendLine("Tab / F1-F4: switch leader (your turn, nothing moving)")
                .AppendLine("Party orders: F follow leader  H hold  T attack nearest enemy")
                .AppendLine("Hold Ctrl: fast-forward");
    }
}
