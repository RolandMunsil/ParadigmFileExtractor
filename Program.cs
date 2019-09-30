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

        // TODO: only special cases
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

        static string outputDir;// = "Out/";
        static string RAW_SUBDIR = "Raw/";
        static string UNPACKED_SUBDIR = "Unpacked/";

        static byte[] romBytes;

        static void Main(string[] args)
        {
            string romPath = String.Join(" ", args).Replace("\"", "");
            romBytes = File.ReadAllBytes(romPath);
            outputDir = Path.GetFileNameWithoutExtension(romPath) + "/";

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

            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(outputDir + RAW_SUBDIR);
            Directory.CreateDirectory(outputDir + UNPACKED_SUBDIR);

            //Save off bits before file table
            File.WriteAllBytes(outputDir + "[0x00000] Data before file table.bin", romBytes.GetSubArray(0, fileTablePtr));

            //Save file table
            int fTableLength = romBytes.ReadInt(fileTablePtr + 4);
            byte[] fTableBytes = romBytes.GetSubArray(fileTablePtr + 8, fTableLength);
            File.WriteAllBytes($"{outputDir}[0x{fileTablePtr:x6}] File Table.bin", fTableBytes);

            Dictionary<string, int> fileTypeCount = new Dictionary<string, int>();
     
            int lastFileEnd = startOfFiles;
            for (int i = 0; i < orderedTable.Count; i++)
            {
                KeyValuePair<int, string> kvPair = orderedTable[i];
                int formPtr = kvPair.Key;
                string formFTType = kvPair.Value;

                if (formPtr > lastFileEnd)
                {
                    File.WriteAllBytes($"{outputDir}[0x{lastFileEnd:x6}].bin", romBytes.GetSubArray(lastFileEnd, formPtr - lastFileEnd));
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
                Directory.CreateDirectory(outputDir + RAW_SUBDIR + niceFileType + "/");

                int sectionLength;

                if (firstMagicWord == "<noheader>")
                {
                    sectionLength = orderedTable[i + 1].Key - formPtr;

                    byte[] headerlessFile = romBytes.GetSubArray(formPtr, sectionLength);
                    string outputName = $"[0x{formPtr:x6}]";

                    AsyncWriteHelper.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{niceFileType}/{outputName}.{niceFileType}", headerlessFile);

                    string filetypeDir = $"{outputDir}{UNPACKED_SUBDIR}{niceFileType}/";
                    Directory.CreateDirectory(filetypeDir);
                    AsyncWriteHelper.WriteAllBytes($"{filetypeDir}{outputName}.{niceFileType}", headerlessFile);

                    //File.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{niceFileType}/[0x{formPtr:x6}].{niceFileType.ToLower()}", 
                    //    romBytes.GetSubArray(formPtr, sectionLength));
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
                // Check if there's actually useful data here
                // In most cases it seems like this is just 0x0 until the next address that's a multiple of 16, and then 0xFF from then on until the end of the ROM.
                int expectedStartOfFFs = lastFileEnd + (0x10 - (lastFileEnd % 0x10));
                int curPos = lastFileEnd;
                bool potentiallyInterestingDataPresent = false;
                for(; curPos < expectedStartOfFFs; curPos++)
                {
                    if(romBytes[curPos] != 0x00)
                    {
                        potentiallyInterestingDataPresent = true;
                    }
                }
                if (!potentiallyInterestingDataPresent)
                {
                    for (; curPos < romBytes.Length; curPos++)
                    {
                        if (romBytes[curPos] != 0xFF)
                        {
                            potentiallyInterestingDataPresent = true;
                        }
                    }
                }

                if (potentiallyInterestingDataPresent)
                {
                    File.WriteAllBytes($"{outputDir}[0x{lastFileEnd:x6}].bin", romBytes.GetSubArray(lastFileEnd, romBytes.Length - lastFileEnd));
                }
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

            // 1) Find first FORM magic word in ROM
            for (int pos = 0; pos < romBytes.Length; pos++)
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
                        //Console.WriteLine($"FFFFFF ptr! @ {curFileTablePos + sectionPos:x}");
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


            string extraNameInfo = "";
            if(niceFileType == "modu")
            {
                int namePtr = 8 + Encoding.ASCII.GetString(formBytes).IndexOf("MDBG");
                string name = Encoding.ASCII.GetString(formBytes, namePtr, 0x20).Replace("\0","");
                extraNameInfo = " " + name;
            }
            string outputName = $"[0x{formPtr:x6}]{extraNameInfo}";

            AsyncWriteHelper.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{niceFileType}/{outputName}.{niceFileType}", formBytes);

            string filetypeDir = $"{outputDir}{UNPACKED_SUBDIR}{niceFileType}/";
            Directory.CreateDirectory(filetypeDir);
            FormUnpacker.UnpackFile(formBytes, $"{filetypeDir}{outputName}/", $"{filetypeDir}{outputName}.{niceFileType}");

            return formLength;
        }
    }
}
