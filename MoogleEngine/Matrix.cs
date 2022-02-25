namespace MoogleEngine
{
    public class Matrix<T> : IEnumerable<T>
    {
        // NOTE: MATRIX USES (X; Y) COORDINATES, X BEING THE HORIZONTAL AXIS
        // (COLUMN) AND Y THE VERTICAL AXIS (ROW)

        // for performance reasons, matrix are stored as one-dimensional arrays
        private readonly T[] _items;

        // though for convinience can be accessed as two-dimensional arrays
        public T this[int x, int y]
        {
            get => _items[(Width * y) + x];
            set => _items[(Width * y) + x] = value;
        }

        public Matrix(T[,] items)
        {
            Width = items.GetLength(0);

            _items = new T[Width * items.GetLength(1)];

            // initialising the matrix in a one dimensional array in a convenient way

            for (int i = 0; i < _items.Length; i++)
            {
                int y = i / Width;
                int x = i - (y * Width);
                _items[i] = items[x, y];
            }
        }
        public Matrix(int width, int height)
        {
            Width = width;
            _items = new T[width * height];
        }

        // Only width is stored, height is obtained from the width
        public int Height => Width == 0 ? 0 : _items.Length / Width;
        public int Width { get; }

        public Vector<T> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Height)
            {
                throw new IndexOutOfRangeException($"{nameof(rowIndex)} must be greater than 0 and lower than the matrix height.");
            }

            int internalRowIndex = rowIndex * Width;

            // range operator goes brr~
            return new Vector<T>(_items[internalRowIndex..(internalRowIndex + Width)]);
        }
        public Vector<T> GetColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= Width)
            {
                throw new IndexOutOfRangeException($"{nameof(colIndex)} must be greater than 0 and lower than column length.");
            }

            Vector<T> col = new(Height);

            for (int i = 0; i < Height; i++)
            {
                col[i] = this[colIndex, i];
            }

            return col;
        }
        public Vector<T>[] GetAllRows()
        {
            Vector<T>[] rows = new Vector<T>[Height];
            for (int i = 0; i < Height; i++)
            {
                rows[i] = GetRow(i);
            }

            return rows;
        }
        public Vector<T>[] GetAllColumns()
        {
            Vector<T>[] cols = new Vector<T>[Width];
            for (int i = 0; i < Width; i++)
            {
                cols[i] = GetColumn(i);
            }

            return cols;
        }

        public override string ToString()
        {
            string result = "";
            for (int i = 0; i < _items.Length; i++)
            {
                if (i % Width == 0)
                {
                    result += "\n";
                }

                result += (_items[i]?.ToString() ?? "") + " ";
            }

            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)_items).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }
}