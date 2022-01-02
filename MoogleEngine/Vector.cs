using System.Collections;

namespace MoogleEngine
{
    public class Vector<T> : IEnumerable<T>
    {
        internal readonly T[] _items;

        public T this[int i]
        {
            get => _items[i];
            set
            {
                _items[i] = value;
            }
        }
        public int Length => _items.Length;
        public Vector(params T[] items)
        {
            _items = items;
        }

        public static double DotProduct(Vector<double> lhs, Vector<double> rhs)
        {
            if (lhs.Length != rhs.Length)
                throw new ArgumentException($"{nameof(lhs)} and {nameof(rhs)} vectors length mismatch.");

            return lhs.Zip(rhs).Select(tuple => tuple.First * tuple.Second).Sum();
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