using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawner Praktikum 9. Meminta rencana ke <see cref="SpawnGenerator"/> lalu meng-Instantiate prefab-nya.
///
/// - R : regenerate dengan seed yang sama (layout identik, uji reproducibility).
/// - N : regenerate dengan seed acak baru.
///
/// Enemy (Behavior Tree Praktikum 6) di-Instantiate di bawah parent non-aktif dulu, supaya target Player,
/// waypoint, dan SafePoint scene bisa diisi sebelum Awake berjalan. Setelah itu baru dipindah ke parent aktif.
/// </summary>
public class SmartSpawner : MonoBehaviour
{
    [SerializeField] private SpawnConfig config = new SpawnConfig();

    [Header("Scene")]
    [SerializeField] private Transform player;
    [Tooltip("Layer obstacle yang tidak boleh ditimpa objek spawn.")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("Titik yang harus tetap kosong, misalnya Exit.")]
    [SerializeField] private Transform[] keepOutPoints = new Transform[0];
    [SerializeField, Min(0f)] private float keepOutRadius = 3f;

    [Header("Enemy Behavior Tree (Praktikum 6)")]
    [SerializeField] private Transform[] waypoints = new Transform[0];
    [SerializeField] private SafePoint[] safePoints = new SafePoint[0];

    [Header("Gameplay")]
    [SerializeField] private P9GameGoal goal;
    [Tooltip("Player dikembalikan ke posisi awal sebelum generation, supaya kondisinya sama setiap kali.")]
    [SerializeField] private bool resetPlayerOnRegenerate = true;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool drawRejectedSamples = true;

    // Seed dibawa melewati reload scene (dipakai saat Player sudah kalah).
    private static int? pendingSeed;

    private Transform spawnRoot;
    private Transform stagingRoot;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;
    private bool hasPlayerStart;
    private readonly List<Vector3> rejectedSamples = new List<Vector3>();
    private readonly List<int> seedHistory = new List<int>();

    public SpawnConfig Config
    {
        get => config;
        set => config = value;
    }

    public SpawnReport LastReport { get; private set; }
    public int GenerationCount { get; private set; }
    public IReadOnlyList<int> SeedHistory => seedHistory;
    public Transform Player => player;

    /// <summary>Dipanggil setiap selesai generation.</summary>
    public event Action<SpawnReport> Generated;

    private Vector3 PlayerReferencePosition =>
        hasPlayerStart ? playerStartPosition : (player != null ? player.position : transform.position);

    private void Awake()
    {
        if (pendingSeed.HasValue)
        {
            config.seed = pendingSeed.Value;
            pendingSeed = null;
        }

        if (player != null)
        {
            playerStartPosition = player.position;
            playerStartRotation = player.rotation;
            hasPlayerStart = true;
        }

        spawnRoot = new GameObject("Spawned (PCG)").transform;
        stagingRoot = new GameObject("Spawn Staging (inactive)").transform;
        stagingRoot.gameObject.SetActive(false);
    }

    private void Start()
    {
        Regenerate();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        if (kb.rKey.wasPressedThisFrame)
            Regenerate();
        else if (kb.nKey.wasPressedThisFrame)
            RegenerateWithRandomSeed();
    }

    // ------------------------------------------------------------------ Public API

    /// <summary>Generation ulang dengan seed yang sedang dipakai.</summary>
    public void Regenerate()
    {
        RegenerateWithSeed(config.seed);
    }

    public void RegenerateWithRandomSeed()
    {
        RegenerateWithSeed(UnityEngine.Random.Range(1, 100000));
    }

