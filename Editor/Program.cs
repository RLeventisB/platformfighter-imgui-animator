using Editor.Gui;
using Editor.Objects;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Editor
{
	public static class Program
	{
		[STAThread]
		public static void Main()
		{
			if (false)
			{
				Directory.CreateDirectory("./parsed");

				foreach (string filePath in Directory.GetFiles(@".\projects\terminados", "*.anim"))
				{
					JsonDataAlt data = JsonSerializer.Deserialize<JsonDataAlt>(File.ReadAllBytes(filePath), SettingsManager.DefaultSerializerOptions);
					List<TextureAnimationObjectAlt> graphicEntities = new List<TextureAnimationObjectAlt>(data.graphicObjects);
					List<TextureAnimationObject> parsedGraphicEntities = new List<TextureAnimationObject>();
					List<HitboxAnimationObjectAlt> hitboxEntities = new List<HitboxAnimationObjectAlt>(data.hitboxObjects);
					List<HitboxAnimationObject> parsedHitboxEntities = new List<HitboxAnimationObject>();

					foreach (TextureAnimationObjectAlt graphicObject in graphicEntities)
					{
						TextureAnimationObject textureObject = new TextureAnimationObject(graphicObject.Name, graphicObject.TextureName);
						textureObject.Position = graphicObject.Position.ToNew<Vector2KeyframeValue>(textureObject);
						textureObject.Scale = graphicObject.Scale.ToNew<Vector2KeyframeValue>(textureObject);
						textureObject.FrameIndex = graphicObject.FrameIndex.ToNew<IntKeyframeValue>(textureObject);
						textureObject.Rotation = graphicObject.Rotation.ToNew<FloatKeyframeValue>(textureObject);
						textureObject.Transparency = graphicObject.Transparency.ToNew<FloatKeyframeValue>(textureObject);
						textureObject.ZIndex = graphicObject.ZIndex.ToNew<FloatKeyframeValue>(textureObject);
						
						parsedGraphicEntities.Add(textureObject);
					}

					foreach (HitboxAnimationObjectAlt hitboxObject in hitboxEntities)
					{
						HitboxAnimationObject newHitboxObject = new HitboxAnimationObject(hitboxObject.Name);
						newHitboxObject.Position = hitboxObject.Position.ToNew<Vector2KeyframeValue>(newHitboxObject);
						newHitboxObject.Size = hitboxObject.Size.ToNew<Vector2KeyframeValue>(newHitboxObject);
						newHitboxObject.Name = hitboxObject.Name;
						newHitboxObject.Damage = hitboxObject.Damage;
						newHitboxObject.SpawnFrame = hitboxObject.SpawnFrame;
						newHitboxObject.FrameDuration = hitboxObject.FrameDuration;
						newHitboxObject.Hitstun = hitboxObject.Hitstun;
						newHitboxObject.HitstunGrowth = hitboxObject.HitstunGrowth;
						newHitboxObject.MaxHitstun = hitboxObject.MaxHitstun;
						newHitboxObject.LaunchAngle = hitboxObject.LaunchAngle;
						newHitboxObject.LaunchPotency = hitboxObject.LaunchPotency;
						newHitboxObject.LaunchPotencyGrowth = hitboxObject.LaunchPotencyGrowth;
						newHitboxObject.LaunchPotencyMax = hitboxObject.LaunchPotencyMax;
						newHitboxObject.ShieldStun = hitboxObject.ShieldStun;
						newHitboxObject.DuelGameLag = hitboxObject.DuelGameLag;
						newHitboxObject.Conditions = hitboxObject.Conditions;
						newHitboxObject.LaunchType = hitboxObject.LaunchType;
						newHitboxObject.Type = hitboxObject.Type;
						newHitboxObject.AttackId = hitboxObject.AttackId;
						newHitboxObject.ImmunityAfterHit = hitboxObject.ImmunityAfterHit;
						newHitboxObject.ShieldLaunchAngle = hitboxObject.ShieldLaunchAngle;
						newHitboxObject.ShieldPotency = hitboxObject.ShieldPotency;
						newHitboxObject.Priority = hitboxObject.Priority;
						newHitboxObject.Rate = hitboxObject.Rate;						
						parsedHitboxEntities.Add(newHitboxObject);
					}
					
					byte[] serializedBytes = JsonSerializer.SerializeToUtf8Bytes(new JsonData(data.looping, data.playingForward, data.playingBackwards, data.selectedFps, data.currentKeyframe,
						data.textures,
						parsedGraphicEntities.ToArray(),
						parsedHitboxEntities.ToArray()
					), SettingsManager.DefaultSerializerOptions);

					File.WriteAllBytes("./parsed/" + Path.GetFileName(filePath), serializedBytes);
				}
			}
			else
			{
				using (EditorApplication game = new EditorApplication())
					game.Run();
			}
		}
	}
	public record JsonDataAlt(bool looping, bool playingForward, bool playingBackwards, int selectedFps, int currentKeyframe, TextureFrame[] textures, TextureAnimationObjectAlt[] graphicObjects, HitboxAnimationObjectAlt[] hitboxObjects)
	{
		[JsonConstructor]
		public JsonDataAlt() : this(false, false, false, 0, 0, Array.Empty<TextureFrame>(), Array.Empty<TextureAnimationObjectAlt>(), Array.Empty<HitboxAnimationObjectAlt>())
		{
		}
	}
	public class HitboxAnimationObjectAlt : IAnimationObjectAlt
	{
		public ushort Hitstun, MaxHitstun, ShieldStun, DuelGameLag, AttackId, ImmunityAfterHit, Priority, FrameDuration, SpawnFrame;
		public float Damage, HitstunGrowth, LaunchAngle, LaunchPotency, LaunchPotencyGrowth, LaunchPotencyMax, ShieldLaunchAngle, ShieldPotency, Rate;
		public KeyframeableValueAlt Size { get; set; }
		public KeyframeableValueAlt Position { get; set; }
		public string Name { get; set; }
		public HitboxType Type;
		public LaunchType LaunchType;
		public HitboxConditions Conditions;
		public List<string> Tags = new List<string>();

		[JsonConstructor]
		public HitboxAnimationObjectAlt() : this(string.Empty)
		{
		}

		public HitboxAnimationObjectAlt(string name)
		{
			Position = new KeyframeableValueAlt(this, Vector2.Zero, PropertyNames.PositionProperty, false);
			Size = new KeyframeableValueAlt(this, Vector2.One * 16, PropertyNames.SizeProperty, false);

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
	}
	public class TextureAnimationObjectAlt : IAnimationObjectAlt
	{
		[JsonConstructor]
		public TextureAnimationObjectAlt()
		{
			Name = null;
			TextureName = null;

			Scale = new KeyframeableValueAlt(this, Vector2.One, PropertyNames.ScaleProperty, false);
			FrameIndex = new KeyframeableValueAlt(this, 0, PropertyNames.FrameIndexProperty, false);
			Rotation = new KeyframeableValueAlt(this, 0, PropertyNames.RotationProperty, false);
			Position = new KeyframeableValueAlt(this, Vector2.Zero, PropertyNames.PositionProperty, false);
			Transparency = new KeyframeableValueAlt(this, 1, PropertyNames.TransparencyProperty, false);
			ZIndex = new KeyframeableValueAlt(this, 0, PropertyNames.ZIndexProperty, false);
		}

		public string TextureName { get; set; }
		public KeyframeableValueAlt Scale { get; set; }
		public KeyframeableValueAlt FrameIndex { get; set; }
		public KeyframeableValueAlt Rotation { get; set; }
		[JsonInclude]
		public string Name { get; set; }
		public KeyframeableValueAlt Position { get; set; }
		public KeyframeableValueAlt Transparency { get; set; }
		public KeyframeableValueAlt ZIndex { get; set; }
	}
	public class KeyframeableValueAlt
	{
		public object DefaultValue;
		public List<KeyframeAlt> keyframes;
		public List<KeyframeLinkAlt> links;
		public List<string> tags;
		public IAnimationObjectAlt Owner { get; init; }
		public string Name { get; init; }

		public KeyframeableValueAlt(IAnimationObjectAlt animationObject, object defaultValue, string name, bool createDefaultKeyframe = true) : this()
		{
			DefaultValue = defaultValue;
			Owner = animationObject;
			Name = name;
		}

		[JsonConstructor]
		protected KeyframeableValueAlt()
		{
			tags = new List<string>();
			keyframes = new List<KeyframeAlt>();
			links = new List<KeyframeLinkAlt>();
		}
		
		public T ToNew<T>(IAnimationObject newOwner) where T : KeyframeableValue, new ()
		{
			T value = new T
			{
				Owner = newOwner,
				Name = Name,
				DefaultValue = DefaultValue
			};

			value.keyframes = new KeyframeList(keyframes.Select(v => v.ToNew(value)));

			foreach (KeyframeLinkAlt link in links)
			{
				value.links.Add(link.ToNew(value));
			}
			return value;
		}
	}
	public interface IAnimationObjectAlt
	{
		public string Name { get; set; }
	}
	public class KeyframeAlt : IComparable<KeyframeAlt>
	{
		public KeyframeableValueAlt ContainingValue;
		public int frame;
		public object value;
		public KeyframeLinkAlt containingLink;

		[JsonConstructor]
		public KeyframeAlt()
		{
			frame = -1;
			value = null;
		}
		
		public int CompareTo(KeyframeAlt other) => frame.CompareTo(other.frame);

		public static implicit operator KeyframeAlt(int value) => new KeyframeAlt
		{
			frame = value
		};

		public Keyframe ToNew<T>(T keyframeableValue) where T : KeyframeableValue, new()
		{
			return new Keyframe(keyframeableValue, frame, value);
		}
	}
	public class KeyframeLinkAlt
	{
		public List<KeyframeAlt> keyframes;
		public KeyframeableValueAlt ContainingValue;
		public InterpolationType interpolationType;
		public bool UseRelativeProgressCalculation = true;

		public KeyframeLink ToNew(KeyframeableValue value)
		{
			return new KeyframeLink(value, keyframes.Select(v => v.frame))
			{
				InterpolationType = interpolationType,
				UseRelativeProgressCalculation = UseRelativeProgressCalculation
			};
		}
	}
}