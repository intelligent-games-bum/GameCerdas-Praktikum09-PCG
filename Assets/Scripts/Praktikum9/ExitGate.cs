using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Pintu keluar. Merah = terkunci, hijau = terbuka (semua item sudah diambil).</summary>
public class ExitGate : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float radius = 1.5f;
    [SerializeField] private Renderer[] indicators = new Renderer[0];
    [SerializeField] private Color lockedColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color openColor = new Color(0.2f, 1f, 0.45f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock block;

    public bool IsOpen { get; private set; }
    public float Radius => radius;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (block == null)
            block = new MaterialPropertyBlock();
        Color c = open ? openColor : lockedColor;
        foreach (Renderer r in indicators)
        {
            if (r == null)
                continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }
    }

    public bool Contains(Vector3 position)
    {
        Vector3 d = position - transform.position;
        d.y = 0f;
        return d.magnitude <= radius;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Handles.color = Application.isPlaying && IsOpen ? openColor : lockedColor;
        Handles.DrawWireDisc(transform.position + Vector3.up * 0.05f, Vector3.up, radius, 3f);
        Handles.Label(transform.position + Vector3.up * 3.2f, IsOpen ? "EXIT (terbuka)" : "EXIT (terkunci)");
#endif
    }
}
