using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WarTransfer;
using WarTransfer.FileTypes;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class TransferWorkflowTests
    {
        private string root, editor, source, targets, output;
        private StringWriter log;
        private CommandLineErrorHandler handler;

        [TestInitialize]
        public void Setup()
        {
            editor = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_MPQ_EDITOR");
            if (string.IsNullOrEmpty(editor) || !File.Exists(editor))
                Assert.Inconclusive("Set WARTRANSFER_TEST_MPQ_EDITOR to run the real MPQ workflow tests.");
            root = Path.Combine(Path.GetTempPath(), "Transfer workflow tests " + Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "source 2.0.w3x");
            targets = Path.Combine(root, "terrains");
            output = Path.Combine(root, "output");
            Directory.CreateDirectory(targets);
            Directory.CreateDirectory(output);
            log = new StringWriter();
            handler = new CommandLineErrorHandler(log, false);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (root != null) Directory.Delete(root, true);
            VersionUtility.UpdateVersion("source.w3x", "");
        }

        private string PackMap(string archive, bool isSource, int type = 0, int playerCount = 4, string scriptOverride = null)
        {
            string contents = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contents);
            MapTestFixture.WriteInfo(contents, type);
            var packHandler = new CommandLineErrorHandler(TextWriter.Null, false);
            var info = W3iUtility.ReadFile(packHandler, Path.Combine(contents, "war3map.w3i"));
            info.Name = "TRIGSTR_001";
            info.Description = "TRIGSTR_002";
            info.Author = "Fixture author";
            info.SuggestedPlayers = "Any";
            info.PrologueText = info.PrologueTitle = info.PrologueSubtitle = "";
            info.Players = new Player[0]; info.PlayerCount = 0;
            info.Forces = new Force[0]; info.ForcesCount = 0;
            info.PlayableWidth = isSource ? 32 : 64;
            info.GameDataSet = isSource ? 1 : 2;
            info.SupportedModes = isSource ? 3 : 1;
            info.GameDataVersion = isSource ? 1 : 0;
            info.Flags = isSource ? W3iFlags.MeleeMap | W3iFlags.UseCustomForces : W3iFlags.UseCustomTechtree;
            info.RandomUnitsTableCount = info.RandomItemsTableCount = 1;
            info.RandomUnitsTables = new[] { new RandomUnitsTable { TableNumber = isSource ? 11 : 22,
                TableName = "TRIGSTR_003", Positions = new int[0], Lines = new RandomUnitsTableLine[0] } };
            info.RandomItemsTables = new[] { new RandomItemsTable { TableId = isSource ? 33 : 44,
                TableName = "TRIGSTR_004", Sets = new RandomItemsTableSet[0] } };
            MapTestFixture.WriteInfo(contents, info);
            string marker = isSource ? "source" : "terrain";
            foreach (string file in new[] { "war3map.w3e", "war3mapUnits.doo", "war3map.w3c", "war3map.w3r", "custom-data.txt" })
                File.WriteAllText(Path.Combine(contents, file), marker + " " + file);
            File.WriteAllText(Path.Combine(contents, "war3map.wts"),
                "STRING 1\n{\n" + marker + " title 1.0\n}\nSTRING 2\n{\n" + marker + " description 1.0\n}\n" +
                "STRING 3\n{\n" + marker + " unit table\n}\nSTRING 4\n{\n" + marker + " item table\n}\n");
            string script = "globals\n    integer udg_Gameplay = 7\nendglobals\n" +
                "function Gameplay takes nothing returns nothing\n    call " + (isSource ? "SourceGameplay" : "TerrainGameplay") + "()\nendfunction\n" +
                "function config takes nothing returns nothing\n    call SetMapName(\"TRIGSTR_001\")\n    call SetPlayers(" + (isSource ? 2 : playerCount) + ")\nendfunction\n" +
                "function main takes nothing returns nothing\n    call " + (isSource ? "SourceTerrain" : "DestinationTerrain") + "()\n" +
                "    call InitBlizzard()\n    call Gameplay()\nendfunction\n";
            File.WriteAllText(Path.Combine(contents, type == 1 ? "war3map.lua" : "war3map.j"),
                type == 1 ? "function main() end" : scriptOverride ?? script);
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(packHandler, contents, archive, editor),
                string.Join("\n", packHandler.Errors));
            return archive;
        }

        private WarTransferArgs Args() => new WarTransferArgs
        {
            SourceMap = source, SourceDirectory = targets, OutputDirectory = output, MpqEditorPath = editor,
            VersionNumberRegex = @"\d+\.\d+"
        };

        [DataTestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(true, false, true, false)]
        [DataRow(false, true, false, true)]
        [DataRow(true, true, true, true)]
        public void TransfersBatchPreservingGameplayTerrainOptionsMetadataStringsAndInputs(
            bool cameras, bool regions, bool unitTables, bool itemTables)
        {
            PackMap(source, true);
            var originals = new Dictionary<string, byte[]> { { source, File.ReadAllBytes(source) } };
            for (int i = 0; i < 2; i++)
            {
                string path = PackMap(Path.Combine(targets, "terrain" + i + " 1.0.w3m"), false, playerCount: 4 + i);
                originals.Add(path, File.ReadAllBytes(path));
            }
            var args = Args();
            args.TransferCameras = cameras; args.TransferRegions = regions;
            args.TransferUnitTables = unitTables; args.TransferItemTables = itemTables;
            new WarTransfer.WarTransfer().ExecuteDataTransfer(handler, args);
            Assert.IsFalse(handler.KillProcess, log.ToString());
            Assert.AreEqual(2, Directory.GetFiles(output, "*.w3x").Length);
            for (int i = 0; i < 2; i++)
            {
                string extracted = Path.Combine(root, "result" + i);
                Assert.IsTrue(MPQUtility.MapFileToMapDirectory(handler,
                    Path.Combine(output, "terrain" + i + " 2.0.w3x"), extracted, editor));
                Assert.AreEqual("source custom-data.txt", File.ReadAllText(Path.Combine(extracted, "custom-data.txt")));
                Assert.AreEqual("terrain war3map.w3e", File.ReadAllText(Path.Combine(extracted, "war3map.w3e")));
                Assert.AreEqual("terrain war3mapUnits.doo", File.ReadAllText(Path.Combine(extracted, "war3mapUnits.doo")));
                Assert.AreEqual((cameras ? "source" : "terrain") + " war3map.w3c", File.ReadAllText(Path.Combine(extracted, "war3map.w3c")));
                Assert.AreEqual((regions ? "source" : "terrain") + " war3map.w3r", File.ReadAllText(Path.Combine(extracted, "war3map.w3r")));
                string script = File.ReadAllText(Path.Combine(extracted, "war3map.j"));
                StringAssert.Contains(script, "call SourceGameplay()");
                StringAssert.Contains(script, "call DestinationTerrain()");
                StringAssert.Contains(script, "call SetPlayers(" + (4 + i) + ")");
                Assert.IsFalse(script.Contains("call SourceTerrain()"));
                Assert.IsFalse(script.Contains("call TerrainGameplay()"));
                var info = W3iUtility.ReadFile(handler, Path.Combine(extracted, "war3map.w3i"));
                Assert.AreEqual(39, info.FormatVersion);
                Assert.AreEqual(0, info.ScriptTypeId);
                Assert.AreEqual(64, info.PlayableWidth);
                Assert.AreEqual(1, info.GameDataSet);
                Assert.AreEqual(3, info.SupportedModes);
                Assert.AreEqual(1, info.GameDataVersion);
                Assert.AreEqual(W3iFlags.MeleeMap | W3iFlags.UseCustomTechtree, info.Flags);
                Assert.AreEqual(unitTables ? 11 : 22, info.RandomUnitsTables[0].TableNumber);
                Assert.AreEqual(itemTables ? 33 : 44, info.RandomItemsTables[0].TableId);
                var strings = WtsUtility.ReadFile(handler, Path.Combine(extracted, "war3map.wts"));
                Assert.AreEqual("terrain title 2.0", strings.GetValue(int.Parse(info.Name.Substring(8))));
                Assert.AreEqual("terrain description 2.0", strings.GetValue(int.Parse(info.Description.Substring(8))));
                Assert.AreEqual((unitTables ? "source" : "terrain") + " unit table", strings.GetValue(int.Parse(info.RandomUnitsTables[0].TableName.Substring(8))));
                Assert.AreEqual((itemTables ? "source" : "terrain") + " item table", strings.GetValue(int.Parse(info.RandomItemsTables[0].TableName.Substring(8))));
            }
            foreach (var original in originals) CollectionAssert.AreEqual(original.Value, File.ReadAllBytes(original.Key));
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void LuaSourceOrDestinationStopsWholeBatchBeforeAnyMerging(bool luaSource)
        {
            PackMap(source, true, luaSource ? 1 : 0);
            PackMap(Path.Combine(targets, "first 1.0.w3x"), false);
            PackMap(Path.Combine(targets, "last 1.0.w3x"), false, luaSource ? 0 : 1);
            string existing = Path.Combine(output, "existing.w3x");
            File.WriteAllText(existing, "existing output stays intact");
            var inputs = Directory.GetFiles(targets).Concat(new[] { source })
                .ToDictionary(path => path, File.ReadAllBytes);
            new WarTransfer.WarTransfer().ExecuteDataTransfer(handler, Args());
            Assert.IsTrue(handler.KillProcess);
            string error = string.Join("\n", handler.Errors);
            StringAssert.Contains(error, "Lua scripts are not supported");
            StringAssert.Contains(error, (luaSource ? "source" : "destination") + " map");
            Assert.IsFalse(log.ToString().Contains("Starting data transfer"), log.ToString());
            Assert.IsFalse(log.ToString().Contains("Copying data to map"), log.ToString());
            CollectionAssert.AreEqual(new[] { existing }, Directory.GetFiles(output));
            Assert.AreEqual("existing output stays intact", File.ReadAllText(existing));
            foreach (var input in inputs) CollectionAssert.AreEqual(input.Value, File.ReadAllBytes(input.Key));
        }

        [TestMethod]
        public void RejectsLuaSourceBeforeOpeningDestinationArchives()
        {
            PackMap(source, true, 1);
            File.WriteAllText(Path.Combine(targets, "broken.w3x"), "not an archive");
            new WarTransfer.WarTransfer().ExecuteDataTransfer(handler, Args());
            Assert.IsTrue(handler.KillProcess);
            Assert.AreEqual(1, handler.Errors.Count, log.ToString());
            StringAssert.Contains(handler.Errors[0], "Lua scripts are not supported: source map");
            Assert.AreEqual(0, Directory.GetFiles(output).Length);
        }

        [TestMethod]
        public void LateScriptFailureLeavesExistingBatchOutputsUntouched()
        {
            PackMap(source, true);
            PackMap(Path.Combine(targets, "first 1.0.w3x"), false);
            PackMap(Path.Combine(targets, "second 1.0.w3x"), false, scriptOverride: "globals\nendglobals\nfunction main takes nothing returns nothing\n");
            string first = Path.Combine(output, "first 2.0.w3x");
            File.WriteAllText(first, "existing output");
            new WarTransfer.WarTransfer().ExecuteDataTransfer(handler, Args());
            Assert.IsTrue(handler.KillProcess);
            Assert.AreEqual("existing output", File.ReadAllText(first));
            Assert.AreEqual(1, Directory.GetFiles(output).Length);
            StringAssert.Contains(string.Join("\n", handler.Errors), "endfunction");
        }

        [TestMethod]
        public void CliReportsPartialPublicationAccuratelyOnSecondOutputFailure()
        {
            PackMap(source, true);
            PackMap(Path.Combine(targets, "first.w3x"), false);
            PackMap(Path.Combine(targets, "second.w3x"), false);
            string first = Path.Combine(output, "first.w3x"), second = Path.Combine(output, "second.w3x");
            File.WriteAllText(first, "old first"); File.WriteAllText(second, "old second");
            var stdout = new StringWriter(); var stderr = new StringWriter();
            using (File.Open(second, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.AreEqual(1, Program.Run(new[] { "--source", source, "--targets", targets, "--output", output,
                    "--mpq-editor", editor, "--overwrite", "--json" }, stdout, stderr), stderr.ToString());
            var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(stdout.ToString());
            Assert.AreEqual(false, result["success"]);
            Assert.AreEqual(1, result["exit_code"]);
            StringAssert.Contains(new JavaScriptSerializer().Serialize(result["outputs"]), "first.w3x");
            Assert.IsFalse(new JavaScriptSerializer().Serialize(result["outputs"]).Contains("second.w3x"));
            Assert.AreEqual("old second", File.ReadAllText(second));
            Assert.AreEqual(0, Directory.GetFiles(output, "*.tmp").Length);
        }

        [TestMethod]
        public void SourceArchiveInTargetsIsExcludedFromTheRealTransfer()
        {
            source = Path.Combine(targets, "source 2.0.w3x");
            PackMap(source, true);
            PackMap(Path.Combine(targets, "terrain 1.0.w3x"), false);
            var before = File.ReadAllBytes(source);
            new WarTransfer.WarTransfer().ExecuteDataTransfer(handler, Args());
            Assert.IsFalse(handler.KillProcess, log.ToString());
            CollectionAssert.AreEqual(new[] { Path.Combine(output, "terrain 2.0.w3x") }, Directory.GetFiles(output));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(source));
        }
    }
}
