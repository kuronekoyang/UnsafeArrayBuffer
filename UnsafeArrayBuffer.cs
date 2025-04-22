using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace kuro
{
    public struct UnsafeArrayBuffer<T> : IList<T>, IDisposable
    {
        private static readonly ArrayPool<T> s_pool = ArrayPool<T>.Shared;
        private T[] _buffer;
        private int _capacity;
        private int _length;


        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length <= 0;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _capacity;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public bool IsReadOnly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InternalBuffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => InternalBuffer[index] = value;
        }

        public T[] InternalBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer ?? Array.Empty<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan() => InternalBuffer.AsSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan(int start) => InternalBuffer.AsSpan().Slice(start);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan(int start, int length) => InternalBuffer.AsSpan().Slice(start, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(T item) => Array.IndexOf(InternalBuffer, item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, T item) => Insert(index, item, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, T item, int count)
        {
            if (count <= 0)
                return;
            if (index < 0 || index > _length)
                throw new ArgumentOutOfRangeException(nameof(index));

            var oldLength = _length;
            Resize(oldLength + count);

            if (oldLength > index)
                Array.Copy(_buffer, index, _buffer, index + count, oldLength - index);
            _buffer.AsSpan(index, count).Fill(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InsertRange(int index, ReadOnlySpan<T> items)
        {
            if (items.IsEmpty)
                return;
            if (index < 0 || index > _length)
                throw new ArgumentOutOfRangeException(nameof(index));

            var oldLength = _length;
            Resize(oldLength + items.Length);

            if (oldLength > index)
                Array.Copy(_buffer, index, _buffer, index + items.Length, oldLength - index);
            items.CopyTo(_buffer.AsSpan(index, items.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index) => RemoveRange(index, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int index, int count)
        {
            if (count <= 0)
                return;
            if (index < 0 || index >= _length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if ((index + count) > _length)
                throw new ArgumentOutOfRangeException(nameof(count));
            _length -= count;
            if (_length > index)
                Array.Copy(_buffer, index + count, _buffer, index, _length - index);
            _buffer.AsSpan(_length, count).Fill(default);
            SetBufferLength(_buffer, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<T> GetEnumerator() => new Enumerator(InternalBuffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(InternalBuffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item) => Add(item, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item, int count)
        {
            if (count <= 0)
                return;
            var oldLength = _length;
            Resize(oldLength + count);
            _buffer.AsSpan(oldLength, count).Fill(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ReadOnlySpan<T> items)
        {
            var oldLength = _length;
            Resize(oldLength + items.Length);
            items.CopyTo(_buffer.AsSpan(oldLength, items.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (_buffer != null)
            {
                if (_length > 0)
                    Array.Clear(_buffer, 0, _length);
                SetBufferLength(_buffer, _capacity);
                s_pool.Return(_buffer);
                _buffer = null;
            }

            _length = 0;
            _capacity = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => Array.IndexOf(InternalBuffer, item) != -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] array, int arrayIndex) => InternalBuffer.CopyTo(array, arrayIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index == -1)
                return false;
            RemoveAt(index);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Resize(int newSize)
        {
            if (newSize <= 0)
            {
                Clear();
            }
            else if (_buffer != null && newSize <= _capacity)
            {
                if (newSize < _length)
                {
                    Array.Clear(_buffer, newSize, _length - newSize);
                }

                // do nothing
                _length = newSize;
                SetBufferLength(_buffer, newSize);
            }
            else
            {
                var newBuffer = s_pool.Rent(newSize);
                var newCapacity = newBuffer.Length;
                if (_buffer != null)
                {
                    Array.Copy(_buffer, newBuffer, _length);
                    if (_length > 0)
                        Array.Clear(_buffer, 0, _length);
                    SetBufferLength(_buffer, _capacity);
                    s_pool.Return(_buffer);
                }

                _buffer = newBuffer;
                _capacity = newCapacity;
                _length = newSize;
                SetBufferLength(_buffer, newSize);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private class DummyArray
        {
            public IntPtr Bounds;
            public IntPtr Count;
            public byte Data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetBufferLength(T[] buffer, int length)
        {
            var dummy = UnsafeUtility.As<T[], DummyArray>(ref buffer);
            dummy.Count = new IntPtr(length);
        }

        public struct Enumerator : IEnumerator<T>
        {
            public int Pos;
            public T[] Array;

            public Enumerator(T[] array)
            {
                this.Pos = -1;
                this.Array = array;
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Pos >= 0 && Pos < Array.Length) ? Array[Pos] : default;
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.Current;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                this.Pos = -1;
                this.Array = System.Array.Empty<T>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (Pos < Array.Length)
                    Pos++;
                return Pos < Array.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                this.Pos = -1;
            }
        }
    }
}