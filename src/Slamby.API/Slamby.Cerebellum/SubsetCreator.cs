using System;
using System.Collections.Generic;
using System.Linq;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;

namespace Slamby.Cerebellum
{
    public class SubsetCreator
    {
        private readonly int _allWordsOccurences;
        private readonly string _indexName;
        private readonly int _nGramCount;
        private readonly List<string> _textFields;
        readonly IQueryFactory queryFactory;
        readonly List<string> attachmentFields;

        /// <summary>
        /// Subset (words and occurences etc.) creator
        /// </summary>
        /// <param name="indexName">the name of the index</param>
        /// <param name="textFields">the text field where the words and occurences come from, the fields where you have the analyzer_1, analyzer_2...</param>
        /// <param name="interPretedFields">this will be used for the all word occurences only (where the tokencount tokenfilter is)</param>
        /// <param name="nGramCount"></param>
        /// <param name="queryFactory"></param>
        /// <param name="attachmentFields"></param>
        public SubsetCreator(string indexName, List<string> textFields, List<string> interPretedFields, int nGramCount, IQueryFactory queryFactory, List<string> attachmentFields)
        {
            this.attachmentFields = attachmentFields;
            this.queryFactory = queryFactory;
            _indexName = indexName;
            _textFields = textFields;
            _nGramCount = nGramCount;

            _allWordsOccurences = queryFactory.GetWordQuery(_indexName).GetAllWordsOccurences(interPretedFields, nGramCount);
        }

        public Subset CreateByTag(string tagId, string tagField)
        {
            var docQuery = queryFactory.GetDocumentQuery(_indexName);
            var wordQuery = queryFactory.GetWordQuery(_indexName);
            var docs = docQuery.GetByTagId(tagId, tagField, DocumentQuery.GetDocumentElasticFields(new[] { DocumentElastic.IdField }));

            Func<string, bool> isAttachmentField = (field) => attachmentFields.Any(attachmentField => 
                string.Equals(attachmentField, field, StringComparison.OrdinalIgnoreCase));

            var fields = _textFields
                .Select(field => isAttachmentField(field) ? $"{field}.content" : field)
                .ToList();

            var wwo = wordQuery.GetWordsWithOccurences(docs.Select(d => d.Id).ToList(), fields, _nGramCount);

            var subset = new Subset
            {
                AllWordsOccurencesSumInCorpus = _allWordsOccurences,
                AllWordsOccurencesSumInTag = wwo.Sum(w => w.Value.Tag),
                WordsWithOccurences = wwo
            };
            
            return subset;
        }
    }
}
