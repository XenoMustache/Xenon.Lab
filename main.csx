#r "nuget: OpenTK, 4.3.0"
#r "nuget: System.Drawing.Common, 5.0.0"

using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Collections.Generic;

// Engine
public class RenderWindow {
	public GameWindow gw;

	IGLFWGraphicsContext ctx;

	GameWindowSettings gws;
	NativeWindowSettings nws;

	public RenderWindow(string title, Vector2i size) {
		gws = new GameWindowSettings();
		nws = new NativeWindowSettings();

		nws.Title = title;
		nws.Size = size;

		gw = new GameWindow(gws, nws);

		ctx = gw.Context;
	}

	public void CenterWindow() {
		unsafe {
			var location = GetMonitorResolution(GLFW.GetPrimaryMonitor());

			gw.Location = (location / 2) - gw.Size / 2;
		}
	}

	unsafe static Vector2i GetMonitorResolution(Monitor* handle) {
		var videoMode = GLFW.GetVideoMode(handle);

		return new Vector2i(videoMode->Width, videoMode->Height);
	}
}

public abstract class GameState {
	public RenderWindow window;

	public abstract void Init();
	public abstract void Draw();
	public abstract void Resize();
	public abstract void Update();
	public abstract void Exit();
}

public class Shader {
	public readonly int handle;
	private readonly Dictionary<string, int> _uniformLocations;

	public Shader(string vertexPath, string fragmentPath) {
		var shaderSource = LoadSource(vertexPath);

		var vertexShader = GL.CreateShader(ShaderType.VertexShader);

		GL.ShaderSource(vertexShader, shaderSource);

		CompileShader(vertexShader);

		shaderSource = LoadSource(fragmentPath);
		var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
		GL.ShaderSource(fragmentShader, shaderSource);
		CompileShader(fragmentShader);

		handle = GL.CreateProgram();

		GL.AttachShader(handle, vertexShader);
		GL.AttachShader(handle, fragmentShader);

		LinkProgram(handle);

		GL.DetachShader(handle, vertexShader);
		GL.DetachShader(handle, fragmentShader);
		GL.DeleteShader(fragmentShader);
		GL.DeleteShader(vertexShader);


		GL.GetProgram(handle, GetProgramParameterName.ActiveUniforms, out var numberOfUniforms);

		_uniformLocations = new Dictionary<string, int>();

		for (var i = 0; i < numberOfUniforms; i++) {
			var key = GL.GetActiveUniform(handle, i, out _, out _);

			var location = GL.GetUniformLocation(handle, key);

			_uniformLocations.Add(key, location);
		}
	}

	private static void CompileShader(int shader) {
		GL.CompileShader(shader);

		GL.GetShader(shader, ShaderParameter.CompileStatus, out var code);
		if (code != (int)All.True) {
			var infoLog = GL.GetShaderInfoLog(shader);
			throw new Exception($"Error occurred whilst compiling Shader({shader}).\n\n{infoLog}");
		}
	}

	private static void LinkProgram(int program) {
		GL.LinkProgram(program);

		GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var code);
		if (code != (int)All.True) {
			throw new Exception($"Error occurred whilst linking Program({program})");
		}
	}

	public void Use() {
		GL.UseProgram(handle);
	}

	public int GetAttribLocation(string attribName) {
		return GL.GetAttribLocation(handle, attribName);
	}

	private static string LoadSource(string path) {
		using (var sr = new StreamReader(path, Encoding.UTF8)) {
			return sr.ReadToEnd();
		}
	}

	public void SetInt(string name, int data) {
		GL.UseProgram(handle);
		GL.Uniform1(_uniformLocations[name], data);
	}

	public void SetFloat(string name, float data) {
		GL.UseProgram(handle);
		GL.Uniform1(_uniformLocations[name], data);
	}

	public void SetMatrix4(string name, Matrix4 data) {
		GL.UseProgram(handle);
		GL.UniformMatrix4(_uniformLocations[name], true, ref data);
	}

	public void SetVector3(string name, Vector3 data) {
		GL.UseProgram(handle);
		GL.Uniform3(_uniformLocations[name], data);
	}
}

public class Texture {
	public readonly int Handle;

