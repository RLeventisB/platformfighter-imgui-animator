global using NVector2 = System.Numerics.Vector2;
global using NVector4 = System.Numerics.Vector4;
global using Vector2 = Microsoft.Xna.Framework.Vector2;
global using Vector4 = Microsoft.Xna.Framework.Vector4;

global using static Editor.Gui.ImGuiEx;

using Editor.Geometry;
using Editor.Graphics;
using Editor.Graphics.Grid;
using Editor.Gui;
using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Rune.MonoGame;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Editor
{
	public class State
	{
		public State()
		{
			Textures = new Dictionary<string, TextureFrame>();
			GraphicEntities = new Dictionary<string, TextureEntity>();
			HitboxEntities = new Dictionary<string, HitboxEntity>();
			Animator = new Animator(GraphicEntities, HitboxEntities);
		}

		public Dictionary<string, TextureFrame> Textures { get; set; }
		public Dictionary<string, TextureEntity> GraphicEntities { get; set; }
		public Dictionary<string, HitboxEntity> HitboxEntities { get; set; }
		public Animator Animator { get; set; }

		public TextureFrame GetTexture(string id)
		{
			return Textures.GetValueOrDefault(id, EditorApplication.SinglePixel);
		}
	}

	public class EditorApplication : Game
	{
		public const int SaveFileMagicNumber = 1296649793;
		public static DragAction currentDragAction;

		public static State State;
		public static EditorApplication Instance;
		public static Vector2 previousMousePos, mousePos;
		public static Vector2 previousMouseWorld, mouseWorld, mouseWorldDelta;
		private static SpriteBatch _spriteBatch;
		public static Camera Camera;

		private DynamicGrid _grid;
		private ImGuiRenderer _imguiRenderer;

		// view state

		private FilePickerDefinition _openFdDefinition;
		private PrimitiveBatch _primitiveBatch;
		public string hoveredentityId = string.Empty;
		private bool nextFrameSave;
		private MouseState previousMouseState;

		public string selectedEntityId = string.Empty;
		public string selectedTextureId = string.Empty;
		/// <summary>
		///     0 = Mostrar posiciones adyacentes
		///     1 = Mostrar rotaciones adyacentes
		///     2 = Confirmar nuevo proyecto
		///     3 = Mostrar frame nuevo al mover keyframes
		///     4 = Reproducir al seleccionar keyframe
		/// </summary>
		public static BitArray settingsFlags = new BitArray(12);

		public EditorApplication()
		{
			new GraphicsDeviceManager(this)
			{
				PreferredBackBufferWidth = 1400,
				PreferredBackBufferHeight = 768,
				IsFullScreen = false
			};

			Content.RootDirectory = "Content";
			Window.AllowUserResizing = true;
			IsFixedTimeStep = true;
			IsMouseVisible = true;
			Instance = this;
		}

		public static TextureFrame SinglePixel { get; private set; }

		protected override void Initialize()
		{
			if (File.Exists("./settings.dat"))
			{
				using (FileStream fs = File.OpenRead("./settings.dat"))
				{
					using (BinaryReader reader = new BinaryReader(fs))
					{
						if (reader.BaseStream.Length >= 2)
						{
							settingsFlags = new BitArray(reader.ReadBytes(2));
						}
					}
				}
			}
			else
			{
				settingsFlags = new BitArray(new[] { true, true, true, true, false });
			}

			_grid = new DynamicGrid(new DynamicGridSettings
				{ GridSizeInPixels = 32 });

			Camera = new Camera();

			// offset a bit to show origin at correct position
			Camera.Move((Vector3.UnitX - Vector3.UnitY) * 64);

			State = new State();
			InitializeDefaultState(State);

			base.Initialize();
		}

		private void ResetEditor(State state)
		{
			InitializeDefaultState(state);
			selectedEntityId = string.Empty;
			selectedTextureId = string.Empty;
			hoveredentityId = string.Empty;
		}

		private void InitializeDefaultState(State state)
		{
			state.Animator.OnKeyframeChanged += () =>
			{
				foreach (TextureEntity entity in State.GraphicEntities.Values)
				{
					foreach (KeyframeableValue propertyId in entity.EnumerateKeyframeableValues())
					{
						propertyId.CacheValue(state.Animator.CurrentKeyframe);
					}
				}
			};
		}

		protected override void LoadContent()
		{
			Texture2D singlePixelTexture = new Texture2D(GraphicsDevice, 1, 1);
			SinglePixel = new TextureFrame(singlePixelTexture, string.Empty, NVector2.One, NVector2.One / 2);
			_imguiRenderer = new ImGuiRenderer(this);
			IcoMoon.AddIconsToDefaultFont(14f);
			_imguiRenderer.RebuildFontAtlas();

			ImGui.StyleColorsDark();
			RangeAccessor<NVector4> colors = ImGui.GetStyle().Colors;

			for (int i = 0; i < colors.Count; i++) // nome guta el azul >:(
			{
				ref NVector4 color = ref colors[i];
				float r = color.X;
				float b = color.Z;
				color.Z = r * (1 + b - r);
				color.X = b;
				color.Y /= 1 + b - r;
			}

			_spriteBatch = new SpriteBatch(GraphicsDevice);
			_primitiveBatch = new PrimitiveBatch(GraphicsDevice);

			base.LoadContent();
		}

		protected override void UnloadContent()
		{
			foreach (TextureFrame texturesValue in State.Textures.Values)
			{
				texturesValue.Dispose();
			}

			base.UnloadContent();
		}

		private void Input()
		{
			MouseState newMouseState = Mouse.GetState();
			previousMousePos = previousMouseState.Position.ToVector2();
			previousMouseWorld = Camera.ScreenToWorld(previousMousePos);

			mousePos = newMouseState.Position.ToVector2();
			mouseWorld = Camera.ScreenToWorld(mousePos);

			mouseWorldDelta = mouseWorld - previousMouseWorld;

			previousMouseState = newMouseState;
		}

		protected override void Update(GameTime gameTime)
		{
			ImGuiIOPtr io = ImGui.GetIO();

			if (!io.WantTextInput)
			{
				State.Animator.UpdateTimelineInputs();
			}

			bool onGrid = !io.WantCaptureMouse && !io.WantCaptureKeyboard;

			if (onGrid)
			{
				if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
				{
					Camera.Move(new Vector3(-mouseWorldDelta.X, -mouseWorldDelta.Y, 0));
				}
			}

			Input();

			currentDragAction?.Update();

			_grid.CalculateBestGridSize(Camera.Zoom);

			_grid.CalculateGridData(data =>
			{
				Viewport viewport = GraphicsDevice.Viewport;
				data.GridDim = viewport.Height;

				Vector2 worldTopLeft = Camera.ScreenToWorld(new Vector2(0, 0));
				Vector2 worldTopRight = Camera.ScreenToWorld(new Vector2(viewport.Width, 0));
				Vector2 worldBottomRight = Camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height));
				Vector2 worldBottomLeft = Camera.ScreenToWorld(new Vector2(0, viewport.Height));

				Aabb bounds = new Aabb();
				bounds.Grow(worldTopLeft.X, worldTopLeft.Y, 0);
				bounds.Grow(worldTopRight.X, worldTopRight.Y, 0);
				bounds.Grow(worldBottomRight.X, worldBottomRight.Y, 0);
				bounds.Grow(worldBottomLeft.X, worldBottomLeft.Y, 0);

				return bounds;
			});

			State.Animator.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(new Color(32, 32, 32));

			_primitiveBatch.Begin(Camera.View, Camera.Projection);
			_grid.Render(_primitiveBatch, Matrix.Identity);
			_primitiveBatch.End();

			Vector3 translation = Camera.View.Translation;

			Matrix spriteBatchTransformation = Matrix.CreateTranslation(Camera.lastSize.X / 2, Camera.lastSize.Y / 2, 0) *
			                                   Matrix.CreateTranslation(translation.X, -translation.Y, 0)
			                                 * Matrix.CreateScale(Camera.Zoom);

			_spriteBatch.Begin(transformMatrix: spriteBatchTransformation, samplerState: SamplerState.PointClamp);

			DrawEntities();

			_spriteBatch.End();

			DrawUi(gameTime);
		}

		private static void DrawEntities()
		{
			foreach (TextureEntity entity in State.GraphicEntities.Values)
			{
				Vector2 position = entity.Position.CachedValue;
				position.Y = -position.Y;

				int frameIndex = entity.FrameIndex.CachedValue;
				float rotation = entity.Rotation.CachedValue;
				Vector2 scale = entity.Scale.CachedValue;

				TextureFrame texture = State.GetTexture(entity.TextureId);
				int framesX = (int)(texture.Width / texture.FrameSize.X);

				int x = frameIndex % framesX;
				int y = frameIndex / framesX;

				Rectangle sourceRect = new Rectangle((int)(x * texture.FrameSize.X), (int)(y * texture.FrameSize.Y),
					(int)texture.FrameSize.X, (int)texture.FrameSize.Y);

				_spriteBatch.Draw(texture, position, sourceRect, Color.White,
					rotation, new Vector2(texture.Pivot.X, texture.Pivot.Y),
					scale, SpriteEffects.None, 0f);
			}
		}

		private void DrawSpriteBounds(string entityId, uint color)
		{
			ImDrawListPtr bgDrawList = ImGui.GetBackgroundDrawList();
			TextureEntity textureEntity = State.GraphicEntities[entityId];
			TextureFrame texture = State.GetTexture(textureEntity.TextureId);
			Vector2 position = textureEntity.Position.CachedValue;
			Vector2 scale = textureEntity.Scale.CachedValue * Camera.Zoom;
			float rotation = textureEntity.Rotation.CachedValue;

			Vector2 sp = Camera.WorldToScreen(new Vector2(position.X, position.Y));

			(NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) = GetQuads(sp.X, sp.Y, -texture.Pivot.X * scale.X, -texture.Pivot.Y * scale.Y, texture.FrameSize.X * scale.X, texture.FrameSize.Y * scale.Y, MathF.Sin(rotation), MathF.Cos(rotation));
			bgDrawList.AddQuad(tl, tr, br, bl, color);
		}

		private (NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) GetQuads(float x, float y, float dx, float dy, float w, float h, float sin, float cos)
		{
			NVector2 tl;
			NVector2 tr;
			NVector2 bl;
			NVector2 br;
			tl.X = x + dx * cos - dy * sin;
			tl.Y = y + dx * sin + dy * cos;
			tr.X = x + (dx + w) * cos - dy * sin;
			tr.Y = y + (dx + w) * sin + dy * cos;
			bl.X = x + dx * cos - (dy + h) * sin;
			bl.Y = y + dx * sin + (dy + h) * cos;
			br.X = x + (dx + w) * cos - (dy + h) * sin;
			br.Y = y + (dx + w) * sin + (dy + h) * cos;

			return (tl, tr, bl, br);
		}

		private void DrawUi(GameTime gameTime)
		{
			_imguiRenderer.BeforeLayout(gameTime);

			ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();

			// Draw viewport overlays
			if (!string.IsNullOrEmpty(hoveredentityId))
				DrawSpriteBounds(hoveredentityId, Color.CornflowerBlue.PackedValue);

			if (!string.IsNullOrEmpty(selectedEntityId))
			{
				DrawSpriteBounds(selectedEntityId, Color.Red.PackedValue);

				RenderRotateIcon(drawList);
			}

			if (Timeline.selectedLink != null)
			{
				DrawLinkOverlays(drawList);
			}

			const int hierarchyWindowWidth = 300;

			ImGui.SetNextWindowPos(new NVector2(0, GraphicsDevice.Viewport.Height - 250), ImGuiCond.Always);
			ImGui.SetNextWindowSize(NVector2.UnitX * (GraphicsDevice.Viewport.Width - hierarchyWindowWidth) + NVector2.UnitY * GraphicsDevice.Viewport.Height, ImGuiCond.Always);

			Timeline.DrawUiTimeline(State.Animator);

			ImGui.SetNextWindowPos(new NVector2(GraphicsDevice.Viewport.Width - hierarchyWindowWidth, 0), ImGuiCond.Always);

			ImGui.SetNextWindowSize(NVector2.UnitX * hierarchyWindowWidth +
			                        NVector2.UnitY * GraphicsDevice.Viewport.Height, ImGuiCond.Always);

			ImGui.Begin("Management", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
			{
				DrawUiActions();
				DrawUiHierarchyFrame();
				DrawUiProperties();
			}

			ImGui.End();

			_imguiRenderer.AfterLayout();

			if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow))
			{
				ImGui.SetNextFrameWantCaptureKeyboard(true);
				ImGui.SetNextFrameWantCaptureMouse(true);
			}
		}

		private void DrawLinkOverlays(ImDrawListPtr drawList)
		{
			switch (Timeline.selectedLink.propertyName)
			{
				case PositionProperty when settingsFlags.Get(0): // draw all keyframe 
					unsafe
					{
						Vector2[] linkPreview = (Vector2[])Timeline.selectedLink.extraData;

						fixed (void* ohno = linkPreview)
						{
							drawList.AddPolyline(ref Unsafe.AsRef<NVector2>(ohno), linkPreview.Length, 0xBBBBBBBB, ImDrawFlags.RoundCornersAll, 2);
						}
					}

					foreach (Keyframe keyframe in Timeline.selectedLink.link.Keyframes)
					{
						Vector2 position = Camera.WorldToScreen((Vector2)keyframe.Value);
						bool hover = IsInsideRectangle(position, new Vector2(10), MathHelper.PiOver4, ImGui.GetMousePos());
						drawList.AddNgon(new NVector2(position.X, position.Y), 10, hover ? 0xCCCCCCCC : 0x77777777, 4);

						if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && currentDragAction is null)
						{
							State.Animator.CurrentKeyframe = keyframe.Frame;
							State.Animator.Stop();
							SetDragAction(new DelegateDragAction("DragPositionKeyframe",
								delegate(Vector2 worldDrag, Vector2 _)
								{
									Vector2 pos = (Vector2)keyframe.Value;
									keyframe.Value = pos + worldDrag;
									Timeline.selectedLink.CalculateExtraData();
								}));
						}
					}

					break;
				case RotationProperty when settingsFlags.Get(1):
					for (int index = 0; index < Timeline.selectedLink.link.Keyframes.Count; index++)
					{
						Keyframe keyframe = Timeline.selectedLink.link.Keyframes[index];
						float rotation = ((float[])Timeline.selectedLink.extraData)[index];
						
						Vector2 position = Camera.WorldToScreen((Vector2)keyframe.Value);
						bool hover = IsInsideRectangle(position, new Vector2(10), ImGui.GetMousePos());
						RenderRotationIcon(drawList, position, rotation);
						drawList.AddNgon(new NVector2(position.X, position.Y), 10, hover ? 0xCCCCCCCC : 0x77777777, 4);

						if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && currentDragAction is null)
						{
							State.Animator.CurrentKeyframe = keyframe.Frame;
							State.Animator.Stop();
							SetDragAction(new DelegateDragAction("DragRotationKeyframe",
								delegate
								{
									Vector2 diff = mouseWorld - position;
									keyframe.Value = Math.Atan2(diff.Y, diff.X);
									Timeline.selectedLink.CalculateExtraData();
								}));
						}
					}

					break;
			}
		}
		private void RenderRotationIcon(ImDrawListPtr drawList, Vector2 worldPos, float rotation)
		{
			Vector2 position = Camera.WorldToScreen(worldPos);
			(float sin, float cos) = MathF.SinCos(rotation);

			ImFontGlyphPtr glyph = ImGui.GetIO().Fonts.Fonts[0].FindGlyph(IcoMoon.NextArrowIcon);
			RenderIcon(drawList, glyph, position, sin, cos);
		}

		private void RenderRotateIcon(ImDrawListPtr drawList)
		{
			TextureEntity textureEntity = State.GraphicEntities[selectedEntityId];
			NVector2 textureSize = State.GetTexture(textureEntity.TextureId).FrameSize;
			Vector2 worldPos = textureEntity.Position.CachedValue;
			Vector2 position = Camera.WorldToScreen(worldPos);
			Vector2 scale = textureEntity.Scale.CachedValue * Camera.Zoom;
			float rotation = textureEntity.Rotation.CachedValue;
			(float sin, float cos) = MathF.SinCos(rotation);

			float length = 16f + textureSize.X / 2 * scale.X;
			position.X += cos * length;
			position.Y += sin * length;
			ImFontGlyphPtr glyph = ImGui.GetIO().Fonts.Fonts[0].FindGlyph(IcoMoon.RotateIcon);
			RenderIcon(drawList, glyph, position, sin, cos);
		}

		private unsafe void RenderIcon(ImDrawListPtr drawList, ImFontGlyphPtr glyph, Vector2 position, float sin, float cos)
		{
			float* glyphDataPtr = (float*)glyph.NativePtr; // jaja imgui.net no acepta el commit DE 4 AÑOS que añade bitfields el cual arregla el orden de ImFontGlyph

			(NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) = GetQuads(position.X, position.Y, -8, -8, 16, 16, sin, cos);

			NVector2 uv0 = new NVector2(glyphDataPtr[6], glyphDataPtr[7]);
			NVector2 uv1 = new NVector2(glyphDataPtr[8], glyphDataPtr[7]);
			NVector2 uv2 = new NVector2(glyphDataPtr[8], glyphDataPtr[9]);
			NVector2 uv3 = new NVector2(glyphDataPtr[6], glyphDataPtr[9]);
			drawList.AddImageQuad(_imguiRenderer.fontTextureId.Value, tl, tr, br, bl, uv0, uv1, uv2, uv3, Color.White.PackedValue);
		}

		private void DrawUiActions()
		{
			NVector2 toolbarSize = NVector2.UnitY * (ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2);
			ImGui.Text($"{IcoMoon.HammerIcon} Actions");
			ImGui.BeginChild(1, toolbarSize, ImGuiChildFlags.FrameStyle);
			{
				if (nextFrameSave)
				{
					_openFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly().Location, "Save", ".anim");
					ImGui.OpenPopup("Save project");
					nextFrameSave = false;
				}

				if (DelegateButton("New project", $"{IcoMoon.HammerIcon}", "New project"))
				{
					if (settingsFlags[2])
					{
						ImGui.OpenPopup("Confirmar nuevo proyecto");
					}
					else
					{
						State = new State();
						ResetEditor(State);
					}
				}

				bool popupOpen = true;
				ImGui.SetNextWindowSize(new NVector2(400, 140));

				if (ImGui.BeginPopupModal("Confirmar nuevo proyecto", ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
				{
					// if (ImGui.BeginChild(1, new NVector2(400, 120), ImGuiChildFlags.FrameStyle, ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.NoResize))
					{
						ImGui.Text("EsTAS seGURO de HACER un NUEVO proYECTO ?\neste menu es horrible");

						if (ImGui.Button("si wn deja de gritar"))
						{
							State = new State();
							ResetEditor(State);
							ImGui.CloseCurrentPopup();
						}

						// ImGui.SameLine();
						if (ImGui.Button("deja guardar >:("))
						{
							ImGui.CloseCurrentPopup();
							nextFrameSave = true;
						}

						// ImGui.SameLine();
						if (ImGui.Button("jaja no"))
						{
							ImGui.CloseCurrentPopup();
						}

						// ImGui.EndChild();
					}

					ImGui.EndPopup();
				}

				ImGui.SameLine();

				if (DelegateButton("Save project", $"{IcoMoon.FloppyDiskIcon}", "Save project"))
				{
					_openFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
						.Location, "Save", ".anim");

					ImGui.OpenPopup("Save project");
				}

				DoPopup("Save project", ref _openFdDefinition, () =>
				{
					if (!Path.HasExtension(_openFdDefinition.SelectedRelativePath))
					{
						_openFdDefinition.SelectedRelativePath += ".anim";
					}

					SaveProject();
				});

				ImGui.SameLine();

				if (DelegateButton("Open project", $"{IcoMoon.FolderOpenIcon}", "Open project"))
				{
					_openFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
						.Location, "Open", ".anim");

					ImGui.OpenPopup("Open project");
				}

				DoPopup("Open project", ref _openFdDefinition, LoadProject);

				ImGui.SameLine();

				if (DelegateButton("Settings", $"{IcoMoon.SettingsIcon}", "Ve los ajust :)))"))
				{
					ImGui.OpenPopup("Settings");
				}

				popupOpen = true;
				ImGui.SetNextWindowContentSize(NVector2.One * 600);

				if (ImGui.BeginPopupModal("Settings", ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
				{
					Checkbox("Mostrar posiciones enlazadas", 0);
					Checkbox("Mostrar rotaciones enlazadas", 1);
					Checkbox("Confirmar nuevo proyecto", 2);
					Checkbox("Mostrar frame nuevo al mover keyframes", 3);
					Checkbox("Reproducir al seleccionar keyframe", 4);

					if (ImGui.Button("OK!") || ImGui.IsKeyPressed(ImGuiKey.Enter))
					{
						if (!File.Exists("./settings.dat"))
						{
							File.Create("./settings.dat");
						}

						using (FileStream fs = File.OpenWrite("./settings.dat"))
						{
							using (BinaryWriter writer = new BinaryWriter(fs))
							{
								byte[] bytes = new byte[2];

								for (int i = 0; i < settingsFlags.Count; i++)
								{
									if (settingsFlags.Get(i)) // WHAT UFE FUJCKKFDGKNFB
										bytes[i / 8] ^= (byte)(1 << i % 8);
								}

								writer.Write(bytes);
							}
						}

						ImGui.CloseCurrentPopup();
					}

					ImGui.SameLine();

					if (ImGui.Button("No >:(") || ImGui.IsKeyPressed(ImGuiKey.Escape))
					{
						ImGui.CloseCurrentPopup();
					}

					ImGui.EndPopup();

					void Checkbox(string text, int index)
					{
						bool value = settingsFlags.Get(index);
						ImGui.Checkbox(text, ref value);
						settingsFlags.Set(index, value);
					}
				}

				ImGui.SameLine();
			}

			ImGui.EndChild();
		}

		private void LoadProject()
		{
			using (FileStream stream = File.OpenRead(_openFdDefinition.SelectedRelativePath))
			{
				using (BinaryReader reader = new BinaryReader(stream))
				{
					if (reader.ReadUInt32() == SaveFileMagicNumber)
					{
						ResetEditor(State = new State());

						switch (reader.ReadByte())
						{
							default:
							case 0:
								State.Animator.FPS = reader.ReadInt32();
								int count = reader.ReadInt32();

								for (int i = 0; i < count; i++)
								{
									reader.ReadString();
									reader.ReadString();
								}

								count = reader.ReadInt32();
								State.Textures.EnsureCapacity(count);

								for (int i = 0; i < count; i++)
								{
									string key = reader.ReadString();
									string path = reader.ReadString();
									Texture2D texture = Texture2D.FromFile(GraphicsDevice, path);

									State.Textures.Add(key, new TextureFrame(texture, path, reader.ReadNVector2(), reader.ReadNVector2()));
								}

								count = reader.ReadInt32();
								State.GraphicEntities.EnsureCapacity(count);

								for (int i = 0; i < count; i++)
								{
									string entityId = reader.ReadString();
									string textureId = reader.ReadString();
									TextureEntity textureEntity = new TextureEntity(entityId, textureId);
									State.GraphicEntities.Add(entityId, textureEntity);

									int entityPropertyCount = reader.ReadInt32();

									for (int j = 0; j < entityPropertyCount; j++)
									{
										string propertyName = reader.ReadString();
										int keyframeCount = reader.ReadInt32();

										for (int h = 0; h < keyframeCount; h++)
										{
											int frame = reader.ReadInt32();
											reader.ReadByte();

											switch (propertyName)
											{
												case "Position":
													Vector2 pos = reader.ReadVector2();
													textureEntity.Position.Add(new Keyframe(textureEntity.Position, frame, pos));

													break;
												case "FrameIndex":
													int frameIndex = reader.ReadInt32();
													textureEntity.FrameIndex.Add(new Keyframe(textureEntity.FrameIndex, frame, frameIndex));

													break;
												case "Rotation":
													float rotation = reader.ReadInt32();
													textureEntity.Rotation.Add(new Keyframe(textureEntity.Rotation, frame, rotation));

													break;
												case "Scale":
													Vector2 scale = reader.ReadVector2();
													textureEntity.Scale.Add(new Keyframe(textureEntity.Scale, frame, scale));

													break;
											}
										}
									}
								}

								break;
							case 1:
								State.Animator.FPS = reader.ReadInt32();
								State.Animator.CurrentKeyframe = reader.ReadInt32();

								count = reader.ReadInt32();
								State.Textures.EnsureCapacity(count);

								for (int i = 0; i < count; i++)
								{
									string key = reader.ReadString();
									string path = reader.ReadString();
									Texture2D texture = Texture2D.FromFile(GraphicsDevice, path);

									State.Textures.Add(key, new TextureFrame(texture, path, reader.ReadNVector2(), reader.ReadNVector2()));
								}

								count = reader.ReadInt32();
								State.GraphicEntities.EnsureCapacity(count);

								for (int i = 0; i < count; i++)
								{
									string name = reader.ReadString();
									string textureId = reader.ReadString();
									TextureEntity entity = new TextureEntity(name, textureId);

									ReadSavedKeyframes(reader, entity);

									State.GraphicEntities.Add(name, entity);
								}

								count = reader.ReadInt32();
								State.HitboxEntities.EnsureCapacity(count);

								for (int i = 0; i < count; i++)
								{
									string name = reader.ReadString();
									HitboxEntity entity = new HitboxEntity(name);

									ReadSavedKeyframes(reader, entity);

									State.HitboxEntities.Add(name, entity);
								}

								break;
						}

						State.Animator.OnKeyframeChanged?.Invoke();
					}
				}
			}

			void ReadSavedKeyframes(BinaryReader reader, IEntity entity)
			{
				int propertyCount = reader.ReadInt32();
				List<KeyframeableValue> values = entity.EnumerateKeyframeableValues();

				for (int j = 0; j < propertyCount; j++)
				{
					int keyframeCount = reader.ReadInt32();
					KeyframeableValue value = values[j];

					for (int k = 0; k < keyframeCount; k++)
					{
						int frame = reader.ReadInt32();
						object data = null;

						switch (value.DefaultValue)
						{
							case float:
								data = reader.ReadSingle();

								break;
							case int:
								data = reader.ReadInt32();

								break;
							case Vector2:
								data = reader.ReadVector2();

								break;
						}

						Keyframe keyframe = new Keyframe(value, frame, data);
						value.Add(keyframe);
					}

					int linksCount = reader.ReadInt32();

					for (int k = 0; k < linksCount; k++)
					{
						List<Keyframe> keyframesInLink = new List<Keyframe>();
						InterpolationType type = (InterpolationType)reader.ReadByte();
						bool relativeThing = reader.ReadBoolean();
						keyframeCount = reader.ReadInt32();

						for (int l = 0; l < keyframeCount; l++)
						{
							int frame = reader.ReadInt32();
							keyframesInLink.Add(value.GetKeyframe(frame));
						}

						value.AddLink(new KeyframeLink(value, keyframesInLink)
						{
							InterpolationType = type,
							UseRelativeProgressCalculation = relativeThing
						});
					}
				}
			}
		}

		private void SaveProject()
		{
			using (FileStream stream = File.Open(_openFdDefinition.SelectedRelativePath, FileMode.OpenOrCreate))
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write(SaveFileMagicNumber);
					writer.Write((byte)1);
					writer.Write(State.Animator.FPS);
					writer.Write(State.Animator.CurrentKeyframe);

					writer.Write(State.Textures.Count);

					foreach (KeyValuePair<string, TextureFrame> texture in State.Textures)
					{
						writer.Write(texture.Key);
						writer.Write(texture.Value.Path);
						writer.Write(texture.Value.FrameSize);
						writer.Write(texture.Value.Pivot);
					}

					writer.Write(State.GraphicEntities.Count);

					foreach (TextureEntity entity in State.Animator.RegisteredGraphics)
					{
						writer.Write(entity.Name);
						writer.Write(entity.TextureId);

						SaveEntityKeyframes(entity, writer);
					}

					writer.Write(State.HitboxEntities.Count);

					foreach (HitboxEntity entity in State.Animator.RegisteredHitboxes)
					{
						writer.Write(entity.Name);

						SaveEntityKeyframes(entity, writer);
					}
				}
			}

			void SaveEntityKeyframes(IEntity entity, BinaryWriter writer)
			{
				List<KeyframeableValue> values = entity.EnumerateKeyframeableValues();
				writer.Write(values.Count);

				foreach (KeyframeableValue value in values)
				{
					writer.Write(value.KeyframeCount);

					foreach (Keyframe keyframe in value.keyframes)
					{
						writer.Write(keyframe.Frame);

						switch (keyframe.Value)
						{
							case float floatValue:
								writer.Write(floatValue);

								break;
							case int intValue:
								writer.Write(intValue);

								break;
							case Vector2 vector2Value:
								writer.Write(vector2Value);

								break;
						}
					}

					writer.Write(value.links.Count);

					foreach (KeyframeLink link in value.links)
					{
						writer.Write((byte)link.InterpolationType);
						writer.Write(link.UseRelativeProgressCalculation);
						writer.Write(link.Keyframes.Count);

						foreach (Keyframe linkKeyframes in link.Keyframes)
						{
							writer.Write(linkKeyframes.Frame);
						}
					}
				}
			}
		}

		private void DrawUiHierarchyFrame()
		{
			NVector2 size = ImGui.GetContentRegionAvail();
			NVector2 itemSpacing = ImGui.GetStyle().ItemSpacing + NVector2.UnitY * 8;
			ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);

			ImGui.Text($"{IcoMoon.ListIcon} Hierarchy");
			ImGui.BeginChild(2, size - NVector2.UnitY * 256, ImGuiChildFlags.FrameStyle);

			{
				// create entity
				bool itemHovered = false;
				ImGui.Text($"{IcoMoon.ImagesIcon} Entities");
				ImGui.SameLine();

				if (State.Textures.Count > 0)
				{
					if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##1"))
					{
						ImGui.OpenPopup("Create entity");
						DoEntityCreatorReset();
					}
				}
				else
				{
					DisabledButton($"{IcoMoon.PlusIcon}");
				}

				DoEntityCreatorModal(State.Textures.Keys.ToArray(), (name, selectedTexture) =>
				{
					TextureEntity textureEntity = new TextureEntity(name, selectedTexture);

					State.GraphicEntities[name] = textureEntity;

					selectedEntityId = name;
				});

				// show all created entities
				ImGui.Indent();
				bool onGrid = !ImGui.GetIO().WantCaptureMouse && !ImGui.GetIO().WantCaptureKeyboard;
				bool clickedOnGrid = onGrid && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
				bool hasSelected = false, selectedSame = false;
				string oldSelectedEntityId = selectedEntityId;

				if (!string.IsNullOrEmpty(selectedEntityId))
				{
					TextureEntity textureEntity = State.GraphicEntities[selectedEntityId];
					Vector2 position = textureEntity.Position.CachedValue;

					if (currentDragAction is not null && currentDragAction.ActionName != "RotateWorldObject")
					{
						Vector2 rotatePosition = Camera.WorldToScreen(position);

						float rotation = textureEntity.Rotation.CachedValue;
						Vector2 scale = textureEntity.Scale.CachedValue * Camera.Zoom;
						Vector2 textureSize = State.GetTexture(textureEntity.TextureId).FrameSize;

						(float sin, float cos) = MathF.SinCos(rotation);

						rotatePosition.X += cos * (16f + textureSize.X / 2 * scale.X);
						rotatePosition.Y -= sin * (16f + textureSize.X / 2 * scale.X);

						rotatePosition = Camera.ScreenToWorld(rotatePosition);

						if (onGrid && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.DistanceSquared(rotatePosition, mouseWorld) < 64)
						{
							selectedSame = true;

							SetDragAction(new DelegateDragAction("RotateWorldObject",
								delegate
								{
									Vector2 diff = mouseWorld - position;
									textureEntity.Rotation.SetKeyframeValue(State.Animator.CurrentKeyframe, MathF.Atan2(diff.Y, diff.X));
								}));
						}
					}
				}

				foreach (TextureEntity entity in State.GraphicEntities.Values)
				{
					if (!hasSelected && entity.IsBeingHovered(mouseWorld, State.Animator.CurrentKeyframe) && clickedOnGrid)
					{
						if (selectedEntityId != entity.Name)
						{
							SetDragAction(new DelegateDragAction("MoveWorldObject",
								delegate
								{
									TextureEntity selectedTextureEntity = State.GraphicEntities[selectedEntityId];
									selectedTextureEntity.Position.SetKeyframeValue(State.Animator.CurrentKeyframe, selectedTextureEntity.Position.CachedValue + mouseWorldDelta);
								}));

							selectedEntityId = entity.Name;
							hasSelected = true;
						}
						else
						{
							SetDragAction(new DelegateDragAction("MoveWorldObject",
								delegate
								{
									TextureEntity selectedTextureEntity = State.GraphicEntities[selectedEntityId];
									selectedTextureEntity.Position.SetKeyframeValue(State.Animator.CurrentKeyframe, selectedTextureEntity.Position.CachedValue + mouseWorldDelta);
								}));

							selectedSame = true;
						}
					}

					bool selected = selectedEntityId == entity.Name;
					ImGui.Selectable(entity.Name, ref selected);

					if (selected)
					{
						selectedTextureId = string.Empty;
						selectedEntityId = entity.Name;
					}

					if (!ImGui.IsItemHovered())
						continue;

					itemHovered = true;
					hoveredentityId = entity.Name;
				}

				if (oldSelectedEntityId != selectedEntityId)
				{
					ResetSavedInput();
				}

				ImGui.Unindent();

				if (!itemHovered)
					hoveredentityId = string.Empty;

				// Add textures
				ImGui.Text($"{IcoMoon.TextureIcon} Textures");
				ImGui.SameLine();

				if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##2"))
				{
					_openFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
						.Location, "Open", ".png");

					ImGui.OpenPopup("Load texture");
				}

				DoPopup("Load texture", ref _openFdDefinition, () =>
				{
					string key = Path.GetFileNameWithoutExtension(_openFdDefinition.SelectedFileName);

					if (!State.Textures.ContainsKey(key))
					{
						string path = _openFdDefinition.SelectedRelativePath;
						Texture2D texture = Texture2D.FromFile(GraphicsDevice, path);

						State.Textures[key] = new TextureFrame(texture, path,
							new NVector2(texture.Width, texture.Height),
							new NVector2(texture.Width / 2f, texture.Height / 2f));

						selectedTextureId = key;
					}
				});

				// show all loaded textures
				ImGui.Indent();

				foreach (string texture in State.Textures.Keys)
				{
					bool selected = selectedTextureId == texture;
					ImGui.Selectable(texture, ref selected);

					if (ImGui.IsItemHovered() && ImGui.IsKeyPressed(ImGuiKey.Delete))
					{
						State.Textures.Remove(texture);
						selectedTextureId = string.Empty;
					}
					else if (selected)
					{
						selectedEntityId = string.Empty;
						selectedTextureId = texture;
					}
				}

				ImGui.Unindent();

				ImGui.TreePop();
			}

			ImGui.EndChild();
			ImGui.PopStyleVar();
		}

		private void DrawUiProperties()
		{
			ImGui.Text($"{IcoMoon.EqualizerIcon} Properties");
			ImGui.BeginChild(3, NVector2.UnitY * 208, ImGuiChildFlags.FrameStyle);
			int currentKeyframe = State.Animator.CurrentKeyframe;

			if (!string.IsNullOrEmpty(selectedEntityId))
			{
				TextureEntity selectedTextureEntity = State.GraphicEntities[selectedEntityId];

				string tempEntityName = SavedInput(string.Empty, selectedTextureEntity.Name);
				ImGui.SameLine();

				if (ImGui.Button("Rename") && !State.GraphicEntities.ContainsKey(tempEntityName))
				{
					RenameEntity(selectedTextureEntity, tempEntityName);
					ResetSavedInput();
				}

				ImGui.Separator();

				ImGui.Columns(2);
				ImGui.SetColumnWidth(0, 28);

				ImGui.NextColumn();
				ImGui.Text("All properties");
				ImGui.Separator();
				ImGui.NextColumn();

				int keyframeButtonId = 0;

				foreach (KeyframeableValue keyframeableValue in selectedTextureEntity.EnumerateKeyframeableValues())
				{
					ImGui.PushID(keyframeButtonId++);

					ImGui.PopID();

					ImGui.NextColumn();

					switch (keyframeableValue.Name)
					{
						case ScaleProperty:
						case PositionProperty:
							Vector2 vector2 = ((Vector2KeyframeValue)keyframeableValue).CachedValue;

							NVector2 newVector2 = new NVector2(vector2.X, vector2.Y);

							if (ImGui.DragFloat2(keyframeableValue.Name, ref newVector2))
								keyframeableValue.SetKeyframeValue(State.Animator.CurrentKeyframe, (Vector2)newVector2);

							break;
						case FrameIndexProperty:
							int frameIndex = ((IntKeyframeValue)keyframeableValue).CachedValue;

							TextureFrame texture = State.GetTexture(selectedTextureEntity.TextureId);
							int framesX = (int)(texture.Width / texture.FrameSize.X);
							int framesY = (int)(texture.Height / texture.FrameSize.Y);

							if (ImGui.SliderInt(keyframeableValue.Name, ref frameIndex, 0, framesX * framesY - 1))
								keyframeableValue.SetKeyframeValue(State.Animator.CurrentKeyframe, frameIndex);

							break;
						case RotationProperty:
							float rotation = ((FloatKeyframeValue)keyframeableValue).CachedValue;

							if (ImGui.SliderAngle(keyframeableValue.Name, ref rotation, -360f, 360f, "%.0f deg", ImGuiSliderFlags.NoRoundToFormat))
								keyframeableValue.SetKeyframeValue(State.Animator.CurrentKeyframe, rotation);

							break;
					}

					ImGui.NextColumn();
				}

				if (State.Animator.RegisteredGraphics.EntityHasKeyframeAtFrame(selectedEntityId, currentKeyframe)) //TODO: select interpolation type in menu
				{
					// ImGui.ListBox("")
				}
			}
			else if (!string.IsNullOrEmpty(selectedTextureId))
			{
				const float scale = 2f;
				TextureFrame selectedTexture = State.GetTexture(selectedTextureId);
				NVector2 currentFrameSize = selectedTexture.FrameSize;
				NVector2 currentPivot = selectedTexture.Pivot;

				ImGui.DragFloat2("Framesize", ref currentFrameSize);
				ImGui.DragFloat2("Pivot", ref currentPivot);

				selectedTexture.FrameSize = currentFrameSize;
				selectedTexture.Pivot = currentPivot;

				NVector2 scaledFrameSize = currentFrameSize * scale;
				NVector2 scaledPivot = currentPivot * scale;

				ImGui.BeginChild(2, NVector2.UnitY * 154f, ImGuiChildFlags.FrameStyle);

				NVector2 contentSize = ImGui.GetContentRegionAvail();
				NVector2 center = ImGui.GetCursorScreenPos() + contentSize * 0.5f;
				NVector2 frameStart = center - scaledFrameSize * 0.5f;

				// draw frame size
				ImDrawListPtr drawList = ImGui.GetWindowDrawList();
				drawList.AddRect(frameStart, frameStart + scaledFrameSize, Color.GreenYellow.PackedValue);

				// horizontal line
				drawList.AddLine(center - NVector2.UnitX * scaledFrameSize * 0.5f,
					center + NVector2.UnitX * scaledFrameSize * 0.5f,
					Color.ForestGreen.PackedValue);

				// vertical line
				drawList.AddLine(center - NVector2.UnitY * scaledFrameSize * 0.5f,
					center + NVector2.UnitY * scaledFrameSize * 0.5f,
					Color.ForestGreen.PackedValue);

				// draw pivot
				drawList.AddCircleFilled(frameStart + scaledPivot, 4, Color.White.PackedValue);

				ImGui.EndChild();
			}

			ImGui.EndChild();
		}

		private void RenameEntity(TextureEntity textureEntity, string newName)
		{
			// re-add entity
			string oldName = textureEntity.Name;
			State.GraphicEntities.Remove(oldName);
			State.GraphicEntities[newName] = textureEntity;
			textureEntity.Name = newName;

			if (State.Animator.RegisteredGraphics.ChangeEntityName(oldName, newName))
			{
				foreach (KeyframeableValue value in textureEntity.EnumerateKeyframeableValues())
				{
					//value.Owner = textureEntity;
				}
			}

			selectedEntityId = newName;

			if (!string.IsNullOrEmpty(hoveredentityId))
				hoveredentityId = newName;

			if (selectedEntityId == oldName)
				selectedEntityId = newName;
		}

		private void DoPopup(string id, ref FilePickerDefinition fpd, Action onDone)
		{
			bool popupOpen = true;
			ImGui.SetNextWindowContentSize(NVector2.One * 400);

			if (ImGui.BeginPopupModal(id, ref popupOpen, ImGuiWindowFlags.NoResize))
			{
				if (DoFilePicker(ref fpd))
					onDone?.Invoke();

				ImGui.EndPopup();
			}
		}

		public static void SetDragAction(DragAction action)
		{
			if (currentDragAction is not null)
			{
				return;
			}

			currentDragAction = action;
		}

		public static void SelectLink(KeyframeLink link)
		{
			Timeline.selectedLink = new SelectedLinkData(link, link.linkedValue.Name);
		}
	}
	public record SelectedLinkData
	{
		public SelectedLinkData(KeyframeLink link, string propertyName)
		{
			this.link = link;
			this.propertyName = propertyName;
			CalculateExtraData();
		}

		public KeyframeLink link { get; init; }
		public string propertyName { get; init; }
		public object extraData { get; set; }

		public void CalculateExtraData()
		{
			RemoveIfPresent(ref EditorApplication.Camera.OnDirty, CalculateExtraData);

			switch (propertyName)
			{
				case PositionProperty when EditorApplication.settingsFlags.Get(0):
					List<Vector2> positions = new List<Vector2>();

					AddDelegateOnce(ref EditorApplication.Camera.OnDirty, CalculateExtraData);
					int minFrame = link.FirstKeyframe.Frame;
					int maxFrame = link.LastKeyframe.Frame;

					KeyframeableValue.CacheValueOnInterpolate = false;

					for (int i = minFrame; i <= maxFrame; i++)
					{
						positions.Add(EditorApplication.Camera.WorldToScreen(((Vector2KeyframeValue)link.linkedValue).Interpolate(i)));
					}

					KeyframeableValue.CacheValueOnInterpolate = true;
					extraData = positions.ToArray();

					break;
				case RotationProperty when EditorApplication.settingsFlags.Get(1):
					List<float> rotations = new List<float>();

					foreach (Keyframe keyframe in link.Keyframes)
					{
						rotations.Add((float)keyframe.Value);
					}

					extraData = rotations.ToArray();

					break;
			}

		}
	}
}