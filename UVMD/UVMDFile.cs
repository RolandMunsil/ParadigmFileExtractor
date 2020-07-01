using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace ParadigmFileExtractor.UVMD
{
    class UVMDFile
    {
        public struct UnknownOptionalHeaderData
        {
            public float[] floats;
            public short short1;
            public short short2;
            public short short3;
            public byte b;

            public UnknownOptionalHeaderData(byte[] data)
            {
                floats = new float[10];
                for (int i = 0; i < 10; i++)
                {
                    floats[i] = data.ReadFloat32(i * 4);
                }
                short1 = data.ReadInt16(40);
                short2 = data.ReadInt16(42);
                short3 = data.ReadInt16(44);
                b = data[46];
            }
        }

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

            public Vertex(byte[] bytes16)
            {
                x = bytes16.ReadInt16(0);
                y = bytes16.ReadInt16(2);
                z = bytes16.ReadInt16(4);
                index = bytes16.ReadUInt16(6);
                unk1 = bytes16.ReadUInt16(8);
                unk2 = bytes16.ReadUInt16(10);
                colorR = bytes16[12];
                colorG = bytes16[13];
                colorB = bytes16[14];
                colorA = bytes16[15];
            }

            public override string ToString()
            {
                return $"({x}, {y}, {z}) [{index}] {unk1 / (float)0b100000} {unk2 / (float)0b100000} 0x{colorR:X2}{colorG:X2}{colorB:X2}{colorA:X2}";
            }
        }

        public struct Matrix
        {
            float[] elements;

            public float this[int i] => elements[i];

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

        struct RSPCommand
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

        struct Material
        {
            public Vertex[] pVertices;
            public uint unk4;
            public RSPCommand[] pCommandsInRDRAM; // this will be a pointer like 0x001B52B8 instead of 0x801B52B8
            public short unk_someLightingThing;
            public ushort vertCount;
            public ushort unksh16;
            public ushort unksh18;
            public ushort unksh20;
        }

        struct ModelPart
        {
            public Material[] pMaterials;
            public byte materialCount;
            public byte b5;
            public byte b6;
            public byte b7;
            public float[] sixFloats; // not a pointer!
            public byte unk1;
            //TODO
            // 11 more bytes
        }

        struct LOD
        {
            public ModelPart[] pModelParts;
            public float f;
            public byte partCount;
            public byte b2;
        }

        struct ResponseStruct
        {
            public LOD[] pLODs; //TODO: ???
            public byte lodCount;
            public byte b3;
            public byte partsPerLOD; // b2
            public byte alwaysZero; //always zero
            public Matrix[] pMatrices;
            public float float1;
            public float float2;
            public float float3;
            //public void* pCommands;
            public ushort vertexCount; // short1
            // next 6 bytes are unknown at this point

        }

        // The RSP commands load vertices read them from a location in RAM
        // I don't want to have to simulate the actual process of placing the vertices in RAM,
        // and we know that each model is only ever going to use it's own vertices. So we'll
        // just pretend all vertex buffers have the same address to make interpreting commands
        // easier.
        static uint FAKE_VERTICES_LOCATION_IN_RAM = 0x80000000;

        public UVMDFile(byte[] fileBytes, Filesystem.Filesystem filesystem)
        {
            Console.WriteLine("NEW UVMD");

            int curPtr = 0;

            ResponseStruct responseStruct;

            {
                responseStruct.lodCount = fileBytes[0];
                responseStruct.partsPerLOD = fileBytes[1];
                responseStruct.b3 = fileBytes[2];
                responseStruct.alwaysZero = fileBytes[3];
                if (responseStruct.alwaysZero != 0)
                {
                    throw new Exception();
                }
                // These are here for memory allocation, but we don't care!
                responseStruct.vertexCount = fileBytes.ReadUInt16(0x4);
                ushort totalMaterials = fileBytes.ReadUInt16(0x6); //24 structs
                ushort totalCommands = fileBytes.ReadUInt16(0x8);


                curPtr = 0xA;

                if ((responseStruct.b3 & 0x80) != 0)
                {
                    // appears to be 10 floats, 3 signed shorts, 1 byte
                    UnknownOptionalHeaderData unknownOptionalData = new UnknownOptionalHeaderData(fileBytes.Subsection(curPtr, 0x2F));
                    curPtr += 0x2F;
                }

                responseStruct.float1 = fileBytes.ReadFloat32(curPtr);
                responseStruct.float2 = fileBytes.ReadFloat32(curPtr + 4);
                responseStruct.float3 = fileBytes.ReadFloat32(curPtr + 8);
            }
            curPtr += 12;


            // Lets allocate all memory here since this is where it happens anyway
            // 36 bytes: already done, that's the response struct

            // b1 * 12: subsections
            responseStruct.pLODs = new LOD[responseStruct.lodCount];

            // b2 * 64: matrices
            responseStruct.pMatrices = new Matrix[responseStruct.partsPerLOD];

            // b1 * b2 * 44: parts
            // this is initialized in the loop

            // short1 * 16
            // this is initialized in the loop

            // short2 * 24
            // this is initialized in the loop

            // short3 * 8
            // this is initialized in the loop

            ////////////////////////////////

            // Overall structure here is correct, but the processing is not implemented yet



            for (int i = 0; i < responseStruct.lodCount; i++)
            {
                Console.WriteLine("  LOD " + i);
                LOD lodInfo;

                lodInfo.partCount = fileBytes[curPtr];
                lodInfo.b2 = fileBytes[curPtr + 1];
                lodInfo.f = fileBytes.ReadFloat32(curPtr + 2);
                lodInfo.f = lodInfo.f * lodInfo.f;

                lodInfo.pModelParts = new ModelPart[lodInfo.partCount];

                curPtr += 6;

                for (int j = 0; j < lodInfo.partCount; j++)
                {
                    Console.WriteLine("    PART " + j);

                    ModelPart modelPart;

                    modelPart.b5 = fileBytes[curPtr];
                    modelPart.b6 = fileBytes[curPtr + 1];
                    modelPart.b7 = fileBytes[curPtr + 2];
                    modelPart.sixFloats = fileBytes.Subsection(curPtr + 3, 24).AsFloats();
                    curPtr += 27;

                    modelPart.sixFloats = modelPart.sixFloats.Select(fl => fl * responseStruct.float2).ToArray();

                    byte stackByte1 = fileBytes[curPtr];
                    byte stackByte2 = fileBytes[curPtr + 1];
                    modelPart.materialCount = fileBytes[curPtr + 2];
                    curPtr += 3;

                    modelPart.pMaterials = new Material[modelPart.materialCount];

                    byte firstWordEverHadHighBitSet = 0;

                    for (int k = 0; k < modelPart.materialCount; k++)
                    {
                        Console.WriteLine("      MATERIAL " + k);

                        Material material = new Material();

                        material.unk4 = fileBytes.ReadUInt32(curPtr);
                        float unk2 = fileBytes.ReadFloat32(curPtr + 4);
                        float unk3 = fileBytes.ReadFloat32(curPtr + 8);
                        float unk4 = fileBytes.ReadFloat32(curPtr + 12);
                        curPtr += 16;
                        material.vertCount = fileBytes.ReadUInt16(curPtr);
                        material.unksh18 = fileBytes.ReadUInt16(curPtr + 2);
                        material.unksh16 = fileBytes.ReadUInt16(curPtr + 4);
                        material.unksh20 = fileBytes.ReadUInt16(curPtr + 6);
                        ushort shortsCount = fileBytes.ReadUInt16(curPtr + 8);
                        ushort commandCount = fileBytes.ReadUInt16(curPtr + 10);
                        curPtr += 12;

                        uint textureRef = material.unk4 & 0xFFF;
                        Console.WriteLine($"      {material.unk4:X8}");
                        if (textureRef != 0xFFF)
                        {
                            //Filesystem.Filesystem.File textureFile = filesystem.GetFile("UVTX", (int)textureRef);
                            // TODO: load texture
                            Console.WriteLine("      Textured!");
                        }

                        if ((int)(material.unk4 << 13) < 0)
                        {
                            //Console.WriteLine($"{twentyFourStruct.unk4:X8} {twentyFourStruct.unk4 << 13:X8}");

                            // TODO: lighting information?
                            // twentyFourStruct.unksh12 = response from light function
                            Console.WriteLine("      Lit!");
                        }
                        else
                        {
                            material.unk_someLightingThing = -1;
                        }

                        if ((material.unk4 & 0x08_000000) != 0)
                            firstWordEverHadHighBitSet = 1;

                        material.pVertices = fileBytes.Subsection(curPtr, material.vertCount * 16).InGroupsOf(16).Select(d => new Vertex(d)).ToArray();
                        curPtr += material.vertCount * 16;


                        if (textureRef != 0xFFF)
                        {
                            foreach(var x in material.pVertices)
                            {
                                Console.WriteLine(x);
                            }
                        }

                            ushort prevUnconvertedTriangle = 0;
                        int ctZero = 0;
                        RSPCommand[] commands = new RSPCommand[commandCount];
                        for (int l = 0; l < shortsCount; l++)
                        {
                            ushort nextShort = fileBytes.ReadUInt16(curPtr); curPtr += 2;

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

                                byte b = fileBytes[curPtr]; curPtr++;

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

                        material.pCommandsInRDRAM = commands;

                        modelPart.pMaterials[k] = material;
                    }

                    modelPart.unk1 = firstWordEverHadHighBitSet;

                    lodInfo.pModelParts[j] = modelPart;
                }

                responseStruct.pLODs[i] = lodInfo;
            }

            for (int m = 0; m < responseStruct.partsPerLOD; m++)
            {
                responseStruct.pMatrices[m] = new Matrix(fileBytes.Subsection(curPtr, 64).AsFloats());
                //Console.WriteLine(responseStruct.pMatrices[m]);
                curPtr += 64;
            }

            if (fileBytes.Length != (int)Math.Ceiling(curPtr / 8f) * 8)
            {
                throw new Exception();
            }




            ////////////////////////////////

            if (responseStruct.vertexCount > 100)
                foreach (LOD lod in responseStruct.pLODs)
                {
                    //if (lod.partCount > 1)
                    //{
                    List<Vertex> lodData = new List<Vertex>();
                    for (int i = 0; i < lod.partCount; i++)
                    {
                        ModelPart part = lod.pModelParts[i];
                        Matrix m = responseStruct.pMatrices[i];

                        lodData.AddRange(part.pMaterials.SelectMany(material => MaterialToVertexData(material.pVertices, material.pCommandsInRDRAM, m)));
                    }
                    DisplayModel(lodData);
                    //}
                }
        }

        private static void DisplayModel(List<Vertex> triangles)
        {
            using (UVMDDisplayWindow window = new UVMDDisplayWindow(800, 600, triangles))
            {
                window.Run(60.0);
            }
        }

        private static List<Vertex> MaterialToVertexData(Vertex[] vertices, RSPCommand[] triEntries, Matrix matrix)
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

        private static Vertex ApplyTransformMatrix(Vertex v, Matrix m)
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
