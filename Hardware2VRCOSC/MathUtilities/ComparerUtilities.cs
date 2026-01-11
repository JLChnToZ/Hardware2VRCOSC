using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MathUtilities {
    /// <summary>
    /// Provides utility methods for comparing values.
    /// </summary>
    public static class ComparerUtilities {
        /// <summary>
        /// Finds the minimum of two values using the specified comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values to compare.</typeparam>
        /// <param name="comparer">The comparer to use for comparison. Will fallback to default comparer if <c>null</c> is passed.</param>
        /// <param name="x">The first value to compare.</param>
        /// <param name="y">The second value to compare.</param>
        /// <returns>The smaller of the two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(this IComparer<T> comparer, T x, T y) =>
            (comparer ?? Comparer<T>.Default).Compare(x, y) < 0 ? x : y;

        /// <inheritdoc cref="Min{T}(IComparer{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(this IComparer<T> comparer, params T[] args) => Min(comparer, args.AsSpan());

        /// <summary>
        /// Finds the minimum value in a collection using the specified comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values to compare.</typeparam>
        /// <param name="comparer">The comparer to use for comparison. Will fallback to default comparer if <c>null</c> is passed.</param>
        /// <param name="args">The collection of values to compare.</param>
        /// <returns>The minimum value in the collection.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(this IComparer<T> comparer, ReadOnlySpan<T> args) {
            if (args.Length == 0) throw new ArgumentException("Cannot find min of an empty collection.", nameof(args));
            T min = args[0];
            comparer ??= Comparer<T>.Default;
            for (int i = 1; i < args.Length; i++)
                if (comparer.Compare(min, args[i]) > 0)
                    min = args[i];
            return min;
        }

        /// <inheritdoc cref="Min{T}(IComparer{T}, ReadOnlySpan{T})"/>
        /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(this IComparer<T> comparer, IEnumerable<T> args) {
            if (args == null) throw new ArgumentNullException(nameof(args));
            using var enumerator = args.GetEnumerator();
            if (!enumerator.MoveNext()) throw new ArgumentException("Cannot find min of an empty collection.", nameof(args));
            T min = enumerator.Current;
            comparer ??= Comparer<T>.Default;
            while (enumerator.MoveNext())
                if (comparer.Compare(min, enumerator.Current) > 0)
                    min = enumerator.Current;
            return min;
        }

        /// <summary>
        /// Finds the maximum of two values using the specified comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values to compare.</typeparam>
        /// <param name="comparer">The comparer to use for comparison. Will fallback to default comparer if <c>null</c> is passed.</param>
        /// <param name="x">The first value to compare.</param>
        /// <param name="y">The second value to compare.</param>
        /// <returns>The larger of the two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(this IComparer<T> comparer, T x, T y) =>
            (comparer ?? Comparer<T>.Default).Compare(x, y) > 0 ? x : y;

        /// <inheritdoc cref="Max{T}(IComparer{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(this IComparer<T> comparer, params T[] args) => Max(comparer, args.AsSpan());

        /// <summary>
        /// Finds the maximum value in a collection using the specified comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values to compare.</typeparam>
        /// <param name="comparer">The comparer to use for comparison. Will fallback to default comparer if <c>null</c> is passed.</param>
        /// <param name="args">The collection of values to compare.</param>
        /// <returns>The maximum value in the collection.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(this IComparer<T> comparer, ReadOnlySpan<T> args) {
            if (args.Length == 0) throw new ArgumentException("Cannot find max of an empty collection.", nameof(args));
            T max = args[0];
            comparer ??= Comparer<T>.Default;
            for (int i = 1; i < args.Length; i++)
                if (comparer.Compare(max, args[i]) < 0)
                    max = args[i];
            return max;
        }

        /// <inheritdoc cref="Max{T}(IComparer{T}, ReadOnlySpan{T})"/>
        /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(this IComparer<T> comparer, IEnumerable<T> args) {
            if (args == null) throw new ArgumentNullException(nameof(args));
            using var enumerator = args.GetEnumerator();
            if (!enumerator.MoveNext()) throw new ArgumentException("Cannot find max of an empty collection.", nameof(args));
            T max = enumerator.Current;
            comparer ??= Comparer<T>.Default;
            while (enumerator.MoveNext())
                if (comparer.Compare(max, enumerator.Current) < 0)
                    max = enumerator.Current;
            return max;
        }

        /// <summary>
        /// Finds the median value of three elements using the specified comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values to compare.</typeparam>
        /// <param name="comparer">The comparer to use for comparison. Will fallback to default comparer if <c>null</c> is passed.</param>
        /// <param name="a">The first value to compare.</param>
        /// <param name="b">The second value to compare.</param>
        /// <param name="c">The third value to compare.</param>
        /// <returns>The median value of the three.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Median<T>(this IComparer<T> comparer, T a, T b, T c) {
            comparer ??= Comparer<T>.Default;
            return Max(comparer, Min(comparer, a, b), Min(comparer, Max(comparer, a, b), c));
        }

        /// <inheritdoc cref="Median{T}(IComparer{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Median<T>(this IComparer<T> comparer, params T[] args) =>
            Median(comparer, args.AsSpan());

        /// <summary>
        /// Finds the median value of a collection using the specified comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values to compare.</typeparam>
        /// <param name="comparer">The comparer to use for comparison. Will fallback to default comparer if <c>null</c> is passed.</param>
        /// <param name="args">The collection of values to compare.</param>
        /// <returns>The median value of the collection.</returns>
        /// <exception cref="ArgumentException">Thrown when the count of elements is not odd.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Median<T>(this IComparer<T> comparer, ReadOnlySpan<T> args) {
            if (args.Length % 2 == 0) throw new ArgumentException("Cannot find median of an even-length collection.", nameof(args));
            comparer ??= Comparer<T>.Default;
            Span<bool> omit = stackalloc bool[args.Length];
            int remains = args.Length;
            while (remains > 1) {
                int minIndex = -1, maxIndex = -1;
                T minVal = default!, maxVal = default!;
                for (int i = 0; i < args.Length; i++) {
                    if (omit[i]) continue;
                    if (minIndex < 0 || comparer.Compare(args[i], minVal) < 0) {
                        minIndex = i;
                        minVal = args[i];
                    }
                    if (maxIndex < 0 || comparer.Compare(args[i], maxVal) > 0) {
                        maxIndex = i;
                        maxVal = args[i];
                    }
                }
                omit[minIndex] = true;
                omit[maxIndex] = true;
                remains -= 2;
            }
            for (int i = 0; i < args.Length; i++)
                if (!omit[i]) return args[i];
            throw new InvalidOperationException("Should not reach here.");
        }
    }
}