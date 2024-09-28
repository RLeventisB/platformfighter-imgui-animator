using Editor.Gui;

using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;
using System.IO;

namespace Editor.Model
{
	public class HitboxAnimationObject : IAnimationObject
	{
		public HitboxAnimationObject(string name)
		{
			Size = Vector2.One * 16;
			Position = Vector2.Zero;
			Name = name;
			Damage = 5f; // how much damage does the hitbox deal
			SpawnFrame = 0; // which frame does the hitbox spawn
			FrameDuration = 4; // how long does the hitbox last (max value exclusive)
			Hitstun = 3; // how long is the opponent on hitsun at base percent on a successful hit
			HitstunGrowth = 3; // the amount of hitstun that is added per damage on a successful hit
			MaxHitstun = 10; // the maximum amount of hitstun that is able to be applied on a successful hit
			LaunchAngle = -30; // the angle that the hitbox launches on a successful hit
			LaunchPotency = 10; // the base potency that the hitbox launches on a successful hit
			LaunchPotencyGrowth = 1f; // how much does the potency add per damage on a successful hit
			LaunchPotencyMax = 100f; // the maximum force that the hitbox launches on a succesful hit
			ShieldStun = 3; // how much does the hitbox stun the victim's shield on hit
			DuelGameLag = 2; // amount of time that the game is lagged when in a 1v1 on a successful hit
			Conditions = HitboxConditions.None; // various conditions of the hitbox hitting
			AttackId = 0; // the id of the attack, when hit, all hitbox in the same id cant hit the victim by ImmunityAfterHit frames
			ImmunityAfterHit = 10; // the amount of frames that the oponent is invulnerable when hitting
			ShieldLaunchAngle = -50; // the angle that the hitbox launches when hitting a shielding opponent
			ShieldPotency = 2; // the force that the hitbox launches when hitting a shielding opponent
			Priority = 0; // the priority of a hitbox, takes effect when multiple hitboxes collide with the victim at the same time, same priority hitboxes are selected randomly
		}

		public HitboxConditions Conditions;
		public ushort Hitstun, MaxHitstun, ShieldStun, DuelGameLag, AttackId, ImmunityAfterHit, Priority;
		public float Damage, HitstunGrowth, LaunchAngle, LaunchPotency, LaunchPotencyGrowth, LaunchPotencyMax, ShieldLaunchAngle, ShieldPotency;
		public Vector2 Size { get; set; }
		public string Name { get; set; }
		public Vector2 Position { get; set; }
		public int SpawnFrame;
		public ushort FrameDuration;
		public List<string> Tags = new List<string>();
		public int EndFrame => SpawnFrame + FrameDuration;
		public HitboxType Type;

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
			Color color = Type switch
			{
				HitboxType.Hurtbox => Color.LightGreen,
				HitboxType.Windbox => Color.Beige,
				HitboxType.CollisionBox => Color.Gray,
				_ => Color.Red
			};

			return Timeline.HitboxMode ? color : color.MultiplyAlpha(0.2f);
		}

		public void Save(BinaryWriter writer)
		{
			writer.Write(Name);
			writer.Write(SpawnFrame);
			writer.Write(FrameDuration);
			writer.Write(Position);
			writer.Write(Size);
			writer.Write(Tags.Count);
			writer.Write((byte)Type);

			foreach (string tag in Tags)
			{
				writer.Write(tag);
			}
		}

		public static HitboxAnimationObject Load(BinaryReader reader)
		{
			string name = reader.ReadString();
			HitboxAnimationObject hitbox = new HitboxAnimationObject(name);
			hitbox.SpawnFrame = reader.ReadInt32();
			hitbox.FrameDuration = reader.ReadUInt16();
			hitbox.Position = reader.ReadVector2();
			hitbox.Size = reader.ReadVector2();

			int tagCount = reader.ReadInt32();
			hitbox.Tags.EnsureCapacity(tagCount);

			for (int j = 0; j < tagCount; j++)
			{
				hitbox.Tags.Add(reader.ReadString());
			}

			return hitbox;
		}
	}
	public enum HitboxType : byte
	{
		Hitbox, Hurtbox, Windbox, CollisionBox
	}
	public enum LaunchType : byte
	{
		SpinningHit, HeadHit, TorsoHit,
	}
	[Flags]
	public enum HitboxConditions : byte
	{
		None = 0, CanBeGrazed = 1, Unblockable = 2, AerialOnly = 4, GroundedOnly = 8
	}
	public enum HitboxLine
	{
		Top, Right, Bottom, Left, None
	}
}