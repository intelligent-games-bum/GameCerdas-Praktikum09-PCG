using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// (Bonus On-screen Behavior Tree Debugger) Panel debug Praktikum 6:
/// - Kiri  : Current Action, mode, personality, HP, perception, cooldown, lalu pohon BT live.
///           Node yang dievaluasi frame ini diwarnai sesuai hasilnya (hijau Success, merah Failure,
///           kuning Running); node yang tidak disentuh (karena cabang di atasnya sudah menang) abu-abu.
/// - Kanan : skor Utility AI tiap action + perbandingan pilihan BT vs Utility.
/// - Label action di atas kepala Enemy.
/// Tombol uji coba: M ganti mode BT/Utility, 1/2/3 ganti personality, K lukai Enemy, L bunuh Enemy,
/// H pulihkan Player, Tab sembunyikan panel, R restart. Klik kiri = Player menyerang.
/// Ukuran UI diskalakan terhadap tinggi layar 720 px supaya tetap terbaca di rekaman 1080p.
/// </summary>
public class BTDebuggerHUD : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private Health enemyHealth;
    [SerializeField] private Health playerHealth;
    [Tooltip("Personality yang bisa dipilih dengan tombol 1, 2, 3, ...")]
    [SerializeField] private EnemyPersonality[] personalities;
    [Tooltip("Script Player yang dimatikan saat Player kalah (gerak, serangan).")]
    [SerializeField] private MonoBehaviour[] disableOnPlayerDeath;
    [Tooltip("Damage ke Enemy saat tombol K ditekan.")]
    [SerializeField, Min(0f)] private float debugDamage = 15f;
    [Tooltip("Tinggi label action di atas pivot (kaki) Enemy.")]
    [SerializeField] private float labelHeight = 3.35f;

    private static readonly Color SuccessColor = new Color(0.35f, 0.95f, 0.4f);
    private static readonly Color FailureColor = new Color(1f, 0.35f, 0.35f);
    private static readonly Color RunningColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color IdleColor = new Color(0.55f, 0.55f, 0.55f);
    private static readonly Color PlayerBarColor = new Color(0.25f, 0.6f, 1f);
    private static readonly Color EnemyBarColor = new Color(0.9f, 0.25f, 0.25f);

    private readonly List<(BTNode node, int depth)> rows = new List<(BTNode, int)>();
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle treeStyle;
    private GUIStyle bigStyle;
    private GUIStyle worldStyle;
    private Texture2D pixel;
    private float uiScale = 1f;
    private bool showPanels = true;

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.Died -= OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        if (disableOnPlayerDeath != null)
        {
            foreach (MonoBehaviour mb in disableOnPlayerDeath)
                if (mb != null)
                    mb.enabled = false;
        }
        Debug.Log("[Praktikum 6] Player kalah. Tekan R untuk restart.");
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.mKey.wasPressedThisFrame && enemy != null)
            enemy.Mode = enemy.Mode == DecisionMode.BehaviorTree ? DecisionMode.UtilityAI : DecisionMode.BehaviorTree;
        if (kb.kKey.wasPressedThisFrame && enemyHealth != null)
            enemyHealth.TakeDamage(debugDamage);
        if (kb.lKey.wasPressedThisFrame && enemyHealth != null)
            enemyHealth.Kill();
        if (kb.hKey.wasPressedThisFrame && playerHealth != null)
            playerHealth.HealTo(1f);
        if (kb.tabKey.wasPressedThisFrame)
            showPanels = !showPanels;
        if (kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        Key[] numberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        for (int i = 0; personalities != null && i < personalities.Length && i < numberKeys.Length; i++)
        {
            if (kb[numberKeys[i]].wasPressedThisFrame && enemy != null)
                enemy.ApplyPersonality(personalities[i]);
        }
    }

    // ------------------------------------------------------------------ GUI

    private void InitStyles()
    {
        if (boxStyle != null)
            return;
        pixel = MakeTex(Color.white);

        boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = MakeTex(new Color(0f, 0f, 0f, 0.65f)) } };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
        labelStyle.normal.textColor = Color.white;
        smallStyle = new GUIStyle(labelStyle) { fontSize = 12 };
        treeStyle = new GUIStyle(smallStyle) { margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(2, 2, 1, 1), wordWrap = false };
        bigStyle = new GUIStyle(labelStyle) { fontSize = 22, fontStyle = FontStyle.Bold };
        worldStyle = new GUIStyle(labelStyle) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private static string Hex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    private void OnGUI()
    {
        if (enemy == null)
            return;
        InitStyles();

        uiScale = Mathf.Max(0.5f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float screenW = Screen.width / uiScale;

        if (showPanels)
        {
            // Kolom kiri: panel status dan panel tree ditumpuk, tingginya otomatis mengikuti isi.
            GUILayout.BeginArea(new Rect(12, 12, 340, 720f - 70f));
            DrawStatusPanel();
            GUILayout.Space(6);
            DrawTreePanel();
            GUILayout.EndArea();
            DrawUtilityPanel(screenW);
        }
        DrawControls(screenW);
        DrawWorldLabel();

        if (playerHealth != null && playerHealth.IsDead)
        {
            var center = new GUIStyle(bigStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 32 };
            GUI.Label(new Rect(0, 720f * 0.4f, screenW, 60), "<color=#ff5050>PLAYER KALAH</color> - tekan R", center);
        }

        GUI.matrix = Matrix4x4.identity;
    }

    private void DrawStatusPanel()
    {
        EnemyAction action = enemy.CurrentAction;
        EnemyPersonality p = enemy.Personality;

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>PRAKTIKUM 6 - BEHAVIOR TREE AI</b>", labelStyle);
        GUILayout.Label($"Current Action: <color={EnemyBrain.ActionColorHex(action)}>{action.ToString().ToUpper()}</color>", bigStyle);
        string modeText = enemy.Mode == DecisionMode.BehaviorTree ? "Behavior Tree (prioritas)" : "Utility AI (skor tertinggi)";
        string personalityText = p != null ? $"<color={Hex(p.color)}>{p.displayName}</color>" : "-";
        GUILayout.Label($"Mode: <b>{modeText}</b>\nPersonality: <b>{personalityText}</b> | Flee <= {enemy.FleeThreshold:P0}", smallStyle);
        GUILayout.Label($"Cabang aktif: <b>{enemy.ActiveBranch}</b>", smallStyle);

        if (enemyHealth != null)
            PanelBar("HP Enemy", enemyHealth, EnemyBarColor, enemy.FleeThreshold);
        if (playerHealth != null)
            PanelBar("HP Player", playerHealth, PlayerBarColor, -1f);

        EnemyPerception per = enemy.Perception;
        GUILayout.Label($"Jarak {enemy.DistanceToTarget:0.0} m | Range {Check(per.InRange)} FOV {Check(per.InFieldOfView)} " +
                        $"LoS {Check(per.HasLineOfSight)} = Lihat {Check(per.CanSeeTarget)}", smallStyle);

        string extra = $"Cooldown Attack: {Cooldown()}";
        if (action == EnemyAction.Flee && enemy.FleeTarget != null)
            extra += $" | Kabur ke {enemy.FleeTarget.name}";
        if (action == EnemyAction.Search)
            extra += enemy.LookingAround ? $" | Melihat sekeliling {enemy.SearchTimeLeft:0.0}s" : " | Menuju posisi terakhir";
        GUILayout.Label(extra, smallStyle);
        GUILayout.EndVertical();
    }

    private string Cooldown()
    {
        CooldownDecorator cd = enemy.AttackCooldownNode;
        return cd.IsReady ? "<color=#5af066>siap</color>" : $"<color=#ffd633>{cd.Remaining:0.0}s / {cd.Cooldown:0.0}s</color>";
    }

    private static string Check(bool value)
    {
        return value ? "<color=#5af066>ya</color>" : "<color=#ff5a5a>tidak</color>";
    }

    private void DrawTreePanel()
    {
        rows.Clear();
        Flatten(enemy.Root, 0);

        GUILayout.BeginVertical(boxStyle);
        string title = enemy.Mode == DecisionMode.BehaviorTree
            ? "<b>BEHAVIOR TREE</b> <size=11>(live)</size>"
            : "<b>BEHAVIOR TREE</b> <size=11>(mode Utility: hanya node action terpilih)</size>";
        GUILayout.Label(title, smallStyle);
        foreach ((BTNode node, int depth) in rows)
        {
            Color c = !node.EvaluatedThisFrame ? IdleColor
                : node.LastState == NodeState.Success ? SuccessColor
                : node.LastState == NodeState.Running ? RunningColor
                : FailureColor;
            string state = node.EvaluatedThisFrame ? node.LastState.ToString() : "-";
            string info = string.IsNullOrEmpty(node.Info) ? "" : $" ({node.Info})";
            if (!node.EvaluatedThisFrame && node.Children.Count > 0 && enemy.Mode == DecisionMode.BehaviorTree)
                info += " [...]";
            string indent = new string(' ', depth * 4);
            GUILayout.Label($"<color={Hex(c)}>{indent}{Tag(node)} {node.Name}{info}  <b>{state}</b></color>", treeStyle);
        }
        GUILayout.EndVertical();
    }

    /// <summary>
    /// Mode BT: cabang yang tidak dievaluasi frame ini ditampilkan satu baris saja (anaknya dilipat),
    /// supaya panel ringkas dan fokus ke jalur yang sedang aktif. Mode Utility: semua node ditampilkan.
    /// </summary>
    private void Flatten(BTNode node, int depth)
    {
        rows.Add((node, depth));
        bool collapse = enemy.Mode == DecisionMode.BehaviorTree && depth > 0 && !node.EvaluatedThisFrame;
        if (collapse)
            return;
        foreach (BTNode child in node.Children)
            Flatten(child, depth + 1);
    }

    private static string Tag(BTNode node)
    {
        switch (node)
        {
            case SelectorNode _: return "[?]";
            case SequenceNode _: return "[>>]";
            case DecoratorNode _: return "[D]";
            case ConditionNode _: return "[c]";
            default: return "[a]";
        }
    }

    private void DrawUtilityPanel(float screenW)
    {
        IReadOnlyList<UtilityScore> scores = enemy.UtilityScores;
        const float width = 300f;
        GUILayout.BeginArea(new Rect(screenW - width - 12, 12, width, 400));
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>UTILITY AI - SKOR</b>", labelStyle);

        foreach (UtilityScore s in scores)
        {
            bool best = s.action == enemy.UtilityChoice;
            Rect r = GUILayoutUtility.GetRect(width - 24, 22);
            GUI.Label(new Rect(r.x, r.y, 70, r.height), best ? $"<b>{s.action}</b>" : s.action.ToString(), smallStyle);
            var bar = new Rect(r.x + 70, r.y + 5, 150, 12);
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(bar, pixel);
            Color c = EnemyBrain.ActionColor(s.action);
            GUI.color = best ? c : new Color(c.r, c.g, c.b, 0.55f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * s.score, bar.height), pixel);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 226, r.y, 60, r.height), best ? $"<b>{s.score:0.00}</b> <" : $"{s.score:0.00}", smallStyle);
        }

        EnemyAction btPick = enemy.Mode == DecisionMode.BehaviorTree ? enemy.CurrentAction : EnemyAction.None;
        string compare = enemy.Mode == DecisionMode.BehaviorTree
            ? $"BT: <b>{btPick}</b> | Utility: <b>{enemy.UtilityChoice}</b> = " +
              (btPick == enemy.UtilityChoice ? "<color=#5af066>SAMA</color>" : "<color=#ffd633>BEDA</color>")
            : $"Selected Action = <b>{enemy.UtilityChoice}</b>";
        GUILayout.Label(compare, smallStyle);
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawControls(float screenW)
    {
        const string text = "WASD gerak | Klik kiri serang | M mode BT/Utility | 1 Balanced 2 Aggressive 3 Coward\n" +
                            "K lukai Enemy | L bunuh Enemy | H pulihkan Player | Tab panel | R restart";
        GUI.Label(new Rect(12, 720f - 44, screenW - 24, 40), $"<size=12>{text}</size>", labelStyle);
    }

    private void PanelBar(string label, Health h, Color color, float marker)
    {
        GUILayout.Label($"{label}: {h.Current:0} / {h.Max:0}", smallStyle);
        Rect r = GUILayoutUtility.GetRect(310, 8);
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(r, pixel);
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * h.Normalized, r.height), pixel);
        if (marker >= 0f)
        {
            GUI.color = new Color(0.3f, 0.6f, 1f);
            GUI.DrawTexture(new Rect(r.x + r.width * marker - 1, r.y - 2, 2, r.height + 4), pixel);
        }
        GUI.color = Color.white;
    }

    /// <summary>Nama action di atas kepala Enemy (bar HP-nya world-space, lihat <see cref="WorldHealthBar"/>).</summary>
    private void DrawWorldLabel()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Vector3 sp = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * labelHeight);
        if (sp.z <= 0f)
            return;

        float x = sp.x / uiScale;
        float y = (Screen.height - sp.y) / uiScale;
        EnemyAction action = enemy.CurrentAction;
        GUI.Label(new Rect(x - 80, y - 12, 160, 22),
            $"<color={EnemyBrain.ActionColorHex(action)}>{action.ToString().ToUpper()}</color>", worldStyle);
    }
}
