using ParadigmFileExtractor.Common;
using ParadigmFileExtractor.Filesystem;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static ParadigmFileExtractor.Common.Texels;

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

                string? cmdString = SaveAsPNG(uvtx, fullOutputPath + outputFileName);

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

        public class TileDescriptor
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

        public class ColorCombinerSettings
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

        public enum Fast3DEX2Opcode
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

        public class RDPState
        {
            // Note: Idk if this is exactly how things are stored in the RDP, this
            // is just based on reading the docs
            public byte[] tmem = new byte[4096];
            public TileDescriptor[] tileDescriptors = new TileDescriptor[8];
            public ColorCombinerSettings colorCombinerSettings = new ColorCombinerSettings();
            public bool texturingEnabled = false;
            public byte tileToUseWhenTexturing = 0xFF;
            public byte maxMipMapLevels = 0xFF;
            public float textureScaleS = -1;
            public float textureScaleT = -1;
            public uint? nextDRAMAddrForLoad = null;
            public BitSize? bitSizeOfNextDataForLoad = null;
        }

        static string? SaveAsPNG(UVTXFile uvtx, string filePathWithoutExtension)
        {

            StringBuilder cmdsString;
            RDPState rdpState = ExecuteCommands(uvtx, out cmdsString);

            // Some don't have a setTileSize, use the size from the uvtx footer
            TileDescriptor tileToBeDrawn = rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing];
            if (tileToBeDrawn.sLo == 0 && tileToBeDrawn.tLo == 0 && tileToBeDrawn.tHi == 0)
            {
                rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing].sLo = 0.5f;
                rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing].tLo = 0.5f;
                rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing].sHi = uvtx.imageWidth - 0.5f;
                rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing].tHi = uvtx.imageHeight - 0.5f;
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
            string filePath = filePathWithoutExtension + "-" + rdpState.tileToUseWhenTexturing + ".png";
            ConvertTileToBitmap(rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing], rdpState.tmem, uvtx.palettes).Save(filePath);
            return cmdsString.ToString();
        }

        //TODO: implement G_SetOtherMode_H, *maybe* G_SETCOMBINE
        public static RDPState ExecuteCommands(UVTXFile uvtx, out StringBuilder cmdsString)
        {
            RDPState rdpState = new RDPState();

            cmdsString = new StringBuilder();
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

                            rdpState.tileToUseWhenTexturing = tileDescIndex;
                            rdpState.maxMipMapLevels = mipMap;
                            rdpState.texturingEnabled = on;
                            rdpState.textureScaleS = scaleFactorS;
                            rdpState.textureScaleT = scaleFactorT;

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

                            TileDescriptor t = rdpState.tileDescriptors[tileDescIndex];
                            t.sLo = sLoRaw / 4.0f;
                            t.tLo = tLoRaw / 4.0f;
                            t.sHi = sHiRaw / 4.0f;
                            t.tHi = tHiRaw / 4.0f;

                            float visWidth = (t.sHi - t.sLo) + 1;
                            float visHeight = (t.tHi - t.tLo) + 1;

                            operationDesc = "G_SETTILESIZE    (Set texture coords and size)";
                            operationDesc += $": tile {tileDescIndex} lo=({t.sLo}, {t.tLo}) hi=({t.sHi}, {t.tHi}) [[{visWidth}, {visHeight}]]";
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

                            if (rdpState.nextDRAMAddrForLoad == null || rdpState.bitSizeOfNextDataForLoad == null)
                            {
                                throw new Exception();
                            }

                            rdpState.tileDescriptors[tileDescIndex].sLo = sLo;
                            rdpState.tileDescriptors[tileDescIndex].tLo = tLo;
                            rdpState.tileDescriptors[tileDescIndex].sHi = sHi;
                            rdpState.tileDescriptors[tileDescIndex].tHi = dxt; // Not 100% sure this is the correct behavior


                            int dataStart = (int)rdpState.nextDRAMAddrForLoad;
                            int dataLengthBytes = (sHi + 1) * Texels.BitSizeToNumBytes((BitSize)rdpState.bitSizeOfNextDataForLoad);
                            int destPtr = rdpState.tileDescriptors[tileDescIndex].tmemAddressInWords * 8;

                            // I'm assuming this is the correct behavior because if I don't do this a lot of textures have a notch at the top right
                            // (Also it would make sense given that interleaving and addresses are all done on 64-bit words
                            dataLengthBytes = (int)Math.Ceiling(dataLengthBytes / 8f) * 8;

                            // Note: technically this inaccurate, we shouldn't clamp. But the instructions read beyond the file and IDK why,
                            // it doesn't seem to serve any purpose so I assume it's a bug (or I don't understand something about how the RSP works)
                            Array.Copy(uvtx.texelData, dataStart, rdpState.tmem, destPtr, Math.Min(uvtx.texelData.Length - dataStart, dataLengthBytes));

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
                            rdpState.tileDescriptors[tileDescIndex] = t;

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

                            rdpState.colorCombinerSettings.primR = r;
                            rdpState.colorCombinerSettings.primG = g;
                            rdpState.colorCombinerSettings.primB = b;
                            rdpState.colorCombinerSettings.primA = a;
                            rdpState.colorCombinerSettings.minLODLevel = minLODLevel;
                            rdpState.colorCombinerSettings.LODfrac = LODfrac;

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

                            rdpState.colorCombinerSettings.envR = r;
                            rdpState.colorCombinerSettings.envG = g;
                            rdpState.colorCombinerSettings.envB = b;
                            rdpState.colorCombinerSettings.envA = a;

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

                            rdpState.nextDRAMAddrForLoad = dramAddress;
                            rdpState.bitSizeOfNextDataForLoad = bitSize;

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

            return rdpState;
        }

        private static Bitmap ConvertTileToBitmap(TileDescriptor tileDesc, byte[] tmem, ushort[][] palettes)
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

            byte[] data = tmem.Subsection(startPtrBytes, bytesPerLine * height);
            ushort[]? palette = tileDesc.palette == 0 ? null : palettes[tileDesc.palette - 1];

            return Texels.ConvertToBitmap(data, tileDesc.format, tileDesc.bitSize, width, height, bytesPerLine, true, true, palette);
        }

        public static Bitmap GetBitmap(UVTXFile uvtx)
        {
            RDPState rdpState = ExecuteCommands(uvtx, out _);
            return ConvertTileToBitmap(rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing], rdpState.tmem, uvtx.palettes);
        }
    }
}
