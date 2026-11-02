using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// HUD Praktikum 9 (uGUI, dibangun lewat kode saat runtime):
/// - Panel debug: seed, layout hash, jumlah spawn, gagal spawn, posisi ditolak per alasan.
/// - Statistik jenis objek (fitur): jumlah tiap jenis enemy/item hasil generation + yang sudah diambil.
/// - Tombol regenerate (fitur): seed sama, seed acak, seed -1/+1, dan jalankan eksperimen.
/// - Status goal: item terkumpul, skor, HP, Exit, menang/kalah.
/// </summary>
public class SpawnerHUD : MonoBehaviour
{
    [SerializeField] private SmartSpawner spawner;
    [SerializeField] private P9GameGoal goal;
    [SerializeField] private SpawnExperimentRunner runner;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.68f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.28f, 0.95f);

    private Font font;
    private Text infoText;
    private Text statusText;
    private Text toastText;
    private float refreshTimer;
    private float toastTimer;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildUI();
    }

    private void OnEnable()
    {
        if (spawner != null)
            spawner.Generated += OnGenerated;
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.Generated -= OnGenerated;
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            Refresh();
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f)
                toastText.text = "";
        }
    }

    private void OnGenerated(SpawnReport report)
    {
        Toast($"Generation #{spawner.GenerationCount} - seed {report.seed}");
        Refresh();
    }

    // ------------------------------------------------------------------ Isi teks

    private void Refresh()
    {
        if (spawner == null)
            return;

        SpawnReport r = spawner.LastReport;
        SpawnConfig c = spawner.Config;
        var sb = new StringBuilder();
        sb.AppendLine("<b><size=17>PRAKTIKUM 9 - SMART SPAWNER (PCG)</size></b>");

        if (r != null)
        {
            sb.AppendLine($"<b>Seed</b> <color=#ffd84a>{r.seed}</color>   layout <color=#9ad>{r.LayoutHash}</color>   gen #{spawner.GenerationCount}");
            sb.AppendLine($"Area {c.areaSize.x:0.#} x {c.areaSize.y:0.#} m | safe {c.playerSafeRadius:0.#} m | spacing {c.minSpacing:0.#} m | max attempts {c.maxAttempts}");
            sb.AppendLine($"<b>Spawn</b>  Enemy <color=#ff7a6a>{r.EnemyCount}/{r.requestedEnemies}</color>   Item <color=#6ac0ff>{r.ItemCount}/{r.requestedItems}</color>   " +
                          $"gagal spawn <color={(r.FailedObjects > 0 ? "#ff5050" : "#8f8")}>{r.FailedObjects}</color>");
            sb.AppendLine($"<b>Rejected</b> {r.RejectedPositions} posisi  (player {r.rejectedNearPlayer} | exit {r.rejectedKeepOut} | spacing {r.rejectedSpacing} | obstacle {r.rejectedObstacle})");
            sb.AppendLine();
            sb.AppendLine("<b>Statistik jenis objek</b>");
            sb.AppendLine($"  Enemy  {TypeLine(r.enemyTypeNames, r.enemyTypeCounts, c.enemyTypes, null)}");
            sb.AppendLine($"  Item   {TypeLine(r.itemTypeNames, r.itemTypeCounts, c.itemTypes, goal != null ? goal.CollectedByType : null)}");
            if (r.legendaryCount > 0)
            {
                int got = goal != null ? goal.LegendaryCollected : 0;
                sb.AppendLine($"  <color=#ffcc33>LEGENDARY x{r.legendaryCount}</color> (diambil {got})");
            }
            else
            {
                sb.AppendLine("  <color=#888>Legendary: tidak muncul di seed ini</color>");
            }
        }

        if (goal != null)
        {
            sb.AppendLine();
            Health hp = goal.PlayerHealth;
            string hpText = hp != null ? $"{hp.Current:0}/{hp.Max:0}" : "-";
            sb.AppendLine($"<b>Goal</b>  item {goal.ItemsCollected}/{goal.ItemsTotal}   skor {goal.Score}   HP {hpText}   {goal.RoundTime:0}s");
            sb.AppendLine(goal.ExitOpen ? "  Exit: <color=#5f5>TERBUKA - menuju Exit!</color>" : "  Exit: <color=#f66>TERKUNCI - ambil semua item</color>");
        }

        if (spawner.SeedHistory.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine($"<color=#aaa>Riwayat seed: {string.Join(", ", spawner.SeedHistory)}</color>");
        }

        sb.AppendLine();
        sb.Append("<color=#ccc>WASD gerak | Klik kiri serang | R regenerate | N seed acak | Esc lepas kursor (klik tombol)</color>");
        infoText.text = sb.ToString();

        statusText.text = goal == null ? "" :
            goal.Current == P9GameGoal.State.Won ? $"<color=#5f5>MENANG!</color>  skor {goal.Score}\n<size=20>tekan R / tombol Regenerate</size>" :
            goal.Current == P9GameGoal.State.Lost ? "<color=#ff5050>KALAH</color>\n<size=20>tekan R untuk mengulang seed yang sama</size>" : "";
    }

    private static string TypeLine(string[] names, int[] counts, SpawnEntry[] entries, IReadOnlyDictionary<string, int> collected)
    {
        float total = 0f;
        foreach (SpawnEntry e in entries)
            if (e != null)
                total += e.weight;

        var parts = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            float pct = total > 0f && i < entries.Length ? entries[i].weight * 100f / total : 0f;
            string s = $"{names[i]} <b>{counts[i]}</b> <color=#999>({pct:0}%)</color>";
            if (collected != null && collected.TryGetValue(names[i], out int got))
                s += $" <color=#8f8>[ambil {got}]</color>";
            parts.Add(s);
        }
        return string.Join("  ", parts);
    }

    private void Toast(string message)
    {
        toastText.text = message;
        toastTimer = 2.5f;
    }

    // ------------------------------------------------------------------ Tombol

    private void OnRegenerate()
    {
        spawner.Regenerate();
    }

    private void OnRandomSeed()
    {
        spawner.RegenerateWithRandomSeed();
    }

    private void OnSeedStep(int step)
    {
        spawner.RegenerateWithSeed(spawner.Config.seed + step);
    }

    private void OnRunExperiments()
    {
        if (runner == null)
            return;
        runner.RunAll();
        Toast("Eksperimen selesai - lihat Console / folder Experiments");
    }

    // ------------------------------------------------------------------ Membangun UI

    private void BuildUI()
    {
        var canvasGo = new GameObject("P9 HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        // Panel debug kiri atas.
        RectTransform panel = CreatePanel(root, "DebugPanel", new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(560f, 360f));
        infoText = CreateText(panel, "Info", 13, TextAnchor.UpperLeft);
        Stretch(infoText.rectTransform, 10f);

        // Baris tombol di bawah panel.
        float y = -378f;
        CreateButton(root, "Regenerate (R)", new Vector2(10f, y), 140f, OnRegenerate);
        CreateButton(root, "Seed Acak (N)", new Vector2(156f, y), 130f, OnRandomSeed);
        CreateButton(root, "Seed -1", new Vector2(292f, y), 80f, () => OnSeedStep(-1));
        CreateButton(root, "Seed +1", new Vector2(378f, y), 80f, () => OnSeedStep(1));
        if (runner != null)
            CreateButton(root, "Run Experiments", new Vector2(10f, y - 42f), 150f, OnRunExperiments);

        // Status menang/kalah di tengah.
        statusText = CreateText(root, "Status", 44, TextAnchor.MiddleCenter);
        statusText.fontStyle = FontStyle.Bold;
        RectTransform st = statusText.rectTransform;
        st.anchorMin = new Vector2(0f, 0.5f);
        st.anchorMax = new Vector2(1f, 0.5f);
        st.sizeDelta = new Vector2(0f, 140f);
        st.anchoredPosition = new Vector2(0f, 60f);
        statusText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

        // Notifikasi singkat di atas tengah.
        toastText = CreateText(root, "Toast", 20, TextAnchor.MiddleCenter);
        RectTransform tt = toastText.rectTransform;
        tt.anchorMin = new Vector2(0f, 1f);
        tt.anchorMax = new Vector2(1f, 1f);
        tt.pivot = new Vector2(0.5f, 1f);
        tt.sizeDelta = new Vector2(0f, 40f);
        tt.anchoredPosition = new Vector2(0f, -12f);
        toastText.gameObject.AddComponent<Outline>();
    }

    private RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = PanelColor;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private Text CreateText(Transform parent, string name, int size, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.supportRichText = true;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void CreateButton(Transform parent, string label, Vector2 position, float width, UnityAction onClick)
    {
        RectTransform rt = CreatePanel(parent, "Button_" + label, new Vector2(0f, 1f), position, new Vector2(width, 34f));
        Image image = rt.GetComponent<Image>();
        image.color = ButtonColor;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.8f, 0.9f, 1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        rt.gameObject.AddComponent<HudPressButton>().onPress.AddListener(onClick);

        Text text = CreateText(rt, "Label", 14, TextAnchor.MiddleCenter);
        text.text = label;
        Stretch(text.rectTransform, 2f);
    }

    private static void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static void EnsureEventSystem()
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null)
            es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        var module = es.GetComponent<InputSystemUIInputModule>();
        if (module == null)
            module = es.gameObject.AddComponent<InputSystemUIInputModule>();
        // Modul yang dibuat lewat kode belum punya action map untuk pointer/klik.
        if (module.actionsAsset == null)
            module.AssignDefaultActions();
    }
}
