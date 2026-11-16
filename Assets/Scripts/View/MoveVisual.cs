using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>How the user's gray box moves during a move's wind-up.</summary>
public enum ActorMotion { None, Lunge, Hop, Spin, Shake, Pulse }

/// <summary>
/// What one move looks like. Right-click in the Project window: Create > Dungeon > Move Visual,
/// then drag it into a MoveAsset's Visual slot.
///
/// Every VFX graph gets the same exposed properties, if it declares them:
/// Origin (Vector3), Target (Vector3), Direction (Vector3), Radius (float), Duration (float).
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Move Visual", fileName = "New Move Visual")]
public sealed class MoveVisual : ScriptableObject
{
    /// <summary>One beat of the move: an optional VFX graph plus its timing.</summary>
    [Serializable]
    public sealed class Phase
    {
        [Tooltip("Optional. Empty = a gray-box placeholder.")]
        public VisualEffectAsset vfx;

        [Tooltip("Seconds at normal speed. The game waits this long, whatever the particles do.")]
        [Min(0)] public float duration = 0.2f;

        [Tooltip("The hit frame, as a fraction of the duration. What this beat causes can start here.")]
        [Range(0, 1)] public float release = 1f;
    }

    [Header("Wind-up (the user)")]
    public ActorMotion motion = ActorMotion.Lunge;
    public Phase windUp = new Phase { duration = 0.22f, release = 0.5f };

    [Header("Travel (projectiles and beams only)")]
    [Tooltip("Projectiles: tiles per second; the duration comes from the distance. 0 = use Travel's duration.")]
    [Min(0)] public float projectileSpeed = 15f;
    public Phase travel = new Phase { duration = 0.15f, release = 1f };

    [Header("Impact")]
    public Phase impact = new Phase { duration = 0.15f, release = 0.3f };

    // ---- lookup by name: the sim only knows a move's VisualId string ----

    static readonly Dictionary<string, MoveVisual> _byId = new Dictionary<string, MoveVisual>();

    /// <summary>Makes this visual findable by its asset name. MoveAsset.ToDef() calls it.</summary>
    public void Register() => _byId[name] = this;

    /// <param name="id">A MoveDef's VisualId.</param>
    /// <param name="visual">The registered visual, if any.</param>
    /// <returns>False if no visual was registered under that id.</returns>
    public static bool TryGet(string id, out MoveVisual visual)
    {
        visual = null;
        return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out visual) && visual != null;
    }
}
