using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarFiles.Serialization;

namespace WarFiles
{
    public class ScriptFile
    {
        public uint PreHeader { get; set; }
        public uint FileVersion { get; set; }
        public string Comment { get; set; }
        public string Header { get; set; }
        public List<string> Triggers { get; set; }

        public ScriptFile()
        {
            Triggers = new List<string>();
        }

        public void Deserialize(BinaryReader reader, TriggerFile triggerFile)
        {
            uint version = reader.ReadUInt32();
            if (version != 0x80000004)
            {
                if (version == 1 || version == 0)
                {
                    FileVersion = version;

                    if (version == 1)
                    {
                        Comment = SerializeHelper.ReadString(reader);

                        uint headerSize = reader.ReadUInt32();
                        Header = new string(SerializeHelper.ReadCharArr(reader, headerSize));
                    }

                    // Skip 4 bytes
                    reader.ReadBytes(4);

                    uint triggerCount = reader.ReadUInt32();

                    for (int i = 0; i < triggerCount; i++)
                    {
                        uint triggerSize = reader.ReadUInt32();
                        string trigger = new string(SerializeHelper.ReadCharArr(reader, triggerSize));
                        Triggers.Add(trigger);
                    }
                }
                else
                {
                    throw new Exception("Invalid file format.");
                }
            }
            else
            {
                PreHeader = version;
                FileVersion = reader.ReadUInt32();
                if (FileVersion != 0 && FileVersion != 1)
                    throw new Exception("Invalid file format.");

                if (FileVersion == 1)
                {
                    Comment = SerializeHelper.ReadString(reader);

                    uint headerSize = reader.ReadUInt32();
                    Header = new string(SerializeHelper.ReadCharArr(reader, headerSize));
                }

                for (int i = 0; i < triggerFile.Triggers.Count; i++)
                {
                    Trigger t = triggerFile.Triggers[i];

                    if (!t.IsComment)
                    {
                        uint triggerSize = reader.ReadUInt32();

                        if (triggerSize > 0)
                        {
                            t.CustomText = new string(SerializeHelper.ReadCharArr(reader, triggerSize));
                        }
                    }
                }
            }
        }
    }
}