using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ParadigmFileExtractor
{
    class Program
    {
        //TODO: should i just get rid of this? search should be able to find it correctly every time.
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

            {"NPWE", 0xDE720}, // Pilotwings 64 (US)
            {"NPWP", 0xE02A0}, // Pilotwings 64 (EU) 
            {"NPWJ", 0xDEC30}, // Pilotwings 64 (JP)

            {"NERE", 0x118690 }, // AeroFighters Assault (US)
            {"NSAP", 0x125460 }, // AeroFighters Assault (EU)

            {"NSAJ", 0x117D50 } // Sonic Wings Assault (JP)
        };

        // Note: files with a mismatch between file table type and type in header will have it
        // concatenated to <headertype>/<filetabletype>

        //TODO: maybe setup auto-filename generation for the ones without a special name
        static Dictionary<string, string> magicWordToFolderName = new Dictionary<string, string>
        {
            {"    ",      "BLANK_FILETYPE" }, // Weird filetype in AeroFighters Assault
            {"3VUE",      "3vue"}, // couldn't find any loader code
            {"ADAT",      "adat"}, // couldn't find any loader code
            {"CNMA",      "cinema"},
            {"DEMO",      "demo"}, // couldn't find any loader code
            {"FTKL",      "itrack"},
            {"LART",      "lart"}, // couldn't find any loader code
            {"PDAT",      "pdat"}, // couldn't find any loader code
            {"SDOC",      "sdoc"}, // couldn't find any loader code
            {"SHAN",      "shan"}, // couldn't find any loader code
            {"SLAN",      "slan"}, // couldn't find any loader code
            {"SPTH",      "spth"}, // couldn't find any loader code
            {"SRED",      "sred"}, // couldn't find any loader code
            {"SSHT",      "ssht"}, // couldn't find any loader code
            {"STRY",      "f1story"},
            {"Trai",      "Trai"}, // Weird filetype in AeroFighters Assault
            {"UPWL",      "upwl"}, // couldn't find any loader code
            {"UPWT",      "upwt"}, // couldn't find any loader code
            {"UVAN",      "janim"},
            {"UVBT",      "blit"},
            {"UVCT",      "contour"},
            {"UVDS",      "dset"},
            {"UVEN",      "env"},
            {"UVFT",      "font"},
            {"UVLT",      "lt"}, // couldn't find any loader code
            {"UVLV",      "lv"}, // couldn't find any loader code
            {"UVMB",      "mb"}, // couldn't find any loader code
            {"UVMD",      "uvmodel"},
            {"UVMO/MODU", "modu"}, // couldn't find any loader code
            {"UVMS",      "ms"}, // couldn't find any loader code
            {"UVPX",      "pfx"},
            {"UVSX",      "sx"}, // couldn't find any loader code
            {"UVSY",      "sy"}, // couldn't find any loader code
            {"UVTR",      "terra"},
            {"UVTP",      "texturexref"},
            {"UVTS/UVSQ", "tseq"},
            {"UVSQ",      "tseq"}, // couldn't find any loader code but it looks like it's the same as UVTS/UVSQ
            {"UVTT",      "track"},
            {"UVTX",      "texture"},
            {"UVVL",      "volume"},
        };

        static string RAW_FILETYPE_DIR = "raw";

        static string outputDir;
        static string RAW_SUBDIR = "Raw/";
        static string UNPACKED_SUBDIR = "Unpacked/";

        static byte[] romBytes;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList(uint[] ProcessList, uint ProcessCount);

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
                if (romBytes.ReadMagicWord(fileTablePtr) != "FORM" || (romBytes.ReadMagicWord(fileTablePtr + 8) != "UVFT" && romBytes.ReadMagicWord(fileTablePtr + 8) != "UVRM"))
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
            bool isCompressedFileTable;
            Dictionary<int, string> fileTablePtrToType = ParseFileTable(fileTablePtr, out isCompressedFileTable);
            List<KeyValuePair<int, string>> orderedTable = fileTablePtrToType.OrderBy(kv => kv.Key).ToList();
            int startOfFiles = orderedTable[0].Key;

            VerifyNoFilesMissingFromFileTable(orderedTable);
            
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
            if(isCompressedFileTable)
            {
                File.WriteAllBytes($"{outputDir}[0x{fileTablePtr:x6}] File Table (decompressed).bin", FormUnpacker.DecompressUVRMFileTable(fTableBytes));
            }
            Console.WriteLine("File table saved to file.");

            Console.WriteLine("Extracting files...");
            Dictionary<string, int> fileTypeCount = new Dictionary<string, int>();

            Stopwatch consoleOutputStopwatch = new Stopwatch();
            consoleOutputStopwatch.Start();
            int prevFileEnd = startOfFiles;
            for (int i = 0; i < orderedTable.Count; i++)
            {
                KeyValuePair<int, string> kvPair = orderedTable[i];
                int formPtr = kvPair.Key;
                string magicWordInFileTable = kvPair.Value;

                if (formPtr > prevFileEnd)
                {
                    File.WriteAllBytes($"{outputDir}[0x{prevFileEnd:x6}].bin", romBytes.GetSubArray(prevFileEnd, formPtr - prevFileEnd));
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

                prevFileEnd = formPtr + sectionLength;

                if ((i % 100 == 99) || consoleOutputStopwatch.ElapsedMilliseconds > 2000)
                {
                    Console.WriteLine($"{i+1}/{orderedTable.Count} files extracted...");
                    consoleOutputStopwatch.Restart();
                }
            }
            
            if (prevFileEnd < romBytes.Length)
            {
                // Check if there's actually useful data here
                // In most cases it seems like this is just 0x0 until the next address that's a multiple of 16, and then 0xFF from then on until the end of the ROM.
                int expectedStartOfFFs = Next16ByteAlignedAddress(prevFileEnd);
                int curPos = prevFileEnd;
                bool potentiallyInterestingDataPresent = false;
                for(; curPos < expectedStartOfFFs; curPos++)
                {
                    if(romBytes[curPos] != 0x00)
                    {
                        potentiallyInterestingDataPresent = true;
                        break;
                    }
                }

                // Some games fill the remaining with 0xFF, some with 0x00
                byte testByte = romBytes[curPos];
                if (!potentiallyInterestingDataPresent)
                {
                    for (; curPos < romBytes.Length; curPos++)
                    {
                        if (romBytes[curPos] != testByte)
                        {
                            potentiallyInterestingDataPresent = true;
                            break;
                        }
                    }
                }

                if (potentiallyInterestingDataPresent)
                {
                    File.WriteAllBytes($"{outputDir}[0x{expectedStartOfFFs:x6}] Data after all files.bin", romBytes.GetSubArray(expectedStartOfFFs, romBytes.Length - expectedStartOfFFs));
                }
            }

            AsyncWriteHelper.WaitForFilesToFinishWriting();
            Console.WriteLine("All files extracted!");
            Console.WriteLine();

            foreach (KeyValuePair<string, int> kvPair in fileTypeCount.OrderBy(kv => kv.Key))
            {
                Console.WriteLine($"{kvPair.Key}: {kvPair.Value} files");
            }

            // If we're the only process attached to the console, (e.g. if the user drags+drops a file onto the program)
            // then the console will close when the program exits. I'd rather not have this happen since most games will
            // extract far too quickly for the user to read any of the output.
            uint processCount = GetConsoleProcessList(new uint[64], 64);
            if(processCount == 1)
            {
                Console.ReadKey();
            }
        }

        private static int Next16ByteAlignedAddress(int address)
        {
            if (address % 0x10 == 0)
                return address;
            else
                return address + (0x10 - (address % 0x10));
        }

        private static int SearchForFileTable(byte[] romBytes)
        {
            //Find first FORM + UVFT magic word in ROM
            for (int pos = 0; pos < romBytes.Length; pos++)
            {
                if(romBytes.ReadMagicWord(pos) == "FORM" && (romBytes.ReadMagicWord(pos+8) == "UVFT" || romBytes.ReadMagicWord(pos + 8) == "UVRM"))
                {
                    return pos;
                }
            }

            return -1;
        }

        private static Dictionary<int, string> ParseFileTable(int FILE_TABLE_LOCATION, out bool isCompressedFileTable)
        {
            Dictionary<int, string> fileTablePtrToType = new Dictionary<int, string>();

            int fileTableLength = romBytes.ReadInt(FILE_TABLE_LOCATION + 4);
            int startOfFiles = FILE_TABLE_LOCATION + 8 + fileTableLength;
            // Align to 16-byte index
            startOfFiles = Next16ByteAlignedAddress(startOfFiles);

            byte[] fileTable = romBytes.GetSubArray(FILE_TABLE_LOCATION + 8, fileTableLength);

            if(fileTable.ReadMagicWord(0) == "UVFT")
            {
                isCompressedFileTable = false;

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
            }
            else if(fileTable.ReadMagicWord(0) == "UVRM")
            {
                isCompressedFileTable = true;

                byte[] decompressedFileTable = FormUnpacker.DecompressUVRMFileTable(fileTable);
                if(decompressedFileTable.Length % 8 != 0)
                {
                    throw new InvalidOperationException("File table is invalid length (not a multiple of 8)!");
                }
                int curFilePos = startOfFiles;
                for (int i = 0; i < decompressedFileTable.Length; i += 8)
                {
                    string magicWord = decompressedFileTable.ReadMagicWord(i);
                    int formLength = decompressedFileTable.ReadInt(i + 4);

                    if((romBytes.ReadInt(curFilePos + 4) + 8) != formLength)
                    {
                        throw new InvalidDataException("Error parsing file table - length in file table does not match length in file header!");
                    }

                    fileTablePtrToType.Add(curFilePos, magicWord);

                    curFilePos += formLength;
                }
            }
            else
            {
                throw new InvalidOperationException("Failed to parse file table due to unrecognized magic word in header!");
            }

            return fileTablePtrToType;
        }

        private static void VerifyNoFilesMissingFromFileTable(List<KeyValuePair<int, string>> orderedFileTable)
        {
            for(int i = 0; i < orderedFileTable.Count - 1; i++)
            {
                int filePtr = orderedFileTable[i].Key;
                if (romBytes.ReadMagicWord(filePtr) != "FORM")
                {
                    if(orderedFileTable[i].Value != "UVRW")
                    {
                        throw new InvalidOperationException("Found headerless file in file table that wasn't a UVRW file!");
                    }
                    // Skip, there's no way of determining the file size (and we'll just output everything anyway)
                    continue;
                }
                int fileLength = romBytes.ReadInt(filePtr + 4);
                if(filePtr + 8 + fileLength != orderedFileTable[i+1].Key)
                {
                    throw new Exception();
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
