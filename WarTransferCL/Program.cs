using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using WarTransfer;

namespace WarTransferCL
{
    public static class Program
    {
        public const string HelpText = @"WarTransferCL transfer --source MAP --targets DIRECTORY --output DIRECTORY [options]

Copies source map data into each target terrain using the shared WarTransfer engine.
Windows / .NET Framework 4.7.2. JASS maps only. No prompts or GUI settings.
Targets: immediate .w3x/.w3m archives; source excluded; outputs are .w3x.
Paths are relative to your current directory. Output must not overlap inputs.

  --mpq-editor PATH          MPQEditor.exe (default: beside WarTransferCL.exe)
  --dry-run                  Validate paths and print planned outputs; no writes
  --json                     One JSON result on stdout; logs on stderr
  --overwrite                Replace existing output maps (inputs never replaced)
  --version-regex PATTERN    Version from source filename; disabled by default
  --transfer-cameras         Use source cameras instead of target cameras
  --transfer-regions         Use source regions instead of target regions
  --transfer-unit-tables     Use source random unit tables
  --transfer-item-tables     Use source random item tables
  --flags LIST               Comma-separated W3iFlags names, none, or 0xHEX mask
                             Default: MeleeMap
  --verbose                  Include archive tool output and progress on stderr
  --help, -h                 Show this help
  --version                  Show CLI version

Exit codes: 0 success/dry-run/help; 1 transfer failure; 2 invalid arguments;
            3 missing MPQEditor; 130 cancelled (Ctrl+C).
Dry-run validates the plan, not map contents or in-game compatibility.
";

        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            return Run(args, Console.Out, Console.Error);
        }

        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            bool json = args.Contains("--json");
            var handler = new CommandLineErrorHandler(stderr, args.Contains("--verbose"));
            CliOptions options = null;
            var published = new List<string>();
            bool cancelled = false;
            bool validated = false;
            int code = 0;
            string staging = null;
            ConsoleCancelEventHandler cancel = (sender, e) => { e.Cancel = true; cancelled = true; handler.KillProcess = true; };
            Console.CancelKeyPress += cancel;
            try
            {
                if (args.Length == 0) throw new ArgumentException("A transfer requires --source, --targets and --output. Use --help for usage.");
                options = CliOptions.Parse(args);
                if (options.Help || options.Version)
                {
                    string text = options.Help ? HelpText : typeof(Program).Assembly.GetName().Version.ToString();
                    if (json) stdout.WriteLine(new JavaScriptSerializer().Serialize(new { schema_version = 1, success = true, exit_code = 0, text }));
                    else stdout.WriteLine(text);
                    return 0;
                }
                options.Validate();
                validated = true;
                if (!File.Exists(options.Transfer.MpqEditorPath))
                {
                    code = 3;
                    handler.ReportError("MPQEditor.exe not found: " + options.Transfer.MpqEditorPath + ". Supply --mpq-editor PATH.", true);
                }
                else if (!options.DryRun && !handler.KillProcess)
                {
                    staging = Path.Combine(Path.GetTempPath(), "WarTransferCL", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(staging);
                    string outputDirectory = options.Transfer.OutputDirectory;
                    options.Transfer.OutputDirectory = staging;
                    try { new WarTransfer.WarTransfer().ExecuteDataTransfer(handler, options.Transfer); }
                    finally { options.Transfer.OutputDirectory = outputDirectory; }
                    if (!handler.KillProcess)
                    {
                        OutputPublisher.Publish(options.Outputs, staging, options.Overwrite, handler, published);
                    }
                }
            }
            catch (ArgumentException ex) { code = validated ? 1 : 2; handler.ReportError(ex.Message, true); }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException ex) { code = 2; handler.ReportError(ex.Message, true); }
            catch (Exception ex) { code = 1; handler.ReportError(ex, true); }
            finally
            {
                if (staging != null)
                {
                    try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                    catch (Exception ex) { code = 1; handler.ReportError("Cannot clean staging directory " + staging + ": " + ex.Message, true); }
                }
                Console.CancelKeyPress -= cancel;
            }
            if (cancelled) code = 130;
            else if (handler.KillProcess && code == 0) code = 1;
            var result = new
            {
                schema_version = 1, success = code == 0, exit_code = code,
                dry_run = options != null && options.DryRun,
                source = options?.Transfer.SourceMap, targets = options?.Targets.ToArray() ?? new string[0],
                output_directory = options?.Transfer.OutputDirectory,
                planned_outputs = options?.Outputs.ToArray() ?? new string[0], outputs = published.ToArray(),
                errors = handler.Errors.ToArray()
            };
            if (json) stdout.WriteLine(new JavaScriptSerializer().Serialize(result));
            else
            {
                if (code == 0)
                {
                    stdout.WriteLine(options.DryRun ? "Dry run succeeded. Planned outputs:" : "Transfer succeeded. Outputs:");
                    foreach (string path in options.DryRun ? options.Outputs : published) stdout.WriteLine(path);
                }
                else stderr.WriteLine("WarTransferCL failed (exit " + code + ").");
            }
            return code;
        }
    }
}
