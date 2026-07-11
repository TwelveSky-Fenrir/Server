using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Fenrir.Generators.Analysis.Model;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    private ImmutableArray<T> ArrayOrEmpty => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

        public int Length => ArrayOrEmpty.Length;

    int IReadOnlyCollection<T>.Count => Length;

    public T this[int index] => ArrayOrEmpty[index];

    public bool Equals(EquatableArray<T> other)
    {
        var mine = ArrayOrEmpty;
        var theirs = other.ArrayOrEmpty;

        if (mine.Length != theirs.Length)
            return false;

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < mine.Length; i++)
            if (!comparer.Equals(mine[i], theirs[i]))
                return false;

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in ArrayOrEmpty)
                hash = hash * 31 + (item is null ? 0 : item.GetHashCode());
            return hash;
        }
    }

    public ImmutableArray<T>.Enumerator GetEnumerator()
    {
        return ArrayOrEmpty.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)ArrayOrEmpty).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)ArrayOrEmpty).GetEnumerator();
    }

    public static implicit operator EquatableArray<T>(ImmutableArray<T> array)
    {
        return new EquatableArray<T>(array);
    }

    public static implicit operator ImmutableArray<T>(EquatableArray<T> array)
    {
        return array.ArrayOrEmpty;
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}
