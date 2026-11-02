using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum EnemyAction { None, Patrol, Chase, Attack, Search, Flee, Dead }

public enum DecisionMode { BehaviorTree, UtilityAI }

/// <summary>
/// Otak Enemy Praktikum 6. Satu set Action (Patrol, Chase, Attack, Search, Flee) bisa dipilih oleh dua cara:
///
/// 1. Behavior Tree (default) - prioritas tetap, anak kiri Selector menang:
///    Root Selector
///    ├── Flee Sequence   : Health Low? → Flee
///    ├── Attack Sequence : Can See Player? → In Attack Range? → (Cooldown → Attack | Wait Cooldown)
///    ├── Chase Sequence  : Can See Player? → Chase
///    ├── Search Sequence : Has Last Seen? → Search Last Seen   (bonus)
///    └── Patrol
///
/// 2. Utility AI (bonus Full Utility Controller) - hitung skor tiap action (<see cref="UtilityScoring"/>),
///    jalankan node action dengan skor tertinggi.
///
/// Skor Utility tetap dihitung saat mode Behavior Tree, supaya pilihan keduanya bisa dibandingkan di HUD.
///
/// Pembagian tugas: EnemyPerception = mata (range, FOV, Raycast), Health = HP,
/// NavMeshAgent = cara bergerak, script ini = memutuskan dan menjalankan action.
/// </summary>
// Jalan setelah EnemyPerception supaya keputusan memakai hasil penglihatan frame yang sama.
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(EnemyPerception))]
public class EnemyBrain : MonoBehaviour
{
    [Header("Decision Making")]
    [SerializeField] private DecisionMode mode = DecisionMode.BehaviorTree;
    [Tooltip("Jika diisi, nilai di personality menimpa nilai Chase/Attack/Search/Flee/Utility di bawah saat Play.")]
    [SerializeField] private EnemyPersonality personality;

