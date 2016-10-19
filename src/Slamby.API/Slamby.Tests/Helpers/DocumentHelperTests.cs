using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Slamby.Common.Helpers;
using Slamby.Elastic.Models;
using Xunit;

namespace Slamby.Tests.Helpers
{
    public class DocumentHelperTests
    {
        #region Test Data
        public static string JsonObject
        {
            get
            {
                return "{" +
                    "  \"login_id\": 123," +
                    "  \"postal_code\": \"1234\"," +
                    "  \"city\": \"Budapest\"" +
                    "}";
            }
        }

        public static IEnumerable<string> JsonPaths
        {
            get
            {
                return new string[] {
                  "login_id",
                  "postal_code",
                  "city"
                 };
            }
        }

        public static IEnumerable<object[]> JsonObjects
        {
            get
            {
                return new[]
                {
                    new object[] { JsonObject, JsonPaths }
                };
            }
        }

        #endregion

        [Theory, MemberData(nameof(JsonObjects))]
        public void GetAllPathTokens_ShouldReturn_ValidFieldList(string json, IEnumerable<string> paths)
        {
            // Assign
            var obj = JObject.Parse(json);

            // Arrange
            var resultPaths = DocumentHelper.GetAllPathTokens(obj);

            // Assert
            paths.ShouldBeEquivalentTo(resultPaths.Keys);
        }

        [Fact]
        public void RemoveTagIds_ShouldRemove_TagIds()
        {
            // Arrange
            var documentElastic = new DocumentElastic()
            {
                DocumentObject = JToken.FromObject(new
                {
                    nested = new
                    {
                        tags = new string[] { "1", "2", "3", "4" }
                    }
                })
            };

            // Act
            DocumentHelper.RemoveTagIds(documentElastic.DocumentObject, "nested.tags", new List<string>() { "2", "4" });

            // Assert
            dynamic dyn = documentElastic.DocumentObject;
            Assert.Equal((dyn.nested.tags as JArray).Select(s => (string)s).ToArray(), new string[] { "1", "3" });
        }
    }
}
