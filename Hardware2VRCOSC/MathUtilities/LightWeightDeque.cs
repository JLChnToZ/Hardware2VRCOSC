using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;

namespace MathUtilities {
    #region interfaces
    /// <summary>An <see cref="IEnumerable{T}"/> interface with <see cref="IEnumerable"/> (Non-generic) implemented.</summary>
    public interface IEnumerableImpl<out T> : IEnumerable<T> {
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>An <see cref="IEnumerator{T}"/> interface with <see cref="IEnumerator"/> (Non-generic) implemented.</summary>
    public interface IEnumeratorImpl<out T> : IEnumerator<T> {
        object? IEnumerator.Current => Current;
    }

    /// <summary>An enumerator that could be directly used in <c>foreach</c> loop.</summary>
    public interface IEnumerableEnumerator<out T> : IEnumerableImpl<T>, IEnumeratorImpl<T> {
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => this;

        void IDisposable.Dispose() { }
    }

    /// <summary>An <see cref="ICollection{T}"/> interface with <see cref="ICollection"/> (Non-generic) and <see cref="IReadOnlyCollection{T}"/> implemented.</summary>
    public interface ICollectionImpl<T> : IEnumerableImpl<T>, ICollection<T>, IReadOnlyCollection<T>, ICollection {
        int ICollection.Count => (this as ICollection<T>).Count;

        int IReadOnlyCollection<T>.Count => (this as ICollection<T>).Count;

        void ICollection.CopyTo(Array array, int offset) => CopyTo((T[])array, offset);
    }

    /// <summary>An <see cref="IList{T}"/> interface with <see cref="IList"/> (Non-generic) and <see cref="IReadOnlyList{T}"/> implemented.</summary>
    public interface IListImpl<T> : ICollectionImpl<T>, IList<T>, IReadOnlyList<T>, IList {
        bool IList.IsReadOnly => (this as IList<T>).IsReadOnly;

        T IReadOnlyList<T>.this[int index] => (this as IList<T>)[index];

        object? IList.this[int index] {
            get => (this as IList<T>)[index];
            set => (this as IList<T>)[index] = (T)value!;
        }
      
        bool ICollection<T>.Contains(T item) => IndexOf(item) >= 0;

        int IList.Add(object? value) {
            int count = (this as ICollection<T>).Count;
            Add((T)value!);
            return count;
        }

        bool IList.Contains(object? value) => value is T item && Contains(item);

        int IList.IndexOf(object? value) => value is T item ? IndexOf(item) : -1;

        void IList.Insert(int index, object? value) => Insert(index, (T)value!);

        void IList.Remove(object? value) {
            if (!Remove((T)value!)) throw new ArgumentException("The specified item is not found in the list.", nameof(value));
        }

        void IList.RemoveAt(int index) => (this as IList<T>).RemoveAt(index);
    }

    /// <summary>Abstracts a bidirectional deque.</summary>
    public interface IDeque<T> : IListImpl<T> {
        /// <summary>Gets or sets the capacity of the deque.</summary>
        int Capacity { get; set; }

        bool ICollection<T>.IsReadOnly => true;

        bool IList.IsFixedSize => false;

        /// <summary>Peeks the first added element without removing it.</summary>
        /// <returns>The first added element, or default value if the deque is empty.</returns>
        T? PeekFirst();

        /// <summary>Tries to peek the first added element without removing it.</summary>
        /// <param name="result">The first added element if succesful.</param>
        /// <returns><c>true</c> if an element was peeked; otherwise, <c>false</c>.</returns>
        bool TryPeekFirst([NotNullWhen(true)] out T? result);

        /// <summary>Peeks the last added element without removing it.</summary>
        /// <returns>The last added element, or default value if the deque is empty.</returns>
        T? PeekLast();

        /// <summary>Tries to peek the last added element without removing it.</summary>
        /// <param name="result">The last added element if succesful.</param>
        /// <returns><c>true</c> if an element was peeked; otherwise, <c>false</c>.</returns>
        bool TryPeekLast([NotNullWhen(true)] out T? result);

