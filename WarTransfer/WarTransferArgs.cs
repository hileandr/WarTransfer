using WarTransfer.FileTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WarTransfer
{
    public class WarTransferArgs
    {
        public string SourceMap { get; set; }
        public string SourceDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public string VersionNumberRegex { get; set; }
        public bool TransferUnitTables { get; set; }
        public bool TransferItemTables { get; set; }
        public bool TransferCameras { get; set; }
        public bool TransferRegions { get; set; }
        public W3iFlags W3iFlagsToTransfer { get; set; }

        public WarTransferArgs()
        {
            SourceMap = "";
            SourceDirectory = "";
            OutputDirectory = "";
            VersionNumberRegex = "";
            TransferUnitTables = false;
            TransferItemTables = false;
            TransferCameras = false;
            TransferRegions = false;
            W3iFlagsToTransfer = W3iFlags.MeleeMap;
        }
    }
}
