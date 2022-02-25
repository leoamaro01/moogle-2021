using System.Diagnostics;
using static MoogleEngine.Utils;

namespace MoogleEngine
{
    // This struct contains all data the Search Engine needs to search any query in a corpus
    public struct SearchEngineData
    {
        // this will contain all distinct words in the corpus
        internal string[] vocabulary;

        // this will contain every file path in the corpus
        internal string[] corpusFiles;

        // this is a mxn matrix, where m is the number of files and n the number of terms in the vocabulary
        // it contains the tf-idf weight of each word in each document
        internal Matrix<double> weightedCorpus;

        // the idf of a term is global and independent of any document, so this vector stores the idf of every term
        internal Vector<double> corpusIDF;

        public static async Task<SearchEngineData> GenerateData()
        {
            string[] vocabulary, corpusFiles;
            Matrix<double> weightedCorpus;
            Vector<double> corpusIDF;

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

            await Task.Run(() =>
            {
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
            });
            // Now we all distinct words to the vocabulary.

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

            SearchEngineData result = new()
            {
                corpusFiles = corpusFiles,
                corpusIDF = corpusIDF,
                vocabulary = vocabulary,
                weightedCorpus = weightedCorpus
            };

            return result;
        }
    }
}