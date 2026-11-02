using System;
using UnityEngine;

[Serializable]
public struct UtilityScore
{
    public EnemyAction action;
    [Range(0f, 1f)] public float score;
}

/// <summary>Semua input yang dibutuhkan untuk menghitung skor. Diisi oleh <see cref="EnemyBrain"/> tiap frame.</summary>
public struct UtilityContext
{
    public float health;            // HP ternormalisasi 0..1
    public float fleeThreshold;     // 0..1
    public bool canSeeTarget;
    public float distance;          // jarak datar ke Player (m)
    public bool inAttackRange;      // sama dengan kondisi "In Attack Range?" di Behavior Tree
    public float attackRange;
    public float viewRange;
    public bool hasLastSeen;        // ada posisi terakhir Player yang belum dicek
    public EnemyAction current;     // action yang sedang jalan (dapat bonus momentum)
    public float momentum;

    public float attackWeight;
    public float chaseWeight;
    public float fleeWeight;
    public float searchWeight;
    public float patrolWeight;
}

/// <summary>
/// Utility-Based AI: setiap action diberi skor 0..1 dari kondisi saat ini, lalu dipilih skor tertinggi.
/// Berbeda dengan Behavior Tree yang memakai urutan prioritas tetap, di sini action yang "biasanya kalah"
/// bisa menang kalau kondisinya cukup kuat (misal Enemy Aggressive tetap menyerang walau HP rendah).
///
/// Rumus (sebelum dikali bobot personality, lalu di-clamp 0..1):
///   Flee   = HP &lt;= threshold : 0.9 + 0.1 * (1 - HP/threshold)
///            HP &gt;  threshold : 0.9 * t^3,  t = 0 saat HP penuh .. 1 saat HP = threshold
///   Attack = terlihat & dalam Attack Range ? 0.9 * lerp(0.6, 1, HP) : 0
///   Chase  = terlihat ? (0.55 + 0.25 * jauh) * lerp(0.7, 1, HP) : 0
///            jauh = 0 di Attack Range .. 1 di batas Vision Range
///   Search = ada posisi terakhir & tidak terlihat ? 0.4 : 0
///   Patrol = 0.1 (baseline, selalu ada)
///   + momentum kecil untuk action yang sedang berjalan supaya tidak gonta-ganti tiap frame.
/// </summary>
public static class UtilityScoring
{
    public static readonly EnemyAction[] Actions =
    {
        EnemyAction.Attack,
        EnemyAction.Chase,
        EnemyAction.Flee,
        EnemyAction.Search,
        EnemyAction.Patrol,
    };

    public static float Score(EnemyAction action, in UtilityContext c)
    {
        float raw;
        switch (action)
        {
            case EnemyAction.Flee:
                raw = FleeScore(c) * c.fleeWeight;
                break;
            case EnemyAction.Attack:
                raw = AttackScore(c) * c.attackWeight;
                break;
            case EnemyAction.Chase:
                raw = ChaseScore(c) * c.chaseWeight;
                break;
            case EnemyAction.Search:
                raw = (c.hasLastSeen && !c.canSeeTarget ? 0.4f : 0f) * c.searchWeight;
                break;
            case EnemyAction.Patrol:
                raw = 0.1f * c.patrolWeight;
                break;
            default:
                return 0f;
        }

        if (raw > 0f && action == c.current)
            raw += c.momentum;
        return Mathf.Clamp01(raw);
    }

    /// <summary>Isi <paramref name="results"/> (panjang = Actions.Length) dan kembalikan action dengan skor tertinggi.</summary>
    public static EnemyAction Select(in UtilityContext c, UtilityScore[] results)
    {
        EnemyAction best = EnemyAction.Patrol;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < Actions.Length; i++)
        {
            float s = Score(Actions[i], c);
            results[i] = new UtilityScore { action = Actions[i], score = s };
            if (s > bestScore)
            {
                bestScore = s;
                best = Actions[i];
            }
        }
        return best;
    }

    private static float FleeScore(in UtilityContext c)
    {
        float threshold = Mathf.Max(0.01f, c.fleeThreshold);
        if (c.health <= threshold)
            return 0.9f + 0.1f * (1f - c.health / threshold);

        float t = Mathf.InverseLerp(1f, threshold, c.health);
        return 0.9f * t * t * t;
    }

    private static float AttackScore(in UtilityContext c)
    {
        if (!c.canSeeTarget || !c.inAttackRange)
            return 0f;
        return 0.9f * Mathf.Lerp(0.6f, 1f, c.health);
    }

    private static float ChaseScore(in UtilityContext c)
    {
        if (!c.canSeeTarget)
            return 0f;
        float far = Mathf.Clamp01((c.distance - c.attackRange) / Mathf.Max(0.01f, c.viewRange - c.attackRange));
        return (0.55f + 0.25f * far) * Mathf.Lerp(0.7f, 1f, c.health);
    }
}
