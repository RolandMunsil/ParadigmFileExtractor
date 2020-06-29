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
        // Note: files with a mismatch between file table type and type in header will have it
        // concatenated to <headertype>/<filetabletype>

        //TODO: maybe setup auto-filename generation for the ones without a special name
        static Dictionary<string, string> magicWordToFolderName = new Dictionary<string, string>
        {
            {"    ",      "BLANK_FILETYPE" }, // Weird filetype in AeroFighters Assault
            {"3VUE",      "3VUE"}, // couldn't find any loader code
            {"ADAT",      "ADAT"}, // couldn't find any loader code
            {"CNMA",      "cinema"},
            {"DEMO",      "DEMO"}, // couldn't find any loader code
            {"FTKL",      "itrack"},
            {"LART",      "LART"}, // couldn't find any loader code
            {"PDAT",      "PDAT"}, // couldn't find any loader code
            {"SDOC",      "SDOC"}, // couldn't find any loader code
            {"SHAN",      "SHAN"}, // couldn't find any loader code
            {"SLAN",      "SLAN"}, // couldn't find any loader code
            {"SPTH",      "SPTH"}, // couldn't find any loader code
            {"SRED",      "SRED"}, // couldn't find any loader code
            {"SSHT",      "SSHT"}, // couldn't find any loader code
            {"STRY",      "f1story"},
            {"Trai",      "Trai"}, // Weird filetype in AeroFighters Assault
            {"UPWL",      "UPWL"}, // couldn't find any loader code
            {"UPWT",      "UPWT"}, // couldn't find any loader code
            {"UVAN",      "janim"},
            {"UVBT",      "blit"},
            {"UVCT",      "contour"},
            {"UVDS",      "dset"},
            {"UVEN",      "env"},
            {"UVFT",      "font"},
            {"UVLT",      "UVLT"}, // couldn't find any loader code
            {"UVLV",      "UVLV"}, // couldn't find any loader code
            {"UVMB",      "UVMB"}, // couldn't find any loader code
            {"UVMD",      "uvmodel"},
            {"UVMO/MODU", "MODU"}, // couldn't find any loader code
            {"UVMS",      "UVMS"}, // couldn't find any loader code
            {"UVPX",      "pfx"},
            {"UVSX",      "UVSX"}, // couldn't find any loader code
            {"UVSY",      "UVSY"}, // couldn't find any loader code
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

            Console.WriteLine("Parsing file table...");
            FileTable fileTable = FileTable.Get(romBytes);
            List<(int, string)> orderedTable = fileTable.AllFileLocationsAndMagicWords.OrderBy(tuple => tuple.Item1).ToList();
            int startOfFiles = orderedTable[0].Item1;

            VerifyNoFilesMissingFromFileTable(orderedTable);
            
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(outputDir + RAW_SUBDIR);
            Directory.CreateDirectory(outputDir + UNPACKED_SUBDIR);

            //Save off bits before file table
            File.WriteAllBytes(outputDir + "[0x00000] Data before file table.bin", romBytes.GetSubArray(0, fileTable.FormEntryLocation));
            Console.WriteLine("Section of ROM that's before file table saved to file.");

            //Save file table
            File.WriteAllBytes($"{outputDir}[0x{fileTable.FormEntryLocation:x6}] File Table.bin", fileTable.RawTableBytes);
            if(fileTable.Type == FileTable.FileTableType.FlightGame)
            {
                File.WriteAllBytes($"{outputDir}[0x{fileTable.FormEntryLocation:x6}] File Table (decompressed).bin", fileTable.DecompressedTableBytes);
            }
            Console.WriteLine("File table saved to file.");

            Console.WriteLine("Extracting files...");
            Dictionary<string, int> fileTypeCount = new Dictionary<string, int>();

            Stopwatch consoleOutputStopwatch = new Stopwatch();
            consoleOutputStopwatch.Start();
            int prevFileEnd = startOfFiles;
            for (int i = 0; i < orderedTable.Count; i++)
            {
                (int, string) kvPair = orderedTable[i];
                int formPtr = kvPair.Item1;
                string magicWordInFileTable = kvPair.Item2;

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
                        sectionLength = orderedTable[i + 1].Item1 - formPtr;
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

        public static int Next16ByteAlignedAddress(int address)
        {
            if (address % 0x10 == 0)
                return address;
            else
                return address + (0x10 - (address % 0x10));
        }

        
        private static void VerifyNoFilesMissingFromFileTable(List<(int, string)> orderedFileTable)
        {
            for(int i = 0; i < orderedFileTable.Count - 1; i++)
            {
                int filePtr = orderedFileTable[i].Item1;
                if (romBytes.ReadMagicWord(filePtr) != "FORM")
                {
                    if(orderedFileTable[i].Item2 != "UVRW")
                    {
                        throw new InvalidOperationException("Found headerless file in file table that wasn't a UVRW file!");
                    }
                    // Skip, there's no way of determining the file size (and we'll just output everything anyway)
                    continue;
                }
                int fileLength = romBytes.ReadInt(filePtr + 4);
                if(filePtr + 8 + fileLength != orderedFileTable[i+1].Item1)
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
