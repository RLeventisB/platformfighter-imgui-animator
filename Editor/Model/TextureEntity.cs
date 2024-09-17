using System.Collections.Generic;

namespace Editor.Model
{
	public class TextureEntity : IEntity
	{
		public TextureEntity(string name, string textureId)
		{
			Scale = new Vector2KeyframeValue(this, ScaleProperty);
			FrameIndex = new IntKeyframeValue(this, FrameIndexProperty);
			Rotation = new FloatKeyframeValue(this, RotationProperty);
			Name = name;
			Position = new Vector2KeyframeValue(this, PositionProperty);
			TextureId = textureId;
		}

		public string TextureId { get; }
		public Vector2KeyframeValue Scale { get; set; }
		public IntKeyframeValue FrameIndex { get; set; }
		public FloatKeyframeValue Rotation { get; set; }

		public string Name { get; set; }
		public Vector2KeyframeValue Position { get; set; }

		public bool IsBeingHovered(Vector2 mouseWorld, int frame)
		{
			Vector2 size = EditorApplication.State.Textures[TextureId].FrameSize * Scale.Interpolate(frame);

			return IsInsideRectangle(Position.Interpolate(frame), size, Rotation.Interpolate(frame), mouseWorld);
		}

		public List<KeyframeableValue> EnumerateKeyframeableValues() => [Position, Scale, Rotation, FrameIndex];
	}
}