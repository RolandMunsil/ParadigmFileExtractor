using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                BitSize bitSize = numBitsToBitSize(bytes.ReadUInt16(2));
                ushort width = bytes.ReadUInt16(4);
                ushort texelsPerLine = bytes.ReadUInt16(6);
                ushort height = bytes.ReadUInt16(8);
                ushort tileWidth = bytes.ReadUInt16(10);
                ushort tileHeight = bytes.ReadUInt16(12);

                byte[] colorData = bytes.Subsection(14, bytes.Length - 14);




                Console.WriteLine(bytes.Subsection(0, 14).PrettyPrint());
                Console.WriteLine($"{outputFileName} {bitSize}-bit {colorFormat}\t <{width} ({texelsPerLine}), {height}> [{tileWidth} {tileHeight}]");
                int bytesPerTile = (int)(tileWidth * tileHeight * bitSizeToNumBytesFloat(bitSize));

                Bitmap outBitmap = new Bitmap(width, height);
                int p = 0;
                int curTopLeftX = 0;
                int curTopLeftY = 0;
                while (p < colorData.Length)
                {
                    if (curTopLeftY + tileHeight > height)
                    {
                        tileHeight = (ushort)(height - curTopLeftY);
                        bytesPerTile = (int)(tileWidth * tileHeight * bitSizeToNumBytesFloat(bitSize));
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
                    Bitmap tile = ConvertToBitmap(tileColorData, colorFormat, bitSize, tileWidth, tileHeight, (int)(tileWidth * bitSizeToNumBytesFloat(bitSize)), bitSize != BitSize._32);
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


        enum ColorFormat
        {
            RGBA = 0,
            YUV = 1,
            CI = 2,
            IA = 3,
            I = 4
        }

        enum BitSize
        {
            _4 = 0,
            _8 = 1,
            _16 = 2,
            _32 = 3
        }

        static BitSize numBitsToBitSize(int numBits)
        {
            return (BitSize)((int)Math.Log(numBits, 2) - 2);
        }

        static int bitSizeToNumBytes(BitSize bitSize)
        {
            switch (bitSize)
            {
                case BitSize._4: throw new Exception();
                case BitSize._8: return 1;
                case BitSize._16: return 2;
                case BitSize._32: return 4;
                default: throw new Exception();
            }
        }

        static float bitSizeToNumBytesFloat(BitSize bitSize)
        {
            switch (bitSize)
            {
                case BitSize._4: return 0.5f;
                case BitSize._8: return 1;
                case BitSize._16: return 2;
                case BitSize._32: return 4;
                default: throw new Exception();
            }
        }

        // TODO: why arent 32-bit textures deinterleaved?
        static Bitmap ConvertToBitmap(byte[] data, ColorFormat format, BitSize bitSize, int width, int height, int bytesPerLine, bool deinterleave)
        {
            if (bytesPerLine % 8 != 0)
            {
                throw new Exception();
            }

            byte[][] lines = new byte[height][];

            for (int l = 0; l < height; l++)
            {
                lines[l] = data.Subsection(l * bytesPerLine, bytesPerLine);
            }

            // Deinterleave odd lines
            if (deinterleave)
            {
                for (int l = 1; l < height; l += 2)
                {
                    byte[] line = lines[l];

                    for (int word = 0; word < bytesPerLine / 8; word++)
                    {
                        int wordPos = word * 8;
                        byte b1 = line[wordPos];
                        byte b2 = line[wordPos + 1];
                        byte b3 = line[wordPos + 2];
                        byte b4 = line[wordPos + 3];
                        byte b5 = line[wordPos + 4];
                        byte b6 = line[wordPos + 5];
                        byte b7 = line[wordPos + 6];
                        byte b8 = line[wordPos + 7];

                        line[wordPos] = b5;
                        line[wordPos + 1] = b6;
                        line[wordPos + 2] = b7;
                        line[wordPos + 3] = b8;
                        line[wordPos + 4] = b1;
                        line[wordPos + 5] = b2;
                        line[wordPos + 6] = b3;
                        line[wordPos + 7] = b4;
                    }

                    // might not be necessary but w/e
                    lines[l] = line;
                }
            }

            Bitmap bmp = new Bitmap(width, height);
            for (int l = 0; l < height; l++)
            {
                byte[] line = lines[l];
                for (int x = 0; x < width; x++)
                {
                    Color texelColor = GetTexel(line, x, format, bitSize);
                    // bmp.SetPixel(x, (height - l) - 1, texelColor);
                    bmp.SetPixel(x, l, texelColor);
                }
            }
            return bmp;
        }

        static Color GetTexel(byte[] line, int texelX, ColorFormat format, BitSize bitSize)
        {
            ulong texel;
            switch (bitSize)
            {
                case BitSize._4:
                    texel = Get4BitTexel(line, texelX);
                    break;
                case BitSize._8:
                    texel = line[texelX];
                    break;
                case BitSize._16:
                    texel = line.ReadUInt16(texelX * 2);
                    break;
                case BitSize._32:
                    texel = line.ReadUInt32(texelX * 4);
                    break;
                default:
                    throw new Exception();
            }

            if (format == ColorFormat.I)
            {
                byte i;
                if (bitSize == BitSize._4)
                    i = (byte)(texel | (texel << 4));
                else if (bitSize == BitSize._8)
                    i = (byte)texel;
                else
                    throw new Exception();
                return Color.FromArgb(i, i, i, i);
            }
            else if (format == ColorFormat.IA)
            {
                byte i;
                byte alpha;
                if (bitSize == BitSize._4)
                {
                    ulong i3 = texel >> 1;
                    i = (byte)(((i3 << 5) | (i3 << 2) | (i3 >> 1)) & 0xFF);
                    alpha = (byte)(0xFF * (texel & 1));
                }
                else if (bitSize == BitSize._8)
                {
                    ulong i4 = texel >> 4;
                    ulong a4 = texel & 0x0F;
                    i = (byte)(i4 | (i4 << 4));
                    alpha = (byte)(a4 | (a4 << 4));
                }
                else if (bitSize == BitSize._16)
                {
                    i = (byte)(texel >> 8);
                    alpha = (byte)(texel & 0xFF);
                }
                else
                {
                    throw new Exception();
                }
                return Color.FromArgb(alpha, i, i, i);
            }
            else if (format == ColorFormat.RGBA)
            {
                if (bitSize == BitSize._16)
                {
                    return RGBA16ToColor((ushort)texel);
                }
                else if (bitSize == BitSize._32)
                {
                    int argb = (int)((texel >> 8) | ((texel & 0xFF) << 24));
                    return Color.FromArgb(argb);
                }
                else
                {
                    throw new Exception();
                }
            }
            else
            {
                throw new Exception();
            }
        }

        private static byte Get4BitTexel(byte[] line, int texelX)
        {
            byte relevantByte = line[texelX / 2];
            if (texelX % 2 == 0)
            {
                return (byte)(relevantByte >> 4);
            }
            else
            {
                return (byte)(relevantByte & 0b00001111);
            }
        }

        private static Color RGBA16ToColor(ushort texel)
        {
            byte r5 = (byte)(texel >> 11);
            byte g5 = (byte)((texel >> 6) & 0b11111);
            byte b5 = (byte)((texel >> 1) & 0b11111);
            byte a1 = (byte)(texel & 0b1);

            byte r = (byte)((r5 << 3) | (r5 >> 2));
            byte g = (byte)((g5 << 3) | (g5 >> 2));
            byte b = (byte)((b5 << 3) | (b5 >> 2));
            byte a = (byte)(0xFF * a1);

            return Color.FromArgb(a, r, g, b);
        }
    }
}
