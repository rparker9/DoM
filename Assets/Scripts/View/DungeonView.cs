using System.Collections.Generic;
using UnityEngine;
using Dungeon.Sim;
using Dungeon.Playback;
using Dungeon.Api;

/// <summary>
/// Draws the dungeon and animates it. Owns one quad per map tile and one <see cref="EntityView"/>
/// gray box per actor.
///
/// <para>As the game's <see cref="IEventAnimator"/>, it gets three calls for every event:
/// <see cref="StartAnim"/> once, <see cref="UpdateAnim"/> with t going from 0 to 1, then
/// <see cref="FinishAnim"/>. Events the leader can't see get all three in the same frame.</para>
///
/// <para>It never reads positions from the sim, which runs ahead of the screen. Boxes only move
/// when their events play.</para>
/// </summary>
public sealed class DungeonView : MonoBehaviour, IEventAnimator
{
    [Header("Animation durations (seconds at normal speed). Move timings live in each MoveVisual.")]
    [SerializeField] float stepDuration = 0.16f;
    [SerializeField] float knockbackDuration = 0.15f;
    [SerializeField] float hurtDuration = 0.18f;
    [SerializeField] float healDuration = 0.25f;
    [SerializeField] float dieDuration = 0.25f;

    const float TileZ = 0.5f;   // floor and walls sit behind the actors (actors are at z = 0)

    DungeonGame _game;
    CuePlayer _cues;   // plays every MoveCue event

    /// <summary>One gray box per actor. A box is removed when its death finishes playing.</summary>
    readonly Dictionary<Entity, EntityView> _views = new Dictionary<Entity, EntityView>();

    // =====================================================================================
    //  Building
    // =====================================================================================

    /// <summary>Creates the map tiles and a box for every actor. Call after spawning, before Begin().</summary>
    /// <param name="game">The game to draw.</param>
    /// <param name="cues">Plays the moves' beats.</param>
    public void Build(DungeonGame game, CuePlayer cues)
    {
        _game = game;
        _cues = cues;
        BuildMap();
        foreach (var e in _game.AllActors) SpawnView(e);
    }

    /// <summary>Finds an actor's gray box. False if it has none (it died and its death has played).</summary>
    public bool TryGetView(Entity e, out EntityView view) => _views.TryGetValue(e, out view) && view != null;

    void BuildMap()
    {
        var root = new GameObject("Map").transform;
        var floorColor = new Color(0.16f, 0.16f, 0.2f, 1f);
        var wallColor = new Color(0.45f, 0.42f, 0.38f, 1f);

        for (int y = 0; y < _game.Height; y++)
        for (int x = 0; x < _game.Width; x++)
        {
            var color = _game.IsWall(x, y) ? wallColor : floorColor;
            var tile = EntityView.MakeQuad(root, $"Tile {x},{y}", new Vector3(x, y, TileZ),
                                           new Vector3(0.96f, 0.96f, 1f), color, EntityView.FlatShader);
            tile.gameObject.isStatic = true;
        }
    }

    void SpawnView(Entity e)
    {
        var a = _game.GetActor(e);
        if (!ColorUtility.TryParseHtmlString(a.Def.ColorHex, out var color)) color = Color.white;
        _views[e] = EntityView.Create(e, a.Name, color, a.X, a.Y, a.Hp, a.MaxHp, a.FacingX, a.FacingY,
                                      EntityView.FlatShader);
    }

    // =====================================================================================
    //  IEventAnimator
    // =====================================================================================

    public double DurationOf(SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped: return stepDuration;
            case SimEventType.Displaced: return knockbackDuration;
            case SimEventType.MoveCue: return _cues.DurationOf(e);
            case SimEventType.Damaged: return hurtDuration;
            case SimEventType.Healed: return healDuration;
            case SimEventType.Died: return dieDuration;
            default: return 0;   // Faced: turning is instant
        }
    }

    /// <summary>Only move beats have hit frames; everything else lets what it causes start at its end.</summary>
    public double ReleaseOf(SimEvent e) => e.Type == SimEventType.MoveCue ? _cues.ReleaseOf(e) : 1.0;

    public void StartAnim(SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped:
            case SimEventType.Displaced:   // knockback slides the box like a step, without turning it
                if (TryGetView(e.Actor, out var walker)) walker.BeginStep(e);
                break;
            case SimEventType.Faced:
                if (TryGetView(e.Actor, out var turner)) turner.SetFacing(e.DirX, e.DirY);
                break;
            case SimEventType.MoveCue:
                _cues.StartCue(e);
                break;
            case SimEventType.Damaged:
                if (TryGetView(e.Target, out var victim)) victim.BeginHurt(e.HpAfter);
                break;
            case SimEventType.Healed:
                if (TryGetView(e.Target, out var healed)) healed.BeginHeal(e.HpAfter);
                break;
        }
    }

    public void UpdateAnim(SimEvent e, float t)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped:
            case SimEventType.Displaced:
                if (TryGetView(e.Actor, out var walker)) walker.TickStep(t);
                break;
            case SimEventType.MoveCue:
                _cues.TickCue(e, t);
                break;
            case SimEventType.Damaged:
                if (TryGetView(e.Target, out var victim)) victim.TickHurt(t);
                break;
            case SimEventType.Healed:
                if (TryGetView(e.Target, out var healed)) healed.TickHeal(t);
                break;
            case SimEventType.Died:
                if (TryGetView(e.Actor, out var dying)) dying.TickDie(t);
                break;
        }
    }

    public void FinishAnim(SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.Stepped:
            case SimEventType.Displaced:
                if (TryGetView(e.Actor, out var walker)) walker.EndStep();
                break;
            case SimEventType.MoveCue:
                _cues.FinishCue(e);
                break;
            case SimEventType.Damaged:
                if (TryGetView(e.Target, out var victim)) victim.EndHurt();
                break;
            case SimEventType.Healed:
                if (TryGetView(e.Target, out var healed)) healed.EndHeal();
                break;
            case SimEventType.Died:
                // The sim destroyed this actor long ago; only now does its box go away.
                if (TryGetView(e.Actor, out var dying))
                {
                    dying.EndDie();
                    _views.Remove(e.Actor);
                }
                break;
        }
    }
}
