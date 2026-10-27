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
    public class ScriptTypeTests
    {
        private string root;
        private CommandLineErrorHandler handler;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "Script type tests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            handler = new CommandLineErrorHandler(TextWriter.Null, false);
        }

        [TestCleanup]
        public void Cleanup() { Directory.Delete(root, true); }

        private IMapScriptMerger Open(string role = "source") =>
            MapScripts.Open(handler, root, role, new WarTransferArgs());

        [DataTestMethod]
        [DataRow(18)]
        [DataRow(25)]
        [DataRow(28)]
        [DataRow(31)]
        [DataRow(32)]
        [DataRow(33)]
        [DataRow(39)]
        public void SelectsJassForLegacyAndCurrentMetadata(int version)
        {
            // Older layouts do not serialize this field, even when set to Lua in memory.
            MapTestFixture.WriteInfo(root, version < 28 ? 1 : 0, version);
            File.WriteAllText(Path.Combine(root, "war3map.j"), "not parsed during script type preflight");
            var merger = Open();
            Assert.IsNotNull(merger);
            Assert.AreEqual(MapScriptType.Jass, merger.ScriptType);
            Assert.IsFalse(handler.KillProcess, string.Join("\n", handler.Errors));
        }

        [DataTestMethod]
        [DataRow("source", false)]
        [DataRow("destination", false)]
        [DataRow("source", true)]
        [DataRow("destination", true)]
        public void SelectsLuaFromMetadataEvenWithAnInactiveJassScript(string role, bool includeJass)
        {
            MapTestFixture.WriteInfo(root, 1);
            File.WriteAllText(Path.Combine(root, "war3map.lua"), "function main() end");
            if (includeJass) File.WriteAllText(Path.Combine(root, "war3map.j"), "inactive JASS");
            Assert.AreEqual(MapScriptType.Lua, Open(role).ScriptType);
            Assert.IsFalse(handler.KillProcess);
        }

        [TestMethod]
        public void MissingDeclaredLuaScriptFailsEvenWithInactiveJass()
        {
            MapTestFixture.WriteInfo(root, 1);
            File.WriteAllText(Path.Combine(root, "war3map.j"), "inactive JASS");
            Assert.IsNull(Open("destination"));
            StringAssert.Contains(string.Join("\n", handler.Errors), "Missing Lua script (war3map.lua)");
        }

        [DataTestMethod]
        [DataRow(28)] [DataRow(31)] [DataRow(32)] [DataRow(33)] [DataRow(39)]
        public void SelectsLuaForMetadataLayoutsWithLanguageFields(int version)
        {
            MapTestFixture.WriteInfo(root, 1, version);
            File.WriteAllText(Path.Combine(root, "war3map.lua"), "not parsed in language preflight");
            Assert.AreEqual(MapScriptType.Lua, Open().ScriptType);
            Assert.IsFalse(handler.KillProcess);
        }

        [TestMethod]
        public void MetadataSelectsJassWhenAnInactiveLuaScriptAlsoExists()
        {
            MapTestFixture.WriteInfo(root);
            File.WriteAllText(Path.Combine(root, "war3map.j"), "active JASS");
            File.WriteAllText(Path.Combine(root, "war3map.lua"), "inactive Lua");
            Assert.AreEqual(MapScriptType.Jass, Open().ScriptType);
            Assert.IsFalse(handler.KillProcess);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(2)]
        public void RejectsUnknownScriptTypeInsteadOfFallingBackToJass(int type)
        {
            MapTestFixture.WriteInfo(root, type);
            File.WriteAllText(Path.Combine(root, "war3map.j"), "JASS");
            Assert.IsNull(Open("destination"));
            Assert.IsTrue(handler.KillProcess);
            StringAssert.Contains(string.Join("\n", handler.Errors), "Unsupported script type " + type);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MissingDeclaredJassScriptFailsPreflight(bool hasLua)
        {
            MapTestFixture.WriteInfo(root);
            if (hasLua) File.WriteAllText(Path.Combine(root, "war3map.lua"), "function main() end");
            Assert.IsNull(Open("destination"));
            Assert.IsTrue(handler.KillProcess);
            string error = string.Join("\n", handler.Errors);
            StringAssert.Contains(error, "Missing JASS script (war3map.j)");
            StringAssert.Contains(error, "destination map");
            if (hasLua) StringAssert.Contains(error, "metadata declares JASS");
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MissingOrInvalidMetadataFailsBeforeScriptParsing(bool invalid)
        {
            if (invalid) File.WriteAllText(Path.Combine(root, "war3map.w3i"), "invalid metadata");
            File.WriteAllText(Path.Combine(root, "war3map.j"), "not parsed");
            Assert.IsNull(Open());
            Assert.IsTrue(handler.KillProcess);
            StringAssert.Contains(string.Join("\n", handler.Errors), "war3map.w3i");
        }
    }

    internal static class MapTestFixture
    {
        internal static void WriteInfo(string directory, int type = 0, int version = 39)
        {
            using (var reader = new BinaryReader(File.OpenRead("../../TestFiles/test39.w3i")))
            {
                var info = W3iFile.Read(reader);
                info.FormatVersion = version;
                info.ScriptTypeId = type;
                WriteInfo(directory, info);
            }
        }

        internal static void WriteInfo(string directory, W3iFile info)
        {
            using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "war3map.w3i"))))
                info.Serialize(writer);
        }
    }
}
