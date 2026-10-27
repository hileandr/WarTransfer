using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using WarTransfer;
using WarTransfer.FileTypes;
using WarTransfer.Scripts;
using WarTransferCL;

namespace Tests
{
    [TestClass, TestCategory("Lua")]
    public class LuaExecutionTests
    {
        private string root, runtime;

        [TestInitialize]
        public void Setup()
        {
            runtime = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_LUA");
            if (!File.Exists(runtime)) Assert.Inconclusive("Set WARTRANSFER_TEST_LUA to the Lua 5.3 interpreter; Tests/BuildLua.ps1 builds it locally.");
            root = Path.Combine(Path.GetTempPath(), "Lua runtime " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TestCleanup]
        public void Cleanup() { if (root != null) Directory.Delete(root, true); }

        private int Run(string script, string arguments, out string log)
        {
            using (var process = new Process { StartInfo = new ProcessStartInfo(runtime,
                "\"" + script + "\" " + arguments) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true } })
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000)) { process.Kill(); Assert.Fail("Lua test interpreter timed out."); }
                log = stdout.Result + stderr.Result;
                return process.ExitCode;
            }
        }

        private string Fixture(string name) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Lua", name);

        private string Merge(bool cameras, bool regions, bool groups, bool items)
        {
            string source = Path.Combine(root, "source"), terrain = Path.Combine(root, "terrain"), output = Path.Combine(root, "output");
            foreach (var directory in new[] { source, terrain, output }) Directory.CreateDirectory(directory);
            File.Copy(Fixture("Source.lua"), Path.Combine(source, "war3map.lua"));
            File.Copy(Fixture("Terrain.lua"), Path.Combine(terrain, "war3map.lua"));
            var sourceStrings = new WtsFile(); var terrainStrings = new WtsFile();
            foreach (string text in new[] { "unused", "title" }) { WtsUtility.AddBlock(sourceStrings, text); WtsUtility.AddBlock(terrainStrings, text); }
            var handler = new CommandLineErrorHandler(TextWriter.Null, false);
            new LuaScriptMerger(new WarTransferArgs { TransferCameras = cameras, TransferRegions = regions,
                TransferUnitTables = groups, TransferItemTables = items }).Merge(handler, source, terrain, output, sourceStrings, terrainStrings);
            Assert.IsFalse(handler.KillProcess, string.Join("\n", handler.Errors));
            return Path.Combine(output, "war3map.lua");
        }

        [DataTestMethod]
        [DataRow(false, false, false, false)] [DataRow(true, false, false, false)]
        [DataRow(false, true, false, false)] [DataRow(false, false, true, false)]
        [DataRow(false, false, false, true)] [DataRow(true, true, true, true)]
        [DataRow(true, false, true, false)] [DataRow(false, true, false, true)]
        public void MergedScriptExecutesSelectedTerrainAndSourceGameplayInOrder(bool cameras, bool regions, bool groups, bool items)
        {
            string merged = Merge(cameras, regions, groups, items), log;
            string arguments = "\"" + merged + "\" " + string.Join(" ", new[] { cameras, regions, groups, items }).ToLowerInvariant();
            Assert.AreEqual(0, Run(Fixture("AssertMerged.lua"), arguments, out log), log);
            StringAssert.Contains(log, "merged Lua runtime checks passed");
        }

        [TestMethod]
        public void IndependentRuntimeChecksRejectChangedGameplay()
        {
            string merged = Merge(false, false, false, false), log;
            File.WriteAllText(merged, File.ReadAllText(merged).Replace("source gameplay", "wrong gameplay"), new UTF8Encoding(false));
            Assert.AreNotEqual(0, Run(Fixture("AssertMerged.lua"), "\"" + merged + "\" false false false false", out log));
            StringAssert.Contains(log, "expected source gameplay");
        }
    }
}
