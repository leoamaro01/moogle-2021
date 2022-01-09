using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualBasic.CompilerServices;
using static MoogleEngine.Utils;
namespace MoogleEngine;


public static class Moogle
{
    static readonly string[] vocabulary;
    static readonly string[] corpusFiles;
    static readonly Matrix<double> weightedCorpus;
    static readonly Vector<double> corpusIDF;
    static Moogle()
    {
        Console.WriteLine("Starting...");
        Stopwatch stopwatch = new();
        stopwatch.Start();
        // Perform document Indexing and Weighting

        // First we define a placeholder for the vocabulary.
        List<string> tempVocabulary = new();

        // Then we get the base "moogle-2021" directory, where the "Content" directory is located
        DirectoryInfo baseDir = Directory.GetParent(Directory.GetCurrentDirectory())
                    ?? throw new IOException("Unexpected error, parent directory not found, are you starting"
                    + "Moogle Server from a non-standard location?");

        // Now we get all files from the "Content" folder, this will be the corpus.
        corpusFiles = Directory.GetFiles(Path.Join(baseDir.FullName, "Content"), "*.txt");

        Dictionary<int, int>[] rawFrequenciesDicts = new Dictionary<int, int>[corpusFiles.Length];

        // Now we all distinct words to the vocabulary.
        for (int i = 0; i < corpusFiles.Length; i++)
        {
            Console.WriteLine($"Reading... ({(float)i / corpusFiles.Length * 100}%)");

            rawFrequenciesDicts[i] = new();

            ForEachWordInFile(corpusFiles[i],
                       word =>
                       {
                           word = Regex.Replace(word, @"\W+", "");
                           if (!tempVocabulary.Contains(word))
                           {
                               tempVocabulary.Add(word);
                               rawFrequenciesDicts[i].Add(tempVocabulary.Count - 1, 1);
                           }
                           else
                           {
                               int termIndex = tempVocabulary.IndexOf(word);
                               if (rawFrequenciesDicts[i].ContainsKey(termIndex))
                                   rawFrequenciesDicts[i][termIndex]++;
                               else
                                   rawFrequenciesDicts[i].Add(termIndex, 1);
                           }
                       });
        }
        Console.WriteLine("Read all files in " + stopwatch.ElapsedMilliseconds + "ms");
        stopwatch.Restart();

        vocabulary = tempVocabulary.ToArray();

        // The raw corpus is a matrix that contains the frequency of every term in every document.
        Matrix<int> rawCorpus = new(vocabulary.Length, corpusFiles.Length);

        for (int i = 0; i < rawCorpus.Height; i++)
            for (int e = 0; e < rawCorpus.Width; e++)
            {
                if (rawFrequenciesDicts[i].ContainsKey(e))
                    rawCorpus[e, i] = rawFrequenciesDicts[i][e];
                else
                    rawCorpus[e, i] = 0;
            }

        Console.WriteLine("Created raw corpus in " + stopwatch.ElapsedMilliseconds + "ms");
        stopwatch.Restart();
        // Now we compute weights to every term in every document using TF-IDF weighting.
        corpusIDF = new Vector<double>(GetCorpusIDF(rawCorpus));
        weightedCorpus = GetCorpusTFIDF(rawCorpus, idf: corpusIDF);

        stopwatch.Stop();
        Console.WriteLine("Weighted corpus in " + stopwatch.ElapsedMilliseconds + "ms");
    }
    public static SearchResult Query(string query)
    {
        Console.WriteLine($"Searched {query}");

        string[] queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Where(w => vocabulary.Contains(w)).ToArray();

        if (queryTerms.Length == 0)
            return new SearchResult();

        System.Console.WriteLine("Split terms");

        Dictionary<string, int> rawQuery = new();

        foreach (string term in queryTerms)
        {
            int starCount = 0;
            foreach (char c in term)
                if (c == '*')
                    starCount++;
                else
                    break;

            if (rawQuery.ContainsKey(term))
                rawQuery[term] += 1 + starCount;
            else
                rawQuery.Add(term, 1 + starCount);
        }

        return new SearchResult(StrictQuery(rawQuery.ToArray()));
    }

    const double minSmilarity = 0;
    // Notes:
    // Strict query assumes all terms are in the vocabulary.
    private static SearchItem[] StrictQuery(KeyValuePair<string, int>[] terms)
    {
        System.Console.WriteLine("Invoked StrictQuery");

        List<string> vocabularyList = vocabulary.ToList();
        Vector<int> rawQuery = new(vocabulary.Length);

        foreach (var term in terms)
            rawQuery[vocabularyList.IndexOf(term.Key)] = term.Value;

        System.Console.WriteLine("Weighting query...");
        Vector<double> weightedQuery = new(GetDocumentTFIDF(rawQuery, idf: corpusIDF));

        Vector<double>[] documents = weightedCorpus.GetAllRows();
        double[] cosines = new double[weightedCorpus.Height];


        System.Console.WriteLine("Finding cosine similarity");
        for (int i = 0; i < cosines.Length; i++)
        {
            Console.WriteLine($"Finding cosine similarity for document {i} ({(float)i / cosines.Length * 100}%)");
            cosines[i] = VectorMath.GetCosineSimilarity(documents[i], weightedQuery);
        }

        List<SearchItem> result = new();

        for (int i = 0; i < cosines.Length; i++)
        {
            if (cosines[i] <= minSmilarity)
                continue;

            Console.WriteLine($"Document {i} similarity: {cosines[i]}");

            result.Add(new SearchItem(
                Path.GetFileNameWithoutExtension(corpusFiles[i]),
                GetSnippet(terms.Select(t => t.Key).ToArray(), corpusFiles[i]),
                (float)cosines[i]));
        }

        return result.ToArray();
    }
    private static string GetSnippet(string[] keywords, string documentPath)
    {
        return "TODO: Snippet creation.";
    }
}
