using UnityEngine;
using Dungeon.Sim;

/// <summary>
/// Gray-box presentation of one entity. Holds only *presented* state (what has been played so far);
/// it never reads positions from the sim. All animation is a function of t so fast-forward is safe.
/// </summary>
public sealed class EntityView : MonoBehaviour
{
    public Entity Entity { get; private set; }

    Renderer _body;
    Transform _nose, _hpFill;
    Color _color;
    int _maxHp;
    Vector3 _rest, _from, _to, _lunge;

    public static EntityView Create(Entity e, string label, Color color, int x, int y,
                                    int hp, int maxHp, int faceX, int faceY, Shader shader)
    {
        var root = new GameObject(label);
        var view = root.AddComponent<EntityView>();
        view.Entity = e;
        view._color = color;
        view._maxHp = Mathf.Max(1, maxHp);

        view._body = MakeQuad(root.transform, "Body", Vector3.zero, new Vector3(0.8f, 0.8f, 1f), color, shader);
        var noseColor = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f);
        view._nose = MakeQuad(root.transform, "Nose", Vector3.zero, new Vector3(0.22f, 0.22f, 1f), noseColor, shader).transform;
        MakeQuad(root.transform, "HpBack", new Vector3(0f, -0.48f, -0.02f), new Vector3(0.8f, 0.08f, 1f), new Color(0.25f, 0.05f, 0.05f, 1f), shader);
        view._hpFill = MakeQuad(root.transform, "HpFill", new Vector3(0f, -0.48f, -0.03f), new Vector3(0.8f, 0.08f, 1f), new Color(0.3f, 0.9f, 0.3f, 1f), shader).transform;

        view._rest = new Vector3(x, y, 0f);
        root.transform.position = view._rest;
        view.SetFacing(faceX, faceY);
        view.SetHp(hp);
        return view;
    }

    static Shader _flatShader;

    /// <summary>Flat-color shader for the gray boxes. Sprites/Default renders in every render pipeline.</summary>
    public static Shader FlatShader
    {
        get
        {
            if (_flatShader == null) _flatShader = Shader.Find("Sprites/Default");
            if (_flatShader == null) _flatShader = Shader.Find("Unlit/Color");
            return _flatShader;
        }
    }

    public static Renderer MakeQuad(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, Shader shader)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.material = new Material(shader) { color = color };
        return r;
    }

    public void SetFacing(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        _nose.localPosition = new Vector3(dx * 0.3f, dy * 0.3f, -0.01f);
    }

    public void SetHp(int hp)
    {
        float frac = Mathf.Clamp01((float)hp / _maxHp);
        _hpFill.localScale = new Vector3(0.8f * frac, 0.08f, 1f);
        _hpFill.localPosition = new Vector3(-0.4f + 0.4f * frac, -0.48f, -0.03f);
    }

    // ---- Stepped ----
    public void BeginStep(SimEvent e)
    {
        _from = new Vector3(e.FromX, e.FromY, 0f);
        _to = new Vector3(e.ToX, e.ToY, 0f);
        SetFacing(e.DirX, e.DirY);
    }
    public void TickStep(float t) => transform.position = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, t));
    public void EndStep() { _rest = _to; transform.position = _rest; }

    // ---- UsedMove ----
    // Line moves lunge toward the facing direction. AllAround moves pulse bigger and back.
    bool _pulse;

    public void BeginUseMove(SimEvent e)
    {
        SetFacing(e.DirX, e.DirY);
        _pulse = e.Move != null && e.Move.Shape == MoveShape.AllAround;
        _lunge = new Vector3(e.DirX, e.DirY, 0f).normalized * 0.35f;
    }
    public void TickUseMove(float t)
    {
        float wave = Mathf.Sin(t * Mathf.PI);   // 0 -> 1 -> 0
        if (_pulse) transform.localScale = Vector3.one * (1f + 0.3f * wave);
        else transform.position = _rest + _lunge * wave;
    }
    public void EndUseMove()
    {
        transform.localScale = Vector3.one;
        transform.position = _rest;
    }

    // ---- Damaged ----
    public void BeginHurt(int remainingHp) => SetHp(remainingHp);
    public void TickHurt(float t)
    {
        _body.material.color = Color.Lerp(Color.white, _color, t);
        transform.position = _rest + new Vector3(Mathf.Sin(t * 40f) * 0.07f * (1f - t), 0f, 0f);
    }
    public void EndHurt() { _body.material.color = _color; transform.position = _rest; }

    // ---- Healed ----
    static readonly Color HealGlow = new Color(0.4f, 1f, 0.5f);

    public void BeginHeal(int hpAfter) => SetHp(hpAfter);
    public void TickHeal(float t) => _body.material.color = Color.Lerp(HealGlow, _color, t);
    public void EndHeal() => _body.material.color = _color;

    // ---- Died ----
    public void TickDie(float t) => transform.localScale = Vector3.one * (1f - t);
    public void EndDie() => Destroy(gameObject);
}
