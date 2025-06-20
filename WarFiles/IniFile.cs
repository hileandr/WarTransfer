using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WarFiles
{
    public class IniFile
    {
        public List<IniEntry> Entries { get; set; }
        public Dictionary<string, IniSection> Sections { get; set; }

        public IniFile()
        {
            Entries = new List<IniEntry>();
            Sections = new Dictionary<string, IniSection>();
        }

        public IEnumerable<IniEntry> GetSectionEntries(string sectionName)
        {
            if (Sections.TryGetValue(sectionName, out IniSection section))
            {
                for (int i = 0; i < section.Length; i++)
                {
                    yield return Entries[section.StartIndex + i];
                }
            }
        }

        public IniEntry FindEntry(string sectionName, string entryKey)
        {
            foreach (IniEntry entry in GetSectionEntries(sectionName))
            {
                if (entry.Key == entryKey)
                {
                    return entry;
                }
            }

            return null;
        }

        public void Deserialize(TextReader reader)
        {
            IniSection currentSection = null;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("//"))
                {
                    continue;
                }
                else if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = new IniSection();
                    currentSection.Name = line.Substring(1, line.Length - 2);
                    currentSection.StartIndex = Entries.Count;

                    Sections.Add(currentSection.Name, currentSection);
                }
                else
                {
                    IniEntry entry = new IniEntry();

                    int indexOfEquals = line.IndexOf('=');
                    if (indexOfEquals != -1)
                    {
                        entry.Key = line.Substring(0, indexOfEquals);
                        entry.Values = line.Substring(indexOfEquals + 1).Split(',');
                    }
                    else
                    {
                        entry.Values = line.Split(',');
                    }

                    Entries.Add(entry);

                    if (currentSection != null)
                    {
                        currentSection.Length++;
                    }
                }
            }    
        }
    }

    public class IniSection
    {
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }

        public IniSection()
        {
            Name = "";
            StartIndex = -1;
            Length = 0;
        }
    }

    public class IniEntry
    {
        public string Key { get; set; }
        public string[] Values { get; set; }
    }
}
