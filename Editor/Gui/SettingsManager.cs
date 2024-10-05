using Editor.Objects;

using ImGuiNET;

using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Editor.Gui
{
	public static class SettingsManager
	{
		public static JsonSerializerOptions DefaultSerializerOptions => new JsonSerializerOptions // returns an new instance everytime because caching is brROKEN  I LOST 6 ENTIRE PROJECTS
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			AllowTrailingCommas = true,
			IncludeFields = true,
			WriteIndented = true,
			ReferenceHandler = ReferenceHandler.Preserve,
			IgnoreReadOnlyFields = true,
			IgnoreReadOnlyProperties = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters =
			{
				new JsonStringEnumConverter<HitboxType>(JsonNamingPolicy.SnakeCaseLower),
				new JsonStringEnumConverter<HitboxConditions>(JsonNamingPolicy.SnakeCaseLower),
				new JsonStringEnumConverter<LaunchType>(JsonNamingPolicy.SnakeCaseLower),
				new JsonStringEnumConverter<InterpolationType>(JsonNamingPolicy.SnakeCaseLower),
			}
		};

		public static ImGuiWindowFlags ToolsWindowFlags => LockToolWindows ? ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking : ImGuiWindowFlags.None;
		public const int SaveFileMagicNumber = 1296649793;
		public const int AllocatedSettings = 16;
		public static int ValidSettings { get; private set; }
		private static BitArray settingsFlags = new BitArray(AllocatedSettings);
		public static BoolSetting ShowPositionLinks = new BoolSetting(0, "Mostrar posiciones enlazadas");
		public static BoolSetting ShowRotationLinks = new BoolSetting(1, "Mostrar rotaciones enlazadas");
		public static BoolSetting ConfirmOnNewProject = new BoolSetting(2, "Confirmar al darle al boton de nuevo proyecto");
		public static BoolSetting ShowNewFrameUponMovingKeyframes = new BoolSetting(3, "Mostrar frame nuevo al mover keyframes");
		public static BoolSetting PlayOnKeyframeSelect = new BoolSetting(4, "Reproducir al seleccionar keyframe");
		public static BoolSetting LockToolWindows = new BoolSetting(5, "Fijar la posicion y tamaño de las ventanas de herramientas");
		public static BoolSetting SetKeyframeOnModify = new BoolSetting(6, "Al cambiar un valor, asignar instantaneamente el valor al keyframe");
		public static BoolSetting CompressOnSave = new BoolSetting(7, "Comprimir el proyecto al guardar");
		public static BoolSetting AddKeyframeToLinkOnModify = new BoolSetting(8, "Al cambiar un valor, el keyframe nuevo se añade al link si hay uno que lo contiene");
		public static string lastProjectSavePath;
		public static BoolSetting[] Settings =>
		[
			ShowPositionLinks,
			ShowRotationLinks,
			ConfirmOnNewProject,
			ShowNewFrameUponMovingKeyframes,
			PlayOnKeyframeSelect,
			LockToolWindows,
			SetKeyframeOnModify,
			CompressOnSave,
			AddKeyframeToLinkOnModify
		];

		public static void LoadProject(string filePath)
		{
			try
			{
				byte[] text = File.ReadAllBytes(filePath);

				if (BitConverter.ToUInt16(text, 0) == 0x9DD5) // file is compressed
				{
					using (MemoryStream stream = new MemoryStream(text))
					using (DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
					{
						using (MemoryStream outputStream = new MemoryStream())
						{
							deflateStream.CopyTo(outputStream);
							text = outputStream.GetBuffer();
						}
					}
				}

				JsonData data = JsonSerializer.Deserialize<JsonData>(text, DefaultSerializerOptions);

				EditorApplication.ApplyJsonData(data);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			lastProjectSavePath = filePath;
		}

		public static void SaveProject(string filePath)
		{
			if (File.Exists(filePath))
				File.Delete(filePath);

			using (FileStream stream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
			{
				stream.Seek(0, SeekOrigin.Begin);
				byte[] serializedJson = JsonSerializer.SerializeToUtf8Bytes(EditorApplication.GetJsonObject(), DefaultSerializerOptions);

				if (CompressOnSave)
				{
					// todo: fix the identifier thingy not working for gods sake
					using (DeflateStream compressor = new DeflateStream(stream, CompressionLevel.SmallestSize))
					{
						compressor.Write(serializedJson);
					}
				}
				else
				{
					stream.Write(serializedJson);
				}
			}

			lastProjectSavePath = filePath;
		}

		public static void Initialize()
		{
			settingsFlags = new BitArray(AllocatedSettings);

			ShowPositionLinks.Set(true);
			ShowRotationLinks.Set(true);
			ConfirmOnNewProject.Set(true);
			ShowNewFrameUponMovingKeyframes.Set(true);
			PlayOnKeyframeSelect.Set(true);
			LockToolWindows.Set(false);
			SetKeyframeOnModify.Set(false);
			CompressOnSave.Set(true);

			if (!File.Exists("./settings.dat"))
				return;

			using (FileStream fs = File.OpenRead("./settings.dat"))
			{
				for (int i = 0; i < AllocatedSettings; i += 8)
				{
					int data = fs.ReadByte();

					if (data == -1)
						break;

					for (int j = 0; j < 8; j++)
					{
						int bit = 1 << j;
						settingsFlags.Set(i + j, (data & bit) == bit);
					}
				}
			}
		}

		public static void SaveSettings()
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
		}

		public record BoolSetting
		{
			public BoolSetting(int index, string description)
			{
				this.index = index;
				this.description = description;
				ValidSettings++;
			}

			public bool Get()
			{
				return settingsFlags.Get(index);
			}

			public void Set(bool value)
			{
				settingsFlags.Set(index, value);
			}

			public static implicit operator bool(BoolSetting setting) => setting.Get();

			public int index { get; init; }
			public string description { get; init; }

			public void Deconstruct(out int index, out string description)
			{
				index = this.index;
				description = this.description;
			}
		}

		public static void DrawSettingsPopup()
		{
			bool popupOpen = true;

			if (ImGui.BeginPopupModal("Settings", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoDocking))
			{
				foreach (BoolSetting setting in Settings)
				{
					Checkbox(setting.description, setting.index);
				}

				if (ImGui.Button("Reiniciar posicion de las herramientas"))
				{
					ImGui.SetWindowPos("Timeline", new NVector2(0, EditorApplication.Graphics.Viewport.Height - Timeline.TimelineVerticalHeight));
					ImGui.SetWindowSize("Timeline", new NVector2(EditorApplication.Graphics.Viewport.Width - Hierarchy.WindowWidth, Timeline.TimelineVerticalHeight));
					ImGui.SetWindowPos("Management", new NVector2(EditorApplication.Graphics.Viewport.Width - Hierarchy.WindowWidth, 0));
					ImGui.SetWindowSize("Management", new NVector2(Hierarchy.WindowWidth, EditorApplication.Graphics.Viewport.Height));
					ImGui.CloseCurrentPopup();

					return;
				}

				ImGui.SetCursorPosY(600 - ImGui.GetStyle().DisplayWindowPadding.Y);
				ImGui.SetNextItemShortcut(ImGuiKey.Enter);

				if (ImGui.Button("OK!"))
				{
					SaveSettings();

					ImGui.CloseCurrentPopup();
				}

				ImGui.SetItemTooltip("Guarda los cambios hechos en los ajustes.\nShortcut: " + ImGui.GetKeyName(ImGuiKey.Enter));

				ImGui.SameLine();
				ImGui.SetNextItemShortcut(ImGuiKey.Escape);

				if (ImGui.Button("No >:("))
				{
					Initialize();

					ImGui.CloseCurrentPopup();
				}

				ImGui.SetItemTooltip("Cancela los cambios en los ajustes.\nShortcut: " + ImGui.GetKeyName(ImGuiKey.Escape));

				ImGui.EndPopup();

				void Checkbox(string text, int index)
				{
					bool value = settingsFlags.Get(index);
					if (ImGui.Checkbox(text, ref value))
						settingsFlags.Set(index, value);
				}
			}
		}
	}
}