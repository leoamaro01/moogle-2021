using System.Linq;
using System.Runtime.Serialization;
using System.Reflection.Metadata;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualBasic.CompilerServices;
using static MoogleEngine.Utils;
namespace MoogleEngine;


public static class Moogle
{
    const int CHARACTERS_PER_SNIPPET = 256;
    const int MIN_RESULTS = 10;
    const int MAX_LEVENSHTEIN_DEPTH = 2;
    const int LAST_SNIPPET_WORD_MAX_LENGTH = 16;
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
            Console.WriteLine($"Reading... ({(float)i / corpusFiles.Length * 100}%) {tempVocabulary.Count} words found.");

            rawFrequenciesDicts[i] = new();

            ForEachWordInFile(corpusFiles[i],
                       word =>
                       {
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
                       }
                       );
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
        char[] operators = { '*' };
        Console.WriteLine($"Searched {query}");

        List<string> queryTerms = new();

        SeparateTextInString(query,
            isSeparator: char.IsWhiteSpace,
            action: word =>
                    {
                        queryTerms.Add(word);
                    },
            filter: c => CharFilter(c) || operators.Contains(c),
            map: CharMap);
        // query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //                             .Where(w => vocabulary.Contains(w)).ToArray();

        if (queryTerms.Count == 0)
            return new SearchResult();

        System.Console.WriteLine("Split terms");

        Dictionary<string, int> rawQuery = new();
        Dictionary<string, int> originalQueryTerms = new();

        for (int i = 0; i < queryTerms.Count; i++)
        {
            string term = queryTerms[i];

            int starCount = 0;
            if (term.StartsWith('*'))
                foreach (char c in term)
                    if (c == '*')
                        starCount++;
                    else
                        break;

            // filter stars out of the term
            queryTerms[i] = term[starCount..];

            if (queryTerms[i] != "" && vocabulary.Contains(queryTerms[i]))
            {
                if (rawQuery.ContainsKey(queryTerms[i]))
                    rawQuery[queryTerms[i]] += 1 + starCount;
                else
                    rawQuery.Add(queryTerms[i], 1 + starCount);

                if (!originalQueryTerms.ContainsKey(queryTerms[i]))
                    originalQueryTerms.Add(queryTerms[i], 1 + starCount);
                else
                    originalQueryTerms[queryTerms[i]] = 1 + starCount;
            }
            else if (queryTerms[i] != "")
            {
                if (!originalQueryTerms.ContainsKey(queryTerms[i]))
                    originalQueryTerms.Add(queryTerms[i], 1 + starCount);
                else
                    originalQueryTerms[queryTerms[i]] = 1 + starCount;

                queryTerms.RemoveAt(i);
                i--;
                continue;
            }
            else
            {
                queryTerms.RemoveAt(i);
                i--;
                continue;
            }
        }
        var firstSearch = StrictQuery(rawQuery.ToArray());
        List<SearchItem> searchResult = new(firstSearch.Item1);
        // Dictionary of queries and their resulting cosines
        Dictionary<List<string>, double> bestCosines = new();
        bestCosines.Add(queryTerms, firstSearch.maxCosine);

        if (searchResult.Count < MIN_RESULTS)
        {
            List<string> queryKeys = new(originalQueryTerms.Keys);

            System.Console.WriteLine("Insufficient results, looking for query variations...");
            for (int i = 1; i <= MAX_LEVENSHTEIN_DEPTH; i++)
            {
                string[][] termsVariations = new string[queryKeys.Count][];

                for (int e = 0; e < termsVariations.Length; e++)
                {
                    termsVariations[e] = GetAllTermVariationsInVocabulary(queryKeys[e], vocabulary, i);

                    System.Console.WriteLine($"{termsVariations[e].Length} variations for term {e}");
                }

                System.Console.WriteLine("Calculating total query variations...");

                // Here we compute how many variations there are in total, we also store the total variations for 
                // each term in a one-dimensional array.
                int totalVariations = 1;
                int[] variationsLengths = new int[termsVariations.Length];
                for (int e = 0; e < termsVariations.Length; e++)
                {
                    variationsLengths[e] = termsVariations[e].Length;
                    totalVariations *= variationsLengths[e];
                }

                if (totalVariations == 1)
                {
                    System.Console.WriteLine("No variations found for depth = " + i);
                    continue;
                }

                int[][] allQueryVariations = GetAllArrayCombinations(variationsLengths);

                System.Console.WriteLine("Got all query variations, total " + allQueryVariations.Length);

                Dictionary<string, int>[] queryVariations = new Dictionary<string, int>[totalVariations];
                for (int e = 0; e < totalVariations; e++)
                {
                    queryVariations[e] = new Dictionary<string, int>();

                    for (int o = 0; o < queryKeys.Count; o++)
                        queryVariations[e].Add(termsVariations[o][allQueryVariations[e][o]], originalQueryTerms[queryKeys[o]]);
                }


                System.Console.WriteLine("Searching with variation queries...");
                List<(List<string> terms, (SearchItem[], double maxCosine))> variationSearches = new();
                for (int e = 0; e < queryVariations.Length; e++)
                {
                    List<string> variationTerms = new(queryVariations[e].Keys);

                    if (variationTerms.SequenceEqual(queryKeys))
                        continue;

                    variationSearches.Add((variationTerms, StrictQuery(queryVariations[e].ToArray())));
                }

                System.Console.WriteLine("Validating searches...");
                var validSearches = variationSearches.Where(r => r.Item2.maxCosine != 0).ToArray();

                validSearches = validSearches.OrderByDescending(s => s.Item2.maxCosine).ToArray();

                for (int e = 0; e < validSearches.Length; e++)
                {
                    bestCosines.Add(validSearches[e].terms, validSearches[e].Item2.maxCosine);

                    searchResult.AddRange(validSearches[e].Item2.Item1);
                }

                if (searchResult.Count >= MIN_RESULTS)
                    break;

                System.Console.WriteLine("Not enough results, going deeper...");
            }
        }

