global using NVector2 = System.Numerics.Vector2;
global using NVector4 = System.Numerics.Vector4;
global using Vector2 = Microsoft.Xna.Framework.Vector2;
global using Vector4 = Microsoft.Xna.Framework.Vector4;
global using Point = Microsoft.Xna.Framework.Point;

global using static Editor.Gui.ImGuiEx;

using Editor.Geometry;
using Editor.Graphics;
using Editor.Graphics.Grid;
using Editor.Gui;
using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rune.MonoGame;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Vector3 = Microsoft.Xna.Framework.Vector3;

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
		public static GraphicsDevice Graphics => Instance.GraphicsDevice;
		public static DragAction currentDragAction;

		public static State State;
		public static EditorApplication Instance;
		private static SpriteBatch _spriteBatch;

		public static DynamicGrid Grid;
		public static ImGuiRenderer ImguiRenderer;

		// view state

		private PrimitiveBatch _primitiveBatch;
		public static string hoveredEntityName = string.Empty;

		public static string selectedEntityName = string.Empty;
		public static string selectedTextureName = string.Empty;
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
			SettingsManager.Initialize();

			Grid = new DynamicGrid(new DynamicGridSettings
				{ GridSizeInPixels = 32 });

			// offset a bit to show origin at correct position
			Camera.Move((Vector3.UnitX - Vector3.UnitY) * 64);

			ResetEditor();

			base.Initialize();
		}

		public static void ResetEditor()
		{
			State = new State();

			InitializeDefaultState(State);
			selectedEntityName = string.Empty;
			selectedTextureName = string.Empty;
			hoveredEntityName = string.Empty;
		}

		private static void InitializeDefaultState(State state)
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
			ImguiRenderer = new ImGuiRenderer(this);
			SinglePixel = new TextureFrame(singlePixelTexture, string.Empty, new Point(1), NVector2.One / 2);
			IcoMoon.AddIconsToDefaultFont(14f);
			ImguiRenderer.RebuildFontAtlas();

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

		protected override void Update(GameTime gameTime)
		{
			ImGuiIOPtr io = ImGui.GetIO();

			Input.Update();

			if (!io.WantTextInput)
			{
				State.Animator.UpdateTimelineInputs();
			}

			bool onGrid = !io.WantCaptureMouse && !io.WantCaptureKeyboard;

			if (onGrid)
			{
				if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
				{
					Camera.Move(new Vector3(-Input.MouseWorldDelta.X, -Input.MouseWorldDelta.Y, 0));
				}
			}

			currentDragAction?.Update();

			Grid.CalculateBestGridSize(Camera.Zoom);

			Grid.CalculateGridData(data =>
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
			Grid.Render(_primitiveBatch, Matrix.Identity);
			_primitiveBatch.End();

			Vector3 translation = Camera.View.Translation;

			Matrix spriteBatchTransformation = Matrix.CreateTranslation(Camera.lastSize.X / 2, Camera.lastSize.Y / 2, 0) *
			                                   Matrix.CreateTranslation(translation.X, -translation.Y, 0)
			                                 * Matrix.CreateScale(Camera.Zoom);

			ImguiRenderer.BeforeLayout(gameTime);

			_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, spriteBatchTransformation);

			DrawEntities();

			_spriteBatch.End();
			
			ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();

			// Draw viewport overlays
			if (!string.IsNullOrEmpty(hoveredEntityName))
				DrawSpriteBounds(drawList, hoveredEntityName, Color.CornflowerBlue.PackedValue);

			if (!string.IsNullOrEmpty(selectedEntityName))
			{
				DrawSpriteBounds(drawList, selectedEntityName, Color.Red.PackedValue);

				RenderRotateIcon(drawList);
			}

			if (Timeline.selectedLink != null)
			{
				DrawLinkOverlays(drawList);
			}
			
			ImGui.SetNextWindowPos(NVector2.Zero);
			ImGui.SetNextWindowSize(new NVector2(Graphics.Viewport.Width, Graphics.Viewport.Height));
			ImGui.Begin("idkeverything", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground);

			ContextMenu.Draw();

			Timeline.DrawUiTimeline(State.Animator);

			Hierarchy.Draw();

			ImGui.End();
			
			ImguiRenderer.AfterLayout();
		}

		private void DrawEntities()
		{
			foreach (TextureEntity entity in State.GraphicEntities.Values)
			{
				if (entity.IsBeingHovered(Input.MouseWorld, State.Animator.CurrentKeyframe) || ImGui.IsMouseReleased(ImGuiMouseButton.Right))
				{
					ContextMenu.Select(entity);
				}
				
				Color color = new Color(1f, 1f, 1f, entity.Transparency.CachedValue);

				if (color.A == 0)
					continue;

				Vector2 position = entity.Position.CachedValue;
				position.Y = -position.Y;

				int frameIndex = entity.FrameIndex.CachedValue;
				float rotation = entity.Rotation.CachedValue;
				Vector2 scale = entity.Scale.CachedValue;

				TextureFrame texture = State.GetTexture(entity.TextureId);
				int framesX = texture.Width / texture.FrameSize.X;

				int x = frameIndex % framesX;
				int y = frameIndex / framesX;

				Rectangle sourceRect = new Rectangle(x * texture.FrameSize.X, y * texture.FrameSize.Y,
					texture.FrameSize.X, texture.FrameSize.Y);

				_spriteBatch.Draw(texture, position, sourceRect, color,
					rotation, new Vector2(texture.Pivot.X, texture.Pivot.Y),
					scale, SpriteEffects.None, 0f);
			}
		}

		private void DrawSpriteBounds(ImDrawListPtr drawlist, string entityId, uint color)
		{
			TextureEntity textureEntity = State.GraphicEntities[entityId];
			TextureFrame texture = State.GetTexture(textureEntity.TextureId);
			Vector2 position = textureEntity.Position.CachedValue;
			Vector2 scale = textureEntity.Scale.CachedValue * Camera.Zoom;
			float rotation = textureEntity.Rotation.CachedValue;

			Vector2 sp = Camera.WorldToScreen(new Vector2(position.X, position.Y));

			(NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) = GetQuads(sp.X, sp.Y, -texture.Pivot.X * scale.X, -texture.Pivot.Y * scale.Y, texture.FrameSize.X * scale.X, texture.FrameSize.Y * scale.Y, MathF.Sin(rotation), MathF.Cos(rotation));
			drawlist.AddQuad(tl, tr, br, bl, color);
		}

		public static (NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) GetQuads(float x, float y, float pivotX, float pivotY, float w, float h, float sin, float cos)
		{
			NVector2 tl;
			NVector2 tr;
			NVector2 bl;
			NVector2 br;
			tl.X = x + pivotX * cos - pivotY * sin;
			tl.Y = y + pivotX * sin + pivotY * cos;
			tr.X = x + (pivotX + w) * cos - pivotY * sin;
			tr.Y = y + (pivotX + w) * sin + pivotY * cos;
			bl.X = x + pivotX * cos - (pivotY + h) * sin;
			bl.Y = y + pivotX * sin + (pivotY + h) * cos;
			br.X = x + (pivotX + w) * cos - (pivotY + h) * sin;
			br.Y = y + (pivotX + w) * sin + (pivotY + h) * cos;

			return (tl, tr, bl, br);
		}

		private void DrawUi()
		{
			Timeline.DrawUiTimeline(State.Animator);

			Hierarchy.Draw();
		}

		private void DrawLinkOverlays(ImDrawListPtr drawList)
		{
			switch (Timeline.selectedLink.propertyName)
			{
				case PositionProperty when SettingsManager.settingsFlags.Get(0): // draw all keyframe
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
				case RotationProperty when SettingsManager.settingsFlags.Get(1):
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
									Vector2 diff = Input.MouseWorld - position;
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
			TextureEntity textureEntity = State.GraphicEntities[selectedEntityName];
			Point textureSize = State.GetTexture(textureEntity.TextureId).FrameSize;
			Vector2 worldPos = textureEntity.Position.CachedValue;
			Vector2 position = Camera.WorldToScreen(worldPos);
			Vector2 scale = textureEntity.Scale.CachedValue * Camera.Zoom;
			float rotation = textureEntity.Rotation.CachedValue;
			(float sin, float cos) = MathF.SinCos(rotation);

			float length = 16f + textureSize.X / 2f * scale.X;
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
			drawList.AddImageQuad(ImguiRenderer.fontTextureId.Value, tl, tr, br, bl, uv0, uv1, uv2, uv3, Color.White.PackedValue);
		}
		

		public static void RenameEntity(TextureEntity textureEntity, string newName)
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

			selectedEntityName = newName;

			if (!string.IsNullOrEmpty(hoveredEntityName))
				hoveredEntityName = newName;

			if (selectedEntityName == oldName)
				selectedEntityName = newName;
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
			RemoveIfPresent(ref Camera.OnDirty, CalculateExtraData);

			switch (propertyName)
			{
				case PositionProperty:
					List<Vector2> positions = new List<Vector2>();

					AddDelegateOnce(ref Camera.OnDirty, CalculateExtraData);
					int minFrame = link.FirstKeyframe.Frame;
					int maxFrame = link.LastKeyframe.Frame;

					KeyframeableValue.CacheValueOnInterpolate = false;

					for (int i = minFrame; i <= maxFrame; i++)
					{
						positions.Add(Camera.WorldToScreen(((Vector2KeyframeValue)link.linkedValue).Interpolate(i)));
					}

					KeyframeableValue.CacheValueOnInterpolate = true;
					extraData = positions.ToArray();

					break;
				case RotationProperty:
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