using Editor.Model;

using ImGuiNET;

using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Editor.Gui
{
	public static class SettingsManager
	{
		public static readonly JsonReaderOptions DefaultReaderOptions = new JsonReaderOptions
		{
			CommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};
		public static readonly JsonWriterOptions DefaultWriterOptions = new JsonWriterOptions
		{
			Indented = true
		};
		public static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			AllowTrailingCommas = true,
			IncludeFields = true,
			WriteIndented = true,
			ReferenceHandler = ReferenceHandler.Preserve,
			IgnoreReadOnlyFields = true,
			IgnoreReadOnlyProperties = true,
		};

		public static ImGuiWindowFlags ToolsWindowFlags => LockToolWindows ? ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking : ImGuiWindowFlags.None;
		public const int SaveFileMagicNumber = 1296649793;
		public const int AllocatedSettings = 16;
		public static int ValidSettings { get; private set; }
		/// <summary>
		///     0 = Mostrar posiciones adyacentes
		///     1 = Mostrar rotaciones adyacentes
		///     2 = Confirmar nuevo proyecto
		///     3 = Mostrar frame nuevo al mover keyframes
		///     4 = Reproducir al seleccionar keyframe
		///     5 = 
		/// </summary>
		private static BitArray settingsFlags = new BitArray(AllocatedSettings);
		public static BoolSetting ShowPositionLinks = new BoolSetting(0, "Mostrar posiciones enlazadas");
		public static BoolSetting ShowRotationLinks = new BoolSetting(1, "Mostrar rotaciones enlazadas");
		public static BoolSetting ConfirmOnNewProject = new BoolSetting(2, "Confirmar al darle al boton de nuevo proyecto");
		public static BoolSetting ShowNewFrameUponMovingKeyframes = new BoolSetting(3, "Mostrar frame nuevo al mover keyframes");
		public static BoolSetting PlayOnKeyframeSelect = new BoolSetting(4, "Reproducir al seleccionar keyframe");
		public static BoolSetting LockToolWindows = new BoolSetting(5, "Fijar la posicion y tamaño de las ventanas de herramientas");
		public static BoolSetting SetKeyframeOnModify = new BoolSetting(6, "Al cambiar un valor, asignar instantaneamente el valor al keyframe");
		public static string lastProjectSavePath;
		public static BoolSetting[] Settings =>
		[
			ShowPositionLinks,
			ShowRotationLinks,
			ConfirmOnNewProject,
			ShowNewFrameUponMovingKeyframes,
			PlayOnKeyframeSelect,
			LockToolWindows,
			SetKeyframeOnModify
		];

		public static void LoadProject(string filePath)
		{
			try
			{
				byte[] text = File.ReadAllBytes(filePath);
				JsonData data = JsonSerializer.Deserialize<JsonData>(text, DefaultSerializerOptions);

				EditorApplication.ApplyJsonData(data);
				return;
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			using (FileStream stream = File.OpenRead(filePath))
			{
				using (BinaryReader reader = new BinaryReader(stream))
				{
					if (stream.Length > 4 && reader.ReadUInt32() == SaveFileMagicNumber)
					{
						EditorApplication.ResetEditor();

						int counter;

						switch (reader.ReadByte())
						{
							default:
							case 0:
								EditorApplication.State.Animator.FPS = reader.ReadInt32();
								EditorApplication.State.Animator.CurrentKeyframe = reader.ReadInt32();

								counter = reader.ReadInt32();
								EditorApplication.State.Textures.EnsureCapacity(counter);

								for (int i = 0; i < counter; i++)
								{
									TextureFrame frame = TextureFrame.Load(reader);

									EditorApplication.State.Textures.Add(frame.Name, frame);
								}

								counter = reader.ReadInt32();
								EditorApplication.State.GraphicEntities.EnsureCapacity(counter);

								for (int i = 0; i < counter; i++)
								{
									TextureAnimationObject animationObject = TextureAnimationObject.Load(reader);

									EditorApplication.State.GraphicEntities.Add(animationObject.Name, animationObject);
								}

								counter = reader.ReadInt32();
								EditorApplication.State.HitboxEntities.EnsureCapacity(counter);

								for (int i = 0; i < counter; i++)
								{
									HitboxAnimationObject hitboxObject = HitboxAnimationObject.Load(reader);

									EditorApplication.State.HitboxEntities.Add(hitboxObject.Name, hitboxObject);
								}

								break;
						}

						EditorApplication.State.Animator.OnKeyframeChanged?.Invoke();
					}
				}
			}

			lastProjectSavePath = filePath;
		}

		public static void SaveProject(string filePath)
		{
			using (FileStream stream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
			{
				// using (DeflateStream compressor = new DeflateStream(stream, CompressionLevel.SmallestSize))
				{
					stream.Write(JsonSerializer.SerializeToUtf8Bytes(EditorApplication.GetJsonObject(), DefaultSerializerOptions));
					/*
					using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, DefaultWriterOptions))
					{
						writer.WriteStartObject();


						writer.WriteStartObject("main_data");
						writer.WriteNumber("save_version", 0);
						writer.WriteNumber("animator_fps", EditorApplication.State.Animator.FPS);
						writer.WriteNumber("saved_keyframe", EditorApplication.State.Animator.CurrentKeyframe);
						writer.WriteEndObject();

						writer.WriteStartArray("texture_frames");

						foreach (TextureFrame texture in EditorApplication.State.Textures.Values)
						{
							texture.Save(writer);
						}

						writer.WriteEndArray();

						writer.WriteStartArray("graphic_objects");

						foreach (TextureAnimationObject entity in EditorApplication.State.Animator.RegisteredGraphics)
						{
							entity.Save(writer);
						}

						writer.WriteEndArray();

						writer.WriteStartArray("hitbox_objects");

						foreach (HitboxAnimationObject entity in EditorApplication.State.Animator.RegisteredHitboxes)
						{
							entity.Save(writer);
						}

						writer.WriteEndArray();
						writer.WriteEndObject();

						writer.Flush();
						writer.Reset();
					}*/
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