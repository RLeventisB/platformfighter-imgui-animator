using Editor;
using Editor.Gui;
using Editor.Objects;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace game
{
	public class Program
	{
		[STAThread]
		public static void Main()
		{
			if (false)
			{
				Directory.CreateDirectory("./parsed");

				foreach (string filePath in Directory.GetFiles(@".\projects\terminados", "*.anim"))
				{
					JsonData data = JsonSerializer.Deserialize<JsonData>(File.ReadAllBytes(filePath), SettingsManager.DefaultSerializerOptions);
					List<Keyframe> keyframesToResolve = new List<Keyframe>(); // since json loads objects as JsonElements :(
					List<TextureAnimationObject> graphicEntities = new List<TextureAnimationObject>();
					List<TextureAnimationObjectAlt> parsedGraphicEntities = new List<TextureAnimationObjectAlt>();
					List<HitboxAnimationObject> hitboxEntities = new List<HitboxAnimationObject>();
					List<HitboxAnimationObjectAlt> parsedHitboxEntities = new List<HitboxAnimationObjectAlt>();

					foreach (TextureAnimationObject graphicObject in data.graphicObjects)
					{
						graphicEntities.Add(graphicObject);

						foreach (KeyframeableValue keyframeableValue in graphicObject.EnumerateKeyframeableValues())
						{
							foreach (KeyframeLink link in keyframeableValue.links)
							{
								link.ContainingValue = keyframeableValue;

								foreach (Keyframe keyframe in link.Keyframes) // sometimes keyframes dont store their containing link so fixup!!
								{
									keyframe.ContainingLink = link;
								}
							}

							keyframesToResolve.AddRange(keyframeableValue.keyframes);
						}
					}

					foreach (HitboxAnimationObject hitboxObject in data.hitboxObjects)
					{
						hitboxEntities.Add(hitboxObject);

						foreach (KeyframeableValue keyframeableValue in hitboxObject.EnumerateKeyframeableValues())
						{
							foreach (KeyframeLink link in keyframeableValue.links)
							{
								link.ContainingValue = keyframeableValue;

								foreach (Keyframe keyframe in link.Keyframes) // sometimes keyframes dont store their containing link so fixup!!
								{
									keyframe.ContainingLink = link;
								}
							}

							keyframesToResolve.AddRange(keyframeableValue.keyframes);
						}
					}

					foreach (Keyframe keyframe in keyframesToResolve)
					{
						if (keyframe.Value is not JsonElement element)
							continue;

						try
						{
							switch (keyframe.ContainingValue)
							{
								case Vector2KeyframeValue:
									keyframe.Value = new Vector2(element.GetProperty("x").GetSingle(), element.GetProperty("y").GetSingle());

									break;
								case IntKeyframeValue:
									keyframe.Value = element.GetInt32();

									break;
								case FloatKeyframeValue:
									keyframe.Value = element.GetSingle();

									break;
							}
						}
						catch (Exception e) // we are COOKED
						{
							Console.WriteLine(e);
						}
					}

					foreach (TextureAnimationObject entity in graphicEntities)
					{
						TextureAnimationObjectAlt objectAlt = new TextureAnimationObjectAlt();
						objectAlt.Name = entity.Name;
						objectAlt.TextureName = entity.TextureName;
						objectAlt.Position = KeyframeableValueAlt.From(objectAlt, entity.Position);
						objectAlt.Scale = KeyframeableValueAlt.From(objectAlt, entity.Scale);
						objectAlt.FrameIndex = KeyframeableValueAlt.From(objectAlt, entity.FrameIndex);
						objectAlt.Rotation = KeyframeableValueAlt.From(objectAlt, entity.Rotation);
						objectAlt.Transparency = KeyframeableValueAlt.From(objectAlt, entity.Transparency);
						objectAlt.ZIndex = KeyframeableValueAlt.From(objectAlt, entity.ZIndex);

						parsedGraphicEntities.Add(objectAlt);
					}

					foreach (HitboxAnimationObject entity in hitboxEntities)
					{
						HitboxAnimationObjectAlt objectAlt = new HitboxAnimationObjectAlt();
						objectAlt.Name = entity.Name;
						objectAlt.Position = KeyframeableValueAlt.From(objectAlt, entity.Position);
						objectAlt.Size = KeyframeableValueAlt.From(objectAlt, entity.Size);
						objectAlt.Damage = entity.Damage;
						objectAlt.SpawnFrame = entity.SpawnFrame;
						objectAlt.FrameDuration = entity.FrameDuration;
						objectAlt.Hitstun = entity.Hitstun;
						objectAlt.HitstunGrowth = entity.HitstunGrowth;
						objectAlt.MaxHitstun = entity.MaxHitstun;
						objectAlt.LaunchAngle = entity.LaunchAngle;
						objectAlt.LaunchPotency = entity.LaunchPotency;
						objectAlt.LaunchPotencyGrowth = entity.LaunchPotencyGrowth;
						objectAlt.LaunchPotencyMax = entity.LaunchPotencyMax;
						objectAlt.ShieldStun = entity.ShieldStun;
						objectAlt.DuelGameLag = entity.DuelGameLag;
						objectAlt.Conditions = entity.Conditions;
						objectAlt.LaunchType = entity.LaunchType;
						objectAlt.Type = entity.Type;
						objectAlt.AttackId = entity.AttackId;
						objectAlt.ImmunityAfterHit = entity.ImmunityAfterHit;
						objectAlt.ShieldLaunchAngle = entity.ShieldLaunchAngle;
						objectAlt.ShieldPotency = entity.ShieldPotency;
						objectAlt.Priority = entity.Priority;
						objectAlt.Rate = entity.Rate;

						parsedHitboxEntities.Add(objectAlt);
					}

					List<KeyframeAlt> allKeyframes = new List<KeyframeAlt>();
					foreach (TextureAnimationObjectAlt graphicEntity in parsedGraphicEntities)
					{
						allKeyframes.AddRange(graphicEntity.Position.keyframes);
						allKeyframes.AddRange(graphicEntity.Scale.keyframes);
						allKeyframes.AddRange(graphicEntity.FrameIndex.keyframes);
						allKeyframes.AddRange(graphicEntity.Rotation.keyframes);
						allKeyframes.AddRange(graphicEntity.Transparency.keyframes);
						allKeyframes.AddRange(graphicEntity.ZIndex.keyframes);
					}
					foreach (HitboxAnimationObjectAlt hitboxEntity in parsedHitboxEntities)
					{
						allKeyframes.AddRange(hitboxEntity.Position.keyframes);
						allKeyframes.AddRange(hitboxEntity.Size.keyframes);
					}

					byte[] serializedBytes = JsonSerializer.SerializeToUtf8Bytes(new JsonDataAlt(data.looping, data.playingForward, data.playingBackwards, data.selectedFps, data.currentKeyframe, 
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
		public List<KeyframeAlt> keyframes;
		public List<KeyframeLinkAlt> links;
		public List<string> tags;
		public IAnimationObjectAlt Owner { get; init; }
		public string Name { get; init; }

		public KeyframeableValueAlt(IAnimationObjectAlt animationObject, object defaultValue, string name, bool createDefaultKeyframe = true) : this()
		{
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

		public static KeyframeableValueAlt From(IAnimationObjectAlt owner, KeyframeableValue original)
		{
			KeyframeableValueAlt objectAlt = new KeyframeableValueAlt(owner, null, original.Name);

			IEnumerable<IEnumerable<int>> linkDatas = original.links.Select(v => v.Keyframes.Select(v => v.Frame));

			foreach (Keyframe keyframe in original.keyframes)
			{
				objectAlt.keyframes.Add(KeyframeAlt.From(objectAlt, keyframe));
			}

			objectAlt.keyframes.Sort();

			foreach (IEnumerable<int> link in linkDatas)
			{
				objectAlt.links.Add(KeyframeLinkAlt.From(objectAlt, link));
			}

			return objectAlt;
		}

		public KeyframeAlt GetKeyframeAt(int frame)
		{
			int foundIndex = keyframes.BinarySearch(frame);

			return foundIndex >= 0 ? keyframes[foundIndex] : null;
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

		public static KeyframeAlt From(KeyframeableValueAlt containingValue, Keyframe keyframe)
		{
			return new KeyframeAlt
			{
				ContainingValue = containingValue,
				frame = keyframe.Frame,
				value = keyframe.Value
			};
		}

		public int CompareTo(KeyframeAlt other) => frame.CompareTo(other.frame);

		public static implicit operator KeyframeAlt(int value) => new KeyframeAlt
		{
			frame = value
		};
	}
	public class KeyframeLinkAlt
	{
		public List<KeyframeAlt> keyframes;
		public KeyframeableValueAlt ContainingValue;
		public InterpolationType interpolationType;
		public bool UseRelativeProgressCalculation = true;

		public static KeyframeLinkAlt From(KeyframeableValueAlt containingValue, IEnumerable<int> frames)
		{
			KeyframeLinkAlt keyframeLinkAlt = new KeyframeLinkAlt
			{
				ContainingValue = containingValue,
				keyframes = frames.Select(containingValue.GetKeyframeAt).ToList()
			};

			foreach (KeyframeAlt keyframe in keyframeLinkAlt.keyframes)
			{
				keyframe.containingLink = keyframeLinkAlt;
			}

			return keyframeLinkAlt;
		}
	}
}