using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using ParadigmFileExtractor.Filesystem;

namespace ParadigmFileExtractor
{
    class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList(uint[] ProcessList, uint ProcessCount);

        static void Main(string[] args)
        {
            string romPath = String.Join(" ", args).Replace("\"", "");
            string outputDir = Path.GetFileNameWithoutExtension(romPath) + "/";

            FilesystemExtractor.ExtractToFolder(File.ReadAllBytes(romPath), outputDir);

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
