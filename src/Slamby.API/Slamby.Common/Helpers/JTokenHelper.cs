using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Slamby.Common
{
    public static class JTokenHelper
    {
        public static bool IsPathExist(this JToken token, string path)
        {
            return GetPathToken(token, path) != null;
        }

        public static List<JToken> GetPathTokens(this JToken token, string path)
        {
            var tokens = new List<JToken> { token };
            return path.Split(new[] { "." }, StringSplitOptions.None)
                .Aggregate(tokens, (current, segment) => GetTokens(current, segment));
        }

        private static List<JToken> GetTokens(List<JToken> tokens, string segment)
        {
            var resultTokens = new List<JToken>();

            foreach (var token in tokens)
            {
                if (token.Type == JTokenType.Array)
                {
                    resultTokens.AddRange(GetTokens(token.Children().ToList(), segment));
                }
                else
                {
                    resultTokens.Add(token.SelectToken(segment));
                }
            }

            return resultTokens;
        }

        public static JToken GetPathToken(this JToken token, string path)
        {
            return path.Split(new[] { "." }, StringSplitOptions.None)
                    .Aggregate(token, (current, segment) => GetToken(current, segment));
        }

        private static JToken GetToken(JToken current, string segment)
        {
            return GetUnderlyingToken(current)?.SelectToken(segment);
        }

        public static JToken GetUnderlyingToken(this JToken current)
        {
            return current?.Type == JTokenType.Array 
                ? current.FirstOrDefault() 
                : current;
        }

        public static IEnumerable<JToken> Flatten(this JToken token)
        {
            return Flatten(new[] { token });
        }

        public static IEnumerable<JToken> Flatten(this IEnumerable<JToken> token)
        {
            return token.Children().SelectMany(child => child.Children().Flatten()).Concat(token);
        }

        public static JToken GetToken(object obj)
        {
            return JToken.FromObject(obj);
        }

        /// <summary>
        /// JSON String or Integer type
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static bool IsStringOrInteger(this JToken token)
        {
            return token.Type == JTokenType.String || 
                token.Type == JTokenType.Integer;
        }

        /// <summary>
        /// JSON String type
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static bool IsString(this JToken token)
        {
            return token.Type == JTokenType.String;
        }

        /// <summary>
        /// JSON Integer type
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static bool IsInteger(this JToken token)
        {
            return token.Type == JTokenType.Integer;
        }
    }
}