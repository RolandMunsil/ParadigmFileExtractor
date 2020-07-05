using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using ParadigmFileExtractor.UVTX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace ParadigmFileExtractor.Common
{
    class ThreeDDisplayWindow : GameWindow
    {
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenGLVertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector4 color;
            public Vector2 texCoord;

            public static int Size
            {
                get
                {
                    return (3 + 3 + 4 + 2) * sizeof(float);
                }
            }
        }

        private const string VERTEX_SHADER_SRC =
@"#version 330 core
layout(location = 0) in vec3 position;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in vec4 inColor;
layout(location = 3) in vec2 inTexCoord;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 normal;
out vec4 color;
out vec2 texCoord;

void main(void)
{
    gl_Position = projection * view * model * vec4(position.yzx, 1.0);
    normal = (model * vec4(inNormal.yzx, 0.0)).xyz;
    color = inColor;
    texCoord = inTexCoord;
}";

        private const string BASIC_FRAG_SHADER_SRC =
@"#version 330
in vec3 normal;
in vec4 color;
in vec2 texCoord;
out vec4 outputColor;

uniform sampler2D texture0;

void main()
{
    vec3 norm = normalize(normal);
    vec3 lightDir = normalize(vec3(1, 1, 1));

    float diff = max(dot(norm, lightDir), 0.0); //We make sure the value is non negative with the max function.

    outputColor = color;
}";

        private const string TEXTURED_FRAG_SHADER_SRC =
@"#version 330
in vec3 normal;
in vec4 color;
in vec2 texCoord;
out vec4 outputColor;

uniform sampler2D texture0;

void main()
{
    vec3 norm = normalize(normal);
    vec3 lightDir = normalize(vec3(1, 1, 1));

    float diff = max(dot(norm, lightDir), 0.0); //We make sure the value is non negative with the max function.

    outputColor = color * texture(texture0, texCoord);
}";


        private int vertexBufferObjectHandle;
        private int vertexArrayObjectHandle;
        private Shader basicShader;
        private Shader textureShader;

        private long startTimeTicks;

        private float modelSize;

        private List<OpenGLVertex> verticesSoFar;
        private OpenGLVertex[] finalizedVertexArray;

        struct VertexDataSegment
        {
            public int startIndex;
            public int length;
            public Texture? texture;
        }

        private List<VertexDataSegment> segments;

        public ThreeDDisplayWindow() : base(800, 600, GraphicsMode.Default, "")
        {
            startTimeTicks = DateTime.Now.Ticks;
            verticesSoFar = new List<OpenGLVertex>();
            segments = new List<VertexDataSegment>();
        }

        public void AddVertices(IList<ThreeD.Vertex> n64Verts)
        {
            VertexDataSegment segment = new VertexDataSegment();
            segment.startIndex = verticesSoFar.Count;
            segment.length = n64Verts.Count;
            segment.texture = null;

            verticesSoFar.AddRange(UVMDVerticesToWindowVertices(n64Verts, null));
            segments.Add(segment);
        }

        public void AddTexturedVertices(IList<ThreeD.Vertex> n64Verts, UVTXFile uvtx)
        {
            VertexDataSegment segment = new VertexDataSegment();
            segment.startIndex = verticesSoFar.Count;
            segment.length = n64Verts.Count;
            segment.texture = new Texture(UVTXConverter.GetBitmap(uvtx));

            UVTXConverter.GetBitmap(uvtx).Save("test.png");

            verticesSoFar.AddRange(UVMDVerticesToWindowVertices(n64Verts, uvtx));
            segments.Add(segment);
        }

        protected override void OnLoad(EventArgs e)
        {
            finalizedVertexArray = verticesSoFar.ToArray();

            foreach (OpenGLVertex vert in finalizedVertexArray)
            {
                modelSize = Math.Max(modelSize, vert.position.Length);
            }



            // Set clear color
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            vertexBufferObjectHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObjectHandle);
            GL.BufferData(BufferTarget.ArrayBuffer, finalizedVertexArray.Length * OpenGLVertex.Size, finalizedVertexArray, BufferUsageHint.StaticDraw);

            basicShader = new Shader(VERTEX_SHADER_SRC, BASIC_FRAG_SHADER_SRC);
            textureShader = new Shader(VERTEX_SHADER_SRC, TEXTURED_FRAG_SHADER_SRC);
            

            vertexArrayObjectHandle = GL.GenVertexArray();
            GL.BindVertexArray(vertexArrayObjectHandle);

            textureShader.Use();
            DefineVertexAttribute(textureShader, "position", 3, 0);
            DefineVertexAttribute(textureShader, "inNormal", 3, 3);
            DefineVertexAttribute(textureShader, "inColor", 4, 6);
            DefineVertexAttribute(textureShader, "inTexCoord", 2, 10);

            textureShader.Use();
            DefineVertexAttribute(textureShader, "position", 3, 0);
            DefineVertexAttribute(textureShader, "inNormal", 3, 3);
            DefineVertexAttribute(textureShader, "inColor", 4, 6);
            DefineVertexAttribute(textureShader, "inTexCoord", 2, 10);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObjectHandle);

            base.OnLoad(e);
        }

        private static void DefineVertexAttribute(Shader shader, string attributeName, int sizeFloats, int posFloats)
        {
            int attribLocation = GL.GetAttribLocation(shader.handle, attributeName);
            GL.EnableVertexAttribArray(attribLocation);
            GL.VertexAttribPointer(attribLocation, sizeFloats, VertexAttribPointerType.Float, false, OpenGLVertex.Size, posFloats * sizeof(float));
        }

        private static List<OpenGLVertex> UVMDVerticesToWindowVertices(IList<ThreeD.Vertex> vertices, UVTXFile? uvtx)
        {
            List<OpenGLVertex> vertexDataList = new List<OpenGLVertex>();
            for (int i = 0; i < vertices.Count; i += 3)
            {
                OpenGLVertex v1 = ConvertSingleVertex(vertices[i + 0], uvtx);
                OpenGLVertex v2 = ConvertSingleVertex(vertices[i + 1], uvtx);
                OpenGLVertex v3 = ConvertSingleVertex(vertices[i + 2], uvtx);

                Vector3 normal = Vector3.Cross(v2.position - v1.position, v3.position - v1.position).Normalized();
                v1.normal = normal;
                v2.normal = normal;
                v3.normal = normal;

                //v1.texCoord = new Vector2(0, 0);
                //v2.texCoord = new Vector2(0, 1);
                //v3.texCoord = new Vector2(1, 0);

                vertexDataList.Add(v1);
                vertexDataList.Add(v2);
                vertexDataList.Add(v3);
            }

            return vertexDataList;
        }

        private static OpenGLVertex ConvertSingleVertex(ThreeD.Vertex v, UVTXFile? uvtx)
        {
            var texCoord = new Vector2();
            if (uvtx != null)
            {
                UVTXConverter.RDPState rdpState = UVTXConverter.ExecuteCommands(uvtx, out _);
                var tileDesc = rdpState.tileDescriptors[rdpState.tileToUseWhenTexturing];
                if (tileDesc.sLo == 0 && tileDesc.tLo == 0 && tileDesc.tHi == 0)
                {
                    //throw new Exception();
                }
                float origS = ((short)v.unk1) / (float)0b100000;
                float origT = ((short)v.unk2) / (float)0b100000;
                float openGLS = (origS - tileDesc.sLo) / (tileDesc.sHi - tileDesc.sLo);
                float openGLT = (origT - tileDesc.tLo) / (tileDesc.tHi - tileDesc.tLo);
                texCoord = new Vector2(openGLS, openGLT);
            }


            return new OpenGLVertex
            {
                position = new Vector3(v.x, v.y, v.z),
                color = new Vector4(v.colorR, v.colorG, v.colorB, v.colorA) / 0xFF,
                texCoord = texCoord
            };
        }

        // Now that initialization is done, let's create our render loop.
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //GL.BindVertexArray(vertexArrayObjectHandle);

            float secs = (float)(DateTime.Now.Ticks - startTimeTicks) / TimeSpan.TicksPerSecond;

            Matrix4 model = Matrix4.CreateRotationY(secs) * Matrix4.CreateScale(1 / modelSize);
            Matrix4 view = Matrix4.LookAt(new Vector3(0.0f, 2.0f, -3.0f), new Vector3(0), new Vector3(0, 1, 0));
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), Width / (float)Height, 0.1f, 100.0f);

            //GL.PointSize(3.0f);
            foreach (VertexDataSegment segment in segments)
            {
                Shader shader;
                if (segment.texture != null)
                {
                    shader = textureShader;
                    shader.Use();
                    segment.texture.Use();
                }
                else
                {
                    shader = basicShader;
                    shader.Use();
                }

                shader.SetMatrix4("model", model);
                shader.SetMatrix4("view", view);
                shader.SetMatrix4("projection", proj);

                GL.DrawArrays(PrimitiveType.Triangles, segment.startIndex, segment.length);
            }

            SwapBuffers();
            base.OnRenderFrame(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            KeyboardState input = Keyboard.GetState();

            if (input.IsKeyDown(Key.Escape))
            {
                Exit();
            }

            base.OnUpdateFrame(e);
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            base.OnResize(e);
        }

        // Now, for cleanup. This isn't technically necessary since C# and OpenGL will clean up all resources automatically when
        // the program closes, but it's very important to know how anyway.
        protected override void OnUnload(EventArgs e)
        {
            // Unbind all the resources by binding the targets to 0/null.
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Delete all the resources.
            GL.DeleteBuffer(vertexBufferObjectHandle);
            GL.DeleteVertexArray(vertexArrayObjectHandle);

            basicShader.Dispose();
            textureShader.Dispose();

            base.OnUnload(e);
        }

        private class Shader : IDisposable
        {
            public int handle;

            public Shader(string vertexShaderSource, string fragShaderSource)
            {
                int VertexShader = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(VertexShader, vertexShaderSource);

                int FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(FragmentShader, fragShaderSource);

                GL.CompileShader(VertexShader);

                string infoLogVert = GL.GetShaderInfoLog(VertexShader);
                if (infoLogVert != String.Empty)
                    Console.WriteLine(infoLogVert);

                GL.CompileShader(FragmentShader);

                string infoLogFrag = GL.GetShaderInfoLog(FragmentShader);

                if (infoLogFrag != String.Empty)
                    Console.WriteLine(infoLogFrag);

                handle = GL.CreateProgram();

                GL.AttachShader(handle, VertexShader);
                GL.AttachShader(handle, FragmentShader);

                GL.LinkProgram(handle);

                GL.DetachShader(handle, VertexShader);
                GL.DetachShader(handle, FragmentShader);
                GL.DeleteShader(FragmentShader);
                GL.DeleteShader(VertexShader);
            }

            public void SetMatrix4(string uniformName, Matrix4 matrix)
            {
                GL.UniformMatrix4(GL.GetUniformLocation(handle, uniformName), false, ref matrix);
            }

            public void Use()
            {
                GL.UseProgram(handle);
            }

            private bool disposedValue = false;

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    GL.DeleteProgram(handle);

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        // https://opentk.net/learn/chapter1/4-textures.html
        private class Texture
        {
            int handle;

            public Texture(Bitmap bitmap)
            {
                handle = GL.GenTexture();
                Use();

                List<byte> pixelData = new List<byte>();
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color color = bitmap.GetPixel(x, (bitmap.Height - y) - 1);
                        pixelData.AddRange(new[] { color.R, color.G, color.B, color.A });
                    }
                }

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelData.ToArray());
            }

            public void Use()
            {
                //GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, handle);
            }
        }
    }
}


