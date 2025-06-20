using WarTransfer.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WarTransfer.FileTypes
{
    [Flags]
    public enum W3iFlags : uint
    {
        HideMinimapInPreviewScreens = 1 << 0,
        ModifyAllyPriorities = 1 << 1,
        MeleeMap = 1 << 2,
        Unknown1 = 1 << 3,  // playable map size was large and has never been reduced to medium ?
        MaskedAreasPartiallyVisible = 1 << 4,
        CustomForcesFixedPlayerSettings = 1 << 5,
        UseCustomForces = 1 << 6,
        UseCustomTechtree = 1 << 7,
        UseCustomAbilities = 1 << 8,
        UseCustomUpgrades = 1 << 9,
        MapPropsOpenedInEditor = 1 << 10,
        ShowWaterWavesOnCliffShores = 1 << 11,
        ShowWaterWavesOnRollingShores = 1 << 12,
        Unknown2 = 1 << 13,
        Unknown3 = 1 << 14,
        Unknown4 = 1 << 15
    }

    public class W3iFile
    {
        public uint FormatVersion { get; set; }
        public uint MapVersion { get; set; }
        public uint EditorVersion { get; set; }
        public uint GameVersionMajor { get; set; }
        public uint GameVersionMinor { get; set; }
        public uint GameVersionPatch { get; set; }
        public uint GameVersionBuild { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string SuggestedPlayers { get; set; }
        public float[] CameraLeftBottom { get; set; }
        public float[] CameraRightTop { get; set; }
        public float[] CameraLeftTop { get; set; }
        public float[] CameraRightBottom { get; set; }
        public int[] CameraCompliments { get; set; }
        public uint PlayableWidth { get; set; }
        public uint PlayableHeight { get; set; }
        public W3iFlags Flags { get; set; }
        public char TileSet { get; set; }
        public uint LoadingScreenId { get; set; }
        public string LoadingScreenModel { get; set; }
        public string LoadingScreenText { get; set; }
        public string LoadingScreenTitle { get; set; }
        public string LoadingScreenSubtitle { get; set; }
        public uint GameDataSet { get; set; }
        public uint MapLoadingScreenId { get; set; }
        public string PrologueScreenModel { get; set; }
        public string PrologueText { get; set; }
        public string PrologueTitle { get; set; }
        public string PrologueSubtitle { get; set; }
        public uint FogStyle { get; set; }
        public float FogStartZHeight { get; set; }
        public float FogEndZHeight { get; set; }
        public float FogDensity { get; set; }
        public byte FogColorRed { get; set; }
        public byte FogColorGreen { get; set; }
        public byte FogColorBlue { get; set; }
        public byte FogColorAlpha { get; set; }
        public FourCC WeatherId { get; set; }
        public string CustomSoundEnvironment { get; set; }
        public byte CustomLightTileset { get; set; }
        public byte WaterTintRed { get; set; }
        public byte WaterTintGreen { get; set; }
        public byte WaterTintBlue { get; set; }
        public byte WaterTintAlpha { get; set; }
        public uint ScriptTypeId { get; set; }
        public uint SupportedModes { get; set; }
        public uint GameDataVersion { get; set; }
        public uint PlayerCount { get; set; }
        public Player[] Players { get; set; }
        public uint ForcesCount { get; set; }
        public Force[] Forces { get; set; }
        public uint AvailableUpgradesCount { get; set; }
        public AvailableUpgrade[] AvailableUpgrades { get; set; }
        public uint AvailableTechCount { get; set; }
        public AvailableTech[] AvailableTechs { get; set; }
        public uint RandomUnitsTableCount { get; set; }
        public RandomUnitsTable[] RandomUnitsTables { get; set; }
        public uint RandomItemsTableCount { get; set; }
        public RandomItemsTable[] RandomItemsTables { get; set; }

        public static W3iFile Read(BinaryReader reader)
        {
            W3iFile file = new W3iFile();
            file.Deserialize(reader);
            return file;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(this.FormatVersion);
            writer.Write(this.MapVersion);
            writer.Write(this.EditorVersion);

            if (this.FormatVersion >= 28)
            {
                writer.Write(this.GameVersionMajor);
                writer.Write(this.GameVersionMinor);
                writer.Write(this.GameVersionPatch);
                writer.Write(this.GameVersionBuild);
            }

            SerializeHelper.WriteString(writer, this.Name);
            SerializeHelper.WriteString(writer, this.Author);
            SerializeHelper.WriteString(writer, this.Description);
            SerializeHelper.WriteString(writer, this.SuggestedPlayers);
            SerializeHelper.WriteArray(writer, this.CameraLeftBottom);
            SerializeHelper.WriteArray(writer, this.CameraRightTop);
            SerializeHelper.WriteArray(writer, this.CameraLeftTop);
            SerializeHelper.WriteArray(writer, this.CameraRightBottom);
            SerializeHelper.WriteArray(writer, this.CameraCompliments);
            writer.Write(this.PlayableWidth);
            writer.Write(this.PlayableHeight);
            writer.Write((uint)this.Flags);
            writer.Write(this.TileSet);
            writer.Write(this.LoadingScreenId);

            if (this.FormatVersion >= 25)
            {
                SerializeHelper.WriteString(writer, this.LoadingScreenModel);
            }

            SerializeHelper.WriteString(writer, this.LoadingScreenText);
            SerializeHelper.WriteString(writer, this.LoadingScreenTitle);
            SerializeHelper.WriteString(writer, this.LoadingScreenSubtitle);

            if (this.FormatVersion >= 25)
            {
                writer.Write(this.GameDataSet);
            }

            if (this.FormatVersion <= 18)
            {
                writer.Write(this.MapLoadingScreenId);
            }

            if (this.FormatVersion >= 25)
            {
                SerializeHelper.WriteString(writer, this.PrologueScreenModel);
            }

            SerializeHelper.WriteString(writer, this.PrologueText);
            SerializeHelper.WriteString(writer, this.PrologueTitle);
            SerializeHelper.WriteString(writer, this.PrologueSubtitle);

            if (this.FormatVersion >= 25)
            {
                writer.Write(this.FogStyle);
                writer.Write(this.FogStartZHeight);
                writer.Write(this.FogEndZHeight);
                writer.Write(this.FogDensity);
                writer.Write(this.FogColorRed);
                writer.Write(this.FogColorGreen);
                writer.Write(this.FogColorBlue);
                writer.Write(this.FogColorAlpha);
                this.WeatherId.Serialize(writer);
                SerializeHelper.WriteString(writer, this.CustomSoundEnvironment);
                writer.Write(this.CustomLightTileset);
                writer.Write(this.WaterTintRed);
                writer.Write(this.WaterTintGreen);
                writer.Write(this.WaterTintBlue);
                writer.Write(this.WaterTintAlpha);

                if (this.FormatVersion >= 28)
                {
                    writer.Write(this.ScriptTypeId);
                    if (this.FormatVersion >= 31)
                    {
                        writer.Write(this.SupportedModes);
                        writer.Write(this.GameDataVersion);
                    }
                }
            }

            writer.Write(this.PlayerCount);
            for (int i = 0; i < this.PlayerCount; i++)
            {
                this.Players[i].Serialize(writer, this.FormatVersion);
            }

            writer.Write(this.ForcesCount);
            for (int i = 0; i < this.ForcesCount; i++)
            {
                this.Forces[i].Serialize(writer, this.FormatVersion);
            }

            writer.Write(this.AvailableUpgradesCount);
            for (int i = 0; i < this.AvailableUpgradesCount; i++)
            {
                this.AvailableUpgrades[i].Serialize(writer, this.FormatVersion);
            }

            writer.Write(this.AvailableTechCount);
            for (int i = 0; i < this.AvailableTechCount; i++)
            {
                this.AvailableTechs[i].Serialize(writer, this.FormatVersion);
            }

            writer.Write(this.RandomUnitsTableCount);
            for (int i = 0; i < this.RandomUnitsTableCount; i++)
            {
                this.RandomUnitsTables[i].Serialize(writer, this.FormatVersion);
            }

            if (this.FormatVersion >= 25)
            {
                writer.Write(this.RandomItemsTableCount);
                for (int i = 0; i < this.RandomItemsTableCount; i++)
                {
                    this.RandomItemsTables[i].Serialize(writer, this.FormatVersion);
                }
            }
        }

        public void Deserialize(BinaryReader reader)
        {
            this.FormatVersion = reader.ReadUInt32();
            this.MapVersion = reader.ReadUInt32();
            this.EditorVersion = reader.ReadUInt32();

            if (this.FormatVersion >= 28)
            {
                this.GameVersionMajor = reader.ReadUInt32();
                this.GameVersionMinor = reader.ReadUInt32();
                this.GameVersionPatch = reader.ReadUInt32();
                this.GameVersionBuild = reader.ReadUInt32();
            }

            this.Name = SerializeHelper.ReadString(reader);
            this.Author = SerializeHelper.ReadString(reader);
            this.Description = SerializeHelper.ReadString(reader);
            this.SuggestedPlayers = SerializeHelper.ReadString(reader);
            this.CameraLeftBottom = SerializeHelper.ReadFloatArr(reader, 2);
            this.CameraRightTop = SerializeHelper.ReadFloatArr(reader, 2);
            this.CameraLeftTop = SerializeHelper.ReadFloatArr(reader, 2);
            this.CameraRightBottom = SerializeHelper.ReadFloatArr(reader, 2);
            this.CameraCompliments = SerializeHelper.ReadIntArr(reader, 4);
            this.PlayableWidth = reader.ReadUInt32();
            this.PlayableHeight = reader.ReadUInt32();
            this.Flags = (W3iFlags)reader.ReadUInt32();
            this.TileSet = reader.ReadChar();
            this.LoadingScreenId = reader.ReadUInt32();

            if (this.FormatVersion >= 25)
            {
                this.LoadingScreenModel = SerializeHelper.ReadString(reader);
            }

            this.LoadingScreenText = SerializeHelper.ReadString(reader);
            this.LoadingScreenTitle = SerializeHelper.ReadString(reader);
            this.LoadingScreenSubtitle = SerializeHelper.ReadString(reader);

            if (this.FormatVersion >= 25)
            {
                this.GameDataSet = reader.ReadUInt32();
            }

            if (this.FormatVersion <= 18)
            {
                this.MapLoadingScreenId = reader.ReadUInt32();
            }

            if (this.FormatVersion >= 25)
            {
                this.PrologueScreenModel = SerializeHelper.ReadString(reader);
            }

            this.PrologueText = SerializeHelper.ReadString(reader);
            this.PrologueTitle = SerializeHelper.ReadString(reader);
            this.PrologueSubtitle = SerializeHelper.ReadString(reader);

            if (this.FormatVersion >= 25)
            {
                this.FogStyle = reader.ReadUInt32();
                this.FogStartZHeight = reader.ReadSingle();
                this.FogEndZHeight = reader.ReadSingle();
                this.FogDensity = reader.ReadSingle();
                this.FogColorRed = reader.ReadByte();
                this.FogColorGreen = reader.ReadByte();
                this.FogColorBlue = reader.ReadByte();
                this.FogColorAlpha = reader.ReadByte();
                this.WeatherId = FourCC.Deserialize(reader);
                this.CustomSoundEnvironment = SerializeHelper.ReadString(reader);
                this.CustomLightTileset = reader.ReadByte();
                this.WaterTintRed = reader.ReadByte();
                this.WaterTintGreen = reader.ReadByte();
                this.WaterTintBlue = reader.ReadByte();
                this.WaterTintAlpha = reader.ReadByte();

                if (this.FormatVersion >= 28)
                {
                    this.ScriptTypeId = reader.ReadUInt32();
                    if (this.FormatVersion >= 31)
                    {
                        this.SupportedModes = reader.ReadUInt32();
                        this.GameDataVersion = reader.ReadUInt32();
                    }
                }
            }

            this.PlayerCount = reader.ReadUInt32();
            this.Players = new Player[this.PlayerCount];
            for (int i = 0; i < this.PlayerCount; i++)
            {
                this.Players[i] = new Player();
                this.Players[i].Deserialize(reader, this.FormatVersion);
            }

            this.ForcesCount = reader.ReadUInt32();
            this.Forces = new Force[this.ForcesCount];
            for (int i = 0; i < this.ForcesCount; i++)
            {
                this.Forces[i] = new Force();
                this.Forces[i].Deserialize(reader, this.FormatVersion);
            }

            this.AvailableUpgradesCount = reader.ReadUInt32();
            this.AvailableUpgrades = new AvailableUpgrade[this.AvailableUpgradesCount];
            for (int i = 0; i < this.AvailableUpgradesCount; i++)
            {
                this.AvailableUpgrades[i] = new AvailableUpgrade();
                this.AvailableUpgrades[i].Deserialize(reader, this.FormatVersion);
            }

            this.AvailableTechCount = reader.ReadUInt32();
            this.AvailableTechs = new AvailableTech[this.AvailableTechCount];
            for (int i = 0; i < this.AvailableTechCount; i++)
            {
                this.AvailableTechs[i] = new AvailableTech();
                this.AvailableTechs[i].Deserialize(reader, this.FormatVersion);
            }

            this.RandomUnitsTableCount = reader.ReadUInt32();
            this.RandomUnitsTables = new RandomUnitsTable[this.RandomUnitsTableCount];
            for (int i = 0; i < this.RandomUnitsTableCount; i++)
            {
                this.RandomUnitsTables[i] = new RandomUnitsTable();
                this.RandomUnitsTables[i].Deserialize(reader, this.FormatVersion);
            }

            if (this.FormatVersion >= 25)
            {
                this.RandomItemsTableCount = reader.ReadUInt32();
                this.RandomItemsTables = new RandomItemsTable[this.RandomItemsTableCount];
                for (int i = 0; i < this.RandomItemsTableCount; i++)
                {
                    this.RandomItemsTables[i] = new RandomItemsTable();
                    this.RandomItemsTables[i].Deserialize(reader, this.FormatVersion);
                }
            }
        }
    }

    public class Player
    {
        public uint InternalNumber { get; set; }
        public uint TypeId { get; set; }
        public uint RaceId { get; set; }
        public uint FixedStartLocation { get; set; }
        public string Name { get; set; }
        public float StartingPosX { get; set; }
        public float StartingPosY { get; set; }
        public uint AllyLowPrioritiesFlag { get; set; }
        public uint AllyHighPrioritiesFlag { get; set; }
        public uint EnemyLowPrioritiesFlag { get; set; }
        public uint EnemyHighPrioritiesFlag { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.InternalNumber);
            writer.Write(this.TypeId);
            writer.Write(this.RaceId);
            writer.Write(this.FixedStartLocation);
            SerializeHelper.WriteString(writer, this.Name);
            writer.Write(this.StartingPosX);
            writer.Write(this.StartingPosY);
            writer.Write(this.AllyLowPrioritiesFlag);
            writer.Write(this.AllyHighPrioritiesFlag);

            if (formatVersion >= 31)
            {
                writer.Write(this.EnemyLowPrioritiesFlag);
                writer.Write(this.EnemyHighPrioritiesFlag);
            }
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.InternalNumber = reader.ReadUInt32();
            this.TypeId = reader.ReadUInt32();
            this.RaceId = reader.ReadUInt32();
            this.FixedStartLocation = reader.ReadUInt32();
            this.Name = SerializeHelper.ReadString(reader);
            this.StartingPosX = reader.ReadSingle();
            this.StartingPosY = reader.ReadSingle();
            this.AllyLowPrioritiesFlag = reader.ReadUInt32();
            this.AllyHighPrioritiesFlag = reader.ReadUInt32();

            if (formatVersion >= 31)
            {
                this.EnemyLowPrioritiesFlag = reader.ReadUInt32();
                this.EnemyHighPrioritiesFlag = reader.ReadUInt32();
            }
        }
    }

    public class Force
    {
        public uint FocusFlags { get; set; }
        public uint PlayerMasks { get; set; }
        public string Name { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.FocusFlags);
            writer.Write(this.PlayerMasks);
            SerializeHelper.WriteString(writer, this.Name);
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.FocusFlags = reader.ReadUInt32();
            this.PlayerMasks = reader.ReadUInt32();
            this.Name = SerializeHelper.ReadString(reader);
        }
    }

    public class AvailableUpgrade
    {
        public uint PlayerFlags { get; set; }
        public FourCC UpgradeId { get; set; }
        public uint Level { get; set; }
        public uint Availability { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.PlayerFlags);
            this.UpgradeId.Serialize(writer);
            writer.Write(this.Level);
            writer.Write(this.Availability);
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.PlayerFlags = reader.ReadUInt32();
            this.UpgradeId = FourCC.Deserialize(reader);
            this.Level = reader.ReadUInt32();
            this.Availability = reader.ReadUInt32();
        }
    }

    public class AvailableTech
    {
        public uint PlayerFlags { get; set; }
        public FourCC TechId { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.PlayerFlags);
            this.TechId.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.PlayerFlags = reader.ReadUInt32();
            this.TechId = FourCC.Deserialize(reader);
        }
    }

    public class RandomUnitsTable
    {
        public uint TableNumber { get; set; }
        public string TableName { get; set; }
        public uint PositionsCount { get; set; }
        public uint[] Positions { get; set; }
        public uint LinesCount { get; set; }
        public RandomUnitsTableLine[] Lines { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.TableNumber);
            SerializeHelper.WriteString(writer, this.TableName);
            writer.Write(this.PositionsCount);
            for (int i = 0; i < this.PositionsCount; i++)
            {
                writer.Write(this.Positions[i]);
            }
            writer.Write(this.LinesCount);
            for (int i = 0; i < this.LinesCount; i++)
            {
                this.Lines[i].Serialize(writer, formatVersion);
            }
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.TableNumber = reader.ReadUInt32();
            this.TableName = SerializeHelper.ReadString(reader);

            this.PositionsCount = reader.ReadUInt32();
            this.Positions = new uint[this.PositionsCount];
            for (int i = 0; i < this.PositionsCount; i++)
            {
                this.Positions[i] = reader.ReadUInt32();
            }

            this.LinesCount = reader.ReadUInt32();
            this.Lines = new RandomUnitsTableLine[this.LinesCount];
            for (int i = 0; i < this.LinesCount; i++)
            {
                this.Lines[i] = new RandomUnitsTableLine();
                this.Lines[i].Deserialize(reader, formatVersion, this.PositionsCount);
            }
        }

        public RandomUnitsTable Clone()
        {
            RandomUnitsTable clone = new RandomUnitsTable();
            clone.TableNumber = this.TableNumber;
            clone.TableName = this.TableName;
            clone.PositionsCount = this.PositionsCount;
            clone.Positions = new uint[this.Positions.Length];
            clone.LinesCount = this.LinesCount;
            clone.Lines = new RandomUnitsTableLine[this.Lines.Length];

            for (int i = 0; i < this.Positions.Length; i++)
            {
                clone.Positions[i] = this.Positions[i];
            }

            for (int i = 0; i < this.Lines.Length; i++)
            {
                clone.Lines[i] = this.Lines[i].Clone();
            }

            return clone;
        }
    }

    public class RandomUnitsTableLine
    {
        public uint Chance { get; set; }
        public FourCC[] PositionIds { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.Chance);
            for (int i = 0; i < PositionIds.Length; i++)
            {
                this.PositionIds[i].Serialize(writer);
            }
        }

        public void Deserialize(BinaryReader reader, uint formatVersion, uint positionsCount)
        {
            this.Chance = reader.ReadUInt32();
            this.PositionIds = new FourCC[positionsCount];
            for (int i = 0; i < positionsCount; i++)
            {
                this.PositionIds[i] = FourCC.Deserialize(reader);
            }
        }

        public RandomUnitsTableLine Clone()
        {
            RandomUnitsTableLine clone = new RandomUnitsTableLine();
            clone.Chance = this.Chance;
            clone.PositionIds = new FourCC[this.PositionIds.Length];

            for (int i = 0; i <  this.PositionIds.Length; i++)
            {
                clone.PositionIds[i] = this.PositionIds[i];
            }

            return clone;
        }
    }

    public class RandomItemsTable
    {
        public uint TableId { get; set; }
        public string TableName { get; set; }
        public uint SetCount { get; set; }
        public RandomItemsTableSet[] Sets { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.TableId);
            SerializeHelper.WriteString(writer, this.TableName);
            writer.Write(this.SetCount);
            for (int i = 0; i < this.SetCount; i++)
            {
                this.Sets[i].Serialize(writer, formatVersion);
            }
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.TableId = reader.ReadUInt32();
            this.TableName = SerializeHelper.ReadString(reader);
            this.SetCount = reader.ReadUInt32();
            this.Sets = new RandomItemsTableSet[this.SetCount];
            for (int i = 0; i < this.SetCount; i++)
            {
                this.Sets[i] = new RandomItemsTableSet();
                this.Sets[i].Deserialize(reader, formatVersion);
            }
        }

        public RandomItemsTable Clone()
        {
            RandomItemsTable clone = new RandomItemsTable();
            clone.TableId = this.TableId;
            clone.TableName = this.TableName;
            clone.SetCount = this.SetCount;
            clone.Sets = new RandomItemsTableSet[this.Sets.Length];

            for (int i = 0; i < clone.Sets.Length; i++)
            {
                clone.Sets[i] = this.Sets[i].Clone();
            }

            return clone;
        }
    }

    public class RandomItemsTableSet
    {
        public uint ItemCount { get; set; }
        public RandomItem[] RandomItems { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.ItemCount);
            for (int i = 0; i < this.ItemCount; i++)
            {
                this.RandomItems[i].Serialize(writer, formatVersion);
            }
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.ItemCount = reader.ReadUInt32();
            this.RandomItems = new RandomItem[this.ItemCount];
            for (int i = 0; i < this.ItemCount; i++)
            {
                this.RandomItems[i] = new RandomItem();
                this.RandomItems[i].Deserialize(reader, formatVersion);
            }
        }

        public RandomItemsTableSet Clone()
        {
            RandomItemsTableSet clone = new RandomItemsTableSet();

            clone.ItemCount = this.ItemCount;
            clone.RandomItems = new RandomItem[this.RandomItems.Length];

            for (int i = 0; i < this.RandomItems.Length; i++)
            {
                clone.RandomItems[i] = this.RandomItems[i].Clone();
            }

            return clone;
        }
    }

    public class RandomItem
    {
        public uint Chance { get; set; }
        public FourCC ItemId { get; set; }

        public void Serialize(BinaryWriter writer, uint formatVersion)
        {
            writer.Write(this.Chance);
            this.ItemId.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, uint formatVersion)
        {
            this.Chance = reader.ReadUInt32();
            this.ItemId = FourCC.Deserialize(reader);
        }

        public RandomItem Clone()
        {
            RandomItem clone = new RandomItem();
            clone.Chance = this.Chance;
            clone.ItemId = this.ItemId;

            return clone;
        }
    }
}
