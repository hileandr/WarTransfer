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
            int inlineStart = -1;
            for (int i = startIndex; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"')
                {
                    if (inlineStart == -1)
                    {
                        inlineStart = i + 1;
                    }
                    else
                    {
                        int length = i - inlineStart;
                        if (length > 0)
                        {
                            return new InlineString(inlineStart, input.Substring(inlineStart, length));
                        }

                        inlineStart = -1;
                    }
                }
            }

            return null;
        }

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
