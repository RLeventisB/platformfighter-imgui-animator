using System.Collections.Generic;

namespace Editor.Model
{
	public class TextureEntity : IEntity
	{
		public TextureEntity(string name, string textureId)
		{
			Scale = new Vector2KeyframeValue(this, Vector2.One, ScaleProperty);
			FrameIndex = new IntKeyframeValue(this, 0, FrameIndexProperty);
			Rotation = new FloatKeyframeValue(this, 0, RotationProperty);
			Name = name;
			Position = new Vector2KeyframeValue(this, Vector2.Zero, PositionProperty);
			Transparency = new FloatKeyframeValue(this, 1, TransparencyProperty);
			TextureId = textureId;
		}

		public string TextureId { get; }
		public Vector2KeyframeValue Scale { get; set; }
		public IntKeyframeValue FrameIndex { get; set; }
		public FloatKeyframeValue Rotation { get; set; }

		public string Name { get; set; }
		public Vector2KeyframeValue Position { get; set; }
		public FloatKeyframeValue Transparency { get; set; }

		public bool IsBeingHovered(Vector2 mouseWorld, int frame)
		{
			Vector2 size = EditorApplication.State.GetTexture(TextureId).FrameSize.ToVector2() * Scale.Interpolate(frame);

			return IsInsideRectangle(Position.Interpolate(frame), size, Rotation.Interpolate(frame), mouseWorld);
		}

		public List<KeyframeableValue> EnumerateKeyframeableValues() => [Position, Scale, Rotation, FrameIndex, Transparency];
	}
}