    public void RegenerateWithSeed(int seed)
    {
        config.seed = seed;

        // Health yang sudah 0 tidak bisa dipulihkan (Dead adalah state akhir), jadi scene dimuat ulang.
        if (goal != null && goal.PlayerDead)
        {
            pendingSeed = seed;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (resetPlayerOnRegenerate)
            ResetPlayer();

        Clear();
        LastReport = Plan(config, rejectedSamples);
        SpawnObjects(LastReport);
        GenerationCount++;
        RememberSeed(seed);

        if (goal != null)
            goal.BeginRound(LastReport.ItemCount);

        if (logToConsole)
            Debug.Log(Describe(LastReport), this);
        Generated?.Invoke(LastReport);
    }

    /// <summary>
    /// Hitung rencana spawn tanpa membuat objek. Dipakai runner eksperimen (juga di Edit Mode).
    /// Posisi Player yang dipakai adalah posisi awalnya, jadi hasilnya tidak bergantung ke mana Player berjalan.
    /// </summary>
    public SpawnReport Plan(SpawnConfig cfg, List<Vector3> rejected = null)
    {
        Physics.SyncTransforms();
        var points = new List<Vector3>();
        foreach (Transform t in keepOutPoints)
            if (t != null)
                points.Add(t.position);

        return SpawnGenerator.Generate(cfg, new SpawnGenerator.Context
        {
            center = transform.position,
            playerPosition = PlayerReferencePosition,
            obstacleMask = obstacleMask,
            keepOutPoints = points,
            keepOutRadius = keepOutRadius,
            rejectedSamples = rejected,
            maxRejectedSamples = 400,
        });
    }

    public void Clear()
    {
        if (spawnRoot == null)
            return;
        for (int i = spawnRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = spawnRoot.GetChild(i).gameObject;
            // Dilepas dulu dari parent: Destroy baru jalan di akhir frame, sedangkan spawn baru terjadi sekarang.
            child.SetActive(false);
            child.transform.SetParent(null);
            Destroy(child);
        }
    }

    public static string Describe(SpawnReport r)
    {
        return $"[Praktikum 9] Seed {r.seed} (layout {r.LayoutHash}) | Enemy {r.EnemyCount}/{r.requestedEnemies}, " +
               $"Item {r.ItemCount}/{r.requestedItems}, gagal spawn {r.FailedObjects} | posisi ditolak {r.RejectedPositions} " +
               $"(player {r.rejectedNearPlayer}, exit {r.rejectedKeepOut}, spacing {r.rejectedSpacing}, obstacle {r.rejectedObstacle})\n" +
               $"Enemy: {r.TypeSummary(SpawnCategory.Enemy)}  |  Item: {r.TypeSummary(SpawnCategory.Item)}";
    }

    // ------------------------------------------------------------------ Instantiate

    private void SpawnObjects(SpawnReport report)
    {
        int index = 0;
        foreach (SpawnPlan plan in report.spawned)
        {
            GameObject prefab = PrefabFor(plan);
            if (prefab == null)
                continue;

            Quaternion rotation = Quaternion.Euler(0f, plan.rotationY, 0f);
            index++;

            if (plan.category == SpawnCategory.Enemy)
            {
                GameObject enemy = Instantiate(prefab, plan.position, rotation, stagingRoot);
                enemy.name = $"{plan.typeName}_{index:00}";
                EnemyPerception perception = enemy.GetComponent<EnemyPerception>();
                if (perception != null)
                    perception.SetTarget(player);
                EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                if (brain != null)
                    brain.SetRoute(waypoints, safePoints);
                enemy.transform.SetParent(spawnRoot, true);
            }
            else
            {
                GameObject item = Instantiate(prefab, plan.position, rotation, spawnRoot);
                item.name = $"{plan.typeName}_{index:00}";
                Collectible collectible = item.GetComponent<Collectible>();
                if (collectible != null)
                    collectible.Setup(goal, player, index * 0.37f);
            }
        }
    }

    private GameObject PrefabFor(SpawnPlan plan)
    {
        if (plan.legendary)
            return config.legendaryPrefab;
        SpawnEntry[] entries = plan.category == SpawnCategory.Enemy ? config.enemyTypes : config.itemTypes;
        return plan.entryIndex >= 0 && plan.entryIndex < entries.Length ? entries[plan.entryIndex]?.prefab : null;
    }

    private void ResetPlayer()
    {
        if (player == null || !hasPlayerStart)
            return;
        // CharacterController menimpa posisi yang diubah langsung, jadi dimatikan sebentar.
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;
        player.SetPositionAndRotation(playerStartPosition, playerStartRotation);
        if (cc != null)
            cc.enabled = true;

        var health = player.GetComponent<Health>();
        if (health != null)
            health.HealTo(1f);
    }

    private void RememberSeed(int seed)
    {
        seedHistory.Remove(seed);
        seedHistory.Insert(0, seed);
        if (seedHistory.Count > 6)
            seedHistory.RemoveAt(seedHistory.Count - 1);
    }

    // ------------------------------------------------------------------ Gizmos

    private void OnDrawGizmos()
    {
        Vector3 center = transform.position;

        // Area spawn.
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(center + Vector3.up * 0.05f, new Vector3(config.areaSize.x, 0.1f, config.areaSize.y));
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.06f);
        Gizmos.DrawCube(center + Vector3.up * 0.02f, new Vector3(config.areaSize.x, 0.02f, config.areaSize.y));

        Vector3 playerPos = Application.isPlaying ? PlayerReferencePosition : (player != null ? player.position : center);

#if UNITY_EDITOR
        // Zona aman Player dan zona legendary.
        Handles.color = new Color(0.2f, 1f, 0.4f, 0.12f);
        Handles.DrawSolidDisc(playerPos + Vector3.up * 0.05f, Vector3.up, config.playerSafeRadius);
        Handles.color = new Color(0.2f, 1f, 0.4f, 1f);
        Handles.DrawWireDisc(playerPos + Vector3.up * 0.05f, Vector3.up, config.playerSafeRadius, 3f);
        Handles.color = new Color(1f, 0.75f, 0.1f, 0.8f);
        Handles.DrawWireDisc(playerPos + Vector3.up * 0.05f, Vector3.up, config.legendaryMinPlayerDistance, 1f);
        Handles.Label(playerPos + Vector3.up * 0.2f + Vector3.forward * config.playerSafeRadius,
            $"Safe radius {config.playerSafeRadius:0.#} m");

        Handles.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        foreach (Transform t in keepOutPoints)
            if (t != null)
                Handles.DrawWireDisc(t.position + Vector3.up * 0.05f, Vector3.up, keepOutRadius, 2f);
#endif

        if (!Application.isPlaying || LastReport == null)
            return;

        // Objek hasil generation: lingkaran = minSpacing.
        foreach (SpawnPlan p in LastReport.spawned)
        {
            Gizmos.color = p.legendary ? new Color(1f, 0.8f, 0.1f)
                : p.category == SpawnCategory.Enemy ? new Color(1f, 0.3f, 0.25f) : new Color(0.3f, 0.75f, 1f);
#if UNITY_EDITOR
            Handles.color = Gizmos.color;
            Handles.DrawWireDisc(p.position + Vector3.up * 0.05f, Vector3.up, config.minSpacing * 0.5f);
#endif
            Gizmos.DrawLine(p.position, p.position + Vector3.up * 2f);
        }

        if (drawRejectedSamples)
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.7f);
            foreach (Vector3 s in rejectedSamples)
                Gizmos.DrawCube(s + Vector3.up * 0.1f, Vector3.one * 0.15f);
        }
    }
}
