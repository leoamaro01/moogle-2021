using System.Diagnostics;
using static MoogleEngine.Utils;
namespace MoogleEngine;


public static class Moogle
{
    // Max characters in a piece of snippet
    const int CHARACTERS_PER_SNIPPET = 256;
    // min results in a search before trying alternative searches
    const int MIN_RESULTS = 16;
    // max depth to look for alternative searches if not enough results are found
    const int MAX_LEVENSHTEIN_DEPTH = 2;

    // cosine values below this will be considered irrelevant and should be ignored
    const double MIN_SIMILARITY = 0.01;

    // we always try to include the last word in a snippet even if it passes the CHARACTERS_PER_SNIPPET, up to a max length
    const int LAST_SNIPPET_WORD_MAX_LENGTH = 16;

    // the length in characters of snippet paragraphs are expected to have, so if the snippet is short
    // by more than this amount, more paragraphs may be added.
    const int STANDARD_PARAGRAPH_LENGTH = 64;

    public static async Task<SearchResult> Query(string query, SearchEngineData engineData)
    {
        // base data gathered from the engineData parameter to be used in search
        string[] vocabulary = engineData.vocabulary;
        string[] corpusFiles = engineData.corpusFiles;
        Matrix<double> weightedCorpus = engineData.weightedCorpus;
        Vector<double> corpusIDF = engineData.corpusIDF;

        // This array contains operators to be allowed in the query, apart from the usual numbers and letters
        char[] operators = { '*', '!', '^', '~' };
        Console.WriteLine($"Searched {query}");

        List<string> queryTerms = new();

        // This separates and filters terms in the query
        SeparateTextInString(query,
            isSeparator: char.IsWhiteSpace,
            action: word =>
                    {
                        queryTerms.Add(word);
                    },
            filter: c => StandardCharFilter(c) || operators.Contains(c),
            map: StandardCharMap);

        // if our query is not valid, return an empty result
        if (queryTerms.Count == 0)
            return new SearchResult();

        // raw query will contain all terms from the query that appear in the vocabulary, with correct frequencies
        Dictionary<string, int> rawQuery = new();
        // original query will contain even terms which arent in the vocabulary, to be used if not enough results
        // are gathered from the regular query
        Dictionary<string, int> originalQueryTerms = new();
        // this list contains the terms that must *not* appear in any returned document.
        List<string> exceptTerms = new();
        // this list contains the terms that *must* appear in any returned document.
        List<string> mandatoryTerms = new();

        // this list of lists contains all tilde-marked sequences.
        List<List<string>> nearbySequences = new();
        // this bool will mark when we are on a roll of characters marked by a tilde
        bool onTildeStreak = false;
        // this marks if the last term was a tilde, if it wasn't and we find another non-tilde term, the streak will end. 
        bool lastTermWasTilde = false;
        for (int i = 0; i < queryTerms.Count; i++)
        {
            string term = queryTerms[i];

            // the tilde operator tells the engine to give higher importance to documents where the
            // surrounding terms are closer 
            if (term.StartsWith('~'))
            {
                if (lastTermWasTilde || i == 0 || i == queryTerms.Count - 1)
                {
                    // if we see 2 tildes in a row or the tilde is the first or last character 
                    // in a query, simply ignore it.
                    queryTerms.RemoveAt(i);
                    i--;
                    continue;
                }
                lastTermWasTilde = true;

                // onTildeStreak is used for grouping several tilde-separated terms
                if (!onTildeStreak)
                {
                    // if we are starting a new streak, add the term before the tilde to the streak
                    onTildeStreak = true;
                    nearbySequences.Add(new List<string>()
                    {
                        queryTerms[i -1]
                    });
                }

                // remove the tilde from the query
                queryTerms.RemoveAt(i);
                i--;
                continue;
            }
            // the star operator is a prefix operator, which increases the frequency of any term,
            // therefore increasing its relevance in the search.
            int starCount = 1;
            if (term.StartsWith('*'))
                foreach (char c in term)
                    if (c == '*')
                        starCount++;
                    else
                        break;

            // the except operator is a prefix operator which specifies that the term must *not*
            // appear in any returned document
            if (term.StartsWith('!'))
            {
                //filter operators out of the term
                foreach (char o in operators)
                    term = term.Replace(o.ToString(), "");

                // remove the unwanted term from the query
                queryTerms.RemoveAt(i);

                // if the item is in the vocabulary, and we didn't register it already,
                // add it to the exceptTerms list.
                if (term != "" && vocabulary.Contains(term) && !exceptTerms.Contains(term))
                    exceptTerms.Add(term);

                i--;
                continue;
            }

            // the ^ operator is used when a term (or it's variations) must appear in any
            // document searched.
            if (term.StartsWith('^'))
            {
                //filter operators out of the term
                foreach (char o in operators)
                    term = term.Replace(o.ToString(), "");

                // if we haven't already registered this term as mandatory, register it
                if (term != "" && !mandatoryTerms.Contains(term))
                    mandatoryTerms.Add(term);
            }

            //filter operators out of the term
            foreach (char o in operators)
                term = term.Replace(o.ToString(), "");

            queryTerms[i] = term;

            if (queryTerms[i] != "" && vocabulary.Contains(queryTerms[i]))
            {
                // if the resulting term was valid, and it is in the vocabulary, we should add it
                // to the search that will be performed.
                if (rawQuery.ContainsKey(queryTerms[i]))
                    rawQuery[queryTerms[i]] += starCount;
                else
                    rawQuery.Add(queryTerms[i], starCount);

                // also add it to the originalTerms
                if (!originalQueryTerms.ContainsKey(queryTerms[i]))
                    originalQueryTerms.Add(queryTerms[i], starCount);
                else
                    originalQueryTerms[queryTerms[i]] += starCount;

                // if we are on a tilde streak, and the last term was a tilde
                // add the current term to the current sequence
                if (onTildeStreak && lastTermWasTilde)
                {
                    nearbySequences[^1].Add(term);
                    lastTermWasTilde = false;
                }
                // if the last term wasn't a tilde, break the tilde streak
                else
                    onTildeStreak = false;
            }
            else if (queryTerms[i] != "")
            {
                // but even if the term isn't in the vocabulary it should be added to the originalQuery
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
                // remove the term if invalid.
                queryTerms.RemoveAt(i);
                i--;
                continue;
            }
        }
        // we make an initial search, with the filtered query, its except, mandatory and
        // nearby terms arrays
        var firstSearch = await StrictQuery(
            rawQuery.ToArray(), mandatoryTerms.ToArray(), exceptTerms.ToArray(),
            engineData, nearbySequences.Select(l => l.ToArray()).ToArray());
        List<SearchItem> searchResult = new(firstSearch.results);

        // Dictionary of queries and their resulting cosines, to rank searches 
        // if several are made
        Dictionary<List<string>, double> bestCosines = new();
        bestCosines.Add(queryTerms, firstSearch.maxCosine);

        // If the initial search doesn't render enough results, more searches must be made
        if (searchResult.Count < MIN_RESULTS)
        {
            // lets store the original query terms in a list, for ease of access
            List<string> queryKeys = new(originalQueryTerms.Keys);

            // save mandatory terms as a boolean mask, since variations of mandatory terms must be mandatory
            bool[] areKeysMandatory = new bool[queryKeys.Count];
            for (int i = 0; i < queryKeys.Count; i++)
                areKeysMandatory[i] = mandatoryTerms.Contains(queryKeys[i]);

            System.Console.WriteLine("Insufficient results, looking for query variations...");

            // make deeper searches until enough results have been found, or max depth has been reached
            for (int i = 1; i <= MAX_LEVENSHTEIN_DEPTH && searchResult.Count < MIN_RESULTS; i++)
            {
                System.Console.WriteLine("Searching for Levenshtein depth " + i);
                // This jagged array will contain a sub array for each query term, and each subarray
                // will contain all possible i-depth variations for that term.
                string[][] termsVariations = new string[queryKeys.Count][];

                System.Console.WriteLine("Calculating term variations...");

                // here we compute all variations in the vocabulary for every term
                for (int e = 0; e < termsVariations.Length; e++)
                    termsVariations[e] = GetAllTermVariationsInVocabulary(queryKeys[e], vocabulary, i);

                System.Console.WriteLine("Calculating total query variations...");

                // Here we compute how many variations there are in total, 
                // we also store the amount of variations for 
                // each term and in a one-dimensional array.
                int totalVariations = 1;
                int[] variationsLengths = new int[termsVariations.Length];
                for (int e = 0; e < termsVariations.Length; e++)
                {
                    variationsLengths[e] = termsVariations[e].Length;
                    totalVariations *= variationsLengths[e];
                }

                // If only 1 variation is found, it must be the original query (not stonks 📉)
                if (totalVariations == 1)
                {
                    System.Console.WriteLine("No variations found for depth = " + i);
                    continue;
                }

                // This recursive method returns every possible combination of array indexes
                // i.e. for 3 arrays, the first two of length 2 and the last of length 3, it would
                // return:
                // 0 0 0, 0 0 1, 0 0 2, 0 1 0, 0 1 1, 0 1 2, 1 0 0, 1 0 1, 1 0 2, 1 1 0,
                // 1 1 1, 1 1 2.
                // 12 in total = 2 * 2 * 3.
                // by having all possible index combinations, we get all possible combinations of term variations.
                int[][] allQueryVariations = GetAllArrayCombinations(variationsLengths);

                System.Console.WriteLine("Got all query variations, total " + allQueryVariations.Length);

                // now we compute all possible queries as <term, raw frequency> dictionaries.
                List<Dictionary<string, int>> queryVariations = new();
                for (int e = 0; e < totalVariations; e++)
                {
                    queryVariations.Add(new Dictionary<string, int>());

                    bool invalid = false;
                    for (int o = 0; o < queryKeys.Count; o++)
                    {
                        // Gets index of variation e for term o.
                        int currentVariationIndex = allQueryVariations[e][o];
                        // Gets the actual variation of term o.
                        string term = termsVariations[o][currentVariationIndex];

                        // Uses the frequency of the original term in the original query.
                        int termFrequency = originalQueryTerms[queryKeys[o]];

                        // variations containing repeated terms are considered invalid
                        if (!queryVariations[^1].ContainsKey(term))
                            queryVariations[^1].Add(term, termFrequency);
                        else
                        {
                            invalid = true;
                            break;
                        }
                    }

                    if (invalid)
                        queryVariations.RemoveAt(queryVariations.Count - 1);
                }

                // lets sort the variations by terms IDF, so the most important searches are performed first.
                queryVariations = queryVariations.OrderByDescending(v =>
                    {
                        // total IDF is calculated by adding up the corresponding IDFs of each term
                        double totalIDF = 0;
                        foreach (var key in v.Keys)
                            totalIDF += corpusIDF[Array.IndexOf(vocabulary, key)];

                        return totalIDF;
                    }).ToList();

                System.Console.WriteLine("Searching with variation queries...");
                // now we search with each query variation until we have enough results
                for (int e = 0; e < queryVariations.Count && searchResult.Count < MIN_RESULTS; e++)
                {
                    // these are the terms of the current variation
                    List<string> variationTerms = new(queryVariations[e].Keys);

                    // if the terms are the original query, we should skip it.
                    if (variationTerms.SequenceEqual(queryKeys))
                        continue;

                    List<string> mandatoryVariations = new();
                    for (int o = 0; o < areKeysMandatory.Length; o++)
                        if (areKeysMandatory[o])
                            mandatoryVariations.Add(variationTerms[o]);

                    // the filter is applied so the search doesn't contain repeated items
                    string[] titlesFilter = searchResult.Select(item => item.Title).ToArray();
                    // The search is performed with the current variation and filters
                    var search = await StrictQuery(
                        queryVariations[e].ToArray(), mandatoryVariations.ToArray(),
                        exceptTerms.ToArray(), engineData, filteredTitles: titlesFilter);

                    if (search.maxCosine != 0)
                    {
                        // if the search provides any results, add it to the searchResult list
                        bestCosines.Add(variationTerms, search.maxCosine);
                        searchResult.AddRange(search.results);
                    }
                }
            }
        }

        // now we look for the best search among all that were made, so we suggest it to the user as a better search
        double maxCosine = bestCosines.Values.Max();
        List<string> bestSearch = new();
        foreach (var search in bestCosines)
            if (search.Value == maxCosine)
            {
                bestSearch = search.Key;
                break;
            }

        // now we don't want to suggest the original query ;P
        string suggestion = "";
        if (!bestSearch.SequenceEqual(queryTerms))
            suggestion = string.Join(' ', bestSearch);

        return new SearchResult(searchResult.ToArray(), suggestion);
    }

