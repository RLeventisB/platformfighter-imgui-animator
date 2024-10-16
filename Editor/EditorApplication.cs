﻿global using NVector2 = System.Numerics.Vector2;
global using NVector4 = System.Numerics.Vector4;
global using Vector2 = Microsoft.Xna.Framework.Vector2;
global using Vector4 = Microsoft.Xna.Framework.Vector4;
global using Point = Microsoft.Xna.Framework.Point;

global using static Editor.Gui.ImGuiEx;

using Editor.Graphics;
using Editor.Graphics.Grid;
using Editor.Gui;
using Editor.Model;
using Editor.Objects;

using ImGuiNET;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rune.MonoGame;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace Editor
{
	public class State
	{
		public State()
		{
			Textures = new Dictionary<string, TextureFrame>();
			GraphicEntities = new Dictionary<string, TextureAnimationObject>();
			HitboxEntities = new Dictionary<string, HitboxAnimationObject>();
			Animator = new Animator(GraphicEntities, HitboxEntities);
		}

		public Dictionary<string, TextureFrame> Textures { get; set; }
		public Dictionary<string, TextureAnimationObject> GraphicEntities { get; set; }
		public Dictionary<string, HitboxAnimationObject> HitboxEntities { get; set; }
		public Animator Animator { get; set; }
		public bool HasAnyEntity => GraphicEntities.Count > 0 || HitboxEntities.Count > 0;

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
		private static SpriteBatch spriteBatch;
		public static PrimitiveBatch primitiveBatch;

		public static DynamicGrid Grid;
		public static ImGuiRenderer ImguiRenderer;

		// view state

		public static string hoveredEntityName = string.Empty;

		public static SelectionData selectedData = new SelectionData();

		public EditorApplication()
		{
			if (File.Exists("./imgui.ini"))
			{
				string[] iniData = File.ReadAllLines("./imgui.ini");
				int windowDataIndex = iniData.ToList().FindIndex(v => v.Contains("[Window][World View]"));
				string sizeData;

				if (windowDataIndex != -1 && iniData.Length > windowDataIndex + 2 && (sizeData = iniData[windowDataIndex + 2]).Contains("Size="))
				{
					string[] resolutionSize = sizeData.Substring(5).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

					if (resolutionSize.Length == 2 && int.TryParse(resolutionSize[0], out int width) && int.TryParse(resolutionSize[1], out int height))
					{
						new GraphicsDeviceManager(this)
						{
							IsFullScreen = false,
							PreferredBackBufferWidth = width,
							PreferredBackBufferHeight = height
						};

						goto skipNormalConstructor;
					}
				}
			}

			new GraphicsDeviceManager(this)
			{
				IsFullScreen = false
			};

		skipNormalConstructor:
			Content.RootDirectory = "Content";
			Window.AllowUserResizing = true;
			IsFixedTimeStep = true;
			IsMouseVisible = true;
			Instance = this;

			ExternalActions.GetCurrentFrame = () => State.Animator.CurrentKeyframe;
			ExternalActions.OnChangeLinkPropertyProperty = link =>
			{
				if (Timeline.selectedLink != null && Timeline.selectedLink.link == link)
				{
					Timeline.selectedLink.CalculateExtraData();
				}
			};

			ExternalActions.OnDeleteLink = link =>
			{
				if (Timeline.selectedLink != null && Timeline.selectedLink.link == link)
					Timeline.selectedLink = null;
			};

			ExternalActions.AreKeyframesSetOnModify = () => SettingsManager.SetKeyframeOnModify;
			ExternalActions.AreKeyframesAddedToLinkOnModify = () => SettingsManager.AddKeyframeToLinkOnModify;
			ExternalActions.GetTextureFrameByName = name => State.Textures.GetValueOrDefault(name, SinglePixel);
		}

		public static TextureFrame SinglePixel { get; private set; }

		protected override void Initialize()
		{
			SettingsManager.Initialize();

			Grid = new DynamicGrid(new DynamicGridSettings
				{ GridSizeInPixels = 32 });

			ResetEditor();

			base.Initialize();
		}

		public static void ResetEditor()
		{
			if (State?.Textures != null)
				foreach (TextureFrame texture in State.Textures.Values.Where(texture => texture.IsBinded))
				{
					TextureManager.UnloadTexture(texture.Path);
				}

			State = new State();

			InitializeDefaultState(State);
			selectedData.Empty();
			hoveredEntityName = string.Empty;
		}

		private static void InitializeDefaultState(State state)
		{
			SettingsManager.lastProjectSavePath = null;
			state.Animator.OnKeyframeChanged += () =>
			{
				foreach (TextureAnimationObject entity in State.GraphicEntities.Values)
				{
					foreach (KeyframeableValue propertyId in entity.EnumerateKeyframeableValues())
					{
						propertyId.CacheValue(state.Animator.CurrentKeyframe);
					}
				}

				foreach (HitboxAnimationObject entity in State.HitboxEntities.Values)
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

			SinglePixel = new TextureFrame("SinglePixel", singlePixelTexture, new Point(1), null, NVector2.One / 2);
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

			spriteBatch = new SpriteBatch(GraphicsDevice);
			primitiveBatch = new PrimitiveBatch(GraphicsDevice);

			base.LoadContent();
		}

		protected override void UnloadContent()
		{
			foreach (TextureFrame texturesValue in State.Textures.Values)
			{
				TextureManager.UnloadTexture(texturesValue.Path);
			}

			base.UnloadContent();
		}

		protected override void Update(GameTime gameTime)
		{
			ImGuiIOPtr io = ImGui.GetIO();

			io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
			Input.Update();

			if (!io.WantTextInput)
			{
				State.Animator.UpdateTimelineInputs();
			}

			currentDragAction?.Update();

			State.Animator.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(new Color(32, 32, 32));

			ImguiRenderer.BeforeLayout(gameTime);

			/*ImGui.Text("Screen: " + Input.MousePos);
			ImGui.Text("Projected:" + Vector2.Transform(Input.MousePos, Camera.Projection));
			ImGui.Text("World:" + Camera.ScreenToWorld(Input.MousePos));
			ImGui.Text("World:" + Camera.WorldToScreen(Camera.ScreenToWorld(Input.MousePos)));*/
			ImGui.SetNextWindowPos(NVector2.Zero);
			ImGui.SetNextWindowSize(new NVector2(Graphics.Viewport.Width, Graphics.Viewport.Height));
			ImGui.Begin("World View", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus);

			// welcome to the worst code flow ever imaginated because i dont want to do double loops or something

			WorldActions.Draw(); // since this has the camera panning this get called first

			if (Camera.Zoom.TickDamping())
				Camera.isViewDirty = true;

			Timeline.TimelineZoom.TickDamping();
			primitiveBatch.Begin(Camera.View, Camera.Projection); // look!!! two begins!!
			Grid.Render(primitiveBatch, Matrix.Identity);
			primitiveBatch.End();

			ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();

			DrawEntities();

			// Draw viewport overlays
			primitiveBatch.Begin(Camera.View, Camera.Projection); // look!!! two begins!!

			if (!string.IsNullOrEmpty(hoveredEntityName))
				DrawSpriteBounds(hoveredEntityName, Color.CornflowerBlue); // this could probably be moved to primitivebatch

			if (selectedData.Type == SelectionType.Graphic)
			{
				foreach (SelectedObject obj in selectedData)
				{
					DrawSpriteBounds(obj.Name, Color.Red);
				}
			}

			if (Timeline.selectedLink != null)
			{
				DrawLinkOverlays(drawList);
			}

			primitiveBatch.End();

			ImGui.End();

			Timeline.DrawUiTimeline(State.Animator);

			Hierarchy.Draw();

			ImguiRenderer.AfterLayout();
		}

		private void DrawEntities()
		{
			if (Timeline.HitboxMode)
			{
				DrawGraphicEntities();
				DrawHitboxEntities();
			}
			else
			{
				DrawHitboxEntities();
				DrawGraphicEntities();
			}
		}

		private static void DrawHitboxEntities()
		{
			primitiveBatch.Begin(Camera.View, Camera.Projection);

			foreach (HitboxAnimationObject entity in State.HitboxEntities.Values.Where(v => v.IsOnFrame(State.Animator.CurrentKeyframe)))
			{
				Vector2 position = entity.Position.CachedValue;
				Vector2 size = entity.Size.CachedValue;

				Vector3 center = new Vector3(position, 0);
				Vector3 size3d = new Vector3(size, 0);
				Color color = entity.GetColor().MultiplyRGB(Timeline.HitboxMode ? 1 : 0.5f);

				bool isSelected = selectedData.Contains(entity);

				if (isSelected)
				{
					color = color.MultiplyRGB(1.4f);
				}

				primitiveBatch.DrawBox(center, size3d, color.MultiplyAlpha(0.2f));

				HitboxLine selectedLine = isSelected ? entity.GetSelectedLine(Input.MouseWorld) : HitboxLine.None;
				Vector3[] points =
				[
					new Vector3(position.X - size.X / 2, position.Y - size.Y / 2, 0),
					new Vector3(position.X + size.X / 2, position.Y - size.Y / 2, 0),
					new Vector3(position.X + size.X / 2, position.Y + size.Y / 2, 0),
					new Vector3(position.X - size.X / 2, position.Y + size.Y / 2, 0)
				];

				for (int i = 0; i < 4; i++)
				{
					primitiveBatch.DrawLine(points[i], points[(i + 1) % 4], (int)selectedLine == i ? Color.Pink : color);
				}

				if (entity.Type is HitboxType.Hitbox)
				{
					float linesAlpha = isSelected ? 1 : 0.5f;

					// draw launch lines

					float angle = MathHelper.ToRadians(entity.LaunchAngle);

					if (entity.LaunchPoint.LengthSquared() != 0)
					{
						Vector2 diff = entity.LaunchPoint - position;
						angle = MathF.Atan2(diff.Y, diff.X);
						primitiveBatch.DrawBox(new Vector3(entity.LaunchPoint, 0), new Vector3(3, 3, 0), Color.Beige, PrimitiveBatch.DrawStyle.Wireframe);
					}

					(float launchSin, float launchCos) = MathF.SinCos(angle);
					Vector3 lineEnd;

					if (entity.LaunchPotencyGrowth != 0)
					{
						float launchPotency100 = entity.LaunchPotency + entity.LaunchPotencyGrowth * 100;
						lineEnd = center + new Vector3(launchCos * launchPotency100, launchSin * launchPotency100, 0);
						primitiveBatch.DrawLine(center, lineEnd, Color.Red.MultiplyAlpha(0.6f * linesAlpha));
					}

					lineEnd = center + new Vector3(launchCos * entity.LaunchPotency, launchSin * entity.LaunchPotency, 0);
					primitiveBatch.DrawLine(center, lineEnd, Color.White.MultiplyAlpha(linesAlpha));

					// draw shield launch line
					(launchSin, launchCos) = MathF.SinCos(MathHelper.ToRadians(entity.ShieldLaunchAngle));

					lineEnd = center + new Vector3(launchCos * entity.ShieldPotency, launchSin * entity.ShieldPotency, 0);
					primitiveBatch.DrawLine(center, lineEnd, Color.LightSkyBlue.MultiplyAlpha(linesAlpha));
				}
			}

			primitiveBatch.End();
		}

		private static void DrawGraphicEntities()
		{
			Matrix spriteBatchTransformation =
				Camera.View;

			spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, spriteBatchTransformation);

			foreach (TextureAnimationObject entity in State.GraphicEntities.Values)
			{
				Color color = new Color(1f, 1f, 1f, entity.Transparency.CachedValue);

				if (Timeline.HitboxMode)
				{
					color.A = (byte)(color.A * 0.5f);
				}

				if (color.A == 0)
					continue;

				Vector2 position = entity.Position.CachedValue;
				int frameIndex = entity.FrameIndex.CachedValue;
				float rotation = entity.Rotation.CachedValue;
				Vector2 scale = entity.Scale.CachedValue;
				SpriteEffects effects = SpriteEffects.None;

				if (scale.X < 0)
				{
					scale.X = -scale.X;
					effects |= SpriteEffects.FlipHorizontally;
				}

				if (scale.Y < 0)
				{
					scale.Y = -scale.Y;
					effects |= SpriteEffects.FlipVertically;
				}

				TextureFrame textureFrame = State.GetTexture(entity.TextureName);
				Texture2D texture = ImguiRenderer.GetTexture(TextureManager.PathIndexMap[textureFrame.Path].ImguiId);
				int framesX = texture.Width / textureFrame.FrameSize.X;
				if (framesX == 0)
					framesX = 1;

				int x = frameIndex % framesX;
				int y = frameIndex / framesX;

				Rectangle sourceRect = new Rectangle(textureFrame.FramePosition.X + x * textureFrame.FrameSize.X, textureFrame.FramePosition.Y + y * textureFrame.FrameSize.Y,
					textureFrame.FrameSize.X, textureFrame.FrameSize.Y);

				spriteBatch.Draw(texture, position, sourceRect, color,
					rotation, textureFrame.Pivot,
					scale, effects, entity.ZIndex.CachedValue);
			}

			spriteBatch.End();
		}

		private void DrawSpriteBounds(string entityId, Color color)
		{
			TextureAnimationObject textureAnimationObject = State.GraphicEntities[entityId];
			TextureFrame texture = State.GetTexture(textureAnimationObject.TextureName);
			Vector2 position = textureAnimationObject.Position.CachedValue;
			Vector2 scale = textureAnimationObject.Scale.CachedValue;
			float rotation = textureAnimationObject.Rotation.CachedValue;

			(Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br) = GetQuads(position.X, position.Y, -texture.Pivot.X * scale.X, -texture.Pivot.Y * scale.Y, texture.FrameSize.X * scale.X, texture.FrameSize.Y * scale.Y, MathF.Sin(rotation), MathF.Cos(rotation));
			primitiveBatch.DrawPolygon([tl, tr, br, bl], color);
		}

		public static (Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br) GetQuads(float x, float y, float pivotX, float pivotY, float w, float h, float sin, float cos)
		{
			MiscellaneousFunctions.GetQuadsPrimitive(x, y, pivotX, pivotY, w, h, sin, cos,
				out float tlX, out float tlY,
				out float trX, out float trY,
				out float blX, out float blY,
				out float brX, out float brY
			);

			return (new Vector3(tlX, tlY, 0), new Vector3(trX, trY, 0), new Vector3(blX, blY, 0), new Vector3(brX, brY, 0));
		}

		public static (NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) GetQuadsNumeric(float x, float y, float pivotX, float pivotY, float w, float h, float sin, float cos)
		{
			MiscellaneousFunctions.GetQuadsPrimitive(x, y, pivotX, pivotY, w, h, sin, cos,
				out float tlX, out float tlY,
				out float trX, out float trY,
				out float blX, out float blY,
				out float brX, out float brY
			);

			return (new NVector2(tlX, tlY), new NVector2(trX, trY), new NVector2(blX, blY), new NVector2(brX, brY));
		}

		private void DrawLinkOverlays(ImDrawListPtr drawList)
		{
			switch (Timeline.selectedLink.propertyName)
			{
				case PropertyNames.PositionProperty when SettingsManager.ShowPositionLinks: // draw all keyframe
					unsafe
					{
						Vector2[] linkPreview = (Vector2[])Timeline.selectedLink.extraData;

						fixed (void* ohno = linkPreview)
						{
							drawList.AddPolyline(ref Unsafe.AsRef<NVector2>(ohno), linkPreview.Length, 0xBBBBBBBB, ImDrawFlags.RoundCornersAll, 2);
						}
					}

					foreach (Keyframe keyframe in Timeline.selectedLink.link.GetKeyframes())
					{
						Vector2 position = Camera.WorldToScreen((Vector2)keyframe.Value);
						bool hover = MiscellaneousFunctions.IsInsideRectangle(position, new Vector2(10), MathHelper.PiOver4, ImGui.GetMousePos());
						drawList.AddNgon(new NVector2(position.X, position.Y), 10, hover ? 0xCCCCCCCC : 0x77777777, 4);

						if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && currentDragAction is null)
						{
							State.Animator.CurrentKeyframe = keyframe.Frame;
							State.Animator.Stop();
							SetDragAction(new DelegateDragAction("DragPositionKeyframe",
								delegate(Vector2 worldDrag, Vector2 _)
								{
									Vector2 pos = (Vector2)keyframe.Value;
									keyframe.Value = pos + worldDrag / Camera.Zoom;
									Timeline.selectedLink.CalculateExtraData();
								}));
						}
					}

					break;
				case PropertyNames.RotationProperty when SettingsManager.ShowRotationLinks:
					for (int index = 0; index < Timeline.selectedLink.link.Frames.Count; index++)
					{
						int frame = Timeline.selectedLink.link.Frames[index];
						float rotation = ((float[])Timeline.selectedLink.extraData)[index];

						// TODO: implement
					}

					break;
			}
		}

		private unsafe void RenderIcon(ImDrawListPtr drawList, ImFontGlyphPtr glyph, Vector2 position, float sin, float cos)
		{
			float* glyphDataPtr = (float*)glyph.NativePtr; // jaja imgui.net no acepta el commit DE 4 AÑOS que añade bitfields el cual arregla el orden de ImFontGlyph

			(NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) = GetQuadsNumeric(position.X, position.Y, -8, -8, 16, 16, sin, cos);

			NVector2 uv0 = new NVector2(glyphDataPtr[6], glyphDataPtr[7]);
			NVector2 uv1 = new NVector2(glyphDataPtr[8], glyphDataPtr[7]);
			NVector2 uv2 = new NVector2(glyphDataPtr[8], glyphDataPtr[9]);
			NVector2 uv3 = new NVector2(glyphDataPtr[6], glyphDataPtr[9]);
			drawList.AddImageQuad(ImguiRenderer.fontTextureId.Value, tl, tr, br, bl, uv0, uv1, uv2, uv3, Color.White.PackedValue);
		}

		public static void RenameEntity(IAnimationObject animationObject, string newName)
		{
			string oldName = animationObject.Name;

			// re-add entity
			switch (animationObject)
			{
				case HitboxAnimationObject:
					State.Animator.RegisteredHitboxes.ChangeEntityName(oldName, newName);

					break;
				case TextureAnimationObject:
					State.Animator.RegisteredGraphics.ChangeEntityName(oldName, newName);

					break;
			}

			selectedData.Set(animationObject);
		}

		public static void RenameTexture(TextureFrame textureFrame, string newName)
		{
			string oldName = textureFrame.Name;
			State.Textures.Remove(oldName);
			State.Textures[newName] = textureFrame;
			textureFrame.Name = newName;

			foreach (TextureAnimationObject entity in State.GraphicEntities.Values)
			{
				if (entity.TextureName == oldName)
				{
					entity.TextureName = newName;
				}
			}

			selectedData.Set(textureFrame);
		}

		public static void SelectLink(KeyframeLink link)
		{
			Timeline.selectedLink = new SelectedLinkData(link, link.ContainingValue.Name);
		}

		public static void SetDragAction(DragAction action)
		{
			if (currentDragAction is not null)
			{
				return;
			}

			currentDragAction = action;
		}

		public static JsonData GetJsonObject()
		{
			return new JsonData
			(
				State.Animator.Looping,
				State.Animator.Playing,
				State.Animator.PlayingBackward,
				State.Animator.FPS,
				State.Animator.CurrentKeyframe,
				State.Textures.Values.ToArray(),
				State.GraphicEntities.Values.ToArray(),
				State.HitboxEntities.Values.ToArray()
			);
		}

		public static void ApplyJsonData(JsonData data)
		{
			ResetEditor();

			foreach (TextureFrame texture in data.textures)
			{
				TextureManager.LoadTexture(texture.Path, out nint id);
				texture.TextureId = id;
				State.Textures.Add(texture.Name, texture);
			}

			foreach (TextureAnimationObject graphicObject in data.graphicObjects)
			{
				RemoveInvalidLinks(graphicObject);
				State.GraphicEntities.Add(graphicObject.Name, graphicObject);
			}

			foreach (HitboxAnimationObject hitboxObject in data.hitboxObjects)
			{
				RemoveInvalidLinks(hitboxObject);
				State.HitboxEntities.Add(hitboxObject.Name, hitboxObject);
			}

			State.Animator.FPS = data.selectedFps;
			State.Animator.CurrentKeyframe = 0;
			State.Animator.CurrentKeyframe = data.currentKeyframe;
			State.Animator.Looping = data.looping;
			State.Animator.PlayingForward = data.playingForward;
			State.Animator.PlayingBackward = data.playingBackwards;
		}

		public static void RemoveInvalidLinks(IAnimationObject animationObject)
		{
			foreach (KeyframeableValue value in animationObject.EnumerateKeyframeableValues())
			{
				for (int index = 0; index < value.links.Count; index++)
				{
					KeyframeLink link = value.links[index];
					link.SanitizeValues();
					List<int> frames = link.Frames.ToList();
					frames.RemoveAll(v => !value.HasKeyframeAtFrame(v));
					link = new KeyframeLink(link.ContainingValue, frames);

					if (link.Count >= 2)
						continue;

					value.links.RemoveAt(index);
					index--;
				}
			}
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

			KeyframeableValue containingValue = link.ContainingValue;

			if (containingValue is null)
			{
				Timeline.selectedLink = null;

				return;
			}

			switch (propertyName)
			{
				case PropertyNames.PositionProperty:

					List<Vector2> positions = new List<Vector2>();

					AddDelegateOnce(ref Camera.OnDirty, CalculateExtraData);
					int minFrame = link.FirstKeyframe.Frame;
					int maxFrame = link.LastKeyframe.Frame;

					KeyframeableValue.CacheValueOnInterpolate = false;

					for (int i = minFrame; i <= maxFrame; i++)
					{
						positions.Add(Camera.WorldToScreen(((Vector2KeyframeValue)containingValue).Interpolate(i)));
					}

					KeyframeableValue.CacheValueOnInterpolate = true;
					extraData = positions.ToArray();

					break;
				case PropertyNames.RotationProperty:
					List<float> rotations = new List<float>();

					foreach (Keyframe keyframe in link.GetKeyframes())
					{
						rotations.Add((float)keyframe.Value);
					}

					extraData = rotations.ToArray();

					break;
			}
		}
	}
}