        /// <summary>Marks removal and returns the last added element.</summary>
        /// <returns>The last added element, or default value if the deque is empty.</returns>
        T? Pop();

        /// <summary>Marks removal and returns the last n added elements.</summary>
        /// <param name="count">The number of elements to mark removal and return.</param>
        /// <returns>
        /// The last n added elements in a span.
        /// The order is the last added element at the end of the span.
        /// </returns>
        ReadOnlySpan<T> Pop(int count);

        /// <summary>Tries to mark removal and return the last added element.</summary>
        /// <param name="result">The last added element if succesful.</param>
        /// <returns><c>true</c> if an element was removed and returned; otherwise, <c>false</c>.</returns>
        bool TryPop([NotNullWhen(true)] out T? result);

        /// <summary>Marks removal and discards the last n added elements.</summary>
        /// <param name="count">The number of elements to discard.</param>
        /// <returns>The number of elements actually discarded.</returns>
        int PopAndDiscard(int count = 1);

        /// <summary>Marks removal and returns the first added element.</summary>
        /// <returns>The first added element, or default value if the deque is empty.</returns>
        T? Dequeue();

        /// <summary>Marks removal and returns the first n added elements.</summary>
        /// <param name="count">The number of elements to mark removal and return.</param>
        /// <returns>
        /// The first n added elements in a span.
        /// The order is the first added element at the beginning of the span.
        /// </returns>
        ReadOnlySpan<T> Dequeue(int count);

        /// <summary>Tries to mark removal and return the first added element.</summary>
        /// <param name="result">The first added element if succesful.</param>
        /// <returns><c>true</c> if an element was removed and returned; otherwise, <c>false</c>.</returns>
        bool TryDequeue([NotNullWhen(true)] out T? result);

        /// <summary>Marks removal and discards the first n added elements.</summary>
        /// <param name="count">The number of elements to discard.</param>
        /// <returns>The number of elements actually discarded.</returns>
        int DequeueAndDiscard(int count = 1);

        /// <summary>Adds a range of elements to the deque.</summary>
        /// <param name="items">The range of elements to add onto the deque.</param>
        void Add(params T[] items) => Add(items.AsSpan());

        /// <summary>Adds a range of elements to the deque.</summary>
        /// <param name="items">The range of elements to add onto the deque.</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
        void Add(IEnumerable<T> items) {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var item in items) Add(item);
        }

        /// <summary>Adds a range of elements to the deque.</summary>
        /// <param name="items">The range of elements to add onto the deque.</param>
        void Add(ReadOnlySpan<T> items);

        /// <summary>Gets the enumerator enumerating the deque in LIFO (last in, first out) order.</summary>
        /// <returns>The enumerator.</returns>
        IEnumerableEnumerator<T> GetLIFOEnumerator();

        /// <summary>Gets the enumerator enumerating the deque in FIFO (first in, first out) order.</summary>
        /// <returns>The enumerator.</returns>
        IEnumerableEnumerator<T> GetFIFOEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetFIFOEnumerator();

        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        bool ICollection<T>.Remove(T item) => false;
    }
    #endregion

    /// <summary>
    /// Represents a deque with fast operations and low memory overhead.
    /// </summary>
    /// <typeparam name="T">The type of elements in the deque.</typeparam>
    /// <remarks>
    /// Due to its zero-overhead design of this deque implementation, there are some quirks of its behavior:
    /// <list type="bullet">
    /// <item><description>
    /// It is implemented as a struct with a internal buffer of elements along side the head and tail indices.
    /// </description></item>
    /// <item><description>
    /// It is not thread-safe, and thus should not be accessed concurrently from multiple threads.
    /// </description></item>
    /// <item><description>
    /// It supports populate multiple elements at once as a <see cref="ReadOnlySpan{T}"/>,
    /// but it is volatile and elements added to the deque afterwards will likely overwrite its contents.
    /// </description></item>
    /// <item><description>
    /// It will not empty the internal buffer slots when elements are removed.
    /// References to removed elements will be kept in the buffer until the deque is &quot;optimized&quot;, overwritten or resized.
    /// </description></item>
    /// <item><description>
    /// Enumerators are also implemented as structs and are volatile.
    /// It will be corrupted if elements are removed and then added to the deque while the enumerator is in use.
    /// </description></item>
    /// </list>
    /// </remarks>
    [Serializable]
    public struct LightWeightDeque<T> : IDeque<T>, IEquatable<LightWeightDeque<T>>, ICloneable, ISerializable {
        static ConditionalWeakTable<T[], object>? syncRoots;
        const int defaultCapacity = 4;
        int head, tail;
        bool isFull;
        T[]? buffer;

