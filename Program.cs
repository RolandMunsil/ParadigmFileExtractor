using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace BARExtractor
{
    class Program
    {
        // Note: files with a mismatch between file table type and type in header will have it
        // concatenated to <headertype>/<filetabletype>
        static Dictionary<string, string> magicWordToFolderName = new Dictionary<string, string>
        {
            {"UVAN",      "janim"},
            {"UVBT",      "blit"},
            {"UVCT",      "contour"},
            {"UVDS",      "dset"},
            {"UVEN",      "env"},
            {"UVFT",      "font"},
            {"UVMB",      "mb"}, // couldn't find any loader code
            {"UVMD",      "uvmodel"},
            {"UVMO/MODU", "modu"}, // couldn't find any loader code
            {"UVMS",      "ms"}, // couldn't find any loader code
            {"UVPX",      "pfx"},
            {"UVRW/VMSK", "ufile"},
            {"UVRW/<noheader>", "headerless_ufile"},
            {"UVSX",      "sx"}, // couldn't find any loader code
            {"UVTR",      "terra"},
            {"UVTS/UVSQ", "tseq"},
            {"UVTT",      "track"},
            {"UVTX",      "texture"},
            {"UVVL",      "volume"},
        };

        static string OUTPUT_DIR = "Out/";
        static string RAW_SUBDIR = "Raw/";
        static string UNPACKED_SUBDIR = "Unpacked/";

        static byte[] romBytes;

        static void Main(string[] args)
        {
            romBytes = File.ReadAllBytes("bar.z64");

            int fileTablePtr = DetermineFileTableLocation(romBytes);

            // Check that file table is in the right place
            if (romBytes.ReadMagicWord(fileTablePtr) != "FORM" || romBytes.ReadMagicWord(fileTablePtr + 8) != "UVFT")
            {
                throw new InvalidOperationException("File table not in the right place! Have you loaded the right ROM?");
            }

            Dictionary<int, string> fileTablePtrToType = ParseFileTable(fileTablePtr);
            List<KeyValuePair<int, string>> orderedTable = fileTablePtrToType.OrderBy(kv => kv.Key).ToList();
            int startOfFiles = orderedTable[0].Key;

            VerifyNoFilesMissingFromFileTable(fileTablePtrToType, startOfFiles);

            Directory.CreateDirectory(OUTPUT_DIR);
            Directory.CreateDirectory(OUTPUT_DIR + RAW_SUBDIR);
            Directory.CreateDirectory(OUTPUT_DIR + UNPACKED_SUBDIR);

            //Save off bits before file table
            File.WriteAllBytes(OUTPUT_DIR + "[0x00000] Data before file table.bin", romBytes.GetSubArray(0, fileTablePtr));

            //Save file table
            int fTableLength = romBytes.ReadInt(fileTablePtr + 4);
            byte[] fTableBytes = romBytes.GetSubArray(fileTablePtr + 8, fTableLength);
            File.WriteAllBytes($"{OUTPUT_DIR}[0x{fileTablePtr:x}] File Table.uvft", fTableBytes);

            Dictionary<string, int> fileTypeCount = new Dictionary<string, int>();
     
            int lastFileEnd = startOfFiles;
            for (int i = 0; i < orderedTable.Count; i++)
            {
                KeyValuePair<int, string> kvPair = orderedTable[i];
                int formPtr = kvPair.Key;
                string formFTType = kvPair.Value;

                if (formPtr > lastFileEnd)
                {
                    File.WriteAllBytes($"{OUTPUT_DIR}[0x{lastFileEnd:x}].bin", romBytes.GetSubArray(lastFileEnd, formPtr - lastFileEnd));
                }

                string firstMagicWord;
                if (romBytes.ReadMagicWord(formPtr) != "FORM")
                {
                    firstMagicWord = "<noheader>";
                }
                else
                {
                    firstMagicWord = romBytes.ReadMagicWord(formPtr + 8);
                }

                string magicWord = formFTType;

                //Just in case there are games with are inconsistencies in the magic numbers
                if (firstMagicWord != formFTType)
                {
                    magicWord += "/" + firstMagicWord;
                }
                string niceFileType = magicWordToFolderName[magicWord];
                Directory.CreateDirectory(OUTPUT_DIR + RAW_SUBDIR + niceFileType + "/");

                int sectionLength;

                if (firstMagicWord == "<noheader>")
                {
                    sectionLength = orderedTable[i + 1].Key - formPtr;
                    File.WriteAllBytes($"{OUTPUT_DIR}{RAW_SUBDIR}{niceFileType}/[{formPtr}].{niceFileType.ToLower()}", 
                        romBytes.GetSubArray(formPtr, sectionLength));
                }
                else
                {
                    sectionLength = 8 + SaveFormToFile(formPtr, niceFileType);
                }

                if (!fileTypeCount.ContainsKey(niceFileType))
                    fileTypeCount[niceFileType] = 1;
                else
                    fileTypeCount[niceFileType]++;

                lastFileEnd = formPtr + sectionLength;
            }

            if(lastFileEnd < romBytes.Length)
            {
                File.WriteAllBytes($"{OUTPUT_DIR}[0x{lastFileEnd:x}].bin", romBytes.GetSubArray(lastFileEnd, romBytes.Length - lastFileEnd));
            }

            AsyncWriteHelper.WaitForFilesToFinishWriting();

            foreach (KeyValuePair<string, int> kvPair in fileTypeCount.OrderBy(kv => kv.Key))
            {
                Console.WriteLine($"{kvPair.Key}: {kvPair.Value} files");
            }
            Console.ReadLine();            
        }

        private static int DetermineFileTableLocation(byte[] romBytes)
        {
            // Check two ways:
            // 1) Interpretation of first 4 instructions
            uint[] instrs =
            {
                (uint)romBytes.ReadInt(0x1000),
                (uint)romBytes.ReadInt(0x1004),
                (uint)romBytes.ReadInt(0x1008),
                (uint)romBytes.ReadInt(0x100C)
            };
            
            uint ramPtr = 0x0;
            bool upperFound = false;
            bool lowerFound = false;
            foreach(uint instr in instrs)
            {
                // lui t0,<data> always starts with 0x3c08, addiu t0,t0,<data> always starts with 0x2508
                if ((instr >> 16) == 0x3c08)
                {
                    if (upperFound)
                        throw new InvalidOperationException("Duplicate lui t0 instructions");
                    ramPtr |= instr << 16;
                    upperFound = true;
                }
                if ((instr >> 16) == 0x2508)
                {
                    if (lowerFound)
                        throw new InvalidOperationException("Duplicate addiu t0,t0 instructions");
                    ramPtr |= instr & 0x0000FFFF;
                    lowerFound = true;
                }
            }
            if(!lowerFound || !upperFound)
            {
                // TODO: should probably just print a warning
                throw new InvalidOperationException("Unable to locate file table based on instructions!");
            }

            int romPtr = (int)(0x1000 + (ramPtr - 0x80000400));

            // Now double check that this is the first FORM object in the ROM
            for(int pos = 0; pos < romBytes.Length; pos++)
            {
                if(romBytes.ReadMagicWord(pos) == "FORM")
                {
                    if(pos == romPtr)
                    {
                        // Methods match, return
                        return (int)romPtr;
                    }
                    else
                    {
                        throw new InvalidOperationException("Mismatch between file table ptr and location of first FORM file");
                    }
                }
            }

            throw new InvalidOperationException("Unable to find file table via search through ROM");
        }

        private static Dictionary<int, string> ParseFileTable(int FILE_TABLE_LOCATION)
        {
            Dictionary<int, string> fileTablePtrToType = new Dictionary<int, string>();

            int fileTableLength = romBytes.ReadInt(FILE_TABLE_LOCATION + 4);
            int startOfFiles = FILE_TABLE_LOCATION + 8 + fileTableLength;
            // Align to 16-byte index
            if ((startOfFiles & 0xF) != 0x0)
            {
                startOfFiles += 0x10 - (startOfFiles & 0xF);
            }

            byte[] fileTable = romBytes.GetSubArray(FILE_TABLE_LOCATION + 8, fileTableLength);

            // Read the file table
            int curFileTablePos = 4;
            while (curFileTablePos < fileTableLength)
            {
                // Read the magic word and length
                string fileType = fileTable.ReadMagicWord(curFileTablePos);
                int sectionLength = fileTable.ReadInt(curFileTablePos + 4);
                curFileTablePos += 8;
                // Read the section
                for (int sectionPos = 0; sectionPos < sectionLength; sectionPos += 4)
                {
                    int tableWord = fileTable.ReadInt(curFileTablePos + sectionPos);
                    if ((uint)tableWord == 0xFFFFFFFF)
                    {
                        Console.WriteLine($"FFFFFF ptr! @ {curFileTablePos + sectionPos:x}");
                        continue;
                    }
                    int filePtr = startOfFiles + tableWord;
                    if (fileTablePtrToType.ContainsKey(filePtr))
                    {
                        throw new Exception("Duplicate references in file table!");
                    }

                    // Console.WriteLine($"Ptr: {tableWord:x} | Type: {fileType}");
                    fileTablePtrToType.Add(filePtr, fileType);
                }
                curFileTablePos += sectionLength;
            }

            return fileTablePtrToType;
        }

        private static void VerifyNoFilesMissingFromFileTable(Dictionary<int, string> fileTablePtrToType, int startOfFiles)
        {
            int curFilePos = startOfFiles;
            while (curFilePos < romBytes.Length - 4)
            {
                if (romBytes.ReadMagicWord(curFilePos) == "FORM")
                {                   
                    if (!fileTablePtrToType.ContainsKey(curFilePos))
                    {
                        //TODO: don't throw exception, instead handle this.
                        throw new Exception("Found file that's not present in file table");
                    }

                    int formLength = romBytes.ReadInt(curFilePos + 4);
                    curFilePos += formLength + 8;
                }
                else
                {
                    curFilePos++;
                }
            }
        }

        public static int SaveFormToFile(int formPtr, string niceFileType)
        {
            if(romBytes.ReadMagicWord(formPtr) != "FORM")
            {
                throw new InvalidOperationException("Tried to read a form where there wasn't any!");
            }

            int formLength = romBytes.ReadInt(formPtr + 4);
            byte[] formBytes = romBytes.GetSubArray(formPtr + 8, formLength);
            if (niceFileType == "texture")
            {
                FormUnpacker.UnpackTexture(formBytes, $"{OUTPUT_DIR}{RAW_SUBDIR}{niceFileType}/", $"{OUTPUT_DIR}{UNPACKED_SUBDIR}{niceFileType}/", $"[{formPtr}]");
            }
            else
            {
                AsyncWriteHelper.WriteAllBytes($"{OUTPUT_DIR}{RAW_SUBDIR}{niceFileType}/[{formPtr}].{niceFileType.ToLower()}", formBytes);
            }

            return formLength;
        }
    }
}
