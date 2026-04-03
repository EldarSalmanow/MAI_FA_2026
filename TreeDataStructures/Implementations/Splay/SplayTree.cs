using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
    where TKey : IComparable<TKey>
{
    public override void Add(TKey key, TValue value)
    {
        if (FindAndSplay(key) is { } node)
        {
            node.Value = value;
        }
        else
        {
            base.Add(key, value); 
        }
    }

    public override bool Remove(TKey key)
    {
        return FindAndSplay(key) != null && base.Remove(key);
    }

    public override bool ContainsKey(TKey key) => FindAndSplay(key) != null;

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (FindAndSplay(key) is { } node)
        {
            value = node.Value;
            
            return true;
        }

        value = default;
        
        return false;
    }
    
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode) => Splay(newNode);

    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        if ((child ?? parent) is { } target)
        {
            Splay(target);
        }
    }

    private BstNode<TKey, TValue>? FindAndSplay(TKey key)
    {
        BstNode<TKey, TValue>? current = Root, parent = null;

        while (current != null)
        {
            var cmp = Comparer.Compare(key, current.Key);
            
            if (cmp == 0)
            {
                break;
            }

            parent = current;
            
            current = cmp < 0 ? current.Left : current.Right;
        }

        if ((current ?? parent) is { } target) 
        {
            Splay(target);
        }

        return current;
    }

    private void Splay(BstNode<TKey, TValue> node)
    {
        while (node.Parent is { } parent)
        {
            // zig
            if (parent.Parent is not { } grand)
            {
                if (node.IsLeftChild)
                {
                    RotateRight(parent);
                }
                else
                {
                    RotateLeft(parent);
                }
            }
            // zig-zig
            else if (node.IsLeftChild && parent.IsLeftChild)
            {
                RotateRight(grand);
                RotateRight(parent);
            }
            else if (node.IsRightChild && parent.IsRightChild)
            {
                RotateLeft(grand);
                RotateLeft(parent);
            }
            // zig-zag
            else if (node.IsRightChild)
            {
                RotateLeft(parent);
                RotateRight(grand);
            }
            else
            {
                RotateRight(parent);
                RotateLeft(grand);
            }
        }
    }
}
