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
