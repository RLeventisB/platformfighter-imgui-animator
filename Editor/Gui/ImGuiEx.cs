#region
using ImGuiNET;

using Microsoft.Xna.Framework;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
#endregion

namespace Editor.Gui
{
	public static partial class ImGuiEx
	{
		public const string ScaleProperty = "Scale";
		public const string FrameIndexProperty = "Frame Index";
		public const string RotationProperty = "Rotation";
		public const string PositionProperty = "Position";
		public const string SizeXProperty = "Size X";
		public const string SizeYProperty = "Size Y";

		private static string savingInputString;

		public static bool IsInsideRectangle(Vector2 position, Vector2 size, float rotation, Vector2 point)
		{
			// Translate point to local coordinates of the rectangle
			double localX = point.X - position.X;
			double localY = point.Y - position.Y;

			// Rotate point around the rectangle center by the negative of the rectangle angle
			double cosAngle = Math.Cos(-rotation);
			double sinAngle = Math.Sin(-rotation);
			double rotatedX = localX * cosAngle - localY * sinAngle;
			double rotatedY = localX * sinAngle + localY * cosAngle;

			// Check if the rotated point is inside the unrotated rectangle
			double halfWidth = size.X / 2;
			double halfHeight = size.Y / 2;

			return Math.Abs(rotatedX) <= halfWidth && Math.Abs(rotatedY) <= halfHeight;
		}

		public static bool IsInsideRectangle(Vector2 position, Vector2 size, Vector2 point)
		{
			return Math.Abs(point.X - position.X) <= size.X / 2 && Math.Abs(point.Y - position.Y) <= size.Y / 2;
		}

		// https://en.wikibooks.org/wiki/Cg_Programming/Unity/Hermite_Curves and chatgpt IM SORRY but I cant understand this
		public static float CubicHermiteInterpolate(float[] points, float t)
		{
			// Ensure at least two points exist for interpolation
			if (points.Length < 2)
				throw new ArgumentException("At least two points are required for interpolation.");

			// Calculate tangent vectors
			float[] tangents = CalculateTangents(points);

			// Calculate segment index and local t
			float segment = t * (points.Length - 1);
			int segmentIndex = (int)segment;
			float localT = segment - segmentIndex;

			// Extrapolate if t is outside the range [0, 1]
			if (t < 0)
			{
				segmentIndex = 0;
				localT = t * (points.Length - 1);
			}
			else if (t >= 1)
			{
				segmentIndex = points.Length - 2;
				localT = t * (points.Length - 1) - segmentIndex;
			}

			// Hermite basis functions
			float h1 = 2 * localT * localT * localT - 3 * localT * localT + 1;
			float h2 = -2 * localT * localT * localT + 3 * localT * localT;
			float h3 = localT * localT * localT - 2 * localT * localT + localT;
			float h4 = localT * localT * localT - localT * localT;

			// Interpolate using Hermite spline formula
			float interpolatedPoint = h1 * points[segmentIndex] +
			                          h2 * points[segmentIndex + 1] +
			                          h3 * tangents[segmentIndex] +
			                          h4 * tangents[segmentIndex + 1];

			return interpolatedPoint;
		}

		public static float[] CalculateTangents(float[] points)
		{
			float[] tangents = new float[points.Length];

			// Calculate tangents based on differences between adjacent points
			for (int i = 0; i < points.Length; i++)
			{
				float tangent = 0f;

				if (i > 0)
					tangent += points[i] - points[i - 1];

				if (i < points.Length - 1)
					tangent += points[i + 1] - points[i];

				tangents[i] = tangent;
			}

			return tangents;
		}

