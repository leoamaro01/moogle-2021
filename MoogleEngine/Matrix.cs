using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoogleEngine
{
    public class Matrix<T>
    {
        internal readonly T[,] _items;
        public T this[int x, int y]
        {
            get => _items[x, y];
            set
            {
                _items[x, y] = value;
            }
        }

        public Matrix(T[,] items)
        {
            _items = items;
        }

        public int RowLength => _items.GetLength(1);
        public int ColumnLength => _items.GetLength(0);

        public Vector<T> GetRow(int rowIndex)
        {
            Vector<T> row = new(new T[RowLength]);

            if (rowIndex < 0 || rowIndex >= row.Length)
                throw new IndexOutOfRangeException($"{nameof(rowIndex)} must be greater than 0 and lower than row length.");

            for (int i = 0; i < row.Length; i++)
                row[i] = _items[i, rowIndex];

            return row;
        }
        public Vector<T> GetColumn(int colIndex)
        {
            Vector<T> col = new(new T[ColumnLength]);

            if (colIndex < 0 || colIndex >= col.Length)
                throw new IndexOutOfRangeException($"{nameof(colIndex)} must be greater than 0 and lower than column length.");

            for (int i = 0; i < col.Length; i++)
                col[i] = _items[colIndex, i];

            return col;
        }
        public Vector<T>[] GetAllRows()
        {
            Vector<T>[] rows = new Vector<T>[RowLength];
            for (int i = 0; i < RowLength; i++)
                rows[i] = GetRow(i);

            return rows;
        }
        public Vector<T>[] GetAllColumns()
        {
            Vector<T>[] cols = new Vector<T>[ColumnLength];
            for (int i = 0; i < ColumnLength; i++)
                cols[i] = GetColumn(i);

            return cols;
        }
    }
}