    // Notes:
    // Strict query assumes all terms are in the vocabulary.
    private static async Task<(SearchItem[] results, double maxCosine)> StrictQuery(
        KeyValuePair<string, int>[] terms, string[] mandatoryTerms,
        string[] exceptTerms, SearchEngineData engineData,
        string[][]? nearbyTerms = null, string[]? filteredTitles = null)
    {
        string[] vocabulary = engineData.vocabulary;
        string[] corpusFiles = engineData.corpusFiles;
        Matrix<double> weightedCorpus = engineData.weightedCorpus;
        Vector<double> corpusIDF = engineData.corpusIDF;

        // StrictQuery searches all documents (except those in the filter) to check their relevance against some query
        Console.WriteLine("Invoked StrictQuery");

        // the filter is optional :P
        filteredTitles ??= Array.Empty<string>();

        List<string> vocabularyList = vocabulary.ToList();

        // the provided query only has as many frequencies as the amount of terms in the query,
        // so this for loop computes a vocabulary-long vector that can be matched against those of the documents.
        Vector<int> rawQuery = new(vocabulary.Length);
        foreach (var term in terms)
            rawQuery[vocabularyList.IndexOf(term.Key)] = term.Value;

        // the raw query is now weighted as a regular document.
        System.Console.WriteLine("Weighting query...");
        Vector<double> weightedQuery = new(GetDocumentTFIDF(rawQuery, idf: corpusIDF));

        Vector<double>[] documents = weightedCorpus.GetAllRows();
        List<(int docIndex, double cosine)> cosines = new();

        System.Console.WriteLine("Finding cosine similarity...");

        await Task.Run(() =>
        {
            for (int i = 0; i < documents.Length; i++)
            {
                // if the document isn't filtered, calculate the cosine similarity between 
                // the query and the document.
                if (!filteredTitles.Contains(Path.GetFileNameWithoutExtension(corpusFiles[i])))
                    cosines.Add((i, VectorMath.GetCosineSimilarity(documents[i], weightedQuery, MIN_SIMILARITY)));
            }
        });

        if (nearbyTerms != null)
        {
            // search score will be scaled up to 1.5x acording to the minimal distance between requested terms
            // since this is a potentially heavy operation, it is implemented as an 
            // async method so the page UI isn't blocked
            await Task.Run(() =>
            {
                for (int i = 0; i < cosines.Count; i++)
                    for (int e = 0; e < nearbyTerms.Length; e++)
                    {
                        int closestDistance = GetMinDistanceBetweenTerms(corpusFiles[cosines[i].docIndex], nearbyTerms[e]);

                        if (closestDistance != 0)
                            cosines[i] = (cosines[i].docIndex,
                                         cosines[i].cosine * (1 + (1 / closestDistance)));
                    }
            });
        }

        System.Console.WriteLine("Computing matches...");

        double maxCosine = cosines.Max(s => s.cosine);

        // if the max cosine in the search is bellow the minimum, then the whole search is invalid
        if (maxCosine <= MIN_SIMILARITY)
        {
            System.Console.WriteLine("Found 0 matches for strict search!");
            return (Array.Empty<SearchItem>(), 0);
        }

        List<SearchItem> result = new();

        for (int i = 0; i < cosines.Count; i++)
        {
            // if document cosine similarity is bellow the minimum, the document is irrelevant
            if (cosines[i].cosine <= MIN_SIMILARITY)
                continue;

            // here we take care of mandatory and except terms
            // every mandatory term must appear in every document
            // and no except term can appear in any document.
            if (mandatoryTerms.Length > 0 || exceptTerms.Length > 0)
            {
                bool skipDoc = false;
                foreach (string mand in mandatoryTerms)
                {
                    int index = Array.IndexOf(vocabulary, mand);

                    if (corpusIDF[index] != 0 && weightedCorpus[index, cosines[i].docIndex] == 0)
                    {
                        skipDoc = true;
                        break;
                    }
                }

                if (skipDoc)
                    continue;

                foreach (string except in exceptTerms)
                {
                    int index = Array.IndexOf(vocabulary, except);

                    if (weightedCorpus[index, cosines[i].docIndex] > 0)
                    {
                        skipDoc = true;
                        break;
                    }
                }

                if (skipDoc)
                    continue;
            }
            // add all relevant results to a list
            result.Add(new SearchItem(
                Path.GetFileNameWithoutExtension(corpusFiles[cosines[i].docIndex]),
                GetSnippet(weightedQuery, corpusFiles[cosines[i].docIndex], engineData),
                (float)cosines[i].cosine));
        }

        System.Console.WriteLine($"Found {result.Count} matches for strict search!");
        // returning results ordered by score.
        return (result.OrderByDescending(item => item.Score).ToArray(), maxCosine);
    }

