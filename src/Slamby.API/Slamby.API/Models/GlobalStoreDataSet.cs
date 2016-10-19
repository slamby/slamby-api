using System.Collections.Generic;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Models
{
    public class GlobalStoreDataSet
    {
        /// <summary>
        /// For old DataSets compatibility, it can be null or empty in this case
        /// </summary>
        public string AliasName { get; set; }

        public string IndexName { get; }

        public DataSet DataSet { get; }

        public bool TagIsArray { get; set; }
        public bool TagIsInteger { get; set; }

        public List<string> DocumentFields { get; }

        public List<string> AttachmentFields { get; }

        public GlobalStoreDataSet(string aliasName, string indexName, DataSet dataSet, IEnumerable<string> documentFields,
            bool tagIsArray, bool tagIsInteger, IEnumerable<string> attachmentFields = null) 
        {
            if (indexName == null)
            {
                throw new System.ArgumentNullException(nameof(indexName));
            }
            if (dataSet == null)
            {
                throw new System.ArgumentNullException(nameof(dataSet));
            }
            if (documentFields == null)
            {
                throw new System.ArgumentNullException(nameof(documentFields));
            }

            AliasName = aliasName;
            IndexName = indexName;
            DataSet = dataSet;
            TagIsArray = tagIsArray;
            TagIsInteger = tagIsInteger;
            DocumentFields = new List<string>(documentFields);
            AttachmentFields = new List<string>(attachmentFields ?? new string[] { });
        }
    }
}
