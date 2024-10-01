using System.Collections.Generic;

namespace Editor.Model
{
	public interface IAnimationObject
	{
		public string Name { get; set; }

		public bool IsBeingHovered(Vector2 mouseWorld, int? frame);

		public List<KeyframeableValue> EnumerateKeyframeableValues();
	}
}