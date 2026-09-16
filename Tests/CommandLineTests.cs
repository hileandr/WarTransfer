using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using WarTransfer;
using WarTransferCL;

namespace Tests
{
    [TestClass]
    public class CommandLineTests
    {
        private string root, source, targets, output, editor;
        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "WarTransferCL tests & " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            source = Path.Combine(root, "source 2.0.w3x");
            targets = Path.Combine(root, "targets");
            output = Path.Combine(root, "output");
            editor = Path.Combine(root, "MPQEditor.exe");
            File.WriteAllText(source, "fixture placeholder");
            File.WriteAllText(editor, "fixture placeholder");
            Directory.CreateDirectory(targets);
            File.WriteAllText(Path.Combine(targets, "terrain 1.0.w3m"), "fixture placeholder");
        }
        [TestCleanup]
        public void Cleanup() { Directory.Delete(root, true); }
        private string[] Args(params string[] extra) => new[] { "transfer", "--source", source, "--targets", targets,
            "--output", output, "--mpq-editor", editor, "--json" }.Concat(extra).ToArray();
        private Dictionary<string, object> Run(int expectedCode, params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Assert.AreEqual(expectedCode, Program.Run(args, stdout, stderr), stderr.ToString());
            var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(stdout.ToString());
            Assert.AreEqual(expectedCode, json["exit_code"]);
            Assert.AreEqual(expectedCode == 0, json["success"]);
            return json;
        }
        [TestMethod]
        public void DryRunPlansVersionedOutputsWithoutWriting()
        {
            var result = Run(0, Args("--dry-run", "--version-regex", @"\d+\.\d+"));
            StringAssert.Contains(new JavaScriptSerializer().Serialize(result["planned_outputs"]), "terrain 2.0.w3x");
            Assert.IsFalse(Directory.Exists(output));
            Assert.AreEqual("fixture placeholder", File.ReadAllText(source));
        }
        [TestMethod]
        public void ParserRejectsTyposDuplicatesMissingValuesAndMissingRequiredOptions()
        {
            Run(2, Args("--dryrun"));
            Run(2, Args("--source", source));
            Run(2, Args("--flags"));
            Run(2, new[] { "--json" });
            Run(2, Args("--flags", "notAFlag"));
            Run(2, Args("--version-regex", "["));
        }
        [TestMethod]
        public void MissingToolAndInvalidArchiveHaveDistinctFailureCodes()
        {
            File.Delete(editor);
            Run(3, Args("--dry-run"));
            File.WriteAllText(editor, "not an executable");
            Run(1, Args());
            Assert.IsFalse(Directory.Exists(output));
        }
        [TestMethod]
        public void OutputsRequireOptInToOverwrite()
        {
            Directory.CreateDirectory(output);
            string existing = Path.Combine(output, "terrain 1.0.w3x");
            File.WriteAllText(existing, "keep this");
            Run(2, Args("--dry-run"));
            Run(0, Args("--dry-run", "--overwrite"));
            Assert.AreEqual("keep this", File.ReadAllText(existing));
        }
        [TestMethod]
        public void RejectsInputOverlapAndDuplicateOutputNames()
        {
            output = Path.Combine(targets, "output");
            Run(2, Args("--dry-run"));
            output = Path.Combine(root, "output");
            File.WriteAllText(Path.Combine(targets, "terrain 1.0.w3x"), "collision");
            Run(2, Args("--dry-run"));
        }
        [TestMethod]
        public void SourceInTargetDirectoryIsExcluded()
        {
            source = Path.Combine(targets, "source 2.0.w3x");
            File.WriteAllText(source, "source");
            var options = CliOptions.Parse(Args("--dry-run"));
            options.Validate();
            Assert.AreEqual(1, options.Targets.Count);
        }
        [TestMethod]
        public void AllTransferOptionsReachSharedArguments()
        {
            var options = CliOptions.Parse(Args("--transfer-cameras", "--transfer-regions", "--transfer-unit-tables",
                "--transfer-item-tables", "--flags=UseCustomForces,MeleeMap"));
            Assert.IsTrue(options.Transfer.TransferCameras && options.Transfer.TransferRegions
                && options.Transfer.TransferUnitTables && options.Transfer.TransferItemTables);
            Assert.AreEqual(WarTransfer.FileTypes.W3iFlags.UseCustomForces | WarTransfer.FileTypes.W3iFlags.MeleeMap, options.Transfer.W3iFlagsToTransfer);
            Assert.AreEqual(0, (int)CliOptions.Parse(Args("--flags=none")).Transfer.W3iFlagsToTransfer);
        }
        [TestMethod]
        public void EveryWorkflowErrorStopsCliWithoutTerminatingHost()
        {
            var log = new StringWriter();
            var handler = new CommandLineErrorHandler(log, false);
            handler.ReportError("copy failed", false);
            handler.KillProcess = false;
            Assert.IsTrue(handler.KillProcess);
            Assert.AreEqual(1, handler.Errors.Count);
            StringAssert.Contains(log.ToString(), "copy failed");
        }
        [TestMethod]
        public void HelpNeedsNoMapOrToolAndSupportsJson()
        {
            Run(0, new[] { "--help", "--json" });
            var writer = new StringWriter();
            Assert.AreEqual(0, Program.Run(new[] { "--help" }, writer, TextWriter.Null));
            StringAssert.Contains(writer.ToString(), "--source");
        }
        [TestMethod]
        public void ArchiveCommandsPreserveFirstFailure()
        {
            var handler = new CommandLineErrorHandler(TextWriter.Null, false);
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            Assert.AreEqual(7, IOUtility.RunProcessCommands(handler, shell, "-NoProfile -Command \"exit 7\"", "-NoProfile -Command \"exit 0\""));
        }

