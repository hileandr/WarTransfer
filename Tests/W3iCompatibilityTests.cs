using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using WarTransfer;
using WarTransfer.FileTypes;

namespace Tests
{
    [TestClass]
    public class W3iCompatibilityTests
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        private static W3iFile Read(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream, Utf8))
            {
                var file = W3iFile.Read(reader);
                Assert.AreEqual(stream.Length, stream.Position, "The entire W3I must be consumed.");
                return file;
            }
        }

        private static byte[] Write(W3iFile file)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Utf8, true))
                    file.Serialize(writer);
                return stream.ToArray();
            }
        }

        [DataTestMethod]
        [DataRow("test33.w3i")]
        [DataRow("test39.w3i")]
        [DataRow("test39_filled.w3i")]
        public void EditorFilesRoundTripWithoutDataLoss(string name)
        {
            var bytes = File.ReadAllBytes("../../TestFiles/" + name);
            CollectionAssert.AreEqual(bytes, Write(Read(bytes)));
        }

        [TestMethod]
        public void Version39ReadsNewFieldsAndKeepsThemAfterStringEdits()
        {
            var file = Read(File.ReadAllBytes("../../TestFiles/test39.w3i"));
            Assert.AreEqual(39, file.FormatVersion);
            Assert.AreEqual(8, file.LoadingScreenCrestRace);
            Assert.AreEqual(10000f, file.TerrainFogLinearStart);
            Assert.AreEqual(10000f, file.TerrainFogLinearEnd);
            Assert.AreEqual(1f, file.TerrainFogMaxOpacity);
            Assert.AreEqual("lake", file.CustomSoundEnvironment);
            Assert.AreEqual(1444, file.ForceDefaultCameraZoom);
            Assert.AreEqual(1555, file.ForceMaxCameraZoom);
            Assert.AreEqual(1333, file.ForceMinCameraZoom);
            Assert.AreEqual(20, file.WaterMinOpacity);
            Assert.AreEqual(100, file.WaterMaxOpacity);
            Assert.AreEqual(10, file.WaterReflectivity);
            Assert.AreEqual(50, file.WaterEdgeSoftness);
            Assert.AreEqual(-1, file.WaterUnknown);
            Assert.AreEqual(6, file.PlayerCount);
            Assert.AreEqual(2, file.Players[0].HudSkin);
            Assert.AreEqual(8, file.Players[1].HudSkin);
            Assert.AreEqual(64, file.Players[2].HudSkin);
            Assert.AreEqual(1, file.Players[1].FixedStartLocation);
            Assert.AreEqual("TRIGSTR_011", file.Players[1].Name);

            var original = Write(file);
            var oldName = file.Name;
            var oldPlayerName = file.Players[0].Name;
            var oldForceName = file.Forces[0].Name;
            file.Name = "Updated map — 地图";
            file.Players[0].Name = "Joueur é";
            file.Forces[0].Name = "New force name";
            var changed = Read(Write(file));
            Assert.AreEqual(file.Name, changed.Name);
            Assert.AreEqual(file.Players[0].Name, changed.Players[0].Name);
            Assert.AreEqual(file.Forces[0].Name, changed.Forces[0].Name);
            changed.Name = oldName;
            changed.Players[0].Name = oldPlayerName;
            changed.Forces[0].Name = oldForceName;
            CollectionAssert.AreEqual(original, Write(changed), "Editing strings must preserve every other field.");
        }

        // Construct the documented wire layout independently of W3iFile.Serialize.
        private static byte[] CameraFixture(int version)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Utf8, true))
                {
                    writer.Write(version);
                    writer.Write(1); writer.Write(1); // saves, editor
                    for (int i = 0; i < 4; i++) writer.Write(1); // game version
                    writer.Write(new byte[4]); // four empty strings
                    writer.Write(new byte[56]); // camera bounds, complements, dimensions
                    writer.Write(0); writer.Write((byte)'L'); // flags, tileset
                    writer.Write(-1); writer.Write(new byte[4]); // loading screen
                    writer.Write(0); writer.Write(new byte[4]); // data set, prologue
                    writer.Write(new byte[24]); // fog, weather
                    writer.Write((byte)0); writer.Write((byte)'L'); // sound, lighting
                    writer.Write(new byte[4]); // water tint
                    writer.Write(0); writer.Write(3); writer.Write(1); // script, modes, data
                    if (version >= 32) { writer.Write(1650); writer.Write(3000); }
                    if (version >= 33) writer.Write(500);
                    writer.Write(new byte[24]); // six empty collections
                }
                return stream.ToArray();
            }
        }

        [DataTestMethod]
        [DataRow(31)]
        [DataRow(32)]
        [DataRow(33)]
        public void CameraLayoutUsesCorrectVersionAndOrder(int version)
        {
            var bytes = CameraFixture(version);
            var file = Read(bytes);
            Assert.AreEqual(version >= 32 ? 1650 : 0, file.ForceDefaultCameraZoom);
            Assert.AreEqual(version >= 32 ? 3000 : 0, file.ForceMaxCameraZoom);
            Assert.AreEqual(version >= 33 ? 500 : 0, file.ForceMinCameraZoom);
            CollectionAssert.AreEqual(bytes, Write(file));
        }

        [TestMethod]
        public void UnknownVersionFailsBeforeInterpretingStrings()
        {
            var ex = Assert.ThrowsException<NotSupportedException>(() => Read(BitConverter.GetBytes(40)));
            StringAssert.Contains(ex.Message, "format version 40");
        }

        [TestMethod]
        public void InvalidUtf8IsReportedWithFormatAndOffset()
        {
            var bytes = CameraFixture(33);
            bytes[28] = 0x80;
            var ex = Assert.ThrowsException<FormatException>(() => Read(bytes));
            Assert.IsInstanceOfType(ex.InnerException, typeof(DecoderFallbackException));
            StringAssert.Contains(ex.Message, "format 33, byte offset");
        }

        [TestMethod]
        public void TruncatedVersion39IsRejected()
        {
            var bytes = File.ReadAllBytes("../../TestFiles/test39.w3i");
            Array.Resize(ref bytes, bytes.Length - 1);
            var ex = Assert.ThrowsException<FormatException>(() => Read(bytes));
            Assert.IsInstanceOfType(ex.InnerException, typeof(EndOfStreamException));
        }

        [TestMethod]
        public void EveryTruncatedPrefixOfVersion39IsRejected()
        {
            var bytes = File.ReadAllBytes("../../TestFiles/test39.w3i");
            for (int length = 0; length < bytes.Length; length++)
            {
                var prefix = new byte[length];
                Array.Copy(bytes, prefix, length);
                Assert.ThrowsException<FormatException>(() => Read(prefix), "Accepted truncated length " + length);
            }
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(int.MaxValue)]
        public void InvalidCollectionCountsAreRejectedBeforeAllocation(int count)
        {
            // The independently constructed layout ends with six collection counts.
            var bytes = CameraFixture(33);
            Array.Copy(BitConverter.GetBytes(count), 0, bytes, bytes.Length - 24, 4);
            var ex = Assert.ThrowsException<FormatException>(() => Read(bytes));
            StringAssert.Contains(ex.Message, "PlayerCount");
        }

        [TestMethod]
        public void ProductionFileIoPreservesVersion39AndStopsOnWriteFailure()
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = File.ReadAllBytes("../../TestFiles/test39_filled.w3i");
                File.WriteAllBytes(path, bytes);
                var handler = new WorkflowHandler();
                var file = W3iUtility.ReadFile(handler, path);
                Assert.IsFalse(handler.KillProcess, handler.Error);
                W3iUtility.WriteFile(handler, path, file);
                Assert.IsFalse(handler.KillProcess, handler.Error);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));

                file.FormatVersion = 40;
                W3iUtility.WriteFile(handler, path, file);
                Assert.IsTrue(handler.KillProcess);
                StringAssert.Contains(handler.Error, "format version 40");
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private sealed class WorkflowHandler : IWorkflowHandler
        {
            public int CurrentStep { get; set; }
            public int TotalSteps { get; set; }
            public bool KillProcess { get; set; }
            public string Error { get; private set; }
            public void ReportError(string error, bool killProcess)
            {
                Error = error;
                KillProcess |= killProcess;
            }
            public void ReportError(Exception error, bool killProcess) => ReportError(error.Message, killProcess);
            public void Log(LogType logType, string message) { }
        }
    }
}
