using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarTransfer.Types;

namespace WarTransfer
{
    class StringUtility
    {
        private const string REF_PREFIX = "TRIGSTR_";

        public static string ReadNullTerminatedString(byte[] bytes, int startIndex)
        {
            string result = "";
            char c = '0';
            while (startIndex < bytes.Length && c != '\0')
            {
                c = (char)bytes[startIndex++];
                result += c;
            }

            return result;
        }

        public static bool IsRef(string s)
        {
            return s.StartsWith(REF_PREFIX);
        }

        public static string MakeRef(int refKey)
        {
            if (refKey < 10)
            {
                return $"{REF_PREFIX}00{refKey}";
            }
            else if (refKey < 100)
            {
                return $"{REF_PREFIX}0{refKey}";
            }
            else
            {
                return $"{REF_PREFIX}{refKey}";
            }
        }

        /// <summary>
        /// Assumes the given string is a reference string (Like TRIGSTR_009)
        /// </summary>
        /// <param name="s">The string to extract the key from</param>
        /// <returns>The string key.</returns>
        public static int ExtractRefKey(string s)
        {
            int key = 0;
            if (int.TryParse(s.Substring(REF_PREFIX.Length), out key))
            {
                return key;
            }

            return -1;
        }

        public static InlineString FindInlineString(string input, int startIndex)
        {
            for (int i = startIndex; i < input.Length; i++)
            {
                if (input[i] == '/' && i + 1 < input.Length && input[i + 1] == '/') return null;
                if (input[i] == '\'')
                {
                    i = LiteralEnd(input, i, '\'');
                }
                else if (input[i] == '"')
                {
                    int end = LiteralEnd(input, i, '"');
                    return new InlineString(i + 1, input.Substring(i + 1, end - i - 1));
                }
            }
            return null;
        }

        private static int LiteralEnd(string input, int start, char quote)
        {
            for (int i = start + 1; i < input.Length; i++)
            {
                if (input[i] == '\\') { i++; continue; }
                if (input[i] == quote) return i;
            }
            throw new FormatException("Unterminated JASS literal: " + input.Trim());
        }

        // Keep token positions stable while excluding literals and trailing comments.
        internal static string CodeOnly(string input)
        {
            var result = input.ToCharArray();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '/' && i + 1 < input.Length && input[i + 1] == '/')
                {
                    for (int j = i; j < result.Length; j++) result[j] = ' ';
                    break;
                }
                if (input[i] == '"' || input[i] == '\'')
                {
                    int end = LiteralEnd(input, i, input[i]);
                    for (int j = i; j <= end; j++) result[j] = ' ';
                    i = end;
                }
            }
            return new string(result);
        }

        internal static string StringContext(string input)
        {
            return new string(CodeOnly(input).Where(c => !char.IsWhiteSpace(c)).ToArray());
        }

        internal static string DecodeJassString(string value)
        {
            var result = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    c = value[++i];
                    switch (c)
                    {
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        case '\\': case '"': break;
                        default: result.Append('\\'); break;
                    }
                }
                result.Append(c);
            }
            return result.ToString();
        }

        internal static string EncodeJassString(string value) => value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        public static string SubstringUntil(string s, char c)
        {
            int index = s.IndexOf(c);
            if (index != -1)
            {
                return s.Substring(0, index);
            }

            return s;
        }

        public static string RemoveAllCharsAndDigits(string s, char c)
        {
            string result = "";
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != c && !char.IsDigit(s[i]))
                {
                    result += s[i];
                }
            }

            return result;
        }

        public static string RemoveAll(string s, params char[] chars)
        {
            string result = "";
            for (int i = 0; i < s.Length; i++)
            {
                bool include = true;
                for (int c = 0; c < chars.Length && include; c++)
                {
                    if (s[i] == chars[c])
                    {
                        include = false;
                    }
                }

                if (include)
                {
                    result += s[i];
                }
            }

            return result;
        }
    }
}
