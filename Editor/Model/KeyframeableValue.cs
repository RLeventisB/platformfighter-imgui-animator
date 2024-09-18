using Editor.Model.Interpolators;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Editor.Model
{
	public class Vector2KeyframeValue : KeyframeableValue
	{
		public Vector2KeyframeValue(IEntity entity, Vector2 defaultValue, string name, params string[] tags) : base(entity, defaultValue, name, typeof(Vector2), tags)
		{
		}

		public Vector2 CachedValue => (Vector2)cachedValue.value;

		public Vector2 Interpolate(int frame)
		{
			Interpolate(this, frame, Vector2Interpolator, out object value2);

			return (Vector2)value2;
		}

		public bool TryInterpolate(int frame, out Vector2 value)
		{
			bool success = Interpolate(this, frame, Vector2Interpolator, out object value2);
			value = (Vector2)value2;

			return success;
		}

		public override void CacheValue(int frame)
		{
			Interpolate(this, frame, Vector2Interpolator, out object value);
			cachedValue = (value, frame);
		}
	}
	public class FloatKeyframeValue : KeyframeableValue
	{
		public FloatKeyframeValue(IEntity entity, float defaultValue, string name, params string[] tags) : base(entity, defaultValue, name, typeof(float), tags)
		{
		}

		public float CachedValue => (float)cachedValue.value;

		public float Interpolate(int frame)
		{
			Interpolate(this, frame, FloatInterpolator, out object value2);

			return (float)value2;
		}

		public bool TryInterpolate(int frame, out float value)
		{
			bool success = Interpolate(this, frame, FloatInterpolator, out object value2);
			value = (float)value2;

			return success;
		}

		public override void CacheValue(int frame)
		{
			Interpolate(this, frame, FloatInterpolator, out object value);
			cachedValue = (value, frame);
		}
	}
	public class IntKeyframeValue : KeyframeableValue
	{
		public IntKeyframeValue(IEntity entity, int defaultValue, string name, params string[] tags) : base(entity, defaultValue, name, typeof(int), tags)
		{
		}

		public int CachedValue => (int)cachedValue.value;

		public int Interpolate(int frame)
		{
			Interpolate(this, frame, FloatInterpolator, out object value2);

			return (int)value2;
		}

		public bool TryInterpolate(int frame, out int value)
		{
			bool success = Interpolate(this, frame, FloatInterpolator, out object value2);
			value = (int)value2;

			return success;
		}

		public override void CacheValue(int frame)
		{
			Interpolate(this, frame, IntegerInterpolator, out object value);
			cachedValue = (value, frame);
		}
	}
	[DebuggerDisplay("{Name}")]
	public abstract class KeyframeableValue
	{
		public static readonly IInterpolator Vector2Interpolator = new DelegatedInterpolator<Vector2>(
			(fraction, first, second) => first + (second - first) * fraction,
			(fraction, values) => CubicHermiteInterpolate(values, fraction));
		public static readonly IInterpolator IntegerInterpolator = new DelegatedInterpolator<int>(
			(fraction, first, second) => (int)(first + (second - first) * fraction),
			(fraction, values) => (int)CubicHermiteInterpolate(values.Select(v => (float)v).ToArray(), fraction));
		public static readonly IInterpolator FloatInterpolator = new DelegatedInterpolator<float>(
			(fraction, first, second) => first + (second - first) * fraction,
			(fraction, values) => CubicHermiteInterpolate(values, fraction));
		public readonly object DefaultValue;

		public readonly List<Keyframe> keyframes;
		public readonly List<KeyframeLink> links;
		public readonly ImmutableArray<string> tags;
		public readonly Type type;
		protected (object value, int frame) cachedValue;

		protected KeyframeableValue(IEntity entity, object defaultValue, string name, Type type, string[] tags)
		{
			DefaultValue = defaultValue;
			cachedValue = (DefaultValue, -1);
			Owner = entity;
			Name = name;
			this.tags = [..tags];
			this.type = type;

			keyframes = new List<Keyframe>
			{
				new Keyframe(this, 0, DefaultValue)
			};

			links = new List<KeyframeLink>();
		}

		public IEntity Owner { get; init; }
		public string Name { get; init; }
		public ref Keyframe this[int index] => ref CollectionsMarshal.AsSpan(keyframes)[index];
		public int KeyframeCount => keyframes.Count;
		public int FirstFrame => HasKeyframes() ? keyframes[0].Frame : -1;
		public int LastFrame => HasKeyframes() ? keyframes[KeyframeCount - 1].Frame : -1;
		public Keyframe FirstKeyframe => HasKeyframes() ? keyframes[0] : null;
		public Keyframe LastKeyframe => HasKeyframes() ? keyframes[KeyframeCount - 1].Frame : null;

		public int Add(Keyframe value)
		{
			int index = FindIndexByKeyframe(value);

			if (index >= 0)
				keyframes[index] = value;
			else
			{
				keyframes.Insert(~index, value);
			}

			InvalidateCachedValue();

			return index;
		}

		public void RemoveAt(int index)
		{
			Keyframe keyframe = this[index];

			if (keyframe.ContainingLink != null)
				keyframe.ContainingLink = keyframe.ContainingLink.Remove(keyframes[index]);

			keyframes.RemoveAt(index);

			InvalidateCachedValue();
		}

		public void AddLink(KeyframeLink link)
		{
			foreach (Keyframe keyframe in link)
			{
				keyframe.ContainingLink = link;
			}

			links.Add(link);

			InvalidateCachedValue();
		}

		public void RemoveLink(KeyframeLink link)
		{
			foreach (Keyframe keyframe in link)
			{
				keyframe.ContainingLink = null;
			}

			links.Remove(link);

			InvalidateCachedValue();
		}

		public List<Keyframe> GetRange(int start, int count) => keyframes.GetRange(start, count);

		public bool HasKeyframes() => keyframes != null && keyframes.Count > 0;

		public bool HasKeyframeAtFrame(int frame) => GetKeyframe(frame) != null;

		public Keyframe GetKeyframe(int frame)
		{
			int foundIndex = FindIndexByKeyframe(frame);

			return foundIndex >= 0 ? keyframes[foundIndex] : null;
		}

		public int FindIndexByKeyframe(Keyframe value) => keyframes.BinarySearch(value);

		public int GetIndexOrNext(Keyframe value)
		{
			int foundIndex = FindIndexByKeyframe(value);

			return foundIndex >= 0 ? foundIndex : ~foundIndex;
		}

		public ref Keyframe GetKeyframeReferenceAt(int index) => ref CollectionsMarshal.AsSpan(keyframes)[index];

		public static bool Interpolate(KeyframeableValue keyframeValue, int frame, IInterpolator interpolator, out object value)
		{
			value = keyframeValue.DefaultValue;

			if (!keyframeValue.HasKeyframes())
				return false;

			if (keyframeValue.cachedValue.frame == frame)
			{
				value = keyframeValue.cachedValue.value;

				return true;
			}

			int keyFrameIndex = keyframeValue.FindIndexByKeyframe(frame);
			Keyframe keyframe;

			if (keyFrameIndex >= 0)
			{
				keyframe = keyframeValue.keyframes[keyFrameIndex];
			}
			else // esto no es para frames negativos!!!! soy extremadamente estupido!!!!!!!
			{
				keyFrameIndex = ~keyFrameIndex;

				if (keyFrameIndex == 0)
					return false;

				keyframe = keyframeValue.keyframes[keyFrameIndex - 1]; // obtener anterior frame
			}

			KeyframeLink link = keyframe.ContainingLink;

			if (link is null || link.Keyframes.Count == 1)
			{
				value = keyframe.Value;
				keyframeValue.cachedValue = (value, frame);

				return true;
			}

			float progress = (frame - link.FirstKeyframe.Frame) / (float)(link.LastKeyframe.Frame - link.FirstKeyframe.Frame);
			float lerpValue = progress;

			switch (link.InterpolationType)
			{
				case InterpolationType.Squared:
					lerpValue *= lerpValue;

					break;
				case InterpolationType.InverseSquared:
					lerpValue = 1 - (1 - progress) * (1 - progress);

					break;
				case InterpolationType.SmoothStep:
					lerpValue = Easing.Quadratic.InOut(progress);

					break;
				case InterpolationType.Cubed:
					lerpValue *= lerpValue * lerpValue;

					break;
				case InterpolationType.InverseCubed:
					lerpValue = 1 - (1 - progress) * (1 - progress) * (1 - progress);

					break;
				case InterpolationType.CubedSmoothStep:
					lerpValue = Easing.Cubic.InOut(progress);

					break;
				case InterpolationType.ElasticOut:
					lerpValue = Easing.Elastic.Out(progress);

					break;
				case InterpolationType.ElasticInOut:
					lerpValue = Easing.Elastic.InOut(progress);

					break;
				case InterpolationType.ElasticIn:
					lerpValue = Easing.Elastic.In(progress);

					break;
				case InterpolationType.BounceIn:
					lerpValue = Easing.Bounce.In(progress);

					break;
				case InterpolationType.BounceOut:
					lerpValue = Easing.Bounce.Out(progress);

					break;
				case InterpolationType.BounceInOut:
					lerpValue = Easing.Bounce.InOut(progress);

					break;
			}

			int i = (int)(link.Keyframes.Count * progress);

			if (i >= link.Keyframes.Count)
				value = link.LastKeyframe.Value;
			else
			{
				object[] objects = link.Keyframes.Select(v => v.Value).ToArray();
				value = interpolator.Interpolate(lerpValue, objects);
			}

			keyframeValue.cachedValue = (value, frame);

			return true;
		}

		public abstract void CacheValue(int frame);

		public void InvalidateCachedValue()
		{
			cachedValue = (DefaultValue, -1);
			CacheValue(EditorApplication.State.Animator.CurrentKeyframe);
		}

		public static IInterpolator ResolveInterpolator(Type type)
		{
			switch (Activator.CreateInstance(type))
			{
				case float:
					return FloatInterpolator;
				case int:
					return IntegerInterpolator;
				case Vector2:
					return Vector2Interpolator;
			}

			return null;
		}

		public Keyframe SetKeyframeValue(int frame, object data)
		{
			Keyframe keyframe = new Keyframe(this, frame, data);
			int index = Add(keyframe);

			if (index < 0) // keyframe was added
			{
				// check if last keyframe has same value
				int indexBefore = ~index - 1;

				if (indexBefore >= 0 && keyframes[indexBefore].Value == data)
				{
					RemoveAt(~index);

					return null;
				}
			}
			else // keyframe replaced old keyframe
			{
				// check if last keyframe has same value

				if (index - 1 >= 0 && keyframes[index - 1].Value.Equals(data))
				{
					RemoveAt(index);

					return null;
				}
			}

			InvalidateCachedValue();

			return keyframe;
		}

		public bool RemoveKeyframe(int frame)
		{
			int index = FindIndexByKeyframe(frame);

			if (index < 0)
				return false;

			keyframes.RemoveAt(index);

			return true;
		}

		public void SortFrames()
		{
			keyframes.Sort();
		}

		public int IndexOfKeyframe(Keyframe keyframe)
		{
			return keyframes.IndexOf(keyframe);
		}
	}
}