    private static string GetSnippet(Vector<double> weightedQuery, string documentPath,
    in SearchEngineData engineData)
    {
        string[] vocabulary = engineData.vocabulary;
        string[] corpusFiles = engineData.corpusFiles;
        Matrix<double> weightedCorpus = engineData.weightedCorpus;
        Vector<double> corpusIDF = engineData.corpusIDF;

        // This list contains all valid paragraphs in the document
        List<string> paragraphs = new();
        ForEachFilteredParagraphInFile(documentPath, p =>
        {
            if (p.Any(char.IsLetterOrDigit))
                paragraphs.Add(p);
        });

        //Finding raw frequencies for each term in each paragraph
        Matrix<int> rawParagraphFrequencies = new(vocabulary.Length, paragraphs.Count);
        for (int i = 0; i < rawParagraphFrequencies.Height; i++)
        {
            string[] splitParagraph = paragraphs[i].Split((char[]?)null, options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int e = 0; e < splitParagraph.Length; e++)
                rawParagraphFrequencies[Array.IndexOf(vocabulary, splitParagraph[e]), i] += 1;
        }

        // this new matrix contains the TFIDF of each paragraph in the file.
        Matrix<double> weightedDocument = GetCorpusTFIDF(rawParagraphFrequencies);
        Vector<double>[] weightedRows = weightedDocument.GetAllRows();

        List<string> nonZeroWords = new();
        for (int i = 0; i < weightedQuery.Length; i++)
            if (weightedQuery[i] != 0)
                nonZeroWords.Add(vocabulary[i]);

        // Calculating Cosine similarity of each paragraph
        double[] paragraphsCosines = new double[weightedRows.Length];
        for (int i = 0; i < paragraphsCosines.Length; i++)
            paragraphsCosines[i] = VectorMath.GetCosineSimilarity(weightedRows[i], weightedQuery);

        List<string> rawParagraphs = new();
        // ostrich algorithm was here
        // Here we save every *raw* paragraph, these will be used as snippets.
        ForEachRawParagraphInFile(documentPath, p =>
        {
            if (p.Any(char.IsLetterOrDigit))
                rawParagraphs.Add(p);
        });

        // Now we order paragraphs by their cosines (descending) (also we delete irrelevant paragraphs)
        rawParagraphs = rawParagraphs
                    .Zip(paragraphsCosines)
                    .Where(p => p.Second != 0)
                    .OrderByDescending(p => p.Second)
                    .Select(p => p.First).ToList();

        // Now we iterate through the best paragraphs and add them to the snippet
        int currentSnippetLength = 0;
        string currentSnippet = "... ";
        for (int i = 0; i < rawParagraphs.Count && currentSnippetLength <= CHARACTERS_PER_SNIPPET - STANDARD_PARAGRAPH_LENGTH; i++)
        {
            string paragraph = rawParagraphs[i];

            int remainingCharacters = CHARACTERS_PER_SNIPPET - currentSnippetLength;

            // If the paragraph is longer than the remaining characters, it must be cut
            if (paragraph.Length > remainingCharacters)
            {
                string cutParagraph = "";

                for (int e = 0; e < remainingCharacters; e++)
                {
                    if (e > remainingCharacters - LAST_SNIPPET_WORD_MAX_LENGTH && char.IsWhiteSpace(paragraph[e]))
                        break;

                    cutParagraph += paragraph[e];
                }

                cutParagraph += " ... ";

                currentSnippet += cutParagraph;

                return currentSnippet;
            }
            // if it is short then just add it to the snippet
            else
            {
                currentSnippet += paragraph + " ... ";
                currentSnippetLength += paragraph.Length;
            }
        }

        return currentSnippet;
    }
}
