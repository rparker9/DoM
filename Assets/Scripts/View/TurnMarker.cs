using UnityEngine;
using Dungeon.Api;

/// <summary>Shows a highlight under the actor you control, but only once you can act.</summary>
public sealed class TurnMarker : MonoBehaviour
{
    const float MarkerZ = 0.05f;   // between the floor (0.5) and the actors (0)

    DungeonGame _game;
    DungeonView _view;
    Transform _marker;

    public void Bind(DungeonGame game, DungeonView view)
    {
        _game = game;
        _view = view;
        _marker = EntityView.MakeQuad(null, "TurnMarker", new Vector3(0f, 0f, MarkerZ), Vector3.one,
                                      new Color(1f, 1f, 1f, 0.25f), EntityView.FlatShader).transform;
        _marker.gameObject.SetActive(false);
    }

    // LateUpdate runs after every Update, so the boxes have already moved this frame.
    void LateUpdate()
    {
        if (_game == null) return;

        bool show = _game.CanPlayerAct && _view.TryGetView(_game.WhoseTurn, out _);
        _marker.gameObject.SetActive(show);
        if (!show) return;

        _view.TryGetView(_game.WhoseTurn, out var box);
        var p = box.transform.position;
        _marker.position = new Vector3(p.x, p.y, MarkerZ);
    }
}
