using ImGuiNET;

using Microsoft.Xna.Framework;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Editor.Gui
{
	public static partial class ImGuiEx
	{
		private static string savingInputString;

		public static bool DragUshort(string name, ref ushort value, float speed = 1)
		{
			unsafe
			{
				GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);
				bool changed = ImGui.DragScalar(name, ImGuiDataType.U16, handle.AddrOfPinnedObject(), speed);

				if (changed)
				{
					value = *(ushort*)handle.AddrOfPinnedObject();
				}

				handle.Free();

				return changed;
			}
		}

		public static bool DragAngleWithWidget(string name, ref float angle, Action<float> setAngle, float speed = 1)
		{
			ImDrawListPtr drawList = ImGui.GetWindowDrawList();
			float height = 35;

			// ImGui.SetCursorScreenPos(new NVector2(cursorPos.X, cursorPos.Y));

			NVector2 cursorPos = ImGui.GetCursorScreenPos();
			NVector2 center = cursorPos + new NVector2(height / 2);

			ImGui.PushItemFlag(ImGuiItemFlags.ButtonRepeat, true);

			if (ImGui.Button("##AngleButton", new NVector2(height)))
			{
				EditorApplication.SetDragAction(new ChangeHitboxAngleAction(center, angle, setAngle));
			}

			ImGui.SameLine();

			bool changed = ImGui.DragFloat(name, ref angle, speed);

			ImGui.PopItemFlag();

			drawList.AddCircle(center, height / 2, Color.White.PackedValue);
			(float sin, float cos) = MathF.SinCos(MathHelper.ToRadians(angle));
			NVector2 lineEnd = cursorPos + new NVector2(height / 2 * (1 + cos), height / 2 * (1 + sin));
			drawList.AddLine(center, lineEnd, Color.White.PackedValue);

			return changed;
		}

		public static Vector2 Rotate(Vector2 v, float degrees)
		{
			switch (degrees % 360)
			{
				case 0f:
					return v;
				case 90f:
					(v.X, v.Y) = (-v.Y, v.X);

					return v;
				case 180f:
					return -v;
				case 270f:
					(v.X, v.Y) = (v.Y, -v.X);

					return v;
				default:
					(float Sin, float Cos) = MathF.SinCos(degrees * (MathHelper.Pi / 180f));

					float tx = v.X;
					float ty = v.Y;
					v.X = Cos * tx - Sin * ty;
					v.Y = Sin * tx + Cos * ty;

					return v;
			}
		}

		public static float Modulas(float input, float divisor) => (input % divisor + divisor) % divisor;

		public static int Modulas(int input, int divisor) => (input % divisor + divisor) % divisor;

		public static short Modulas(short input, int divisor) => (short)((input % divisor + divisor) % divisor);

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

		public static void Write(this BinaryWriter writer, Point point)
		{
			writer.Write(point.X);
			writer.Write(point.Y);
		}

		public static NVector2 ReadNVector2(this BinaryReader reader)
		{
			return new NVector2(reader.ReadSingle(), reader.ReadSingle());
		}

		public static Point ReadPoint(this BinaryReader reader)
		{
			return new Point(reader.ReadInt32(), reader.ReadInt32());
		}

		public static Vector2 ReadVector2(this BinaryReader reader)
		{
			return new Vector2(reader.ReadSingle(), reader.ReadSingle());
		}

		public static void WriteVector2(this Utf8JsonWriter writer, string propertyName, Vector2 vector2)
		{
			writer.WritePropertyName(propertyName);
			writer.WriteStartObject();
			writer.WriteNumber("x", vector2.X);
			writer.WriteNumber("y", vector2.Y);
			writer.WriteEndObject();
		}

		public static void WritePoint(this Utf8JsonWriter writer, string propertyName, Point point)
		{
			writer.WritePropertyName(propertyName);
			writer.WriteStartObject();
			writer.WriteNumber("x", point.X);
			writer.WriteNumber("y", point.Y);
			writer.WriteEndObject();
		}

		public static void WriteNVector2(this Utf8JsonWriter writer, string propertyName, NVector2 vector2)
		{
			writer.WritePropertyName(propertyName);
			writer.WriteStartObject();
			writer.WriteNumber("x", vector2.X);
			writer.WriteNumber("y", vector2.Y);
			writer.WriteEndObject();
		}

		public static void WriteEnum<T>(this Utf8JsonWriter writer, string propertyName, T value) where T : struct, Enum
		{
			writer.WriteString(propertyName, Enum.GetName(value) ?? string.Empty);
		}

		public static T? ReadEnum<T>(this Utf8JsonReader reader, string propertyName, T value) where T : struct, Enum
		{
			if (reader.GetString()!.Equals(propertyName))
			{
				reader.Read();

				return Enum.Parse<T>(reader.GetString()!);
			}

			return null;
		}

		public static float InverseLerp(float value, float min, float max)
		{
			return (value - min) / (max - min);
		}

		public static NVector2 InverseLerp(NVector2 value, NVector2 min, NVector2 max)
		{
			return (value - min) / (max - min);
		}

		public static string SavedInput(string id, string defaultInput, out bool changed)
		{
			if (string.IsNullOrEmpty(savingInputString))
				savingInputString = defaultInput;

			changed = ImGui.InputText(id, ref savingInputString, 64);

			return savingInputString;
		}

		public static void ResetSavedInput(string newInput = null)
		{
			savingInputString = newInput ?? string.Empty;
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

			if (!string.IsNullOrEmpty(descr))
			{
				ImGui.SetItemTooltip(descr);
			}

			if (buttonPressed)
				callback?.Invoke(id);

			return buttonPressed;
		}

		public static class IcoMoon
		{
			public const char RotateIcon = '\ue984';

			public const char BackwardIcon = '\uea1f';
			public const char ForwardIcon = '\uea20';

			public const char PreviousIcon = '\uea23';
			public const char NextIcon = '\uea24';

			public const char PreviousArrowIcon = '\uea40';
			public const char NextArrowIcon = '\uea3c';

			public const char FirstIcon = '\uea21';
			public const char LastIcon = '\uea22';

			public const char LoopIcon = '\uea2d';

			public const char TextureIcon = '\ueacd';
			public const char ImageIcon = '\ue90d';
			public const char ImagesIcon = '\ue90e';
			public const char TargetIcon = '\ue9b3';

			public const char BranchesIcon = '\ue9bc';
			public const char ListIcon = '\ue9ba';
			public const char EqualizerIcon = '\ue992';
			public const char SettingsIcon = '\ue994';
			public const char HammerIcon = '\ue996';
			public const char KeyIcon = '\ue98d';

			public const char EmptyFileIcon = '\ue924';
			public const char FloppyDiskIcon = '\ue962';
			public const char FolderOpenIcon = '\ue930';

			public const char PlusIcon = '\uea0a';
			public const char MinusIcon = '\uea0b';
			public static ImFontPtr font;

			public static unsafe void AddIconsToDefaultFont(float fontSize)
			{
				const string fontFilePath = "Content/IcoMoon-Free.ttf";
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

		// this is internal!!! why tho;
		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static unsafe extern byte* igGetKeyChordName(ImGuiKey key);

		public static unsafe string GetKeyChordName(ImGuiKey shortcut)
		{
			byte* ptr = igGetKeyChordName(shortcut);
			int byteCount = 0;
			while (ptr[byteCount] != 0)
				++byteCount;

			return Encoding.UTF8.GetString(ptr, byteCount);
		}

		public static Vector2 Vec2Abs(Vector2 vector2)
		{
			return new Vector2(MathF.Abs(vector2.X), MathF.Abs(vector2.Y));
		}
	}
	public class DampedValue
	{
		private float _value;
		private float _target;

		public DampedValue(float initialValue = 0f)
		{
			_value = _target = initialValue;
		}

		public float SnapLeniency { get; set; } = 0.001f;
		public float Damping { get; set; } = 0.2f;
		public float Value => _value;
		public float Target
		{
			get => _target;
			set => _target = value;
		}

		public void SnapValue(float target)
		{
			_value = target;
			_target = target;
		}

		public static implicit operator float(DampedValue value) => value.Value;

		public static implicit operator DampedValue(float value) => new DampedValue(value);

		public bool TickDamping()
		{
			if (_value != _target)
			{
				_value = MathHelper.Lerp(_value, _target, Damping);

				if (_value != _target && MathF.Abs(_value - _target) < SnapLeniency)
					_value = _target;

				return true;
			}

			return false;
		}
	}
}