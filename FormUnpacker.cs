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
        public static void UnpackTexture(byte[] form, string rawDir, string unpackedDir, string filename)
        {
            Directory.CreateDirectory(rawDir);
            Directory.CreateDirectory(unpackedDir);

            int pos = 4;
            while (form.ReadMagicWord(pos) == "PAD ")
            {
                int padLength = form.ReadInt(pos + 4);
                pos += 8 + padLength;
            }
            string firstActualMagicWord = form.ReadMagicWord(pos);
            if(firstActualMagicWord == "GZIP")
            {
                byte[] gzipData = ReadDataInSubSection("GZIP", form, pos, true);

                if(form.ReadMagicWord(pos + 8) != "COMM")
                {
                    throw new InvalidOperationException();
                }
                int decompressedLength = form.ReadInt(pos + 12);
                if(form.ReadMagicWord(pos + 16) != "MIO0")
                {
                    throw new InvalidOperationException();
                }
                if(decompressedLength != form.ReadInt(pos + 20))
                {
                    throw new InvalidOperationException();
                }
                string fileLoc = $"{rawDir}{filename}.compressed_texture";
                AsyncWriteHelper.WriteAllBytes(fileLoc, form);
                string cmdText = $"/C mio0.exe -d -o {pos + 16} {fileLoc} {unpackedDir}{filename}.texture";

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = cmdText;
                process.StartInfo = startInfo;
                process.Start();
            }
            else if (firstActualMagicWord == "COMM")
            {
                byte[] commData = ReadDataInSubSection("COMM", form, pos, true);

                AsyncWriteHelper.WriteAllBytes($"{rawDir}{filename}.non_compressed_texture", commData);
                AsyncWriteHelper.WriteAllBytes($"{unpackedDir}{filename}.texture", commData);
            }
            else
            {
                throw new InvalidOperationException(); 
            }
        }

        public static byte[] ReadDataInSubSection(string expectedWord, byte[] form, int subSecPos, bool shouldBeLastSubSection = false)
        {
            if(form.ReadMagicWord(subSecPos) != expectedWord)
            {
                throw new InvalidOperationException();
            }
            int length = form.ReadInt(subSecPos + 4);

            if(shouldBeLastSubSection && (subSecPos + 8 + length) != form.Length)
            {
                throw new InvalidOperationException();
            }

            return form.GetSubArray(subSecPos + 8, length);
        }
    }
}