        [TestMethod]
        public void CancellationStopsRunningArchiveProcess()
        {
            var handler = new CommandLineErrorHandler(TextWriter.Null, false);
            string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var cancel = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(300);
                handler.KillProcess = true;
            });
            Assert.AreEqual(-1, IOUtility.RunProcessCommand(handler, shell, "-NoProfile -Command \"Start-Sleep -Seconds 30\""));
            cancel.Wait();
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(10));
        }

        [TestMethod]
        public void ExecutableWorksOutsideItsDirectoryAndProducesOnlyJsonOnStdout()
        {
            var start = new System.Diagnostics.ProcessStartInfo(typeof(Program).Assembly.Location, "--help --json")
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                string json = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, errors);
                var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                Assert.AreEqual(true, result["success"]);
                Assert.AreEqual("", errors);
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void TransfersRealArchivesAndPreservesInputs()
        {
            string tool = Environment.GetEnvironmentVariable("WARTRANSFER_TEST_MPQ_EDITOR");
            if (string.IsNullOrEmpty(tool) || !File.Exists(tool))
                Assert.Inconclusive("Set WARTRANSFER_TEST_MPQ_EDITOR to run the real MPQ integration test.");
            editor = Path.GetFullPath(tool);
            var handler = new CommandLineErrorHandler(new StringWriter(), true);
            string contents = Path.Combine(root, "contents");
            Directory.CreateDirectory(contents);
            File.Copy("../../TestFiles/test39.w3i", Path.Combine(contents, "war3map.w3i"));
            File.WriteAllText(Path.Combine(contents, "war3map.j"),
                "globals\nendglobals\nfunction config takes nothing returns nothing\nendfunction\n" +
                "function main takes nothing returns nothing\n    call InitBlizzard(  )\nendfunction\n");
            File.WriteAllText(Path.Combine(contents, "war3map.wts"), string.Join("\n", Enumerable.Range(1, 100)
                .Select(i => "STRING " + i + "\n{\nFixture " + i + "\n}\n")));
            File.WriteAllText(Path.Combine(contents, "custom-data.txt"), "source data");
            File.WriteAllText(Path.Combine(contents, "war3map.w3e"), "source terrain marker");
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(handler, contents, source, 10, editor));
            File.WriteAllText(Path.Combine(contents, "custom-data.txt"), "target data");
            File.WriteAllText(Path.Combine(contents, "war3map.w3e"), "target terrain marker");
            string target = Path.Combine(targets, "terrain 1.0.w3m");
            Assert.IsTrue(MPQUtility.MapDirectoryToMapFile(handler, contents, target, 10, editor));
            var sourceBytes = File.ReadAllBytes(source);
            var targetBytes = File.ReadAllBytes(target);
            Run(0, Args("--version-regex", @"\d+\.\d+"));
            string produced = Path.Combine(output, "terrain 2.0.w3x");
            Assert.IsTrue(File.Exists(produced));
            string extracted = Path.Combine(root, "extracted");
            Assert.IsTrue(MPQUtility.MapFileToMapDirectory(handler, produced, extracted, editor));
            Assert.AreEqual("source data", File.ReadAllText(Path.Combine(extracted, "custom-data.txt")));
            Assert.AreEqual("target terrain marker", File.ReadAllText(Path.Combine(extracted, "war3map.w3e")));
            var info = W3iUtility.ReadFile(handler, Path.Combine(extracted, "war3map.w3i"));
            Assert.AreEqual(39, info.FormatVersion);
            Assert.AreEqual(8, info.Players[1].HudSkin);
            Assert.IsFalse(handler.KillProcess, string.Join("\n", handler.Errors));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Run(0, Args("--version-regex", @"\d+\.\d+", "--overwrite"));

            // A broken input must not destroy the existing output, even with --overwrite.
            var producedBytes = File.ReadAllBytes(produced);
            File.WriteAllText(source, "broken archive");
            Run(1, Args("--version-regex", @"\d+\.\d+", "--overwrite"));
            CollectionAssert.AreEqual(producedBytes, File.ReadAllBytes(produced));
        }
    }
}
