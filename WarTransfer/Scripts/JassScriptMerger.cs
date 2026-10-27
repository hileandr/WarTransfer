using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WarTransfer.FileTypes;
using WarTransfer.Types;

namespace WarTransfer.Scripts
{
    internal sealed class JassScriptMerger : IMapScriptMerger
    {
        private readonly WarTransferArgs m_args;

        public JassScriptMerger(WarTransferArgs args)
        {
            m_args = args;
        }

        public MapScriptType ScriptType => MapScriptType.Jass;

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

        private static bool IsCall(JFileTextNode node, string name) => node.NodeType == NodeType.Text &&
            Regex.IsMatch(StringUtility.CodeOnly(node.TextValue), @"^\s*call\s+" + name + @"\s*\(\s*\)\s*$");

        public void Merge(IWorkflowHandler e, string sourceMapDirectory, string terrainMapDirectory, string outputMapDirectory, WtsFile newWtsFile, WtsFile oldWtsFile)
        {
            JFile sourceMapJ = JUtility.ReadFile(e, Path.Combine(sourceMapDirectory, "war3map.j"));
            JFile thisMapJ = JUtility.ReadFile(e, Path.Combine(terrainMapDirectory, "war3map.j"));
            if (e.KillProcess) return;
            if (sourceMapJ.Root.Find<JFileGlobalsNode>() == null || thisMapJ.Root.Find<JFileGlobalsNode>() == null)
            {
                e.ReportError("Cannot merge map scripts: both maps must contain a globals block.", true);
                return;
            }

            if (!e.KillProcess && !m_args.TransferCameras)
            {
                OverwriteCameraVariables(e, thisMapJ, sourceMapJ);
            }

            if (!e.KillProcess && !m_args.TransferRegions)
            {
                OverwriteRegionVariables(e, thisMapJ, sourceMapJ);
            }

            if (!e.KillProcess && !m_args.TransferUnitTables)
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
                        if ((patternName == "CreateRegions" && m_args.TransferRegions) ||
                            (patternName == "CreateCameras" && m_args.TransferCameras) ||
                            (patternName == "InitRandomGroups" && m_args.TransferUnitTables)) continue;
                        if (patternName.Equals("main"))
                        {
                            if (!MergeMains(e, sourceMapFunction, thisMapFunction, newWtsFile, oldWtsFile)) return;
                        }
                        else UpdateJFileStrings(e, newWtsFile, oldWtsFile, sourceMapFunction, thisMapFunction);
                        sourceMapFunction.Parent.ReplaceChild(sourceMapFunction, thisMapFunction);
                    }
                    else if (sourceMapFunction != null)
                    {
                        if (patternName.Equals("InitRandomGroups") && m_args.TransferUnitTables)
                            continue;
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
                            UpdateJFileStrings(e, newWtsFile, oldWtsFile, null, thisMapFunction);
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
                JUtility.WriteFile(Path.Combine(outputMapDirectory, "war3map.j"), sourceMapJ);
            }
        }

        private void UpdateJFileStrings(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, JFileFunctionNode sourceNode, JFileFunctionNode targetNode,
            int sourceChildCount = int.MaxValue, int targetChildCount = int.MaxValue)
        {
            if (targetNode.Children != null)
            {
                List<JFileInlineString> sourceInlineStrings = new List<JFileInlineString>();
                List<JFileInlineString> targetInlineStrings = new List<JFileInlineString>();

                if (sourceNode != null) JUtility.FindInlineStrings(sourceNode.Children.Take(sourceChildCount), sourceInlineStrings);
                JUtility.FindInlineStrings(targetNode.Children.Take(targetChildCount), targetInlineStrings);

                var matches = sourceInlineStrings.GroupBy(x => x.Context)
                    .ToDictionary(x => x.Key, x => new Queue<JFileInlineString>(x));
                var replacements = new List<Tuple<JFileInlineString, JFileInlineString>>();
                foreach (var value in targetInlineStrings)
                {
                    Queue<JFileInlineString> queue;
                    var match = matches.TryGetValue(value.Context, out queue) && queue.Count > 0 ? queue.Dequeue() : null;
                    replacements.Add(Tuple.Create(value, match));
                }
                // Edit rightmost literals first, preserving the offsets of earlier ones.
                for (int i = replacements.Count - 1; i >= 0; i--)
                {
                    if (targetInlineStrings[i].Context.StartsWith("callExecuteFunc(", StringComparison.Ordinal))
                    {
                        RemapExecuteFuncCall(e, targetInlineStrings[i], sourceInlineStrings);
                    }
                    else
                    {
                        RemapJStringReference(e, newWtsFile, oldWtsFile, targetInlineStrings[i], replacements[i].Item2);
                    }
                }
            }
        }

        private void RemapJStringReference(IWorkflowHandler e, WtsFile newWtsFile, WtsFile oldWtsFile, JFileInlineString inlineString, JFileInlineString sourceMatch)
        {
            string oldValue = StringUtility.DecodeJassString(inlineString.Value);
            string newValue;
            if (sourceMatch == null)
            {
                try { newValue = WtsUtility.AddStrRef(newWtsFile, oldWtsFile, VersionUtility.ChangeVersion(oldWtsFile, oldValue)); }
                catch (FormatException ex) { e.ReportError(ex.Message, true); return; }
            }
            else
            {
                oldValue = VersionUtility.ChangeVersion(oldWtsFile, oldValue);
                newValue = WtsUtility.RemapStrRef(e, oldWtsFile, newWtsFile,
                    StringUtility.DecodeJassString(sourceMatch.Value), oldValue);
            }
            ReplaceLiteral(inlineString, StringUtility.EncodeJassString(newValue));
        }

