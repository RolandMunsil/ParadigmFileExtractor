using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor
{
    static class ByteArrayHelpers
    {
        public static byte[] GetSubArray(this byte[] file, int start, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(file, start, result, 0, length);
            return result;
        }

        public static int ReadInt(this byte[] file, int offset)
        {
            return (file[offset] << 24) | (file[offset+1] << 16) | (file[offset+2] << 8) | file[offset+3];
        }

        public static string ReadMagicWord(this byte[] file, int offset)
        {
            return Encoding.ASCII.GetString(file, offset, 4);
        }
    }
}