	public Texture(string path) {
		Handle = GL.GenTexture();

		Use();

		using (var image = new Bitmap(path)) {
			var data = image.LockBits(
				new Rectangle(0, 0, image.Width, image.Height),
				ImageLockMode.ReadOnly,
				System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			GL.TexImage2D(TextureTarget.Texture2D,
				0,
				PixelInternalFormat.Rgba,
				image.Width,
				image.Height,
				0,
				PixelFormat.Bgra,
				PixelType.UnsignedByte,
				data.Scan0);
		}
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

		GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
	}

	public void Use(TextureUnit unit = TextureUnit.Texture0) {
		GL.ActiveTexture(unit);
		GL.BindTexture(TextureTarget.Texture2D, Handle);
	}
}

// Game
public class Game { // Has game logic, remember to remove temp states for abstraction
	RenderWindow rw;
	GameState cs;

	public Game(RenderWindow window) {
		this.rw = window;

		rw.gw.Load += () => Init();
		rw.gw.UpdateFrame += (e) => Update();
		rw.gw.RenderFrame += (e) => Draw();
		rw.gw.Resize += (e) => Resize();
		rw.gw.Unload += () => Exit();

		rw.gw.Run();
	}

	void Init() {
		rw.CenterWindow();

		cs = new ElementBuffer();
		cs.window = rw;

		cs.Init();
	}

	void Update() {
		cs.Update();
	}

	void Draw() => cs.Draw();
	void Resize() => cs.Resize();
	void Exit() => cs.Exit();
}

public class Triangles : GameState { // Testing states, temporary
	private readonly float[] _vertices = {
			-0.5f, -0.5f, 0.0f, // Bottom-left vertex
             0.5f, -0.5f, 0.0f, // Bottom-right vertex
             0.0f,  0.5f, 0.0f  // Top vertex
        };

	private int _vertexBufferObject;
	private int _vertexArrayObject;

	private Shader _shader;

	public override void Init() {
		GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

		_vertexBufferObject = GL.GenBuffer();

		GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
		GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

		_shader = new Shader("assets/shaders/generic.vert", "assets/shaders/generic.frag");
		_shader.Use();

		_vertexArrayObject = GL.GenVertexArray();
		GL.BindVertexArray(_vertexArrayObject);

		GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
		GL.EnableVertexAttribArray(0);
		GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
	}

	public override void Draw() {
		GL.Clear(ClearBufferMask.ColorBufferBit);

		_shader.Use();

		GL.BindVertexArray(_vertexArrayObject);
		GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

		window.gw.SwapBuffers();
	}

	public override void Update() { }

	public override void Resize() {
		GL.Viewport(0, 0, window.gw.Size.X, window.gw.Size.Y);
	}

	public override void Exit() {
		GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
		GL.BindVertexArray(0);
		GL.UseProgram(0);

		GL.DeleteBuffer(_vertexBufferObject);
		GL.DeleteVertexArray(_vertexArrayObject);

		GL.DeleteProgram(_shader.handle);
	}
}

public class ElementBuffer : GameState {
	private readonly float[] _vertices = {
			 0.5f,  0.5f, 0.0f, // top right
             0.5f, -0.5f, 0.0f, // bottom right
            -0.5f, -0.5f, 0.0f, // bottom left
            -0.5f,  0.5f, 0.0f, // top left
    };

	private readonly uint[] _indices = {
			0, 1, 3, // Bottom half
            1, 2, 3  // Top half
    };

	private int _vertexBufferObject;
	private int _vertexArrayObject;

	private Shader _shader;

	private int _elementBufferObject;

	public override void Init() {
		GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

		_vertexBufferObject = GL.GenBuffer();
		GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
		GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

		_elementBufferObject = GL.GenBuffer();
		GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);

		GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

		_shader = new Shader("assets/shaders/generic.vert", "assets/shaders/generic.frag");
		_shader.Use();

		_vertexArrayObject = GL.GenVertexArray();
		GL.BindVertexArray(_vertexArrayObject);

		GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

		GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);

		GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
		GL.EnableVertexAttribArray(0);
	}

	public override void Draw() {
		GL.Clear(ClearBufferMask.ColorBufferBit);

		_shader.Use();

		GL.BindVertexArray(_vertexArrayObject);

		GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);

		window.gw.SwapBuffers();
	}

	public override void Update() { }

	public override void Resize() {
		GL.Viewport(0, 0, window.gw.Size.X, window.gw.Size.Y);
	}

	public override void Exit() {
		GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
		GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
		GL.BindVertexArray(0);
		GL.UseProgram(0);

		GL.DeleteBuffer(_vertexBufferObject);
		GL.DeleteBuffer(_elementBufferObject);
		GL.DeleteVertexArray(_vertexArrayObject);
		GL.DeleteProgram(_shader.handle);
	}
}

// Primary script logic
new Game(new RenderWindow("Hello World!", new Vector2i(800, 600)));
