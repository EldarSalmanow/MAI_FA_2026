using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value) => 
        new(key, value);

    protected virtual (TreapNode<TKey, TValue>?, TreapNode<TKey, TValue>?)
        Split(TreapNode<TKey, TValue>? node, TKey key)
    {
        if (node is null) return (null, null);

        return Comparer.Compare(node.Key, key) <= 0
            ? SplitLeft(node, key)
            : SplitRight(node, key);
    }

    private (TreapNode<TKey, TValue>?, TreapNode<TKey, TValue>?) SplitLeft(TreapNode<TKey, TValue> node, TKey key)
    {
        var (left, right) = Split(node.Right, key);

        node.Right = left;
        left?.Parent = node;
        node.Parent = null;
        right?.Parent = null;

        return (node, right);
    }

    private (TreapNode<TKey, TValue>?, TreapNode<TKey, TValue>?) SplitRight(TreapNode<TKey, TValue> node, TKey key)
    {
        var (left, right) = Split(node.Left, key);

        node.Left = right;
        right?.Parent = node;
        node.Parent = null;
        left?.Parent = null;

        return (left, node);
    }

    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left is null)
        {
            right?.Parent = null;
            
            return right;
        }

        if (right is null)
        {
            left.Parent = null;
            
            return left;
        }

        if (left.Priority >= right.Priority)
        {
            left.Right = Merge(left.Right, right);
            
            if (left.Right is { } leftRight) leftRight.Parent = left;
            
            left.Parent = null;
            
            return left;
        }

        right.Left = Merge(left, right.Left);

        if (right.Left is { } rightLeft) rightLeft.Parent = right;
        
        right.Parent = null;
        
        return right;
    }


    public override void Add(TKey key, TValue value)
    {
        if (FindNode(key) is { } existing)
        {
            existing.Value = value;

            return;
        }

        var node = CreateNode(key, value);

        var (left, right) = Split(Root, key);

        Root = Merge(Merge(left, node), right);
        Count++;

        OnNodeAdded(node);
    }

    public override bool Remove(TKey key)
    {
        TreapNode<TKey, TValue>? current = Root, parent = null;

        while (current != null)
        {
            var cmp = Comparer.Compare(key, current.Key);

            if (cmp == 0)
            {
                var replacement = Merge(current.Left, current.Right);

                if (parent is null)
                {
                    Root = replacement;
                }
                else if (current == parent.Left)
                {
                    parent.Left = replacement;
                }
                else
                {
                    parent.Right = replacement;
                }

                replacement?.Parent = parent;

                current.Left = null;
                current.Right = null;
                current.Parent = null;

                Count--;
                
                OnNodeRemoved(parent, replacement);

                return true;
            }

            parent = current;
            
            current = cmp < 0 ? current.Left : current.Right;
        }

        return false;
    }
}