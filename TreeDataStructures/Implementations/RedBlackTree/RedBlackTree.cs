using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode) => FixInsert(newNode);

    public override bool Remove(TKey key)
    {
        var node = FindNode(key);
        if (node is null) return false;

        var removedColor = node.Color;
        RbNode<TKey, TValue>? fixNode, fixParent;

        if (node.Left is null)
        {
            fixNode = node.Right;
            fixParent = node.Parent;
            
            Transplant(node, node.Right);
        }
        else if (node.Right is null)
        {
            fixNode = node.Left;
            fixParent = node.Parent;
            
            Transplant(node, node.Left);
        }
        else
        {
            var successor = Minimum(node.Right);
            var successorParent = successor.Parent;
            removedColor = successor.Color;
            fixNode = successor.Right;

            if (successorParent != node)
            {
                Transplant(successor, successor.Right);
                
                successor.Right = node.Right;
                successor.Right?.Parent = successor;
            }

            Transplant(node, successor);
            
            successor.Left = node.Left;
            successor.Left?.Parent = successor;
            successor.Color = node.Color;

            fixParent = successorParent == node ? successor : successorParent;
        }

        node.Left = node.Right = node.Parent = null;
        
        Count--;

        if (removedColor == RbColor.Black)
        {
            FixDelete(fixNode, fixParent);
        }

        OnNodeRemoved(fixParent, fixNode);
        
        return true;
    }

    private void FixInsert(RbNode<TKey, TValue> node)
    {
        while (node.Parent is { Color: RbColor.Red } parent)
        {
            var grand = parent.Parent;
            if (grand is null) break;

            var isLeftParent = parent == grand.Left;
            var uncle = isLeftParent ? grand.Right : grand.Left;

            if (ColorOf(uncle) == RbColor.Red)
            {
                parent.Color = RbColor.Black;
                uncle?.Color = RbColor.Black;
                grand.Color = RbColor.Red;
                node = grand;
                
                continue;
            }

            var nodeIsInner = isLeftParent ? node == parent.Right : node == parent.Left;
            if (nodeIsInner)
            {
                node = parent;
                if (isLeftParent)
                {
                    RotateLeft(node);
                }
                else
                {
                    RotateRight(node);
                }

                parent = node.Parent;
                grand = parent?.Parent;
                if (parent is null || grand is null) break;
            }

            parent.Color = RbColor.Black;
            grand.Color = RbColor.Red;
            if (isLeftParent)
            {
                RotateRight(grand);
            }
            else
            {
                RotateLeft(grand);
            }
        }

        Root?.Color = RbColor.Black;
    }

    private void FixDelete(RbNode<TKey, TValue>? node, RbNode<TKey, TValue>? parent)
    {
        var current = node;
        var currentParent = parent;

        while (current != Root && ColorOf(current) == RbColor.Black)
        {
            if (currentParent is null) break;
            var isLeftChild = current == currentParent.Left;
            var sibling = isLeftChild ? currentParent.Right : currentParent.Left;

            if (ColorOf(sibling) == RbColor.Red)
            {
                sibling!.Color = RbColor.Black;
                currentParent.Color = RbColor.Red;
                if (isLeftChild)
                {
                    RotateLeft(currentParent);
                    sibling = currentParent.Right;
                }
                else
                {
                    RotateRight(currentParent);
                    sibling = currentParent.Left;
                }
            }

            var siblingLeftBlack = ColorOf(isLeftChild ? sibling?.Left : sibling?.Right) == RbColor.Black;
            var siblingRightBlack = ColorOf(isLeftChild ? sibling?.Right : sibling?.Left) == RbColor.Black;

            if (siblingLeftBlack && siblingRightBlack)
            {
                sibling?.Color = RbColor.Red;
                current = currentParent;
                currentParent = currentParent.Parent;
            }
            else
            {
                if (ColorOf(isLeftChild ? sibling?.Right : sibling?.Left) == RbColor.Black)
                {
                    if (isLeftChild)
                    {
                        sibling?.Left?.Color = RbColor.Black;
                        if (sibling != null)
                        {
                            sibling.Color = RbColor.Red;
                            RotateRight(sibling);
                        }

                        sibling = currentParent.Right;
                    }
                    else
                    {
                        sibling?.Right?.Color = RbColor.Black;
                        if (sibling != null)
                        {
                            sibling.Color = RbColor.Red;
                            RotateLeft(sibling);
                        }

                        sibling = currentParent.Left;
                    }
                }

                sibling?.Color = currentParent.Color;
                currentParent.Color = RbColor.Black;
                if (isLeftChild)
                {
                    sibling?.Right?.Color = RbColor.Black;
                    RotateLeft(currentParent);
                }
                else
                {
                    sibling?.Left?.Color = RbColor.Black;
                    RotateRight(currentParent);
                }

                current = Root;
                break;
            }
        }

        current?.Color = RbColor.Black;
    }

    private RbColor ColorOf(RbNode<TKey, TValue>? node) => node?.Color ?? RbColor.Black;
}