using ParadigmFileExtractor.Common;
using ParadigmFileExtractor.Util;
using ParadigmFileExtractor.UVTX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
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

        // THIS IS A TEMPORARY HACK
        List<(ThreeD.Vertex[], ThreeD.RSPCommand[], UVTXFile?)> hack_triData = new List<(ThreeD.Vertex[], ThreeD.RSPCommand[], UVTXFile?)>();

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

            //using (ThreeDDisplayWindow window = new ThreeDDisplayWindow())
            //{
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

                    UVTXFile uvtx = null;
                    //UVTXConverter.RDPState rdpState;
                    if ((d & 0xFFF) != 0xFFF)
                    {
                        uvtx = new UVTXFile(filesystem.GetFile("UVTX", (int)(d & 0xFFF)).Section("COMM"));
                        //rdpState = UVTXConverter.ExecuteCommands(new UVTXFile(uvtx.Section("COMM")), out _);
                    }

                    ushort numTriShorts = data.NextU16();
                    ushort numCommands = data.NextU16();

                    ThreeD.Vertex[] x = data.NextSubArray(a * 16).InGroupsOf(16).Select(d => new ThreeD.Vertex(d)).ToArray();

                    ThreeD.RSPCommand[] cmds = ThreeD.UnpackTriangleCommands(data, numTriShorts, numCommands);

                    //if (h_sh4 > 16)
                    //{
                    //    // not sure if identity is correct
                    //    var tris = ThreeD.MaterialToVertexData(x, cmds, ThreeD.Matrix.Identity);
                    //    if (uvtx == null)
                    //        window.AddVertices(tris);
                    //    else
                    //        window.AddTexturedVertices(tris, new UVTXFile(uvtx.Section("COMM")));

                    //}

                    hack_triData.Add((x, cmds, uvtx));

                    data.NextU16();
                    data.NextU16();
                    data.NextU16();
                    data.NextU16();
                    data.NextU32();
                    data.NextU32();
                    data.NextU32();
                    data.NextU32();
                }

            //    if (h_sh4 > 16)
            //        window.Run();
            //}

            unk4_1 = data.NextU32();
            unk4_2 = data.NextU32();
            unk4_3 = data.NextU32();
            unk4_4 = data.NextU32();

            if (data.Length != (int)Math.Ceiling(data.Position / 8f) * 8)
            {
                throw new Exception();
            }
        }

        public void AddToWindow(ThreeDDisplayWindow window, ThreeD.Matrix matrix)
        {
            foreach((var vertices, var cmds, var uvtx) in hack_triData)
            {
                var tris = ThreeD.MaterialToVertexData(vertices, cmds, matrix);
                if (uvtx == null)
                    window.AddVertices(tris);
                else
                    window.AddTexturedVertices(tris, uvtx);
            }
        }
    }
}
