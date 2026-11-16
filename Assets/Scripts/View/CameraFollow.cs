using UnityEngine;
using Dungeon.Api;

/// <summary>
/// Keeps the camera on the leader's gray box. That's where the leader appears on screen, which
/// can lag behind where the sim says it is.
/// </summary>
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] float size = 7f;          // orthographic half-height, in tiles
    [SerializeField] float sharpness = 8f;     // higher = snappier follow

    const float CameraZ = -10f;

    DungeonGame _game;
    DungeonView _view;
    Camera _cam;

    public void Bind(DungeonGame game, DungeonView view)
    {
        _game = game;
        _view = view;

        _cam = Camera.main;
        if (_cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            _cam = go.AddComponent<Camera>();
        }
        _cam.orthographic = true;
        _cam.orthographicSize = size;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        _cam.transform.rotation = Quaternion.identity;   // look straight down +Z at the XY plane
        _cam.transform.position = Target();
    }

    // LateUpdate runs after every Update, so the boxes have already moved this frame.
    void LateUpdate()
    {
        if (_game == null) return;
        // Frame-rate-independent smoothing: covers the same share of the gap per second at any fps.
        float blend = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, Target(), blend);
    }

    Vector3 Target()
    {
        Vector3 target = _view.TryGetView(_game.Leader, out var leaderView)
            ? leaderView.transform.position
            : _cam.transform.position;   // no leader box: stay put
        target.z = CameraZ;
        return target;
    }
}
