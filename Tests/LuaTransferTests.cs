using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using WarTransfer;
using WarTransfer.FileTypes;
using WarTransfer.Scripts;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    public class LuaTransferTests
    {
        private string root, source, terrain, output;
        private CommandLineErrorHandler handler;
        private WtsFile sourceStrings, terrainStrings;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "Lua transfer " + Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "source"); terrain = Path.Combine(root, "terrain"); output = Path.Combine(root, "out");
            foreach (string dir in new[] { source, terrain, output }) Directory.CreateDirectory(dir);
            handler = new CommandLineErrorHandler(TextWriter.Null, false);
            sourceStrings = new WtsFile(); terrainStrings = new WtsFile();
            VersionUtility.UpdateVersion("source.w3x", "");
        }

        [TestCleanup]
        public void Cleanup() { Directory.Delete(root, true); VersionUtility.UpdateVersion("source.w3x", ""); }

        internal static string Function(string name, string body = "") => "function " + name + "()\n" + body + "\nend\n";
        internal static string Script(string functions = "", string globals = "", string prefix = "", string suffix = "Gameplay()", int players = 2) =>
            globals + "\n-- source formatting\n" + Function("Gameplay", "-- preserve me\nlocal f = function(x) if x then return 'end' end end\nf(true)") +
            functions + Function("config", "SetMapName('TRIGSTR_001')\nSetPlayers(" + players + ")") +
            Function("main", prefix + "\nInitBlizzard()\n" + suffix);

        private string Merge(string sourceScript, string terrainScript, WarTransferArgs args = null, bool success = true)
        {
            File.WriteAllText(Path.Combine(source, "war3map.lua"), sourceScript);
            File.WriteAllText(Path.Combine(terrain, "war3map.lua"), terrainScript);
            if (!sourceStrings.FileMap.ContainsKey(0)) WtsUtility.AddBlock(sourceStrings, "unused");
            if (!terrainStrings.FileMap.ContainsKey(0)) WtsUtility.AddBlock(terrainStrings, "unused");
            if (!sourceStrings.FileMap.ContainsKey(1)) WtsUtility.AddBlock(sourceStrings, "source name");
            if (!terrainStrings.FileMap.ContainsKey(1)) WtsUtility.AddBlock(terrainStrings, "terrain name");
            new LuaScriptMerger(args ?? new WarTransferArgs()).Merge(handler, source, terrain, output, sourceStrings, terrainStrings);
            if (success) Assert.IsFalse(handler.KillProcess, string.Join("\n", handler.Errors));
            else Assert.IsTrue(handler.KillProcess, "Expected a fatal Lua error");
            string path = Path.Combine(output, "war3map.lua");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        [TestMethod]
        public void KeepsSourceGameplayAndUsesDestinationConfigurationAndTerrainPrefix()
        {
            string game = Function("MyGame", "local quoted = [==[function main() end -- TRIGSTR_999]==]\nreturn quoted");
            string result = Merge(Script(game, "udg_Value = 7", "SourceTerrain()", "MyGame()"),
                Script(Function("ObsoleteGame", "error('old data')"), "udg_Value = 99", "DestinationTerrain()", "ObsoleteGame()", 8));
            StringAssert.Contains(result, game);
            StringAssert.Contains(result, "udg_Value = 7");
            StringAssert.Contains(result, "DestinationTerrain()");
            StringAssert.Contains(result, "SetPlayers(8)");
            Assert.IsFalse(result.Contains("SourceTerrain()"));
            Assert.IsFalse(result.Contains("ObsoleteGame"));
            Assert.IsFalse(result.Contains("udg_Value = 99"));
        }

        [DataTestMethod]
        [DataRow(false, false)] [DataRow(false, true)] [DataRow(true, false)] [DataRow(true, true)]
        public void SelectsCameraRegionAndRandomGroupGlobalsAndFunctions(bool cameras, bool regions)
        {
            Func<string, string> globals = label => "gg_cam_" + label + " = nil\ngg_rct_" + label + " = nil\ngg_rg_" + label + " = {}\n" +
                "gg_unit_" + label + " = nil\ngg_snd_" + label + " = nil\nudg_" + label + " = 7\n";
            Func<string, string> functions = label => Function("CreateCameras", "Camera('" + label + "')") +
                Function("CreateRegions", "Region('" + label + "')") + Function("InitRandomGroups", "Group('" + label + "')");
            string result = Merge(Script(functions("Source"), globals("Source")), Script(functions("Terrain"), globals("Terrain")),
                new WarTransferArgs { TransferCameras = cameras, TransferRegions = regions });
            StringAssert.Contains(result, "gg_cam_" + (cameras ? "Source" : "Terrain") + " = nil");
            StringAssert.Contains(result, "gg_rct_" + (regions ? "Source" : "Terrain") + " = nil");
            StringAssert.Contains(result, "Camera('" + (cameras ? "Source" : "Terrain") + "')");
            StringAssert.Contains(result, "Region('" + (regions ? "Source" : "Terrain") + "')");
            StringAssert.Contains(result, "gg_rg_Terrain = {}");
            StringAssert.Contains(result, "gg_unit_Terrain = nil");
            StringAssert.Contains(result, "gg_snd_Source = nil");
            Assert.IsFalse(result.Contains("gg_unit_Source"));
            Assert.IsFalse(result.Contains("udg_Terrain"));
        }

        [DataTestMethod]
        [DataRow(false)] [DataRow(true)]
        public void SelectsRandomUnitTableCodeAndGlobalsTogether(bool transfer)
        {
            string result = Merge(Script(Function("InitRandomGroups", "SourceGroups()"), "gg_rg_Source = {}"),
                Script(Function("InitRandomGroups", "TerrainGroups()"), "gg_rg_Terrain = {}"), new WarTransferArgs { TransferUnitTables = transfer });
            StringAssert.Contains(result, transfer ? "SourceGroups()" : "TerrainGroups()");
            StringAssert.Contains(result, transfer ? "gg_rg_Source = {}" : "gg_rg_Terrain = {}");
        }

        [TestMethod]
        public void AddsAndRemovesPlacementFunctionsAndCallbacksIncludingItemsAndDestructables()
        {
            string result = Merge(Script(Function("CreateUnitsForPlayer0", "OldUnits()") + Function("Unit000001_DropItems", "OldDrops()")),
                Script(Function("CreateUnitsForPlayer2", "NewUnits()") + Function("Unit000002_DropItems", "NewDrops()") +
                    Function("CreateAllItems", "NewItems()") + Function("CreateAllDestructables", "NewDestructables()")));
            foreach (string value in new[] { "NewUnits()", "NewDrops()", "NewItems()", "NewDestructables()" }) StringAssert.Contains(result, value);
            Assert.IsFalse(result.Contains("OldUnits()")); Assert.IsFalse(result.Contains("OldDrops()"));
        }

        [DataTestMethod]
        [DataRow(false)] [DataRow(true)]
        public void SelectsItemTableCallbacks(bool transfer)
        {
            string result = Merge(Script(Function("ItemTable000001_DropItems", "SourceDrop()")),
                Script(Function("ItemTable000001_DropItems", "TerrainDrop()")), new WarTransferArgs { TransferItemTables = transfer });
            StringAssert.Contains(result, transfer ? "SourceDrop()" : "TerrainDrop()");
        }

        [TestMethod]
        public void AddsMissingInitializationBeforePlacedUnitsAndRetainsSourceSounds()
        {
            string functions = Function("CreateCameras", "CameraInit()") + Function("CreateRegions", "RegionInit()") + Function("InitSounds", "SourceSounds()");
            string result = Merge(Script(functions, prefix: "InitSounds()"),
                Script(Function("CreateAllUnits", "PlaceUnits()"), prefix: "CreateAllUnits()"),
                new WarTransferArgs { TransferCameras = true, TransferRegions = true });
            var main = LuaDocument.Parse(result).FunctionText("main");
            foreach (string name in new[] { "InitSounds()", "CreateCameras()", "CreateRegions()" })
                Assert.IsTrue(main.IndexOf(name, StringComparison.Ordinal) < main.IndexOf("CreateAllUnits()", StringComparison.Ordinal), main);
            StringAssert.Contains(result, "SourceSounds()");
        }

        [TestMethod]
        public void DoesNotKeepDestinationSoundInitializationWhenSourceRunsItAfterBlizzard()
        {
            string result = Merge(Script(Function("InitSounds", "SourceSounds()"), suffix: "InitSounds()\nGameplay()"),
                Script(Function("InitSounds", "OldSounds()"), prefix: "InitSounds()"));
            string main = LuaDocument.Parse(result).FunctionText("main");
            Assert.IsTrue(main.IndexOf("InitSounds()", StringComparison.Ordinal) > main.IndexOf("InitBlizzard()", StringComparison.Ordinal));
            Assert.IsFalse(result.Contains("OldSounds()"));
        }

        [TestMethod]
        public void RemapsOnlyImportedStringLiteralsAndPreservesSourceSuffixAndComments()
        {
            WtsUtility.AddBlock(sourceStrings, "source tooltip");
            WtsUtility.AddBlock(terrainStrings, "terrain tooltip 1.0");
            VersionUtility.UpdateVersion("source 2.0.w3x", @"\d+\.\d+");
            string result = Merge(Script(suffix: "Display('TRIGSTR_000')\nGameplay()"),
                Script(prefix: "-- 'TRIGSTR_999' is a comment\nDisplay([=[TRIGSTR_000]=])\nDisplay(\"TRIGSTR_000\")"));
            StringAssert.Contains(result, "-- 'TRIGSTR_999' is a comment");
            StringAssert.Contains(result, "Display('TRIGSTR_000')");
            Assert.AreEqual("source tooltip", sourceStrings.GetValue(0));
            Assert.IsTrue(sourceStrings.FileMap.Keys.Any(k => sourceStrings.GetValue(k) == "terrain tooltip 2.0"));
            var main = LuaDocument.Parse(result).FunctionText("main");
            Assert.IsFalse(main.Contains("[=[TRIGSTR_000]=]"));
        }

        [DataTestMethod]
        [DataRow("if true then InitBlizzard() end")]
        [DataRow("local f = function() InitBlizzard() end")]
        [DataRow("InitBlizzard()\nInitBlizzard()")]
        [DataRow("InitBlizzard(1)")]
        [DataRow("-- InitBlizzard()")]
        public void InvalidInitializationFailsWithoutReplacingExistingScript(string body)
        {
            File.WriteAllText(Path.Combine(output, "war3map.lua"), "existing output");
            string broken = Script().Replace("InitBlizzard()", body);
            Assert.AreEqual("existing output", Merge(broken, Script(), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "InitBlizzard()");
        }

        [DataTestMethod]
        [DataRow("function main( end")]
        [DataRow("--[=[ unterminated comment")]
        [DataRow("local x = 'unterminated")]
        [DataRow("function main() end\nfunction main() end")]
        [DataRow("local function main() end")]
        [DataRow("local main\nfunction main() end")]
        [DataRow("main = function() end")]
        [DataRow("if true then function main() end end")]
        [DataRow("function main(...) end")]
        [DataRow("local x <const> = 1")]
        public void InvalidOrUnsupportedEntrypointsFailClosed(string script)
        {
            Assert.IsNull(Merge(script, Script(), success: false));
        }

        [TestMethod]
        public void MissingTerrainStringFailsWithoutPublishing()
        {
            Assert.IsNull(Merge(Script(), Script(prefix: "Display('TRIGSTR_999')"), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "Missing string reference");
        }

        [TestMethod]
        public void RejectsMixedGlobalAssignmentInsteadOfDroppingGameplayState()
        {
            Assert.IsNull(Merge(Script(globals: "gg_rct_Old, udg_Gameplay = nil, 7"), Script(), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "mixing terrain globals");
        }

        [TestMethod]
        public void RejectsDestinationLocalDependenciesInsteadOfGeneratingBrokenClosures()
        {
            Assert.IsNull(Merge(Script(), Script(Function("CreateRegions", "Region(bounds)"), "local bounds = 7"), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "destination-local 'bounds'");
        }

        [TestMethod]
        public void RejectsSourceMainLocalsThatWouldBeLostAtTheMergeBoundary()
        {
            Assert.IsNull(Merge(Script(prefix: "local state = 1", suffix: "Display(state)"), Script(), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "source-local declarations");
        }

        [TestMethod]
        public void PreservesShebangAndNonReferenceStringsWhenInsertingGlobals()
        {
            string result = Merge("#!/usr/bin/lua\n" + Script(), Script(globals: "gg_rct_New = nil", prefix: "Display('TRIGSTR_001 trailing text')"));
            Assert.IsTrue(result.StartsWith("#!/usr/bin/lua\n", StringComparison.Ordinal));
            StringAssert.Contains(result, "Display('TRIGSTR_001 trailing text')");
            StringAssert.Contains(result, "gg_rct_New = nil");
        }

        [TestMethod]
        public void MissingStringWithVersionReplacementReportsTheReference()
        {
            VersionUtility.UpdateVersion("source2.0.w3x", @"\d+\.\d+");
            Assert.IsNull(Merge(Script(), Script(prefix: "Display('TRIGSTR_999')"), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "Missing string reference 'TRIGSTR_999'");
        }

        [TestMethod]
        public void RejectsDestinationMainLocalsThatCouldShadowSourceSuffix()
        {
            Assert.IsNull(Merge(Script(suffix: "Display(state)"), Script(prefix: "local state = 999"), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "destination-local declarations");
        }

        [DataTestMethod]
        [DataRow("OldHelper()")]
        [DataRow("RegisterDrop(OldHelper)")]
        [DataRow("ExecuteFunc('OldHelper')")]
        public void RejectsUncopiedDestinationHelpersIncludingCallbacks(string prefix)
        {
            Assert.IsNull(Merge(Script(), Script(Function("OldHelper", "OldGameplay()"), prefix: prefix), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "destination");
            StringAssert.Contains(string.Join("\n", handler.Errors), "OldHelper");
        }

        [TestMethod]
        public void RejectsImportedGlobalDependingOnDestinationLocal()
        {
            Assert.IsNull(Merge(Script(), Script(globals: "local bounds = {}\ngg_rct_New = bounds"), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "destination-local 'bounds'");
        }

        [TestMethod]
        public void RejectsMissingSelectedGeneratedFunctionRatherThanWritingBrokenLua()
        {
            Assert.IsNull(Merge(Script(), Script(Function("CreateAllUnits", "InitRandomGroups()")),
                new WarTransferArgs { TransferUnitTables = true }, success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "missing generated function 'InitRandomGroups'");
        }

        [DataTestMethod]
        [DataRow(true)] [DataRow(false)]
        public void InsertsMissingRegionOrCameraInTheGeneratedInitializationOrder(bool missingRegions)
        {
            string added = missingRegions ? "CreateRegions" : "CreateCameras";
            string existing = missingRegions ? "CreateCameras" : "CreateRegions";
            string result = Merge(Script(Function(added), prefix: added + "()"),
                Script(Function(existing) + Function("CreateAllUnits"), prefix: existing + "()\nCreateAllUnits()"),
                new WarTransferArgs { TransferRegions = missingRegions, TransferCameras = !missingRegions });
            string main = LuaDocument.Parse(result).FunctionText("main");
            Assert.IsTrue(main.IndexOf("CreateRegions()", StringComparison.Ordinal) < main.IndexOf("CreateCameras()", StringComparison.Ordinal), main);
            Assert.IsTrue(main.IndexOf("CreateCameras()", StringComparison.Ordinal) < main.IndexOf("CreateAllUnits()", StringComparison.Ordinal), main);
        }

        [TestMethod]
        public void MissingGeneratedCallbackFailsBeforeWriting()
        {
            Assert.IsNull(Merge(Script(), Script(Function("CreateAllUnits", "RegisterDrop(Unit000005_DropItems)")), success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "missing generated callback");
        }

        [DataTestMethod]
        [DataRow("gg_cam_Terrain", true, false, false)]
        [DataRow("gg_rct_Terrain", false, true, false)]
        [DataRow("gg_rg_Terrain", false, false, true)]
        public void RejectsPlacementDependenciesOnSourceSelectedGlobalsWithDifferentNames(string name, bool cameras, bool regions, bool groups)
        {
            Assert.IsNull(Merge(Script(), Script(Function("CreateAllUnits", "Use(" + name + ")"), globals: name + " = {}"),
                new WarTransferArgs { TransferCameras = cameras, TransferRegions = regions, TransferUnitTables = groups }, success: false));
            StringAssert.Contains(string.Join("\n", handler.Errors), "source-selected global '" + name + "'");
        }
    }
}
