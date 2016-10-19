using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using Slamby.API.Models;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.Common;
using Slamby.Common.Helpers;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Xunit;

namespace Slamby.Tests.Services
{
    public class DocumentServiceTests
    {
        #region Test data

        #region Sample Document

        public static IEnumerable<object[]> DocumentsWithMissingFields
        {
            get
            {
                return new[]
                {
                    new object[] { new { _id = "", tag = "", int1 = "", int2 = "" } },
                    new object[] { new { id = "", _tag = "", int1 = "", int2 = "" } },
                    new object[] { new { id = "", tag = "", _int1 = "", int2 = "" } },
                    new object[] { new { id = "", tag = "", int1 = "", _int2 = "" } }
                };
            }
        }
        public static IEnumerable<object[]> DocumentsWithExtraFields
        {
            get
            {
                return new[]
                {
                    new object[] { new { id = "", tag = "", int1 = "", int2 = "", ext = "" } }
                };
            }
        }
        public static IEnumerable<object[]> DocumentsWithInvalidIdTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { id = "" } }, // empty
                    new object[] { new { id = new string[] { } } }, // array type
                    new object[] { new { id = new { } } }, // object type
                };
            }
        }
        public static IEnumerable<object[]> SampleDocumentsWithInvalidTagTypes
        {
            get
            {
                return new[]
                {
                    // Tag type definition does NOT count
                    new object[] { new { tag = "" }, null }, // empty value
                    new object[] { new { tag = false }, null }, // boolean value
                    new object[] { new { tag = (string)null }, null }, // null value
                    new object[] { new { tag = new string[] { "" } }, null }, // empty array
                    new object[] { new { tag = new { } }, null }, // object type
                    new object[] { new { tag = new string[][] { new string[] { "" } } }, null }, // empty array in array
                    new object[] { new { tag = new object[] { new { } } }, null }, // empty object in array
                    new object[] { new { tag = new string[][] { new string[] { "item" } } }, null }, // not empty array in array
                    new object[] { new { tag = new object[] { new { item = "item" } } }, null }, // not empty object in array

                    // Tag type definition DOES count
                    new object[] { new { tag = "tag1" }, true }, // simple type but defined as array
                    new object[] { new { tag = new string[] { "tag1" } }, false }, // array but defined as simple type
                };
            }
        }
        public static IEnumerable<object[]> DocumentsWithInvalidTagTypes
        {
            get
            {
                return new[]
                {
                    // Tag type definition does NOT count
                    new object[] { new { tag = false }, null }, // boolean value
                    new object[] { new { tag = new { } }, null }, // object type
                    new object[] { new { tag = new string[][] { new string[] { "item" } } }, null }, // not empty array in array
                    new object[] { new { tag = new object[] { new { item = "item" } } }, null }, // not empty object in array

                    // Tag type definition DOES count
                    new object[] { new { tag = "tag1" }, true }, // simple type but defined as array
                    new object[] { new { tag = new string[] { "tag1" } }, false }, // array but defined as simple type
                };
            }
        }
        public static IEnumerable<object[]> DocumentsWithInvalidInterpretedTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { int1 = new string[] { "" }, int2 = "int2" } }, // array type
                    new object[] { new { int1 = new { }, int2 = "int2" } }, // object type
                    new object[] { new { int1 = "int1" , int2 = new string[] { "" } } }, // array type
                    new object[] { new { int1 = "int1", int2 = new { } } }, // object type
                };
            }
        }

        #endregion

        #region Schema

        public static IEnumerable<object[]> DocumentsWithInvalidSchemaIdTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { type = "object", properties = new { id = new { type = "array", items = new { type = "string" } } } } }, // array type
                    new object[] { new { type = "object", properties = new { id = new { type = "object", properties = new { } } } } }, // object type
                };
            }
        }
        public static IEnumerable<object[]> DocumentsWithInvalidSchemaTagTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { type = "object", properties = new { tag = new { type = "attachment" } } } }, // attachment type
                    new object[] { new { type = "object", properties = new { tag = new { type = "object", properties = new { } } } } }, // object type
                };
            }
        }
        public static IEnumerable<object[]> DocumentsWithInvalidSchemaInterpretedTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { type = "object", properties = new { int1 = new { type = "array" } } } }, // attachment type
                    new object[] { new { type = "object", properties = new { int1 = new { type = "object", properties = new { } } } } }, // object type
                };
            }
        }

        #endregion

        public static IEnumerable<object[]> SampleDocumentsWithValidTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { id = "id1", tag = "tag1", int1 = "int1", int2 = "int2" }, false },
                    new object[] { new { id = 123456, tag = "tag1", int1 = "int1", int2 = "int2" }, false },

                    new object[] { new { id = "id1", tag = 12345, int1 = "int1", int2 = "int2" }, false },
                    new object[] { new { id = "id1", tag = new string[] { "tag1", "tag2" }, int1 = "int1", int2 = "int2" }, true },

                    new object[] { new { id = "id1", tag = "tag1", int1 = "", int2 = "" }, false },
                };
            }
        }

        public static IEnumerable<object[]> DocumentsWithValidTypes
        {
            get
            {
                return new[]
                {
                    new object[] { new { id = "id1", tag = "tag1", int1 = "int1", int2 = "int2" }, false },
                    new object[] { new { id = 123456, tag = "tag1", int1 = "int1", int2 = "int2" }, false },

                    new object[] { new { id = "id1", tag = (string)null, int1 = "int1", int2 = "int2" }, false },
                    new object[] { new { id = "id1", tag = "", int1 = "int1", int2 = "int2" }, false },
                    new object[] { new { id = "id1", tag = new string[] { }, int1 = "int1", int2 = "int2" }, true },
                    new object[] { new { id = "id1", tag = 12345, int1 = "int1", int2 = "int2" }, false },
                    new object[] { new { id = "id1", tag = new string[] { "tag1", "tag2" }, int1 = "int1", int2 = "int2" }, true },
                    new object[] { new { id = "id1", tag = new string[] { "tag1", "tag2" }, int1 = "int1", int2 = "int2" }, true },

                    new object[] { new { id = "id1", tag = "tag1", int1 = "", int2 = "" }, false },
                };
            }
        }

        private static GlobalStoreDataSet GetSampleDataSet()
        {
            return new GlobalStoreDataSet(
                "dataset_name",
                "dataset_guid",
                new DataSet()
                {
                    IdField = "id",
                    TagField = "tag",
                    InterpretedFields = new List<string> { "int1", "int2" }
                },
                new string[] { "id", "tag", "int1", "int2" },
                false,
                false,
                new string[] { });
        }

        #endregion

        #region Document

        [Theory, MemberData(nameof(DocumentsWithMissingFields))]
        public void ValidateDocument_ShouldNotValidate_DocumentWithMissingFields(object document)
        {
            // Arrange
            var dataSet = GetSampleDataSet();
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            globalStoreManagerMock.Setup(s => s.DataSets.Get(It.IsAny<string>())).Returns(dataSet);
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateDocument("dataset_name", document);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithExtraFields))]
        public void ValidateDocument_ShouldNotValidate_DocumentWithExtraFields(object document)
        {
            // Arrange
            var dataSet = GetSampleDataSet();
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            globalStoreManagerMock.Setup(s => s.DataSets.Get(It.IsAny<string>())).Returns(dataSet);
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateDocument("dataset_name", document);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Fact]
        public void ValidateDocument_ShouldValidate_DocumentWithExistingFields()
        {
            // Arrange
            var dataSet = GetSampleDataSet();
            var documentQueryMock = new Mock<IDocumentQuery>();
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            globalStoreManagerMock.Setup(s => s.DataSets.Get(It.IsAny<string>())).Returns(dataSet);
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var document = new { id = "", tag = "", int1 = "", int2 = new string[] { } };

            // Act
            var result = service.ValidateDocument("dataset_name", document);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithInvalidIdTypes))]
        public void ValidateIdField_ShouldNotValidate_DocumentWithInvalidIdType(object document)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var dataSet = GetSampleDataSet();

            // Act
            var result = service.ValidateIdField(JTokenHelper.GetToken(document), dataSet.DataSet.IdField);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(SampleDocumentsWithInvalidTagTypes))]
        public void ValidateTagField_ShouldNotValidate_SampleDocumentWithInvalidTagType(object document, bool? tagIsArray)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var dataSet = GetSampleDataSet();

            // Act
            var result = service.ValidateTagField(JTokenHelper.GetToken(document), dataSet.DataSet.TagField, tagIsArray, null, true);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithInvalidTagTypes))]
        public void ValidateTagField_ShouldNotValidate_DocumentWithInvalidTagType(object document, bool? tagIsArray)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService( globalStoreManagerMock.Object, null, null);
            var dataSet = GetSampleDataSet();

            // Act
            var result = service.ValidateTagField(JTokenHelper.GetToken(document), dataSet.DataSet.TagField, tagIsArray, null, false);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithInvalidInterpretedTypes))]
        public void ValidateInterpretedFields_ShouldNotValidate_DocumentWithInvalidInterpretedType(object document)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var dataSet = GetSampleDataSet();

            // Act
            var result = service.ValidateInterpretedFields(JToken.FromObject(document), dataSet.DataSet.InterpretedFields);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithValidTypes))]
        public void ValidateDocument_ShouldValidate_DocumentWithValidType(object document, bool tagIsArray)
        {
            // Arrange
            var dataSet = GetSampleDataSet();
            dataSet.TagIsArray = tagIsArray;
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            globalStoreManagerMock.Setup(s => s.DataSets.Get(It.IsAny<string>())).Returns(dataSet);
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateDocument("dataset_name", document);

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }

        [Theory, MemberData(nameof(SampleDocumentsWithValidTypes))]
        public void ValidateDocument_ShouldValidate_SampleDocumentWithValidType(object document, bool tagIsArray)
        {
            // Arrange
            var dataSet = GetSampleDataSet();
            dataSet.TagIsArray = tagIsArray;
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            globalStoreManagerMock.Setup(s => s.DataSets.Get(It.IsAny<string>())).Returns(dataSet);
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateSampleDocument(JToken.FromObject(document), "id", "tag", new[] { "int1", "int2" });

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }

        [Theory,
            InlineData(""),
            InlineData("adasdas"),
            InlineData("dGVzdA="),
            InlineData("dGVzdA= ")]
        public void ValidateAttachmentFields_ShouldNotValidate_InvalidBase64AttachmentFields(string base64)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var attachmentFields = new string[] { "nested.file" };
            var document = new
            {
                nested = new { file = base64 }
            };

            // Act
            var result = service.ValidateAttachmentFields(document, attachmentFields);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory,
            InlineData("dGVzdA=="),
            InlineData(" dGVzdA== "),
            InlineData(" dGV\nzdA== "),
            InlineData(" dGV\rzdA== "),
            InlineData(" dGV\r\nzdA== ")]
        public void ValidateAttachmentFields_ShouldValidate_ValidBase64AttachmentFields(string base64)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var attachmentFields = new string[] { "nested.file" };
            var document = new
            {
                nested = new { file = base64 }
            };

            // Act
            var result = service.ValidateAttachmentFields(document, attachmentFields);

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }

        #endregion

        #region Schema

        [Theory, MemberData(nameof(DocumentsWithInvalidSchemaIdTypes))]
        public void ValidateSchemaIdField_ShouldNotValidate_SchemaWithInvalidType(object schema)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateSchemaIdField(JToken.FromObject(schema), "id");

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithInvalidSchemaTagTypes))]
        public void ValidateSchemaTagField_ShouldNotValidate_SchemaWithInvalidType(object schema)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateSchemaTagField(JToken.FromObject(schema), "tag");

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory, MemberData(nameof(DocumentsWithInvalidSchemaInterpretedTypes))]
        public void ValidateSchemaInterpretedField_ShouldNotValidate_SchemaWithInvalidType(object schema)
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);

            // Act
            var result = service.ValidateSchemaInterpretedFields(JToken.FromObject(schema), new string[] { "tag" });

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Fact]
        public void ValidateSchema_ShouldValidate_SchemaWithValidSchema()
        {
            // Arrange
            var globalStoreManagerMock = new Mock<IGlobalStoreManager>();
            var service = new DocumentService(globalStoreManagerMock.Object, null, null);
            var schema = new { id = 1, title = "", desc = "", tags = new string[] { } };
            var type = schema.GetType();
            var token = GenerateSchema(type);

            // Act
            var result = service.ValidateSchema(token, "id", "tags", new string[] { "title", "desc" });

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }

        #endregion
        
        private JToken GenerateSchema(Type type)
        {
            var schemaJson = JsonSchema4.FromType(type, new JsonSchemaGeneratorSettings() { NullHandling = NullHandling.Swagger }).ToJson();
            return JsonConvert.DeserializeObject(schemaJson) as JToken;
        }
    }
}
