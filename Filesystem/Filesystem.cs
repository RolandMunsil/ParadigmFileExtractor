using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.Filesystem
{
    class Filesystem
    {
        public class File
        {
            public string fileTypeFromFileTable;
            public string fileTypeFromFileHeader;
            public byte[] bytes;
            public int formLocationInROM;

            public File(string fileTypeFromFileTable, string fileTypeFromFileHeader, byte[] bytes, int formLocationInROM)
            {
                this.fileTypeFromFileTable = fileTypeFromFileTable;
                this.fileTypeFromFileHeader = fileTypeFromFileHeader;
                this.bytes = bytes;
                this.formLocationInROM = formLocationInROM;
            }

            public List<(string, byte[])> Sections 
            { 
                get
                {
                    return FormUnpacker.ExtractFileSections(bytes);
                }
            }
        }

        public ICollection<File> AllFiles => filesByLocation.Values;
        public int StartLocationInROM => FileTable.FormEntryLocation;
        public int EndLocationInROM { get; private set; }
        public FileTable FileTable { get; private set; }
        public int FileCount => filesByLocation.Count;

        private Dictionary<int, File> filesByLocation;

        public Filesystem(byte[] romBytes)
        {
            FileTable = FileTable.Get(romBytes);

            filesByLocation = new Dictionary<int, File>();
            foreach(File file in GetAllFiles(FileTable, romBytes))
            {
                filesByLocation[file.formLocationInROM] = file;
            }

            // We know the last file was not raw otherwise the program would have crashed
            File lastFile = filesByLocation.Last().Value;
            EndLocationInROM = lastFile.formLocationInROM + 8 + lastFile.bytes.Length;
        }

        public File GetFile(string fileType, int index)
        {
            int loc = FileTable.GetFileLocationByIndex(fileType, index);
            return loc == -1 ? null : filesByLocation[loc];
        }

        private static IEnumerable<File> GetAllFiles(FileTable fileTable, byte[] romBytes)
        {
            List<(int, string)> orderedTable = fileTable.AllFileLocationsAndMagicWords.OrderBy(tuple => tuple.Item1).ToList();
            int startOfFiles = orderedTable[0].Item1;

            int prevFileEnd = startOfFiles;
            for (int i = 0; i < orderedTable.Count; i++)
            {
                (int formPtr, string magicWordInFileTable) = orderedTable[i];

                if (formPtr != prevFileEnd)
                {
                    throw new Exception();
                    //File.WriteAllBytes($"{outputDir}[0x{prevFileEnd:x6}].bin", romBytes.GetSubArray(prevFileEnd, formPtr - prevFileEnd));
                }

                byte[] data;
                string magicWordInFileHeader;

                if (magicWordInFileTable == "UVRW" && romBytes.ReadMagicWord(formPtr) != "FORM")
                {
                    // This is a raw file, it needs special handling.
                    magicWordInFileHeader = null;

                    prevFileEnd = orderedTable[i + 1].Item1;

                    data = romBytes.Subsection(formPtr, prevFileEnd - formPtr);
                }
                else
                {
                    int fileLength = romBytes.ReadInt32(formPtr + 4);
                    magicWordInFileHeader = romBytes.ReadMagicWord(formPtr + 8);
                    data = romBytes.Subsection(formPtr + 8, fileLength);

                    prevFileEnd = formPtr + 8 + fileLength;
                }

                yield return new File(magicWordInFileTable, magicWordInFileHeader, data, formPtr);
            }
        }
        
    }
}
