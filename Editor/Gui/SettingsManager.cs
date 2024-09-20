using Editor.Model;

using Microsoft.Xna.Framework.Graphics;

using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Editor.Gui
{
	public static class SettingsManager
	{
		public const int SaveFileMagicNumber = 1296649793;
		/// <summary>
		///     0 = Mostrar posiciones adyacentes
		///     1 = Mostrar rotaciones adyacentes
		///     2 = Confirmar nuevo proyecto
		///     3 = Mostrar frame nuevo al mover keyframes
		///     4 = Reproducir al seleccionar keyframe
		/// </summary>
		public static BitArray settingsFlags = new BitArray(12);

		
		public static void LoadProject()
		{
			using (FileStream stream = File.OpenRead(Hierarchy.OpenFdDefinition.SelectedRelativePath))
			{
				using (BinaryReader reader = new BinaryReader(stream))
				{
					if (reader.ReadUInt32() == SaveFileMagicNumber)
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
									string key = reader.ReadString();
									string path = reader.ReadString();
									Texture2D texture = Texture2D.FromFile(EditorApplication.Graphics, path);

									EditorApplication.State.Textures.Add(key, new TextureFrame(texture, path, reader.ReadPoint(), reader.ReadNVector2()));
								}

								counter = reader.ReadInt32();
								EditorApplication.State.GraphicEntities.EnsureCapacity(counter);

								for (int i = 0; i < counter; i++)
								{
									string name = reader.ReadString();
									string textureId = reader.ReadString();
									TextureEntity entity = new TextureEntity(name, textureId);

									ReadSavedKeyframes(reader, entity);

									EditorApplication.	State.GraphicEntities.Add(name, entity);
								}

								counter = reader.ReadInt32();
								EditorApplication.State.HitboxEntities.EnsureCapacity(counter);

								for (int i = 0; i < counter; i++)
								{
									string name = reader.ReadString();
									HitboxEntity entity = new HitboxEntity(name);

									ReadSavedKeyframes(reader, entity);

									EditorApplication.	State.HitboxEntities.Add(name, entity);
								}

								break;
						}

						EditorApplication.	State.Animator.OnKeyframeChanged?.Invoke();
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

		public static void SaveProject()
		{
			using (FileStream stream = File.Open(Hierarchy.OpenFdDefinition.SelectedRelativePath, FileMode.OpenOrCreate))
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write(SaveFileMagicNumber);
					writer.Write((byte)0);
					writer.Write(EditorApplication.State.Animator.FPS);
					writer.Write(EditorApplication.State.Animator.CurrentKeyframe);

					writer.Write(EditorApplication.State.Textures.Count);

					foreach (KeyValuePair<string, TextureFrame> texture in EditorApplication.State.Textures)
					{
						writer.Write(texture.Key);
						writer.Write(texture.Value.Path);
						writer.Write(texture.Value.FrameSize);
						writer.Write(texture.Value.Pivot);
					}

					writer.Write(EditorApplication.State.GraphicEntities.Count);

					foreach (TextureEntity entity in EditorApplication.State.Animator.RegisteredGraphics)
					{
						writer.Write(entity.Name);
						writer.Write(entity.TextureId);

						SaveEntityKeyframes(entity, writer);
					}

					writer.Write(EditorApplication.State.HitboxEntities.Count);

					foreach (HitboxEntity entity in EditorApplication.State.Animator.RegisteredHitboxes)
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

		public static void Initialize()
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
	}
}