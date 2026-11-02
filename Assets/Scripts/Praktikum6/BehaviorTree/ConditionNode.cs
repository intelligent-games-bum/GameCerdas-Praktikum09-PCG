using System;

/// <summary>Leaf yang hanya mengecek kondisi: true -> Success, false -> Failure. Tidak pernah Running.</summary>
public class ConditionNode : BTNode
{
    private readonly Func<bool> condition;

    public ConditionNode(string name, Func<bool> condition) : base(name)
    {
        this.condition = condition;
    }

    protected override NodeState OnEvaluate()
    {
        return condition() ? NodeState.Success : NodeState.Failure;
    }
}