    [Header("Current Action (read only)")]
    [SerializeField] private EnemyAction currentAction = EnemyAction.None;
    [SerializeField] private EnemyAction previousAction = EnemyAction.None;
    [Tooltip("Cabang BT yang menang pada frame ini, atau pilihan Utility.")]
    [SerializeField] private string activeBranch = "-";
    [SerializeField] private EnemyAction utilityChoice = EnemyAction.Patrol;
    [SerializeField] private UtilityScore[] utilityScores = new UtilityScore[0];

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField, Min(0f)] private float patrolSpeed = 2f;
    [SerializeField, Min(0f)] private float waypointTolerance = 0.6f;
    [Tooltip("Jeda singkat di tiap waypoint (detik).")]
    [SerializeField, Min(0f)] private float waypointWait = 1f;

    [Header("Chase")]
    [SerializeField, Min(0f)] private float chaseSpeed = 4.5f;

    [Header("Attack")]
    [Tooltip("Masuk Attack jika jarak <= nilai ini.")]
    [SerializeField, Min(0f)] private float attackRange = 2f;
    [Tooltip("Selama sedang Attack, Player dianggap masih dalam jangkauan sampai jarak > nilai ini (hysteresis).")]
    [SerializeField, Min(0f)] private float attackExitRange = 3f;
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0.05f)] private float attackCooldown = 1.5f;
    [Tooltip("Jeda sebelum pukulan pertama setelah mulai Attack.")]
    [SerializeField, Min(0f)] private float attackWindup = 0.4f;
    [SerializeField, Min(0f)] private float turnSpeed = 10f;

    [Header("Search Last Seen Position (bonus)")]
    [SerializeField, Min(0f)] private float searchSpeed = 3f;
    [Tooltip("Lama melihat sekeliling setelah sampai di posisi terakhir Player (detik).")]
    [SerializeField, Min(0f)] private float searchDuration = 4f;
    [Tooltip("Kecepatan memutar badan saat melihat sekeliling (derajat/detik).")]
    [SerializeField, Min(0f)] private float lookAroundSpeed = 100f;

    [Header("Flee")]
    [Tooltip("Flee jika HP <= fraksi ini dari Max HP.")]
    [SerializeField, Range(0f, 1f)] private float fleeThreshold = 0.3f;
    [SerializeField, Min(0f)] private float fleeSpeed = 6f;
    [SerializeField] private SafePoint[] safePoints;

    [Header("Utility AI")]
    [Tooltip("Bonus skor untuk action yang sedang jalan, mencegah gonta-ganti tiap frame.")]
    [SerializeField, Range(0f, 0.3f)] private float utilityMomentum = 0.05f;
    [SerializeField, Min(0f)] private float attackWeight = 1f;
    [SerializeField, Min(0f)] private float chaseWeight = 1f;
    [SerializeField, Min(0f)] private float fleeWeight = 1f;
    [SerializeField, Min(0f)] private float searchWeight = 1f;
    [SerializeField, Min(0f)] private float patrolWeight = 1f;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    private NavMeshAgent agent;
    private Health health;
    private EnemyPerception perception;
    private Health targetHealth;

    private BTNode root;
    private CooldownDecorator attackCooldownNode;
    private readonly Dictionary<EnemyAction, BTNode> utilityNodes = new Dictionary<EnemyAction, BTNode>();

    private int waypointIndex;
    private float waitTimer;
    private SafePoint fleeTarget;

    private bool hasLastSeen;
    private Vector3 lastSeenPosition;
    private bool lookingAround;
    private float lookTimer;

    /// <summary>(dari, ke) setiap kali action berganti.</summary>
    public event Action<EnemyAction, EnemyAction> ActionChanged;
    /// <summary>Dipanggil setiap kali pukulan dilepaskan.</summary>
    public event Action Attacked;

    public DecisionMode Mode
    {
        get => mode;
        set
        {
            if (mode == value)
                return;
            mode = value;
            Log($"Mode keputusan diganti ke <b>{mode}</b>");
        }
    }

    public EnemyPersonality Personality => personality;
    public EnemyAction CurrentAction => currentAction;
    public string ActiveBranch => activeBranch;
    public EnemyAction UtilityChoice => utilityChoice;
    public IReadOnlyList<UtilityScore> UtilityScores => utilityScores;
    public BTNode Root => root;
    public CooldownDecorator AttackCooldownNode => attackCooldownNode;
    public float FleeThreshold => fleeThreshold;
    public float AttackRange => attackRange;
    public SafePoint FleeTarget => fleeTarget;
    public bool HasLastSeen => hasLastSeen;
    public Vector3 LastSeenPosition => lastSeenPosition;
    public bool LookingAround => currentAction == EnemyAction.Search && lookingAround;
    public float SearchTimeLeft => Mathf.Max(0f, searchDuration - lookTimer);
    public EnemyPerception Perception => perception;

    private Transform Target => perception.Target;
    private bool TargetAlive => Target != null && (targetHealth == null || !targetHealth.IsDead);

    public float DistanceToTarget
    {
        get
        {
            if (Target == null)
                return float.PositiveInfinity;
            Vector3 d = Target.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        perception = GetComponent<EnemyPerception>();
        if (Target != null)
            targetHealth = Target.GetComponent<Health>();

        utilityScores = new UtilityScore[UtilityScoring.Actions.Length];
        BuildTree();
        if (personality != null)
            ApplyPersonality(personality);
    }

    /// <summary>
    /// Dipakai spawner (Praktikum 9) sebelum Enemy aktif: prefab tidak bisa menyimpan referensi
    /// ke waypoint dan SafePoint di scene. Panggil sebelum Awake (Instantiate di parent non-aktif).
    /// </summary>
    public void SetRoute(Transform[] newWaypoints, SafePoint[] newSafePoints)
    {
        waypoints = newWaypoints;
        safePoints = newSafePoints;
    }

    private void OnValidate()
    {
        attackExitRange = Mathf.Max(attackExitRange, attackRange + 0.1f);
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
    }

    // ------------------------------------------------------------------ Behavior Tree

    private void BuildTree()
    {
        var flee = new ActionNode("Flee", Flee);
        attackCooldownNode = new CooldownDecorator("Cooldown", attackCooldown, new ActionNode("Attack", Attack));
        // Saat cooldown, Enemy tetap di tempat menghadap Player (bukan jatuh ke Chase lalu menabrak Player).
        var attackOrWait = new SelectorNode("Attack Or Wait", attackCooldownNode, new ActionNode("Wait Cooldown", WaitCooldown));
        var chase = new ActionNode("Chase", Chase);
        var search = new ActionNode("Search Last Seen", Search);
        var patrol = new ActionNode("Patrol", Patrol);

        root = new SelectorNode("Root Selector",
            new SequenceNode("Flee Sequence",
                new ConditionNode("Health Low?", IsHealthLow),
                flee),
            new SequenceNode("Attack Sequence",
                new ConditionNode("Can See Player?", CanSeePlayer),
                new ConditionNode("In Attack Range?", InAttackRange),
                attackOrWait),
            new SequenceNode("Chase Sequence",
                new ConditionNode("Can See Player?", CanSeePlayer),
                chase),
            new SequenceNode("Search Sequence",
                new ConditionNode("Has Last Seen?", HasLastSeenPosition),
                search),
            patrol);

        // Utility AI memakai node action yang sama, hanya cara memilihnya yang berbeda.
        utilityNodes[EnemyAction.Flee] = flee;
        utilityNodes[EnemyAction.Attack] = attackOrWait;
        utilityNodes[EnemyAction.Chase] = chase;
        utilityNodes[EnemyAction.Search] = search;
        utilityNodes[EnemyAction.Patrol] = patrol;
    }

    private void Update()
    {
        if (currentAction == EnemyAction.Dead)
            return;
        if (health.IsDead)
        {
            Enter(EnemyAction.Dead);
            return;
        }

        RememberLastSeen();
        utilityChoice = UtilityScoring.Select(BuildUtilityContext(), utilityScores);

        if (mode == DecisionMode.BehaviorTree)
        {
            root.Evaluate();
            activeBranch = WinningBranch();
        }
        else
        {
            NodeState state = utilityNodes[utilityChoice].Evaluate();
            if (state == NodeState.Failure && utilityChoice != EnemyAction.Patrol)
                utilityNodes[EnemyAction.Patrol].Evaluate();
            activeBranch = $"Utility: {utilityChoice}";
        }
    }

    private string WinningBranch()
    {
        foreach (BTNode child in root.Children)
        {
            if (child.EvaluatedThisFrame && child.LastState != NodeState.Failure)
                return child.Name;
        }
        return "-";
    }

    // ------------------------------------------------------------------ Conditions

    private bool IsHealthLow()
    {
        return health.Normalized <= fleeThreshold;
    }

    private bool CanSeePlayer()
    {
        return perception.CanSeeTarget && TargetAlive;
    }

    private bool InAttackRange()
    {
        float range = currentAction == EnemyAction.Attack ? attackExitRange : attackRange;
        return DistanceToTarget <= range;
    }

    private bool HasLastSeenPosition()
    {
        return hasLastSeen;
    }

    // ------------------------------------------------------------------ Actions

    private NodeState Patrol()
    {
        Enter(EnemyAction.Patrol);
        if (waypoints == null || waypoints.Length == 0 || agent.pathPending)
            return NodeState.Running;
        if (agent.remainingDistance > waypointTolerance)
            return NodeState.Running;

        // Sampai di waypoint: tunggu sebentar, lalu lanjut ke berikutnya (loop).
        waitTimer += Time.deltaTime;
        if (waitTimer >= waypointWait)
        {
            waitTimer = 0f;
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            GoToWaypoint();
        }
        return NodeState.Running;
    }

    private NodeState Chase()
    {
        Enter(EnemyAction.Chase);
        if ((agent.destination - Target.position).sqrMagnitude > 0.04f)
            agent.SetDestination(Target.position);
        return NodeState.Running;
    }

    private NodeState Attack()
    {
        Enter(EnemyAction.Attack);
        FaceTarget();
        if (targetHealth != null)
            targetHealth.TakeDamage(attackDamage);
        Attacked?.Invoke();
        Log($"ATTACK! damage {attackDamage:0}" +
            (targetHealth != null ? $" (HP Player {targetHealth.Current:0}/{targetHealth.Max:0})" : "") +
            $", cooldown {attackCooldownNode.Cooldown:0.0}s");
        return NodeState.Success;
    }

    private NodeState WaitCooldown()
    {
        Enter(EnemyAction.Attack);
        FaceTarget();
        return NodeState.Running;
    }

    private NodeState Search()
    {
        Enter(EnemyAction.Search);

        if (!lookingAround)
        {
            if (agent.pathPending)
                return NodeState.Running;
            if (agent.hasPath && agent.remainingDistance > waypointTolerance)
                return NodeState.Running;

            lookingAround = true;
            lookTimer = 0f;
            agent.isStopped = true;
            agent.updateRotation = false;
            Log("Sampai di posisi terakhir Player, melihat sekeliling");
        }

        lookTimer += Time.deltaTime;
        transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f);
        if (lookTimer < searchDuration)
            return NodeState.Running;

        hasLastSeen = false;
        Log("Player tidak ditemukan, kembali patroli");
        return NodeState.Success;
    }

    private NodeState Flee()
    {
        Enter(EnemyAction.Flee);
        if (fleeTarget == null)
            return NodeState.Failure;
        if (!fleeTarget.Contains(transform.position))
            return NodeState.Running;

        fleeTarget.Rest(health);
        Log($"Sampai di {fleeTarget.name}, HP pulih ke {health.Current:0}/{health.Max:0}");
        fleeTarget = null;
        hasLastSeen = false;
        return NodeState.Success;
    }

    // ------------------------------------------------------------------ Enter / Exit action

    private void Enter(EnemyAction next)
    {
        if (next == currentAction)
            return;

        EnemyAction from = currentAction;
        ExitAction(from);
        previousAction = from;
        currentAction = next;
        EnterAction(next);

        Log($"{from} -> <b>{next.ToString().ToUpper()}</b>  [{ModeLabel}] {Reason(next)}");
        ActionChanged?.Invoke(from, next);
    }

    private void EnterAction(EnemyAction action)
    {
        switch (action)
        {
            case EnemyAction.Patrol:
                Move(patrolSpeed, 0f);
                waitTimer = 0f;
                waypointIndex = NearestWaypointIndex();
                GoToWaypoint();
                break;

            case EnemyAction.Chase:
                Move(chaseSpeed, attackRange * 0.8f);
                agent.SetDestination(Target.position);
                break;

            case EnemyAction.Attack:
                agent.isStopped = true;
                agent.ResetPath();
                agent.updateRotation = false;
                attackCooldownNode.DelayAtLeast(attackWindup);
                break;

            case EnemyAction.Search:
                Move(searchSpeed, 0f);
                lookingAround = false;
                lookTimer = 0f;
                agent.SetDestination(lastSeenPosition);
                break;

            case EnemyAction.Flee:
                Move(fleeSpeed, 0f);
                fleeTarget = PickSafePoint();
                if (fleeTarget != null)
                    agent.SetDestination(fleeTarget.transform.position);
                break;

            case EnemyAction.Dead:
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                agent.enabled = false;
                perception.enabled = false;
                activeBranch = "-";
                break;
        }
    }

    private void ExitAction(EnemyAction action)
    {
        switch (action)
        {
            case EnemyAction.Attack:
            case EnemyAction.Search:
                agent.updateRotation = true;
                break;
            case EnemyAction.Flee:
                fleeTarget = null;
                break;
        }
    }

    private void Move(float speed, float stoppingDistance)
    {
        agent.isStopped = false;
        agent.updateRotation = true;
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>Selama Player terlihat, simpan posisinya. Setelah hilang, posisi ini jadi tujuan Search.</summary>
    private void RememberLastSeen()
    {
        if (!CanSeePlayer())
            return;
        hasLastSeen = true;
        lastSeenPosition = perception.LastKnownPosition;
    }

    /// <summary>Dipukul dari belakang (Player tidak terlihat): anggap posisi Player sebagai petunjuk dan cari ke sana.</summary>
    private void OnDamaged(float amount)
    {
        if (health.IsDead || !TargetAlive || perception.CanSeeTarget)
            return;

        hasLastSeen = true;
        lastSeenPosition = Target.position;
        if (currentAction == EnemyAction.Search)
        {
            lookingAround = false;
            lookTimer = 0f;
            Move(searchSpeed, 0f);
            agent.SetDestination(lastSeenPosition);
        }
        Log("Diserang dari arah yang tidak terlihat, mencari penyerang");
    }

    private void GoToWaypoint()
    {
        if (waypoints != null && waypoints.Length > 0 && waypoints[waypointIndex] != null)
            agent.SetDestination(waypoints[waypointIndex].position);
    }

    private int NearestWaypointIndex()
    {
        int best = waypointIndex;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; waypoints != null && i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;
            float d = (waypoints[i].position - transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    private void FaceTarget()
    {
        Vector3 dir = Target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    /// <summary>Pilih SafePoint yang paling menjauhkan dari Player: skor = jarak ke Player - jarak ke Enemy.</summary>
    private SafePoint PickSafePoint()
    {
        SafePoint best = null;
        float bestScore = float.NegativeInfinity;
        foreach (SafePoint sp in safePoints)
        {
            if (sp == null)
                continue;
            Vector3 p = sp.transform.position;
            float fromPlayer = Target != null ? Vector3.Distance(p, Target.position) : 0f;
            float score = fromPlayer - Vector3.Distance(p, transform.position);
            if (score > bestScore)
            {
                bestScore = score;
                best = sp;
            }
        }
        return best;
    }

    private UtilityContext BuildUtilityContext()
    {
        return new UtilityContext
        {
            health = health.Normalized,
            fleeThreshold = fleeThreshold,
            canSeeTarget = CanSeePlayer(),
            distance = DistanceToTarget,
            inAttackRange = InAttackRange(),
            attackRange = attackRange,
            viewRange = perception.ViewRange,
            hasLastSeen = hasLastSeen,
            current = currentAction,
            momentum = utilityMomentum,
            attackWeight = attackWeight,
            chaseWeight = chaseWeight,
            fleeWeight = fleeWeight,
            searchWeight = searchWeight,
            patrolWeight = patrolWeight,
        };
    }

    // ------------------------------------------------------------------ Personality

    public void ApplyPersonality(EnemyPersonality p)
    {
        if (p == null)
            return;
        personality = p;
        fleeThreshold = p.fleeThreshold;
        chaseSpeed = p.chaseSpeed;
        attackCooldown = p.attackCooldown;
        attackDamage = p.attackDamage;
        searchDuration = p.searchDuration;
        attackWeight = p.attackWeight;
        chaseWeight = p.chaseWeight;
        fleeWeight = p.fleeWeight;
        searchWeight = p.searchWeight;
        patrolWeight = p.patrolWeight;

        if (attackCooldownNode != null)
            attackCooldownNode.Cooldown = attackCooldown;
        if (currentAction == EnemyAction.Chase)
            agent.speed = chaseSpeed;

        Log($"Personality: <b>{p.displayName}</b> (Flee <= {fleeThreshold:P0}, cooldown {attackCooldown:0.0}s, chase {chaseSpeed:0.0} m/s)");
    }

    // ------------------------------------------------------------------ Debug

    private string ModeLabel => mode == DecisionMode.BehaviorTree ? "BT" : "Utility";

    private string Reason(EnemyAction action)
    {
        string reason;
        switch (action)
        {
            case EnemyAction.Flee:
                reason = $"HP {health.Current:0}/{health.Max:0} <= {fleeThreshold:P0}";
                break;
            case EnemyAction.Attack:
                reason = $"Player terlihat, jarak {DistanceToTarget:0.0} m <= Attack Range {attackRange:0.#} m";
                break;
            case EnemyAction.Chase:
                reason = $"Player terlihat, jarak {DistanceToTarget:0.0} m";
                break;
            case EnemyAction.Search:
                reason = $"Player hilang, cek posisi terakhir ({lastSeenPosition.x:0.0}, {lastSeenPosition.z:0.0})";
                break;
            case EnemyAction.Patrol:
                reason = "Player tidak terlihat";
                break;
            case EnemyAction.Dead:
                return "HP habis";
            default:
                return string.Empty;
        }

        if (mode == DecisionMode.UtilityAI)
        {
            foreach (UtilityScore s in utilityScores)
                if (s.action == action)
                    reason += $" | skor {s.score:0.00}";
        }
        return reason;
    }

    private void Log(string message)
    {
        if (logActions)
            Debug.Log($"<color={ActionColorHex(currentAction)}>[EnemyBrain:{currentAction}]</color> {message}", this);
    }

    public static Color ActionColor(EnemyAction action)
    {
        switch (action)
        {
            case EnemyAction.Patrol: return new Color(0.3f, 0.9f, 0.35f);
            case EnemyAction.Chase: return new Color(1f, 0.8f, 0.15f);
            case EnemyAction.Attack: return new Color(1f, 0.2f, 0.2f);
            case EnemyAction.Search: return new Color(0.85f, 0.45f, 1f);
            case EnemyAction.Flee: return new Color(0.3f, 0.6f, 1f);
            default: return new Color(0.55f, 0.55f, 0.55f);
        }
    }

    public static string ActionColorHex(EnemyAction action)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(ActionColor(action));
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position + Vector3.up * 0.05f;

#if UNITY_EDITOR
        Handles.color = new Color(1f, 0.15f, 0.15f);
        Handles.DrawWireDisc(origin, Vector3.up, attackRange, 3f);
        Handles.color = new Color(1f, 0.55f, 0.1f);
        Handles.DrawWireDisc(origin, Vector3.up, attackExitRange, 1.5f);
#endif

        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform a = waypoints[i];
                Transform b = waypoints[(i + 1) % waypoints.Length];
                if (a == null || b == null)
                    continue;
                Gizmos.DrawLine(a.position + Vector3.up * 0.1f, b.position + Vector3.up * 0.1f);
                Gizmos.DrawSphere(a.position + Vector3.up * 0.1f, 0.25f);
            }
        }

        // Posisi terakhir Player terlihat (tujuan Search).
        if (Application.isPlaying && hasLastSeen)
        {
            Gizmos.color = ActionColor(EnemyAction.Search);
            Vector3 p = lastSeenPosition + Vector3.up * 0.1f;
            Gizmos.DrawLine(p + new Vector3(-0.5f, 0f, -0.5f), p + new Vector3(0.5f, 0f, 0.5f));
            Gizmos.DrawLine(p + new Vector3(-0.5f, 0f, 0.5f), p + new Vector3(0.5f, 0f, -0.5f));
            Gizmos.DrawWireSphere(p, 0.6f);
        }

        if (Application.isPlaying && agent != null && agent.enabled && agent.hasPath)
        {
            Gizmos.color = ActionColor(currentAction);
            Gizmos.DrawLine(origin, agent.destination + Vector3.up * 0.05f);
        }
    }
}
