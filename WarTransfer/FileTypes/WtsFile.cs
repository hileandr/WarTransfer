using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WarTransfer.FileTypes
{
    public class WtsFile
    {
        public IDictionary<int, string[]> FileMap;

        public WtsFile()
        {
            FileMap = new SortedDictionary<int, string[]>();
        }

        public IEnumerable<string> GetLines(int key)
        {
            return FileMap[key];
        }

        public List<string> GetLines()
        {
            List<string> lines = new List<string>();
            foreach (int key in FileMap.Keys)
            {
                lines.AddRange(FileMap[key]);
            }

            return lines;
        }

        public string GetValue(int key)
        {
            string value = "";
            string[] lines = FileMap[key];
            int valueStartIndex = -1;
            int lineLength = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (valueStartIndex == -1 && lines[i] == "{")
                {
                    valueStartIndex = i + 1;
                }
                else if (valueStartIndex >= i)
                {
                    if (lines[i] == "}")
                    {
                        break;
                    }
                    else
                    {
                        lineLength++;
                        if (lineLength > 1)
                        {
                            value += "\r\n";
                        }

                        value += lines[i];
                    }
                }
            }

            return value;
        }
    }
}
