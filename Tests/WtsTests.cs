using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using WarTransfer;
using WarTransfer.FileTypes;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    public class WtsTests
    {
        private string root, file;
        private CommandLineErrorHandler handler;
        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "WTS tests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            file = Path.Combine(root, "war3map.wts");
            handler = new CommandLineErrorHandler(TextWriter.Null, false);
        }
        [TestCleanup]
        public void Cleanup() { Directory.Delete(root, true); }

        [TestMethod]
        public void EmptyUnicodeMultilineAndHeaderLikeValuesRoundTrip()
        {
            File.WriteAllText(file, "// header\nSTRING 0\n{\n\n}\nSTRING 99\n{\n地图 é\nSTRING 123 is value text\nlast line\n}\n");
            var values = WtsUtility.ReadFile(handler, file);
            Assert.IsFalse(handler.KillProcess);
            Assert.AreEqual("", values.GetValue(0));
            Assert.AreEqual("地图 é\r\nSTRING 123 is value text\r\nlast line", values.GetValue(99));
            WtsUtility.WriteFile(file, values);
            Assert.AreEqual(values.GetValue(99), WtsUtility.ReadFile(handler, file).GetValue(99));
        }

        [DataTestMethod]
        [DataRow("STRING 1\n{\nvalue")]
        [DataRow("STRING 1\nvalue\n}")]
        [DataRow("STRING 1\n{\na\n}\nSTRING 1\n{\nb\n}")]
        [DataRow("STRING bad\n{\na\n}")]
        public void MalformedWtsStopsWorkflow(string text)
        {
            File.WriteAllText(file, text);
            WtsUtility.ReadFile(handler, file);
            Assert.IsTrue(handler.KillProcess);
            StringAssert.Contains(string.Join("\n", handler.Errors), "Invalid WTS");
        }

        private static WtsFile Values(int key, string value)
        {
            var strings = new WtsFile();
            strings.FileMap.Add(key, new[] { "STRING " + key, "{", value, "}" });
            return strings;
        }

        [TestMethod]
        public void ReferenceLiteralTransitionsAndCollisionsPreserveUnrelatedEntries()
        {
            var old = Values(5, "terrain");
            var output = Values(5, "gameplay");
            string imported = WtsUtility.RemapStrRef(handler, old, output, "literal", "TRIGSTR_005");
            Assert.AreNotEqual("TRIGSTR_005", imported);
            Assert.AreEqual("gameplay", output.GetValue(5));
            Assert.AreEqual("terrain", output.GetValue(StringUtility.ExtractRefKey(imported)));
            Assert.AreEqual("TRIGSTR_005", WtsUtility.RemapStrRef(handler, old, output, "TRIGSTR_005", ""));
            Assert.AreEqual("", output.GetValue(5));
        }

        [TestMethod]
        public void MissingReferenceFailsWithoutChangingExistingValues()
        {
            var output = Values(5, "keep");
            Assert.AreEqual("TRIGSTR_005", WtsUtility.RemapStrRef(handler, new WtsFile(), output, "TRIGSTR_005", "TRIGSTR_999"));
            Assert.IsTrue(handler.KillProcess);
            Assert.AreEqual("keep", output.GetValue(5));
        }

        [TestMethod]
        public void MissingSourceReferenceWithLiteralReplacementReportsItsKey()
        {
            var output = Values(5, "keep");
            Assert.AreEqual("TRIGSTR_999", WtsUtility.RemapStrRef(handler, new WtsFile(), output, "TRIGSTR_999", "literal"));
            Assert.IsTrue(handler.KillProcess);
            StringAssert.Contains(string.Join("\n", handler.Errors), "TRIGSTR_999");
            Assert.AreEqual("keep", output.GetValue(5));
        }
    }
}
