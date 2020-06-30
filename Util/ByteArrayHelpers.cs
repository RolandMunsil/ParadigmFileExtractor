using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor
{
    static class ByteArrayHelpers
    {
        public static byte[] Subsection(this byte[] file, int start, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(file, start, result, 0, length);
            return result;
        }

        public static short ReadInt16(this byte[] file, int offset)
        {
            return (short)((file[offset] << 8) | file[offset + 1]);
        }

        public static ushort ReadUInt16(this byte[] file, int offset)
        {
            return (ushort)ReadInt16(file, offset);
        }

        public static int ReadInt32(this byte[] file, int offset)
        {
            return (file[offset] << 24) | (file[offset + 1] << 16) | (file[offset + 2] << 8) | file[offset + 3];
        }

        public static uint ReadUInt32(this byte[] file, int offset)
        {
            return (uint)ReadInt32(file, offset);
        }

        public static long ReadInt64(this byte[] file, int offset)
        {
            return (((long)ReadUInt32(file, offset)) << 32) | ReadUInt32(file, offset + 4);
        }

        public static ulong ReadUInt64(this byte[] file, int offset)
        {
            return (ulong)ReadInt64(file, offset);
        }

        public static float ReadFloat32(this byte[] file, int offset)
        {
            byte[] endianFixed = { file[offset + 3], file[offset + 2], file[offset + 1], file[offset + 0] };
            return BitConverter.ToSingle(endianFixed, 0);
            //return BitConverter.Int32BitsToSingle(ReadInt32(file, offset));
        }

        public static string ReadMagicWord(this byte[] file, int offset)
        {
            return Encoding.ASCII.GetString(file, offset, 4);
        }

        public static uint Bits(this uint word, int bitsOffset, int bitCount)
        {
            uint mask = (uint)(1 << bitCount) - 1;

            return (word >> bitsOffset) & mask;
        }

        public static bool Bit(this uint word, int bitsOffset)
        {
            uint bit = Bits(word, bitsOffset, 1);
            if (bit != 0 && bit != 1)
            {
                throw new Exception();
            }
            return bit == 1;
        }

        public static ulong Bits(this ulong word, int bitsOffset, int bitCount)
        {
            ulong mask = ((ulong)1 << bitCount) - 1;

            return (word >> bitsOffset) & mask;
        }

        public static bool Bit(this ulong word, int bitsOffset)
        {
            ulong bit = Bits(word, bitsOffset, 1);
            if (bit != 0 && bit != 1)
            {
                throw new Exception();
            }
            return bit == 1;
        }

        public static float[] AsFloats(this byte[] data)
        {
            if (data.Length % 4 != 0)
                throw new InvalidOperationException();

            float[] floats = new float[data.Length / 4];
            for (int i = 0; i < data.Length / 4; i++)
            {
                floats[i] = ReadFloat32(data, i * 4);
            }
            return floats;
        }

        public static IEnumerable<byte[]> InGroupsOf(this byte[] data, int bytesPerGroup)
        {
            if (data.Length % bytesPerGroup != 0)
                throw new InvalidOperationException();

            int pos = 0;
            while (pos < data.Length)
            {
                yield return data.Subsection(pos, bytesPerGroup);
                pos += bytesPerGroup;
            }
        }

        public static string PrettyPrint(this byte[] bytes)
        {
            return String.Join(" ", bytes.Select(b => b.ToString("X2")));
        }
    }
}
