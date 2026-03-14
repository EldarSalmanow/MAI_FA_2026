using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right)
        Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        return SplitInternal(root, key, includeEqualToLeft: true);
    }

    // Универсальный split:
    // includeEqualToLeft = true  -> Left: <= key, Right: > key
    // includeEqualToLeft = false -> Left: <  key, Right: >= key
    private (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right)
        SplitInternal(TreapNode<TKey, TValue>? root, TKey key, bool includeEqualToLeft)
    {
        if (root == null)
        {
            return (null, null);
        }

        int cmp = Comparer.Compare(root.Key, key);
        bool goesToLeftPart = cmp < 0 || (includeEqualToLeft && cmp == 0);

        if (goesToLeftPart)
        {
            var (leftPart, rightPart) = SplitInternal(root.Right, key, includeEqualToLeft);

            root.Right = leftPart;
            if (leftPart != null)
            {
                leftPart.Parent = root;
            }

            root.Parent = null;
            if (rightPart != null)
            {
                rightPart.Parent = null;
            }

            return (root, rightPart);
        }
        else
        {
            var (leftPart, rightPart) = SplitInternal(root.Left, key, includeEqualToLeft);

            root.Left = rightPart;
            if (rightPart != null)
            {
                rightPart.Parent = root;
            }

            root.Parent = null;
            if (leftPart != null)
            {
                leftPart.Parent = null;
            }

            return (leftPart, root);
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null)
        {
            if (right != null) right.Parent = null;
            return right;
        }

        if (right == null)
        {
            left.Parent = null;
            return left;
        }

        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            if (left.Right != null)
            {
                left.Right.Parent = left;
            }

            left.Parent = null;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            if (right.Left != null)
            {
                right.Left.Parent = right;
            }

            right.Parent = null;
            return right;
        }
    }

    public override void Add(TKey key, TValue value)
    {
        var existing = FindNode(key);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        var newNode = CreateNode(key, value);

        var (left, right) = Split(Root, key);
        Root = Merge(Merge(left, newNode), right);
        if (Root != null)
        {
            Root.Parent = null;
        }

        Count++;
        OnNodeAdded(newNode);
    }

    public override bool Remove(TKey key)
    {
        var nodeToRemove = FindNode(key);
        if (nodeToRemove == null)
        {
            return false;
        }

        var parentBefore = nodeToRemove.Parent;

        // Разбиваем на < key и >= key
        var (less, greaterOrEqual) = SplitInternal(Root, key, includeEqualToLeft: false);
        // Из >= key выделяем == key и > key
        var (equal, greater) = Split(greaterOrEqual, key);

        TreapNode<TKey, TValue>? replacement = null;
        if (equal != null)
        {
            // На случай расширения под дубликаты: склеиваем детей удаляемого фрагмента.
            replacement = Merge(equal.Left, equal.Right);
            if (replacement != null)
            {
                replacement.Parent = null;
            }
        }

        Root = Merge(Merge(less, replacement), greater);
        if (Root != null)
        {
            Root.Parent = null;
        }

        Count--;
        OnNodeRemoved(parentBefore, replacement);

        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new TreapNode<TKey, TValue>(key, value);
    }

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        // Для Treap вся балансировка выполняется напрямую в Add через Split/Merge.
    }

    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
        // Для Treap вся балансировка выполняется напрямую в Remove через Split/Merge.
    }
}
