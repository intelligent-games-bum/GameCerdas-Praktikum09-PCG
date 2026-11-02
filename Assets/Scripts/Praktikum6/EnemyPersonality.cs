using UnityEngine;

/// <summary>
/// (Bonus Personality Enemy) Sekumpulan angka yang membentuk "watak" Enemy.
/// Behavior Tree-nya tetap sama; yang berubah hanya threshold dan kecepatan (dipakai BT)
/// serta bobot skor (dipakai Utility AI). Contoh: Aggressive baru kabur saat HP sangat rendah,
/// Coward kabur jauh lebih awal.
/// </summary>
[CreateAssetMenu(fileName = "EnemyPersonality", menuName = "Praktikum 6/Enemy Personality")]
public class EnemyPersonality : ScriptableObject
{
    public string displayName = "Balanced";
    public Color color = Color.white;

    [Header("Behavior Tree")]
    [Tooltip("Flee jika HP <= fraksi ini dari Max HP.")]
    [Range(0f, 1f)] public float fleeThreshold = 0.3f;
    [Min(0f)] public float chaseSpeed = 4.5f;
    [Min(0.05f)] public float attackCooldown = 1.5f;
    [Min(0f)] public float attackDamage = 10f;
    [Tooltip("Lama melihat sekeliling di posisi terakhir Player terlihat (detik).")]
    [Min(0f)] public float searchDuration = 4f;

    [Header("Utility AI (pengali skor)")]
    [Min(0f)] public float attackWeight = 1f;
    [Min(0f)] public float chaseWeight = 1f;
    [Min(0f)] public float fleeWeight = 1f;
    [Min(0f)] public float searchWeight = 1f;
    [Min(0f)] public float patrolWeight = 1f;
}