        private static void ReplaceLiteral(JFileInlineString value, string replacement)
        {
            string text = value.Node.TextValue;
            value.Node.TextValue = text.Substring(0, value.StartIndex) + replacement +
                text.Substring(value.StartIndex + value.Value.Length);
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
                if (insertionPoint == null)
                    insertionPoint = destinationGlobals.Find<JFileTextNode>(x => StringUtility.CodeOnly(x.TextValue).Trim() == "endglobals");

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
                // Generated scripts need not retain the editor's decorative comments.
                destination.Root.Find<JFileGlobalsNode>().AddNextSibling(functionBlock);
            }
        }

        private void RemapExecuteFuncCall(IWorkflowHandler e, JFileInlineString inlineString, List<JFileInlineString> sourceInlineStrings)
        {
            string calledFunc = StringUtility.RemoveAllCharsAndDigits(inlineString.Value, '_');
            var matching = sourceInlineStrings.Where(x => x.Context.StartsWith("callExecuteFunc(", StringComparison.Ordinal) &&
                calledFunc.Equals(StringUtility.RemoveAllCharsAndDigits(x.Value, '_'))).ToList();
            if (matching.Select(x => x.Value).Distinct().Count() > 1)
            {
                e.ReportError("Ambiguous ExecuteFunc mapping for '" + inlineString.Value + "'.", true);
                return;
            }
            JFileInlineString sourceMatch = matching.FirstOrDefault();

            if (sourceMatch != null)
            {
                JFileTextNode targetNode = inlineString.Node;
                string sourceFuncName = sourceMatch.Value;

                ReplaceLiteral(inlineString, sourceFuncName);
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

        private bool MergeMains(IWorkflowHandler e, JFileFunctionNode sourceMain, JFileFunctionNode targetMain, WtsFile outputStrings, WtsFile terrainStrings)
        {
            JFileTextNode sourceInitSounds = sourceMain.Find<JFileTextNode>(x => IsCall(x, "InitSounds"));

            var sourceMarkers = UnconditionalInitializationMarkers(sourceMain);
            var targetMarkers = UnconditionalInitializationMarkers(targetMain);
            if (sourceMarkers.Count != 1 || targetMarkers.Count != 1)
            {
                e.ReportError("Cannot merge main: source and destination must each have exactly one InitBlizzard() call.", true);
                return false;
            }
            var sourceInitBlizzard = sourceMarkers[0];
            var targetInitBlizzard = targetMarkers[0];
            // Only the terrain prefix survives this merge. Remap it before appending
            // the source suffix, whose string references already belong to outputStrings.
            UpdateJFileStrings(e, outputStrings, terrainStrings, sourceMain, targetMain,
                sourceMain.Children.IndexOf(sourceInitBlizzard), targetMain.Children.IndexOf(targetInitBlizzard));
            if (e.KillProcess) return false;
            foreach (string name in new[] { "CreateCameras", "CreateRegions", "InitRandomGroups" })
            {
                bool transfer = name == "CreateCameras" ? m_args.TransferCameras :
                    name == "CreateRegions" ? m_args.TransferRegions : m_args.TransferUnitTables;
                if (transfer && sourceMain.Document.GetFunction(name) != null &&
                    targetMain.Find<JFileTextNode>(x => IsCall(x, name)) == null)
                    targetInitBlizzard.AddPreviousSibling(new JFileTextNode(targetMain.Document, "    call " + name + "()"));
            }

            bool sourceSoundsBeforeInit = sourceInitSounds != null &&
                sourceMain.Children.IndexOf(sourceInitSounds) < sourceMain.Children.IndexOf(sourceInitBlizzard);
            var targetSoundsBeforeInit = targetMain.FindAll<JFileTextNode>(x => IsCall(x, "InitSounds") &&
                targetMain.Children.IndexOf(x) < targetMain.Children.IndexOf(targetInitBlizzard));
            if (!sourceSoundsBeforeInit)
                foreach (var sound in targetSoundsBeforeInit) sound.Remove();
            if (sourceSoundsBeforeInit && targetSoundsBeforeInit.Count == 0)
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
            return true;
        }

        private static List<JFileTextNode> UnconditionalInitializationMarkers(JFileFunctionNode function)
        {
            var result = new List<JFileTextNode>();
            int depth = 0;
            foreach (var node in function.Children.OfType<JFileTextNode>())
            {
                string code = StringUtility.CodeOnly(node.TextValue).Trim();
                if (Regex.IsMatch(code, @"^(if\b|loop\s*$)")) depth++;
                if (IsCall(node, "InitBlizzard"))
                {
                    if (depth != 0) return new List<JFileTextNode>();
                    result.Add(node);
                }
                if (code == "endif" || code == "endloop") depth--;
            }
            return depth == 0 ? result : new List<JFileTextNode>();
        }
    }
}
