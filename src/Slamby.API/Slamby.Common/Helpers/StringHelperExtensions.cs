using System;
using System.Text.RegularExpressions;

namespace Slamby.Common.Helpers
{
    public static class StringHelperExtensions
    {
        public static string TrimStart(this string source, string trimString)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (trimString == null)
            {
                throw new ArgumentNullException(nameof(trimString));
            }

            string result = source;

            while (result.StartsWith(trimString, StringComparison.Ordinal))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string TrimEnd(this string source, string trimString)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (trimString == null)
            {
                throw new ArgumentNullException(nameof(trimString));
            }

            string result = source;

            while (result.EndsWith(trimString, StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - trimString.Length);
            }

            return result;
        }

        public static string SafeTrim(this string source)
        {
            return (source ?? string.Empty).Trim();
        }

        /// <summary>
        /// Appends separator string if the source is not null or empty
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public static string AppendSeparator(this string source, string separator)
        {
            if (separator == null)
            {
                throw new ArgumentNullException(nameof(separator));
            }

            string result = source;

            if (!string.IsNullOrEmpty(source))
            {
                result = source + separator;
            }

            return result;
        }

        public static bool IsBase64(this string text)
        {
            return (text.Length % 4 == 0) && Regex.IsMatch(text, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
        }

        public static string CleanBase64(this string text)
        {
            return text.Replace(" ", "")
                .Replace("\n", "")
                .Replace("\r", "");
        }

        public static bool EqualsOrdinalIgnoreCase(this string str1, string str2)
        {
            return string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
        }
    }
}
