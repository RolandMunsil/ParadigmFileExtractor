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
        static Dictionary<string, int> serialToFileTablePtr = new Dictionary<string, int>
        {
            {"NNSE", 0x237D0}, // Beetle Adventure Racing (US)
            {"NNSP", 0x237D0}, // Beetle Adventure Racing (EU)
            {"NB8J", 0x23270}, // Beetle Adventure Racing (JP)
            {"NNSX", 0x233E0}, // HSV Adventure Racing (AU)

            {"NICE", 0x5AF680}, // Indy Racing 2000 (US)

            {"NDUE", 0x6EE260}, // Duck Dodgers Starring Daffy Duck (US) 
            {"NDUP", 0x6EE5E0}, // Daffy Duck Starring as Duck Dodgers (EU)

            {"NFWE", 0x334F0}, // F-1 World Grand Prix (US)
            {"NFWP", 0x335C0}, // F-1 World Grand Prix (EU)
            {"NFWF", 0x335A0}, // F-1 World Grand Prix (FR)
            {"NFWD", 0x33B70}, // F-1 World Grand Prix (DE)
            {"NFWJ", 0x33720}, // F-1 World Grand Prix (JP)

            {"NF2P", 0x2F040}, // F-1 World Grand Prix II (EU)

            //TODO:
            // Pilotwings, AeroFighters, Sonc Wings
            // F-1 World Grand Prix beta is supported by the manual search 
        };

        // Note: files with a mismatch between file table type and type in header will have it
        // concatenated to <headertype>/<filetabletype>
        static Dictionary<string, string> magicWordToFolderName = new Dictionary<string, string>
        {
            {"CNMA",      "cinema"},
            {"DEMO",      "demo"},
            {"FTKL",      "itrack"},
            {"STRY",      "f1story"},
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
            {"UVSX",      "sx"}, // couldn't find any loader code
            {"UVTR",      "terra"},
            {"UVTP",      "texturexref"},
            {"UVTS/UVSQ", "tseq"},
            {"UVTT",      "track"},
            {"UVTX",      "texture"},
            {"UVVL",      "volume"},
        };

        static string RAW_FILETYPE_DIR = "raw";

        static string outputDir;
        static string RAW_SUBDIR = "Raw/";
        static string UNPACKED_SUBDIR = "Unpacked/";

        static byte[] romBytes;

        static void Main(string[] args)
        {
            string romPath = String.Join(" ", args).Replace("\"", "");
            romBytes = File.ReadAllBytes(romPath);
            outputDir = Path.GetFileNameWithoutExtension(romPath) + "/";

            Console.WriteLine("Locating file table...");
            string serial = romBytes.ReadMagicWord(0x3B);
            int fileTablePtr;
            bool hadToSearch = false;
            if(serialToFileTablePtr.ContainsKey(serial))
            {
                fileTablePtr = serialToFileTablePtr[serial];
                Console.WriteLine($"Matched game serial to file table location! ({serial} -> 0x{fileTablePtr:x})");

                // Check that this pointer is correct
                if (romBytes.ReadMagicWord(fileTablePtr) != "FORM" || romBytes.ReadMagicWord(fileTablePtr + 8) != "UVFT")
                {
                    Console.WriteLine("Stored file table location was incorrect - initiating a search for file table header...");
                    fileTablePtr = SearchForFileTable(romBytes);
                    hadToSearch = true;
                }
            }
            else
            {
                Console.WriteLine("Game serial not in database - initiating a search for file table header...");
                fileTablePtr = SearchForFileTable(romBytes);
                hadToSearch = true;
            }

            if(fileTablePtr == -1)
            {
                Console.WriteLine("ERROR: Could not locate file table.");
                throw new InvalidOperationException();
            }
            else if (hadToSearch)
            {
                Console.WriteLine("File table found by searching!");
            }

            Console.WriteLine("Parsing file table...");
            Dictionary<int, string> fileTablePtrToType = ParseFileTable(fileTablePtr);
            List<KeyValuePair<int, string>> orderedTable = fileTablePtrToType.OrderBy(kv => kv.Key).ToList();
            int startOfFiles = orderedTable[0].Key;

            VerifyNoFilesMissingFromFileTable(fileTablePtrToType, startOfFiles);

            
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(outputDir + RAW_SUBDIR);
            Directory.CreateDirectory(outputDir + UNPACKED_SUBDIR);

            //Save off bits before file table
            File.WriteAllBytes(outputDir + "[0x00000] Data before file table.bin", romBytes.GetSubArray(0, fileTablePtr));
            Console.WriteLine("Section of ROM that's before file table saved to file.");

            //Save file table
            int fTableLength = romBytes.ReadInt(fileTablePtr + 4);
            byte[] fTableBytes = romBytes.GetSubArray(fileTablePtr + 8, fTableLength);
            File.WriteAllBytes($"{outputDir}[0x{fileTablePtr:x6}] File Table.bin", fTableBytes);
            Console.WriteLine("File table saved to file.");

            Console.WriteLine("Extracting files...");
            Dictionary<string, int> fileTypeCount = new Dictionary<string, int>();
     
            int lastFileEnd = startOfFiles;
            for (int i = 0; i < orderedTable.Count; i++)
            {
                KeyValuePair<int, string> kvPair = orderedTable[i];
                int formPtr = kvPair.Key;
                string magicWordInFileTable = kvPair.Value;

                if (formPtr > lastFileEnd)
                {
                    File.WriteAllBytes($"{outputDir}[0x{lastFileEnd:x6}].bin", romBytes.GetSubArray(lastFileEnd, formPtr - lastFileEnd));
                }

                int sectionLength;
                string niceFileType;

                if (magicWordInFileTable == "UVRW")
                {
                    niceFileType = "raw";

                    // This is a raw file, it needs special handling.
                    if (romBytes.ReadMagicWord(formPtr) != "FORM")
                    {
                        // File has no header
                        sectionLength = orderedTable[i + 1].Key - formPtr;
                        SaveHeaderlessFile(formPtr, sectionLength);
                    }
                    else
                    {
                        // File has a header
                        string fileType = romBytes.ReadMagicWord(formPtr + 8).ToLower();
                        sectionLength = 8 + SaveFormToFile(formPtr, fileType, RAW_FILETYPE_DIR);
                    }

                }
                else
                {
                    string magicWordInFileHeader = romBytes.ReadMagicWord(formPtr + 8);

                    string magicWord = magicWordInFileTable;
                    //Just in case there are games with are inconsistencies in the magic numbers
                    if (magicWordInFileHeader != magicWordInFileTable)
                    {
                        magicWord += "/" + magicWordInFileHeader;
                    }

                    niceFileType = magicWordToFolderName[magicWord];

                    sectionLength = 8 + SaveFormToFile(formPtr, niceFileType);
                }

                if (!fileTypeCount.ContainsKey(niceFileType))
                    fileTypeCount[niceFileType] = 1;
                else
                    fileTypeCount[niceFileType]++;

                lastFileEnd = formPtr + sectionLength;

                if (i % 100 == 99)
                {
                    Console.WriteLine($"{i+1}/{orderedTable.Count} files extracted...");
                }
            }
            
            if (lastFileEnd < romBytes.Length)
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
            Console.WriteLine("All files extracted!");
            Console.WriteLine();

            foreach (KeyValuePair<string, int> kvPair in fileTypeCount.OrderBy(kv => kv.Key))
            {
                Console.WriteLine($"{kvPair.Key}: {kvPair.Value} files");
            }
            Console.ReadLine();            
        }

        private static int SearchForFileTable(byte[] romBytes)
        {
            //Find first FORM + UVFT magic word in ROM
            for (int pos = 0; pos < romBytes.Length; pos++)
            {
                if(romBytes.ReadMagicWord(pos) == "FORM" && romBytes.ReadMagicWord(pos+8) == "UVFT")
                {
                    return pos;
                }
            }

            return -1;
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
                        throw new Exception($"Found file that's not present in file table (@ 0x{curFilePos:x}). The file table has probably been parsed incorrectly.");
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

        private static void SaveHeaderlessFile(int formPtr, int sectionLength)
        {
            byte[] headerlessFile = romBytes.GetSubArray(formPtr, sectionLength);
            string outputName = $"[0x{formPtr:x6}]";

            Directory.CreateDirectory($"{outputDir}{RAW_SUBDIR}{RAW_FILETYPE_DIR}/");
            AsyncWriteHelper.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{RAW_FILETYPE_DIR}/{outputName}.headerless_file", headerlessFile);

            string filetypeDir = $"{outputDir}{UNPACKED_SUBDIR}{RAW_FILETYPE_DIR}/";
            Directory.CreateDirectory(filetypeDir);
            AsyncWriteHelper.WriteAllBytes($"{filetypeDir}{outputName}.headerless_file", headerlessFile);
        }

        public static int SaveFormToFile(int formPtr, string niceFileType, string override_outputTypeDir = null)
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
            string outputTypeDir = override_outputTypeDir ?? niceFileType;

            Directory.CreateDirectory($"{outputDir}{RAW_SUBDIR}{outputTypeDir}/");
            AsyncWriteHelper.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{outputTypeDir}/{outputName}.{niceFileType}", formBytes);

            string filetypeDir = $"{outputDir}{UNPACKED_SUBDIR}{outputTypeDir}/";
            Directory.CreateDirectory(filetypeDir);
            FormUnpacker.UnpackFile(formBytes, $"{filetypeDir}{outputName}/", $"{filetypeDir}{outputName}.{niceFileType}");

            return formLength;
        }
    }
}
