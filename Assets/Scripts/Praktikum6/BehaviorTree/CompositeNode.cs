using System.Collections.Generic;

/// <summary>Node dengan banyak anak (Selector, Sequence). Urutan anak = urutan evaluasi.</summary>
public abstract class CompositeNode : BTNode
{
    protected readonly List<BTNode> children;

    protected CompositeNode(string name, params BTNode[] children) : base(name)
    {
        this.children = new List<BTNode>(children);
    }

    public override IReadOnlyList<BTNode> Children => children;
}
