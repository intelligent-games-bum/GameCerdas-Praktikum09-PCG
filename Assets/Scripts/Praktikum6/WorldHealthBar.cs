using UnityEngine;

/// <summary>
/// (Bonus Health Bar) Bar HP world-space di atas kepala karakter, selalu menghadap kamera.
/// - Warna isi: hijau (HP tinggi) → kuning → merah (HP rendah).
/// - Bar putih "jejak damage" turun perlahan di belakang isi, jadi besar damage terlihat.
/// - Garis penanda threshold Flee (jika <see cref="brain"/> diisi): saat isi melewati garis ini Enemy kabur.
/// Quad-nya dibuat saat Awake, jadi komponen ini cukup ditempel tanpa menyiapkan child manual.
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Opsional. Jika diisi, threshold Flee ditandai di bar.")]
    [SerializeField] private EnemyBrain brain;
    [Tooltip("Material unlit (warna diatur lewat MaterialPropertyBlock).")]
    [SerializeField] private Material barMaterial;
    [SerializeField] private float height = 2.6f;
    [SerializeField] private float width = 1.2f;
    [SerializeField] private float thickness = 0.14f;
    [Tooltip("Kecepatan jejak damage mengejar isi bar (fraksi HP per detik).")]
    [SerializeField] private float trailSpeed = 0.6f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly Color BackColor = new Color(0.08f, 0.08f, 0.08f);
    private static readonly Color TrailColor = new Color(1f, 1f, 1f);
    private static readonly Color MarkerColor = new Color(0.3f, 0.6f, 1f);

    private Transform root;
    private Transform fillPivot;
    private Transform trailPivot;
    private Transform marker;
    private Renderer fillRenderer;
    private MaterialPropertyBlock block;
    private float trail = 1f;
    private Camera cam;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
        if (barMaterial == null)
            barMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        block = new MaterialPropertyBlock();

        root = new GameObject("HealthBar").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.up * height;

        CreateQuad(root, "Back", new Vector3(0f, 0f, 0.01f), new Vector3(width + 0.06f, thickness + 0.06f, 1f), BackColor);
        trailPivot = CreatePivot("TrailPivot", -0.005f);
        CreateQuad(trailPivot, "Trail", new Vector3(0.5f, 0f, 0f), new Vector3(1f, thickness, 1f), TrailColor);
        fillPivot = CreatePivot("FillPivot", -0.01f);
        fillRenderer = CreateQuad(fillPivot, "Fill", new Vector3(0.5f, 0f, 0f), new Vector3(1f, thickness, 1f), Color.green);
        if (brain != null)
            marker = CreateQuad(root, "FleeMarker", new Vector3(0f, 0f, -0.02f), new Vector3(0.03f, thickness + 0.1f, 1f), MarkerColor).transform;

        trail = health.Normalized;
    }

    private Transform CreatePivot(string name, float z)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(-width * 0.5f, 0f, z);
        pivot.localScale = new Vector3(width, 1f, 1f);
        return pivot;
    }

    private Renderer CreateQuad(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPos;
        quad.transform.localScale = localScale;

        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = barMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SetColor(r, color);
        return r;
    }

    private void SetColor(Renderer r, Color c)
    {
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        r.SetPropertyBlock(block);
    }

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;
        if (cam != null)
            root.rotation = cam.transform.rotation;

        float hp = health.Normalized;
        trail = Mathf.Max(hp, Mathf.MoveTowards(trail, hp, trailSpeed * Time.deltaTime));

        fillPivot.localScale = new Vector3(width * hp, 1f, 1f);
        trailPivot.localScale = new Vector3(width * trail, 1f, 1f);
        SetColor(fillRenderer, hp > 0.5f
            ? Color.Lerp(Color.yellow, Color.green, (hp - 0.5f) * 2f)
            : Color.Lerp(Color.red, Color.yellow, hp * 2f));

        if (marker != null)
            marker.localPosition = new Vector3(-width * 0.5f + width * brain.FleeThreshold, 0f, -0.02f);
    }
}
