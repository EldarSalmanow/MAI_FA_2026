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
        if (node == null)
        {
            return (null, null);
        }

        node.Parent = null;

        if (Comparer.Compare(key, node.Key) < 0)
        {
            var (left, right) = Split(node.Left, key);
            
            node.Left = right;
            right?.Parent = node;

            return (left, node);
        }
        else
        {
            var (left, right) = Split(node.Right, key);
            
            node.Right = left;
            left?.Parent = node;

            return (node, right);
        }
    }

    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null)
        {
            return right;
        }

        if (right == null)
        {
            return left;
        }

        TreapNode<TKey, TValue> root;

        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            left.Right!.Parent = left;
            
            root = left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            right.Left!.Parent = right;
            
            root = right;
        }

        root.Parent = null; 
        
        return root;
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
        if (FindNode(key) is not { } node)
        {
            return false;
        }

        var replacement = Merge(node.Left, node.Right);

        replacement?.Parent = node.Parent;

        if (node.Parent == null)
        {
            Root = replacement;
        }
        else if (node.IsLeftChild)
        {
            node.Parent.Left = replacement;
        }
        else
        {
            node.Parent.Right = replacement;
        }

        node.Left = node.Right = node.Parent = null;
        
        Count--;
        OnNodeRemoved(node.Parent, replacement);

        return true;
    }
}