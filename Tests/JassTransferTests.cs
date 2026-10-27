using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using WarTransfer;
using WarTransfer.FileTypes;
using WarTransfer.Scripts;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    public class JassTransferTests
    {
        private string root, source, terrain, output;
        private CommandLineErrorHandler handler;
        private WtsFile sourceStrings, terrainStrings;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "JASS transfer tests " + Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "source");
            terrain = Path.Combine(root, "terrain");
            output = Path.Combine(root, "output");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(terrain);
            Directory.CreateDirectory(output);
            handler = new CommandLineErrorHandler(TextWriter.Null, false);
            sourceStrings = new WtsFile();
            terrainStrings = new WtsFile();
            VersionUtility.UpdateVersion("source.w3x", "");
        }

        [TestCleanup]
        public void Cleanup()
        {
            VersionUtility.UpdateVersion("source.w3x", "");
            Directory.Delete(root, true);
        }

        private static string Function(string name, string body = "") =>
            "function " + name + " takes nothing returns nothing\n" + body + "endfunction\n";

        private static string Script(string functions, string globals = "") =>
            "// Source formatting and comments\nglobals\n" + globals + "endglobals\n\n" +
            "// Custom Script Code\n" + functions;

        private string Merge(string sourceScript, string terrainScript, WarTransferArgs args = null, bool expectSuccess = true)
        {
            File.WriteAllText(Path.Combine(source, "war3map.j"), sourceScript);
            File.WriteAllText(Path.Combine(terrain, "war3map.j"), terrainScript);
            IMapScriptMerger merger = new JassScriptMerger(args ?? new WarTransferArgs());
            merger.Merge(handler, source, terrain, output, sourceStrings, terrainStrings);
            if (expectSuccess) Assert.IsFalse(handler.KillProcess, string.Join("\n", handler.Errors));
            string path = Path.Combine(output, "war3map.j");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        [TestMethod]
        public void KeepsGameplayAndSoundsButUsesTerrainConfigurationAndMainPrefix()
        {
            string sourceScript = Script(Function("Gameplay", "    call SourceGameplay()\n") +
                Function("InitSounds", "    call SourceSounds()\n") +
                Function("config", "    call SetPlayers(2)\n") +
                Function("main", "    call SourceTerrain()\n    call InitSounds()\n    call InitBlizzard()\n    call Gameplay()\n"));
            string terrainScript = Script(Function("TerrainGameplay", "    call UnwantedGameplay()\n") +
                Function("config", "    call SetPlayers(4)\n") +
                Function("main", "    call DestinationTerrain()\n    call InitBlizzard()\n    call TerrainGameplay()\n"));
            string result = Merge(sourceScript, terrainScript);
            StringAssert.Contains(result, "// Source formatting and comments");
            StringAssert.Contains(result, "call SourceGameplay()");
            StringAssert.Contains(result, "call SourceSounds()");
            StringAssert.Contains(result, "call SetPlayers(4)");
            StringAssert.Contains(result, "call DestinationTerrain()");
            StringAssert.Contains(result, "call Gameplay()");
            Assert.IsFalse(result.Contains("call SourceTerrain()"));
            Assert.IsFalse(result.Contains("TerrainGameplay"));
            Assert.IsTrue(result.IndexOf("call InitSounds()") < result.IndexOf("call InitBlizzard()"));
        }

        [TestMethod]
        public void ReplacesRemovesAndInsertsGeneratedFunctionsBeforeTheirUse()
        {
            string sourceScript = Script(Function("CreateUnitsForPlayer0", "    call OldUnits()\n") +
                Function("CreateUnitsForPlayer1", "    call ObsoleteUnits()\n") +
                Function("CreateAllUnits", "    call CreateUnitsForPlayer0()\n"));
            string terrainScript = Script(Function("CreateUnitsForPlayer0", "    call DestinationUnits()\n") +
                Function("CreateUnitsForPlayer2", "    call MoreUnits()\n") +
                Function("CreateBuildingsForPlayer2", "    call CreateUnitsForPlayer2()\n") +
                Function("CreateAllUnits", "    call CreateUnitsForPlayer0()\n    call CreateBuildingsForPlayer2()\n"));
            string result = Merge(sourceScript, terrainScript);
            StringAssert.Contains(result, "call DestinationUnits()");
            StringAssert.Contains(result, "call MoreUnits()");
            Assert.IsFalse(result.Contains("OldUnits"));
            Assert.IsFalse(result.Contains("ObsoleteUnits"));
            Assert.IsTrue(result.IndexOf("function CreateUnitsForPlayer2 ") < result.IndexOf("function CreateBuildingsForPlayer2 "));
            Assert.IsTrue(result.IndexOf("function CreateBuildingsForPlayer2 ") < result.IndexOf("function CreateAllUnits "));
        }

        [TestMethod]
        public void InsertsGeneratedDropCallbacksBeforeFunctionsThatReferenceThem()
        {
            string result = Merge(Script(Function("CreateAllUnits")),
                Script(Function("Unit000001_DropItems", "    call DropUnitItems()\n") +
                    Function("ItemTable000002_DropItems", "    call DropTableItems()\n") +
                    Function("CreateAllUnits", "    call TriggerAddAction(t, function Unit000001_DropItems)\n" +
                        "    call TriggerAddAction(t, function ItemTable000002_DropItems)\n")));
            StringAssert.Contains(result, "call DropUnitItems()");
            StringAssert.Contains(result, "call DropTableItems()");
            Assert.IsTrue(result.IndexOf("function Unit000001_DropItems ") < result.IndexOf("function CreateAllUnits "));
            Assert.IsTrue(result.IndexOf("function ItemTable000002_DropItems ") < result.IndexOf("function CreateAllUnits "));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SelectsCameraAndRegionGlobalsAndKeepsCustomGlobals(bool transferSource)
        {
            string globals = "    integer udg_Gameplay = 7\n    integer array gg_rg_{0}\n" +
                "    rect gg_rct_{0} = null\n    camerasetup gg_cam_{0} = null\n" +
                "    sound gg_snd_{0} = null\n    trigger gg_trg_Gameplay = null\n";
            string result = Merge(Script("", string.Format(globals, "Source")),
                Script("", string.Format(globals, "Terrain")), new WarTransferArgs
                { TransferCameras = transferSource, TransferRegions = transferSource });
            StringAssert.Contains(result, "integer udg_Gameplay = 7");
            StringAssert.Contains(result, "sound gg_snd_Source");
            StringAssert.Contains(result, "integer array gg_rg_Terrain");
            string chosen = transferSource ? "Source" : "Terrain";
            string removed = transferSource ? "Terrain" : "Source";
            StringAssert.Contains(result, "rect gg_rct_" + chosen);
            StringAssert.Contains(result, "camerasetup gg_cam_" + chosen);
            Assert.IsFalse(result.Contains("rect gg_rct_" + removed));
            Assert.IsFalse(result.Contains("camerasetup gg_cam_" + removed));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void KeepsSourceOnlyCameraAndRegionFunctionsWhenRequested(bool transferSource)
        {
            string functions = Function("CreateRegions", "    call SourceRegions()\n") +
                Function("CreateCameras", "    call SourceCameras()\n");
            string result = Merge(Script(functions), Script(""), new WarTransferArgs
                { TransferCameras = transferSource, TransferRegions = transferSource });
            Assert.AreEqual(transferSource, result.Contains("call SourceRegions()"));
            Assert.AreEqual(transferSource, result.Contains("call SourceCameras()"));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void InsertsTerrainOnlyCameraAndRegionFunctionsOrEmptyStubs(bool transferSource)
        {
            string functions = Function("CreateRegions", "    call TerrainRegions()\n") +
                Function("CreateCameras", "    call TerrainCameras()\n");
            string result = Merge(Script(""), Script(functions), new WarTransferArgs
                { TransferCameras = transferSource, TransferRegions = transferSource });
            StringAssert.Contains(result, "function CreateRegions ");
            StringAssert.Contains(result, "function CreateCameras ");
            Assert.AreEqual(!transferSource, result.Contains("call TerrainRegions()"));
            Assert.AreEqual(!transferSource, result.Contains("call TerrainCameras()"));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ExistingCameraAndRegionFunctionsHonorTransferOptions(bool transferSource)
        {
            string sourceFunctions = Function("CreateRegions", "    call SourceRegions()\n") +
                Function("CreateCameras", "    call SourceCameras()\n");
            string terrainFunctions = Function("CreateRegions", "    call TerrainRegions()\n") +
                Function("CreateCameras", "    call TerrainCameras()\n");
            string result = Merge(Script(sourceFunctions), Script(terrainFunctions), new WarTransferArgs
                { TransferCameras = transferSource, TransferRegions = transferSource });
            Assert.AreEqual(transferSource, result.Contains("call SourceRegions()"));
            Assert.AreEqual(transferSource, result.Contains("call SourceCameras()"));
            Assert.AreEqual(!transferSource, result.Contains("call TerrainRegions()"));
            Assert.AreEqual(!transferSource, result.Contains("call TerrainCameras()"));
        }

        [TestMethod]
        public void RemapsTerrainStringsWithoutOverwritingGameplayStringReferences()
        {
            sourceStrings.FileMap.Add(1, new[] { "STRING 1", "{", "Source title", "}" });
            sourceStrings.FileMap.Add(2, new[] { "STRING 2", "{", "Gameplay text", "}" });
            terrainStrings.FileMap.Add(2, new[] { "STRING 2", "{", "Terrain title", "}" });
            string result = Merge(Script(Function("config", "    call SetMapName(\"TRIGSTR_001\")\n") +
                Function("Gameplay", "    call BJDebugMsg(\"TRIGSTR_002\")\n")),
                Script(Function("config", "    call SetMapName(\"TRIGSTR_002\")\n")));
            StringAssert.Contains(result, "call SetMapName(\"TRIGSTR_001\")");
            StringAssert.Contains(result, "call BJDebugMsg(\"TRIGSTR_002\")");
            Assert.AreEqual("Terrain title", sourceStrings.GetValue(1));
            Assert.AreEqual("Gameplay text", sourceStrings.GetValue(2));
        }

        [TestMethod]
        public void RemapsGeneratedExecuteFuncNamesAndRemovesUnmatchedCalls()
        {
            string result = Merge(Script(Function("config", "    call ExecuteFunc(\"InitUpgrade_111\")\n")),
                Script(Function("config", "    call ExecuteFunc(\"InitUpgrade_999\")\n    call ExecuteFunc(\"Unused_123\")\n")));
            StringAssert.Contains(result, "call ExecuteFunc(\"InitUpgrade_111\")");
            Assert.IsFalse(result.Contains("InitUpgrade_999"));
            Assert.IsFalse(result.Contains("Unused_123"));
        }

        [TestMethod]
        public void UninsertableGeneratedFunctionStopsWithoutWritingScript()
        {
            string result = Merge(Script(""), Script(Function("CreateUnitsForPlayer5")), expectSuccess: false);
            Assert.IsTrue(handler.KillProcess);
            Assert.IsNull(result);
            StringAssert.Contains(string.Join("\n", handler.Errors), "CreateUnitsForPlayer5");
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ScriptReadFailureLeavesExistingOutputUntouched(bool missingSource)
        {
            string presentMap = missingSource ? terrain : source;
            File.WriteAllText(Path.Combine(presentMap, "war3map.j"), Script(""));
            string outputScript = Path.Combine(output, "war3map.j");
            File.WriteAllText(outputScript, "existing script");
            IMapScriptMerger merger = new JassScriptMerger(new WarTransferArgs());
            merger.Merge(handler, source, terrain, output, sourceStrings, terrainStrings);
            Assert.IsTrue(handler.KillProcess);
            Assert.AreEqual("existing script", File.ReadAllText(outputScript));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("    call InitBlizzard()\n    call InitBlizzard()\n")]
        [DataRow("    // call InitBlizzard()\n")]
        [DataRow("    call F(\"call InitBlizzard()\")\n")]
        [DataRow("    if true then\n    call InitBlizzard()\n    endif\n")]
        public void MissingDuplicateAndCommentedInitializationMarkersFailSafely(string terrainMain)
        {
            string result = Merge(Script(Function("main", "    call InitBlizzard()\n")),
                Script(Function("main", terrainMain)), expectSuccess: false);
            Assert.IsNull(result);
            Assert.IsTrue(handler.KillProcess);
            StringAssert.Contains(string.Join("\n", handler.Errors), "exactly one InitBlizzard");
        }

        [TestMethod]
        public void MissingGlobalsProducesWorkflowError()
        {
            Assert.IsNull(Merge(Function("main"), Script(""), expectSuccess: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "globals block");
        }

        [TestMethod]
        public void SourceCamerasAndRegionsAreCalledWhenTerrainDoesNotCallThem()
        {
            string sourceFunctions = Function("CreateRegions") + Function("CreateCameras") +
                Function("main", "    call CreateRegions()\n    call CreateCameras()\n    call InitBlizzard()\n");
            string result = Merge(Script(sourceFunctions), Script(Function("main", "    call InitBlizzard()\n")),
                new WarTransferArgs { TransferCameras = true, TransferRegions = true });
            StringAssert.Contains(result, "call CreateRegions()");
            StringAssert.Contains(result, "call CreateCameras()");
        }

        [TestMethod]
        public void RepeatedCallContextsAndMultipleLiteralsRemapIndependently()
        {
            sourceStrings.FileMap.Add(1, new[] { "STRING 1", "{", "old one", "}" });
            sourceStrings.FileMap.Add(2, new[] { "STRING 2", "{", "old two", "}" });
            terrainStrings.FileMap.Add(9, new[] { "STRING 9", "{", "new one", "}" });
            terrainStrings.FileMap.Add(10, new[] { "STRING 10", "{", "new two", "}" });
            string result = Merge(Script(Function("config", "call F(\"TRIGSTR_001\")\ncall F(\"TRIGSTR_002\")\n")),
                Script(Function("config", "call F(\"TRIGSTR_009\")\ncall F(\"TRIGSTR_010\")\n")));
            Assert.AreEqual("new one", sourceStrings.GetValue(1));
            Assert.AreEqual("new two", sourceStrings.GetValue(2));
            StringAssert.Contains(result, "F(\"TRIGSTR_001\")");
            StringAssert.Contains(result, "F(\"TRIGSTR_002\")");
        }

        [TestMethod]
        public void EscapedAndEmptyLiteralsArePreservedAndChangedByPosition()
        {
            string result = Merge(Script(Function("config", "call F(\"old\", \"old\")\n")),
                Script(Function("config", "call F(\"say \\\"hi\\\"\", \"\") // \"unchanged\"\n")));
            StringAssert.Contains(result, "F(\"say \\\"hi\\\"\", \"\") // \"unchanged\"");
        }

        [TestMethod]
        public void RegionInsertionWorksWithoutDecorativeCommentsOrExistingTriggerGlobals()
        {
            string result = Merge("globals\nendglobals\n", "globals\nrect gg_rct_Terrain = null\nendglobals\n" + Function("CreateRegions"));
            StringAssert.Contains(result, "rect gg_rct_Terrain");
            StringAssert.Contains(result, "function CreateRegions ");
        }

        [TestMethod]
        public void TabSeparatedGeneratedGlobalsAreReplaced()
        {
            string result = Merge(Script("", "\trect\tgg_rct_Source = null\n"),
                Script("", "\trect\tgg_rct_Terrain = null\n"));
            StringAssert.Contains(result, "gg_rct_Terrain");
            Assert.IsFalse(result.Contains("gg_rct_Source"));
        }

        [TestMethod]
        public void AmbiguousGeneratedExecuteFuncMappingFailsSafely()
        {
            Assert.IsNull(Merge(Script(Function("config", "call ExecuteFunc(\"InitUpgrade_111\")\ncall ExecuteFunc(\"InitUpgrade_222\")\n")),
                Script(Function("config", "call ExecuteFunc(\"InitUpgrade_999\")\n")), expectSuccess: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "Ambiguous ExecuteFunc");
        }

        [TestMethod]
        public void AddedTerrainFunctionImportsItsStringReferences()
        {
            sourceStrings.FileMap.Add(1, new[] { "STRING 1", "{", "gameplay", "}" });
            terrainStrings.FileMap.Add(1, new[] { "STRING 1", "{", "terrain text", "}" });
            string result = Merge(Script(Function("CreateAllUnits")),
                Script(Function("CreateUnitsForPlayer0", "call F(\"TRIGSTR_001\")\n") +
                    Function("CreateAllUnits", "call CreateUnitsForPlayer0()\n")));
            Assert.AreEqual("gameplay", sourceStrings.GetValue(1));
            Assert.AreEqual("terrain text", sourceStrings.GetValue(0));
            StringAssert.Contains(result, "F(\"TRIGSTR_000\")");
        }

        [TestMethod]
        public void MissingStringReferenceInAddedFunctionIsReported()
        {
            Assert.IsNull(Merge(Script(Function("CreateAllUnits")),
                Script(Function("CreateUnitsForPlayer0", "call F(\"TRIGSTR_999\")\n") +
                    Function("CreateAllUnits", "call CreateUnitsForPlayer0()\n")), expectSuccess: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "TRIGSTR_999");
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RandomGroupGlobalsAndInitializerFollowUnitTableSelection(bool sourceTables)
        {
            string result = Merge(Script(Function("InitRandomGroups", "call SourceGroups()\n"), "integer array gg_rg_Source\n"),
                Script(Function("InitRandomGroups", "call TerrainGroups()\n"), "integer array gg_rg_Terrain\n"),
                new WarTransferArgs { TransferUnitTables = sourceTables });
            Assert.AreEqual(sourceTables, result.Contains("gg_rg_Source"));
            Assert.AreEqual(sourceTables, result.Contains("call SourceGroups()"));
            Assert.AreEqual(!sourceTables, result.Contains("gg_rg_Terrain"));
            Assert.AreEqual(!sourceTables, result.Contains("call TerrainGroups()"));
        }

        [TestMethod]
        public void TerrainOnlySoundInitializationIsRemoved()
        {
            string result = Merge(Script(Function("main", "call InitBlizzard()\n")),
                Script(Function("main", "call InitSounds()\ncall InitBlizzard()\n")));
            Assert.IsFalse(result.Contains("call InitSounds()"));
        }

        [TestMethod]
        public void SourceMainSuffixKeepsItsOwnStringReferences()
        {
            sourceStrings.FileMap.Add(5, new[] { "STRING 5", "{", "source gameplay text", "}" });
            string result = Merge(Script(Function("main", "call InitBlizzard()\ncall BJDebugMsg(\"TRIGSTR_005\")\n")),
                Script(Function("main", "call InitBlizzard()\n")));
            StringAssert.Contains(result, "BJDebugMsg(\"TRIGSTR_005\")");
            Assert.AreEqual("source gameplay text", sourceStrings.GetValue(5));
        }

        [TestMethod]
        public void OnlyRetainedTerrainMainPrefixImportsTerrainStrings()
        {
            sourceStrings.FileMap.Add(1, new[] { "STRING 1", "{", "source music", "}" });
            sourceStrings.FileMap.Add(5, new[] { "STRING 5", "{", "source gameplay", "}" });
            terrainStrings.FileMap.Add(9, new[] { "STRING 9", "{", "terrain music", "}" });
            string result = Merge(Script(Function("main", "call SetMapMusic(\"TRIGSTR_001\", true, 0)\ncall InitBlizzard()\ncall F(\"TRIGSTR_005\")\n")),
                Script(Function("main", "call SetMapMusic(\"TRIGSTR_009\", true, 0)\ncall InitBlizzard()\ncall F(\"TRIGSTR_999\")\n")));
            StringAssert.Contains(result, "SetMapMusic(\"TRIGSTR_001\", true, 0)");
            StringAssert.Contains(result, "F(\"TRIGSTR_005\")");
            Assert.IsFalse(result.Contains("TRIGSTR_999"));
            Assert.AreEqual("terrain music", sourceStrings.GetValue(1));
            Assert.AreEqual("source gameplay", sourceStrings.GetValue(5));
        }
    }
}
