using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace WarTransferCL
{
    class Program
    {
        // TO DO
        static void Main(string[] args)
        {
            CommandLineErrorHandler e = new CommandLineErrorHandler();

            //Settings.LoadSettings(e);
            //MapDataTransferArgs config = new MapDataTransferArgs();

            //config.SourceMap = Settings.LastSourceMap;
            ////config.SourceMap = "";
            //if (args.Length > 0)
            //{
            //    config.SourceMap = args[0];
            //}

            //config.SourceDirectory = Settings.LastSourceDirectory;
            ////config.SourceDirectory = "";
            //if (args.Length > 1)
            //{
            //    config.SourceDirectory = args[1];
            //}

            //if (Settings.EnableVersioning)
            //{
            //    config.VersionNumberRegex = Settings.VersionNumberRegex;
            //}

            //config.OutputDirectory = Settings.LastOutputDirectory;
            ////config.OutputDirectory = "";
            //if (args.Length > 2)
            //{
            //    config.OutputDirectory = args[2];
            //}

            //MapDataTransfer transferTool = new MapDataTransfer();
            //transferTool.ExecuteDataTransfer(e, config);

            //Settings.SaveSettings(e);

            //Console.WriteLine("Process complete. Press any key to exit.");

            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
