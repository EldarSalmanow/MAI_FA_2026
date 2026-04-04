using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode) => FixInsert(newNode);

    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        var removedColor = node.Color;
        RbNode<TKey, TValue>? fixNode, fixParent;
        bool fixNodeIsLeft;

        if (node.Left == null || node.Right == null)
        {
            fixNode = node.Left ?? node.Right;
            fixParent = node.Parent;
            fixNodeIsLeft = node.IsLeftChild;
            
            Transplant(node, fixNode);
        }
        else
        {
            var successor = Minimum(node.Right);
            
            removedColor = successor.Color;
            fixNode = successor.Right;
            fixParent = successor.Parent == node ? successor : successor.Parent;
            fixNodeIsLeft = successor.IsLeftChild;

            if (successor.Parent != node)
            {
                Transplant(successor, successor.Right);
                
                successor.Right = node.Right;
                successor.Right.Parent = successor;
            }

            Transplant(node, successor);
            
            successor.Left = node.Left;
            successor.Left.Parent = successor;
            successor.Color = node.Color;
        }

        node.Left = node.Right = node.Parent = null; 

        if (removedColor == RbColor.Black)
        {
            FixDelete(fixNode, fixParent, fixNodeIsLeft);
        }

        OnNodeRemoved(fixParent, fixNode);
    }

    private void FixInsert(RbNode<TKey, TValue> node)
    {
        while (node.Parent is { Color: RbColor.Red } parent)
        {
            var grand = parent.Parent;
            
            if (grand is null)
            {
                break;
            }

            var isLeft = parent == grand.Left;
            var uncle = isLeft ? grand.Right : grand.Left;

            if (ColorOf(uncle) == RbColor.Red)
            {
                parent.Color = RbColor.Black;
                uncle!.Color = RbColor.Black;
                grand.Color = RbColor.Red;
                
                node = grand;
            }
            else
            {
                if (node == (isLeft ? parent.Right : parent.Left))
                {
                    node = parent;
                    
                    if (isLeft)
                    {
                        RotateLeft(node);
                    }
                    else
                    {
                        RotateRight(node);
                    }
                    
                    parent = node.Parent!;
                    grand = parent.Parent!;
                }

                parent.Color = RbColor.Black;
                grand.Color = RbColor.Red;

                if (isLeft)
                {
                    RotateRight(grand);
                }
                else
                {
                    RotateLeft(grand);
                }
            }
        }

        Root?.Color = RbColor.Black;
    }

    private void FixDelete(RbNode<TKey, TValue>? node, RbNode<TKey, TValue>? parent, bool isLeft)
    {
        var current = node;

        while (current != Root && ColorOf(current) == RbColor.Black)
        {
            if (parent is null) break;

            var sibling = isLeft ? parent.Right : parent.Left;

            if (ColorOf(sibling) == RbColor.Red)
            {
                sibling!.Color = RbColor.Black;
                parent.Color = RbColor.Red;
                
                if (isLeft)
                {
                    RotateLeft(parent);
                }
                else
                {
                    RotateRight(parent);
                }
                
                sibling = isLeft ? parent.Right : parent.Left;
            }

            if (ColorOf(sibling?.Left) == RbColor.Black 
             && ColorOf(sibling?.Right) == RbColor.Black)
            {
                sibling?.Color = RbColor.Red;
                current = parent;
                parent = current.Parent;
                
                if (parent != null) isLeft = current.IsLeftChild; 
            }
            else
            {
                if (ColorOf(isLeft ? sibling?.Right : sibling?.Left) == RbColor.Black)
                {
                    if (sibling != null)
                    {
                        var inner = isLeft ? sibling.Left : sibling.Right;
                        inner?.Color = RbColor.Black;

                        sibling.Color = RbColor.Red;
                        if (isLeft) RotateRight(sibling); else RotateLeft(sibling);
                    }
                    sibling = isLeft ? parent.Right : parent.Left;
                }

                if (sibling != null)
                {
                    sibling.Color = parent.Color;
                    
                    var outer = isLeft ? sibling.Right : sibling.Left;
                    
                    outer?.Color = RbColor.Black;
                }
                
                parent.Color = RbColor.Black;
                if (isLeft) RotateLeft(parent); else RotateRight(parent);

                current = Root;
                
                break;
            }
        }

        current?.Color = RbColor.Black;
    }

    private RbColor ColorOf(RbNode<TKey, TValue>? node) => node?.Color ?? RbColor.Black;
}