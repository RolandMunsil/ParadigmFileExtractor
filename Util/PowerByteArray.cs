using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.Util
{
    public class PowerByteArray: IList<byte>
    {
        public int Position { get; set; }
        byte[] bytes;

        public PowerByteArray(byte[] bytes)
        {
            this.bytes = bytes;
            this.Position = 0;
        }




        #region Indexed Reads
        public sbyte ReadS8(int offset)
        {
            return (sbyte)bytes[offset];
        }

        public byte ReadU8(int offset)
        {
            return bytes[offset];
        }

        public short ReadS16(int offset)
        {
            return (short)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        public ushort ReadU16(int offset)
        {
            return (ushort)ReadS16(offset);
        }

        public int ReadS32(int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        public uint ReadU32(int offset)
        {
            return (uint)ReadS32(offset);
        }

        public long ReadS64(int offset)
        {
            return (((long)ReadU32(offset)) << 32) | ReadU32(offset + 4);
        }

        public ulong ReadU64(int offset)
        {
            return (ulong)ReadS64(offset);
        }

        public float ReadFloat(int offset)
        {
            byte[] endianFixed = { bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset + 0] };
            return BitConverter.ToSingle(endianFixed, 0);
        }

        public string ReadMagicWord(int offset)
        {
            return Encoding.ASCII.GetString(bytes, offset, 4);
        }

        public PowerByteArray SubArray(int start, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(bytes, start, result, 0, length);
            return new PowerByteArray(result);
        }
        #endregion

        #region 'Next' Reads
        public sbyte NextS8()
        {
            sbyte returnValue = ReadS8(Position);
            Position += sizeof(sbyte);
            return returnValue;
        }
        public byte NextU8()
        {
            byte returnValue = ReadU8(Position);
            Position += sizeof(byte);
            return returnValue;
        }
        public short NextS16()
        {
            short returnValue = ReadS16(Position);
            Position += sizeof(short);
            return returnValue;
        }
        public ushort NextU16()
        {
            ushort returnValue = ReadU16(Position);
            Position += sizeof(ushort);
            return returnValue;
        }
        public int NextS32()
        {
            int returnValue = ReadS32(Position);
            Position += sizeof(int);
            return returnValue;
        }
        public uint NextU32()
        {
            uint returnValue = ReadU32(Position);
            Position += sizeof(uint);
            return returnValue;
        }
        public long NextS64()
        {
            long returnValue = ReadS64(Position);
            Position += sizeof(long);
            return returnValue;
        }
        public ulong NextU64()
        {
            ulong returnValue = ReadU64(Position);
            Position += sizeof(ulong);
            return returnValue;
        }
        public float NextFloat()
        {
            float returnValue = ReadFloat(Position);
            Position += sizeof(float);
            return returnValue;
        }
        public string NextMagicWord()
        {
            string returnValue = ReadMagicWord(Position);
            Position += returnValue.Length;
            return returnValue;
        }
        public PowerByteArray NextSubArray(int length)
        {
            PowerByteArray returnValue = SubArray(Position, length);
            Position += returnValue.Length;
            return returnValue;
        }
        #endregion

        public float[] AsFloats()
        {
            if (Length % 4 != 0)
                throw new InvalidOperationException();

            float[] floats = new float[Length / 4];
            for (int i = 0; i < Length / 4; i++)
            {
                floats[i] = ReadFloat(i * 4);
            }
            return floats;
        }

        public IEnumerable<PowerByteArray> InGroupsOf(int bytesPerGroup)
        {
            if (Length % bytesPerGroup != 0)
                throw new InvalidOperationException();

            int pos = 0;
            while (pos < Length)
            {
                yield return SubArray(pos, bytesPerGroup);
                pos += bytesPerGroup;
            }
        }

        public string PrettyPrint()
        {
            return String.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

        public override string ToString()
        {
            return PrettyPrint();
        }

        #region Basic Reimplimentation of Array & IList methods
        public byte this[int index] 
        { 
            get => bytes[index]; 
            set => bytes[index] = value; 
        }

        public int Count => bytes.Length;
        public int Length => bytes.Length;
        public bool IsReadOnly => false;



        public bool Contains(byte item)
        {
            return bytes.Contains(item);
        }

        public void CopyTo(byte[] array, int arrayIndex)
        {
            bytes.CopyTo(array, arrayIndex);
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return ((IList<byte>)bytes).GetEnumerator();
        }

        public int IndexOf(byte item)
        {
            return ((IList<byte>)bytes).IndexOf(item);
        }
        #endregion

        #region Unsupported Operations
        IEnumerator IEnumerable.GetEnumerator()
        {
            return bytes.GetEnumerator();
        }

        public void Add(byte item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public void Insert(int index, byte item)
        {
            throw new NotSupportedException();
        }

        public bool Remove(byte item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}
