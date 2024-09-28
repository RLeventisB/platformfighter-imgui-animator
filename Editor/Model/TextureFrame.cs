using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using System.IO;

namespace Editor.Model
{
	public static class TextureManager
	{
		public static readonly Dictionary<string, RegistryData> PathIndexMap = new Dictionary<string, RegistryData>();

		public static void LoadTexture(string path, out IntPtr id)
		{
			object obj = 1;

			lock (obj)
			{
				if (PathIndexMap.TryGetValue(path, out RegistryData data))
				{
					data.Count++;
					id = data.ImguiId;
					PathIndexMap[path] = data;
					EditorApplication.ImguiRenderer.GetTexture(id);

					return;
				}

				Texture2D newTexture = Texture2D.FromFile(EditorApplication.Graphics, path);

				nint newTextureId = EditorApplication.ImguiRenderer.BindTexture(newTexture);
				FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(path));
				watcher.Filter = Path.GetFileName(path);
				watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
				watcher.EnableRaisingEvents = true;

				PathIndexMap[path] = new RegistryData(path, newTextureId, 1, watcher);

				id = newTextureId;
			}
		}

		public static bool UnloadTexture(string path)
		{
			object obj = 1;

			lock (obj)
			{
				if (PathIndexMap.TryGetValue(path, out RegistryData data))
				{
					data.Count--;

					if (data.Count > 0)
						return false;

					Texture2D texture = EditorApplication.ImguiRenderer.GetTexture(data.ImguiId);
					EditorApplication.ImguiRenderer.UnbindTexture(data.ImguiId);
					texture.Dispose();
					data.Dispose();
					PathIndexMap.Remove(path);

					return true;
				}

				return false;
			}
		}

		public struct RegistryData : IDisposable
		{
			public string Path { get; }
			public IntPtr ImguiId { get; set; }
			public int Count { get; set; }
			public FileSystemWatcher Watcher { get; }

			public RegistryData(string path, nint imguiId, int count, FileSystemWatcher watcher)
			{
				Path = path;
				ImguiId = imguiId;
				Count = count;
				Watcher = watcher;
				watcher.Changed += ReloadTexture;
			}

			public void ReloadTexture(object sender, FileSystemEventArgs args)
			{
				Texture2D texture = EditorApplication.ImguiRenderer.GetTexture(ImguiId);
				EditorApplication.ImguiRenderer.UnbindTexture(ImguiId);
				texture.Dispose();

				texture = Texture2D.FromFile(EditorApplication.Graphics, Path);

				EditorApplication.ImguiRenderer.loadedTextures[ImguiId] = texture;
			}

			public void Dispose()
			{
				Watcher.Dispose();
			}
		}
	}
	public class TextureFrame : IDisposable, IAnimationObject
	{
		public string Path { get; set; }
		public string Name { get; set; }

		public NVector2 Pivot { get; set; }
		public Point FrameSize { get; set; }
		public Point FramePosition { get; set; }

		public TextureFrame(string name, string path, Point? frameSize = null, Point? framePosition = null, NVector2? pivot = null)
		{
			Name = name;
			Path = path;
			FrameSize = frameSize ?? new Point(Texture.Width, Texture.Height);
			FramePosition = framePosition ?? Point.Zero;
			Pivot = pivot ?? new NVector2(Texture.Width / 2f, Texture.Height / 2f);
			TextureManager.LoadTexture(path, out nint id);
			TextureId = id;
		}

		private void ReloadTexture(object sender, FileSystemEventArgs e)
		{
			if (e.FullPath != Path)
				return;

			TextureManager.UnloadTexture(Path);

			TextureManager.LoadTexture(Path, out nint id);
			TextureId = id;
		}

		public TextureFrame(string name, Texture2D texture, Point frameSize, Point? framePosition = null, NVector2? pivot = null)
		{
			Name = name;
			Path = "Syntetic";
			FrameSize = frameSize;
			FramePosition = framePosition ?? Point.Zero;
			Pivot = pivot ?? NVector2.Zero;
			TextureId = EditorApplication.ImguiRenderer.BindTexture(texture);
		}

		public Point Size => new Point(Width, Height);
		public int Width => Texture.Width;
		public int Height => Texture.Height;
		public Texture2D Texture => EditorApplication.ImguiRenderer.GetTexture(TextureId);
		public nint TextureId { get; private set; }

		public static implicit operator Texture2D(TextureFrame f)
		{
			return f.Texture;
		}

		public bool IsBeingHovered(Vector2 mouseWorld, int frame) => false;

		public void Dispose()
		{
			EditorApplication.ImguiRenderer.UnbindTexture(TextureId);
			Texture?.Dispose();
		}

		public void Remove()
		{
			EditorApplication.State.Textures.Remove(Name);
			if (EditorApplication.selectedData.IsOf(this))
				EditorApplication.selectedData.Empty();

			if (Path == "Syntetic")
			{
				EditorApplication.ImguiRenderer.UnbindTexture(TextureId);
			}
			else
				TextureManager.UnloadTexture(Path);
		}

		public void Save(BinaryWriter writer)
		{
			writer.Write(Name);
			writer.Write(Path);
			writer.Write(FrameSize);
			writer.Write(FramePosition);
			writer.Write(Pivot);
		}

		public static TextureFrame Load(BinaryReader reader)
		{
			string name = reader.ReadString();
			string path = reader.ReadString();
			Point frameSize = reader.ReadPoint();
			Point framePosition = reader.ReadPoint();
			NVector2 pivot = reader.ReadNVector2();

			return new TextureFrame(name, path, frameSize, framePosition, pivot);
		}
	}
}