        double maxCosine = bestCosines.Values.Max();
        List<string> bestSearch = new();
        foreach (var search in bestCosines)
            if (search.Value == maxCosine)
            {
                bestSearch = search.Key;
                break;
            }

        string suggestion = "";
        if (bestSearch != queryTerms)
        {
            suggestion = string.Join(' ', bestSearch);
        }

        return new SearchResult(searchResult.ToArray(), suggestion);
    }
    const double MIN_SIMILARITY = 0.01;
    // Notes:
    // Strict query assumes all terms are in the vocabulary.
    private static (SearchItem[], double maxCosine) StrictQuery(KeyValuePair<string, int>[] terms)
    {
        Console.WriteLine("Invoked StrictQuery");

        List<string> vocabularyList = vocabulary.ToList();
        Vector<int> rawQuery = new(vocabulary.Length);

        foreach (var term in terms)
            rawQuery[vocabularyList.IndexOf(term.Key)] = term.Value;

        System.Console.WriteLine("Weighting query...");
        Vector<double> weightedQuery = new(GetDocumentTFIDF(rawQuery, idf: corpusIDF));

        Vector<double>[] documents = weightedCorpus.GetAllRows();
        double[] cosines = new double[weightedCorpus.Height];

        System.Console.WriteLine("Finding cosine similarity...");
        for (int i = 0; i < cosines.Length; i++)
        {
            cosines[i] = VectorMath.GetCosineSimilarity(documents[i], weightedQuery, MIN_SIMILARITY);
        }

        System.Console.WriteLine("Computing matches...");

        double maxCosine = cosines.Max();
        if (maxCosine <= MIN_SIMILARITY)
        {
            System.Console.WriteLine("Found 0 matches for strict search!");
            return (Array.Empty<SearchItem>(), 0);
        }

        List<SearchItem> result = new();

        for (int i = 0; i < cosines.Length; i++)
        {
            if (cosines[i] <= MIN_SIMILARITY)
                continue;

            result.Add(new SearchItem(
                Path.GetFileNameWithoutExtension(corpusFiles[i]),
                GetSnippet(weightedQuery, corpusFiles[i]),
                (float)cosines[i]));
        }

        System.Console.WriteLine($"Found {result.Count} matches for strict search!");
        return (result.OrderByDescending(item => item.Score).ToArray(), maxCosine);
    }
    private static string GetSnippet(Vector<double> weightedQuery, string documentPath)
    {
        List<string> docList = new();
        ForEachFilteredParagraphInFile(documentPath, p =>
        {
            if (p.Any(char.IsLetterOrDigit))
                docList.Add(p);
        });

        //Finding raw frequencies for each term in each paragraph
        Matrix<int> rawParagraphFrequencies = new(vocabulary.Length, docList.Count);
        for (int i = 0; i < rawParagraphFrequencies.Height; i++)
        {
            string[] splitParagraph = docList[i].Split((char[]?)null, options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int e = 0; e < splitParagraph.Length; e++)
                rawParagraphFrequencies[Array.IndexOf(vocabulary, splitParagraph[e]), i] += 1;
        }

        Matrix<double> weightedDocument = GetCorpusTFIDF(rawParagraphFrequencies);
        Vector<double>[] weightedRows = weightedDocument.GetAllRows();

        double[] docScores = new double[weightedRows.Length];
        for (int i = 0; i < docScores.Length; i++)
            docScores[i] = VectorMath.GetCosineSimilarity(weightedRows[i], weightedQuery);

        List<string> rawParagraphs = new();
        // ostritch philosophy was here
        ForEachRawParagraphInFile(documentPath, p =>
        {
            if (p.Any(char.IsLetterOrDigit))
                rawParagraphs.Add(p);
        });
        string bestParagraph = rawParagraphs[Array.IndexOf(docScores, docScores.Max())];

        if (bestParagraph.Length > CHARACTERS_PER_SNIPPET)
        {
            string cutParagraph = "";

            for (int i = 0; i < CHARACTERS_PER_SNIPPET; i++)
            {
                if (i > CHARACTERS_PER_SNIPPET - LAST_SNIPPET_WORD_MAX_LENGTH && bestParagraph[i] == ' ')
                    break;

                cutParagraph += bestParagraph[i];
            }

            cutParagraph += "(...)";

            return cutParagraph;
        }

        return bestParagraph;
    }
}
