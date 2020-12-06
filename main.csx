#r "nuget: OpenTK, 4.3.0"

using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

new Game(new RenderWindow("Hello World!", new Vector2i(800, 600)));

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
		if (handle == null)
			throw new ArgumentNullException(nameof(handle));

		var videoMode = GLFW.GetVideoMode(handle);

		return new Vector2i(videoMode->Width, videoMode->Height);
	}
}

// Has game temp logic, remember to remove for abstraction
public class Game {
	RenderWindow rw;
	GameState cs;

	public Game(RenderWindow window) {
		this.rw = window;

		rw.gw.Load += () => Init();
		rw.gw.UpdateFrame += (e) => Update();
		rw.gw.RenderFrame += (e) => Render();
		rw.gw.Unload += () => Exit();

		rw.gw.Run();
	}

	void Init() {
		rw.CenterWindow();

		cs = new Triangles();
		cs.Initialize();
	}

	void Update() { }

	void Render() {
		cs.Render();

		rw.gw.SwapBuffers();
	}

	void Exit() { }
}

public abstract class GameState {
	public abstract void Initialize();
	public abstract void Render();
	public abstract void Update();
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

// Testing states, temporary
public class Triangles : GameState {
	private readonly float[] _vertices = {
			-0.5f, -0.5f, 0.0f, // Bottom-left vertex
             0.5f, -0.5f, 0.0f, // Bottom-right vertex
             0.0f,  0.5f, 0.0f  // Top vertex
        };

	private int _vertexBufferObject;
	private int _vertexArrayObject;

	private Shader _shader;

	public override void Initialize() {
		GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

		_vertexBufferObject = GL.GenBuffer();

		GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
		GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

		_shader = new Shader("assets/shaders/triangle.vert", "assets/shaders/triangle.frag");
		_shader.Use();

		_vertexArrayObject = GL.GenVertexArray();
		GL.BindVertexArray(_vertexArrayObject);

		GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
		GL.EnableVertexAttribArray(0);
		GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
	}

	public override void Render() {
		GL.Clear(ClearBufferMask.ColorBufferBit);

		_shader.Use();

		GL.BindVertexArray(_vertexArrayObject);
		GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
	}

	public override void Update() { }
}
