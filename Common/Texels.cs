using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace ParadigmFileExtractor.Common
{
    public class Texels
    {
        public enum ColorFormat
        {
            RGBA = 0,
            YUV = 1,
            CI = 2,
            IA = 3,
            I = 4
        }

        public enum BitSize
        {
            _4 = 0,
            _8 = 1,
            _16 = 2,
            _32 = 3
        }

        public static BitSize NumBitsToBitSize(int numBits)
        {
            return (BitSize)((int)Math.Log(numBits, 2) - 2);
        }

        public static int BitSizeToNumBytes(BitSize bitSize)
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

        public static float BitSizeToNumBytesFloat(BitSize bitSize)
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

        public static Bitmap ConvertToBitmap(byte[] data, ColorFormat format, BitSize bitSize, int width, int height, int bytesPerLine, bool flipVertically, bool deinterleave, ushort[]? palette)
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
                    Color texelColor;
                    if (format == ColorFormat.CI)
                    {
                        if (bitSize != BitSize._4)
                        {
                            throw new Exception();
                        }

                        if(palette == null)
                        {
                            throw new Exception();
                        }

                        int index = Get4BitTexel(line, x);

                        // We know it's always in RGBA16
                        texelColor = RGBA16ToColor(palette[index]);
                    }
                    else
                    {
                        texelColor = GetTexel(line, x, format, bitSize);
                    }

                    bmp.SetPixel(x, flipVertically ? (height - 1) - l : l, texelColor);
                }
            }
            return bmp;
        }
        public static Color GetTexel(byte[] line, int texelX, ColorFormat format, BitSize bitSize)
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
