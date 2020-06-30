using ParadigmFileExtractor.Filesystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.Filesystem
{
    public class FileTable
    {
        public enum FileTableType
        {
            Normal,
            FlightGame // compressed
        }

        public FileTableType Type { get; private set; }
        public int FormEntryLocation { get; private set; }
        public byte[] RawTableBytes { get; private set; }

        // only valid if type = FlightGame
        public byte[] DecompressedTableBytes { get; private set; }

        private Dictionary<string, List<int>> magicWordToFileLocations;


        public IEnumerable<(int, string)> AllFileLocationsAndMagicWords
        {
            get
            {
                return from KeyValuePair<string, List<int>> kv in magicWordToFileLocations
                       from int loc in kv.Value
                       where loc != -1
                       select (loc, kv.Key);
            }
        }

        public int GetFileLocationByIndex(string filetype, int index)
        {
            return magicWordToFileLocations[filetype][index];
        }



        public static FileTable Get(byte[] rom)
        {
            return new FileTable(rom);
        }

        private FileTable(byte[] rom)
        {
            (FormEntryLocation, Type) = SearchForFileTable(rom);

            int fileTableLength = rom.ReadInt(FormEntryLocation + 4);
            RawTableBytes = rom.GetSubArray(FormEntryLocation + 8, fileTableLength);
            ParseFileTable();
        }

        private static (int, FileTableType) SearchForFileTable(byte[] romBytes)
        {
            //Find first FORM + UVFT magic word in ROM
            for (int pos = 0; pos < romBytes.Length; pos++)
            {
                string magicWord = romBytes.ReadMagicWord(pos + 8);
                if (romBytes.ReadMagicWord(pos) == "FORM" && (magicWord == "UVFT" || magicWord == "UVRM"))
                {
                    FileTableType fileTableType = magicWord == "UVFT" ? FileTableType.Normal : FileTableType.FlightGame;
                    return (pos, fileTableType);
                }
            }

            throw new ArgumentException("Could not find file table in ROM");
        }

        private void ParseFileTable()
        {
            magicWordToFileLocations = new Dictionary<string, List<int>>();

            Action<string, int> addToDict = (magicWord, location) =>
            {
                if (!magicWordToFileLocations.ContainsKey(magicWord))
                    magicWordToFileLocations.Add(magicWord, new List<int>());
                magicWordToFileLocations[magicWord].Add(location);
            };

            int startOfFiles = FormEntryLocation + 8 + RawTableBytes.Length;
            // Align to 16-byte index
            startOfFiles = FilesystemExtractor.Next16ByteAlignedAddress(startOfFiles);

            if (this.Type == FileTableType.Normal)
            {
                // Read the file table
                int curFileTablePos = 4; // Start at 4 to skip magic word
                while (curFileTablePos < RawTableBytes.Length)
                {
                    // Read the magic word and length
                    string fileType = RawTableBytes.ReadMagicWord(curFileTablePos);
                    int sectionLength = RawTableBytes.ReadInt(curFileTablePos + 4);
                    curFileTablePos += 8;
                    // Read the section
                    for (int sectionPos = 0; sectionPos < sectionLength; sectionPos += 4)
                    {
                        int fileOffset = RawTableBytes.ReadInt(curFileTablePos + sectionPos);
                        if (fileOffset == -1)
                        {
                            addToDict(fileType, -1);
                        }
                        else
                        {
                            addToDict(fileType, startOfFiles + fileOffset);
                        }
                    }
                    curFileTablePos += sectionLength;
                }
            }
            else // this.type == Type.FlightGame
            {
                DecompressedTableBytes = FormUnpacker.DecompressUVRMFileTable(RawTableBytes);
                if (DecompressedTableBytes.Length % 8 != 0)
                {
                    throw new InvalidOperationException("File table is invalid length (not a multiple of 8)!");
                }
                int curFilePos = startOfFiles;
                for (int i = 0; i < DecompressedTableBytes.Length; i += 8)
                {
                    string magicWord = DecompressedTableBytes.ReadMagicWord(i);
                    int formLength = DecompressedTableBytes.ReadInt(i + 4);

                    //if ((romBytes.ReadInt(curFilePos + 4) + 8) != formLength)
                    //{
                    //    throw new InvalidDataException("Error parsing file table - length in file table does not match length in file header!");
                    //}

                    addToDict(magicWord, curFilePos);

                    curFilePos += formLength;
                }
            }
        }
    }
}
