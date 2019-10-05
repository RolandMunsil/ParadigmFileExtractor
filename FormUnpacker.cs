using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BARExtractor
{
    class FormUnpacker
    {
        public static void UnpackFile(byte[] file, string unpackDir, string unpackFilename)
        {
            List<(string, byte[])> sections = new List<(string, byte[])>();

            int subSectionCtr = 1;
            int pos = 4;
            while (pos < file.Length)
            {
                string nextMagicWord = file.ReadMagicWord(pos);
                byte[] containedData;
                string filename;
                int sectionLength;
                switch (nextMagicWord)
                {
                    case "PAD ":
                        pos += 8 + file.ReadInt(pos + 4);
                        continue;
                    case "GZIP":
                        string compressedDataMagicWord;
                        containedData = ExtractGzipSection(file, pos, out sectionLength, out compressedDataMagicWord);
                        filename = $"{subSectionCtr:d2}.{compressedDataMagicWord}";
                        break;
                    case "COMM":
                    case "CODE":
                    case "MDBG":
                    case "RELA":
                    case "PNTS":
                    case "LNKS":
                    case "VOBJ":
                    case "TEXT":
                    case "PIID":
                    case "PHDR":
                    case "PDAT":
                    case "STRG":
                    case "FRMT":
                    case ".CTL":
                    case ".TBL":
                    case "SEQS":
                    case "BITM":
                    case "SCPT":
                        containedData = ReadDataInSection(file, pos, out sectionLength);
                        filename = $"{subSectionCtr:d2}.{nextMagicWord.ToLower().Replace(".", "")}";
                        break;
                    default:
                        throw new Exception($"Unknown magic word \"{nextMagicWord}\" in file, or file parser is parsing incorrectly");
                }
                subSectionCtr++;
                sections.Add((filename, containedData));
                pos += sectionLength;
            }

            if(sections.Count == 1)
            {
                AsyncWriteHelper.WriteAllBytes($"{unpackFilename}", sections[0].Item2);
            }
            else
            {
                Directory.CreateDirectory(unpackDir);
                foreach ((string filename, byte[] data) in sections)
                {
                    AsyncWriteHelper.WriteAllBytes($"{unpackDir}{filename}", data);
                }
            }
        }

        private static byte[] ExtractGzipSection(byte[] form, int subSecPos, out int sectionlength, out string compressedDataMagicWord)
        {
            sectionlength = 8 + form.ReadInt(subSecPos + 4);

            compressedDataMagicWord = form.ReadMagicWord(subSecPos + 8);
            int decompressedLength = form.ReadInt(subSecPos + 12);
            if (form.ReadMagicWord(subSecPos + 16) != "MIO0")
            {
                throw new InvalidOperationException("GZIP file is missing MIO0 in header.");
            }
            if (decompressedLength != form.ReadInt(subSecPos + 20))
            {
                throw new InvalidOperationException("Length mismatch in GZIP header.");
            }

            byte[] decompressed = PeepsCompress.MIO0.Decompress(subSecPos + 16, form);
            if(decompressed.Length != decompressedLength)
            {
                throw new InvalidOperationException($"Expected length: {decompressedLength}. Actual length: {decompressed.Length}");
            }
            return decompressed;
        }

        public static byte[] ReadDataInSection(byte[] form, int subSecPos, out int sectionlength)
        {
            int dataLength = form.ReadInt(subSecPos + 4);
            sectionlength = 8 + dataLength;
            return form.GetSubArray(subSecPos + 8, dataLength);
        }
    }
}
