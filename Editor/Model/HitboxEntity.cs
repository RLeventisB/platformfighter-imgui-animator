using Editor.Gui;

using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;

namespace Editor.Model
{
	public class HitboxEntity : IEntity
	{
		public HitboxEntity(string name)
		{
			Size = Vector2.One * 16;
			Position = Vector2.Zero;
			Name = name;
			SpawnFrame = 0;
			FrameDuration = 4;
		}

		public Vector2 Size { get; set; }
		public string Name { get; set; }
		public Vector2 Position { get; set; }
		public int SpawnFrame;
		public ushort FrameDuration;
		public List<string> Tags = new List<string>();
		public int EndFrame => SpawnFrame + FrameDuration;

		public bool IsBeingHovered(Vector2 mouseWorld, int frame)
		{
			return IsOnFrame(frame) && IsInsideRectangle(Position, Size, mouseWorld);
		}

		public bool IsOnFrame(int frame)
		{
			return frame >= SpawnFrame && frame < EndFrame;
		}

		public HitboxLine GetSelectedLine(Vector2 mouseWorld)
		{
			float topDistance = MathF.Abs(Position.Y - Size.Y / 2 - mouseWorld.Y);
			float rightDistance = MathF.Abs(Position.X + Size.X / 2 - mouseWorld.X);
			float bottomDistance = MathF.Abs(Position.Y + Size.Y / 2 - mouseWorld.Y);
			float leftDistance = MathF.Abs(Position.X - Size.X / 2 - mouseWorld.X);

			float min = Math.Min(Math.Min(topDistance, bottomDistance), Math.Min(rightDistance, leftDistance));

			if (min > 2)
				return HitboxLine.None;

			if (min == topDistance)
				return HitboxLine.Top;

			if (min == rightDistance)
				return HitboxLine.Right;

			return min == bottomDistance ? HitboxLine.Bottom : HitboxLine.Left;
		}

		public Color GetColor()
		{
			return Timeline.HitboxMode ? Color.Red : Color.Red.MultiplyAlpha(0.2f);
		}
	}
	public enum HitboxLine
	{
		Top, Right, Bottom, Left, None
	}
}