using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Dungeon.Sim;
using Dungeon.Api;

/// <summary>
/// Plays the beats of every move (MoveCue events): the user's wind-up motion, then any VFX.
/// Timing comes from the move's MoveVisual, never from the particles, so the game stays in step.
/// Moves without a MoveVisual (or a phase without a VFX graph) get gray-box placeholders.
/// </summary>
public sealed class CuePlayer : MonoBehaviour
{
    [Header("Placeholder timings (moves without a MoveVisual)")]
    [SerializeField] float windUpDuration = 0.22f;
    [SerializeField] float projectileSpeed = 15f;   // tiles per second
    [SerializeField] float beamDuration = 0.15f;
    [SerializeField] float impactDuration = 0.15f;

    [Tooltip("A VFX that's still emitting this long after its beat ended is stopped and reused anyway.")]
    [SerializeField] float maxLingerSeconds = 3f;

    const float CueZ = -0.1f;   // in front of the actors (actors are at z = 0)

    /// <summary>What's on screen for one beat that's playing.</summary>
    sealed class ActiveCue
    {
        public Vector3 Origin, Target;
        public VisualEffect Vfx;
        public VisualEffectAsset Asset;
        public Renderer Placeholder;
    }

    /// <summary>A VFX whose beat is over, fading out before it goes back to the pool.</summary>
    struct Lingering
    {
        public VisualEffect Vfx;
        public VisualEffectAsset Asset;
        public float Since;
    }

    DungeonGame _game;
    DungeonView _view;
    readonly Dictionary<int, ActiveCue> _active = new Dictionary<int, ActiveCue>();   // by event Id
    readonly List<Lingering> _lingering = new List<Lingering>();
    readonly Dictionary<VisualEffectAsset, Stack<VisualEffect>> _pool = new Dictionary<VisualEffectAsset, Stack<VisualEffect>>();

    /// <summary>Connects the player to the game (for playback speed) and the view (for actor boxes).</summary>
    public void Bind(DungeonGame game, DungeonView view)
    {
        _game = game;
        _view = view;
    }

    // =====================================================================================
    //  Timing (called by DungeonView, which the EventPlayer asks)
    // =====================================================================================

    /// <summary>How long a beat plays, in seconds at normal speed.</summary>
    public double DurationOf(SimEvent e)
    {
        MoveVisual.TryGet(e.Move?.VisualId, out var visual);
        switch (e.Phase)
        {
            case CuePhase.WindUp:
                return visual != null ? visual.windUp.duration : windUpDuration;
            case CuePhase.Travel:
                float speed = visual != null ? visual.projectileSpeed : projectileSpeed;
                if (e.Delivery == Delivery.Projectile && speed > 0) return Distance(e) / speed;
                return visual != null ? visual.travel.duration : beamDuration;
            default:
                return visual != null ? visual.impact.duration : impactDuration;
        }
    }

    /// <summary>The beat's hit frame (0 to 1): when what it causes may start.</summary>
    public double ReleaseOf(SimEvent e)
    {
        if (MoveVisual.TryGet(e.Move?.VisualId, out var visual)) return PhaseOf(visual, e.Phase).release;
        switch (e.Phase)
        {
            case CuePhase.WindUp: return 0.5;   // halfway through the lunge
            case CuePhase.Impact: return 0.3;
            default: return 1.0;
        }
    }

    // =====================================================================================
    //  Playing (called by DungeonView's animation callbacks)
    // =====================================================================================

    /// <summary>Starts a beat: the user's motion for a wind-up, then a VFX or a placeholder.</summary>
    public void StartCue(SimEvent e)
    {
        MoveVisual.TryGet(e.Move?.VisualId, out var visual);
        var cue = new ActiveCue { Origin = TileToWorld(e.FromX, e.FromY), Target = TileToWorld(e.ToX, e.ToY) };
        _active[e.Id] = cue;

        if (e.Phase == CuePhase.WindUp && _view.TryGetView(e.Actor, out var user))
            user.BeginMotion(visual != null ? visual.motion : DefaultMotion(e.Move), e.DirX, e.DirY);

        var asset = visual != null ? PhaseOf(visual, e.Phase).vfx : null;
        if (asset != null)
        {
            cue.Asset = asset;
            cue.Vfx = Spawn(asset, cue, e);
        }
        else
        {
            cue.Placeholder = MakePlaceholder(e, cue);
        }
    }

    /// <param name="e">The beat.</param>
    /// <param name="t">Progress, 0 to 1.</param>
    public void TickCue(SimEvent e, float t)
    {
        if (e.Phase == CuePhase.WindUp && _view.TryGetView(e.Actor, out var user)) user.TickMotion(t);
        if (!_active.TryGetValue(e.Id, out var cue)) return;

        // A projectile's head follows our clock, so it lands exactly when the scheduler says.
        if (e.Phase == CuePhase.Travel && e.Delivery == Delivery.Projectile)
        {
            var pos = Vector3.Lerp(cue.Origin, cue.Target, t);
            if (cue.Vfx != null) cue.Vfx.transform.position = pos;
            if (cue.Placeholder != null) cue.Placeholder.transform.position = pos;
        }
        else if (cue.Placeholder != null)
        {
            var color = cue.Placeholder.material.color;
            color.a = 0.6f * (1f - t);   // beams and impacts fade out
            cue.Placeholder.material.color = color;
        }
    }

