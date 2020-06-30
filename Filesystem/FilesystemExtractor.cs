using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.Filesystem
{
    public class FilesystemExtractor
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

        const string RAW_FILETYPE_DIR = "_raw";
        const string RAW_SUBDIR = "Original/";
        const string UNPACKED_SUBDIR = "Unpacked/";

        public static void ExtractToFolder(byte[] romBytes, string outputDir)
        {
            Console.WriteLine("Parsing filesystem...");
            Filesystem filesystem = new Filesystem(romBytes);

            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(outputDir + RAW_SUBDIR);
            Directory.CreateDirectory(outputDir + UNPACKED_SUBDIR);

            //Save off bits before file table
            File.WriteAllBytes(outputDir + "[0x00000] Data before file table.bin", romBytes.Subsection(0, filesystem.StartLocationInROM));
            Console.WriteLine("Section of ROM that's before file table saved to file.");

            //Save file table
            File.WriteAllBytes($"{outputDir}[0x{filesystem.FileTable.FormEntryLocation:x6}] File Table.bin", filesystem.FileTable.RawTableBytes);
            if (filesystem.FileTable.Type == FileTable.FileTableType.FlightGame)
            {
                File.WriteAllBytes($"{outputDir}[0x{filesystem.FileTable.FormEntryLocation:x6}] File Table (decompressed).bin", filesystem.FileTable.DecompressedTableBytes);
            }
            Console.WriteLine("File table saved to file.");

            Console.WriteLine("Extracting files...");
            Dictionary<string, int> fileTypeCount = new Dictionary<string, int>();

            Stopwatch consoleOutputStopwatch = new Stopwatch();
            consoleOutputStopwatch.Start();

            int fCt = 0;
            foreach (Filesystem.File file in filesystem.AllFiles)
            {
                string outputSubfolder;
                string fileExtension;

                if(file.fileTypeFromFileTable == "UVRW")
                {
                    // special handling for raw data
                    outputSubfolder = RAW_FILETYPE_DIR;
                    if(file.fileTypeFromFileHeader == null)
                    {
                        fileExtension = "_headerless_file";
                    }
                    else
                    {
                        fileExtension = file.fileTypeFromFileHeader;
                    }
                }
                else
                {
                    outputSubfolder = GetNiceFileType(file.fileTypeFromFileTable, file.fileTypeFromFileHeader);
                    fileExtension = outputSubfolder;
                }

                SaveUnmodifiedFile(file, outputDir, outputSubfolder, fileExtension);
                SaveFileSections(file, outputDir, outputSubfolder, fileExtension);

                if ((fCt++ % 100 == 99) || consoleOutputStopwatch.ElapsedMilliseconds > 2000)
                {
                    Console.WriteLine($"{fCt}/{filesystem.FileCount} files extracted...");
                    consoleOutputStopwatch.Restart();
                }
            }

            if (filesystem.EndLocationInROM < romBytes.Length)
            {
                // Check if there's actually useful data here
                // In most cases it seems like this is just 0x0 until the next address that's a multiple of 16, and then 0xFF from then on until the end of the ROM.
                int expectedStartOfFFs = Next16ByteAlignedAddress(filesystem.EndLocationInROM);
                int curPos = filesystem.EndLocationInROM;
                bool potentiallyInterestingDataPresent = false;
                for (; curPos < expectedStartOfFFs; curPos++)
                {
                    if (romBytes[curPos] != 0x00)
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
                    File.WriteAllBytes($"{outputDir}[0x{expectedStartOfFFs:x6}] Data after all files.bin", romBytes.Subsection(expectedStartOfFFs, romBytes.Length - expectedStartOfFFs));
                }
            }

            AsyncWriteHelper.WaitForFilesToFinishWriting();
            Console.WriteLine("All files extracted!");
            Console.WriteLine();

            foreach ((string fileType, int count) in filesystem.AllFiles.GroupBy(file => file.fileTypeFromFileTable).Select(grp => (grp.Key, grp.Count())).OrderBy(tuple => tuple.Key))
            {
                Console.WriteLine($"{fileType}: {count} files");
            }
        }

        private static void SaveFileSections(Filesystem.File file, string filesystemOutputDir, string outputSubfolder, string fileExtension)
        {
            string directoryPath = $"{filesystemOutputDir}{UNPACKED_SUBDIR}{outputSubfolder}/";
            Directory.CreateDirectory(directoryPath);

            string extraNameInfo = "";
            if (file.fileTypeFromFileTable == "UVMO")
            {
                byte[] mdbgSection = file.Sections.Single(tuple => tuple.Item1 == "MDBG").Item2;
                string name = Encoding.ASCII.GetString(mdbgSection).Replace("\0", "");
                extraNameInfo = " " + name;
            }

            string outputName = $"[0x{file.formLocationInROM:x6}]{extraNameInfo}";

            if (file.fileTypeFromFileHeader == null) // This is a UVRW file with no header
            {
                // Save file as-is
                AsyncWriteHelper.WriteAllBytes($"{directoryPath}{outputName}.{fileExtension}", file.bytes);
            }
            else
            {
                if (file.Sections.Count == 1)
                {
                    AsyncWriteHelper.WriteAllBytes($"{directoryPath}{outputName}.{fileExtension}", file.Sections[0].Item2);
                }
                else
                {
                    string unpackDir = directoryPath + outputName + "/";
                    Directory.CreateDirectory(unpackDir);
                    for (int i = 0; i < file.Sections.Count; i++)
                    {
                        (string sectionType, byte[] sectionData) = file.Sections[i];
                        sectionType = sectionType.Replace('.', '_');
                        AsyncWriteHelper.WriteAllBytes($"{unpackDir}{i + 1:d3}.{sectionType}", sectionData);
                    }
                }
            }
        }

        private static void SaveUnmodifiedFile(Filesystem.File file, string filesystemOutputDir, string outputSubfolder, string fileExtension)
        {
            string directoryPath = $"{filesystemOutputDir}{RAW_SUBDIR}{outputSubfolder}/";
            Directory.CreateDirectory(directoryPath);

            string extraNameInfo = "";
            if (file.fileTypeFromFileTable == "UVMO")
            {
                byte[] mdbgSection = file.Sections.Single(tuple => tuple.Item1 == "MDBG").Item2;
                string name = Encoding.ASCII.GetString(mdbgSection).Replace("\0", "");
                extraNameInfo = " " + name;
            }

            string outputName = $"[0x{file.formLocationInROM:x6}]{extraNameInfo}";

            AsyncWriteHelper.WriteAllBytes($"{directoryPath}{outputName}.{fileExtension}", file.bytes);
        }

        private static string GetNiceFileType(string magicWordInFileTable, string magicWordInFileHeader)
        {
            string magicWord = magicWordInFileTable;

            if (magicWordInFileHeader != magicWordInFileTable)
            {
                magicWord += "/" + magicWordInFileHeader;
            }

            return magicWordToFolderName[magicWord];
        }

        public static int Next16ByteAlignedAddress(int address)
        {
            if (address % 0x10 == 0)
                return address;
            else
                return address + (0x10 - (address % 0x10));
        }

        //private static void SaveHeaderlessFile(int formPtr, int sectionLength, byte[] romBytes, string outputDir)
        //{
        //    byte[] headerlessFile = romBytes.GetSubArray(formPtr, sectionLength);
        //    string outputName = $"[0x{formPtr:x6}]";

        //    Directory.CreateDirectory($"{outputDir}{RAW_SUBDIR}{RAW_FILETYPE_DIR}/");
        //    AsyncWriteHelper.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{RAW_FILETYPE_DIR}/{outputName}.headerless_file", headerlessFile);

        //    string filetypeDir = $"{outputDir}{UNPACKED_SUBDIR}{RAW_FILETYPE_DIR}/";
        //    Directory.CreateDirectory(filetypeDir);
        //    AsyncWriteHelper.WriteAllBytes($"{filetypeDir}{outputName}.headerless_file", headerlessFile);
        //}

        //public static int SaveFormToFile(int formPtr, string niceFileType, byte[] romBytes, string outputDir, string override_outputTypeDir = null)
        //{
        //    if (romBytes.ReadMagicWord(formPtr) != "FORM")
        //    {
        //        throw new InvalidOperationException("Tried to read a form where there wasn't any!");
        //    }

        //    int formLength = romBytes.ReadInt(formPtr + 4);
        //    byte[] formBytes = romBytes.GetSubArray(formPtr + 8, formLength);


        //    string extraNameInfo = "";
        //    if (niceFileType == "modu")
        //    {
        //        int namePtr = 8 + Encoding.ASCII.GetString(formBytes).IndexOf("MDBG");
        //        string name = Encoding.ASCII.GetString(formBytes, namePtr, 0x20).Replace("\0", "");
        //        extraNameInfo = " " + name;
        //    }
        //    string outputName = $"[0x{formPtr:x6}]{extraNameInfo}";
        //    string outputTypeDir = override_outputTypeDir ?? niceFileType;

        //    Directory.CreateDirectory($"{outputDir}{RAW_SUBDIR}{outputTypeDir}/");
        //    AsyncWriteHelper.WriteAllBytes($"{outputDir}{RAW_SUBDIR}{outputTypeDir}/{outputName}.{niceFileType}", formBytes);

        //    string filetypeDir = $"{outputDir}{UNPACKED_SUBDIR}{outputTypeDir}/";
        //    Directory.CreateDirectory(filetypeDir);
        //    FormUnpacker.UnpackFile(formBytes, $"{filetypeDir}{outputName}/", $"{filetypeDir}{outputName}.{niceFileType}");

        //    return formLength;
        //}
    }
}
