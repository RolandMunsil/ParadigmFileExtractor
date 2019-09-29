using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;

namespace PeepsCompress
{
    class MIO0
    {

        public byte[] decompress(int offset, FileStream inputFile)
        {
            BigEndianBinaryReader br = new BigEndianBinaryReader(inputFile)

            List<byte> newFile = new List<byte>();

            string magicNumber = Encoding.ASCII.GetString(br.ReadBytes(4));

            if (magicNumber == "MIO0")
            {
                int decompressedLength = br.ReadInt32();
                int compressedOffset = br.ReadInt32() + offset;
                int uncompressedOffset = br.ReadInt32() + offset;
                int currentOffset;

                while (newFile.Count < decompressedLength)
                {

                    byte bits = br.ReadByte(); //byte of layout bits
                    BitArray arrayOfBits = new BitArray(new byte[1] { bits });

                    for (int i = 7; i > -1 && (newFile.Count < decompressedLength); i--) //iterate through layout bits
                    {

                        if (arrayOfBits[i] == true)
                        {
                            //non-compressed
                            //add one byte from uncompressedOffset to newFile

                            currentOffset = (int)inputFile.Position;

                            inputFile.Seek(uncompressedOffset, SeekOrigin.Begin);

                            newFile.Add(br.ReadByte());
                            uncompressedOffset++;

                            inputFile.Seek(currentOffset, SeekOrigin.Begin);

                        }
                        else
                        {
                            //compressed
                            //read 2 bytes
                            //4 bits = length
                            //12 bits = offset

                            currentOffset = (int)inputFile.Position;
                            inputFile.Seek(compressedOffset, SeekOrigin.Begin);

                            byte byte1 = br.ReadByte();
                            byte byte2 = br.ReadByte();
                            compressedOffset += 2;

                            //Note: For Debugging, binary representations can be printed with:  Convert.ToString(numberVariable, 2);

                            byte byte1Upper = (byte)((byte1 & 0x0F));//offset bits
                            byte byte1Lower = (byte)((byte1 & 0xF0) >> 4); //length bits

                            int combinedOffset = ((byte1Upper << 8) | byte2);

                            int finalOffset = 1 + combinedOffset;
                            int finalLength = 3 + byte1Lower;

                            for (int k = 0; k < finalLength; k++) //add data for finalLength iterations
                            {
                                newFile.Add(newFile[newFile.Count - finalOffset]); //add byte at offset (fileSize - finalOffset) to file
                            }

                            inputFile.Seek(currentOffset, SeekOrigin.Begin); //return to layout bits

                        }
                    }
                }

                inputFile.Close();

                return newFile.ToArray();
            }
            else
            {
                throw new Exception("This is not an MIO0 file.");
            }
        }
    }

}

