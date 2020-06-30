using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using ParadigmFileExtractor.Filesystem;
using ParadigmFileExtractor.UVTX;
using ParadigmFileExtractor.UVBT;
using ParadigmFileExtractor.UVMD;

namespace ParadigmFileExtractor
{
    class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList(uint[] ProcessList, uint ProcessCount);

        static void Main(string[] args)
        {
            string action = args[0];
            string romPath = String.Join(" ", args.Skip(1)).Replace("\"", "");
            string outputDir = Path.GetFileNameWithoutExtension(romPath) + "/";

            switch(action)
            {
                case "dump-filesystem":
                    FilesystemExtractor.ExtractToFolder(File.ReadAllBytes(romPath), outputDir);
                    break;
                case "dump-converted-images":
                    //UVTXConverter.DumpTextures(File.ReadAllBytes(romPath), outputDir);
                    UVBTConverter.DumpBlits(File.ReadAllBytes(romPath), outputDir);
                    break;
                case "show-models":
                    UVMDDisplayer.DisplayModels(File.ReadAllBytes(romPath));
                    break;

            }

            // If we're the only process attached to the console, (e.g. if the user drags+drops a file onto the program)
            // then the console will close when the program exits. I'd rather not have this happen since most games will
            // extract far too quickly for the user to read any of the output.
            uint processCount = GetConsoleProcessList(new uint[64], 64);
            if(processCount == 1)
            {
                Console.ReadKey();
            }
        }
    }
}
