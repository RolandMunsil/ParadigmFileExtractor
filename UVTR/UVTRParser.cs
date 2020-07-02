using ParadigmFileExtractor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ParadigmFileExtractor.UVMD.UVMDFile;

namespace ParadigmFileExtractor.UVTR
{
    class UVTRParser
    {
        class UVCT
        {
            // placeholder
        }

        struct UVTR140Struct
        {
            public Matrix m1;
            public UVCT pUVCT;
            public ushort uvctFileIndex;
            public float unkfloat;
            public Matrix m2;
        }

        struct UVTR
        {
            public float[] h_6float;
            public byte h_b1;
            public byte h_b2;
            // 2 unknown bytes
            public float h_f1;
            public float h_f2;
            public float h_f3;
            public UVTR140Struct[] pB1xB2Mem;
            public ushort ushort1;
            public ushort uvrwFileIndex;
            public uint unk4Bytes;
        }

        public static void ParseUVTRs(byte[] romBytes)
        {
            //Directory.CreateDirectory(outputDir + "Converted");
            //string fullOutputPath = outputDir + "Converted/UVTR/";
            //Directory.CreateDirectory(fullOutputPath);

            // ok this is dumb i need to fix this
            Filesystem.Filesystem filesystem = new Filesystem.Filesystem(romBytes);

            foreach (Filesystem.Filesystem.File file in filesystem.AllFiles.Where(file => file.fileTypeFromFileHeader == "UVTR"))
            {
                PowerByteArray data = new PowerByteArray(file.Section("COMM"));

                UVTR uvtr = new UVTR();
                uvtr.h_6float = data.NextSubArray(24).AsFloats();
                uvtr.h_b1 = data.NextU8();
                uvtr.h_b2 = data.NextU8();
                uvtr.h_f1 = data.NextFloat();
                uvtr.h_f2 = data.NextFloat();
                uvtr.h_f3 = data.NextFloat();

                uvtr.pB1xB2Mem = new UVTR140Struct[uvtr.h_b1 * uvtr.h_b2];
                for(int i = 0; i < uvtr.pB1xB2Mem.Length; i++)
                {
                    UVTR140Struct oneFourty = new UVTR140Struct();
                    if(data.NextU8() == 0)
                    {
                        continue;
                    }
                    oneFourty.m1 = new Matrix(data.NextSubArray(64).AsFloats());
                    byte unkByte = data.NextU8();

                    // m2 = identity matrix
                    oneFourty.m2 = new Matrix(new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 });

                    // Copy translation from m1 to m2
                    oneFourty.m2[12] = oneFourty.m1[12];
                    oneFourty.m2[13] = oneFourty.m1[13];
                    oneFourty.m2[14] = oneFourty.m1[14];

                    /* FUN_801360cc(m2, m2) */

                    oneFourty.uvctFileIndex = data.NextU16();
                    var uvct = filesystem.GetFile("UVCT", oneFourty.uvctFileIndex);

                    /* oneFourty.pUVCT = processed filesystem.GetFile("UVCT", oneFourty.uvctFileIndex); */

                    /* if uvct does not exist, set oneFourty.unkFloat to 0 and move to next loop*/

                    /* at this point some stuff happens with the UVCT, idk what exactly */

                    uvtr.pB1xB2Mem[i] = oneFourty;
                }

                uvtr.ushort1 = data.NextU16();
                uvtr.uvrwFileIndex = data.NextU16();

                /*
                  if (DAT_8002d1a8 == 0) {
                    uvtrObj->ushort1 = 0;
                    uVar1 = uvtrObj->ushort1;
                  }
                  else {
                    uVar1 = uvtrObj->ushort1;
                  }
                */

                if (uvtr.ushort1 != 0)
                {
                    /* Load UVRW COMM section */

                    uvtr.unk4Bytes = data.NextU32();

                    data.NextU16();
                    data.NextU16();
                    data.NextU16();

                    /* a whole bunch of nonsense */

                    /* includes reading a few more shorts (maybe) */
                }

                
            }
        }
    }
}