		public static Vector2 CubicHermiteInterpolate(Vector2[] points, float t)
		{
			// Ensure at least two points exist for interpolation
			if (points.Length < 2)
				throw new ArgumentException("At least two points are required for interpolation.");

			// Calculate tangent vectors
			Vector2[] tangents = CalculateTangents(points);

			// Calculate segment index and local t
			float segment = t * (points.Length - 1);
			int segmentIndex = (int)segment;
			float localT = segment - segmentIndex;

			// Extrapolate if t is outside the range [0, 1]
			if (t < 0)
			{
				segmentIndex = 0;
				localT = t * (points.Length - 1);
			}
			else if (t >= 1)
			{
				segmentIndex = points.Length - 2;
				localT = t * (points.Length - 1) - segmentIndex;
			}

			// Hermite basis functions
			float h1 = 2 * localT * localT * localT - 3 * localT * localT + 1;
			float h2 = -2 * localT * localT * localT + 3 * localT * localT;
			float h3 = localT * localT * localT - 2 * localT * localT + localT;
			float h4 = localT * localT * localT - localT * localT;

			// Interpolate using Hermite spline formula
			Vector2 interpolatedPoint = h1 * points[segmentIndex] +
			                            h2 * points[segmentIndex + 1] +
			                            h3 * tangents[segmentIndex] +
			                            h4 * tangents[segmentIndex + 1];

			return interpolatedPoint;
		}

		public static Vector2[] CalculateTangents(Vector2[] points)
		{
			Vector2[] tangents = new Vector2[points.Length];

			// Calculate tangents based on differences between adjacent points
			for (int i = 0; i < points.Length; i++)
			{
				Vector2 tangent = Vector2.Zero;

				if (i > 0)
					tangent += points[i] - points[i - 1];

				if (i < points.Length - 1)
					tangent += points[i + 1] - points[i];

				tangents[i] = tangent;
			}

			return tangents;
		}

		public static T Log<T>(this T obj)
		{
			Console.WriteLine(obj);

			return obj;
		}

		public static T AddDelegateOnce<T>(ref T action, T addedAction) where T : Delegate
		{
			if (action == null)
				return action = addedAction;

			if (!action.GetInvocationList().Contains(addedAction))
			{
				return action = (T)Delegate.Combine(action, addedAction);
			}

			return action;
		}

		public static T RemoveIfPresent<T>(ref T action, T addedAction) where T : Delegate
		{
			if (action == null)
				return action = addedAction;

			if (action.GetInvocationList().Contains(addedAction))
			{
				return action = (T)Delegate.RemoveAll(action, addedAction);
			}

			return action;
		}

		public static void Write(this BinaryWriter writer, NVector2 vector2)
		{
			writer.Write(vector2.X);
			writer.Write(vector2.Y);
		}

		public static void Write(this BinaryWriter writer, Vector2 vector2)
		{
			writer.Write(vector2.X);
			writer.Write(vector2.Y);
		}

		public static NVector2 ReadNVector2(this BinaryReader reader)
		{
			return new NVector2(reader.ReadSingle(), reader.ReadSingle());
		}

		public static Vector2 ReadVector2(this BinaryReader reader)
		{
			return new Vector2(reader.ReadSingle(), reader.ReadSingle());
		}

		public static string SavedInput(string id, string defaultInput)
		{
			if (string.IsNullOrEmpty(savingInputString))
				savingInputString = defaultInput;

			ImGui.InputText(id, ref savingInputString, 64);

			return savingInputString;
		}

		public static void ResetSavedInput()
		{
			savingInputString = string.Empty;
		}

		public static bool ToggleButton(string id, string descr, ref bool toggled)
		{
			uint backgroundColor = toggled
				? ImGui.GetColorU32(ImGuiCol.ButtonActive)
				: ImGui.GetColorU32(ImGuiCol.Button);

			ImGui.PushStyleColor(ImGuiCol.Button, backgroundColor);
			bool pressed = ImGui.Button(id);

			if (pressed)
				toggled = !toggled;

			ImGui.PopStyleColor();

			if (!string.IsNullOrEmpty(descr) && ImGui.IsItemHovered())
			{
				ImGui.BeginTooltip();
				ImGui.Text(descr);
				ImGui.EndTooltip();
			}

			return pressed;
		}

