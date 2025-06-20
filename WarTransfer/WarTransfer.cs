using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WarTransfer.FileTypes;
using WarTransfer.Types;

namespace WarTransfer
{
    public class WarTransfer
    {
        private string m_sourceDirectory = null;
        private string m_sourceMap = null;
        private string m_outDirectory = null;
        private WarTransferArgs m_args = null;

        private static readonly string TempSourceDirectory = Path.Combine(Path.GetTempPath(), "WarTransfer", "Source");
        private static readonly string TempTargetDirectory = Path.Combine(Path.GetTempPath(), "WarTransfer", "Target");

        private static readonly string[] War3FilesToCopy = {
            "war3map.doo",
            "war3map.mmp",
            "war3mapMap.blp",
            "war3map.shd",
            "war3map.w3e",
            "war3map.wpm",
            "war3map.blp",
            "war3mapUnits.doo",
        };

        private static readonly Dictionary<string, Regex> JFileFunctionsToReplace = new Dictionary<string, Regex>
        {
            { "config",                         new Regex("^config$", RegexOptions.Compiled) },
            { "main",                           new Regex("^main$", RegexOptions.Compiled) },
            { "CreateAllUnits",                 new Regex("^CreateAllUnits$", RegexOptions.Compiled) },
            { "CreateNeutralPassiveBuildings",  new Regex("^CreateNeutralPassiveBuildings$", RegexOptions.Compiled) },
            { "CreatePlayerBuildings",          new Regex("^CreatePlayerBuildings$", RegexOptions.Compiled) },
            { "CreateNeutralPassive",           new Regex("^CreateNeutralPassive$", RegexOptions.Compiled) },
            { "CreateNeutralHostile",           new Regex("^CreateNeutralHostile$", RegexOptions.Compiled) },
            { "CreatePlayerUnits",              new Regex("^CreatePlayerUnits$", RegexOptions.Compiled) },
            { "CreateUnitsForPlayer",           new Regex("^CreateUnitsForPlayer\\d+$", RegexOptions.Compiled) },
            { "CreateBuildingsForPlayer",       new Regex("^CreateBuildingsForPlayer\\d+$", RegexOptions.Compiled) },
            { "InitCustomPlayerSlots",          new Regex("^InitCustomPlayerSlots$", RegexOptions.Compiled) },
            { "InitCustomTeams",                new Regex("^InitCustomTeams$", RegexOptions.Compiled) },
            { "InitAllyPriorities",             new Regex("^InitAllyPriorities$", RegexOptions.Compiled) },
            { "InitRandomGroups",               new Regex("^InitRandomGroups$", RegexOptions.Compiled) },
            { "CreateRegions",                  new Regex("^CreateRegions$", RegexOptions.Compiled) },
            { "CreateCameras",                  new Regex("^CreateCameras$", RegexOptions.Compiled) },
            { "Unit_DropItems",                 new Regex("^Unit(?:\\d+)_DropItems$", RegexOptions.Compiled) },
            { "ItemTable_DropItems",            new Regex("^ItemTable(?:\\d+)_DropItems$", RegexOptions.Compiled) }
        };

        private static readonly Regex InitSoundsRegex = new Regex("\\s*call InitSounds\\(\\s*\\)\\s*");
        private static readonly Regex InitBlizzardRegex = new Regex("\\s*call InitBlizzard\\(\\s*\\)\\s*");

