using ParadigmFileExtractor.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace ParadigmFileExtractor.UVFT
{
    class UVFTConverter
    {
        struct CharacterDescriptor
        {
            public ushort widthTexels;
            public ushort sourceIMAGTexelsPerLine;
            public ushort startTexel;
            public ushort sourceIMAGIndex;
            public ushort heightTexels;
            public int indexInBITM; 

            public CharacterDescriptor(byte[] data, int indexInBITM)
            {
                if (data.ReadUInt16(0x06) != 0 || data.ReadUInt16(0x08) != 0 || data.ReadUInt16(0x0E) != 0)
                    throw new Exception();

                widthTexels = data.ReadUInt16(0x0);
                sourceIMAGTexelsPerLine = data.ReadUInt16(0x2);
                startTexel = data.ReadUInt16(0x04);
                sourceIMAGIndex = data.ReadUInt16(0x0A);
                heightTexels = data.ReadUInt16(0x0C);
                this.indexInBITM = indexInBITM;
            }
        }

        public static void DumpFonts(byte[] romBytes, string outputDir)
        {
            Directory.CreateDirectory(outputDir + "Converted");
            string fullOutputPath = outputDir + "Converted/font/";
            Directory.CreateDirectory(fullOutputPath);

            // ok this is dumb i need to fix this
            Filesystem.Filesystem filesystem = new Filesystem.Filesystem(romBytes);

            foreach (Filesystem.Filesystem.File file in filesystem.AllFiles.Where(file => file.fileTypeFromFileHeader == "UVFT"))
            {
                // TODO
                string outputSubfolder = $"{fullOutputPath}[0x{file.formLocationInROM:x6}]/";
                Directory.CreateDirectory(outputSubfolder);

                List<byte[]> imageDatas = file.SectionsOfType("IMAG");
                byte[] charIndexToASCII = file.Section("STRG");
                byte[] frmtSec = file.Section("FRMT");
                if (frmtSec.Length != 8)
                {
                    throw new Exception();
                }
                Texels.ColorFormat colorFormat = (Texels.ColorFormat)frmtSec.ReadUInt32(0);
                Texels.BitSize bitSize = (Texels.BitSize)frmtSec.ReadUInt32(4);

                CharacterDescriptor[] characterDescriptors = file.Section("BITM")
                    .InGroupsOf(16)
                    .Select((charDescData, index) => new CharacterDescriptor(charDescData, index))
                    .ToArray();

                // First, save off the main images
                //for (int i = 0; i < imageDatas.Count; i++)
                //{
                //    byte[] imageData = imageDatas[i];
                //    IEnumerable<CharacterDescriptor> relevantChars = characterDescriptors.Where(c => c.sourceIMAGIndex == i);
                //    int texelsPerLine = relevantChars.First().sourceIMAGTexelsPerLine;
                //    if(relevantChars.Any(c => c.sourceIMAGTexelsPerLine != texelsPerLine))
                //    {
                //        throw new Exception("Inconsistent texels per line in BITM section!");
                //    }
                //    int imagHeightTexels = relevantChars.Max(c => c.heightTexels);
                //    int bytesPerLine = Texels.GetNumBytes(texelsPerLine, bitSize);

                //    Bitmap bitmap = Texels.ConvertToBitmap(imageData, colorFormat, bitSize, texelsPerLine, imagHeightTexels, bytesPerLine, false, false, null);
                //    bitmap.Save($"{outputSubfolder}{i+1}.png");
                //}

                // Now save each of the characters
                //string individualCharsDirectory = $"{outputSubfolder}Individual Characters/";
                //Directory.CreateDirectory(individualCharsDirectory);
                foreach(CharacterDescriptor charDesc in characterDescriptors)
                {
                    byte[] sourceIMAG = imageDatas[charDesc.sourceIMAGIndex];
                    byte[] texelData;
                    if (bitSize == Texels.BitSize._4)
                    {
                        texelData = GetByteArrayStartingFromFourBitIndex(sourceIMAG, charDesc.startTexel).ToArray();
                    }
                    else
                    {
                        texelData = sourceIMAG.Skip(Texels.GetNumBytes(charDesc.startTexel, bitSize)).ToArray();
                    }

                    int bytesPerLine = Texels.GetNumBytes(charDesc.sourceIMAGTexelsPerLine, bitSize);

                    Bitmap bitmap = Texels.ConvertToBitmap(texelData, colorFormat, bitSize, charDesc.widthTexels, charDesc.heightTexels, bytesPerLine, false, false, null);

                    string filename = $"{charDesc.indexInBITM:D2}";
                    char ascii = Convert.ToChar(charIndexToASCII[charDesc.indexInBITM]);
                    if (!Path.GetInvalidFileNameChars().Contains(ascii))
                    {
                        filename += $" ({ascii})";
                    }

                    bitmap.Save($"{outputSubfolder}{filename}.png");
                }
                Console.WriteLine($"Converted font at {file.formLocationInROM:x6}");
            }
        }

        private static IEnumerable<byte> GetByteArrayStartingFromFourBitIndex(byte[] b, int fourBitUnits)
        {
            if (fourBitUnits % 2 == 0)
            {
                return b.Skip(fourBitUnits / 2);
            }
            else
            {
                return GetByteArrayStartingFromOddFourBitIndex(b, fourBitUnits);
            }
        }

        private static IEnumerable<byte> GetByteArrayStartingFromOddFourBitIndex(byte[] b, int fourBitUnits)
        {
            int i = fourBitUnits / 2;
            while (i < b.Length - 1)
            {
                yield return (byte)(((b[i] & 0x0F) << 4) | ((b[i + 1] & 0xF0) >> 4));
                i++;
            }
            yield return (byte)((b[b.Length - 1] & 0x0F) << 4);
        }
    }
}
