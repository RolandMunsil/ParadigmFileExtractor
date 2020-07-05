using ParadigmFileExtractor.Util;
using ParadigmFileExtractor.UVMD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.Common
{
    public class ThreeD
    {
        public struct Vertex
        {
            public short x;
            public short y;
            public short z;
            public ushort index;
            public ushort unk1;
            public ushort unk2;
            public byte colorR;
            public byte colorG;
            public byte colorB;
            public byte colorA;

            public Vertex(PowerByteArray bytes16)
            {
                x = bytes16.ReadS16(0);
                y = bytes16.ReadS16(2);
                z = bytes16.ReadS16(4);
                index = bytes16.ReadU16(6);
                unk1 = bytes16.ReadU16(8);
                unk2 = bytes16.ReadU16(10);
                colorR = bytes16[12];
                colorG = bytes16[13];
                colorB = bytes16[14];
                colorA = bytes16[15];
            }

            public override string ToString()
            {
                return $"({x}, {y}, {z}) [{index}] {(short)unk1 / (float)0b100000} {(short)unk2 / (float)0b100000} 0x{colorR:X2}{colorG:X2}{colorB:X2}{colorA:X2}";
            }
        }

        public struct Matrix
        {
            float[] elements;

            public float this[int index]
            {
                get => elements[index];
                set => elements[index] = value;
            }

            public static Matrix Identity => new Matrix(new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 });

            public Matrix(float[] elements)
            {
                if (elements.Length != 16)
                {
                    throw new Exception();
                }

                this.elements = elements;
            }

            public override string ToString()
            {
                float[] e = elements;
                return $"┌{e[0]:G5}, {e[4]:G5}, {e[8]:G5}, {e[12]:G5}┐\n"
                    + $"│{e[1]:G5}, {e[5]:G5}, {e[9]:G5}, {e[13]:G5}│\n"
                    + $"│{e[2]:G5}, {e[6]:G5}, {e[10]:G5}, {e[14]:G5}│\n"
                    + $"└{e[3]:G5}, {e[7]:G5}, {e[11]:G5}, {e[15]:G5}┘\n";
            }
        }

        public struct RSPCommand
        {
            public uint uint1;
            public uint uint2;

            public static RSPCommand CreateFromShorts(ushort u1, ushort u2)
            {
                RSPCommand cmd = new RSPCommand();
                if (u2 != 0)
                {
                    cmd.uint1 = ((uint)(((uint)(u1 & 0x7c00) >> 10) << 0x11) | (uint)(((uint)(u1 & 0x3e0) >> 5) << 9) | (uint)((u1 & 0x1f) << 1) | (uint)0x6000000);
                    cmd.uint2 = ((uint)(((uint)(u2 & 0x7c00) >> 10) << 0x11) | (uint)(((uint)(u2 & 0x3e0) >> 5) << 9) | (uint)((u2 & 0x1f) << 1));
                }
                else
                {
                    cmd.uint1 = ((uint)(((uint)(u1 & 0x7c00) >> 10) << 0x11) | (uint)(((uint)(u1 & 0x3e0) >> 5) << 9) | (uint)((u1 & 0x1f) << 1) | (uint)0x5000000);
                    cmd.uint2 = 0;
                }
                return cmd;
            }

            public override string ToString()
            {
                return $"{uint1:X8} {uint2:X8}";
            }
        }

        // The RSP commands load vertices read them from a location in RAM
        // I don't want to have to simulate the actual process of placing the vertices in RAM,
        // and we know that each model is only ever going to use it's own vertices. So we'll
        // just pretend all vertex buffers have the same address to make interpreting commands
        // easier.
        static uint FAKE_VERTICES_LOCATION_IN_RAM = 0x80000000;

        public static RSPCommand[] UnpackTriangleCommands(PowerByteArray fileBytes, ushort shortsCount, ushort commandCount)
        {
            ushort prevUnconvertedTriangle = 0;
            int ctZero = 0;
            RSPCommand[] commands = new RSPCommand[commandCount];
            for (int l = 0; l < shortsCount; l++)
            {
                ushort nextShort = fileBytes.NextU16();

                if ((nextShort & 0x8000) == 0x8000)
                {
                    if (prevUnconvertedTriangle == 0)
                    {
                        prevUnconvertedTriangle = nextShort;
                    }
                    else
                    {
                        commands[ctZero] = RSPCommand.CreateFromShorts(prevUnconvertedTriangle, nextShort);
                        prevUnconvertedTriangle = 0;
                        ctZero++;
                    }
                }
                else
                {

                    if (prevUnconvertedTriangle != 0)
                    {
                        commands[ctZero] = RSPCommand.CreateFromShorts(prevUnconvertedTriangle, 0);
                        prevUnconvertedTriangle = 0;
                        ctZero++;
                    }

                    byte b = fileBytes.NextU8();

                    uint numVerts = 1 + (uint)(((nextShort & 0x6000) >> 10) | ((b & 0xE0) >> 5));
                    uint vbidx = (uint)(b & 0x1F);
                    uint nn = (numVerts & 0xFF) << 12;
                    uint aa = ((vbidx + numVerts) & 0x7F) << 1;
                    uint vertLoadStartIdx = (uint)(nextShort & 0x1FFF);

                    uint commandUpper = 0x01000000 | nn | aa;
                    // s7 (replaced by FAKE_VERTICES_LOCATION_IN_RAM) at this point will be a pointer to the previously loaded set of vertices
                    uint commandLower = (vertLoadStartIdx * 16) + (FAKE_VERTICES_LOCATION_IN_RAM - 0x80000000);

                    commands[ctZero].uint1 = commandUpper;
                    commands[ctZero].uint2 = commandLower;
                    ctZero++;
                }
            }

            if (prevUnconvertedTriangle != 0)
            {
                commands[ctZero] = RSPCommand.CreateFromShorts(prevUnconvertedTriangle, 0);
                prevUnconvertedTriangle = 0;
                ctZero++;
            }

            // Add an ENDDL
            commands[ctZero] = new RSPCommand
            {
                uint1 = 0xDF000000,
                uint2 = 0x00000000
            };
            ctZero++;

            if (ctZero != commands.Length)
            {
                throw new Exception();
            }

            return commands;
        }

        public static List<Vertex> MaterialToVertexData(Vertex[] vertices, RSPCommand[] triEntries, Matrix matrix)
        {
            Dictionary<uint, Vertex> rspVertexBuffer = new Dictionary<uint, Vertex>();
            List<Vertex> triangles = new List<Vertex>();

            foreach (RSPCommand cmd in triEntries)
            {
                uint opcode = cmd.uint1 >> 24;
                if (opcode == 0x01)
                {
                    uint numVerts = (cmd.uint1 >> 12) & 0xFF;
                    uint destIdx = ((cmd.uint1 & 0xFF) >> 1) - numVerts;
                    uint srcIdx = cmd.uint2 / 16;
                    for (uint v = 0; v < numVerts; v++)
                    {
                        rspVertexBuffer[destIdx + v] = vertices[srcIdx + v];
                    }
                }
                else if (opcode == 0x05)
                {
                    uint v0 = ((cmd.uint1 >> 16) & 0xFF) / 2;
                    uint v1 = ((cmd.uint1 >> 8) & 0xFF) / 2;
                    uint v2 = ((cmd.uint1 >> 0) & 0xFF) / 2;

                    triangles.AddRange(new[] { rspVertexBuffer[v0], rspVertexBuffer[v1], rspVertexBuffer[v2] });
                }
                else if (opcode == 0x06)
                {
                    uint v0 = ((cmd.uint1 >> 16) & 0xFF) / 2;
                    uint v1 = ((cmd.uint1 >> 8) & 0xFF) / 2;
                    uint v2 = ((cmd.uint1 >> 0) & 0xFF) / 2;

                    triangles.AddRange(new[] { rspVertexBuffer[v0], rspVertexBuffer[v1], rspVertexBuffer[v2] });

                    v0 = ((cmd.uint2 >> 16) & 0xFF) / 2;
                    v1 = ((cmd.uint2 >> 8) & 0xFF) / 2;
                    v2 = ((cmd.uint2 >> 0) & 0xFF) / 2;

                    triangles.AddRange(new[] { rspVertexBuffer[v0], rspVertexBuffer[v1], rspVertexBuffer[v2] });
                }
                else if (opcode == 0xDF)
                {
                    // End of the display list
                    break;
                }
                else
                    throw new Exception();
            }

            return triangles.Select(vert => ApplyTransformMatrix(vert, matrix)).ToList();
        }

        public static Vertex ApplyTransformMatrix(Vertex v, Matrix m)
        {
            return new Vertex
            {
                // I *believe* keeping them as shorts is the correct behavior. I may be wrong though.
                x = (short)(v.x * m[0] + v.y * m[4] + v.z * m[8] + m[12]),
                y = (short)(v.x * m[1] + v.y * m[5] + v.z * m[9] + m[13]),
                z = (short)(v.x * m[2] + v.y * m[6] + v.z * m[10] + m[14]),
                index = v.index,
                unk1 = v.unk1,
                unk2 = v.unk2,
                colorR = v.colorR,
                colorG = v.colorG,
                colorB = v.colorB,
                colorA = v.colorA,
            };
        }
    }
}
