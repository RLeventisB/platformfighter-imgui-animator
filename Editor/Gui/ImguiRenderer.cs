using ImGuiNET;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Runtime.InteropServices;

using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Editor.Gui
{
	/// <summary>
	/// ImGui renderer for use with XNA-likes (FNA & MonoGame)
	/// </summary>
	public class ImGuiRenderer
	{
		private readonly Game _game;

		// Graphics
		private readonly GraphicsDevice _graphicsDevice;

		private BasicEffect _effect;
		private readonly RasterizerState _rasterizerState;

		private byte[] _vertexData;
		private VertexBuffer _vertexBuffer;
		private int _vertexBufferSize;

		private byte[] _indexData;
		private IndexBuffer _indexBuffer;
		private int _indexBufferSize;

		// Textures
		private readonly Dictionary<IntPtr, Texture2D> _loadedTextures;

		private int _textureId;
		public IntPtr? fontTextureId;

		// Input
		private int _scrollWheelValue;

		private readonly List<(ImGuiKey guiKey, Keys xnaKey)> _keys = new List<(ImGuiKey, Keys)>();

		public ImGuiRenderer(Game game)
		{
			IntPtr context = ImGui.CreateContext();
			ImGui.SetCurrentContext(context);

			_game = game ?? throw new ArgumentNullException(nameof(game));
			_graphicsDevice = game.GraphicsDevice;

			_loadedTextures = new Dictionary<IntPtr, Texture2D>();

			_rasterizerState = new RasterizerState
			{
				CullMode = CullMode.None,
				DepthBias = 0,
				FillMode = FillMode.Solid,
				MultiSampleAntiAlias = false,
				ScissorTestEnable = true,
				SlopeScaleDepthBias = 0
			};

			SetupInput();
		}

		#region ImGuiRenderer
		/// <summary>
		/// Creates a texture and loads the font data from ImGui. Should be called when the <see cref="GraphicsDevice" /> is initialized but before any rendering is done
		/// </summary>
		public unsafe void RebuildFontAtlas()
		{
			// Get font texture from ImGui
			ImGuiIOPtr io = ImGui.GetIO();
			io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);

			// Copy the data to a managed array
			byte[] pixels = new byte[width * height * bytesPerPixel];
			Marshal.Copy((nint)pixelData, pixels, 0, pixels.Length);

			// Create and register the texture as an XNA texture
			Texture2D tex2d = new Texture2D(_graphicsDevice, width, height, false, SurfaceFormat.Color);
			tex2d.SetData(pixels);

			using (FileStream stream = File.OpenWrite("./singlebiome.png"))
			{
				tex2d.SaveAsPng(stream, width, height);
			}

			// Should a texture already have been build previously, unbind it first so it can be deallocated
			if (fontTextureId.HasValue) UnbindTexture(fontTextureId.Value);

			// Bind the new texture to an ImGui-friendly id
			fontTextureId = BindTexture(tex2d);

			// Let ImGui know where to find the texture
			io.Fonts.SetTexID(fontTextureId.Value);
			io.Fonts.ClearTexData(); // Clears CPU side texture data

			// io.Fonts.Build();
		}

		/// <summary>
		/// Creates a pointer to a texture, which can be passed through ImGui calls such as <see cref="MediaTypeNames.Image" />. That pointer is then used by ImGui to let us know what texture to draw
		/// </summary>
		public IntPtr BindTexture(Texture2D texture)
		{
			IntPtr id = new IntPtr(_textureId++);

			_loadedTextures.Add(id, texture);

			return id;
		}

		/// <summary>
		/// Removes a previously created texture pointer, releasing its reference and allowing it to be deallocated
		/// </summary>
		public void UnbindTexture(IntPtr textureId)
		{
			_loadedTextures.Remove(textureId);
		}

		/// <summary>
		/// Sets up ImGui for a new frame, should be called at frame start
		/// </summary>
		public void BeforeLayout(GameTime gameTime)
		{
			ImGui.GetIO().DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

			UpdateInput();

			ImGui.NewFrame();
		}

		/// <summary>
		/// Asks ImGui for the generated geometry data and sends it to the graphics pipeline, should be called after the UI is drawn using ImGui.** calls
		/// </summary>
		public void AfterLayout()
		{
			ImGui.Render();

			RenderDrawData(ImGui.GetDrawData());
		}
		#endregion ImGuiRenderer

		#region Setup & Update
		/// <summary>
		/// Maps ImGui keys to XNA keys. We use this later on to tell ImGui what keys were pressed
		/// </summary>
		protected unsafe void SetupInput()
		{
			ImGuiIOPtr io = ImGui.GetIO();

			void what(ImGuiKey guiKey, Keys xnaKey)
			{
				// io.SetKeyEventNativeData(guiKey, )

				// rangeAccessor[(int)ImGuiKey.Tab - 512].AnalogValue = (int)xnaKey;
				_keys.Add((guiKey, xnaKey));
			}

			what(ImGuiKey.Tab, Keys.Tab);
			what(ImGuiKey.LeftArrow, Keys.Left);
			what(ImGuiKey.RightArrow, Keys.Right);
			what(ImGuiKey.UpArrow, Keys.Up);
			what(ImGuiKey.DownArrow, Keys.Down);
			what(ImGuiKey.PageUp, Keys.PageUp);
			what(ImGuiKey.PageDown, Keys.PageDown);
			what(ImGuiKey.Home, Keys.Home);
			what(ImGuiKey.End, Keys.End);
			what(ImGuiKey.Delete, Keys.Delete);
			what(ImGuiKey.Backspace, Keys.Back);
			what(ImGuiKey.Enter, Keys.Enter);
			what(ImGuiKey.Escape, Keys.Escape);
			what(ImGuiKey.LeftCtrl, Keys.LeftControl);
			what(ImGuiKey.RightCtrl, Keys.RightControl);
			what(ImGuiKey.LeftAlt, Keys.LeftAlt);
			what(ImGuiKey.RightAlt, Keys.RightAlt);

			// do shift and super
			what(ImGuiKey.LeftShift, Keys.LeftShift);
			what(ImGuiKey.RightShift, Keys.RightShift);
			what(ImGuiKey.LeftSuper, Keys.LeftWindows);
			what(ImGuiKey.RightSuper, Keys.RightWindows);
			what(ImGuiKey.A, Keys.A);
			what(ImGuiKey.L, Keys.L);
			what(ImGuiKey.C, Keys.C);
			what(ImGuiKey.V, Keys.V);
			what(ImGuiKey.X, Keys.X);
			what(ImGuiKey.Y, Keys.Y);
			what(ImGuiKey.Z, Keys.Z);
			what(ImGuiKey.H, Keys.H);
			what(ImGuiKey.V, Keys.V);
			what(ImGuiKey.W, Keys.W);

			// MonoGame-specific //////////////////////
			_game.Window.TextInput += (s, a) =>
			{
				if (a.Character == '\t') return;

				io.AddInputCharacter(a.Character);
			};
			///////////////////////////////////////////

			// FNA-specific ///////////////////////////
			//TextInputEXT.TextInput += c =>
			//{
			//    if (c == '\t') return;

			//    ImGui.GetIO().AddInputCharacter(c);
			//};
			///////////////////////////////////////////

			ImFont* font = ImGui.GetIO().Fonts.AddFontDefault();
			font->Scale = 1.2f;
		}

		/// <summary>
		/// Updates the <see cref="Effect" /> to the current matrices and texture
		/// </summary>
		protected Effect UpdateEffect(Texture2D texture)
		{
			_effect ??= new BasicEffect(_graphicsDevice);

			ImGuiIOPtr io = ImGui.GetIO();

			// MonoGame-specific //////////////////////
			float offset = .5f;
			///////////////////////////////////////////

			// FNA-specific ///////////////////////////
			//var offset = 0f;
			///////////////////////////////////////////

			_effect.World = Matrix.Identity;
			_effect.View = Matrix.Identity;
			_effect.Projection = Matrix.CreateOrthographicOffCenter(offset, io.DisplaySize.X + offset, io.DisplaySize.Y + offset, offset, -1f, 1f);
			_effect.TextureEnabled = true;
			_effect.Texture = texture;
			_effect.VertexColorEnabled = true;

			return _effect;
		}

		/// <summary>
		/// Sends XNA input state to ImGui
		/// </summary>
		protected unsafe void UpdateInput()
		{
			ImGuiIOPtr io = new ImGuiIOPtr(ImGuiNative.igGetIO());
			RangeAccessor<bool> mouseDown = io.MouseDown;
			MouseState mouse = Mouse.GetState();

			if (!EditorApplication.Instance.IsActive)
			{
				io.KeyCtrl = io.KeyAlt = io.KeySuper = io.KeyShift = false;
				mouseDown[0] = mouseDown[1] = mouseDown[2] = false;
				io.MouseWheel = 0;
				_scrollWheelValue = mouse.ScrollWheelValue;

				return;
			}

			KeyboardState keyboard = Keyboard.GetState();

			for (int i = 0; i < _keys.Count; i++)
			{
				io.AddKeyEvent(_keys[i].guiKey, keyboard.IsKeyDown(_keys[i].xnaKey));
			}

			io.AddKeyEvent(ImGuiKey.ReservedForModCtrl, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
			io.AddKeyEvent(ImGuiKey.ReservedForModShift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
			io.AddKeyEvent(ImGuiKey.ReservedForModAlt, keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
			io.AddKeyEvent(ImGuiKey.ReservedForModSuper, keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows));

			// io.KeyShift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
			// io.KeyCtrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
			// io.KeyAlt = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
			// io.KeySuper = keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows);

			io.DisplaySize = new NVector2(_graphicsDevice.PresentationParameters.BackBufferWidth, _graphicsDevice.PresentationParameters.BackBufferHeight);
			io.DisplayFramebufferScale = new NVector2(1f, 1f);

			io.MousePos = new NVector2(mouse.X, mouse.Y);

			mouseDown[0] = mouse.LeftButton == ButtonState.Pressed;
			mouseDown[1] = mouse.RightButton == ButtonState.Pressed;
			mouseDown[2] = mouse.MiddleButton == ButtonState.Pressed;

			int scrollDelta = mouse.ScrollWheelValue - _scrollWheelValue;
			io.MouseWheel = scrollDelta > 0 ? 1 : scrollDelta < 0 ? -1 : 0;
			_scrollWheelValue = mouse.ScrollWheelValue;
		}
		#endregion Setup & Update

		#region Internals
		/// <summary>
		/// Gets the geometry as set up by ImGui and sends it to the graphics device
		/// </summary>
		private void RenderDrawData(ImDrawDataPtr drawData)
		{
			// Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, vertex/texcoord/color pointers
			Viewport lastViewport = _graphicsDevice.Viewport;
			Rectangle lastScissorBox = _graphicsDevice.ScissorRectangle;

			_graphicsDevice.BlendFactor = Color.White;
			_graphicsDevice.BlendState = BlendState.NonPremultiplied;
			_graphicsDevice.RasterizerState = _rasterizerState;
			_graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

			// Handle cases of screen coordinates != from framebuffer coordinates (e.g. retina displays)
			drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

			// Setup projection
			_graphicsDevice.Viewport = new Viewport(0, 0, _graphicsDevice.PresentationParameters.BackBufferWidth, _graphicsDevice.PresentationParameters.BackBufferHeight);

			UpdateBuffers(drawData);

			RenderCommandLists(drawData);

			// Restore modified state
			_graphicsDevice.Viewport = lastViewport;
			_graphicsDevice.ScissorRectangle = lastScissorBox;
		}

		private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
		{
			if (drawData.TotalVtxCount == 0)
			{
				return;
			}

			// Expand buffers if we need more room
			if (drawData.TotalVtxCount > _vertexBufferSize)
			{
				_vertexBuffer?.Dispose();

				_vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
				_vertexBuffer = new VertexBuffer(_graphicsDevice, DrawVertexDeclaration.DrawVertDeclaration.Declaration, _vertexBufferSize, BufferUsage.None);
				_vertexData = new byte[_vertexBufferSize * DrawVertexDeclaration.DrawVertDeclaration.Size];
			}

			if (drawData.TotalIdxCount > _indexBufferSize)
			{
				_indexBuffer?.Dispose();

				_indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
				_indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
				_indexData = new byte[_indexBufferSize * sizeof(ushort)];
			}

			// Copy ImGui's vertices and indices to a set of managed byte arrays
			int vtxOffset = 0;
			int idxOffset = 0;

			for (int n = 0; n < drawData.CmdListsCount; n++)
			{
				ImDrawListPtr cmdList = drawData.CmdLists[n];

				fixed (void* vtxDstPtr = &_vertexData[vtxOffset * DrawVertexDeclaration.DrawVertDeclaration.Size])
				fixed (void* idxDstPtr = &_indexData[idxOffset * sizeof(ushort)])
				{
					Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDstPtr, _vertexData.Length, cmdList.VtxBuffer.Size * DrawVertexDeclaration.DrawVertDeclaration.Size);
					Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDstPtr, _indexData.Length, cmdList.IdxBuffer.Size * sizeof(ushort));
				}

				vtxOffset += cmdList.VtxBuffer.Size;
				idxOffset += cmdList.IdxBuffer.Size;
			}

			// Copy the managed byte arrays to the gpu vertex- and index buffers
			_vertexBuffer.SetData(_vertexData, 0, drawData.TotalVtxCount * DrawVertexDeclaration.DrawVertDeclaration.Size);
			_indexBuffer.SetData(_indexData, 0, drawData.TotalIdxCount * sizeof(ushort));
		}

		private void RenderCommandLists(ImDrawDataPtr drawData)
		{
			_graphicsDevice.SetVertexBuffer(_vertexBuffer);
			_graphicsDevice.Indices = _indexBuffer;

			int vtxOffset = 0;
			int idxOffset = 0;

			for (int n = 0; n < drawData.CmdListsCount; n++)
			{
				ImDrawListPtr cmdList = drawData.CmdLists[n];

				for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
				{
					ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[cmdi];

					if (!_loadedTextures.TryGetValue(drawCmd.TextureId, out Texture2D value))
					{
						throw new InvalidOperationException($"Could not find a texture with id '{drawCmd.TextureId}', please check your bindings");
					}

					_graphicsDevice.ScissorRectangle = new Rectangle(
						(int)drawCmd.ClipRect.X,
						(int)drawCmd.ClipRect.Y,
						(int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
						(int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)
					);

					Effect effect = UpdateEffect(value);

					foreach (EffectPass pass in effect.CurrentTechnique.Passes)
					{
						pass.Apply();

#pragma warning disable CS0618 // // FNA does not expose an alternative method.
						_graphicsDevice.DrawIndexedPrimitives(
							primitiveType: PrimitiveType.TriangleList,
							baseVertex: vtxOffset,
							minVertexIndex: 0,
							numVertices: cmdList.VtxBuffer.Size,
							startIndex: idxOffset,
							primitiveCount: (int)drawCmd.ElemCount / 3
						);
#pragma warning restore CS0618
					}

					idxOffset += (int)drawCmd.ElemCount;
				}

				vtxOffset += cmdList.VtxBuffer.Size;
			}
		}
		#endregion Internals
	}
}