        public void ExecuteDataTransfer(IWorkflowHandler e, WarTransferArgs args)
        {
            this.m_sourceMap = args.SourceMap;
            this.m_sourceDirectory = Path.GetFullPath(args.SourceDirectory);
            this.m_outDirectory = args.OutputDirectory;
            this.m_args = args;

            if (!MPQUtility.VerifyMPQEditor())
            {
                e.Log(LogType.Error, "Ladik's MPQ editor not found. This is a required dependency.");
            }

            if (!e.KillProcess)
            {
                VersionUtility.UpdateVersion(this.m_sourceMap, args.VersionNumberRegex);

                e.Log(LogType.UserInfo, "Getting ready...");

                if (!Directory.Exists(m_sourceDirectory))
                {
                    e.ReportError($"Unable to find desired source directory: '{m_sourceDirectory}'", true);
                }
            }

            if (!e.KillProcess)
            {
                CreateDirectories(e);
            }

            List<string> maps = null;
            if (!e.KillProcess)
            {
                e.TotalSteps = IOUtility.CountFilesAndDirectories(m_sourceDirectory, "*.w3x", "*.w3m") * 3;

                // Copy files to temp directory
                maps = CopyMapsToDirectory(e, m_sourceDirectory, TempSourceDirectory, TargetMapType.Directory);
            }

            if (!e.KillProcess)
            {
                // Ensure source map is in the same temp directory
                EnsureSourceMap(e);

                if (maps != null && !e.KillProcess)
                {
                    e.TotalSteps = e.TotalSteps - 2;
                    maps.Remove(m_sourceMap);
                }
            }

            if (!e.KillProcess)
            {
                if (maps == null || maps.Count == 0)
                {
                    e.ReportError($"No eligible maps were found in directory \"{m_sourceDirectory}\".", true);
                }
            }

            if (!e.KillProcess)
            {
                e.Log(LogType.UserInfo, "Starting data transfer...");
            }

            if (!e.KillProcess)
            {
                for (int m = 0; m < maps.Count && !e.KillProcess; m++)
                {
                    string currentMap = maps[m];
                    string mapName = Path.GetFileName(currentMap);
                    string targetDirectory = Path.Combine(TempTargetDirectory, mapName);

                    string newMap = VersionUtility.ChangeFileNameVersion(targetDirectory);

                    e.Log(LogType.UserInfo, $"Copying data to map \"{newMap}\".");

                    IOUtility.CopyDirectory(e, m_sourceMap, newMap);

                    if (!e.KillProcess)
                    {
                        CopyFiles(e, currentMap, newMap);
                    }

                    e.CurrentStep++;
                }
            }

            if (!e.KillProcess)
            {
                e.Log(LogType.UserInfo, $"Saving final output maps...");

                CopyMapsToDirectory(e, TempTargetDirectory, m_outDirectory, TargetMapType.File);
            }

            if (!e.KillProcess)
            {
                e.Log(LogType.UserInfo, $"Work Complete!");
            }
        }

        private void CreateDirectories(IWorkflowHandler e)
        {
            IOUtility.DeleteDirectory(e, TempSourceDirectory);
            IOUtility.DeleteDirectory(e, TempTargetDirectory);

            IOUtility.EnsureDirectoryExistence(e, m_outDirectory);
            IOUtility.EnsureDirectoryExistence(e, TempSourceDirectory);
            IOUtility.EnsureDirectoryExistence(e, TempTargetDirectory);
        }

        private void EnsureSourceMap(IWorkflowHandler e)
        {
            string sourceMapName = Path.GetFileName(m_sourceMap);

            if (File.Exists(m_sourceMap))
            {
                string sourceMapCopy = Path.Combine(TempSourceDirectory, Path.GetFileName(m_sourceMap));
                bool success = MPQUtility.MapFileToMapDirectory(e, m_sourceMap, sourceMapCopy);
                if (success)
                {
                    m_sourceMap = sourceMapCopy;
                }
                else
                {
                    e.KillProcess = true;
                }
            }
            else if (Directory.Exists(m_sourceMap))
            {
                bool success = true;
                string desiredLocation = Path.Combine(TempSourceDirectory, sourceMapName);
                if (m_sourceMap != desiredLocation && !Directory.Exists(desiredLocation))
                {
                    success = IOUtility.CopyDirectory(e, m_sourceMap, desiredLocation);
                }

                if (success)
                {
                    m_sourceMap = desiredLocation;
                }
            }
            else
            {
                e.ReportError($"Unable to find source map \"{m_sourceMap}\"", true);
            }
        }

