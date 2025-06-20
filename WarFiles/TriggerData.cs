using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WarFiles
{
    public class TriggerData
    {
        public Dictionary<string, GUIDefinition> EventDefs { get; set; }
        public Dictionary<string, GUIDefinition> ConditionDefs { get; set; }
        public Dictionary<string, GUIDefinition> ActionDefs { get; set; }
        public Dictionary<string, GUIDefinition> CallDefs { get; set; }

        public TriggerData()
        {
            EventDefs = new Dictionary<string, GUIDefinition>();
            ConditionDefs = new Dictionary<string, GUIDefinition>();
            ActionDefs = new Dictionary<string, GUIDefinition>();
            CallDefs = new Dictionary<string, GUIDefinition>();
        }

        public Dictionary<string, GUIDefinition> GetDefinitions(GUIFunctionType type)
        {
            switch (type)
            {
                case GUIFunctionType.Event: return EventDefs;
                case GUIFunctionType.Condition: return ConditionDefs;
                case GUIFunctionType.Action: return ActionDefs;
                default: throw new NotImplementedException();
            }
        }

        public Dictionary<string, GUIDefinition> GetDefinitions(GUISubParameterType type)
        {
            switch (type)
            {
                case GUISubParameterType.Events: return EventDefs;
                case GUISubParameterType.Conditions: return ConditionDefs;
                case GUISubParameterType.Actions: return ActionDefs;
                case GUISubParameterType.Calls: return CallDefs;
                default: throw new NotImplementedException();
            }
        }

        public uint GetArgCount(string name)
        {
            GUIDefinition def;

            if (EventDefs.TryGetValue(name, out def))
            {
                return (uint)def.ArgTypes.Count;
            }

            if (ConditionDefs.TryGetValue(name, out def))
            {
                return (uint)def.ArgTypes.Count;
            }

            if (ActionDefs.TryGetValue(name, out def))
            {
                return (uint)def.ArgTypes.Count;
            }

            if (CallDefs.TryGetValue(name, out def))
            {
                return (uint)def.ArgTypes.Count;
            }

            throw new KeyNotFoundException();
        }

        public void Deserialize(TextReader reader)
        {
            Dictionary<string, GUIDefinition> currentSection = null;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.StartsWith("//"))
                {
                    // Skip comments
                    continue;
                }
                else if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    switch (line)
                    {
                        case "[TriggerEvents]": currentSection = EventDefs; break;
                        case "[TriggerConditions]": currentSection = ConditionDefs; break;
                        case "[TriggerActions]": currentSection = ActionDefs; break;
                        case "[TriggerCalls]": currentSection = CallDefs; break;
                        default: currentSection = null; break;
                    }
                }
                else if (currentSection != null && line.Length > 0)
                {
                    GUIDefinition def = new GUIDefinition();
                    def.Deserialize(reader, line, currentSection == CallDefs);

                    currentSection.Add(def.Name, def);
                }
            }
        }
    }

    public class GUIDefinition
    {
        public string Name { get; set; }
        public int GameVersion { get; set; }
        public string ReturnType { get; set; }
        public List<string> ArgTypes { get; set; }
        public List<KeyValuePair<string, string>> Data { get; set; }

        public GUIDefinition()
        {
            ArgTypes = new List<string>();
            Data = new List<KeyValuePair<string, string>>();
        }

        public void Deserialize(TextReader reader, string firstLine, bool isCall)
        {
            int indexOfEquals = firstLine.IndexOf('=');
            if (indexOfEquals != -1)
            {
                Name = firstLine.Substring(0, indexOfEquals);
                string[] args = firstLine.Substring(indexOfEquals + 1).Split(',');

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Length > 0 && !double.TryParse(args[i], out double result))
                    {
                        if (isCall && string.IsNullOrEmpty(ReturnType))
                        {
                            ReturnType = args[i];
                        }
                        else if (args[i] != "nothing")
                        {
                            ArgTypes.Add(args[i]);
                        }
                    }
                }

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0)
                    {
                        if (line.StartsWith("//"))
                            continue;

                        indexOfEquals = line.IndexOf('=');
                        if (indexOfEquals != -1 && line.StartsWith("_"))
                        {
                            string dataKey = line.Substring(0, indexOfEquals);

                            // Normalize data key
                            // It's something like "_NAME_DataKey="
                            dataKey = dataKey.Substring(Math.Min(Name.Length + 2, dataKey.Length));

                            string dataValue = line.Substring(indexOfEquals + 1);
                            Data.Add(new KeyValuePair<string, string>(dataKey, dataValue));
                        }
                        else
                        {
                            throw new Exception();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                throw new Exception();
            }
        }
    }
}