    /// <summary>Ends a beat. Its VFX stops emitting and fades out on its own, then returns to the pool.</summary>
    public void FinishCue(SimEvent e)
    {
        if (e.Phase == CuePhase.WindUp && _view.TryGetView(e.Actor, out var user)) user.EndMotion();
        if (!_active.TryGetValue(e.Id, out var cue)) return;
        _active.Remove(e.Id);

        if (cue.Placeholder != null)
        {
            Destroy(cue.Placeholder.material);
            Destroy(cue.Placeholder.gameObject);
        }
        if (cue.Vfx != null)
        {
            cue.Vfx.Stop();   // no new particles; the existing ones finish their lifetimes
            _lingering.Add(new Lingering { Vfx = cue.Vfx, Asset = cue.Asset, Since = Time.time });
        }
    }

    // =====================================================================================
    //  VFX pooling and speed
    // =====================================================================================

    void Update()
    {
        if (_game == null) return;

        // Particles run on their own clock, so match it to playback speed (fast-forward included).
        float rate = _game.PlaybackSpeed;
        foreach (var cue in _active.Values)
            if (cue.Vfx != null) cue.Vfx.playRate = rate;

        for (int i = _lingering.Count - 1; i >= 0; i--)
        {
            var l = _lingering[i];
            if (l.Vfx == null) { _lingering.RemoveAt(i); continue; }
            l.Vfx.playRate = rate;

            float age = Time.time - l.Since;
            bool faded = l.Vfx.aliveParticleCount == 0 && age > 0.1f;
            if (!faded && age < maxLingerSeconds) continue;

            l.Vfx.enabled = false;
            if (!_pool.TryGetValue(l.Asset, out var stack))
            {
                stack = new Stack<VisualEffect>();
                _pool[l.Asset] = stack;
            }
            stack.Push(l.Vfx);
            _lingering.RemoveAt(i);
        }
    }

    /// <summary>Takes a VFX from the pool (or makes one), fills in the standard properties, and starts it.</summary>
    VisualEffect Spawn(VisualEffectAsset asset, ActiveCue cue, SimEvent e)
    {
        VisualEffect vfx = null;
        if (_pool.TryGetValue(asset, out var stack))
            while (vfx == null && stack.Count > 0) vfx = stack.Pop();   // skips any destroyed meanwhile

        if (vfx == null)
        {
            var go = new GameObject("VFX " + asset.name);
            go.transform.SetParent(transform, false);
            vfx = go.AddComponent<VisualEffect>();
            vfx.enabled = false;
            vfx.visualEffectAsset = asset;
        }

        var dir = cue.Target - cue.Origin;
        dir = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.right;
        vfx.transform.position = e.Phase == CuePhase.Impact ? cue.Target : cue.Origin;

        // The property convention: a graph uses whichever of these it declares.
        if (vfx.HasVector3("Origin")) vfx.SetVector3("Origin", cue.Origin);
        if (vfx.HasVector3("Target")) vfx.SetVector3("Target", cue.Target);
        if (vfx.HasVector3("Direction")) vfx.SetVector3("Direction", dir);
        if (vfx.HasFloat("Radius")) vfx.SetFloat("Radius", e.Radius);
        if (vfx.HasFloat("Duration")) vfx.SetFloat("Duration", (float)DurationOf(e));

        vfx.playRate = _game.PlaybackSpeed;
        vfx.enabled = true;   // enabling restarts the effect and sends its initial event
        return vfx;
    }

    // =====================================================================================
    //  Placeholders and helpers
    // =====================================================================================

    /// <summary>Gray-box stand-ins: a dot for projectiles, a bar for beams, a square for impacts.</summary>
    Renderer MakePlaceholder(SimEvent e, ActiveCue cue)
    {
        var white = new Color(1f, 1f, 1f, 0.6f);
        switch (e.Phase)
        {
            case CuePhase.Travel when e.Delivery == Delivery.Projectile:
                return EntityView.MakeQuad(null, "Projectile", cue.Origin, new Vector3(0.25f, 0.25f, 1f),
                                           Color.white, EntityView.FlatShader);

            case CuePhase.Travel:   // beam
                var beam = EntityView.MakeQuad(null, "Beam", (cue.Origin + cue.Target) * 0.5f,
                                               new Vector3(Vector3.Distance(cue.Origin, cue.Target), 0.15f, 1f),
                                               white, EntityView.FlatShader);
                var d = cue.Target - cue.Origin;
                beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                return beam;

            case CuePhase.Impact:
                float size = e.Radius > 0 ? e.Radius * 2 + 1 : 0.9f;
                return EntityView.MakeQuad(null, "Impact", cue.Target, new Vector3(size, size, 1f),
                                           white, EntityView.FlatShader);

            default:
                return null;   // the wind-up is just the actor's motion
        }
    }

    static MoveVisual.Phase PhaseOf(MoveVisual visual, CuePhase phase) =>
        phase == CuePhase.WindUp ? visual.windUp : phase == CuePhase.Travel ? visual.travel : visual.impact;

    /// <summary>Wind-up motion for moves without a MoveVisual.</summary>
    static ActorMotion DefaultMotion(MoveDef move)
    {
        if (move == null) return ActorMotion.Lunge;
        switch (move.Area)
        {
            case MoveArea.Around: return ActorMotion.Pulse;
            case MoveArea.Self:
            case MoveArea.Team: return ActorMotion.Hop;
            default: return ActorMotion.Lunge;
        }
    }

    static Vector3 TileToWorld(int x, int y) => new Vector3(x, y, CueZ);

    static float Distance(SimEvent e) =>
        Mathf.Max(0.5f, Vector2.Distance(new Vector2(e.FromX, e.FromY), new Vector2(e.ToX, e.ToY)));
}
