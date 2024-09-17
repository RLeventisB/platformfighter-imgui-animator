using System.Collections.Generic;

namespace Editor.Model
{
	public class HitboxEntity : IEntity
	{
		public HitboxEntity(string name)
		{
			SizeX = new FloatKeyframeValue(this, SizeXProperty);
			SizeY = new FloatKeyframeValue(this, SizeYProperty);
			Position = new Vector2KeyframeValue(this, PositionProperty);
			Name = name;
		}

		public FloatKeyframeValue SizeX { get; set; }
		public FloatKeyframeValue SizeY { get; set; }

		public string Name { get; set; }
		public Vector2KeyframeValue Position { get; set; }

		public bool IsBeingHovered(Vector2 mouseWorld, int frame)
		{
			Vector2 size = new Vector2(SizeX.Interpolate(frame), SizeY.Interpolate(frame));

			return IsInsideRectangle(Position.Interpolate(frame), size, mouseWorld);
		}

		public List<KeyframeableValue> EnumerateKeyframeableValues() => [Position, SizeX, SizeY];
	}
}