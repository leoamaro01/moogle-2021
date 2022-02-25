namespace MoogleEngine;

// This class contains all utility methods used in the proyect.
internal static class Utils
{
    /// <summary>
    /// Computes the TF of an entire document.
    /// </summary>
    /// <param name="rawDocument">The raw frequencies of each term in the document.</param>
    /// <returns>An array containing the TF of each term in a document.</returns>
    public static double[] GetDocumentTermFrequency(Vector<int> rawDocument)
    {
        double[] result = new double[rawDocument.Length];
        int maxFreq = rawDocument.Max();

        // if the max frequency in the document is 0, simply return 0 for the whole array.
        if (maxFreq == 0)
            return result;

        for (int i = 0; i < result.Length; i++)
        {
            if (rawDocument[i] == 0)
            {
                result[i] = 0;
                continue;
            }

            // Augmented Term Frequency formula
            result[i] = 0.5 + 0.5 * ((double)rawDocument[i] / maxFreq);
        }

        return result;
    }
    /// <summary>
    /// Returns the logarithmically scaled inverse fraction of the documents that contain 
    /// the word (obtained by dividing the total number of documents by the number of documents
    /// containing the term, and then taking the logarithm of that quotient)
    /// </summary>
    /// <param name="termIndex">The index of the term in the corpus.</param>
    /// <param name="rawCorpus">The corpus matrix containing the raw count of each term in each document</param>
    /// <returns>Returns the IDF of a term in a corpus.</returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public static double GetInverseDocumentFrequency(int termIndex, Matrix<int> rawCorpus)
    {
        if (termIndex < 0 || termIndex >= rawCorpus.Width)
            throw new IndexOutOfRangeException($"{nameof(termIndex)} must be greater than 0 and lower than {nameof(rawCorpus)}.ColumnLength.");

        int corpusLength = rawCorpus.Height;
        int documentsWithTerm = rawCorpus.GetColumn(termIndex).Count(v => v != 0);

        if (documentsWithTerm == 0)
            return 0;

        return Math.Log((double)corpusLength / documentsWithTerm);
    }
    /// <summary>
    /// Computes the IDF of every term in a corpus.
    /// </summary>
    /// <param name="rawCorpus">The corpus matrix containing the raw count of each term in each document</param>
    /// <returns>An array containing the IDF of each term.</returns>
    public static double[] GetCorpusIDF(Matrix<int> rawCorpus)
    {
        double[] idfs = new double[rawCorpus.Width];

        for (int i = 0; i < idfs.Length; i++)
            idfs[i] = GetInverseDocumentFrequency(i, rawCorpus);

        return idfs;
    }
    /// <summary>
    /// Computes the TF-IDF of a single document in a corpus.<br>
    /// Note: `document` and `rawCorpus` can't be simultaneously null.
    /// </summary>
    /// <param name="document">The vector containing the raw frequencies of each term in a document</param>
    /// <param name="rawCorpus">The corpus matrix containing the raw count of each term in each document</param>
    /// <param name="idf">The IDF vector containing the IDFs of each term in the corpus.</param>
    /// <returns>An array containing the TF-IDF of each term in a document.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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
    /// <summary>
    /// Computes the TF-IDF of every term in every document in a corpus.
    /// </summary>
    /// <param name="rawCorpus">The corpus matrix containing the raw count of each term in each document</param>
    /// <param name="idf">The IDF vector containing the IDFs of each term in the corpus.</param>
    /// <returns>A matrix with the TF-IDF of each term in each document in the corpus.</returns>
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

