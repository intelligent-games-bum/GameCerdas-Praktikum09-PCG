using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Satu jenis objek yang bisa di-spawn beserta bobot peluangnya.</summary>
[Serializable]
public class SpawnEntry
{
    public string displayName = "Type";
    public GameObject prefab;
    [Tooltip("Bobot relatif. Peluang = bobot / total bobot (60:30:10 = 60%, 30%, 10%).")]
    [Min(0f)] public float weight = 1f;

    public SpawnEntry Clone()
    {
        return (SpawnEntry)MemberwiseClone();
    }
}

/// <summary>Semua parameter generation. Dipisah dari spawner supaya runner eksperimen bisa menyalin lalu mengubahnya.</summary>
[Serializable]
public class SpawnConfig
{
    [Header("Seed")]
    [Tooltip("Seed yang sama + kondisi scene yang sama = hasil generation yang sama persis.")]
    public int seed = 100;

    [Header("Jumlah & Area")]
    [Min(0)] public int enemyCount = 8;
    [Min(0)] public int itemCount = 15;
    [Tooltip("Ukuran area spawn (X, Z) dalam meter, berpusat di posisi spawner.")]
    public Vector2 areaSize = new Vector2(34f, 34f);

    [Header("Validasi Posisi")]
    [Tooltip("Objek tidak boleh muncul lebih dekat dari jarak ini ke Player (zona aman).")]
    [Min(0f)] public float playerSafeRadius = 5f;
    [Tooltip("Jarak minimum antarobjek yang di-spawn.")]
    [Min(0f)] public float minSpacing = 1.5f;
    [Tooltip("Jari-jari kapsul pengecekan obstacle di posisi kandidat.")]
    [Min(0.05f)] public float obstacleClearance = 0.7f;
    [Tooltip("Batas percobaan posisi per objek. Habis = objek gagal di-spawn.")]
    [Min(1)] public int maxAttempts = 30;

    [Header("Weighted Random")]
    public SpawnEntry[] enemyTypes = new SpawnEntry[0];
    public SpawnEntry[] itemTypes = new SpawnEntry[0];

    [Header("Fitur: Legendary Item")]
    public GameObject legendaryPrefab;
    [Tooltip("Peluang tiap item menjadi legendary.")]
    [Range(0f, 1f)] public float legendaryChance = 0.08f;
    [Min(0)] public int maxLegendary = 1;
    [Tooltip("Legendary hanya muncul jauh dari Player (hadiah yang berisiko).")]
    [Min(0f)] public float legendaryMinPlayerDistance = 12f;

    [Header("Fitur: Random Rotation")]
    public bool randomRotation = true;

    public SpawnConfig Clone()
    {
        var c = (SpawnConfig)MemberwiseClone();
        c.enemyTypes = CloneEntries(enemyTypes);
        c.itemTypes = CloneEntries(itemTypes);
        return c;
    }

    public void SetEnemyWeights(params float[] weights)
    {
        for (int i = 0; i < enemyTypes.Length && i < weights.Length; i++)
            enemyTypes[i].weight = weights[i];
    }

    private static SpawnEntry[] CloneEntries(SpawnEntry[] source)
    {
        if (source == null)
            return new SpawnEntry[0];
        var result = new SpawnEntry[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = source[i]?.Clone();
        return result;
    }
}

public enum SpawnCategory { Enemy, Item }

/// <summary>Hasil perencanaan satu objek: belum di-Instantiate, hanya data.</summary>
public struct SpawnPlan
{
    public SpawnCategory category;
    /// <summary>Indeks di enemyTypes / itemTypes, -1 untuk legendary.</summary>
    public int entryIndex;
    public string typeName;
    public Vector3 position;
    public float rotationY;
    public bool legendary;
}

public enum RejectReason { None, NearPlayer, KeepOut, Spacing, Obstacle }

/// <summary>Ringkasan satu kali generation: apa yang berhasil, apa yang ditolak, dan kenapa.</summary>
public class SpawnReport
{
    public int seed;
    public readonly List<SpawnPlan> spawned = new List<SpawnPlan>();
    public int requestedEnemies;
    public int requestedItems;

    /// <summary>Objek yang dilewati karena maxAttempts habis.</summary>
    public int failedEnemies;
    public int failedItems;

    /// <summary>Jumlah posisi kandidat yang ditolak, per alasan.</summary>
    public int rejectedNearPlayer;
    public int rejectedKeepOut;
    public int rejectedSpacing;
    public int rejectedObstacle;

    public int[] enemyTypeCounts = new int[0];
    public int[] itemTypeCounts = new int[0];
    public string[] enemyTypeNames = new string[0];
    public string[] itemTypeNames = new string[0];
    public int legendaryCount;

