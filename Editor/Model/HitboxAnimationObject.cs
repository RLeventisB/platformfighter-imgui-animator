using Editor.Graphics;
using Editor.Gui;

using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Editor.Model
{
	public class HitboxAnimationObject : IAnimationObject
	{
		[JsonConstructor]
		public HitboxAnimationObject() : this(string.Empty)
		{
		}

		public HitboxAnimationObject(string name)
		{
			Position = new Vector2KeyframeValue(this, Vector2.Zero, PositionProperty, false);
			Size = new Vector2KeyframeValue(this, Vector2.One * 16, SizeProperty, false);

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
			LaunchType = LaunchType.TorsoHit;
			Type = HitboxType.Hitbox;
			AttackId = 0; // the id of the attack, when hit, all hitbox in the same id cant hit the victim by ImmunityAfterHit frames
			ImmunityAfterHit = 10; // the amount of frames that the oponent is invulnerable when hitting
			ShieldLaunchAngle = -50; // the angle that the hitbox launches when hitting a shielding opponent
			ShieldPotency = 2; // the force that the hitbox launches when hitting a shielding opponent
			Priority = 0; // the priority of a hitbox, takes effect when multiple hitboxes collide with the victim at the same time, same priority hitboxes are selected randomly
			Rate = 0.98f;
		}

		public ushort Hitstun, MaxHitstun, ShieldStun, DuelGameLag, AttackId, ImmunityAfterHit, Priority, FrameDuration, SpawnFrame;
		public float Damage, HitstunGrowth, LaunchAngle, LaunchPotency, LaunchPotencyGrowth, LaunchPotencyMax, ShieldLaunchAngle, ShieldPotency, Rate;
		public Vector2KeyframeValue Size { get; set; }
		public Vector2KeyframeValue Position { get; set; }
		public string Name { get; set; }
		public HitboxType Type;
		public LaunchType LaunchType;
		public HitboxConditions Conditions;
		public List<string> Tags = new List<string>();

		public HitboxAnimationObject(HitboxAnimationObject clone)
		{
			Position = new Vector2KeyframeValue(this, Vector2.Zero, PositionProperty, false);
			Size = new Vector2KeyframeValue(this, Vector2.One * 16, SizeProperty, false);

			Position.CloneKeyframeDataFrom(clone.Position);
			Size.CloneKeyframeDataFrom(clone.Size);
			Name = clone.Name;

			Damage = clone.Damage;
			Rate = clone.Rate;

			SpawnFrame = clone.SpawnFrame;
			FrameDuration = clone.FrameDuration;

			Hitstun = clone.Hitstun;
			HitstunGrowth = clone.HitstunGrowth;
			MaxHitstun = clone.MaxHitstun;

			LaunchAngle = clone.LaunchAngle;
			LaunchPotency = clone.LaunchPotency;
			LaunchPotencyGrowth = clone.LaunchPotencyGrowth;
			LaunchPotencyMax = clone.LaunchPotencyMax;

			ShieldStun = clone.ShieldStun;
			ShieldLaunchAngle = clone.ShieldLaunchAngle;
			ShieldPotency = clone.ShieldPotency;

			DuelGameLag = clone.DuelGameLag;
			AttackId = clone.AttackId;
			ImmunityAfterHit = clone.ImmunityAfterHit;

			Priority = clone.Priority;

			Type = clone.Type;
			LaunchType = clone.LaunchType;
			Conditions = clone.Conditions;
		}

		public int EndFrame => SpawnFrame + FrameDuration;

		public bool IsBeingHovered(Vector2 mouseWorld, int? frame)
		{
			frame ??= EditorApplication.State.Animator.CurrentKeyframe;

			return IsOnFrame(frame.Value) && IsInsideRectangle(Position.CachedValue, Size.CachedValue, mouseWorld);
		}

		public bool IsOnFrame(int frame)
		{
			return frame >= SpawnFrame && frame < EndFrame;
		}

		public HitboxLine GetSelectedLine(Vector2 mouseWorld)
		{
			Vector2 position = Position.CachedValue;
			Vector2 size = Size.CachedValue;
			float topDistance = MathF.Abs(position.Y - size.Y / 2 - mouseWorld.Y) * Camera.Zoom;
			float rightDistance = MathF.Abs(position.X + size.X / 2 - mouseWorld.X) * Camera.Zoom;
			float bottomDistance = MathF.Abs(position.Y + size.Y / 2 - mouseWorld.Y) * Camera.Zoom;
			float leftDistance = MathF.Abs(position.X - size.X / 2 - mouseWorld.X) * Camera.Zoom;
			bool inXRange = mouseWorld.X > position.X - size.X / 2 && mouseWorld.X < position.X + size.X / 2;
			bool inYRange = mouseWorld.Y > position.Y - size.Y / 2 && mouseWorld.Y < position.Y + size.Y / 2;

			float min = Math.Min(Math.Min(topDistance, bottomDistance), Math.Min(rightDistance, leftDistance));

			if (min > 2)
				return HitboxLine.None;

			if (min == topDistance && inXRange)
				return HitboxLine.Top;

			if (min == rightDistance && inYRange)
				return HitboxLine.Right;

			if (min == bottomDistance && inXRange)
				return HitboxLine.Bottom;

			if (min == leftDistance && inXRange)
				return HitboxLine.Left;

			return HitboxLine.None;
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

		public List<KeyframeableValue> EnumerateKeyframeableValues() => [Position, Size];
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