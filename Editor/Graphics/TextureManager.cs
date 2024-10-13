using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Editor.Graphics
{
	public static class TextureManager
	{
		public static readonly Dictionary<string, RegistryData> PathIndexMap = new Dictionary<string, RegistryData>();

		public static Texture2D GetTexture(string path)
		{
			object obj = 1;

			lock (obj)
			{
				return EditorApplication.ImguiRenderer.GetTexture(PathIndexMap[path].ImguiId);
			}
		}

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

					Texture2D texture = EditorApplication.ImguiRenderer.loadedTextures[data.ImguiId];
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
				lock (EditorApplication.ImguiRenderer.loadedTextures)
				{
					int attempts = 0;

					while (attempts < 10)
					{
						try
						{
							Texture2D texture = EditorApplication.ImguiRenderer.loadedTextures[ImguiId];
							EditorApplication.ImguiRenderer.UnbindTexture(ImguiId);
							texture.Dispose();
							texture = Texture2D.FromFile(EditorApplication.Graphics, Path);
							EditorApplication.ImguiRenderer.loadedTextures[ImguiId] = texture;
							attempts = 10;
						}
						catch
						{
							attempts++;
							Thread.Sleep(100);
						}
					}
				}
			}

			public void Dispose()
			{
				Watcher.Dispose();
			}
		}
	}
}