        public readonly T this[int index] {
            get {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return buffer![(head + index) % buffer.Length];
            }
            set {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                buffer![(head + index) % buffer.Length] = value;
            }
        }

        /// <summary><c>true</c> if the deque is empty; otherwise, <c>false</c>.</summary>
        [MemberNotNullWhen(false, nameof(buffer))]
        public readonly bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => head == tail && !isFull;
        }

        public readonly int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer == null ? 0 : CalcCountUnchecked();
        }

        public int Capacity {
            readonly get => buffer == null ? 0 : buffer.Length;
            set {
                if (value < 0) return;
                if (buffer == null) {
                    buffer = value > 0 ? new T[value] : null;
                    return;
                }
                int count = CalcCountUnchecked();
                value = Math.Max(count, value);
                if (value == buffer.Length) return;
                if (value == 0) {
                    buffer = null;
                    head = tail = 0;
                    isFull = false;
                    return;
                }
                buffer = CopyDefraged(new T[value]);
                head = 0;
                tail = count;
                isFull = tail == value;
            }
        }

        readonly bool ICollection.IsSynchronized => false;

        readonly object ICollection.SyncRoot {
            get {
                if (syncRoots == null) Interlocked.CompareExchange(ref syncRoots, new(), null);
                return buffer == null ? new object() : syncRoots.GetOrCreateValue(buffer);
            }
        }

        public LightWeightDeque(int capacity) : this() =>
            buffer = capacity > 0 ? new T[capacity] : null;

        public LightWeightDeque(IEnumerable<T> items) : this() {
            if (items == null) return;
            if (items is T[] array) {
                buffer = new T[NextPowerOf2(Math.Max(defaultCapacity, array.Length))];
                Array.Copy(array, buffer, array.Length);
                tail = buffer.Length;
                isFull = tail == head && tail > 0;
                return;
            }
            if (items is ICollection<T> collection) {
                int count = collection.Count;
                if (count <= 0) return;
                buffer = new T[NextPowerOf2(Math.Max(defaultCapacity, count))];
                collection.CopyTo(buffer, 0);
                tail = count;
                isFull = tail == head && tail > 0;
                return;
            }
            if (items is IReadOnlyCollection<T> readOnlyCollection) {
                int count = readOnlyCollection.Count;
                if (count <= 0) return;
                buffer = new T[NextPowerOf2(Math.Max(defaultCapacity, count))];
                foreach (var item in items) buffer[tail++] = item;
            }
            foreach (var item in items) Add(item);
        }

        public LightWeightDeque(ReadOnlySpan<T> items) :
            this(NextPowerOf2(Math.Max(defaultCapacity, items.Length))) {
            items.CopyTo(buffer);
            tail = items.Length;
            isFull = items.Length == buffer!.Length;
        }

        public LightWeightDeque(params T[] items) : this(items.AsSpan()) { }

        LightWeightDeque(SerializationInfo info, StreamingContext context) {
            if (info == null) throw new ArgumentNullException(nameof(info));
            buffer = info.GetValue(nameof(buffer), typeof(T[])) as T[];
            head = 0;
            tail = buffer?.Length ?? 0;
            isFull = buffer != null && head == tail;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T? PeekFirst() {
            TryPeekFirst(out T? result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T? PeekLast() {
            TryPeekLast(out T? result);
            return result;
        }

        public T? Pop() {
            var results = Pop(1);
            return results.IsEmpty ? default : results[0];
        }

        public bool TryPop([NotNullWhen(true)] out T? result) {
            var results = Pop(1);
            if (results.IsEmpty) {
                result = default;
                return false;
            }
            result = results[0]!;
            return true;
        }

        public T? Dequeue() {
            var results = Dequeue(1);
            return results.IsEmpty ? default : results[0];
        }

        public bool TryDequeue([NotNullWhen(true)] out T? result) {
            var results = Dequeue(1);
            if (results.IsEmpty) {
                result = default;
                return false;
            }
            result = results[0]!;
            return true;
        }

        public void Add(params T[] items) => Add(items.AsSpan());

        public void Add(IEnumerable<T> items) {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items is T[] array) {
                Add(array.AsSpan());
                return;
            }
            if (items is ICollection<T> collection) {
                int count = collection.Count;
                if (count <= 0) return;
                EnsureNewCapacity(count, true);
                collection.CopyTo(buffer, tail);
                tail = (tail + count) % buffer.Length;
                isFull = tail == head;
                return;
            }
            if (items is IReadOnlyCollection<T> readOnlyCollection) {
                int count = readOnlyCollection.Count;
                if (count <= 0) return;
                EnsureNewCapacity(count);
            }
            foreach (var item in items) Add(item);
        }

        /// <summary>Inserts an object at the top of the deque.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use Add instead.")]
        public void Push(T item) => Add(item);

        /// <summary>Inserts an object at the top of the deque.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use Add instead.")]
        public void Enqueue(T item) => Add(item);

        /// <summary>Defragments the internal buffer to reduce performance overhead.</summary>
        /// <returns><c>true</c> if the buffer was defragmented; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Defragment() {
            if (buffer == null || head < tail) return false;
            if (head == tail && !isFull) {
                head = tail = 0;
                return true;
            }
            buffer = CopyDefraged();
            head = 0;
            tail = CalcCountUnchecked();
            return true;
        }

        /// <summary>Defragments the internal buffer and cleans up references to removed elements.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DefragmentAndCleanup() {
            if (!Defragment()) CleanUpRemovedReferences();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            head = tail = 0;
            isFull = false;
        }

        public readonly bool TryPeekFirst([NotNullWhen(true)] out T? result) {
            if (IsEmpty) {
                result = default;
                return false;
            }
            result = buffer[head]!;
            return true;
        }

        public readonly bool TryPeekLast([NotNullWhen(true)] out T? result) {
            if (IsEmpty) {
                result = default;
                return false;
            }
            result = buffer![tail - 1]!;
            return true;
        }

        public void Add(T item) {
            EnsureNewCapacity();
            buffer[tail] = item;
            tail = (tail + 1) % buffer.Length;
            isFull = tail == head;
        }

        public void Add(ReadOnlySpan<T> items) {
            if (items.IsEmpty) return;
            EnsureNewCapacity(items.Length);
            if (head < tail)
                items.CopyTo(buffer.AsSpan(tail));
            else {
                int split = items.Length - (buffer.Length - tail);
                if (split > 0) {
                    items[..split].CopyTo(buffer.AsSpan(tail));
                    items[split..].CopyTo(buffer.AsSpan(0, head));
                } else
                    items.CopyTo(buffer.AsSpan(tail));
            }
            tail = (tail + items.Length) % buffer.Length;
            isFull = tail == head;
        }

        /// <inheritdoc cref="IDeque{T}.Pop(int)"/>
        /// <remarks>
        /// The returned <see cref="ReadOnlySpan{T}"/> directly references the internal buffer for fast access.
        /// It is violitle, most cases any elements added to the deque afterwards will likely overwrite the returned span.
        /// </remarks>
        public ReadOnlySpan<T> Pop(int count) {
            if (count <= 0 || IsEmpty) return default;
            int inCount = CalcCountUnchecked();
            if (count > inCount) count = inCount;
            int newTail = (tail + buffer.Length - count) % buffer.Length;
            if (newTail > tail && newTail < head) {
                Defragment();
                newTail = tail - count;
            }
            tail = newTail;
            isFull = false;
            return buffer.AsSpan(tail, count);
        }

        /// <inheritdoc cref="IDeque{T}.Dequeue(int)"/>
        /// <remarks>
        /// The returned <see cref="ReadOnlySpan{T}"/> directly references the internal buffer for fast access.
        /// It is violitle, most cases any elements added to the deque afterwards will likely overwrite the returned span.
        /// </remarks>
        public ReadOnlySpan<T> Dequeue(int count) {
            if (count <= 0 || IsEmpty) return default;
            int inCount = CalcCountUnchecked();
            if (count > inCount) count = inCount;
            int newHead = (head + count) % buffer.Length;
            if (newHead > tail && newHead < head) {
                Defragment();
                newHead = count;
            }
            var result = buffer.AsSpan(head, count);
            head = newHead;
            isFull = false;
            return result;
        }

        public int PopAndDiscard(int count = 1) {
            if (count <= 0 || IsEmpty) return 0;
            int inCount = CalcCountUnchecked();
            if (count > inCount) count = inCount;
            tail = (tail + buffer.Length - count) % buffer.Length;
            isFull = false;
            return count;
        }

        public int DequeueAndDiscard(int count = 1) {
            if (count <= 0 || IsEmpty) return 0;
            int inCount = CalcCountUnchecked();
            if (count > inCount) count = inCount;
            head = (head + count) % buffer.Length;
            isFull = false;
            return count;
        }

        /// <inheritdoc cref="IDeque{T}.GetLIFOEnumerator()"/>
        /// <remarks>
        /// The state of the enumerator will not affect the state of the deque,
        /// but attempting to remove and then add elements to the deque will corrupt the enumerator.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly LIFOEnumerator GetLIFOEnumerator() => new(this);

        /// <inheritdoc cref="IDeque{T}.GetFIFOEnumerator()"/>
        /// <remarks>
        /// The state of the enumerator will not affect the state of the deque,
        /// but attempting to remove and then add elements to the deque will corrupt the enumerator.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FIFOEnumerator GetFIFOEnumerator() => new(this);

        static int NextPowerOf2(int value) {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly int CalcCountUnchecked() =>
            isFull ? buffer!.Length : (tail + buffer!.Length - head) % buffer.Length;

        [MemberNotNull(nameof(buffer))]
        bool EnsureNewCapacity(int requestedItemCount = 1, bool noWrap = false) {
            int currentCount = Count;
            int requiredCapacity = NextPowerOf2(Math.Max(defaultCapacity, currentCount + requestedItemCount + 1));
            if (buffer == null) {
                buffer = new T[requiredCapacity];
                return true;
            }
            if (buffer.Length >= requiredCapacity &&
                (!noWrap || tail + requestedItemCount <= (head < tail ? buffer.Length : head)))
                return false;
            buffer = CopyDefraged(new T[requiredCapacity]);
            head = 0;
            tail = currentCount;
            isFull = currentCount == buffer.Length;
            return true;
        }

        readonly T[] CopyDefraged(T[]? dest = null, int offset = 0) {
            int count = CalcCountUnchecked();
            dest ??= new T[count];
            if (head < tail) {
                Array.Copy(buffer!, head, dest, offset, count);
            } else {
                int split = buffer!.Length - head;
                Array.Copy(buffer, head, dest, offset, split);
                Array.Copy(buffer, 0, dest, offset + split, tail);
            }
            return dest;
        }

        /// <summary>Cleans up references to removed elements.</summary>
        public readonly void CleanUpRemovedReferences() {
            if (isFull || buffer == null) return;
            if (head == tail) {
                Array.Clear(buffer, 0, buffer.Length);
                return;
            }
            if (head > tail) {
                Array.Clear(buffer, tail, head - tail);
                return;
            }
            Array.Clear(buffer, 0, head);
            Array.Clear(buffer, tail, buffer.Length - tail);
        }

        /// <summary>Creates a shallow copy of the deque.</summary>
        /// <returns>A shallow copy of the deque.</returns>
        /// <remarks>The internal buffer is ensured to be decoupled from the original deque.</remarks>
        public readonly LightWeightDeque<T> Clone() => buffer == null ? default : new() {
            buffer = CopyDefraged(),
            head = 0,
            tail = CalcCountUnchecked(),
            isFull = true,
        };

        public readonly bool Contains(T item) => buffer != null && !IsEmpty && (head < tail ?
            Array.IndexOf(buffer, item, head, tail - head) >= 0 : (
            Array.IndexOf(buffer, item, 0, tail) >= 0 ||
            Array.IndexOf(buffer, item, head, buffer.Length - head) >= 0
        ));

        public readonly int IndexOf(T item) {
            if (buffer == null || IsEmpty) return -1;
            int count = CalcCountUnchecked();
            int index;
            if (head >= tail) {
                index = Array.IndexOf(buffer, item, 0, tail);
                if (index >= 0) return index + buffer.Length - head;
            }
            index = Array.IndexOf(buffer, item, head, head < tail ? count : buffer.Length - head);
            return index >= 0 ? index - head : -1;
        }

        public readonly void CopyTo(T[] array, int arrayIndex) {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (IsEmpty) return;
            CopyDefraged(array, arrayIndex);
        }

        readonly IEnumerableEnumerator<T> IDeque<T>.GetLIFOEnumerator() => GetLIFOEnumerator();

        readonly IEnumerableEnumerator<T> IDeque<T>.GetFIFOEnumerator() => GetFIFOEnumerator();

        readonly object ICloneable.Clone() => Clone();

        public readonly bool Equals(LightWeightDeque<T> other) =>
            buffer == other.buffer && head == other.head &&
            tail == other.tail && isFull == other.isFull;

        public override readonly bool Equals(object? obj) =>
            obj is LightWeightDeque<T> other && Equals(other);

        public override readonly int GetHashCode() =>
            HashCode.Combine(buffer, head, tail, isFull);

        public override readonly string ToString() =>
            $"{nameof(LightWeightDeque<T>)}<{typeof(T).Name}>[{Count}]";

        readonly void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            if (info == null) throw new ArgumentNullException(nameof(info));
            info.AddValue(nameof(buffer), IsEmpty ? null : CopyDefraged(), typeof(T[]));
        }

        public static bool operator ==(LightWeightDeque<T> left, LightWeightDeque<T> right) => left.Equals(right);

        public static bool operator !=(LightWeightDeque<T> left, LightWeightDeque<T> right) => !left.Equals(right);

        /// <summary>Enumerates the deque in LIFO (last in, first out) order.</summary>
        /// <remarks>
        /// The state of the enumerator will not affect the state of the deque,
        /// but attempting to remove and then add elements to the deque will corrupt the enumerator.
        /// </remarks>
        public struct FIFOEnumerator : IEnumerableEnumerator<T> {
            readonly T[] buffer;
            readonly int tail;
            int head;

            public readonly T Current => buffer[head % buffer.Length];

            /// <summary>Initializes a new instance of the <see cref="FIFOEnumerator"/> struct.</summary>
            /// <param name="deque">The deque to enumerate.</param>
            public FIFOEnumerator(LightWeightDeque<T> deque) {
                buffer = deque.buffer!;
                head = deque.head - 1;
                tail = deque.tail + (deque.tail < deque.head || deque.isFull ? deque.buffer!.Length : 0);
            }

            public bool MoveNext() => ++head < tail;

            void IEnumerator.Reset() => throw new NotSupportedException();
        }

        /// <summary>Enumerates the deque in FIFO (first in, first out) order.</summary>
        /// <remarks>
        /// The state of the enumerator will not affect the state of the deque,
        /// but attempting to remove and then add elements to the deque will corrupt the enumerator.
        /// </remarks>
        public struct LIFOEnumerator : IEnumerableEnumerator<T> {
            readonly T[] buffer;
            readonly int head;
            int tail;

            public readonly T Current => buffer[tail % buffer.Length];

            /// <summary>Initializes a new instance of the <see cref="LIFOEnumerator"/> struct.</summary>
            /// <param name="deque">The deque to enumerate.</param>
            public LIFOEnumerator(LightWeightDeque<T> deque) {
                buffer = deque.buffer!;
                head = deque.head;
                tail = deque.tail + (deque.tail < deque.head || deque.isFull ? deque.buffer!.Length : 0);
            }

            public bool MoveNext() => --tail >= head;

            void IEnumerator.Reset() => throw new NotSupportedException();
        }
    }
}