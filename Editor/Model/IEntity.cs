using System.Collections.Generic;

namespace Editor.Model
{
	public interface IEntity
	{
		public string Name { get; set; }
		public Vector2KeyframeValue Position { get; set; }

		bool IsBeingHovered(Vector2 mouseWorld, int frame);

		List<KeyframeableValue> EnumerateKeyframeableValues();
	}
}