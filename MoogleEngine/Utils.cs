using System.Xml;
using System.Diagnostics.SymbolStore;
using System.ComponentModel;
using System.Reflection.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoogleEngine
{
    public static class Utils
    {
        public static double GetTermFrequency(int termIndex, Vector<int> document)
        {
            if (termIndex < 0 || termIndex >= document.Length)
                throw new IndexOutOfRangeException($"{nameof(termIndex)} must be greater than 0 and lower than {nameof(document)}.Length.");

            if (document[termIndex] == 0) return 0;

            // Term frequency is calculated by dividing the frequency of the term by the sum of the frequencies of all other terms
            return document[termIndex] / document.Sum();
        }
        public static double GetInverseDocumentFrequency(int termIndex, Matrix<int> corpus)
        {
            if (termIndex < 0 || termIndex >= corpus.ColumnLength)
                throw new IndexOutOfRangeException($"{nameof(termIndex)} must be greater than 0 and lower than {nameof(corpus)}.ColumnLength.");

            // Returns the logarithmically scaled inverse fraction of the documents that contain 
            // the word(obtained by dividing the total number of documents by the number of documents
            // containing the term, and then taking the logarithm of that quotient)

            int corpusLength = corpus.RowLength;
            int documentsWithTerm = corpus.GetAllRows().Count(v => v[termIndex] != 0);

            if (documentsWithTerm == 0)
                return 0;

            return Math.Log(corpusLength / documentsWithTerm);
        }
        public static double GetTFIDF(int termIndex, int documentIndex, Matrix<int> corpus)
            => GetTermFrequency(termIndex, corpus.GetRow(documentIndex))
                * GetInverseDocumentFrequency(termIndex, corpus);
    }
}