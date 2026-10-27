using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using WarTransfer;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class ArchiveOverlayTests
    {
        private string root, contents, original, staged, editor;
        private CommandLineErrorHandler handler;
        [TestInitialize]
        public void Setup()
        {
            editor = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_MPQ_EDITOR");
            if (!File.Exists(editor)) Assert.Inconclusive("Set WARTRANSFER_TEST_MPQ_EDITOR.");
            root = Path.Combine(Path.GetTempPath(), "Overlay tests " + Guid.NewGuid().ToString("N"));
            contents = Path.Combine(root, "contents"); original = Path.Combine(root, "source.w3x");
            staged = Path.Combine(root, "staged");
            Directory.CreateDirectory(contents);
            File.WriteAllText(Path.Combine(contents, "unique-fixture-asset.bin"), "unmodified payload");
            File.WriteAllText(Path.Combine(contents, "war3map.j"), "old script");
            handler = new CommandLineErrorHandler(TextWriter.Null, false);
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(handler, contents, original, editor));
        }
        [TestCleanup]
        public void Cleanup() { if (root != null) Directory.Delete(root, true); }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void OverlayPreservesNamedAndUnnamedAssetsAndInputArchive(bool unnamed)
        {
            if (unnamed) HideAssetName(original);
            var before = File.ReadAllBytes(original);
            Assert.IsTrue(MPQUtility.MapFileToMapDirectory(handler, original, contents, editor));
            if (unnamed) Assert.IsTrue(Directory.GetFiles(contents, "File*.*").Length > 0, "Fixture must contain genuinely unnamed entries.");
            Assert.IsTrue(IOUtility.CopyDirectory(handler, contents, staged));
            File.WriteAllText(Path.Combine(staged, "war3map.j"), "updated script");
            string result = Path.Combine(root, "result.w3x");
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(handler, staged, result, editor, original, contents), string.Join("\n", handler.Errors));
            string extracted = Path.Combine(root, "result");
            Assert.IsTrue(MPQUtility.MapFileToMapDirectory(handler, result, extracted, editor));
            Assert.AreEqual("updated script", File.ReadAllText(Path.Combine(extracted, "war3map.j")));
            foreach (string asset in Directory.GetFiles(contents).Where(f => Path.GetFileName(f) != "war3map.j" && !Path.GetFileName(f).StartsWith("(")))
                CollectionAssert.AreEqual(File.ReadAllBytes(asset), File.ReadAllBytes(Path.Combine(extracted, Path.GetFileName(asset))));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
        }

        [TestMethod]
        public void AddedOrRemovedEntriesUseFreshArchiveWithExactFileSet()
        {
            Assert.IsTrue(IOUtility.CopyDirectory(handler, contents, staged));
            File.Delete(Path.Combine(staged, "unique-fixture-asset.bin"));
            File.WriteAllText(Path.Combine(staged, "new-asset.txt"), "new payload");
            string result = Path.Combine(root, "result.w3x");
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(handler, staged, result, editor, original, contents));
            string extracted = Path.Combine(root, "result");
            Assert.IsTrue(MPQUtility.MapFileToMapDirectory(handler, result, extracted, editor));
            Assert.IsFalse(File.Exists(Path.Combine(extracted, "unique-fixture-asset.bin")));
            Assert.AreEqual("new payload", File.ReadAllText(Path.Combine(extracted, "new-asset.txt")));
        }

        [TestMethod]
        public void ModifyingUnnamedEntryFailsWithoutReplacingExistingOutput()
        {
            HideAssetName(original);
            Assert.IsTrue(MPQUtility.MapFileToMapDirectory(handler, original, contents, editor));
            Assert.IsTrue(IOUtility.CopyDirectory(handler, contents, staged));
            File.AppendAllText(Directory.GetFiles(staged, "File*.*").Single(), "modified");
            string result = Path.Combine(root, "existing.w3x");
            File.WriteAllText(result, "existing output");
            Assert.IsFalse(MPQUtility.MapDirectoryToMapFile(handler, staged, result, editor, original, contents));
            Assert.AreEqual("existing output", File.ReadAllText(result));
            StringAssert.Contains(string.Join("\n", handler.Errors), "Cannot modify unnamed");
        }

        [TestMethod]
        public void NamedMapWithWarcraftPreambleRetainsRebuildBehavior()
        {
            byte[] mpq = File.ReadAllBytes(original);
            var headered = new byte[mpq.Length + 512];
            Array.Copy(new byte[] { 0x48, 0x4D, 0x33, 0x57 }, headered, 4);
            Array.Copy(mpq, 0, headered, 512, mpq.Length);
            File.WriteAllBytes(original, headered);
            Assert.IsTrue(IOUtility.CopyDirectory(handler, contents, staged));
            File.WriteAllText(Path.Combine(staged, "war3map.j"), "new script");
            string result = Path.Combine(root, "result.w3x");
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(handler, staged, result, editor, original, contents));
            Assert.AreEqual(0x1A51504Du, BitConverter.ToUInt32(File.ReadAllBytes(result), 0));
            CollectionAssert.AreEqual(headered, File.ReadAllBytes(original));
        }

        // Test fixture manipulation only: rename an asset through its hash entry without
        // updating its listfile, producing a genuine unnamed import. MPQ v1 wire
        // format/hash algorithms: https://github.com/ladislav-zezula/StormLib
        private static void HideAssetName(string archive)
        {
            var bytes = File.ReadAllBytes(archive);
            Assert.AreEqual(0x1A51504Du, BitConverter.ToUInt32(bytes, 0));
            Assert.AreEqual((ushort)0, BitConverter.ToUInt16(bytes, 12), "Fixture requires MPQ v1.");
            int offset = checked((int)BitConverter.ToUInt32(bytes, 16));
            int count = checked((int)BitConverter.ToUInt32(bytes, 24));
            var crypt = new uint[0x500];
            uint seed = 0x100001;
            for (int low = 0; low < 256; low++)
                for (int bank = 0; bank < 5; bank++)
                {
                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    uint high = (seed & 0xffff) << 16;
                    seed = (seed * 125 + 3) % 0x2AAAAB;
                    crypt[bank * 256 + low] = high | (seed & 0xffff);
                }
            var words = new uint[count * 4];
            uint key = 0xC3AF3770, state = 0xEEEEEEEE;
            for (int i = 0; i < words.Length; i++)
            {
                state = unchecked(state + crypt[0x400 + (key & 255)]);
                words[i] = BitConverter.ToUInt32(bytes, offset + i * 4) ^ unchecked(key + state);
                key = unchecked(((~key << 21) + 0x11111111) | (key >> 11));
                state = unchecked(words[i] + state + (state << 5) + 3);
            }
            uint first = Hash("unique-fixture-asset.bin", 1, crypt), second = Hash("unique-fixture-asset.bin", 2, crypt);
            int oldIndex = -1;
            for (int i = 0; i < words.Length; i += 4)
                if (words[i] == first && words[i + 1] == second) oldIndex = i;
            Assert.IsTrue(oldIndex >= 0);
            uint locale = words[oldIndex + 2], block = words[oldIndex + 3];
            words[oldIndex + 3] = 0xFFFFFFFE;
            const string hiddenName = "opaque-protected-name.bin";
            int slot = (int)(Hash(hiddenName, 0, crypt) % count) * 4;
            while (words[slot + 3] != 0xFFFFFFFF && words[slot + 3] != 0xFFFFFFFE) slot = (slot + 4) % words.Length;
            words[slot] = Hash(hiddenName, 1, crypt); words[slot + 1] = Hash(hiddenName, 2, crypt);
            words[slot + 2] = locale; words[slot + 3] = block;
            key = 0xC3AF3770; state = 0xEEEEEEEE;
            for (int i = 0; i < words.Length; i++)
            {
                state = unchecked(state + crypt[0x400 + (key & 255)]);
                Array.Copy(BitConverter.GetBytes(words[i] ^ unchecked(key + state)), 0, bytes, offset + i * 4, 4);
                key = unchecked(((~key << 21) + 0x11111111) | (key >> 11));
                state = unchecked(words[i] + state + (state << 5) + 3);
            }
            File.WriteAllBytes(archive, bytes);
        }

        private static uint Hash(string text, int bank, uint[] table)
        {
            uint a = 0x7FED7FED, b = 0xEEEEEEEE;
            foreach (char c in text.ToUpperInvariant())
            {
                a = table[bank * 256 + c] ^ unchecked(a + b);
                b = unchecked(c + a + b + (b << 5) + 3);
            }
            return a;
        }
    }
}
