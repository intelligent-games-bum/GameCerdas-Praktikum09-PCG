using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Menjalankan semua pengujian Praktikum 9 sekaligus dan menulis hasilnya ke Console, CSV, dan Markdown.
/// Hanya menghitung rencana spawn (<see cref="SmartSpawner.Plan"/>), tidak membuat objek,
/// jadi bisa dijalankan saat Play maupun di Edit Mode (menu "Praktikum 9/Run Experiments").
///
/// Semua eksperimen memakai konfigurasi spawner di scene sebagai dasar, lalu hanya satu parameter yang diubah.
/// </summary>
public class SpawnExperimentRunner : MonoBehaviour
{
    [SerializeField] private SmartSpawner spawner;

    [Header("1. Perbandingan seed")]
    [SerializeField] private int[] seeds = { 100, 200, 300 };

    [Header("2. Reproducibility")]
    [SerializeField] private int reproSeed = 500;
    [SerializeField, Min(2)] private int reproRuns = 3;

    [Header("3 & 4. Safe radius dan min spacing (seed tetap)")]
    [SerializeField] private int parameterSeed = 500;
    [SerializeField] private float[] safeRadii = { 3f, 5f, 8f };
    [SerializeField] private float[] spacings = { 0.5f, 1.5f, 3f };

    [Header("5. Bobot enemy (beberapa generation)")]
    [SerializeField] private float[] weightsA = { 60f, 30f, 10f };
    [SerializeField] private float[] weightsB = { 33f, 33f, 34f };
    [SerializeField, Min(1)] private int weightGenerations = 20;
    [SerializeField] private int weightSeedStart = 1;

    [Header("Output")]
    [Tooltip("Relatif terhadap folder Assets.")]
    [SerializeField] private string outputFolder = "Scenes/Praktikum9/Experiments";

    public string LastSummary { get; private set; } = "";

    private readonly List<string[]> csv = new List<string[]>();
    private readonly StringBuilder md = new StringBuilder();

    [ContextMenu("Run All Experiments")]
    public void RunAll()
    {
        if (spawner == null)
        {
            Debug.LogError("[Praktikum 9] SpawnExperimentRunner: spawner belum diisi.", this);
            return;
        }

        csv.Clear();
        md.Clear();
        csv.Add(new[] { "eksperimen", "variasi", "seed", "safe_radius", "min_spacing", "bobot_enemy",
            "enemy", "item", "legendary", "gagal_spawn", "posisi_ditolak", "tolak_player", "tolak_exit",
            "tolak_spacing", "tolak_obstacle", "jenis_enemy", "jenis_item", "layout_hash" });

        SpawnConfig baseCfg = spawner.Config.Clone();
        md.AppendLine("# Hasil Eksperimen Praktikum 9 - Smart Spawner");
        md.AppendLine();
        md.AppendLine($"Dibuat {DateTime.Now:yyyy-MM-dd HH:mm}. Konfigurasi dasar: enemy {baseCfg.enemyCount}, item {baseCfg.itemCount}, " +
                      $"area {baseCfg.areaSize.x:0.#} x {baseCfg.areaSize.y:0.#} m, safe radius {baseCfg.playerSafeRadius:0.#}, " +
                      $"min spacing {baseCfg.minSpacing:0.#}, max attempts {baseCfg.maxAttempts}, bobot enemy {Weights(baseCfg)}.");
        md.AppendLine();
        md.AppendLine("`Gagal spawn` = objek yang dilewati karena maxAttempts habis. `Posisi ditolak` = jumlah posisi kandidat yang gagal validasi.");
        md.AppendLine();

        // 1. Seed berbeda.
        BeginTable("1. Perbandingan seed", "Variasi");
        foreach (int s in seeds)
        {
            SpawnConfig c = baseCfg.Clone();
            c.seed = s;
            Row("Seed", $"Seed {s}", c);
        }

        // 2. Reproducibility.
        BeginTable($"2. Reproducibility (seed {reproSeed} x {reproRuns})", "Run");
        string firstHash = null;
        bool identical = true;
        for (int i = 0; i < reproRuns; i++)
        {
            SpawnConfig c = baseCfg.Clone();
            c.seed = reproSeed;
            SpawnReport r = Row("Reproducibility", $"Run {i + 1}", c);
            firstHash ??= r.LayoutHash;
            identical &= r.LayoutHash == firstHash;
        }
        md.AppendLine();
        md.AppendLine(identical
            ? $"Semua run menghasilkan layout hash `{firstHash}` yang sama: **identik**."
            : "Layout hash berbeda antar run: **tidak identik** (cek apakah obstacle/Player berpindah).");

        // 3. Safe radius.
        BeginTable($"3. Jarak aman Player (seed {parameterSeed})", "Safe radius");
        foreach (float v in safeRadii)
        {
            SpawnConfig c = baseCfg.Clone();
            c.seed = parameterSeed;
            c.playerSafeRadius = v;
            Row("Safe radius", F(v), c);
        }

        // 4. Min spacing.
        BeginTable($"4. Minimum spacing (seed {parameterSeed})", "Min spacing");
        foreach (float v in spacings)
        {
            SpawnConfig c = baseCfg.Clone();
            c.seed = parameterSeed;
            c.minSpacing = v;
            Row("Min spacing", F(v), c);
        }

        // 5. Bobot enemy.
        WeightExperiment(baseCfg);

        LastSummary = md.ToString();
        Debug.Log(LastSummary, this);
        WriteFiles();
    }

