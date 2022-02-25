using System.Collections;

namespace MoogleEngine;

public class Vector<T> : IEnumerable<T>, IEnumerable
{
    internal readonly T[] _items;

    public T this[int i]
    {
        get => i < Length ?
                _items[i] :
                throw new IndexOutOfRangeException("Index out of the range of the vector's Length");
        set => _items[i] = i < Length ?
                value :
                throw new IndexOutOfRangeException("Index out of the range of the vector's Length");
    }
    public int Length => _items.Length;

    public Vector(T[] items)
    {
        _items = items;
    }
    public Vector(int length)
    {
        _items = new T[length];
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _items.GetEnumerator();
    }
}