using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    public class PublishingTests
    {
        private string root, staging, first, second;
        private CommandLineErrorHandler handler;
        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "Publishing tests " + Guid.NewGuid().ToString("N"));
            staging = Path.Combine(root, "staging");
            Directory.CreateDirectory(staging);
            first = Path.Combine(root, "first.w3x"); second = Path.Combine(root, "second.w3x");
            File.WriteAllText(first, "old first"); File.WriteAllText(second, "old second");
            File.WriteAllText(Path.Combine(staging, "first.w3x"), "new first");
            File.WriteAllText(Path.Combine(staging, "second.w3x"), "new second");
            handler = new CommandLineErrorHandler(TextWriter.Null, false);
        }
        [TestCleanup]
        public void Cleanup() { Directory.Delete(root, true); }

        [TestMethod]
        public void MissingStagedOutputPreventsWholeBatchPublication()
        {
            File.Delete(Path.Combine(staging, "second.w3x"));
            var published = new List<string>();
            Assert.ThrowsException<IOException>(() => OutputPublisher.Publish(new[] { first, second }, staging, true, handler, published));
            Assert.AreEqual(0, published.Count);
            Assert.AreEqual("old first", File.ReadAllText(first));
        }

        [TestMethod]
        public void LockedSecondOutputPreservesItAndReportsOnlyPublishedFirstOutput()
        {
            var published = new List<string>();
            using (File.Open(second, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.ThrowsException<IOException>(() => OutputPublisher.Publish(new[] { first, second }, staging, true, handler, published));
            CollectionAssert.AreEqual(new[] { first }, published);
            Assert.AreEqual("new first", File.ReadAllText(first));
            Assert.AreEqual("old second", File.ReadAllText(second));
            Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp").Length);
        }

        private sealed class CancelAfterPublication : Collection<string>
        {
            internal CommandLineErrorHandler Handler;
            protected override void InsertItem(int index, string item)
            {
                base.InsertItem(index, item);
                Handler.KillProcess = true;
            }
        }

        [TestMethod]
        public void CancellationBetweenOutputsLeavesRemainingOutputIntact()
        {
            var published = new CancelAfterPublication { Handler = handler };
            OutputPublisher.Publish(new[] { first, second }, staging, true, handler, published);
            Assert.AreEqual(1, published.Count);
            Assert.AreEqual("old second", File.ReadAllText(second));
            Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp").Length);
        }

        [TestMethod]
        public void SourceCopyFailureLeavesExistingOutputAndCleansTemporaryFile()
        {
            using (File.Open(Path.Combine(staging, "first.w3x"), FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.ThrowsException<IOException>(() => OutputPublisher.Publish(new[] { first }, staging, true, handler, new List<string>()));
            Assert.AreEqual("old first", File.ReadAllText(first));
            Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp").Length);
        }
    }
}