		public static bool RoundedToggleButton(string id, ref bool toggled)
		{
			ImGui.Text(id);
			ImGui.SameLine();

			NVector2 screenCursor = ImGui.GetCursorScreenPos();
			ImDrawListPtr drawList = ImGui.GetWindowDrawList();
			bool pressed = false;

			float height = ImGui.GetFrameHeight();
			float width = height * 1.55f;
			float radius = height * 0.5f;

			if (ImGui.InvisibleButton(id, new NVector2(width, height)))
			{
				toggled = !toggled;
				pressed = true;
			}

			uint backgroundColor;

			if (ImGui.IsItemHovered())
			{
				backgroundColor = ImGui.GetColorU32(ImGuiCol.ChildBg);
			}
			else
			{
				backgroundColor = toggled
					? ImGui.GetColorU32(ImGuiCol.ButtonHovered)
					: ImGui.GetColorU32(ImGuiCol.Button);
			}

			drawList.AddRectFilled(screenCursor, new NVector2(screenCursor.X + width, screenCursor.Y + height),
				backgroundColor, radius);

			NVector2 centre = toggled
				? new NVector2(screenCursor.X + width - radius, screenCursor.Y + radius)
				: new NVector2(screenCursor.X + radius, screenCursor.Y + radius);

			drawList.AddCircleFilled(centre, radius - 1.5f, Color.White.PackedValue);

			return pressed;
		}

		public static bool DelegateButton(string id, string text, string descr = null, Action<string> callback = null)
		{
			bool buttonPressed = ImGui.Button(text);

			if (!string.IsNullOrEmpty(descr) && ImGui.IsItemHovered())
			{
				ImGui.BeginTooltip();
				ImGui.Text(descr);
				ImGui.EndTooltip();
			}

			if (buttonPressed)
				callback?.Invoke(id);

			return buttonPressed;
		}

		public static void DisabledButton(string id)
		{
			NVector2 pos = ImGui.GetCursorScreenPos();
			ImGuiStylePtr style = ImGui.GetStyle();
			NVector2 textSize = ImGui.CalcTextSize(id);
			textSize.X += style.FramePadding.X * 2;

			// textSize.Y += style.FramePadding.Y * 2;

			ImGui.GetWindowDrawList()
				.AddRectFilled(pos, pos + textSize, ImGui.GetColorU32(ImGuiCol.FrameBg));

			ImGui.SetCursorScreenPos(pos + NVector2.UnitX * style.FramePadding.X);
			ImGui.TextDisabled(id);
		}

		public static class IcoMoon
		{
			public const char RotateIcon = '\ue984';

			public const char BackwardIcon = '\uea1f';
			public const char ForwardIcon = '\uea20';

			public const char PreviousIcon = '\uea23';
			public const char NextIcon = '\uea24';

			public const char FirstIcon = '\uea21';
			public const char LastIcon = '\uea22';

			public const char LoopIcon = '\uea2d';

			public const char TextureIcon = '\ueacd';
			public const char ImageIcon = '\ue90d';
			public const char ImagesIcon = '\ue90e';

			public const char BranchesIcon = '\ue9bc';
			public const char ListIcon = '\ue9ba';
			public const char EqualizerIcon = '\ue992';
			public const char SettingsIcon = '\ue994';
			public const char HammerIcon = '\ue996';
			public const char KeyIcon = '\ue98d';

			public const char FloppyDiskIcon = '\ue962';
			public const char FolderOpenIcon = '\ue930';

			public const char PlusIcon = '\uea0a';
			public const char MinusIcon = '\uea0b';
			public static ImFontPtr font;

			public static unsafe void AddIconsToDefaultFont(float fontSize)
			{
				const string fontFilePath = "IcoMoon-Free.ttf";
				ushort* rangesIntPtr = (ushort*)NativeMemory.AllocZeroed(new nuint(4));
				rangesIntPtr[0] = '\ue900';
				rangesIntPtr[1] = '\ueaea';

				ImFontConfig* config = ImGuiNative.ImFontConfig_ImFontConfig();
				config->MergeMode = 1;
				config->OversampleH = 3;
				config->OversampleV = 3;
				config->GlyphOffset = NVector2.UnitY * 2;
				config->FontDataOwnedByAtlas = 1;
				config->PixelSnapH = 1;
				config->GlyphMaxAdvanceX = float.MaxValue;
				config->RasterizerMultiply = 1.0f;
				font = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, fontSize, config, (nint)rangesIntPtr);

				// NativeMemory.Free(rangesIntPtr);
			}
		}
	}
}