        private List<string> CopyMapsToDirectory(IWorkflowHandler e, string sourceDirectory, string targetDirectory, TargetMapType targetType)
        {
            string[] mapFiles = IOUtility.GetFiles(sourceDirectory, "*.w3x", "*.w3m");
            string[] mapDirectories = IOUtility.GetDirectories(sourceDirectory, "*.w3x", "*.w3m");

            List<string> result = new List<string>(mapFiles.Length + mapDirectories.Length);

            // This code currently updates any w3m files to w3x - shortcut for now.
            // In the future, only modify if transferring w3x -> w3m

            if (mapFiles.Length > 0 && targetType == TargetMapType.Directory)
            {
                for (int m = 0; m < mapFiles.Length && !e.KillProcess; m++)
                {
                    string mapFile = mapFiles[m];
                    string mapName = Path.GetFileNameWithoutExtension(mapFiles[m]);
                    string outMap = Path.Combine(targetDirectory, mapName + ".w3x");

                    e.Log(LogType.Debug, $"Copying map file \"{mapName}\" to \"{outMap}\".");

                    bool success = MPQUtility.MapFileToMapDirectory(e, mapFile, outMap);

                    if (success)
                    {
                        result.Add(outMap);
                    }

                    e.CurrentStep++;
                }
            }
            else
            {
                for (int m = 0; m < mapFiles.Length && !e.KillProcess; m++)
                {
                    string mapFile = mapFiles[m];
                    string mapName = Path.GetFileNameWithoutExtension(mapFile);
                    string outMap = Path.Combine(targetDirectory, mapName + ".w3x");

                    e.Log(LogType.Debug, $"Copying map file \"{mapName}\" to \"{outMap}\".");

                    if (!IOUtility.CopyFile(e, mapFile, outMap))
                    {
                        e.CurrentStep++;

                        continue;
                    }

                    e.CurrentStep++;

                    result.Add(outMap);
                }
            }

            if (mapDirectories.Length > 0 && targetType == TargetMapType.File)
            {
                for (int m = 0; m < mapDirectories.Length && !e.KillProcess; m++)
                {
                    string mapDirectory = mapDirectories[m];
                    string mapName = Path.GetFileNameWithoutExtension(mapDirectory);
                    string outMap = Path.Combine(targetDirectory, mapName + ".w3x");

                    e.Log(LogType.Debug, $"Copying map file \"{mapName}\" to \"{outMap}\".");

                    bool success = MPQUtility.MapDirectoryToMapFile(e, mapDirectory, outMap);

                    if (success)
                    {
                        result.Add(outMap);
                    }

                    e.CurrentStep++;
                }
            }
            else
            {
                for (int m = 0; m < mapDirectories.Length && !e.KillProcess; m++)
                {
                    string mapDirectory = mapDirectories[m];
                    string mapName = Path.GetFileNameWithoutExtension(mapDirectory);
                    string outMap = Path.Combine(targetDirectory, mapName + ".w3x");

                    e.Log(LogType.Debug, $"Copying map file \"{mapName}\" to \"{outMap}\".");

                    if (!IOUtility.CopyDirectory(e, mapDirectory, outMap))
                    {
                        e.CurrentStep++;

                        continue;
                    }

                    e.CurrentStep++;

                    result.Add(outMap);
                }
            }

            return result;
        }

        private List<string> GetWar3FilesToCopy(string mapDirectory)
        {
            List<string> filesList = new List<string>(War3FilesToCopy);

            if (!m_args.TransferCameras)
            {
                filesList.Add("war3map.w3c");
            }

            if (!m_args.TransferRegions)
            {
                filesList.Add("war3map.w3r");
            }

            List<string> files = new List<string>(filesList.Count);

            for (int i = 0; i < filesList.Count; i++)
            {
                string filePath = Path.Combine(mapDirectory, filesList[i]);
                if (File.Exists(filePath))
                {
                    files.Add(filesList[i]);
                }
            }

            return files;
        }

        private void CopyFiles(IWorkflowHandler e, string sourceDirectory, string targetDirectory)
        {
            List<string> filesToCopy = GetWar3FilesToCopy(sourceDirectory);

            for (int f = 0; f < filesToCopy.Count; f++)
            {
                string fileName = filesToCopy[f];
                File.Copy(Path.Combine(sourceDirectory, fileName), Path.Combine(targetDirectory, fileName), true);
            }

            WtsFile newWtsFile = WtsUtility.ReadFile(e, Path.Combine(m_sourceMap, "war3map.wts"));
            WtsFile oldWtsFile = WtsUtility.ReadFile(e, Path.Combine(sourceDirectory, "war3map.wts"));

            if (!e.KillProcess)
            {
                CopyModifiedWtiFile(e, newWtsFile, oldWtsFile, sourceDirectory, targetDirectory);
            }

            if (!e.KillProcess)
            {
                CopyModifiedJFile(e, newWtsFile, oldWtsFile, sourceDirectory, targetDirectory);
            }

            if (!e.KillProcess)
            {
                WtsUtility.WriteFile(Path.Combine(targetDirectory, "war3map.wts"), newWtsFile);
            }
        }

