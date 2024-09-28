﻿using Editor.Gui;
using Editor.Model.Interpolators;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Editor.Model
{
	public class Vector2KeyframeValue : KeyframeableValue
	{
		[JsonConstructor]
		public Vector2KeyframeValue()
		{
		}

		public Vector2KeyframeValue(TextureAnimationObject animationObject, Vector2 defaultValue, string name, bool createDefaultKeyframe = true) : base(animationObject, defaultValue, name, typeof(Vector2), createDefaultKeyframe)
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
		[JsonConstructor]
		public FloatKeyframeValue()
		{
		}

		public FloatKeyframeValue(TextureAnimationObject animationObject, float defaultValue, string name, bool createDefaultKeyframe = true) : base(animationObject, defaultValue, name, typeof(float), createDefaultKeyframe)
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
		[JsonConstructor]
		public IntKeyframeValue()
		{
		}

		public IntKeyframeValue(TextureAnimationObject animationObject, int defaultValue, string name, bool createDefaultKeyframe = true) : base(animationObject, defaultValue, name, typeof(int), createDefaultKeyframe)
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
		// its 1:32 am i dont want to refactor another parameter on this boilerplate code
		public static bool CacheValueOnInterpolate = true;

		public static readonly IInterpolator Vector2Interpolator = new DelegatedInterpolator<Vector2>(
			(fraction, first, second) => first + (second - first) * fraction,
			(fraction, values) => InterpolateCatmullRom(values, fraction * (values.Length - 1)));
		public static readonly IInterpolator IntegerInterpolator = new DelegatedInterpolator<int>(
			(fraction, first, second) => (int)(first + (second - first) * fraction),
			(fraction, values) => InterpolateCatmullRom(values, fraction * (values.Length - 1)));
		public static readonly IInterpolator FloatInterpolator = new DelegatedInterpolator<float>(
			(fraction, first, second) => first + (second - first) * fraction,
			(fraction, values) => InterpolateCatmullRom(values, fraction * (values.Length - 1)));

		[JsonIgnore]
		public object DefaultValue;

		[JsonInclude]
		public List<Keyframe> keyframes;
		[JsonInclude]
		public List<KeyframeLink> links;
		[JsonInclude]
		public List<string> tags;
		[JsonIgnore]
		protected (object value, int frame) cachedValue;

		public TextureAnimationObject Owner { get; init; }
		public string Name { get; init; }

		protected KeyframeableValue(TextureAnimationObject animationObject, object defaultValue, string name, Type type, bool createDefaultKeyframe = true) : this()
		{
			DefaultValue = defaultValue;
			cachedValue = (DefaultValue, -1);
			Owner = animationObject;
			Name = name;

			if (createDefaultKeyframe)
				keyframes.Add(new Keyframe(this, 0, DefaultValue));
		}

		[JsonConstructor]
		protected KeyframeableValue()
		{
			tags = new List<string>();
			keyframes = new List<Keyframe>();
			links = new List<KeyframeLink>();
		}

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
				keyframes[index].Value = value.Value;
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

			keyframe.ContainingLink?.Remove(keyframes[index]);

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

			if (Timeline.selectedLink != null && Timeline.selectedLink.link == link)
				Timeline.selectedLink = null;

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

			if (CacheValueOnInterpolate && keyframeValue.cachedValue.frame == frame)
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
				keyFrameIndex = ~keyFrameIndex - 1;

				if (keyFrameIndex < 0) // ok pq estaba esto en ingles peor eto pasa cuando el frame es negativo y antes del link creo
					keyFrameIndex = 0;

				keyframe = keyframeValue.keyframes[keyFrameIndex]; // obtener anterior frame
			}

			KeyframeLink link = keyframe.ContainingLink;

			if (link is null || link.Count == 1)
			{
				value = keyframe.Value;
				if (CacheValueOnInterpolate)
					keyframeValue.cachedValue = (value, frame);

				return true;
			}

			if (frame <= 0) // fast returns
			{
				value = link.FirstKeyframe.Value;

				return true;
			}

			if (frame >= link.LastKeyframe.Frame)
			{
				value = link.LastKeyframe.Value;

				return true;
			}

			int linkFrameDuration = link.LastKeyframe.Frame - link.FirstKeyframe.Frame;
			float progressedFrame = (frame - link.FirstKeyframe.Frame) / (float)linkFrameDuration;
			float usedFrame = progressedFrame;
			/*float progress;

			if (link.UseRelativeProgressCalculation)
			{
				float localProgress = (frame - keyframe.Frame) / (float)(link.Keyframes[keyFrameIndex + 1].Frame - keyframe.Frame);
				float frameProgress = (float)(keyframe.Frame - link.FirstKeyframe.Frame) / (link.LastKeyframe.Frame - link.FirstKeyframe.Frame);
				progress = (keyFrameIndex + localProgress) / (link.Length - 1);
			}
			else
			{
				progress = (frame - link.FirstKeyframe.Frame) / (float)(link.LastKeyframe.Frame - link.FirstKeyframe.Frame);
			}

			float lerpValue = progress;*/

			switch (link.InterpolationType)
			{
				case InterpolationType.Squared:
					usedFrame *= progressedFrame;

					break;
				case InterpolationType.InverseSquared:
					usedFrame = 1 - (1 - progressedFrame) * (1 - progressedFrame);

					break;
				case InterpolationType.SmoothStep:
					usedFrame = Easing.Quadratic.InOut(progressedFrame);

					break;
				case InterpolationType.Cubed:
					usedFrame *= progressedFrame * progressedFrame;

					break;
				case InterpolationType.InverseCubed:
					usedFrame = 1 - (1 - progressedFrame) * (1 - progressedFrame) * (1 - progressedFrame);

					break;
				case InterpolationType.CubedSmoothStep:
					usedFrame = Easing.Cubic.InOut(progressedFrame);

					break;
				case InterpolationType.ElasticOut:
					usedFrame = Easing.Elastic.Out(progressedFrame);

					break;
				case InterpolationType.ElasticInOut:
					usedFrame = Easing.Elastic.InOut(progressedFrame);

					break;
				case InterpolationType.ElasticIn:
					usedFrame = Easing.Elastic.In(progressedFrame);

					break;
				case InterpolationType.BounceIn:
					usedFrame = Easing.Bounce.In(progressedFrame);

					break;
				case InterpolationType.BounceOut:
					usedFrame = Easing.Bounce.Out(progressedFrame);

					break;
				case InterpolationType.BounceInOut:
					usedFrame = Easing.Bounce.InOut(progressedFrame);

					break;
				case InterpolationType.SineIn:
					usedFrame = Easing.Sinusoidal.In(progressedFrame);

					break;
				case InterpolationType.SineOut:
					usedFrame = Easing.Sinusoidal.Out(progressedFrame);

					break;
				case InterpolationType.SineInOut:
					usedFrame = Easing.Sinusoidal.InOut(progressedFrame);

					break;
				case InterpolationType.ExponentialIn:
					usedFrame = Easing.Exponential.In(progressedFrame);

					break;
				case InterpolationType.ExponentialOut:
					usedFrame = Easing.Exponential.Out(progressedFrame);

					break;
				case InterpolationType.ExponentialInOut:
					usedFrame = Easing.Exponential.InOut(progressedFrame);

					break;
				case InterpolationType.CircularIn:
					usedFrame = Easing.Circular.In(progressedFrame);

					break;
				case InterpolationType.CircularOut:
					usedFrame = Easing.Circular.Out(progressedFrame);

					break;
				case InterpolationType.CircularInOut:
					usedFrame = Easing.Circular.InOut(progressedFrame);

					break;
				case InterpolationType.BackIn:
					usedFrame = Easing.Back.In(progressedFrame);

					break;
				case InterpolationType.BackOut:
					usedFrame = Easing.Back.Out(progressedFrame);

					break;
				case InterpolationType.BackInOut:
					usedFrame = Easing.Back.InOut(progressedFrame);

					break;
			}

			if (link.UseRelativeProgressCalculation)
			{
				float interpolatedFrame = usedFrame * linkFrameDuration;
				keyFrameIndex = keyframeValue.FindIndexByKeyframe((int)interpolatedFrame);

				if (keyFrameIndex < 0)
				{
					keyFrameIndex = ~keyFrameIndex - 1;
				}

				keyFrameIndex = Math.Clamp(keyFrameIndex, 0, link.Count - 2);

				float localProgress = (interpolatedFrame - link.GetKeyframeClamped(keyFrameIndex).Frame) / (link.GetKeyframeClamped(keyFrameIndex + 1).Frame - link.GetKeyframeClamped(keyFrameIndex).Frame);
				usedFrame = (keyFrameIndex + localProgress) / (link.Count - 1);
			}

			object[] objects = link.Keyframes.Select(v => v.Value).ToArray();
			value = interpolator.Interpolate(usedFrame, objects);

			if (CacheValueOnInterpolate)
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

			if (SettingsManager.SetKeyframeOnModify)
			{
				Add(keyframe);

				InvalidateCachedValue();
			}
			else
			{
				cachedValue = (data, frame);
			}

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

		public KeyframeableValue CloneKeyframeData(KeyframeableValue other)
		{
			foreach (Keyframe keyframe in other.keyframes)
			{
				Add(new Keyframe(this, keyframe.Frame, keyframe.Value));
			}

			foreach (KeyframeLink link in other.links)
			{
				List<int> linkKeyframes = new List<int>();

				foreach (Keyframe keyframe in link)
				{
					linkKeyframes.Add(keyframe.Frame);
				}

				AddLink(new KeyframeLink(this, linkKeyframes.Select(GetKeyframe)));
			}

			return this;
		}

		public KeyframeableValue AddTags(IEnumerable<string> list)
		{
			tags.AddRange(list);

			return this;
		}

		public KeyframeableValue AddTag(string tag)
		{
			tags.Add(tag);

			return this;
		}
	}
}