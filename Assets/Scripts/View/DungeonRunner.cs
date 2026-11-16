using System.Collections.Generic;
using UnityEngine;
using Dungeon.Sim;
using Dungeon.Api;

/// <summary>
/// Runs a dungeon floor using only <see cref="DungeonGame"/> API calls.
/// Put it on an empty GameObject and press Play. It adds the other components it needs.
///
/// <para><b>Who does what</b></para>
/// <list type="bullet">
///   <item><b>DungeonRunner</b> (this): builds the floor, spawns actors, starts the game, updates it every frame.</item>
///   <item><see cref="DungeonView"/>: draws the map and actors, and animates every event.</item>
///   <item><see cref="DungeonInput"/>: turns keys into API calls.</item>
///   <item><see cref="CameraFollow"/>, <see cref="TurnMarker"/>, <see cref="DungeonHud"/>: camera, turn highlight, debug panel.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRunner : MonoBehaviour
{
    // =====================================================================================
    //  Inspector settings
    // =====================================================================================

    [Header("Content (both lists need at least one actor)")]
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
    [Tooltip("Moves play one at a time, even between unrelated actors. M toggles it while playing.")]
    [SerializeField] bool movesPlayOneAtATime = true;
    [Tooltip("Events farther than this many tiles from the leader play instantly.")]
    [SerializeField] int viewRadius = 6;
    [SerializeField] float fastForwardSpeed = 4f;

    const int StartX = 4, StartY = 4;   // where the party starts
    const int MaxPartySize = 4;

    // =====================================================================================
    //  The game and the components that show it
    // =====================================================================================

    DungeonGame _game;
    DungeonView _view;
    DungeonInput _input;
    DungeonHud _hud;
    CameraFollow _cameraFollow;
    TurnMarker _turnMarker;

    void Awake()
    {
        // Use the components already on this GameObject, or add them. That way this one script is
        // enough to play, and you can still add any of them yourself to change their settings.
        _view = GetOrAdd<DungeonView>();
        _input = GetOrAdd<DungeonInput>();
        _hud = GetOrAdd<DungeonHud>();
        _cameraFollow = GetOrAdd<CameraFollow>();
        _turnMarker = GetOrAdd<TurnMarker>();
    }

    void Start()
    {
        // The view receives the animation callbacks, so it has to exist before the game.
        _game = new DungeonGame(seed, width, height, animator: _view);
        ApplySettings();

        BuildFloor();
        if (!SpawnActors())
        {
            enabled = false;   // nothing to play; the other components stay idle without a game
            return;
        }

        _view.Build(_game);
        _input.Bind(_game);
        _hud.Bind(_game);
        _cameraFollow.Bind(_game, _view);
        _turnMarker.Bind(_game, _view);

        _game.Begin();
    }

    /// <summary>Plays animations forward and runs a queued action once the player can act.
    /// DungeonInput runs earlier in the frame, so this frame's keys are already handled.</summary>
    void Update() => _game.Update(Time.deltaTime);

    /// <summary>Unity calls this when you change a value in the Inspector, including while playing.</summary>
    void OnValidate()
    {
        if (_game != null) ApplySettings();
    }

    void ApplySettings()
    {
        _game.MovesPlayOneAtATime = movesPlayOneAtATime;
        _game.ViewRadius = viewRadius;
        _game.FastForwardSpeed = fastForwardSpeed;
    }

    // =====================================================================================
    //  Building the floor
    // =====================================================================================

    void BuildFloor()
    {
        _game.SurroundWithWalls();
        _game.AddRandomWalls(randomWalls, keepClearX: StartX, keepClearY: StartY);
    }

    /// <summary>
    /// Spawns the party and the monsters. Returns false, after logging why, when there's nothing
    /// to play: no actors assigned in the Party or Monsters list.
    /// </summary>
    bool SpawnActors()
    {
        // Empty slots in the Inspector lists are skipped, so one missing actor can't crash the game.
        var partyAssets = party.FindAll(a => a != null);
        var monsterAssets = monsters.FindAll(a => a != null);

        if (partyAssets.Count == 0 || monsterAssets.Count == 0)
        {
            Debug.LogWarning("The Party or Monsters list has no actors assigned. Drag ActorAssets into both lists in the Inspector.");
            _game.EndGame();
            return false;
        }

        Debug.Log($"Spawning {partyAssets.Count} party members and {monsterCount} monsters on a {width}x{height} floor.");

        // The party starts bunched together. The first member spawned starts as leader.
        int[,] partySpots = { { 0, 0 }, { -1, 0 }, { 0, -1 }, { -1, -1 } };
        int partySize = Mathf.Min(partyAssets.Count, MaxPartySize);
        for (int i = 0; i < partySize; i++)
        {
            var def = partyAssets[i].ToDef();
            int x = StartX + partySpots[i, 0], y = StartY + partySpots[i, 1];
            Debug.Log($"Spawning party member {def.Name} at ({x}, {y})");
            if (_game.IsFree(x, y)) _game.SpawnPartyMember(def, x, y);
        }

        // Monsters go on random free tiles, away from the party.
        for (int i = 0; i < monsterCount; i++)
        {
            ActorDef def = monsterAssets[i % monsterAssets.Count].ToDef();
            if (_game.TryFindFreeTile(StartX, StartY, minDistance: 6, out int x, out int y))
                _game.SpawnMonster(def, x, y);
        }
        return true;
    }

    T GetOrAdd<T>() where T : Component
    {
        var existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }
}