    public int RejectedPositions => rejectedNearPlayer + rejectedKeepOut + rejectedSpacing + rejectedObstacle;
    public int FailedObjects => failedEnemies + failedItems;
    public int EnemyCount => requestedEnemies - failedEnemies;
    public int ItemCount => requestedItems - failedItems;

    /// <summary>Sidik jari layout (posisi + jenis). Sama = layout identik, dipakai uji reproducibility.</summary>
    public string LayoutHash
    {
        get
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (SpawnPlan p in spawned)
                {
                    h = Mix(h, (int)p.category);
                    h = Mix(h, p.legendary ? -1 : p.entryIndex);
                    h = Mix(h, Mathf.RoundToInt(p.position.x * 100f));
                    h = Mix(h, Mathf.RoundToInt(p.position.z * 100f));
                    h = Mix(h, Mathf.RoundToInt(p.rotationY * 10f));
                }
                return h.ToString("X8");
            }
        }
    }

    private static uint Mix(uint h, int value)
    {
        unchecked
        {
            for (int i = 0; i < 4; i++)
            {
                h ^= (byte)(value >> (i * 8));
                h *= 16777619;
            }
            return h;
        }
    }

    public string TypeSummary(SpawnCategory category)
    {
        string[] names = category == SpawnCategory.Enemy ? enemyTypeNames : itemTypeNames;
        int[] counts = category == SpawnCategory.Enemy ? enemyTypeCounts : itemTypeCounts;
        var parts = new List<string>();
        for (int i = 0; i < names.Length; i++)
            parts.Add($"{names[i]} {counts[i]}");
        if (category == SpawnCategory.Item && legendaryCount > 0)
            parts.Add($"Legendary {legendaryCount}");
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Inti Procedural Content Generation. Murni menghitung rencana spawn (tanpa Instantiate),
/// jadi bisa dipakai saat Play maupun oleh runner eksperimen di Edit Mode.
///
/// Algoritma per objek:
/// 1. Pilih jenis dengan weighted random.
/// 2. Ambil posisi acak di dalam area, lalu validasi berurutan (cek murah dulu, Physics paling akhir):
///    jarak ke Player >= safe radius → di luar zona terlarang (Exit) → jarak ke objek lain >= minSpacing → tidak menabrak obstacle.
/// 3. Gagal? Coba posisi baru, maksimal maxAttempts kali. Habis = objek dilewati (gagal spawn).
///
/// Setiap aspek memakai System.Random sendiri yang diturunkan dari seed, sehingga mengubah satu fitur
/// (mis. mematikan random rotation) tidak menggeser urutan angka acak untuk posisi dan jenis.
/// </summary>
public static class SpawnGenerator
{
    public struct Context
    {
        public Vector3 center;
        public Vector3 playerPosition;
        public LayerMask obstacleMask;
        public IList<Vector3> keepOutPoints;
        public float keepOutRadius;
        /// <summary>Opsional: diisi posisi kandidat yang ditolak, untuk Gizmos.</summary>
        public List<Vector3> rejectedSamples;
        public int maxRejectedSamples;
    }

    public static SpawnReport Generate(SpawnConfig cfg, Context ctx)
    {
        var report = new SpawnReport
        {
            seed = cfg.seed,
            requestedEnemies = cfg.enemyCount,
            requestedItems = cfg.itemCount,
            enemyTypeCounts = new int[cfg.enemyTypes.Length],
            itemTypeCounts = new int[cfg.itemTypes.Length],
            enemyTypeNames = Names(cfg.enemyTypes),
            itemTypeNames = Names(cfg.itemTypes),
        };
        ctx.rejectedSamples?.Clear();

        var positionRng = new System.Random(cfg.seed);
        var typeRng = new System.Random(Derive(cfg.seed, 0x5bd1e995));
        var rotationRng = new System.Random(Derive(cfg.seed, 0x27d4eb2d));
        var legendaryRng = new System.Random(Derive(cfg.seed, 0x165667b1));

        for (int i = 0; i < cfg.enemyCount; i++)
        {
            int type = PickWeighted(typeRng, cfg.enemyTypes);
            float rotation = (float)(rotationRng.NextDouble() * 360.0);
            if (type < 0)
            {
                report.failedEnemies++;
                continue;
            }

            if (TryFindPosition(cfg, ctx, positionRng, report, cfg.playerSafeRadius, out Vector3 pos))
            {
                report.spawned.Add(new SpawnPlan
                {
                    category = SpawnCategory.Enemy,
                    entryIndex = type,
                    typeName = cfg.enemyTypes[type].displayName,
                    position = pos,
                    rotationY = cfg.randomRotation ? rotation : 0f,
                });
                report.enemyTypeCounts[type]++;
            }
            else
            {
                report.failedEnemies++;
            }
        }

        for (int i = 0; i < cfg.itemCount; i++)
        {
            int type = PickWeighted(typeRng, cfg.itemTypes);
            float rotation = (float)(rotationRng.NextDouble() * 360.0);
            // Angka legendary selalu diambil supaya urutan acaknya tidak bergantung pada hasil sebelumnya.
            bool legendaryRoll = legendaryRng.NextDouble() < cfg.legendaryChance;
            bool legendary = legendaryRoll && cfg.legendaryPrefab != null && report.legendaryCount < cfg.maxLegendary;
            if (type < 0 && !legendary)
            {
                report.failedItems++;
                continue;
            }

            float safe = legendary ? Mathf.Max(cfg.playerSafeRadius, cfg.legendaryMinPlayerDistance) : cfg.playerSafeRadius;
            if (TryFindPosition(cfg, ctx, positionRng, report, safe, out Vector3 pos))
            {
                report.spawned.Add(new SpawnPlan
                {
                    category = SpawnCategory.Item,
                    entryIndex = legendary ? -1 : type,
                    typeName = legendary ? "Legendary" : cfg.itemTypes[type].displayName,
                    position = pos,
                    rotationY = cfg.randomRotation ? rotation : 0f,
                    legendary = legendary,
                });
                if (legendary)
                    report.legendaryCount++;
                else
                    report.itemTypeCounts[type]++;
            }
            else
            {
                report.failedItems++;
            }
        }

        return report;
    }

    /// <summary>Weighted random: ambil r di [0, total), kurangi bobot satu per satu sampai r habis.</summary>
    public static int PickWeighted(System.Random rng, SpawnEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
            return -1;

        float total = 0f;
        int last = -1;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null || entries[i].weight <= 0f)
                continue;
            total += entries[i].weight;
            last = i;
        }
        if (total <= 0f)
            return -1;

        double r = rng.NextDouble() * total;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null || entries[i].weight <= 0f)
                continue;
            r -= entries[i].weight;
            if (r < 0.0)
                return i;
        }
        return last;
    }

    private static bool TryFindPosition(SpawnConfig cfg, Context ctx, System.Random rng, SpawnReport report,
        float safeRadius, out Vector3 result)
    {
        for (int attempt = 0; attempt < cfg.maxAttempts; attempt++)
        {
            Vector3 candidate = new Vector3(
                ctx.center.x + (float)(rng.NextDouble() - 0.5) * cfg.areaSize.x,
                ctx.center.y,
                ctx.center.z + (float)(rng.NextDouble() - 0.5) * cfg.areaSize.y);

            RejectReason reason = Validate(cfg, ctx, report, candidate, safeRadius);
            if (reason == RejectReason.None)
            {
                result = candidate;
                return true;
            }

            switch (reason)
            {
                case RejectReason.NearPlayer: report.rejectedNearPlayer++; break;
                case RejectReason.KeepOut: report.rejectedKeepOut++; break;
                case RejectReason.Spacing: report.rejectedSpacing++; break;
                case RejectReason.Obstacle: report.rejectedObstacle++; break;
            }
            if (ctx.rejectedSamples != null && ctx.rejectedSamples.Count < ctx.maxRejectedSamples)
                ctx.rejectedSamples.Add(candidate);
        }

        result = default;
        return false;
    }

    private static RejectReason Validate(SpawnConfig cfg, Context ctx, SpawnReport report, Vector3 p, float safeRadius)
    {
        if (FlatDistance(p, ctx.playerPosition) < safeRadius)
            return RejectReason.NearPlayer;

        if (ctx.keepOutPoints != null)
        {
            foreach (Vector3 k in ctx.keepOutPoints)
                if (FlatDistance(p, k) < ctx.keepOutRadius)
                    return RejectReason.KeepOut;
        }

        foreach (SpawnPlan other in report.spawned)
            if (FlatDistance(p, other.position) < cfg.minSpacing)
                return RejectReason.Spacing;

        float r = cfg.obstacleClearance;
        Vector3 bottom = p + Vector3.up * (r + 0.05f);
        Vector3 top = p + Vector3.up * Mathf.Max(r + 0.05f, 1.6f);
        if (Physics.CheckCapsule(bottom, top, r, ctx.obstacleMask, QueryTriggerInteraction.Ignore))
            return RejectReason.Obstacle;

        return RejectReason.None;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static int Derive(int seed, int salt)
    {
        unchecked
        {
            uint x = (uint)seed * 2654435761u ^ (uint)salt;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            return (int)(x & 0x7fffffff);
        }
    }

    private static string[] Names(SpawnEntry[] entries)
    {
        var names = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            names[i] = entries[i] != null ? entries[i].displayName : "-";
        return names;
    }
}
