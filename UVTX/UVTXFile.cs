using ParadigmFileExtractor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace ParadigmFileExtractor.UVTX
{
    class UVTXFile
    {
        /*
        struct StartStruct
        {
            public float f1;
            public float f2;
            public float f3;
            public float f4;
            public float f5;
            public float f6;
            public byte b1;
            public byte b2;
            public byte b3;
            //public byte b4; // this might not exist

        }


        uint unk4_1;
        StartStruct? pFirstStartStruct;
        StartStruct? pSecondStartStruct;
        uint unk4_2;
        uint unkP;
        uint unk4_3;
        ushort unk2_1;
        ushort unk2_2;
        ushort unk2_3;
        ushort unk2_4;
        byte unkB1;
        byte unkB2;
        byte unkB3;
        byte unkB4;
        byte unkB5;
        byte unkB6;
        byte unkB7;
        byte unkB8;


        public UVTXFile(PowerByteArray sectionBytes)
        {
            ushort texelDataLengthBytes = sectionBytes.NextU16();
            if(texelDataLengthBytes > 0x1000)
            {
                texelDataLengthBytes = 0x1000;
                Console.WriteLine("!! check !!");
            }
            ushort commandCount = sectionBytes.NextU16();
            float h_f1 = sectionBytes.NextFloat();
            float h_f2 = sectionBytes.NextFloat();

            StartStruct? firstStartStruct;
            if(h_f1 == 0 && h_f2 == 0)
            {
                firstStartStruct = null;
            }
            else
            {
                firstStartStruct = new StartStruct
                {
                    f1 = 1,
                    f2 = 1,
                    f3 = h_f1,
                    f4 = h_f2,
                    f5 = 0,
                    f6 = 0,
                    b1 = 0,
                    b2 = 0,
                    b3 = 1

                };
            }

            float h_f3 = sectionBytes.NextFloat();
            float h_f4 = sectionBytes.NextFloat();
            float h_f5 = sectionBytes.NextFloat();
            float h_f6 = sectionBytes.NextFloat();

            StartStruct? secondStartStruct;
            if (h_f3 == 0 && h_f4 == 0 && h_f5 == 0 && h_f6 == 0)
            {
                secondStartStruct = null;
            }
            else
            {
                secondStartStruct = new StartStruct
                {
                    f1 = 1,
                    f2 = 1,
                    f3 = h_f3,
                    f4 = h_f4,
                    f5 = h_f5,
                    f6 = h_f6,
                    b1 = 0,
                    b2 = 0,
                    b3 = 1

                };
            }

            sectionBytes.Position += texelDataLengthBytes;

            ulong[] commands = new ulong[commandCount * 8];

            unk2_3 = sectionBytes.NextU16();
            unk2_4 = sectionBytes.NextU16();
            unkB5 = sectionBytes.NextU8();
            unkB6 = sectionBytes.NextU8();
            unkB7 = sectionBytes.NextU8();
            unk4_3 = sectionBytes.NextU32();
            unk2_1 = sectionBytes.NextU16();
            ushort x = sectionBytes.NextU16();
            Console.WriteLine(x >> 8);
            unkB8 = (byte)(x & 0xFF);
            unkB2 = sectionBytes.NextU8();

            // It seems like these are genuinely ignored
            Console.WriteLine(sectionBytes.NextU8());
            Console.WriteLine(sectionBytes.NextU8());
            Console.WriteLine(sectionBytes.NextU8());
            Console.WriteLine(sectionBytes.NextU8());
            Console.WriteLine(sectionBytes.NextU32());
            unkB4 = sectionBytes.NextU8();
            unkB3 = sectionBytes.NextU8();
            unkB1 = 0xFF;
        }
        */



        static int HEADER_LENGTH = 0x1C;
        static int EXTRA_DATA_LENGTH = 0x1A;
        static int UNKNOWN_EXTRA_DATA_LENGTH = 0x15;

        public float[] unknownFloats;
        public byte[] texelData;
        public byte[][] displayListCommands;
        public ushort imageWidth;
        public ushort imageHeight;
        public byte[] unknownData;
        public byte mipMapCount; // 1 = just a single texture
        public ushort[][] palettes;

        public UVTXFile(byte[] fileBytes)
        {
            // Data sizes from header
            short dataSize = fileBytes.ReadInt16(0x00);
            short displayListCmdCount = fileBytes.ReadInt16(0x02);


            // Unknown floats
            unknownFloats = new float[6];

            unknownFloats[0] = fileBytes.ReadFloat32(0x04);
            unknownFloats[1] = fileBytes.ReadFloat32(0x08);

            unknownFloats[2] = fileBytes.ReadFloat32(0x0C);
            unknownFloats[3] = fileBytes.ReadFloat32(0x10);
            unknownFloats[4] = fileBytes.ReadFloat32(0x14);
            unknownFloats[5] = fileBytes.ReadFloat32(0x18);

            // Texels
            texelData = fileBytes.Subsection(HEADER_LENGTH, dataSize);

            // Display list
            displayListCommands = new byte[displayListCmdCount][];
            int displayListStart = HEADER_LENGTH + dataSize;
            for (int c = 0; c < displayListCmdCount; c++)
            {
                displayListCommands[c] = fileBytes.Subsection(displayListStart + (c * 8), 8);
            }


            // ===== Extra data =====
            int displayListSize = displayListCmdCount * 0x08;
            int startOfExtraData = displayListStart + displayListSize;
            byte[] extraData = fileBytes.Subsection(startOfExtraData, EXTRA_DATA_LENGTH);
            // Image width/height
            imageWidth = extraData.ReadUInt16(0x00);
            imageHeight = extraData.ReadUInt16(0x02);

            // Unknown data
            unknownData = extraData.Subsection(0x04, UNKNOWN_EXTRA_DATA_LENGTH);

            // Mip map
            mipMapCount = extraData.Last();
            // ===== End extra data =====

            // Palettes
            int palettesStart = startOfExtraData + EXTRA_DATA_LENGTH;
            byte[] palettesData = fileBytes.Subsection(palettesStart, (fileBytes.Length - 2) - palettesStart);

            int numPalettes = palettesData.Length / 32;
            palettes = new ushort[numPalettes][];
            for (int p = 0; p < numPalettes; p++)
            {
                palettes[p] = new ushort[16];
                for (int texel = 0; texel < 16; texel++)
                {
                    palettes[p][texel] = palettesData.ReadUInt16((p * 32) + (texel * 2));
                }
            }

            // Final two bytes
            // These are always 0, no idea why they're there. 
            byte[] finalTwoBytes = fileBytes.Subsection(fileBytes.Length - 2, 2);
            if (finalTwoBytes[0] != 0 || finalTwoBytes[1] != 0)
            {
                throw new Exception();
            }
        }
    }
}
