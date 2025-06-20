using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarFiles.Serialization;

namespace WarFiles
{
    public enum ElementType : uint
    {
        Map = 1,
        Library = 2,
        Category = 4,
        GUI = 8,
        Comment = 16,
        Script = 32,
        Variable = 64
    }

    public class TriggerFile
    {
        public uint FormatVersion { get; set; }
        public uint[] DeletedMapIds { get; set; }
        public uint[] DeletedLibraryIds { get; set; }
        public uint[] DeletedCategoryIds { get; set; }
        public uint[] DeletedTriggerIds { get; set; }
        public uint[] DeletedCommentIds { get; set; }
        public uint[] DeletedScriptIds { get; set; }
        public uint[] DeletedVariableIds { get; set; }
        public uint SubVersion { get; set; }
        public List<TriggerCategory> Categories { get; set; }
        public List<TriggerVariable> Variables { get; set; }
        public List<Trigger> Triggers { get; set; }

        public void Deserialize(BinaryReader reader, TriggerData triggerData)
        {
            // Should be 'WTG!'
            FourCC header = FourCC.Deserialize(reader);

            uint version = reader.ReadUInt32();

            if (version == 0x80000004)
            {
                FormatVersion = reader.ReadUInt32();
                Deserialize_131(reader, triggerData);
            }
            else
            {
                FormatVersion = version;
                throw new NotImplementedException();
            }
        }

        private void Deserialize_131(BinaryReader reader, TriggerData triggerData)
        {
            DeletedMapIds = DeserializeDeletedIds(reader);
            DeletedLibraryIds = DeserializeDeletedIds(reader);
            DeletedCategoryIds = DeserializeDeletedIds(reader);
            DeletedTriggerIds = DeserializeDeletedIds(reader);
            DeletedCommentIds = DeserializeDeletedIds(reader);
            DeletedScriptIds = DeserializeDeletedIds(reader);
            DeletedVariableIds = DeserializeDeletedIds(reader);

            uint unknown1 = reader.ReadUInt32();
            uint unknown2 = reader.ReadUInt32();

            SubVersion = reader.ReadUInt32();

            uint numVariables = reader.ReadUInt32();
            if (numVariables > 0)
            {
                Variables = new List<TriggerVariable>();
                for (uint i = 0; i < numVariables; i++)
                {
                    TriggerVariable variable = new TriggerVariable();
                    variable.Deserialize(reader, FormatVersion);

                    Variables.Add(variable);
                }
            }

            uint numElements = reader.ReadUInt32();

            Categories = new List<TriggerCategory>();
            Triggers = new List<Trigger>();

            for (uint i = 0; i < numElements; i++)
            {
                ElementType elementType = (ElementType)reader.ReadUInt32();
                switch (elementType)
                {
                    case ElementType.Map:
                    case ElementType.Library:
                    case ElementType.Category:
                        {
                            TriggerCategory category = new TriggerCategory();
                            category.Deserialize(reader, FormatVersion, true);

                            Categories.Add(category);
                        }
                        break;
                    case ElementType.GUI:
                    case ElementType.Comment:
                    case ElementType.Script:
                        {
                            Trigger trigger = new Trigger(elementType);
                            trigger.Deserialize(reader, FormatVersion, true, triggerData);

                            Triggers.Add(trigger);
                        }
                        break;
                    case ElementType.Variable:
                        {
                            uint variableId = reader.ReadUInt32();
                            string variableName = SerializeHelper.ReadString(reader);
                            uint variableParentId = reader.ReadUInt32();
                        }
                        break;
                }
            }
        }

        private uint[] DeserializeDeletedIds(BinaryReader reader)
        {
            uint skip = reader.ReadUInt32();

            uint numDeletedIds = reader.ReadUInt32();
            uint[] result = new uint[numDeletedIds];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = reader.ReadUInt32();
            }