        private void CopyModifiedJFile(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, string sourceDirectory, string targetDirectory)
        {
            JFile sourceMapJ = JUtility.ReadFile(e, Path.Combine(m_sourceMap, "war3map.j"));
            JFile thisMapJ = JUtility.ReadFile(e, Path.Combine(sourceDirectory, "war3map.j"));

            if (!e.KillProcess && !m_args.TransferCameras)
            {
                OverwriteCameraVariables(e, thisMapJ, sourceMapJ);
            }

            if (!e.KillProcess && !m_args.TransferRegions)
            {
                OverwriteRegionVariables(e, thisMapJ, sourceMapJ);
            }

            if (!e.KillProcess)
            {
                OverwriteRandomGroupVariables(e, thisMapJ, sourceMapJ);
            }

            if (!e.KillProcess)
            {
                IEnumerable<FunctionName> sourceMapFuncNames = JUtility.FindFunctionNamesMatching(sourceMapJ, JFileFunctionsToReplace);
                IEnumerable<FunctionName> thisMapFuncNames = JUtility.FindFunctionNamesMatching(thisMapJ, JFileFunctionsToReplace);

                List<FunctionName> functions = sourceMapFuncNames.Concat(thisMapFuncNames).Distinct().ToList();
                List<JFileFunctionNode> functionsToAdd = new List<JFileFunctionNode>();

                for (int i = 0; i < functions.Count && !e.KillProcess; i++)
                {
                    string functionName = functions[i].Name;
                    string patternName = functions[i].PatternName;

                    JFileFunctionNode sourceMapFunction = sourceMapJ.GetFunction(functionName);
                    JFileFunctionNode thisMapFunction = thisMapJ.GetFunction(functionName);

                    if (sourceMapFunction != null && thisMapFunction != null)
                    {
                        if (patternName.Equals("main"))
                        {
                            MergeMains(sourceMapFunction, thisMapFunction);
                        }

                        UpdateJFileStrings(e, newWtsFile, oldWtsFile, sourceMapFunction, thisMapFunction);
                        sourceMapFunction.Parent.ReplaceChild(sourceMapFunction, thisMapFunction);
                    }
                    else if (sourceMapFunction != null)
                    {
                        if (patternName.Equals("CreateRegions") && m_args.TransferRegions)
                            continue;

                        if (patternName.Equals("CreateCameras") && m_args.TransferCameras)
                            continue;

                        bool success = sourceMapFunction.Remove();
                        if (!success)
                        {
                            e.ReportError($"Attempted to remove function '{sourceMapFunction.FunctionName}', but the remove failed!", false);
                        }
                    }
                    else if (thisMapFunction != null)
                    {
                        if (patternName.Equals("CreateRegions"))
                        {
                            if (!m_args.TransferRegions)
                            {
                                InsertFunctionBlock(e, sourceMapJ, thisMapFunction, "Regions");
                            }
                            else
                            {
                                // If we got here, we need to insert an empty function because CreateRegions may be called in the main function.
                                InsertFunctionBlock(e, sourceMapJ, JFileFunctionNode.CreateEmpty(sourceMapJ, "CreateRegions"), "Regions");
                            }
                        }
                        else if (patternName.Equals("CreateCameras"))
                        {
                            if (!m_args.TransferCameras)
                            {
                                InsertFunctionBlock(e, sourceMapJ, thisMapFunction, "Cameras");
                            }
                            else
                            {
                                // If we got here, we need to insert an empty function because CreateCameras may be called in the main function.
                                InsertFunctionBlock(e, sourceMapJ, JFileFunctionNode.CreateEmpty(sourceMapJ, "CreateCameras"), "Cameras");
                            }
                        }
                        else
                        {
                            functionsToAdd.Add(thisMapFunction);
                        }
                    }
                }

                bool functionAdded = true;
                while (functionsToAdd.Count > 0 && functionAdded)
                {
                    functionAdded = false;

                    for (int i = functionsToAdd.Count - 1; i >= 0; i--)
                    {
                        if (JUtility.AddFunctionBeforeUse(sourceMapJ.Root, functionsToAdd[i]))
                        {
                            functionAdded = true;
                            functionsToAdd.RemoveAt(i);
                        }
                    }
                }

                if (functionsToAdd.Count > 0)
                {
                    foreach (JFileFunctionNode node in functionsToAdd)
                    {
                        e.ReportError($"Attempted to insert function '{node.FunctionName}', but no calling function found!", false);
                    }
                }
            }

            if (!e.KillProcess)
            {
                JUtility.WriteFile(Path.Combine(targetDirectory, "war3map.j"), sourceMapJ);
            }
        }

