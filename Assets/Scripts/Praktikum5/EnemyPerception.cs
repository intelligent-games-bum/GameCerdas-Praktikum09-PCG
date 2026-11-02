using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// "Mata" Enemy. Player dianggap terlihat hanya jika lolos tiga pengecekan berurutan:
/// 1. Vision Range  : jarak ke Player &lt;= viewRange.
/// 2. Field of View : sudut antara arah hadap Enemy dan arah ke Player &lt;= viewAngle / 2.
/// 3. Line of Sight : Raycast dari mata ke badan Player tidak menabrak apa pun di obstacleMask.
///
/// Pengecekan murah (jarak, sudut) dilakukan dulu, Raycast paling akhir.
/// Script ini hanya MELAPORKAN apa yang dilihat; keputusan berpindah state ada di <see cref="EnemyFSM"/>.
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float viewRange = 12f;
    [SerializeField, Range(1f, 360f)] private float viewAngle = 110f;
    [Tooltip("Tinggi mata Enemy dari pivot (kaki).")]
    [SerializeField] private float eyeHeight = 1.6f;
    [Tooltip("Titik yang dibidik pada Player, diukur dari pivot (kaki) Player.")]
    [SerializeField] private float targetHeight = 1.0f;
    [Tooltip("Layer yang menghalangi pandangan (dinding, batu, rumah).")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("Debug (read only)")]
    [SerializeField] private bool inRange;
    [SerializeField] private bool inFieldOfView;
    [SerializeField] private bool hasLineOfSight;
    [SerializeField] private bool canSeeTarget;

    private Health targetHealth;

    public Transform Target => target;
    public bool CanSeeTarget => canSeeTarget;
    public bool InRange => inRange;
    public bool InFieldOfView => inFieldOfView;
    public bool HasLineOfSight => hasLineOfSight;
    public Vector3 LastKnownPosition { get; private set; }
    public float TimeSinceLastSeen { get; private set; } = float.PositiveInfinity;
    public float ViewRange => viewRange;
    public float ViewAngle => viewAngle;

    private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
    private Vector3 TargetPoint => target.position + Vector3.up * targetHeight;

    private void Awake()
    {
        if (target != null)
            targetHealth = target.GetComponent<Health>();
    }

    /// <summary>Dipakai spawner (Praktikum 9): prefab tidak bisa menyimpan referensi ke Player di scene.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetHealth = target != null ? target.GetComponent<Health>() : null;
    }

    private void Update()
    {
        Sense();

        if (canSeeTarget)
        {
            LastKnownPosition = target.position;
            TimeSinceLastSeen = 0f;
        }
        else
        {
            TimeSinceLastSeen += Time.deltaTime;
        }
    }

    private void Sense()
    {
        inRange = inFieldOfView = hasLineOfSight = canSeeTarget = false;
        if (target == null || (targetHealth != null && targetHealth.IsDead))
            return;

        Vector3 toTarget = TargetPoint - EyePosition;
        Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);

        inRange = flat.magnitude <= viewRange;
        if (!inRange)
            return;

        inFieldOfView = Vector3.Angle(transform.forward, flat) <= viewAngle * 0.5f;
        if (!inFieldOfView)
            return;

        // Raycast hanya ke layer obstacle: kalau kena sesuatu sebelum sampai Player, pandangan terhalang.
        hasLineOfSight = !Physics.Raycast(EyePosition, toTarget.normalized, toTarget.magnitude,
            obstacleMask, QueryTriggerInteraction.Ignore);
        canSeeTarget = hasLineOfSight;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        Color fovColor = canSeeTarget ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.9f, 0.2f);

        // Vision Range
        Handles.color = new Color(fovColor.r, fovColor.g, fovColor.b, 0.9f);
        Handles.DrawWireDisc(origin, Vector3.up, viewRange);

        // Vision Angle (kerucut FOV)
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Handles.color = new Color(fovColor.r, fovColor.g, fovColor.b, 0.12f);
        Handles.DrawSolidArc(origin, Vector3.up, left, viewAngle, viewRange);
        Handles.color = fovColor;
        Handles.DrawLine(origin, origin + left * viewRange, 2f);
        Handles.DrawLine(origin, origin + right * viewRange, 2f);
#endif
        // Garis Line of Sight: hijau = terlihat, merah = terhalang obstacle.
        if (target != null && inRange && inFieldOfView)
        {
            Gizmos.color = hasLineOfSight ? Color.green : Color.red;
            Gizmos.DrawLine(EyePosition, TargetPoint);
        }
    }
}