    private void WeightExperiment(SpawnConfig baseCfg)
    {
        md.AppendLine();
        md.AppendLine($"## 5. Bobot enemy ({weightGenerations} generation, seed {weightSeedStart}..{weightSeedStart + weightGenerations - 1})");
        md.AppendLine();

        string[] names = Array.ConvertAll(baseCfg.enemyTypes, e => e.displayName);
        var header = new StringBuilder("| Bobot | Total enemy |");
        var line = new StringBuilder("|---|---|");
        foreach (string n in names)
        {
            header.Append($" {n} (aktual) | {n} (target) |");
            line.Append("---|---|");
        }
        header.Append(" Rata-rata ditolak |");
        line.Append("---|");
        md.AppendLine(header.ToString());
        md.AppendLine(line.ToString());

        foreach (float[] weights in new[] { weightsA, weightsB })
        {
            var counts = new int[names.Length];
            int total = 0;
            int rejected = 0;
            for (int g = 0; g < weightGenerations; g++)
            {
                SpawnConfig c = baseCfg.Clone();
                c.seed = weightSeedStart + g;
                c.SetEnemyWeights(weights);
                SpawnReport r = spawner.Plan(c);
                for (int i = 0; i < counts.Length && i < r.enemyTypeCounts.Length; i++)
                    counts[i] += r.enemyTypeCounts[i];
                total += r.EnemyCount;
                rejected += r.RejectedPositions;
                AddCsv("Bobot enemy", $"{WeightsText(weights)} gen {g + 1}", c, r);
            }

            float weightSum = 0f;
            foreach (float w in weights)
                weightSum += w;

            var row = new StringBuilder($"| {WeightsText(weights)} | {total} |");
            for (int i = 0; i < names.Length; i++)
            {
                float actual = total > 0 ? counts[i] * 100f / total : 0f;
                float target = i < weights.Length && weightSum > 0f ? weights[i] * 100f / weightSum : 0f;
                row.Append($" {counts[i]} ({F(actual)}%) | {F(target)}% |");
            }
            row.Append($" {F(rejected / (float)weightGenerations)} |");
            md.AppendLine(row.ToString());
        }
    }

    // ------------------------------------------------------------------ Tabel

    private void BeginTable(string title, string variationLabel)
    {
        md.AppendLine();
        md.AppendLine($"## {title}");
        md.AppendLine();
        md.AppendLine($"| {variationLabel} | Seed | Enemy | Item | Legendary | Gagal spawn | Posisi ditolak (player / exit / spacing / obstacle) | Jenis enemy | Jenis item | Layout hash |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
    }

    private SpawnReport Row(string experiment, string variation, SpawnConfig c)
    {
        SpawnReport r = spawner.Plan(c);
        md.AppendLine($"| {variation} | {c.seed} | {r.EnemyCount}/{r.requestedEnemies} | {r.ItemCount}/{r.requestedItems} | {r.legendaryCount} | " +
                      $"{r.FailedObjects} | {r.RejectedPositions} ({r.rejectedNearPlayer} / {r.rejectedKeepOut} / {r.rejectedSpacing} / {r.rejectedObstacle}) | " +
                      $"{r.TypeSummary(SpawnCategory.Enemy)} | {r.TypeSummary(SpawnCategory.Item)} | `{r.LayoutHash}` |");
        AddCsv(experiment, variation, c, r);
        return r;
    }

    private void AddCsv(string experiment, string variation, SpawnConfig c, SpawnReport r)
    {
        csv.Add(new[]
        {
            experiment, variation, c.seed.ToString(), F(c.playerSafeRadius), F(c.minSpacing), Weights(c),
            r.EnemyCount.ToString(), r.ItemCount.ToString(), r.legendaryCount.ToString(), r.FailedObjects.ToString(),
            r.RejectedPositions.ToString(), r.rejectedNearPlayer.ToString(), r.rejectedKeepOut.ToString(),
            r.rejectedSpacing.ToString(), r.rejectedObstacle.ToString(),
            r.TypeSummary(SpawnCategory.Enemy), r.TypeSummary(SpawnCategory.Item), r.LayoutHash,
        });
    }

    private void WriteFiles()
    {
        try
        {
            string folder = Path.Combine(Application.dataPath, outputFolder);
            Directory.CreateDirectory(folder);

            var sb = new StringBuilder();
            foreach (string[] row in csv)
                sb.AppendLine(string.Join(",", Array.ConvertAll(row, Escape)));

            string csvPath = Path.Combine(folder, "P09_ExperimentResults.csv");
            string mdPath = Path.Combine(folder, "P09_ExperimentResults.md");
            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
            File.WriteAllText(mdPath, md.ToString(), Encoding.UTF8);
            Debug.Log($"[Praktikum 9] Hasil eksperimen ditulis ke:\n{csvPath}\n{mdPath}", this);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Praktikum 9] Gagal menulis file hasil eksperimen: {e.Message}", this);
        }
    }

    private static string Escape(string value)
    {
        return value.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static string Weights(SpawnConfig c)
    {
        return WeightsText(Array.ConvertAll(c.enemyTypes, e => e.weight));
    }

    private static string WeightsText(float[] weights)
    {
        return string.Join(":", Array.ConvertAll(weights, F));
    }

    private static string F(float v)
    {
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