        //private void CopyModifiedJFile(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, string sourceDirectory, string targetDirectory)
        //{
        //    JFile sourceMapJ = JUtility.ReadFile(e, Path.Combine(m_sourceMap, "war3map.j"));
        //    JFile thisMapJ = JUtility.ReadFile(e, Path.Combine(sourceDirectory, "war3map.j"));

        //    if (!e.KillProcess && !m_transferCameras)
        //    {
        //        OverwriteCameraVariables(e, thisMapJ, sourceMapJ);
        //    }

        //    if (!e.KillProcess && !m_transferRegions)
        //    {
        //        OverwriteRegionVariables(e, thisMapJ, sourceMapJ);
        //    }

        //    if (!e.KillProcess)
        //    {
        //        for (int i = 0; i < JFileFunctionsToReplace.Length && !e.KillProcess; i++)
        //        {
        //            Regex functionNamePattern = JFileFunctionsToReplace[i];

        //            List<JFileFunctionNode> sourceMapFunctions = JUtility.FindFunctions(sourceMapJ.Root, functionNamePattern).ToList();
        //            List<JFileFunctionNode> thisMapFunctions = JUtility.FindFunctions(thisMapJ.Root, functionNamePattern).ToList();

        //            JFileFunctionNode sourceMapFunction = JUtility.FindFunction(sourceMapJ.Root, functionNamePattern);
        //            JFileFunctionNode thisMapFunction = JUtility.FindFunction(thisMapJ.Root, functionNamePattern);

        //            if (thisMapFunction != null)
        //            {
        //                string functionName = thisMapFunction.FunctionName;

        //                if (sourceMapFunction != null)
        //                {
        //                    if (functionName.Equals("main"))
        //                    {
        //                        MergeMains(sourceMapFunction, thisMapFunction);
        //                    }

        //                    UpdateJFileStrings(e, newWtsFile, oldWtsFile, sourceMapFunction, thisMapFunction);
        //                    JUtility.ReplaceNode(sourceMapFunction, thisMapFunction);
        //                }
        //                else if (functionName.Equals("CreateRegions"))
        //                {
        //                    if (!m_transferRegions)
        //                    {
        //                        InsertFunctionBlock(e, sourceMapJ, thisMapFunction, "Regions");
        //                    }
        //                }
        //                else if (functionName.Equals("CreateCameras"))
        //                {
        //                    if (!m_transferCameras)
        //                    {
        //                        InsertFunctionBlock(e, sourceMapJ, thisMapFunction, "Cameras");
        //                    }
        //                }
        //                else
        //                {
        //                    bool success = JUtility.AddFunctionBeforeCall(sourceMapJ.Root, thisMapFunction);
        //                    if (!success)
        //                    {
        //                        e.ReportError($"Attempted to insert function '{thisMapFunction.FunctionName}', but no calling function found!", false);
        //                    }
        //                }
        //            }
        //            else if (sourceMapFunction != null)
        //            {
        //                string functionName = sourceMapFunction.FunctionName;

        //                if (functionName.Equals("CreateRegions") && m_transferRegions)
        //                    continue;

        //                if (functionName.Equals("CreateCameras") && m_transferCameras)
        //                    continue;

        //                JUtility.RemoveNode(e, sourceMapFunction);
        //            }
        //        }
        //    }

        //    if (!e.KillProcess)
        //    {
        //        JUtility.WriteFile(Path.Combine(targetDirectory, "war3map.j"), sourceMapJ);
        //    }
        //}


        private void CopyModifiedWtiFile(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, string sourceDirectory, string targetDirectory)
        {
            W3iFile sourceMapW3i = W3iUtility.ReadFile(e, Path.Combine(m_sourceMap, "war3map.w3i"));
            W3iFile newW3iFile = W3iUtility.ReadFile(e, Path.Combine(sourceDirectory, "war3map.w3i"));

            if (!e.KillProcess)
            {
                CopyRandomTables(e, sourceMapW3i, newW3iFile);
            }

            if (!e.KillProcess)
            {
                RemapW3iStringReferences(e, sourceMapW3i, newW3iFile, newWtsFile, oldWtsFile);
                UpdateCustomValues(sourceMapW3i, newW3iFile);

                W3iUtility.WriteFile(e, Path.Combine(targetDirectory, "war3map.w3i"), newW3iFile);
            }
        }

