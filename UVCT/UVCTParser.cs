using ParadigmFileExtractor.Common;
using ParadigmFileExtractor.Util;
using ParadigmFileExtractor.UVMD;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.UVCT
{
    class UVCTParser
    {
        public static void ParseUVCTs(byte[] romBytes)
        {
            // ok this is dumb i need to fix this
            Filesystem.Filesystem filesystem = new Filesystem.Filesystem(romBytes);

            foreach (Filesystem.Filesystem.File file in filesystem.AllFiles.Where(file => file.fileTypeFromFileHeader == "UVCT"))
            {
                PowerByteArray data = new PowerByteArray(file.Section("COMM"));

                new UVCTFile(data, filesystem);
            }
        }
    }
}
