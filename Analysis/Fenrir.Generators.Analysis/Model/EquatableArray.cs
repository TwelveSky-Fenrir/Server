using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Wraps an <see cref="ImmutableArray{T}" /> with real element-wise equality/hashing, for use on any
///     record (<see cref="TypeModel" />, <see cref="GeneratedTypeResult" />) that flows through an
///     <c>IIncrementalGenerator</c> pipeline stage.
/// </summary>
/// <remarks>
///     <see cref="ImmutableArray{T}" /> implements <see cref="IEquatable{T}" /> itself, but its
///     <c>Equals</c>/<c>GetHashCode</c> compare the underlying array <em>reference</em>, not its contents —
///     converting a model from <c>class</c> to <c>record</c> does nothing to fix this, since the compiler-
///     synthesized record equality just forwards to each member's own <c>Equals</c>. Every
///     <c>FieldScanner.Scan</c>/<c>TypeModelBuilder.Build*</c> pass rebuilds a brand-new backing array from
///     scratch even when its contents are byte-for-byte identical to the previous pass, so two structurally
///     identical <see cref="TypeModel" />/<see cref="GeneratedTypeResult" /> instances across consecutive
///     incremental passes would never compare equal with a raw <see cref="ImmutableArray{T}" /> member, and
///     Roslyn would never skip the (redundant) downstream recompute/re-emit — this is called out explicitly
///     in Roslyn's own incremental-generator cookbook ("Pipeline model design": wrap array-typed members with
///     value-equatable collections). Any record property that flows through the pipeline must use this
///     wrapper instead of a raw <see cref="ImmutableArray{T}" />.
/// </remarks>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    private ImmutableArray<T> ArrayOrEmpty => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    /// <summary>
    ///     Named to match <see cref="ImmutableArray{T}" />'s own <c>Length</c>, not <c>Count</c>, so call
    ///     sites (including C# property patterns like <c>AllowedStates.Length: > 0</c>) don't need to change.
    /// </summary>
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
