#r "nuget: OpenTK, 4.3.0"
#r "nuget: System.Drawing.Common, 5.0.0"
#r "nuget: System.CodeDom, 5.0.0"
#r "nuget: System.Runtime.Loader, 4.3.0"
#r "nuget: Newtonsoft.Json, 12.0.3"
#r "nuget: Microsoft.CodeAnalysis.CSharp, 3.8.0"

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
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Runtime.Loader;

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

public static class Script {
	public static void Compile(string file, string method = "Start", Object[] args = null, string location = "", string assembly = null) {
		string codeToCompile = File.ReadAllText($"{location}{file}.cs");

		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(codeToCompile);

		string assemblyName = Path.GetRandomFileName();

		var refPaths = new[] {
			typeof(System.Object).GetTypeInfo().Assembly.Location,
			typeof(Console).GetTypeInfo().Assembly.Location,
			Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll")
		};

		MetadataReference[] references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

		CSharpCompilation compilation = CSharpCompilation.Create(
			assemblyName,
			syntaxTrees: new[] { syntaxTree },
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		using (var ms = new MemoryStream()) {
			EmitResult result = compilation.Emit(ms);

			if (!result.Success) {
				Write($"Compilation of {file}.cs failed!");
				IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
					diagnostic.IsWarningAsError ||
					diagnostic.Severity == DiagnosticSeverity.Error);

				foreach (Diagnostic diagnostic in failures) {
					Console.Error.WriteLine("\t{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
				}
			} else {
				ms.Seek(0, SeekOrigin.Begin);

				Assembly assmbly = AssemblyLoadContext.Default.LoadFromStream(ms);
				var type = assmbly.GetType($"{assembly}.{file}");
				var instance = assmbly.CreateInstance($"{assembly}.{file}");
				var meth = type.GetMember(method).First() as MethodInfo;
				meth.Invoke(instance, args);
			}
		}
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

		//cs = new ElementBuffer();
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

// Primary script logic
// new Game(new RenderWindow("Hello World!", new Vector2i(800, 600)));

Script.Compile("Example", "Start", new [] { 1.ToString() }, "assets\\scripts\\");