        private void CopyRandomTables(IWorkflowHandler e, W3iFile sourceMapW3i, W3iFile newW3iFile)
        {
            if (m_args.TransferUnitTables)
            {
                newW3iFile.RandomUnitsTableCount = sourceMapW3i.RandomUnitsTableCount;
                newW3iFile.RandomUnitsTables = new RandomUnitsTable[sourceMapW3i.RandomUnitsTables.Length];

                for (int i = 0; i < sourceMapW3i.RandomUnitsTables.Length; i++)
                {
                    newW3iFile.RandomUnitsTables[i] = sourceMapW3i.RandomUnitsTables[i].Clone();
                }
            }

            if (m_args.TransferItemTables)
            {
                newW3iFile.RandomItemsTableCount = sourceMapW3i.RandomItemsTableCount;
                newW3iFile.RandomItemsTables = new RandomItemsTable[sourceMapW3i.RandomItemsTables.Length];

                for (int i = 0; i < sourceMapW3i.RandomItemsTables.Length; i++)
                {
                    newW3iFile.RandomItemsTables[i] = sourceMapW3i.RandomItemsTables[i].Clone();
                }
            }
        }

        private void UpdateCustomValues(W3iFile sourceMapW3i, W3iFile newW3iFile)
        {
            newW3iFile.SupportedModes = sourceMapW3i.SupportedModes;
            newW3iFile.GameDataSet = sourceMapW3i.GameDataSet;
            newW3iFile.GameDataVersion = sourceMapW3i.GameDataVersion;

            // Clear the flags we will copy
            newW3iFile.Flags &= ~m_args.W3iFlagsToTransfer;

            // Copy flags marked to be transferred
            for (int i = 0; i < 32; i++)
            {
                W3iFlags flag = (W3iFlags)(1 << i);
                if ((m_args.W3iFlagsToTransfer & flag) != 0)
                {
                    newW3iFile.Flags |= (sourceMapW3i.Flags & flag);
                }
            }
        }

