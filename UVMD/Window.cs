using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParadigmFileExtractor.UVMD
{
    class Window : GameWindow
    {
        private readonly float[] vertexData;

        private int vertexBufferObjectHandle;
        private int vertexArrayObjectHandle;
        private Shader shader;

        private string vertShader =
@"#version 330 core
layout(location = 0) in vec3 position;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in vec4 inColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 normal;
out vec4 color;

void main(void)
{
    gl_Position = projection * view * model * vec4(position.yzx, 1.0);
    normal = (model * vec4(inNormal.yzx, 0.0)).xyz;
    color = inColor;
}";

        private string fragShader =
@"#version 330
in vec3 normal;
in vec4 color;
out vec4 outputColor;
void main()
{
    vec3 norm = normalize(normal);
    vec3 lightDir = normalize(vec3(1, 1, 1));

    float diff = max(dot(norm, lightDir), 0.0); //We make sure the value is non negative with the max function.

    outputColor = color;
}";

        private long startTimeTicks;

        private float modelSize;

        public Window(int width, int height, IList<float> vertexDataList)
            : base(width, height, GraphicsMode.Default, "")
        {
            startTimeTicks = DateTime.Now.Ticks;

            for (int i = 0; i < vertexDataList.Count; i += 10)
            {
                float x = vertexDataList[i];
                float y = vertexDataList[i + 1];
                float z = vertexDataList[i + 2];
                modelSize = Math.Max(modelSize, (float)Math.Sqrt(x * x + y * y + z * z));
            }

            this.vertexData = vertexDataList.ToArray();
        }

        public static List<float> UVMDVerticesToWindowVertices(IList<UVMDFile.Vertex> vertices, UVMDFile.Matrix matrix)
        {
            List<float> vertexDataList = new List<float>();
            for (int i = 0; i < vertices.Count; i += 3)
            {
                Vector3 p1 = ApplyTransformMatrix(new Vector3(vertices[i + 0].x, vertices[i + 0].y, vertices[i + 0].z), matrix);
                Vector4 p1Color = new Vector4(vertices[i + 0].colorR, vertices[i + 0].colorG, vertices[i + 0].colorB, vertices[i + 0].colorA) / 0xFF;
                Vector3 p2 = ApplyTransformMatrix(new Vector3(vertices[i + 1].x, vertices[i + 1].y, vertices[i + 1].z), matrix);
                Vector4 p2Color = new Vector4(vertices[i + 1].colorR, vertices[i + 1].colorG, vertices[i + 1].colorB, vertices[i + 1].colorA) / 0xFF;
                Vector3 p3 = ApplyTransformMatrix(new Vector3(vertices[i + 2].x, vertices[i + 2].y, vertices[i + 2].z), matrix);
                Vector4 p3Color = new Vector4(vertices[i + 2].colorR, vertices[i + 2].colorG, vertices[i + 2].colorB, vertices[i + 2].colorA) / 0xFF;
                Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).Normalized();

                vertexDataList.AddRange(new[]
                {
                    p1.X, p1.Y, p1.Z, normal.X, normal.Y, normal.Z, p1Color.X, p1Color.Y, p1Color.Z, p1Color.W,
                    p2.X, p2.Y, p2.Z, normal.X, normal.Y, normal.Z, p2Color.X, p2Color.Y, p2Color.Z, p2Color.W,
                    p3.X, p3.Y, p3.Z, normal.X, normal.Y, normal.Z, p3Color.X, p3Color.Y, p3Color.Z, p3Color.W,
                });
            }

            return vertexDataList;
        }

        private static Vector3 ApplyTransformMatrix(Vector3 v, UVMDFile.Matrix m)
        {
            return new Vector3(
                    (float)Math.Round(v.X * m[0] + v.Y * m[4] + v.Z * m[8] + m[12]),
                    (float)Math.Round(v.X * m[1] + v.Y * m[5] + v.Z * m[9] + m[13]),
                    (float)Math.Round(v.X * m[2] + v.Y * m[6] + v.Z * m[10] + m[14])
                );
        }

        protected override void OnLoad(EventArgs e)
        {
            // Set clear color
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            vertexBufferObjectHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObjectHandle);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * sizeof(float), vertexData, BufferUsageHint.StaticDraw);

            shader = new Shader(vertShader, fragShader);
            shader.Use();

            vertexArrayObjectHandle = GL.GenVertexArray();
            GL.BindVertexArray(vertexArrayObjectHandle);

            int positionLocation = GL.GetAttribLocation(shader.handle, "position");
            GL.EnableVertexAttribArray(positionLocation);
            // Remember to change the stride as we now have 6 floats per vertex
            GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 10 * sizeof(float), 0);

            // We now need to define the layout of the normal so the shader can use it
            int normalLocation = GL.GetAttribLocation(shader.handle, "inNormal");
            GL.EnableVertexAttribArray(normalLocation);
            GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, 10 * sizeof(float), 3 * sizeof(float));

            int colorLocation = GL.GetAttribLocation(shader.handle, "inColor");
            GL.EnableVertexAttribArray(colorLocation);
            GL.VertexAttribPointer(colorLocation, 4, VertexAttribPointerType.Float, false, 10 * sizeof(float), 6 * sizeof(float));



            //GL.VertexAttribPointer(0, 6, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);

            // Enable variable 0 in the shader.
            //GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObjectHandle);

            base.OnLoad(e);
        }

        // Now that initialization is done, let's create our render loop.
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //shader.Use();
            //GL.BindVertexArray(vertexArrayObjectHandle);

            float secs = (float)(DateTime.Now.Ticks - startTimeTicks) / TimeSpan.TicksPerSecond;

            Matrix4 model = Matrix4.CreateRotationY(secs) * Matrix4.CreateScale(1 / modelSize);
            Matrix4 view = Matrix4.LookAt(new Vector3(0.0f, 2.0f, -3.0f), new Vector3(0), new Vector3(0, 1, 0));
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), Width / (float)Height, 0.1f, 100.0f);

            shader.SetMatrix4("model", model);
            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", proj);

            GL.PointSize(3.0f);
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexData.Length / 10);

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

            GL.DeleteProgram(shader.handle);
            base.OnUnload(e);
        }
    }

    public class Shader : IDisposable
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

        ~Shader()
        {
            GL.DeleteProgram(handle);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
