using ParadigmFileExtractor.Common;
using ParadigmFileExtractor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.UVCT
{
    class UVCTFile
    {
        public struct HSH1Data
        {

        }

        public struct HSH3Struct
        {

        }

        public struct HSH4Data
        {

        }

        public HSH1Data[] pHSH1Data;
        public ushort hsh1DataCt;
        public HSH4Data[] pHSH4Data;
        public ushort hsh4DataCt;
        public HSH3Struct[] pHSH3Data;
        public ushort hsh3DataCt;
        public uint unk4_1;
        public uint unk4_2;
        public uint unk4_3;
        public uint unk4_4;
        public float f1;
        public float f2;
        public float f3;

        public UVCTFile(PowerByteArray data, Filesystem.Filesystem filesystem)
        {
            ushort h_sh1 = data.NextU16();
            ushort h_sh2 = data.NextU16();
            ushort h_sh3 = data.NextU16();
            ushort h_sh4 = data.NextU16();
            hsh1DataCt = h_sh1;
            hsh4DataCt = h_sh4;
            hsh3DataCt = h_sh3;

            float h_f1 = data.NextFloat();
            float h_f2 = data.NextFloat();
            float h_f3 = data.NextFloat();

            f1 = h_f1;
            f2 = h_f2;
            f3 = h_f3;


            /* allocate h_sh1 * 16 */

            PowerByteArray hsh2Data = data.NextSubArray(h_sh2 * 8);

            /* allocate h_sh3 * 76 */

            for (int i = 0; i < h_sh3; i++)
            {
                byte n = data.NextU8();

                PowerByteArray[] nData = data.NextSubArray(n * 64).InGroupsOf(64).ToArray();

                ushort uvmdIndex = data.NextU16();
                data.NextU32();
                data.NextU32();
                data.NextU32();
                data.NextU32();
                data.NextU16();
                data.NextU16();

                var uvmd = filesystem.GetFile("UVMD", uvmdIndex);
                Console.WriteLine(uvmd.formLocationInROM);
                //var uvmd2 = new UVMDFile(new PowerByteArray(uvmd.Section("COMM")), filesystem);

                // way more going on here just gonna ignore for now
            }

            /* allocate h_sh4 * 60 */

            var allTris = new List<ThreeD.Vertex>();

            for (int i = 0; i < h_sh4; i++)
            {
                uint d = data.NextU32();
                data.NextU32();
                data.NextU32();
                data.NextU32();
                ushort a = data.NextU16();
                data.NextU16();
                data.NextU16();
                data.NextU16();

                Filesystem.Filesystem.File uvtx;
                if ((d & 0xFFF) != 0xFFF)
                    uvtx = filesystem.GetFile("UVTX", (int)(d & 0xFFF));

                ushort numTriShorts = data.NextU16();
                ushort numCommands = data.NextU16();

                ThreeD.Vertex[] x = data.NextSubArray(a * 16).InGroupsOf(16).Select(d => new ThreeD.Vertex(d)).ToArray();

                var cmds = ThreeD.UnpackTriangleCommands(data, numTriShorts, numCommands);

                //if (cmds.Length > 10)
                {
                    // not sure if identity is correct
                    var tris = ThreeD.MaterialToVertexData(x, cmds, ThreeD.Matrix.Identity);
                    allTris.AddRange(tris);

                }

                data.NextU16();
                data.NextU16();
                data.NextU16();
                data.NextU16();
                data.NextU32();
                data.NextU32();
                data.NextU32();
                data.NextU32();
            }

            using (UVMD.UVMDDisplayWindow window = new UVMD.UVMDDisplayWindow(800, 600, allTris))
            {
                window.Run(60.0);
            }

            unk4_1 = data.NextU32();
            unk4_2 = data.NextU32();
            unk4_3 = data.NextU32();
            unk4_4 = data.NextU32();

            if (data.Length != (int)Math.Ceiling(data.Position / 8f) * 8)
            {
                throw new Exception();
            }
        }
    }
}
