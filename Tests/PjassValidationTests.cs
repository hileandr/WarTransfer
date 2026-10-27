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
    [TestCategory("Pjass")]
    public class PjassValidationTests
    {
        private string root, pjass;
        [TestInitialize]
        public void Setup()
        {
            pjass = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_PJASS");
            if (string.IsNullOrEmpty(pjass)) pjass = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Warcraft III\_retail_\x86_64\JassHelper\pjass.exe");
            if (!File.Exists(pjass)) Assert.Inconclusive("Set WARTRANSFER_TEST_PJASS to enable independent script checks.");
            root = Path.Combine(Path.GetTempPath(), "pjass tests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }
        [TestCleanup]
        public void Cleanup() { if (root != null) Directory.Delete(root, true); }

        [TestMethod]
        public void MergedScriptPassesIndependentCompilerAndBrokenScriptFails()
        {
            string source = Path.Combine(root, "source"), terrain = Path.Combine(root, "terrain"), output = Path.Combine(root, "output");
            Directory.CreateDirectory(source); Directory.CreateDirectory(terrain); Directory.CreateDirectory(output);
            const string prefix = "globals\ninteger udg_Gameplay = 0\nendglobals\n";
            const string natives = "native SetPlayers takes integer n returns nothing\n" +
                "function InitBlizzard takes nothing returns nothing\nendfunction\n";
            File.WriteAllText(Path.Combine(source, "war3map.j"), prefix + natives +
                "function Gameplay takes nothing returns nothing\nset udg_Gameplay = 42\nendfunction\n" +
                "function config takes nothing returns nothing\ncall SetPlayers(2)\nendfunction\n" +
                "function main takes nothing returns nothing\ncall InitBlizzard()\ncall Gameplay()\nendfunction\n");
            File.WriteAllText(Path.Combine(terrain, "war3map.j"), prefix + natives +
                "function config takes nothing returns nothing\ncall SetPlayers(4)\nendfunction\n" +
                "function main takes nothing returns nothing\ncall InitBlizzard()\nendfunction\n");
            var log = new StringWriter();
            var handler = new CommandLineErrorHandler(log, true);
            new JassScriptMerger(new WarTransferArgs()).Merge(handler, source, terrain, output, new WtsFile(), new WtsFile());
            Assert.IsFalse(handler.KillProcess, log.ToString());
            string result = Path.Combine(output, "war3map.j");
            Assert.AreEqual(0, IOUtility.RunProcessCommand(handler, pjass, "\"" + result + "\""), log.ToString());
            File.AppendAllText(result, "function broken takes nothing returns nothing\ncall DoesNotExist()\nendfunction\n");
            Assert.AreNotEqual(0, IOUtility.RunProcessCommand(handler, pjass, "\"" + result + "\""));
        }

        [TestMethod]
        public void LocalCompiledMapRoundTripsAndPassesPjass()
        {
            string script = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_REAL_JASS");
            if (string.IsNullOrEmpty(script)) script = Path.Combine(Path.GetDirectoryName(pjass), @"logs\outputwar3map.j");
            string libraries = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_JASS_LIBRARIES");
            if (string.IsNullOrEmpty(libraries)) libraries = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Warcraft III\JassHelper");
            if (!File.Exists(script) || !File.Exists(Path.Combine(libraries, "common.j")) || !File.Exists(Path.Combine(libraries, "blizzard.j")))
                Assert.Inconclusive("Set WARTRANSFER_TEST_REAL_JASS and WARTRANSFER_TEST_JASS_LIBRARIES for a real compiled-script check.");
            var log = new StringWriter();
            var handler = new CommandLineErrorHandler(log, true);
            var parsed = JUtility.ReadFile(handler, script);
            Assert.IsFalse(handler.KillProcess, log.ToString());
            string output = Path.Combine(root, "roundtrip.j");
            JUtility.WriteFile(output, parsed);
            Assert.AreEqual(File.ReadAllText(script).Replace("\r\n", "\n").TrimEnd('\n'),
                File.ReadAllText(output).Replace("\r\n", "\n").TrimEnd('\n'));
            Assert.AreEqual(0, IOUtility.RunProcessCommand(handler, pjass,
                "\"" + Path.Combine(libraries, "common.j") + "\" \"" + Path.Combine(libraries, "blizzard.j") + "\" \"" + output + "\""), log.ToString());
        }
    }
}
