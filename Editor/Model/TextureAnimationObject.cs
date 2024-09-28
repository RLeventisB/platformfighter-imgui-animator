using System.Collections.Generic;
using System.IO;

namespace Editor.Model
{
	public class TextureAnimationObject : IAnimationObject
	{
		public TextureAnimationObject(string name, string textureName)
		{
			Scale = new Vector2KeyframeValue(this, Vector2.One, ScaleProperty);
			FrameIndex = new IntKeyframeValue(this, 0, FrameIndexProperty);
			Rotation = new FloatKeyframeValue(this, 0, RotationProperty);
			Name = name;
			Position = new Vector2KeyframeValue(this, Vector2.Zero, PositionProperty);
			Transparency = new FloatKeyframeValue(this, 1, TransparencyProperty);
			ZIndex = new FloatKeyframeValue(this, 0, ZIndexProperty);
			TextureName = textureName;
		}

		public string TextureName { get; set; }
		public Vector2KeyframeValue Scale { get; set; }
		public IntKeyframeValue FrameIndex { get; set; }
		public FloatKeyframeValue Rotation { get; set; }

		public string Name { get; set; }
		public Vector2KeyframeValue Position { get; set; }
		public FloatKeyframeValue Transparency { get; set; }
		public FloatKeyframeValue ZIndex { get; set; }

		public bool IsBeingHovered(Vector2 mouseWorld, int frame)
		{
			TextureFrame texture = EditorApplication.State.GetTexture(TextureName);
			Vector2 size = texture.FrameSize.ToVector2() * Vec2Abs(Scale.Interpolate(frame));

			return IsInsideRectangle(Position.Interpolate(frame) - texture.Pivot + texture.FrameSize.ToVector2() / 2, size, Rotation.Interpolate(frame), mouseWorld);
		}

		public List<KeyframeableValue> EnumerateKeyframeableValues() => [Position, Scale, Rotation, FrameIndex, Transparency, ZIndex];

		public void Save(BinaryWriter writer)
		{
			writer.Write(Name);
			writer.Write(TextureName);

			List<KeyframeableValue> values = EnumerateKeyframeableValues();
			writer.Write(values.Count);

			foreach (KeyframeableValue value in values)
			{
				writer.Write(value.KeyframeCount);

				foreach (Keyframe keyframe in value.keyframes)
				{
					writer.Write(keyframe.Frame);

					switch (keyframe.Value)
					{
						case float floatValue:
							writer.Write(floatValue);

							break;
						case int intValue:
							writer.Write(intValue);

							break;
						case Vector2 vector2Value:
							writer.Write(vector2Value);

							break;
					}
				}

				writer.Write(value.links.Count);

				foreach (KeyframeLink link in value.links)
				{
					writer.Write((byte)link.InterpolationType);
					writer.Write(link.UseRelativeProgressCalculation);
					writer.Write(link.Keyframes.Count);

					foreach (Keyframe linkKeyframes in link.Keyframes)
					{
						writer.Write(linkKeyframes.Frame);
					}
				}
			}
		}

		public static TextureAnimationObject Load(BinaryReader reader)
		{
			string name = reader.ReadString();
			string textureId = reader.ReadString();
			TextureAnimationObject animationObject = new TextureAnimationObject(name, textureId);

			int propertyCount = reader.ReadInt32();
			List<KeyframeableValue> values = animationObject.EnumerateKeyframeableValues();

			for (int j = 0; j < propertyCount; j++)
			{
				int keyframeCount = reader.ReadInt32();
				KeyframeableValue value = values[j];

				for (int k = 0; k < keyframeCount; k++)
				{
					int frame = reader.ReadInt32();
					object data = null;

					switch (value.DefaultValue)
					{
						case float:
							data = reader.ReadSingle();

							break;
						case int:
							data = reader.ReadInt32();

							break;
						case Vector2:
							data = reader.ReadVector2();

							break;
					}

					Keyframe keyframe = new Keyframe(value, frame, data);
					value.Add(keyframe);
				}

				int linksCount = reader.ReadInt32();

				for (int k = 0; k < linksCount; k++)
				{
					List<Keyframe> keyframesInLink = new List<Keyframe>();
					InterpolationType type = (InterpolationType)reader.ReadByte();
					bool relativeThing = reader.ReadBoolean();
					keyframeCount = reader.ReadInt32();

					for (int l = 0; l < keyframeCount; l++)
					{
						int frame = reader.ReadInt32();
						keyframesInLink.Add(value.GetKeyframe(frame));
					}

					value.AddLink(new KeyframeLink(value, keyframesInLink)
					{
						InterpolationType = type,
						UseRelativeProgressCalculation = relativeThing
					});
				}
			}

			return animationObject;
		}
	}
}