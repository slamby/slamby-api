using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Slamby.Common.Helpers
{
    public static class DocumentHelper
    {
        public static IEnumerable<string> GetExtraFields(object document, IEnumerable<string> validFieldNames)
        {
            return GetAllPaths(document).Except(validFieldNames);
        }

        /// <summary>
        /// Returns with the value of the given path.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="path">E.g.: name.firstname</param>
        /// <returns>Framework's primitive types</returns>
        public static object GetValue(object document, string path)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(nameof(path));
            }

            var token = JTokenHelper.GetPathToken(JToken.FromObject(document), path);
            if (token == null)
            {
                return null;
            }

            var valueToken = token as JValue;
            if (valueToken != null)
            {
                return valueToken.Value;
            }

            var arrayToken = token as JArray;
            if (arrayToken != null)
            {
                if (arrayToken.First?.Type == JTokenType.Integer)
                {
                    return arrayToken.Select(item => (int)item).ToArray();
                }

                return arrayToken.Select(item => (string)item).ToArray();
            }

            return token;
        }

        public static bool IsExist(object document, string fieldName)
        {
            var token = JTokenHelper.GetPathToken(JToken.FromObject(document), fieldName);
            return token != null;
        }

        public static bool IsPrimitiveType(object document, string fieldName)
        {
            var token = JTokenHelper.GetPathToken(JToken.FromObject(document), fieldName);
            return token != null && token is JValue;
        }

        public static List<string> GetAllPaths(object obj)
        {
            return GetAllPaths(JTokenHelper.GetToken(obj));
        }

        public static List<string> GetAllPaths(JToken token)
        {
            return GetAllPathTokens(token).Keys.ToList();
        }

        public static Dictionary<string, JToken> GetAllPathTokens(object obj)
        {
            return GetAllPathTokens(JTokenHelper.GetToken(obj));
        }

        public static Dictionary<string, JToken> GetAllPathTokens(JToken token)
        {
            var indexerRegex = new Regex(@"(\[\d*\])");
            var paths = token.Flatten()
                    .Where(child => child.Parent != null)
                    .Select(child => new KeyValuePair<string, JToken>(indexerRegex.Replace(child.Path, ""), child))
                    .GroupBy(
                        k => k.Key, 
                        v => v.Value, 
                        (key, group) => new KeyValuePair<string, JToken>(key, group.First())) // Type Object & Property can have the same name
                    .ToDictionary(k => k.Key, v => v.Value);

            return paths;
        }

        public static List<string> GetAllPropertyNames(JToken token)
        {
            var keys = new List<string>();
            JContainer container = token as JContainer;
            if (container == null) return new List<string>();

            foreach (JToken el in container.Children())
            {
                JProperty p = el as JProperty;
                if (p != null)
                {
                    keys.Add(p.Name);
                }
                keys.AddRange(GetAllPropertyNames(el));
            }
            return keys;
        }

        public static void RemoveTagIds(object document, string fieldName, List<string> tagIdsToRemove)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var token = document as JToken;
            if (token == null)
            {
                return;
            }

            var field = JTokenHelper.GetPathToken(token, fieldName);

            var valueToken = field as JValue;
            if (valueToken != null)
            {
                valueToken.Value = string.Empty;
            }

            var arrayToken = field as JArray;
            if (arrayToken != null)
            {
                var items = arrayToken.Where(tag => tagIdsToRemove.Contains((string)tag))
                                    .Select(tag => tag)
                                    .ToList();

                foreach (var item in items.Where(i => i != null))
                {
                    arrayToken.Remove(item);
                }
            }
        }

        public static string GetConcatenatedText(object document, List<string> pathsToConcatenate, object originalDocument = null)
        {
            if (originalDocument == null)
                return string.Join(
                    $" {Constants.TextFieldSeparator} ", 
                    pathsToConcatenate.Select(p => GetValue(document, p)?.ToString()).Where(p => !string.IsNullOrEmpty(p)));

            var textList = new List<string>();
            foreach (var path in pathsToConcatenate)
            {
                var value = GetValue(document, path);
                if (value == null) value = GetValue(originalDocument, path);
                if (value != null) textList.Add(value.ToString());
            }
            return string.Join(
                    $" {Constants.TextFieldSeparator} ",
                    textList.Where(p => !string.IsNullOrEmpty(p)));
        }
    }
}
