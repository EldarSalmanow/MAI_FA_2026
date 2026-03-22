using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode) => FixInsert(newNode);

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
    }

    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        var removed = node;
        var removedOriginalColor = removed.Color;

        RbNode<TKey, TValue>? x;
        RbNode<TKey, TValue>? xParent;
        bool xIsLeftChild;

        if (node.Left == null)
        {
            x = node.Right;
            xParent = node.Parent;
            xIsLeftChild = x == null && xParent != null && ReferenceEquals(node, xParent.Left);

            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            x = node.Left;
            xParent = node.Parent;
            xIsLeftChild = x == null && xParent != null && ReferenceEquals(node, xParent.Left);

            Transplant(node, node.Left);
        }
        else
        {
            removed = Minimum(node.Right);
            removedOriginalColor = removed.Color;
            x = removed.Right;

            if (ReferenceEquals(removed.Parent, node))
            {
                xParent = removed;
                xIsLeftChild = false;
            }
            else
            {
                xParent = removed.Parent;
                xIsLeftChild = true;

                Transplant(removed, removed.Right);

                removed.Right = node.Right;
                removed.Right!.Parent = removed;
            }

            Transplant(node, removed);

            removed.Left = node.Left;
            removed.Left!.Parent = removed;
            removed.Color = node.Color;
        }

        if (removedOriginalColor == RbColor.Black)
        {
            FixDelete(
                x,
                x?.Parent ?? xParent,
                x != null ? x.IsLeftChild : xIsLeftChild
            );
        }

        if (Root != null)
        {
            Root.Color = RbColor.Black;
        }
    }

    private void FixInsert(RbNode<TKey, TValue> node)
    {
        var current = node;

        while (current.Parent is { Color: RbColor.Red } parent)
        {
            var grand = parent.Parent!;

            if (ReferenceEquals(parent, grand.Left))
            {
                var uncle = grand.Right;
                if (IsRed(uncle))
                {
                    parent.Color = RbColor.Black;
                    uncle!.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                    current = grand;

                    continue;
                }

                if (ReferenceEquals(current, parent.Right))
                {
                    current = parent;
                    RotateLeft(current);
                    parent = current.Parent!;
                    grand = parent.Parent!;
                }

                parent.Color = RbColor.Black;
                grand.Color = RbColor.Red;
                RotateRight(grand);
            }
            else
            {
                var uncle = grand.Left;
                if (IsRed(uncle))
                {
                    parent.Color = RbColor.Black;
                    uncle!.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                    current = grand;

                    continue;
                }

                if (ReferenceEquals(current, parent.Left))
                {
                    current = parent;
                    RotateRight(current);
                    parent = current.Parent!;
                    grand = parent.Parent!;
                }

                parent.Color = RbColor.Black;
                grand.Color = RbColor.Red;
                RotateLeft(grand);
            }
        }

        if (Root != null)
        {
            Root.Color = RbColor.Black;
        }
    }

    private void FixDelete(RbNode<TKey, TValue>? node, RbNode<TKey, TValue>? parent, bool isLeftChild)
    {
        var current = node;
        var currentParent = parent;
        var currentIsLeft = isLeftChild;

        while (!ReferenceEquals(current, Root) && IsBlack(current))
        {
            if (currentParent == null)
            {
                break;
            }

            var currentOnLeft = current != null
                ? ReferenceEquals(current, currentParent.Left)
                : currentIsLeft;

            var sibling = currentOnLeft ? currentParent.Right : currentParent.Left;

            if (IsRed(sibling))
            {
                sibling!.Color = RbColor.Black;
                currentParent.Color = RbColor.Red;

                if (currentOnLeft)
                {
                    RotateLeft(currentParent);
                }
                else
                {
                    RotateRight(currentParent);
                }

                sibling = currentOnLeft ? currentParent.Right : currentParent.Left;
            }

            var nearChild = currentOnLeft ? sibling?.Left : sibling?.Right;
            var farChild = currentOnLeft ? sibling?.Right : sibling?.Left;

            if (IsBlack(nearChild) && IsBlack(farChild))
            {
                if (sibling != null)
                {
                    sibling.Color = RbColor.Red;
                }

                current = currentParent;
                currentParent = current.Parent;
                currentIsLeft = currentParent != null && ReferenceEquals(current, currentParent.Left);

                continue;
            }

            if (IsBlack(farChild))
            {
                if (nearChild != null)
                {
                    nearChild.Color = RbColor.Black;
                }

                if (sibling != null)
                {
                    sibling.Color = RbColor.Red;

                    if (currentOnLeft)
                    {
                        RotateRight(sibling);
                    }
                    else
                    {
                        RotateLeft(sibling);
                    }
                }

                sibling = currentOnLeft ? currentParent.Right : currentParent.Left;
                farChild = currentOnLeft ? sibling?.Right : sibling?.Left;
            }

            if (sibling != null)
            {
                sibling.Color = currentParent.Color;
            }

            currentParent.Color = RbColor.Black;

            if (farChild != null)
            {
                farChild.Color = RbColor.Black;
            }

            if (currentOnLeft)
            {
                RotateLeft(currentParent);
            }
            else
            {
                RotateRight(currentParent);
            }

            current = Root;
        }

        if (current != null)
        {
            current.Color = RbColor.Black;
        }
    }

    private static bool IsRed(RbNode<TKey, TValue>? node) => node?.Color == RbColor.Red;

    private static bool IsBlack(RbNode<TKey, TValue>? node) => node == null || node.Color == RbColor.Black;
}