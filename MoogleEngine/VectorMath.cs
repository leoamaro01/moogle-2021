namespace MoogleEngine;

internal static class VectorMath
{
    // Computes cosine similarity between two vectors
    public static double GetCosineSimilarity(in Vector<double> lhs, in Vector<double> rhs, in double epsilon = 0)
    {
        double dot = DotProduct(lhs, rhs);
        if (dot <= epsilon) return 0;

        double lNorm = Norm(lhs);
        if (lNorm <= epsilon) return 0;

        double rNorm = Norm(rhs);
        if (rNorm <= epsilon) return 0;

        return dot / (lNorm * rNorm);
    }
    // Computes the dot product between two vectors
    public static double DotProduct(in Vector<double> lhs, in Vector<double> rhs)
    {
        if (lhs.Length != rhs.Length)
            throw new ArgumentException($"{nameof(lhs)} and {nameof(rhs)} vectors length mismatch.");

        double result = 0;
        for (int i = 0; i < lhs.Length; i++)
            result += lhs[i] * rhs[i];

        return result;
    }
    public static double Norm(in Vector<double> vector)
    {
        // The norm of a vector is the square root of the sum of the square of its elements
        return Math.Sqrt(DotProduct(vector, vector));
    }
}