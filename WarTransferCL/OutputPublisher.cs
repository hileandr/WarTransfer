using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WarTransfer;

namespace WarTransferCL
{
    internal static class OutputPublisher
    {
        internal static void Publish(IEnumerable<string> plannedOutputs, string staging, bool overwrite,
            IWorkflowHandler handler, ICollection<string> published)
        {
            var outputs = plannedOutputs.ToArray();
            foreach (string path in outputs)
                if (!File.Exists(Path.Combine(staging, Path.GetFileName(path))))
                    throw new IOException("Transfer did not produce expected output: " + path);
            foreach (string path in outputs)
            {
                if (handler.KillProcess) break;
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string pending = Path.Combine(directory, ".wartransfer-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.Copy(Path.Combine(staging, Path.GetFileName(path)), pending);
                    if (overwrite && File.Exists(path)) File.Replace(pending, path, null);
                    else File.Move(pending, path);
                    published.Add(path);
                }
                finally { if (File.Exists(pending)) File.Delete(pending); }
            }
        }
    }
}
