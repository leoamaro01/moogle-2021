using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoogleEngine;

public class Matrix<T>
{
    private readonly T[] _items;
    private readonly int _matrixWidth;
    public T this[int x, int y]
    {
        get => _items[_matrixWidth * y + x];
        set
        {
            _items[_matrixWidth * y + x] = value;
        }
    }

    public Matrix(T[,] items)
    {
        _matrixWidth = items.GetLength(0);

        _items = new T[_matrixWidth * items.GetLength(1)];

        for (int i = 0; i < _items.Length; i++)
        {
            int y = i / _matrixWidth;
            int x = i - (y * _matrixWidth);
            _items[i] = items[x, y];
        }
    }
    public Matrix(int width, int height)
    {
        _matrixWidth = width;
        _items = new T[width * height];
    }

    public int Height => _items.Length / _matrixWidth;
    public int Width => _matrixWidth;

    public Vector<T> GetRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= Height)
            throw new IndexOutOfRangeException($"{nameof(rowIndex)} must be greater than 0 and lower than the matrix height.");

        int internalRowIndex = (rowIndex * _matrixWidth);

        return new Vector<T>(_items[internalRowIndex..(internalRowIndex + Width)]);
    }
    public Vector<T> GetColumn(int colIndex)
    {
        if (colIndex < 0 || colIndex >= Width)
            throw new IndexOutOfRangeException($"{nameof(colIndex)} must be greater than 0 and lower than column length.");

        Vector<T> col = new(Height);

        for (int i = 0; i < Height; i++)
            col[i] = this[colIndex, i];

        return col;
    }
    public Vector<T>[] GetAllRows()
    {
        Vector<T>[] rows = new Vector<T>[Height];
        for (int i = 0; i < Height; i++)
            rows[i] = GetRow(i);

        return rows;
    }
    public Vector<T>[] GetAllColumns()
    {
        Vector<T>[] cols = new Vector<T>[Width];
        for (int i = 0; i < Width; i++)
            cols[i] = GetColumn(i);

        return cols;
    }

    public override string ToString()
    {
        string result = "";
        for (int i = 0; i < _items.Length; i++)
        {
            if (i % Width == 0)
                result += "\n";

            result += (_items[i]?.ToString() ?? "") + " ";
        }

        return result;
    }
}