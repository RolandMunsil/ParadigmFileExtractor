using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.UVMD
{
    class UVMDDisplayer
    {
        public static void DisplayModels(byte[] romBytes)
        {
            Filesystem.Filesystem filesystem = new Filesystem.Filesystem(romBytes);

            foreach (Filesystem.Filesystem.File file in filesystem.AllFiles.Where(file => file.fileTypeFromFileHeader == "UVMD"))
            {
                UVMDFile uvmd = new UVMDFile(file.Sections.Single().Item2);
            }
        }
    }
}
