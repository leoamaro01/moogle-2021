using System.Collections;

namespace MoogleEngine
{
    public class Vector<T> : IEnumerable<T>
    {
        internal readonly T[] _items;

        public T this[int i]
        {
            get => _items[i];
            set => _items[i] = value;
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
}