            return result;
        }
    }

    public class TriggerCategory
    {
        public uint Id { get; set; }
        public uint ParentId { get; set; }
        public string Name { get; set; }
        public bool IsComment { get; set; }
        public bool IsExpanded { get; set; }

        public void Deserialize(BinaryReader reader, uint formatVersion, bool is131)
        {
            if (is131)
            {
                Deserialize_131(reader, formatVersion);
            }
            else
            {
                
                //Index = reader.ReadUInt32();
                //Name = SerializeHelper.ReadString(reader);

                //if (formatVersion == 7)
                //{
                //    IsComment = (reader.ReadUInt32() != 0);
                //}
            }
        }

        private void Deserialize_131(BinaryReader reader, uint formatVersion)
        {
            Id = reader.ReadUInt32();
            Name = SerializeHelper.ReadString(reader);
            
            if (formatVersion == 7)
            {
                IsComment = (reader.ReadUInt32() != 0);
            }

            IsExpanded = (reader.ReadUInt32() != 0);
            ParentId = reader.ReadUInt32();
        }
    }

    public class TriggerVariable
    {
        public uint Id { get; set; }
        public uint ParentId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsArray { get; set; }
        public uint ArraySize { get; set; }
        public bool IsInitialized { get; set; }
        public string InitialValue { get; set; }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            Name = SerializeHelper.ReadString(reader);
            Type = SerializeHelper.ReadString(reader);

            uint unknown = reader.ReadUInt32();

            IsArray = (reader.ReadUInt32() != 0);

            if (formatVersion == 7)
            {
                ArraySize = reader.ReadUInt32();
            }

            IsInitialized = (reader.ReadUInt32() != 0);
            InitialValue = SerializeHelper.ReadString(reader);
            Id = reader.ReadUInt32();
            ParentId = reader.ReadUInt32();
        }
    }

    public class Trigger
    {
        public ElementType ElementType { get; set; }
        public uint Id { get; set; }
        public uint ParentId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CustomText { get; set; }
        public bool IsComment { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsCustomText { get; set; }
        public bool InitiallyOn { get; set; }
        public bool RunOnMapInitialization { get; set; }
        public List<GUIFunction> GUIFunctions { get; set; }

        public Trigger(ElementType elementType)
        {
            this.ElementType = elementType;
        }

        public void Deserialize(BinaryReader reader, uint formatVersion, bool use131, TriggerData triggerData)
        {
            if (use131)
            {
                Deserialize_131(reader, formatVersion, triggerData);
            }
            else
            {
                //Name = SerializeHelper.ReadString(reader);
                //Description = SerializeHelper.ReadString(reader);

                //if (formatVersion == 7)
                //{
                //    IsComment = (reader.ReadUInt32() != 0);
                //}

                //IsEnabled = (reader.ReadUInt32() != 0);
                //IsCustomText = (reader.ReadUInt32() != 0);
                //InitiallyOn = (reader.ReadUInt32() != 0);
                //CategoryIndex = reader.ReadUInt32();

                //uint numGUIFunctions = reader.ReadUInt32();

                //if (numGUIFunctions > 0)
                //{
                //    GUIFunctions = new List<GUIFunction>();
                //    for (uint i = 0; i < numGUIFunctions; i++)
                //    {
                //        GUIFunction guiFunction = new GUIFunction();
                //        guiFunction.Deserialize(reader, formatVersion, false);

                //        GUIFunctions.Add(guiFunction);
                //    }
                //}
            }
        }

        private void Deserialize_131(BinaryReader reader, uint formatVersion, TriggerData triggerData)
        {
            Name = SerializeHelper.ReadString(reader);
            Description = SerializeHelper.ReadString(reader);

            if (formatVersion == 7)
            {
                IsComment = (reader.ReadUInt32() != 0);
            }

            Id = reader.ReadUInt32();
            IsEnabled = (reader.ReadUInt32() != 0);
            IsCustomText = (reader.ReadUInt32() != 0);
            InitiallyOn = (reader.ReadUInt32() == 0);
            RunOnMapInitialization = (reader.ReadUInt32() != 0);
            ParentId = reader.ReadUInt32();

            uint numGUIFunctions = reader.ReadUInt32();

            if (numGUIFunctions > 0)
            {
                GUIFunctions = new List<GUIFunction>();
                for (uint i = 0; i < numGUIFunctions; i++)
                {
                    GUIFunction guiFunction = new GUIFunction();
                    guiFunction.Deserialize(reader, formatVersion, false, true, triggerData);

                    GUIFunctions.Add(guiFunction);
                }
            }
        }
    }

    public enum GUIFunctionType : uint
    {
        Event = 0,
        Condition = 1,
        Action = 2
    }

    public class GUIFunction
    {
        public GUIFunctionType FunctionType { get; set; }
        public uint GroupNumber { get; set; }
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public GUIParam[] Params { get; set; }
        public uint Data1 { get; set; }
        public List<GUIFunction> Children { get; set; }

        public void Deserialize(BinaryReader reader, uint formatVersion, bool isChild, bool use131, TriggerData triggerData)
        {
            if (use131)
            {
                Deserialize_131(reader, formatVersion, isChild, triggerData);
            }
            else
            {
                //FunctionType = (GUIFunctionType)reader.ReadUInt32();

                //if (isChild)
                //{
                //    GroupNumber = reader.ReadUInt32();
                //}

                //Name = SerializeHelper.ReadString(reader);
                //IsEnabled = (reader.ReadUInt32() != 0);

                //uint paramCount = reader.ReadUInt32();

                //Params = new GUIParam[paramCount];
                //for (uint i = 0; i < paramCount; i++)
                //{
                //    Params[i] = new GUIParam();
                //    Params[i].Deserialize(reader, formatVersion);
                //}

                //if (formatVersion == 7)
                //{
                //    // Not sure if numChildren should be read here or not regardless of format
                //    uint numChildren = reader.ReadUInt32();

                //    if (numChildren > 0)
                //    {
                //        Children = new List<GUIFunction>();
                //        for (uint i = 0; i < numChildren; i++)
                //        {
                //            GUIFunction child = new GUIFunction();
                //            child.Deserialize(reader, formatVersion, true, use131);
                //            Children.Add(child);
                //        }
                //    }
                //}
            }
        }

        private void Deserialize_131(BinaryReader reader, uint formatVersion, bool isChild, TriggerData triggerData)
        {
            FunctionType = (GUIFunctionType)reader.ReadUInt32();

            if (isChild)
            {
                GroupNumber = reader.ReadUInt32();
            }

            Name = SerializeHelper.ReadString(reader);
            IsEnabled = (reader.ReadUInt32() != 0);

            uint paramCount = triggerData.GetArgCount(Name);

            Params = new GUIParam[paramCount];
            for (uint i = 0; i < paramCount; i++)
            {
                Params[i] = new GUIParam();
                Params[i].Deserialize(reader, formatVersion, triggerData);
            }

            if (formatVersion == 7)
            {
                uint numChildren = reader.ReadUInt32();

                if (numChildren > 0)
                {
                    Children = new List<GUIFunction>();
                    for (uint i = 0; i < numChildren; i++)
                    {
                        GUIFunction child = new GUIFunction();
                        child.Deserialize_131(reader, formatVersion, true, triggerData);
                        Children.Add(child);
                    }
                }
            }
        }
    }

    public enum GUIParamType : uint
    {
        Preset = 0,
        Variable = 1,
        Function = 2,
        String = 3
    }

    public class GUIParam
    {
        public GUIParamType ParamType { get; set; }
        public string Value { get; set; }
        public bool HasSubParameter { get; set; }
        public bool IsArray { get; set; }
        public GUISubParameter SubParameter { get; set; }
        public List<GUIParam> Children { get; set; }

        public void Deserialize(BinaryReader reader, uint fileVersion, TriggerData triggerData)
        {
            ParamType = (GUIParamType)reader.ReadUInt32();
            Value = SerializeHelper.ReadString(reader);
            HasSubParameter = (reader.ReadUInt32() != 0);
            if (HasSubParameter)
            {
                SubParameter = new GUISubParameter();
                SubParameter.Deserialize(reader, fileVersion, triggerData);
            }

            if (fileVersion == 4)
            {
                if (ParamType == GUIParamType.Function)
                {
                    uint unknown = reader.ReadUInt32();
                }
                else
                {
                    IsArray = (reader.ReadUInt32() != 0);
                }
            }
            else
            {
                if (HasSubParameter)
                {
                    uint unknown = reader.ReadUInt32();
                }

                IsArray = (reader.ReadUInt32() != 0);
            }

            if (IsArray)
            {
                GUIParam child = new GUIParam();
                child.Deserialize(reader, fileVersion, triggerData);

                Children = new List<GUIParam>() { child };
            }
        }
    }

    public enum GUISubParameterType : uint
    {
        Events = 0,
        Conditions = 1,
        Actions = 2,
        Calls = 3
    }

    public class GUISubParameter
    {
        public GUISubParameterType SubParamType { get; set; }
        public string Name { get; set; }
        public bool BeginParameters { get; set; }
        public List<GUIParam> Parameters { get; set; }

        public void Deserialize(BinaryReader reader, uint fileVersion, TriggerData triggerData)
        {
            SubParamType = (GUISubParameterType)reader.ReadUInt32();
            Name = SerializeHelper.ReadString(reader);
            BeginParameters = (reader.ReadUInt32() != 0);
            if (BeginParameters)
            {
                uint paramCount = triggerData.GetArgCount(Name);

                Parameters = new List<GUIParam>();
                for (uint i = 0; i < paramCount; i++)
                {
                    GUIParam param = new GUIParam();
                    param.Deserialize(reader, fileVersion, triggerData);

                    Parameters.Add(param);
                }
            }
        }
    }
}