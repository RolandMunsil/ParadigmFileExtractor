using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;
using BARExtractor;

namespace PeepsCompress
{
    static class MIO0
    {
        public static byte[] Decompress(int offset, byte[] data)
        {
            MemoryStream inputFile = new MemoryStream(data);

            List<byte> newFile = new List<byte>();

            string magicNumber = data.ReadMagicWord(offset + 0);

            if (magicNumber == "MIO0")
            {
                int decompressedLength = data.ReadInt(offset + 4);
                int compressedOffset = data.ReadInt(offset + 8) + offset;
                int uncompressedOffset = data.ReadInt(offset + 12) + offset;
                int currentOffset;

                inputFile.Seek(offset + 16, SeekOrigin.Begin);

                while (newFile.Count < decompressedLength)
                {

                    byte bits = (byte)inputFile.ReadByte(); //byte of layout bits
                    BitArray arrayOfBits = new BitArray(new byte[1] { bits });

                    for (int i = 7; i > -1 && (newFile.Count < decompressedLength); i--) //iterate through layout bits
                    {

                        if (arrayOfBits[i] == true)
                        {
                            //non-compressed
                            //add one byte from uncompressedOffset to newFile

                            currentOffset = (int)inputFile.Position;

                            inputFile.Seek(uncompressedOffset, SeekOrigin.Begin);

                            newFile.Add((byte)inputFile.ReadByte());
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

                            byte byte1 = (byte)inputFile.ReadByte();
                            byte byte2 = (byte)inputFile.ReadByte();
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

