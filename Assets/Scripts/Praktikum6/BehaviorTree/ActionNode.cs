using System;

/// <summary>Leaf yang melakukan sesuatu (bergerak, menyerang). Hasilnya ditentukan oleh fungsi action.</summary>
public class ActionNode : BTNode
{
    private readonly Func<NodeState> action;

    public ActionNode(string name, Func<NodeState> action) : base(name)
    {
        this.action = action;
    }

    protected override NodeState OnEvaluate()
    {
        return action();
    }
}
