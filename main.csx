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

#region Engine
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
	public abstract void Initialize();
	public abstract void Draw();
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

// A helper class, much like Shader, meant to simplify loading textures.
public class Texture {
	public readonly int Handle;

	// Create texture from path.
	public Texture(string path) {
		// Generate handle
		Handle = GL.GenTexture();

		// Bind the handle
		Use();

		// For this example, we're going to use .NET's built-in System.Drawing library to load textures.

		// Load the image
		using (var image = new Bitmap(path)) {
			// First, we get our pixels from the bitmap we loaded.
			// Arguments:
			//   The pixel area we want. Typically, you want to leave it as (0,0) to (width,height), but you can
			//   use other rectangles to get segments of textures, useful for things such as spritesheets.
			//   The locking mode. Basically, how you want to use the pixels. Since we're passing them to OpenGL,
			//   we only need ReadOnly.
			//   Next is the pixel format we want our pixels to be in. In this case, ARGB will suffice.
			//   We have to fully qualify the name because OpenTK also has an enum named PixelFormat.
			var data = image.LockBits(
				new Rectangle(0, 0, image.Width, image.Height),
				ImageLockMode.ReadOnly,
				System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			// Now that our pixels are prepared, it's time to generate a texture. We do this with GL.TexImage2D
			// Arguments:
			//   The type of texture we're generating. There are various different types of textures, but the only one we need right now is Texture2D.
			//   Level of detail. We can use this to start from a smaller mipmap (if we want), but we don't need to do that, so leave it at 0.
			//   Target format of the pixels. This is the format OpenGL will store our image with.
			//   Width of the image
			//   Height of the image.
			//   Border of the image. This must always be 0; it's a legacy parameter that Khronos never got rid of.
			//   The format of the pixels, explained above. Since we loaded the pixels as ARGB earlier, we need to use BGRA.
			//   Data type of the pixels.
			//   And finally, the actual pixels.
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

		// Now that our texture is loaded, we can set a few settings to affect how the image appears on rendering.

		// First, we set the min and mag filter. These are used for when the texture is scaled down and up, respectively.
		// Here, we use Linear for both. This means that OpenGL will try to blend pixels, meaning that textures scaled too far will look blurred.
		// You could also use (amongst other options) Nearest, which just grabs the nearest pixel, which makes the texture look pixelated if scaled too far.
		// NOTE: The default settings for both of these are LinearMipmap. If you leave these as default but don't generate mipmaps,
		// your image will fail to render at all (usually resulting in pure black instead).
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

		// Now, set the wrapping mode. S is for the X axis, and T is for the Y axis.
		// We set this to Repeat so that textures will repeat when wrapped. Not demonstrated here since the texture coordinates exactly match
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
		GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

		// Next, generate mipmaps.
		// Mipmaps are smaller copies of the texture, scaled down. Each mipmap level is half the size of the previous one
		// Generated mipmaps go all the way down to just one pixel.
		// OpenGL will automatically switch between mipmaps when an object gets sufficiently far away.
		// This prevents distant objects from having their colors become muddy, as well as saving on memory.
		GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
	}

	// Activate texture
	// Multiple textures can be bound, if your shader needs more than just one.
	// If you want to do that, use GL.ActiveTexture to set which slot GL.BindTexture binds to.
	// The OpenGL standard requires that there be at least 16, but there can be more depending on your graphics card.
	public void Use(TextureUnit unit = TextureUnit.Texture0) {
		GL.ActiveTexture(unit);
		GL.BindTexture(TextureTarget.Texture2D, Handle);
	}
}

#endregion

#region Game
public class Game { // Has game logic, remember to remove temp states for abstraction
	RenderWindow rw;
	GameState cs;

	public Game(RenderWindow window) {
		this.rw = window;

		rw.gw.Load += () => Init();
		rw.gw.UpdateFrame += (e) => Update();
		rw.gw.RenderFrame += (e) => Draw();
		rw.gw.Unload += () => Exit();

		rw.gw.Run();
	}

	void Init() {
		rw.CenterWindow();

		cs = new Triangles();
		cs.Initialize();
	}

	void Update() { }

	void Draw() {
		cs.Draw();

		rw.gw.SwapBuffers();
	}

	void Exit() { }
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

	public override void Draw() {
		GL.Clear(ClearBufferMask.ColorBufferBit);

		_shader.Use();

		GL.BindVertexArray(_vertexArrayObject);
		GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
	}

	public override void Update() { }
}

#endregion

// Primary script logic
new Game(new RenderWindow("Hello World!", new Vector2i(800, 600)));
