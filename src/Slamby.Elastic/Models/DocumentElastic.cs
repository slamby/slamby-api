using Nest;
using System;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = DocumentElastic.DocumentTypeName, IdProperty = "Id")]
    public class DocumentElastic : IModel
    {
        public const string DocumentTypeName = "document";

        public const string DocumentObjectMappingName = "document_object";
        public const string IdField = "id";
        public const string CreatedDateField = "created_date";
        public const string ModifiedDateField = "modified_date";
        public const string OwnFields = "id,status,created_date,modified_date,text";
        public const string TextField = "text";

        [String(Name = IdField)]
        public string Id { get; set; }

        [Number(NumberType.Integer, Name = "status")]
        public int Status { get; set; }

        [Date(Name = CreatedDateField)]
        public DateTime CreatedDate { get; set; }

        [Date(Name = ModifiedDateField)]
        public DateTime ModifiedDate { get; set; }

        [Object(Name = DocumentObjectMappingName)]
        public object DocumentObject { get; set; }

        [String(Name = TextField)]
        public string Text { get; set; }

        public DocumentElastic()
        {
            CreatedDate = DateTime.UtcNow;
            ModifiedDate = DateTime.UtcNow;
        }
    }
}
