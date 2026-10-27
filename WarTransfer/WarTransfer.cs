using System;
using System.Collections.Generic;
using System.IO;
using WarTransfer.Scripts;
using WarTransfer.FileTypes;
using WarTransfer.Types;

namespace WarTransfer
{
    public class WarTransfer
    {
        private string m_sourceDirectory = null;
        private string m_sourceMap = null;
        private string m_sourceArchive = null;
        private string m_outDirectory = null;
        private WarTransferArgs m_args = null;

        private string TempSourceDirectory;
        private string TempTargetDirectory;

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

        public void ExecuteDataTransfer(IWorkflowHandler e, WarTransferArgs args)
        {
            // Each invocation owns only this generated directory, including on failure.
            string workspace = Path.Combine(Path.GetTempPath(), "WarTransfer", Guid.NewGuid().ToString("N"));
            TempSourceDirectory = Path.Combine(workspace, "Source");
            TempTargetDirectory = Path.Combine(workspace, "Target");
            try
            {
                ExecuteDataTransferCore(e, args);
            }
            finally
            {
                IOUtility.DeleteDirectory(e, workspace);
            }
        }

        private void ExecuteDataTransferCore(IWorkflowHandler e, WarTransferArgs args)
        {
            this.m_sourceMap = args.SourceMap;
            this.m_sourceArchive = File.Exists(args.SourceMap) ? Path.GetFullPath(args.SourceMap) : null;
            this.m_sourceDirectory = Path.GetFullPath(args.SourceDirectory);
            this.m_outDirectory = args.OutputDirectory;
            this.m_args = args;

            VerifyRequirements(e);

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

            IMapScriptMerger scriptMerger = null;
            if (!e.KillProcess)
            {
                e.TotalSteps = IOUtility.CountFilesAndDirectories(m_sourceDirectory, "*.w3x", "*.w3m") * 3;
                EnsureSourceMap(e);
                if (!e.KillProcess)
                {
                    scriptMerger = MapScripts.Open(e, m_sourceMap, "source", m_args);
                }
            }

            List<string> maps = null;
            if (!e.KillProcess)
            {
                // Extract destination maps only after validating the source language.
                maps = CopyMapsToDirectory(e, m_sourceDirectory, TempSourceDirectory, TargetMapType.Directory);
                if (!e.KillProcess)
                {
                    if (maps.RemoveAll(path => path.Equals(m_sourceMap, StringComparison.OrdinalIgnoreCase)) > 0)
                        e.TotalSteps -= 2;
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
                foreach (string map in maps)
                {
                    var terrainMerger = MapScripts.Open(e, map, "destination", m_args);
                    if (e.KillProcess) break;
                    if (terrainMerger.ScriptType != scriptMerger.ScriptType)
                    {
                        e.ReportError($"Cannot transfer {scriptMerger.ScriptType} scripts to {terrainMerger.ScriptType} destination map '{map}'. Script conversion is not supported.", true);
                        break;
                    }
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
                        CopyFiles(e, currentMap, newMap, scriptMerger);
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

        private void VerifyRequirements(IWorkflowHandler e)
        {
            if (!MPQUtility.VerifyMPQEditor(m_args.MpqEditorPath))
            {
                e.Log(LogType.Error, "Ladik's MPQ editor not found. This is a required dependency.");
                e.KillProcess = true;
            }

            if (!IOUtility.TestFileAccess(m_args.SourceMap))
            {
                string fileName = Path.GetFileName(m_args.SourceMap);

                e.Log(LogType.Error, $"The source file {fileName} is inaccessible. It is likely being used by another process, such as the Warcraft III World Editor.");
                e.KillProcess = true;
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
            bool success = false;
            string sourceMapName = Path.GetFileName(m_sourceMap);

            if (File.Exists(m_sourceMap))
            {
                string sourceMapCopy = Path.Combine(TempSourceDirectory, Path.GetFileNameWithoutExtension(m_sourceMap) + ".w3x");
                success = MPQUtility.MapFileToMapDirectory(e, m_sourceMap, sourceMapCopy, m_args.MpqEditorPath);
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
                success = true;
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
                    if (m_sourceArchive != null && Path.GetFullPath(mapFile).Equals(m_sourceArchive, StringComparison.OrdinalIgnoreCase))
                    {
                        e.TotalSteps -= 3;
                        continue;
                    }
                    string mapName = Path.GetFileNameWithoutExtension(mapFiles[m]);
                    string outMap = Path.Combine(targetDirectory, mapName + ".w3x");

                    e.Log(LogType.Debug, $"Copying map file \"{mapName}\" to \"{outMap}\".");

                    // Destination imports/object data are never used: all required
                    // terrain, metadata, strings and script entries start with war3map.
                    bool success = MPQUtility.MapFileToMapDirectory(e, mapFile, outMap, m_args.MpqEditorPath, "war3map*");

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

                    bool success = MPQUtility.MapDirectoryToMapFile(e, mapDirectory, outMap, m_args.MpqEditorPath, m_sourceArchive, m_sourceMap);

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

        private void CopyFiles(IWorkflowHandler e, string sourceDirectory, string targetDirectory, IMapScriptMerger scriptMerger)
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
                scriptMerger.Merge(e, m_sourceMap, sourceDirectory, targetDirectory, newWtsFile, oldWtsFile);
            }

            if (!e.KillProcess)
            {
                WtsUtility.WriteFile(Path.Combine(targetDirectory, "war3map.wts"), newWtsFile);
            }
        }

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
                if (m_args.TransferUnitTables) continue; // Names already reference source WTS.
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
                if (m_args.TransferItemTables) continue;
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

    }
}
