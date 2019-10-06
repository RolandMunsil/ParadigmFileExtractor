using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor
{
    class FormUnpacker
    {
        public static void UnpackFile(byte[] file, string unpackDir, string unpackFilename)
        {
            List<(string, byte[])> sections = ExtractFileSections(file);

            if (sections.Count == 1)
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

        public static byte[] DecompressUVRMFileTable(byte[] compressedFileTable)
        {
            List<(string, byte[])> sections = ExtractFileSections(compressedFileTable);

            if (sections.Count != 1)
            {
                throw new InvalidDataException("Unknown file table format - UVRM File Table has more than 1 section!");
            }

            return sections[0].Item2;
        }

        private static List<(string, byte[])> ExtractFileSections(byte[] file)
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
                    case "\0\0\0\0":
                        // Some files in AeroFighters Assault just have a bunch of 0s at the end randomly???
                        while(pos < file.Length && file.ReadInt(pos) == 0x0)
                        {
                            pos += 4;
                        }
                        continue;
                    case "PAD ":
                        pos += 8 + file.ReadInt(pos + 4);
                        continue;
                    case "GZIP":
                        string compressedDataMagicWord;
                        containedData = ExtractGzipSection(file, pos, out sectionLength, out compressedDataMagicWord);
                        filename = $"{subSectionCtr:d2}.{compressedDataMagicWord}";
                        break;
                    default:
                        containedData = ReadDataInSection(file, pos, out sectionLength);
                        filename = $"{subSectionCtr:d2}.{nextMagicWord.ToLower().Replace(".", "")}";
                        break;
                }
                subSectionCtr++;
                sections.Add((filename, containedData));
                pos += sectionLength;
            }

            return sections;
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
