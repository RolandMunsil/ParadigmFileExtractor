using ParadigmFileExtractor.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static ParadigmFileExtractor.Common.Texels;

namespace ParadigmFileExtractor.UVBT
{
    class UVBTConverter
    {
        // This should work for all Paradigm games it looks like
        public static void DumpBlits(byte[] romBytes, string outputDir)
        {
            Directory.CreateDirectory(outputDir + "Converted");
            string fullOutputPath = outputDir + "Converted/blit/";
            Directory.CreateDirectory(fullOutputPath);

            // ok this is dumb i need to fix this
            Filesystem.Filesystem filesystem = new Filesystem.Filesystem(romBytes);

            foreach (Filesystem.Filesystem.File file in filesystem.AllFiles.Where(file => file.fileTypeFromFileHeader == "UVBT"))
            {
                string outputFileName = $"[0x{file.formLocationInROM:x6}]";
                byte[] bytes = file.Sections.Single().Item2;

                ColorFormat colorFormat = (ColorFormat)bytes.ReadUInt16(0);
                BitSize bitSize = Texels.NumBitsToBitSize(bytes.ReadUInt16(2));
                ushort width = bytes.ReadUInt16(4);
                ushort texelsPerLine = bytes.ReadUInt16(6);
                ushort height = bytes.ReadUInt16(8);
                ushort tileWidth = bytes.ReadUInt16(10);
                ushort tileHeight = bytes.ReadUInt16(12);

                byte[] colorData = bytes.Subsection(14, bytes.Length - 14);




                Console.WriteLine(bytes.Subsection(0, 14).PrettyPrint());
                Console.WriteLine($"{outputFileName} {bitSize}-bit {colorFormat}\t <{width} ({texelsPerLine}), {height}> [{tileWidth} {tileHeight}]");
                int bytesPerTile = (int)(tileWidth * tileHeight * Texels.BitSizeToNumBytesFloat(bitSize));

                Bitmap outBitmap = new Bitmap(width, height);
                int p = 0;
                int curTopLeftX = 0;
                int curTopLeftY = 0;
                while (p < colorData.Length)
                {
                    if (curTopLeftY + tileHeight > height)
                    {
                        tileHeight = (ushort)(height - curTopLeftY);
                        bytesPerTile = (int)(tileWidth * tileHeight * Texels.BitSizeToNumBytesFloat(bitSize));
                    }

                    byte[] tileColorData;
                    if (p + bytesPerTile >= colorData.Length)
                    {
                        tileColorData = new byte[bytesPerTile];
                        Array.Copy(colorData, p, tileColorData, 0, colorData.Length - p);
                    }
                    else
                    {
                        tileColorData = colorData.Subsection(p, bytesPerTile);
                    }
                    // TODO: why arent 32-bit textures deinterleaved?
                    Bitmap tile = Texels.ConvertToBitmap(tileColorData, colorFormat, bitSize, tileWidth, tileHeight, (int)(tileWidth * Texels.BitSizeToNumBytesFloat(bitSize)), false, bitSize != BitSize._32, null);
                    CopyToMainBitmap(tile, curTopLeftX, curTopLeftY, outBitmap);


                    p += bytesPerTile;

                    curTopLeftX += tileWidth;

                    if (curTopLeftX >= width)
                    {
                        curTopLeftX = 0;
                        curTopLeftY += tileHeight;

                        if (curTopLeftY >= height)
                        {
                            break;
                        }
                    }
                }

                outBitmap.Save(fullOutputPath + outputFileName + ".png");
            }
        }

        static void CopyToMainBitmap(Bitmap tile, int startX, int startY, Bitmap outBitmap)
        {
            for (int srcX = 0; srcX < tile.Width; srcX++)
            {
                for (int srcY = 0; srcY < tile.Height; srcY++)
                {
                    int destX = startX + srcX;
                    int destY = startY + srcY;

                    if (destX < outBitmap.Width && destY < outBitmap.Height)
                    {
                        outBitmap.SetPixel(destX, destY, tile.GetPixel(srcX, srcY));
                    }
                }
            }
        }
    }
}
