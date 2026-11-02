using UnityEngine;

/// <summary>
/// Membatasi seberapa sering anaknya boleh dijalankan.
/// - Masih cooldown -> Failure, anak TIDAK dievaluasi sama sekali.
/// - Siap -> evaluasi anak. Jika anak Success, cooldown dimulai lagi.
/// Dipakai untuk Attack supaya pukulan tidak terjadi setiap frame.
/// </summary>
public class CooldownDecorator : DecoratorNode
{
    private float readyTime;

    public CooldownDecorator(string name, float cooldown, BTNode child) : base(name, child)
    {
        Cooldown = cooldown;
    }

    /// <summary>Durasi cooldown (detik). Boleh diubah saat runtime (misal ganti personality).</summary>
    public float Cooldown { get; set; }
    public float Remaining => Mathf.Max(0f, readyTime - Time.time);
    public bool IsReady => Time.time >= readyTime;

    public override string Info => IsReady ? "siap" : $"{Remaining:0.0}s";

    /// <summary>Pastikan anak belum boleh jalan setidaknya selama <paramref name="seconds"/> dari sekarang.</summary>
    public void DelayAtLeast(float seconds)
    {
        readyTime = Mathf.Max(readyTime, Time.time + seconds);
    }

    protected override NodeState OnEvaluate()
    {
        if (!IsReady)
            return NodeState.Failure;

        NodeState state = child.Evaluate();
        if (state == NodeState.Success)
            readyTime = Time.time + Cooldown;
        return state;
    }
}
