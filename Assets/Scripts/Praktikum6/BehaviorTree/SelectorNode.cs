/// <summary>
/// Selector (logika OR / "fallback"): coba anak dari kiri ke kanan.
/// - Anak Success / Running -> Selector langsung mengembalikan hasil itu, anak setelahnya tidak dievaluasi.
/// - Anak Failure -> lanjut ke anak berikutnya.
/// - Semua anak Failure -> Selector Failure.
/// Karena itu anak paling kiri adalah prioritas tertinggi.
/// </summary>
public class SelectorNode : CompositeNode
{
    public SelectorNode(string name, params BTNode[] children) : base(name, children)
    {
    }

    protected override NodeState OnEvaluate()
    {
        foreach (BTNode child in children)
        {
            NodeState state = child.Evaluate();
            if (state != NodeState.Failure)
                return state;
        }
        return NodeState.Failure;
    }
}
