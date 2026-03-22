using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        RebalanceUpward(newNode.Parent);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
    }

    protected override void RemoveNode(AvlNode<TKey, TValue> node)
    {
        var start1 = node.Parent;
        var successor = node.Left != null && node.Right != null ? Minimum(node.Right) : null;
        var start2 = successor?.Parent != node ? successor?.Parent : null;

        base.RemoveNode(node);

        RebalanceUpward(start1);

        if (start2 != null && !ReferenceEquals(start2, start1))
        {
            RebalanceUpward(start2);
        }

        if (successor != null &&
            !ReferenceEquals(successor, start1) &&
            !ReferenceEquals(successor, start2))
        {
            RebalanceUpward(successor);
        }
    }

    private void RebalanceUpward(AvlNode<TKey, TValue>? start)
    {
        var current = start;

        while (current != null)
        {
            UpdateHeight(current);
            var balance = BalanceFactor(current);

            if (balance > 1)
            {
                var left = current.Left!;
                if (BalanceFactor(left) < 0)
                {
                    RotateLeft(left);
                    UpdateHeight(left);
                    UpdateHeight(left.Parent!);
                }

                var oldRoot = current;
                RotateRight(oldRoot);

                UpdateHeight(oldRoot);
                if (oldRoot.Parent != null)
                {
                    UpdateHeight(oldRoot.Parent);
                    current = oldRoot.Parent.Parent;
                }
                else
                {
                    current = null;
                }
            }
            else if (balance < -1)
            {
                var right = current.Right!;
                if (BalanceFactor(right) > 0)
                {
                    RotateRight(right);
                    UpdateHeight(right);
                    UpdateHeight(right.Parent!);
                }

                var oldRoot = current;
                RotateLeft(oldRoot);

                UpdateHeight(oldRoot);
                if (oldRoot.Parent != null)
                {
                    UpdateHeight(oldRoot.Parent);
                    current = oldRoot.Parent.Parent;
                }
                else
                {
                    current = null;
                }
            }
            else
            {
                current = current.Parent;
            }
        }
    }

    private static int Height(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private static void UpdateHeight(AvlNode<TKey, TValue> node)
    {
        node.Height = Math.Max(Height(node.Left), Height(node.Right)) + 1;
    }

    private static int BalanceFactor(AvlNode<TKey, TValue> node)
    {
        return Height(node.Left) - Height(node.Right);
    }
}