        private void RemapW3iStringReferences(IWorkflowHandler e, W3iFile sourceMapW3i, W3iFile newW3iFile, WtsFile sourceMapWts, WtsFile oldWtsFile)
        {
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.Name);
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.Author);
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.Description);
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.SuggestedPlayers);
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.PrologueText);
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.PrologueTitle);
            WtsUtility.RemapStrRef(e, sourceMapW3i, newW3iFile, oldWtsFile, sourceMapWts, x => x.PrologueSubtitle);

            newW3iFile.Name = VersionUtility.ChangeVersion(sourceMapWts, newW3iFile.Name);
            newW3iFile.Description = VersionUtility.ChangeVersion(sourceMapWts, newW3iFile.Description);

            for (int i = 0; i < newW3iFile.PlayerCount; i++)
            {
                if (sourceMapW3i.PlayerCount > i)
                {
                    WtsUtility.RemapStrRef(e, sourceMapW3i.Players[i], newW3iFile.Players[i], oldWtsFile, sourceMapWts, x => x.Name);
                }
                else
                {
                    newW3iFile.Players[i].Name = WtsUtility.AddStrRef(sourceMapWts, oldWtsFile, newW3iFile.Players[i].Name);
                }
            }

            for (int i = 0; i < newW3iFile.ForcesCount; i++)
            {
                if (sourceMapW3i.ForcesCount > i)
                {
                    WtsUtility.RemapStrRef(e, sourceMapW3i.Forces[i], newW3iFile.Forces[i], oldWtsFile, sourceMapWts, x => x.Name);
                }
                else
                {
                    newW3iFile.Forces[i].Name = WtsUtility.AddStrRef(sourceMapWts, oldWtsFile, newW3iFile.Forces[i].Name);
                }
            }

            for (int i = 0; i < newW3iFile.RandomUnitsTableCount; i++)
            {
                if (sourceMapW3i.RandomUnitsTableCount > i)
                {
                    WtsUtility.RemapStrRef(e, sourceMapW3i.RandomUnitsTables[i], newW3iFile.RandomUnitsTables[i], oldWtsFile, sourceMapWts, x => x.TableName);
                }
                else
                {
                    newW3iFile.RandomUnitsTables[i].TableName = WtsUtility.AddStrRef(sourceMapWts, oldWtsFile, newW3iFile.RandomUnitsTables[i].TableName);
                }
            }

            for (int i = 0; i < newW3iFile.RandomItemsTableCount; i++)
            {
                if (sourceMapW3i.RandomItemsTableCount > i)
                {
                    WtsUtility.RemapStrRef(e, sourceMapW3i.RandomItemsTables[i], newW3iFile.RandomItemsTables[i], oldWtsFile, sourceMapWts, x => x.TableName);
                }
                else
                {
                    newW3iFile.RandomItemsTables[i].TableName = WtsUtility.AddStrRef(sourceMapWts, oldWtsFile, newW3iFile.RandomItemsTables[i].TableName);
                }
            }
        }

        private void UpdateJFileStrings(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, JFileFunctionNode sourceNode, JFileFunctionNode targetNode)
        {
            if (sourceNode.Children != null && targetNode.Children != null)
            {
                List<JFileInlineString> sourceInlineStrings = new List<JFileInlineString>();
                List<JFileInlineString> targetInlineStrings = new List<JFileInlineString>();

                JUtility.FindInlineStrings(sourceNode.Children, sourceInlineStrings);
                JUtility.FindInlineStrings(targetNode.Children, targetInlineStrings);

                for (int i = 0; i < targetInlineStrings.Count; i++)
                {
                    if (targetInlineStrings[i].Context.Contains("ExecuteFunc("))
                    {
                        RemapExecuteFuncCall(e, targetInlineStrings[i], sourceInlineStrings);
                    }
                    else
                    {
                        JFileInlineString sourceMatch = sourceInlineStrings.Find(x => x.Context.Equals(targetInlineStrings[i].Context));
                        RemapJStringReference(e, newWtsFile, oldWtsFile, targetInlineStrings[i], sourceMatch);
                    }
                }
            }
        }

        private void RemapJStringReference(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, JFileInlineString inlineString, JFileInlineString sourceMatch)
        {
            if (sourceMatch != null && !inlineString.Value.Equals(sourceMatch.Value))
            {
                string oldValue = VersionUtility.ChangeVersion(oldWtsFile, inlineString.Value);
                string newValue = WtsUtility.RemapStrRef(e, oldWtsFile, newWtsFile, sourceMatch.Value, oldValue);
                inlineString.Node.TextValue = inlineString.Node.TextValue.Replace(oldValue, newValue);
            }
        }

        private void OverwriteRegionVariables(IWorkflowHandler e, JFile source, JFile destination)
        {
            OverwriteGlobalVariables(e, source, destination, "rect gg_rct_", "camerasetup gg_cam_", "sound gg_snd_");
        }

        private void OverwriteCameraVariables(IWorkflowHandler e, JFile source, JFile destination)
        {
            OverwriteGlobalVariables(e, source, destination, "camerasetup gg_cam_", "sound gg_snd_");
        }

        private void OverwriteRandomGroupVariables(IWorkflowHandler e, JFile source, JFile destination)
        {
            OverwriteGlobalVariables(e, source, destination, "integer array gg_rg_", "trigger gg_trg_Melee_Initialization", "rect gg_rct_");
        }

        private void OverwriteGlobalVariables(IWorkflowHandler e, JFile source, JFile destination, string globalDeclPrefix, params string[] insertionPointDeclPrefix)
        {
            JFileGlobalsNode sourceGlobals = source.Root.Find<JFileGlobalsNode>();
            JFileGlobalsNode destinationGlobals = destination.Root.Find<JFileGlobalsNode>();

            FormatOptions formatOptions = FormatOptions.Trim | FormatOptions.CollapseWhiteSpace;

            List<JFileTextNode> sourceVars = sourceGlobals.FindAll<JFileTextNode>(x => x.ToString(formatOptions).StartsWith(globalDeclPrefix, StringComparison.OrdinalIgnoreCase));
            List<JFileTextNode> destinationVars = destinationGlobals.FindAll<JFileTextNode>(x => x.ToString(formatOptions).StartsWith(globalDeclPrefix, StringComparison.OrdinalIgnoreCase));

            foreach (JFileTextNode node in destinationVars)
            {
                node.Remove();
            }

            if (sourceVars.Count > 0)
            {
                JFileTextNode insertionPoint = null;
                for (int i = 0; i < insertionPointDeclPrefix.Length && insertionPoint == null; i++)
                {
                    insertionPoint = destinationGlobals.Find<JFileTextNode>(x => x.ToString(formatOptions).StartsWith(insertionPointDeclPrefix[i], StringComparison.OrdinalIgnoreCase));
                }

                if (insertionPoint == null)
                {
                    insertionPoint = destinationGlobals.Find<JFileTextNode>(x => x.ToString(formatOptions).StartsWith("trigger gg_trg_", StringComparison.OrdinalIgnoreCase));
                }

                if (insertionPoint != null)
                {
                    insertionPoint.AddPreviousSiblings(sourceVars);
                }
                else
                {
                    e.ReportError("Error: Could not find insertion point for region variables!", false);
                }
            }
        }

        private void InsertFunctionBlock(IWorkflowHandler e, JFile destination, JFileFunctionNode functionBlock, string blockName)
        {
            List<JFileCommentNode> customScripts = destination.Root.FindAll<JFileCommentNode>(x => x.TextValue.Trim('/', '*', ' ').Equals("Custom Script Code"));
            JFileNode insertionPoint = null;
            if (customScripts.Count > 0)
            {
                insertionPoint = customScripts[customScripts.Count - 1].GetPreviousSibling();
                while (insertionPoint != null && insertionPoint.NodeType == NodeType.Comment)
                {
                    insertionPoint = insertionPoint.GetPreviousSibling();
                }

                if (insertionPoint != null)
                {
                    insertionPoint = insertionPoint.AddPreviousSibling(new JFileTextNode(destination, ""));
                    JFileCommentNode[] commentBlock = JUtility.CreateCommentBlock(destination, blockName);
                    insertionPoint.AddNextSiblings(commentBlock);

                    insertionPoint = commentBlock[4].AddNextSibling(new JFileTextNode(destination, ""));
                    insertionPoint = insertionPoint.AddNextSibling(functionBlock);
                }
            }
            
            if (customScripts.Count == 0 || insertionPoint == null)
            {
                e.ReportError($"Error: Could not find insertion point for function block {functionBlock.FunctionName}", false);
            }
        }

        private void RemapExecuteFuncCall(IWorkflowHandler e, JFileInlineString inlineString, List<JFileInlineString> sourceInlineStrings)
        {
            string calledFunc = StringUtility.RemoveAllCharsAndDigits(inlineString.Value, '_');
            JFileInlineString sourceMatch = sourceInlineStrings.Find(x => calledFunc.Equals(StringUtility.RemoveAllCharsAndDigits(x.Value, '_')));

            if (sourceMatch != null)
            {
                JFileTextNode targetNode = inlineString.Node;
                string sourceFuncName = sourceMatch.Value;

                targetNode.TextValue = targetNode.TextValue.Replace(inlineString.Value, sourceFuncName);
            }
            else
            {
                bool success = inlineString.Node.Remove();
                if (!success)
                {
                    e.ReportError($"Attempted to remove node '{inlineString.Node.TextValue}', but the remove failed!", false);
                }
            }
        }

        private void MergeMains(JFileFunctionNode sourceMain, JFileFunctionNode targetMain)
        {
            JFileTextNode sourceInitSounds = sourceMain.Find<JFileTextNode>(x => InitSoundsRegex.IsMatch(x.TextValue));
            JFileTextNode targetInitSounds = targetMain.Find<JFileTextNode>(x => InitSoundsRegex.IsMatch(x.TextValue));

            JFileTextNode sourceInitBlizzard = sourceMain.Find<JFileTextNode>(x => InitBlizzardRegex.IsMatch(x.TextValue));
            JFileTextNode targetInitBlizzard = targetMain.Find<JFileTextNode>(x => InitBlizzardRegex.IsMatch(x.TextValue));

            if (sourceInitSounds != null && targetInitSounds == null)
            {
                targetInitBlizzard.AddPreviousSibling(sourceInitSounds.Clone(targetMain.Document));
            }

            JUtility.RemoveNextSiblingsAll(targetInitBlizzard);

            JFileNode targetNode = targetInitBlizzard;

            int i = sourceMain.Children.IndexOf(sourceInitBlizzard) + 1;
            for (; i < sourceMain.Children.Count; i++)
            {
                JFileNode newChild = sourceMain.Children[i].Clone(targetNode.Document);
                targetNode.AddNextSibling(newChild);
                targetNode = newChild;
            }
        }
    }
}
