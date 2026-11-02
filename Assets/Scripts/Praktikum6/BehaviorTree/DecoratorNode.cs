using System.Collections.Generic;

/// <summary>Node dengan tepat satu anak yang mengubah / membatasi cara anak itu dijalankan.</summary>
public abstract class DecoratorNode : BTNode
{
    protected readonly BTNode child;
    private readonly BTNode[] childList;

    protected DecoratorNode(string name, BTNode child) : base(name)
    {
        this.child = child;
        childList = new[] { child };
    }

    public override IReadOnlyList<BTNode> Children => childList;
}
