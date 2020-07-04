using ParadigmFileExtractor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ParadigmFileExtractor.Common.ThreeD;

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

            public UnknownOptionalHeaderData(PowerByteArray data)
            {
                floats = new float[10];
                for (int i = 0; i < 10; i++)
                {
                    floats[i] = data.ReadFloat(i * 4);
                }
                short1 = data.ReadS16(40);
                short2 = data.ReadS16(42);
                short3 = data.ReadS16(44);
                b = data[46];
            }
        }

        public struct Material
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

        public struct ModelPart
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

        public struct LOD
        {
            public ModelPart[] pModelParts;
            public float f;
            public byte partCount;
            public byte b2;
        }

        public LOD[] pLODs;
        public byte lodCount;
        public byte b3;
        public byte partsPerLOD;
        //public byte alwaysZero; //always zero
        public Matrix[] pMatrices;
        public float float1;
        public float float2;
        public float float3;
        //public void* pCommands;
        public ushort vertexCount;
        // next 6 bytes are unknown at this point (I think there's likely nothing here, it's just aligned to 8 bytes

        //public ResponseStruct responseStruct;
        public UVMDFile(PowerByteArray fileBytes, Filesystem.Filesystem filesystem)
        {
            Console.WriteLine("NEW UVMD");

            {
                lodCount = fileBytes.NextU8();
                partsPerLOD = fileBytes.NextU8();
                b3 = fileBytes.NextU8();
                byte alwaysZero = fileBytes.NextU8();
                if (alwaysZero != 0)
                {
                    throw new Exception();
                }
                
                vertexCount = fileBytes.NextU16();
                // These are here for memory allocation, but we don't care!
                ushort totalMaterials = fileBytes.NextU16(); //24 structs
                ushort totalCommands = fileBytes.NextU16();

                if ((b3 & 0x80) != 0)
                {
                    // appears to be 10 floats, 3 signed shorts, 1 byte
                    // TODO
                    UnknownOptionalHeaderData unknownOptionalData = new UnknownOptionalHeaderData(fileBytes.NextSubArray(0x2F));
                }

                float1 = fileBytes.NextFloat();
                float2 = fileBytes.NextFloat();
                float3 = fileBytes.NextFloat();
            }

            // Lets allocate all memory here since this is where it happens anyway
            // 36 bytes: already done, that's the response struct

            // b1 * 12: subsections
            pLODs = new LOD[lodCount];

            // b2 * 64: matrices
            pMatrices = new Matrix[partsPerLOD];

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



            for (int i = 0; i < lodCount; i++)
            {
                Console.WriteLine("  LOD " + i);
                LOD lodInfo;

                lodInfo.partCount = fileBytes.NextU8();
                lodInfo.b2 = fileBytes.NextU8();
                lodInfo.f = fileBytes.NextFloat();
                lodInfo.f = lodInfo.f * lodInfo.f;

                lodInfo.pModelParts = new ModelPart[lodInfo.partCount];

                for (int j = 0; j < lodInfo.partCount; j++)
                {
                    Console.WriteLine("    PART " + j);

                    ModelPart modelPart;

                    modelPart.b5 = fileBytes.NextU8();
                    modelPart.b6 = fileBytes.NextU8();
                    modelPart.b7 = fileBytes.NextU8();
                    modelPart.sixFloats = fileBytes.NextSubArray(24).AsFloats();

                    modelPart.sixFloats = modelPart.sixFloats.Select(fl => fl * float2).ToArray();

                    byte stackByte1 = fileBytes.NextU8();
                    byte stackByte2 = fileBytes.NextU8();
                    modelPart.materialCount = fileBytes.NextU8();

                    modelPart.pMaterials = new Material[modelPart.materialCount];

                    byte firstWordEverHadHighBitSet = 0;

                    for (int k = 0; k < modelPart.materialCount; k++)
                    {
                        Console.WriteLine("      MATERIAL " + k);

                        Material material = new Material();

                        material.unk4 = fileBytes.NextU32();
                        float unk2 = fileBytes.NextFloat();
                        float unk3 = fileBytes.NextFloat();
                        float unk4 = fileBytes.NextFloat();
                        material.vertCount = fileBytes.NextU16();
                        material.unksh18 = fileBytes.NextU16();
                        material.unksh16 = fileBytes.NextU16();
                        material.unksh20 = fileBytes.NextU16();
                        ushort shortsCount = fileBytes.NextU16();
                        ushort commandCount = fileBytes.NextU16();

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

                        material.pVertices = fileBytes.NextSubArray(material.vertCount * 16).InGroupsOf(16).Select(d => new Vertex(d)).ToArray();


                        if (textureRef != 0xFFF)
                        {
                            foreach (var x in material.pVertices)
                            {
                                Console.WriteLine(x);
                            }
                        }

                        RSPCommand[] commands = UnpackTriangleCommands(fileBytes, shortsCount, commandCount);

                        material.pCommandsInRDRAM = commands;

                        modelPart.pMaterials[k] = material;
                    }

                    modelPart.unk1 = firstWordEverHadHighBitSet;

                    lodInfo.pModelParts[j] = modelPart;
                }

                pLODs[i] = lodInfo;
            }

            for (int m = 0; m < partsPerLOD; m++)
            {
                pMatrices[m] = new Matrix(fileBytes.NextSubArray(64).AsFloats());
            }

            if (fileBytes.Length != (int)Math.Ceiling(fileBytes.Position / 8f) * 8)
            {
                throw new Exception();
            }




            ////////////////////////////////

            if (vertexCount > 100)
                foreach (LOD lod in pLODs)
                {
                    //if (lod.partCount > 1)
                    //{
                    List<Vertex> lodData = new List<Vertex>();
                    for (int i = 0; i < lod.partCount; i++)
                    {
                        ModelPart part = lod.pModelParts[i];
                        Matrix m = pMatrices[i];

                        lodData.AddRange(part.pMaterials.SelectMany(material => MaterialToVertexData(material.pVertices, material.pCommandsInRDRAM, m)));
                    }
                    DisplayModel(lodData);
                    //}
                }
        }

        public static void DisplayModel(List<Vertex> triangles)
        {
            using (UVMDDisplayWindow window = new UVMDDisplayWindow(800, 600, triangles))
            {
                window.Run(60.0);
            }
        }
    }
}
