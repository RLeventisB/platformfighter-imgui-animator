using Microsoft.Xna.Framework.Graphics;

using System;

namespace Editor.Model
{
	public class TextureFrame : IDisposable, IEntity
	{
		private readonly Texture2D _texture;
		private readonly nint _textureId;
		public string Path { get; set; }
		public string Name { get; set; }

		public NVector2 Pivot { get; set; }
		public Point FrameSize { get; set; }
		public Point FramePosition { get; set; }

		public TextureFrame(string name, Texture2D texture, string path, Point frameSize, Point? framePosition = null, NVector2? pivot = null)
		{
			Name = name;
			Path = path;
			FrameSize = frameSize;
			FramePosition = framePosition ?? Point.Zero;
			Pivot = pivot ?? NVector2.Zero;
			_texture = texture;
			_textureId = EditorApplication.ImguiRenderer.BindTexture(_texture);
		}

		public int Width => _texture.Width;
		public int Height => _texture.Height;
		public Texture2D Texture => _texture;
		public nint TextureId => _textureId;

		public static implicit operator Texture2D(TextureFrame f)
		{
			return f._texture;
		}

		public void Dispose()
		{
			EditorApplication.ImguiRenderer.UnbindTexture(_textureId);
			_texture?.Dispose();
		}
	}
}