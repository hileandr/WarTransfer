using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using WarTransfer.FileTypes;
using WarTransfer.Types;

namespace WarTransfer
{
    public static class WtsUtility
    {
        const string STRING_KEY_PREFIX = "STRING ";

        public static WtsFile ReadFile(IWorkflowHandler e, string filePath)
        {
            WtsFile wts = new WtsFile();
            string[] lines = ReadLinesFromFile(e, filePath);

            if (!e.KillProcess)
            {
                int currentKey = -1;
                int startLine = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    bool isLastLine = (i == lines.Length - 1);

                    if (IsKeyLine(line) || (currentKey != -1 && isLastLine))
                    {
                        if (currentKey != -1)
                        {
                            int count = i - startLine;
                            if (isLastLine)
                                count++;

                            string[] segment = new string[count];
                            for (int j = 0; j < count; j++)
                            {
                                segment[j] = lines[startLine + j];
                            }

                            wts.FileMap.Add(currentKey, segment);
                        }

                        currentKey = ParseKey(line);
                        startLine = i;
                    }
                }
            }

            return wts;
        }

        public static void WriteFile(string outputPath, WtsFile wts)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            File.WriteAllLines(outputPath, wts.GetLines(), Encoding.UTF8);
        }

        private static bool IsKeyLine(string line)
        {
            return (line.StartsWith(STRING_KEY_PREFIX));
        }

        private static int ParseKey(string line)
        {
            int key;
            if (line.Length > STRING_KEY_PREFIX.Length && int.TryParse(line.Substring(STRING_KEY_PREFIX.Length), out key))
            {
                return key;
            }

            return -1;
        }

        private static string KeyLine(int key)
        {
            return STRING_KEY_PREFIX + key;
        }

        public static void ReplaceBlock(WtsFile source, WtsFile destination, int sourceBlockKey, int destBlockKey)
        {
            string[] sourceBlock = source.FileMap[sourceBlockKey];
            string[] oldBlock = destination.FileMap[destBlockKey];

            string[] newBlock = new string[sourceBlock.Length];
            newBlock[0] = oldBlock[0];
            for (int i = 1; i < sourceBlock.Length; i++)
            {
                newBlock[i] = sourceBlock[i];
            }

            destination.FileMap[destBlockKey] = newBlock;
        }

        public static void ReplaceBlock(WtsFile target, string sourceValue, int targetBlockKey)
        {
            string[] targetBlock = target.FileMap[targetBlockKey];
            target.FileMap[targetBlockKey] = new string[]
            {
                targetBlock[0],
                "{",
                sourceValue.Trim('\0'),
                "}",
                ""
            };
        }

        public static int AddBlock(WtsFile target, string value)
        {
            return AddBlock(target, new string[] { "{", value, "}", "" });
        }

        public static int AddBlock(WtsFile target, IEnumerable<string> lines)
        {
            int newKey = GetNextKey(target);
            List<string> newEntry = new List<string>(lines);
            if (IsKeyLine(newEntry[0]))
            {
                newEntry[0] = KeyLine(newKey);
            }
            else
            {
                newEntry.Insert(0, KeyLine(newKey));
            }

            target.FileMap.Add(newKey, newEntry.ToArray());

            return newKey;
        }

        public static bool RemoveBlock(WtsFile file, int key)
        {
            return file.FileMap.Remove(key);
        }

        public static string AddStrRef(WtsFile sourceWts, WtsFile oldWts, string targetValue)
        {
            if (StringUtility.IsRef(targetValue))
            {
                int refKey = StringUtility.ExtractRefKey(targetValue);
                refKey = AddBlock(sourceWts, oldWts.GetLines(refKey));

                return StringUtility.MakeRef(refKey);
            }

            return targetValue;
        }

        public static bool CompareRefEquality(WtsFile f1, WtsFile f2, int key1, int key2)
        {
            string[] lines1 = f1.FileMap[key1];
            string[] lines2 = f2.FileMap[key2];

            if (lines1.Length == lines2.Length)
            {
                // Skip first line because it's the key
                for (int i = 1; i < lines1.Length; i++)
                {
                    if (!lines1[i].Equals(lines2[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static string RemapStrRef(IWorkflowHandler e, WtsFile sourceWts, WtsFile destinationWts, string oldValue, string desiredValue)
        {
            string result = desiredValue;
            if (StringUtility.IsRef(desiredValue))
            {
                int valueLocation = StringUtility.ExtractRefKey(desiredValue);
                int destination;

                if (StringUtility.IsRef(oldValue))
                {
                    destination = StringUtility.ExtractRefKey(oldValue);

                    if (sourceWts.FileMap.ContainsKey(valueLocation) && destinationWts.FileMap.ContainsKey(destination))
                    {
                        if (CompareRefEquality(sourceWts, destinationWts, valueLocation, destination))
                        {
                            // No need to remap, the values are the same.
                            return oldValue;
                        }
                        else
                        {
                            ReplaceBlock(sourceWts, destinationWts, valueLocation, destination);
                        }
                    }
                    else if (!destinationWts.FileMap.ContainsKey(destination))
                    {
                        destination = AddBlock(destinationWts, sourceWts.FileMap[valueLocation]);
                    }
                    else
                    {
                        string untouchedValue = destinationWts.GetValue(destination);
                        e.ReportError($"Unable to find string reference \"{valueLocation}\" in source map. Using value \"{untouchedValue}\" instead.", false);
                        return oldValue;
                    }
                }
                else
                {
                    destination = AddBlock(destinationWts, sourceWts.FileMap[valueLocation]);
                }

                result = StringUtility.MakeRef(destination);
            }
            else if (!desiredValue.Equals("\0"))
            {
                if (StringUtility.IsRef(oldValue))
                {
                    int refKey = StringUtility.ExtractRefKey(oldValue);
                    ReplaceBlock(destinationWts, desiredValue, refKey);

                    result = StringUtility.MakeRef(refKey);
                }
            }

            return result;
        }

        public static void RemapStrRef<T>(IWorkflowHandler e, T source, T target, WtsFile sourceWts, WtsFile destinationWts, Expression<Func<T, string>> stringSelector)
        {
            var selector = stringSelector.Compile();

            string sourceValue = selector(source);
            string targetValue = selector(target);

            object newValue = RemapStrRef(e,sourceWts, destinationWts, sourceValue, targetValue);

            var prop = ReflectionUtility.GetProperty(stringSelector);

            prop.SetValue(target, newValue);
        }

        private static int GetNextKey(WtsFile file)
        {
            var keys = file.FileMap.Keys.OrderBy(x => x);
            int nextKey = 0;

            foreach (int key in keys)
            {
                if (key != nextKey)
                {
                    break;
                }

                nextKey = key + 1;
            }


            return nextKey;
        }

        private static string[] ReadLinesFromFile(IWorkflowHandler e, string filePath)
        {
            string[] lines = null;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception ex)
            {
                e.ReportError(ex, true);
            }

            return lines;
        }
    }
}
