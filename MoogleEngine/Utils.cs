using System.IO;
namespace MoogleEngine;

internal static class Utils
{
    public static double GetTermFrequency(int termIndex, Vector<int> rawDocument)
    {
        if (termIndex < 0 || termIndex >= rawDocument.Length)
            throw new IndexOutOfRangeException($"{nameof(termIndex)} must be greater than 0 and lower than {nameof(rawDocument)}.Length.");

        if (rawDocument[termIndex] == 0) return 0;

        // Term frequency is calculated by dividing the frequency of the term by the sum of the frequencies of all other terms
        return (double)rawDocument[termIndex] / rawDocument.Max();
    }
    public static double[] GetDocumentTermFrequency(Vector<int> rawDocument)
    {
        double[] result = new double[rawDocument.Length];
        int maxFreq = rawDocument.Max();

        for (int i = 0; i < result.Length; i++)
        {
            if (rawDocument[i] == 0)
            {
                result[i] = 0;
                continue;
            }

            result[i] = (double)rawDocument[i] / maxFreq;
        }

        return result;
    }
    public static double GetInverseDocumentFrequency(int termIndex, Matrix<int> rawCorpus)
    {
        if (termIndex < 0 || termIndex >= rawCorpus.Width)
            throw new IndexOutOfRangeException($"{nameof(termIndex)} must be greater than 0 and lower than {nameof(rawCorpus)}.ColumnLength.");

        // Returns the logarithmically scaled inverse fraction of the documents that contain 
        // the word(obtained by dividing the total number of documents by the number of documents
        // containing the term, and then taking the logarithm of that quotient)

        int corpusLength = rawCorpus.Height;
        int documentsWithTerm = rawCorpus.GetColumn(termIndex).Count(v => v != 0);

        if (documentsWithTerm == 0)
            return 0;

        return Math.Log((double)corpusLength / documentsWithTerm);
    }
    public static double[] GetCorpusIDF(Matrix<int> rawCorpus)
    {
        double[] idfs = new double[rawCorpus.Width];

        for (int i = 0; i < idfs.Length; i++)
            idfs[i] = GetInverseDocumentFrequency(i, rawCorpus);

        return idfs;
    }
    public static double GetTFIDF(int termIndex, int documentIndex, Matrix<int> rawCorpus, double? idf = null)
        => GetTermFrequency(termIndex, rawCorpus.GetRow(documentIndex))
            * (idf ?? GetInverseDocumentFrequency(termIndex, rawCorpus));
    public static double[] GetDocumentTFIDF(int documentIndex, Matrix<int> rawCorpus, Vector<double>? idf = null)
        => GetDocumentTFIDF(rawCorpus.GetRow(documentIndex), rawCorpus, idf);
    public static double[] GetDocumentTFIDF(Vector<int> document, Matrix<int>? rawCorpus = null, Vector<double>? idf = null)
    {
        if (idf == null && rawCorpus == null)
            throw new ArgumentNullException(nameof(idf), "rawCorpus and idf can't be simultaneously null.");

        double[] result = new double[document.Length];

        double[] docTF = GetDocumentTermFrequency(document);

        if (rawCorpus != null)
            idf ??= new Vector<double>(GetCorpusIDF(rawCorpus));

        if (idf != null)
            for (int i = 0; i < result.Length; i++)
                result[i] = docTF[i] * idf[i];

        return result;
    }
    public static Matrix<double> GetCorpusTFIDF(Matrix<int> rawCorpus, Vector<double>? idf = null)
    {
        Matrix<double> result = new(rawCorpus.Width, rawCorpus.Height);

        double[] idfs = idf?.ToArray() ?? GetCorpusIDF(rawCorpus);

        for (int i = 0; i < rawCorpus.Height; i++)
        {
            double[] docTFs = GetDocumentTermFrequency(rawCorpus.GetRow(i));

            for (int e = 0; e < rawCorpus.Width; e++)
                result[e, i] = docTFs[e] * idfs[e];
        }

        return result;
    }
    internal static bool CharFilter(char c)
    => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c);
    internal static char CharMap(char c) => c.ToString().ToLower().Normalize(System.Text.NormalizationForm.FormD)[0];
    public static void ForEachFilteredParagraphInFile(string filePath, Action<string> action)
    => SeparateTextInFile(filePath, c => c == '\n', action, CharFilter, CharMap);
    public static void ForEachRawParagraphInFile(string filePath, Action<string> action)
    => SeparateTextInFile(filePath, c => c == '\n', action, c => true, c => c);
    public static void ForEachWordInFile(string filePath, Action<string> action)
    => SeparateTextInFile(filePath, char.IsWhiteSpace, action, CharFilter, CharMap);
    public static void SeparateTextInFile(
        string filePath, Func<char, bool> isSeparator, Action<string> action,
        Func<char, bool> filter, Func<char, char> map)
    {
        using StreamReader reader = new(filePath);

        string currentWord = "";
        while (!reader.EndOfStream)
        {
            char input = (char)reader.Read();
            char c = map(input);

            if (isSeparator(c))
            {
                currentWord = currentWord.Trim();

                if (currentWord != "")
                    action(currentWord);

                currentWord = "";
            }
            else if (filter(c))
                currentWord += c;
        }

        currentWord = currentWord.Trim();

        if (currentWord != "")
            action(currentWord);
    }
    public static void SeparateTextInString(
        string str, Func<char, bool> isSeparator, Action<string> action,
        Func<char, bool> filter, Func<char, char> map)
    {
        using StringReader reader = new(str);

        string currentWord = "";
        while (reader.Peek() != -1)
        {
            currentWord = currentWord.Trim();

            char input = (char)reader.Read();
            char c = map(input);

            if (isSeparator(c))
            {
                if (currentWord != "")
                    action(currentWord);

                currentWord = "";
            }
            else if (filter(c))
                currentWord += c;
        }

        currentWord = currentWord.Trim();

        if (currentWord != "")
            action(currentWord);
    }
}