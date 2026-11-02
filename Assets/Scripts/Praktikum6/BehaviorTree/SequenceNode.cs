/// <summary>
/// Sequence (logika AND): jalankan anak dari kiri ke kanan.
/// - Anak Failure / Running -> Sequence langsung mengembalikan hasil itu.
/// - Anak Success -> lanjut ke anak berikutnya.
/// - Semua anak Success -> Sequence Success.
/// Pola umum: beberapa Condition di depan sebagai "gerbang", Action di paling belakang.
/// </summary>
public class SequenceNode : CompositeNode
{
    public SequenceNode(string name, params BTNode[] children) : base(name, children)
    {
    }

    protected override NodeState OnEvaluate()
    {
        foreach (BTNode child in children)
        {
            NodeState state = child.Evaluate();
            if (state != NodeState.Success)
                return state;
        }
        return NodeState.Success;
    }
}
