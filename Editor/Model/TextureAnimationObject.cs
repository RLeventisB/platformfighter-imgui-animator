using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Editor.Model
{
	public class TextureAnimationObject : IAnimationObject
	{
		[JsonConstructor]
		public TextureAnimationObject()
		{
			Name = null;
			TextureName = null;

			Scale = new Vector2KeyframeValue(this, Vector2.One, ScaleProperty, false);
			FrameIndex = new IntKeyframeValue(this, 0, FrameIndexProperty, false);
			Rotation = new FloatKeyframeValue(this, 0, RotationProperty, false);
			Position = new Vector2KeyframeValue(this, Vector2.Zero, PositionProperty, false);
			Transparency = new FloatKeyframeValue(this, 1, TransparencyProperty, false);
			ZIndex = new FloatKeyframeValue(this, 0, ZIndexProperty, false);
		}

		public TextureAnimationObject(string name, string textureName)
		{
			Name = name;
			TextureName = textureName;

			Scale = new Vector2KeyframeValue(this, Vector2.One, ScaleProperty);
			FrameIndex = new IntKeyframeValue(this, 0, FrameIndexProperty);
			Rotation = new FloatKeyframeValue(this, 0, RotationProperty);
			Position = new Vector2KeyframeValue(this, Vector2.Zero, PositionProperty);
			Transparency = new FloatKeyframeValue(this, 1, TransparencyProperty);
			ZIndex = new FloatKeyframeValue(this, 0, ZIndexProperty);
		}

		public string TextureName { get; set; }
		public Vector2KeyframeValue Scale { get; set; }
		public IntKeyframeValue FrameIndex { get; set; }
		public FloatKeyframeValue Rotation { get; set; }
		[JsonInclude]
		public string Name { get; set; }
		public Vector2KeyframeValue Position { get; set; }
		public FloatKeyframeValue Transparency { get; set; }
		public FloatKeyframeValue ZIndex { get; set; }

		public bool IsBeingHovered(Vector2 mouseWorld, int? frame)
		{
			frame ??= EditorApplication.State.Animator.CurrentKeyframe;
			TextureFrame texture = EditorApplication.State.GetTexture(TextureName);
			Vector2 scale = Scale.Interpolate(frame.Value);
			Vector2 size = texture.FrameSize.ToVector2() * Vec2Abs(scale);

			float rotation = Rotation.Interpolate(frame.Value);

			return IsPointInsideRotatedRectangle(Position.Interpolate(frame.Value), size, rotation, -texture.Pivot * Vec2Abs(scale), mouseWorld);
		}

		public List<KeyframeableValue> EnumerateKeyframeableValues() => [Position, Scale, Rotation, FrameIndex, Transparency, ZIndex];
	}
}