    // Standard Filter applied to characters in Filtered reads
    internal static bool StandardCharFilter(char c)
    => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c);
    // Standard Map applied to characters (used for normalization (elimination of accents and such))
    internal static char StandardCharMap(char c)
     => c.ToString().ToLower().Normalize(System.Text.NormalizationForm.FormD)[0];
    // Make an action on each filtered paragraph in a file.
    public static void ForEachFilteredParagraphInFile(string filePath, Action<string> action)
    => SeparateTextInFile(filePath, c => c == '\n', action, StandardCharFilter, StandardCharMap);
    // Make an action on each unfiltered paragraph in a file.
    public static void ForEachRawParagraphInFile(string filePath, Action<string> action)
    => SeparateTextInFile(filePath, c => c == '\n', action, c => true, c => c);
    // Make an action on each filtered word in a file.
    public static void ForEachWordInFile(string filePath, Action<string> action)
    => SeparateTextInFile(filePath, char.IsWhiteSpace, action, StandardCharFilter, StandardCharMap);
    // Make an action on parts of a file, defined by an isSeparator function, applying
    // a filter and a map to each character.
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
    // Same as SeparateTextInFile but in a string.
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

    /// <summary>
    /// Computes the minimal distance between a series of terms in a file.
    /// </summary>
    /// <param name="documentPath">File path.</param>
    /// <returns>An Int32 that represents the minimal distance between the terms.</returns>
    public static int GetMinDistanceBetweenTerms(string documentPath, string[] terms)
    {
        int[][] positions = GetAllTermsPositionsInDocument(documentPath, terms);

        int foundTerms = positions.Count(p => p.Length != 0);

        // if less than two terms where found in a document, 
        // finding the distance is useless and error-prone
        if (foundTerms < 2)
            return 0;

        return GetShortestPathInIndexArray(positions, 0, -1, -1);
    }
    private static int GetShortestPathInIndexArray(in int[][] indexArray, int depth, int max, int min)
    {
        // This recursive method computes every possible path in a positional array
        // and returns the shortest path length (obtained by finding the highest and lowest
        // positions in the array, and finding the distance between them)

        if (depth >= indexArray.Length)
            return max - min;

        if (indexArray[depth].Length == 0)
            return GetShortestPathInIndexArray(indexArray, depth + 1, max, min);

        int[] distances = new int[indexArray[depth].Length];
        for (int i = 0; i < distances.Length; i++)
            // for each distance in the current depth, get the shortest path from that distance
            // to the next depths, passing as max index the max between the previous max or the
            // current position, and a similar process is used for passing the min parameter.
            distances[i] = GetShortestPathInIndexArray(indexArray, depth + 1,
                            max >= 0 ? Math.Max(max, indexArray[depth][i]) : indexArray[depth][i],
                            min >= 0 ? Math.Min(min, indexArray[depth][i]) : indexArray[depth][i]);

        return distances.Min();
    }
    private static int[][] GetAllTermsPositionsInDocument(string documentPath, string[] terms)
    {
        // this method finds every position in a document of each term in an array.
        List<int>[] result = new List<int>[terms.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = new();

        int wordIndex = 0;
        ForEachWordInFile(documentPath, w =>
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (terms[i] == w)
                {
                    result[i] ??= new();
                    result[i].Add(wordIndex);
                    break;
                }
            }

            wordIndex++;
        });

        return result.Select(l => l.ToArray()).ToArray();
    }
    public static string[] GetAllTermVariationsInVocabulary(string term, string[] vocabulary, int distance)
    {
        // This method finds every variation of a term in a vocabulary, up to a maximal Levenshtein distance
        List<string> variations = new();
        foreach (string word in vocabulary)
            if (CalculateDLDistance(term, word) <= distance)
                variations.Add(word);

        return variations.ToArray();
    }
    public static int[][] GetAllArrayCombinations(in int[] lengths)
    {
        // By using an array containing the lengths of other arrays, this method
        // returns each possible index combination of said arrays

        int totalLength = 1;
        foreach (int length in lengths)
            totalLength *= length;

        List<int[]> result = new();

        GetAllArrayCombinations(lengths, new List<int>(), ref result);

        return result.ToArray();
    }
    private static void GetAllArrayCombinations(in int[] lengths, List<int> previousTerms, ref List<int[]> result)
    {
        // This method recursively computes every index combination of several arrays,
        // which lengths are stored in the lengths array.

        int n = previousTerms.Count;

        // wrap things up if we are at the last term.
        if (n == lengths.Length - 1)
        {
            int[] value = new int[lengths.Length];
            previousTerms.CopyTo(value);

            for (int i = 0; i < lengths[n]; i++)
            {
                value[n] = i;
                result.Add((int[])value.Clone());
            }
            return;
        }
        // if we aren't at the last term, find all combinations for the next terms
        else
        {
            for (int i = 0; i < lengths[n]; i++)
            {
                List<int> nextTerms = new(previousTerms);
                nextTerms.Add(i);
                GetAllArrayCombinations(lengths, nextTerms, ref result);
            }
        }
    }

    public static int CalculateDLDistance(string a, string b)
        => CalculateDLDistance(a, b, a.Length - 1, b.Length - 1);

    // Method for calculating Damerau–Levenshtein distance between two strings (a & b)
    // i indicates the 0-indexed length of the current substring of a, and j of b.
    private static int CalculateDLDistance(string a, string b, int i, int j)
    {
        // if both substrings still have valid lengths
        if (i > -1 && j > -1)
        {
            // if there is a swap between this character and the next, it counts as a single operation in Damerau-Levenshtein.
            if (i > 0 && j > 0 && a[i] == b[j - 1] && a[i - 1] == b[j])
                return CalculateDLDistance(a, b, i - 2, j - 2) + 1;
            // if there isn't a swap, and the current characters are different, then a substitution must be made.
            else
                return CalculateDLDistance(a, b, i - 1, j - 1) + (a[i] == b[j] ? 0 : 1);
        }
        // this happens if a is longer than b
        else if (i > -1)
            return CalculateDLDistance(a, b, i - 1, j) + 1;
        // this happens if b is longer than a
        else if (j > -1)
            return CalculateDLDistance(a, b, i, j - 1) + 1;
        // base case
        else
            return 0;
    }
}