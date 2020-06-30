using ParadigmFileExtractor.Filesystem;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace ParadigmFileExtractor.UVTX
{
    class UVTXConverter
    {
        public static void DumpTextures(byte[] romBytes, string outputDir)
        {
            Directory.CreateDirectory(outputDir + "Converted");
            string fullOutputPath = outputDir + "Converted/texture/";
            Directory.CreateDirectory(fullOutputPath);

            // ok this is dumb i need to fix this
            Filesystem.Filesystem filesystem = new Filesystem.Filesystem(romBytes);

            foreach (Filesystem.Filesystem.File file in filesystem.AllFiles.Where(file => file.fileTypeFromFileHeader == "UVTX"))
            {
                // TODO
                string outputFileName = $"[0x{file.formLocationInROM:x6}]";
                UVTXFile uvtx = new UVTXFile(file.Sections.Single().Item2);

                StringBuilder fileStringBuilder = new StringBuilder();

                fileStringBuilder.AppendLine("========================================");
                fileStringBuilder.AppendLine($"{outputFileName} ({uvtx.texelData.Length} data | {uvtx.displayListCommands.Length} cmds | {uvtx.palettes.Length} palettes)");
                fileStringBuilder.AppendLine($"(footer) size <{uvtx.imageWidth}, {uvtx.imageHeight}> | {uvtx.mipMapCount} mm |");

                string? cmdString = ConvertToPNG(uvtx, fullOutputPath + outputFileName);

                if (cmdString == null)
                {
                    continue;
                }
                fileStringBuilder.Append(cmdString);

                fileStringBuilder.AppendLine("Unknown: \n" + String.Join(" ", uvtx.unknownData.Select(b => b.ToString("X2"))));
                fileStringBuilder.AppendLine("Floats: " + String.Join(", ", uvtx.unknownFloats.Select(f => f.ToString("F8"))));
                Console.Write(fileStringBuilder.ToString());
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

        class TileDescriptor
        {
            public ColorFormat format;
            public BitSize bitSize;
            // num 64-bit words per row of tile
            // note that data is padded so that this is an integer
            public ushort wordsPerLine;
            // tmem address in 64-bit words. i.e. 0x3 = byte 12;
            public ushort tmemAddressInWords;
            // palette number (0-15) for CI textures
            public byte palette;
            public bool mirrorEnableS;
            public bool mirrorEnableT;
            public bool clampEnableS;
            public bool clampEnableT;
            public byte maskS;
            public byte maskT;
            public byte shiftS;
            public byte shiftT;
            // 10.2 fixed point, high and low positions of texture in texture image space
            // We represent as float
            public float sLo, tLo, sHi, tHi;
        }

        class ColorCombinerSettings
        {
            public byte primR;
            public byte primG;
            public byte primB;
            public byte primA;

            public byte envR;
            public byte envG;
            public byte envB;
            public byte envA;

            // fixed point 0.8 numbers
            public float minLODLevel;
            public float LODfrac;
        }

        enum Fast3DEX2Opcode
        {
            G_TEXTURE = 0xD7,
            G_ENDDL = 0xDF,
            G_SetOtherMode_H = 0xE3,
            G_RDPLOADSYNC = 0xE6,
            G_RDPTILESYNC = 0xE8,
            G_SETTILESIZE = 0xF2,
            G_LOADBLOCK = 0xF3,
            G_SETTILE = 0xF5,
            G_SETPRIMCOLOR = 0xFA,
            G_SETENVCOLOR = 0xFB,
            G_SETCOMBINE = 0xFC,
            G_SETTIMG = 0xFD
        }

        static Dictionary<int, string> otherModeHShiftToStr = new Dictionary<int, string>()
        {
            { 0, "G_MDSFT_BLENDMASK" },
            { 4, "G_MDSFT_ALPHADITHER" },
            { 6, "G_MDSFT_RGBDITHER" },
            { 8, "G_MDSFT_COMBKEY" },
            { 9, "G_MDSFT_TEXTCONV" },
            { 12, "G_MDSFT_TEXTFILT" },
            { 14, "G_MDSFT_TEXTLUT" },
            { 16, "G_MDSFT_TEXTLOD" }, // Note that this is used to make mipmaps work!
            { 17, "G_MDSFT_TEXTDETAIL" },
            { 19, "G_MDSFT_TEXTPERSP" },
            { 20, "G_MDSFT_CYCLETYPE" },
            { 22, "G_MDSFT_COLORDITHER" },
            { 23, "G_MDSFT_PIPELINE" }
        };

        static string? ConvertToPNG(UVTXFile uvtx, string filePathWithoutExtension)
        {
            byte[] tmem = new byte[4096];
            TileDescriptor[] tileDescriptors = new TileDescriptor[8];
            ColorCombinerSettings colorCombinerSettings = new ColorCombinerSettings();
            bool texturingEnabled = false;
            byte tileToUseWhenTexturing = 0xFF;
            byte maxMipMapLevels = 0xFF;
            float textureScaleS = -1;
            float textureScaleT = -1;

            uint? nextDRAMAddrForLoad = null;
            BitSize? bitSizeOfNextDataForLoad = null;

            StringBuilder cmdsString = new StringBuilder();

            HashSet<int> tilesWhereSetTileSizeWasCalled = new HashSet<int>();

            foreach (byte[] commandBytes in uvtx.displayListCommands)
            {
                byte[] bytes = commandBytes;

                string operationDesc;
                switch ((Fast3DEX2Opcode)bytes[0])
                {
                    case Fast3DEX2Opcode.G_TEXTURE:
                        {
                            ulong word = bytes.ReadUInt64(0);
                            byte mipMap = (byte)(word.Bits(43, 3) + 1);
                            byte tileDescIndex = (byte)word.Bits(40, 3);

                            bool on = word.Bit(33);
                            float scaleFactorS = bytes.ReadUInt16(4) / (float)0x10000;
                            float scaleFactorT = bytes.ReadUInt16(6) / (float)0x10000;

                            tileToUseWhenTexturing = tileDescIndex;
                            maxMipMapLevels = mipMap;
                            texturingEnabled = on;
                            textureScaleS = scaleFactorS;
                            textureScaleT = scaleFactorT;

                            operationDesc = "G_TEXTURE        (Set RSP texture state)";
                            operationDesc += $": tile {tileDescIndex} scale=<{scaleFactorS}, {scaleFactorT}>; mm={mipMap} on={on}";
                            break;
                        }
                    case Fast3DEX2Opcode.G_SetOtherMode_H:
                        {
                            // TODO: actually implement this?
                            operationDesc = "G_SetOtherMode_H (Set Other Modes Hi)";

                            int length = bytes[3] + 1;
                            int shift = 32 - length - bytes[2];

                            string str = otherModeHShiftToStr[shift];
                            byte val = (byte)bytes.ReadUInt64(0).Bits(shift, length);

                            // TODO?: https://wiki.cloudmodding.com/oot/F3DZEX#RDP_Other_Modes.2C_Higher_Half
                            string valStr = Convert.ToString(val, 2).PadLeft(length, '0');

                            operationDesc += $": {str} = {valStr}";
                            break;
                        }
                    case Fast3DEX2Opcode.G_SETTILESIZE:
                        {
                            ulong word = bytes.ReadUInt64(0);


                            ushort sLoRaw = (ushort)word.Bits(44, 12);
                            ushort tLoRaw = (ushort)word.Bits(32, 12);
                            byte tileDescIndex = (byte)word.Bits(24, 3);
                            ushort sHiRaw = (ushort)word.Bits(12, 12);
                            ushort tHiRaw = (ushort)word.Bits(0, 12);

                            TileDescriptor t = tileDescriptors[tileDescIndex];
                            t.sLo = sLoRaw / 4.0f;
                            t.tLo = tLoRaw / 4.0f;
                            t.sHi = sHiRaw / 4.0f;
                            t.tHi = tHiRaw / 4.0f;

                            float visWidth = (t.sHi - t.sLo) + 1;
                            float visHeight = (t.tHi - t.tLo) + 1;

                            operationDesc = "G_SETTILESIZE    (Set texture coords and size)";
                            operationDesc += $": tile {tileDescIndex} lo=({t.sLo}, {t.tLo}) hi=({t.sHi}, {t.tHi}) [[{visWidth}, {visHeight}]]";

                            tilesWhereSetTileSizeWasCalled.Add(tileDescIndex);
                            break;
                        }
                    case Fast3DEX2Opcode.G_LOADBLOCK:
                        {
                            ulong word = bytes.ReadUInt64(0);

                            ushort sLo = (ushort)word.Bits(44, 12);
                            ushort tLo = (ushort)word.Bits(32, 12);
                            byte tileDescIndex = (byte)word.Bits(24, 3);
                            ushort sHi = (ushort)word.Bits(12, 12);
                            ushort dxt = (ushort)word.Bits(0, 12);

                            if (dxt != 0)
                            {
                                throw new Exception();
                            }

                            if (sLo != 0 || tLo != 0)
                            {
                                throw new Exception();
                            }

                            if (nextDRAMAddrForLoad == null || bitSizeOfNextDataForLoad == null)
                            {
                                throw new Exception();
                            }

                            tileDescriptors[tileDescIndex].sLo = sLo;
                            tileDescriptors[tileDescIndex].tLo = tLo;
                            tileDescriptors[tileDescIndex].sHi = sHi;
                            tileDescriptors[tileDescIndex].tHi = dxt; // Not 100% sure this is the correct behavior


                            int dataStart = (int)nextDRAMAddrForLoad;
                            int dataLengthBytes = (sHi + 1) * bitSizeToNumBytes((BitSize)bitSizeOfNextDataForLoad);
                            int destPtr = tileDescriptors[tileDescIndex].tmemAddressInWords * 8;

                            // I'm assuming this is the correct behavior because if I don't do this a lot of textures have a notch at the top right
                            // (Also it would make sense given that interleaving and addresses are all done on 64-bit words
                            dataLengthBytes = (int)Math.Ceiling(dataLengthBytes / 8f) * 8;

                            // Note: technically this inaccurate, we shouldn't clamp. But the instructions read beyond the file and IDK why,
                            // it doesn't seem to serve any purpose so I assume it's a bug (or I don't understand something about how the RSP works)
                            Array.Copy(uvtx.texelData, dataStart, tmem, destPtr, Math.Min(uvtx.texelData.Length - dataStart, dataLengthBytes));

                            operationDesc = "G_LOADBLOCK      (Load data into TMEM (uses params set in SETTIMG))";
                            operationDesc += $": tile {tileDescIndex} sLo={sLo} tLo={tLo} sHi={sHi} dxt={dxt}";
                            break;
                        }
                    case Fast3DEX2Opcode.G_SETTILE:
                        {
                            ulong word = bytes.ReadUInt64(0);
                            TileDescriptor t = new TileDescriptor
                            {
                                format = (ColorFormat)word.Bits(53, 3),
                                bitSize = (BitSize)word.Bits(51, 2),
                                wordsPerLine = (ushort)word.Bits(41, 9),
                                tmemAddressInWords = (ushort)word.Bits(32, 9),
                                palette = (byte)word.Bits(20, 4),
                                clampEnableT = word.Bit(19),
                                mirrorEnableT = word.Bit(18),
                                maskT = (byte)word.Bits(14, 4),
                                shiftT = (byte)word.Bits(10, 4),
                                clampEnableS = word.Bit(9),
                                mirrorEnableS = word.Bit(8),
                                maskS = (byte)word.Bits(4, 4),
                                shiftS = (byte)word.Bits(0, 4),
                            };
                            byte tileDescIndex = (byte)word.Bits(24, 3);
                            tileDescriptors[tileDescIndex] = t;

                            operationDesc = "G_SETTILE        (Set texture properties)";
                            operationDesc += $": tile {tileDescIndex} fmt={t.bitSize}-bit {t.format} wordsPerLine={t.wordsPerLine} addrWords={t.tmemAddressInWords} palette={t.palette}"
                                + $" s(clmp={t.clampEnableS} mirr={t.mirrorEnableS} mask={t.maskS} shift={t.shiftS}) t(clmp={t.clampEnableT} mirr={t.mirrorEnableT} mask={t.maskT} shift={t.shiftT})";

                            break;
                        }
                    case Fast3DEX2Opcode.G_SETPRIMCOLOR:
                        {
                            float minLODLevel = bytes[2] / 0x100f;
                            float LODfrac = bytes[3] / 0x100f;
                            byte r = bytes[4];
                            byte g = bytes[5];
                            byte b = bytes[6];
                            byte a = bytes[7];

                            colorCombinerSettings.primR = r;
                            colorCombinerSettings.primG = g;
                            colorCombinerSettings.primB = b;
                            colorCombinerSettings.primA = a;
                            colorCombinerSettings.minLODLevel = minLODLevel;
                            colorCombinerSettings.LODfrac = LODfrac;

                            operationDesc = "G_SETPRIMCOLOR   (Set color combiner primitive color + LOD)";
                            operationDesc += $": rgba({r}, {g}, {b}, {a}) minLOD={minLODLevel} LODfrac={LODfrac}";
                            break;
                        }
                    case Fast3DEX2Opcode.G_SETENVCOLOR:
                        {
                            byte r = bytes[4];
                            byte g = bytes[5];
                            byte b = bytes[6];
                            byte a = bytes[7];

                            colorCombinerSettings.envR = r;
                            colorCombinerSettings.envG = g;
                            colorCombinerSettings.envB = b;
                            colorCombinerSettings.envA = a;

                            operationDesc = "G_SETENVCOLOR    (Set color combiner environment color)";
                            operationDesc += $": rgba({r}, {g}, {b}, {a})";
                            break;
                        }
                    case Fast3DEX2Opcode.G_SETCOMBINE:
                        operationDesc = "G_SETCOMBINE     (Set color combiner algorithm)";
                        break;
                    case Fast3DEX2Opcode.G_SETTIMG:
                        {
                            ulong word = bytes.ReadUInt64(0);

                            ColorFormat format = (ColorFormat)word.Bits(53, 3);
                            BitSize bitSize = (BitSize)word.Bits(51, 2);
                            uint dramAddress = (uint)word.Bits(0, 25);

                            nextDRAMAddrForLoad = dramAddress;
                            bitSizeOfNextDataForLoad = bitSize;

                            operationDesc = "G_SETTIMG        (Set pointer to data to load + size of data)";
                            operationDesc += $": DRAM 0x{dramAddress:X8}; fmt={bitSize}-bit {format}";

                            break;
                        }
                    case Fast3DEX2Opcode.G_RDPLOADSYNC:
                        operationDesc = "G_RDPLOADSYNC    (Wait for texture load)";
                        break;
                    case Fast3DEX2Opcode.G_RDPTILESYNC:
                        operationDesc = "G_RDPTILESYNC    (Wait for rendering + update tile descriptor attributes)";
                        break;
                    case Fast3DEX2Opcode.G_ENDDL:
                        operationDesc = "G_ENDDL          (End display list)";
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                string bytesStr = String.Join(" ", bytes.Select(b => b.ToString("X2")));
                cmdsString.AppendLine(bytesStr + " | " + operationDesc);
            }

            // Some don't have a setTileSize, use the size from the uvtx footer
            if (!tilesWhereSetTileSizeWasCalled.Contains(tileToUseWhenTexturing))
            {
                tileDescriptors[tileToUseWhenTexturing].sLo = 0.5f;
                tileDescriptors[tileToUseWhenTexturing].tLo = 0.5f;
                tileDescriptors[tileToUseWhenTexturing].sHi = uvtx.imageWidth - 0.5f;
                tileDescriptors[tileToUseWhenTexturing].tHi = uvtx.imageHeight - 0.5f;
            }

            // TODO: support Color Combiner? https://wiki.cloudmodding.com/oot/F3DZEX/Opcode_Details#0xFC_.E2.80.94_G_SETCOMBINE https://wiki.cloudmodding.com/oot/F3DZEX#Color_Combiner_Settings
            // TODO: save mipmaps?

            // NOTE: some files without mipmaps define more than just the normal two tiles (i.e. 7 for load, 1 for actual tile)
            // It looks like all of these just define Tile 2, and none of their Tile 2s seem to contain meaningful image data
            // in fact I think it's just referencing the same data as Tile 1 but starting from a different location and maybe
            // in a different format.
            // It seems like all of them set G_MDSFT_CYCLETYPE to 01 (which is otherwise mostly only done for textures with mipmaps)
            // so I *assume* this is some trick needed for a complex effect (since 01 is "2 cycles per pixel" mode) 

            // Save tile, ignore mipmaps
            string filePath = filePathWithoutExtension + "-" + tileToUseWhenTexturing + ".png";
            SaveTile(tileDescriptors[tileToUseWhenTexturing], tmem, uvtx.palettes, filePath);
            return cmdsString.ToString();
        }

        static void SaveTile(TileDescriptor tileDesc, byte[] tmem, ushort[][] palettes, string filePath)
        {
            if (Math.Floor(tileDesc.sHi - tileDesc.sLo) != tileDesc.sHi - tileDesc.sLo)
            {
                throw new Exception();
            }
            if (Math.Floor(tileDesc.tHi - tileDesc.tLo) != tileDesc.tHi - tileDesc.tLo)
            {
                throw new Exception();
            }

            // Break into lines
            int startPtrBytes = tileDesc.tmemAddressInWords * 8;
            int width = (int)(tileDesc.sHi - tileDesc.sLo + 1);
            int height = (int)(tileDesc.tHi - tileDesc.tLo + 1);
            int bytesPerLine = tileDesc.wordsPerLine * 8;

            byte[][] lines = new byte[height][];

            for (int l = 0; l < height; l++)
            {
                lines[l] = tmem.Subsection(startPtrBytes + (l * bytesPerLine), bytesPerLine);
            }

            // Deinterleave odd lines
            for (int l = 1; l < height; l += 2)
            {
                byte[] line = lines[l];

                for (int word = 0; word < tileDesc.wordsPerLine; word++)
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

            Bitmap bmp = new Bitmap(width, height);
            for (int l = 0; l < height; l++)
            {
                byte[] line = lines[l];
                for (int x = 0; x < width; x++)
                {
                    Color texelColor;
                    if (tileDesc.format == ColorFormat.CI)
                    {
                        if (tileDesc.bitSize != BitSize._4)
                        {
                            throw new Exception();
                        }

                        // The texture file doesn't store the 0 palette but never uses it
                        ushort[] palette = palettes[tileDesc.palette - 1];

                        int index = Get4BitTexel(line, x);

                        // We know it's always in RGBA16
                        texelColor = RGBA16ToColor(palette[index]);
                    }
                    else
                    {
                        texelColor = GetTexel(line, x, tileDesc);
                    }
                    bmp.SetPixel(x, (height - l) - 1, texelColor);
                }
            }
            bmp.Save(filePath);
        }

        static Color GetTexel(byte[] line, int texelX, TileDescriptor tileDescriptor)
        {
            ulong texel;
            switch (tileDescriptor.bitSize)
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

            if (tileDescriptor.format == ColorFormat.I)
            {
                byte i;
                if (tileDescriptor.bitSize == BitSize._4)
                    i = (byte)(texel | (texel << 4));
                else if (tileDescriptor.bitSize == BitSize._8)
                    i = (byte)texel;
                else
                    throw new Exception();
                return Color.FromArgb(i, i, i, i);
            }
            else if (tileDescriptor.format == ColorFormat.IA)
            {
                byte i;
                byte alpha;
                if (tileDescriptor.bitSize == BitSize._4)
                {
                    ulong i3 = texel >> 1;
                    i = (byte)(((i3 << 5) | (i3 << 2) | (i3 >> 1)) & 0xFF);
                    alpha = (byte)(0xFF * (texel & 1));
                }
                else if (tileDescriptor.bitSize == BitSize._8)
                {
                    ulong i4 = texel >> 4;
                    ulong a4 = texel & 0x0F;
                    i = (byte)(i4 | (i4 << 4));
                    alpha = (byte)(a4 | (a4 << 4));
                }
                else if (tileDescriptor.bitSize == BitSize._16)
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
            else if (tileDescriptor.format == ColorFormat.RGBA)
            {
                if (tileDescriptor.bitSize == BitSize._16)
                {
                    return RGBA16ToColor((ushort)texel);
                }
                else if (tileDescriptor.bitSize == BitSize._32)
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
