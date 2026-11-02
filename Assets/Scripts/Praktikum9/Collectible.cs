using UnityEngine;

/// <summary>
/// Item yang bisa diambil Player. Diambil cukup dengan mendekat (jarak datar &lt;= pickupRadius),
/// tanpa trigger collider, supaya tidak bergantung pada pengaturan Rigidbody / CharacterController.
/// </summary>
public class Collectible : MonoBehaviour
{
    [SerializeField] private string typeName = "Coin";
    [SerializeField, Min(0)] private int score = 10;
    [Tooltip("HP Player yang dipulihkan saat diambil.")]
    [SerializeField, Min(0f)] private float heal;
    [SerializeField] private bool legendary;
    [SerializeField, Min(0.1f)] private float pickupRadius = 1.2f;

    [Header("Visual")]
    [SerializeField] private Transform model;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [Tooltip("Putaran model (derajat/detik). 0 = diam, sehingga random rotation tetap terlihat.")]
    [SerializeField] private float spinSpeed;
    [SerializeField] private Light glow;

    private P9GameGoal goal;
    private Transform player;
    private Vector3 modelRest;
    private float phase;
    private float glowBase;

    public string TypeName => typeName;
    public int Score => score;
    public float Heal => heal;
    public bool Legendary => legendary;

    private void Awake()
    {
        if (model != null)
            modelRest = model.localPosition;
        if (glow != null)
            glowBase = glow.intensity;
    }

    public void Setup(P9GameGoal owner, Transform playerTransform, float animationPhase)
    {
        goal = owner;
        player = playerTransform;
        phase = animationPhase;
    }

    private void Update()
    {
        float t = Time.time * bobSpeed + phase;
        if (model != null)
        {
            model.localPosition = modelRest + Vector3.up * (Mathf.Sin(t) * bobHeight);
            if (spinSpeed != 0f)
                model.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }
        if (glow != null)
            glow.intensity = glowBase * (0.75f + 0.25f * Mathf.Sin(t * 2f));

        if (player == null || goal == null || !goal.IsPlaying)
            return;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.magnitude <= pickupRadius)
        {
            goal.Collect(this);
            Destroy(gameObject);
            enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = legendary ? new Color(1f, 0.8f, 0.1f) : new Color(0.3f, 0.75f, 1f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
