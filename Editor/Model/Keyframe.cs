#region
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
#endregion

namespace Editor.Model
{
	[DebuggerDisplay("{Value}")]
	public class Keyframe : IComparable<Keyframe>
	{
		public readonly KeyframeableValue ContainingValue;
		private object _value;
		public KeyframeLink ContainingLink;

		public Keyframe(KeyframeableValue containingValue, int frame, object data)
		{
			ContainingValue = containingValue;
			Frame = frame;
			Value = data;
		}

		public int Frame { get; set; }
		public object Value
		{
			get => _value;
			set
			{
				_value = value;
				ContainingValue?.InvalidateCachedValue();
			}
		}

		public int CompareTo(Keyframe other) => Frame.CompareTo(other.Frame);

		public static implicit operator Keyframe(int value) => new Keyframe(null, value, default);

		public override int GetHashCode() => Value.GetType().GetHashCode() ^ Frame;
	}
	public class KeyframeLink : IEnumerable<Keyframe>
	{
		public readonly ImmutableArray<Keyframe> Keyframes;
		public readonly KeyframeableValue linkedValue;

		public KeyframeLink(KeyframeableValue linkedValue, IEnumerable<Keyframe> keyframes)
		{
			this.linkedValue = linkedValue;
			Keyframes = keyframes.ToImmutableArray().Sort(); // ????

			CalculateBorderKeyframes();
			InterpolationType = InterpolationType.Lineal;
		}

		public Keyframe this[int index] => GetAt(index);
		public InterpolationType InterpolationType { get; set; }
		public int Length => Keyframes.Length;
		public Keyframe FirstKeyframe { get; private set; }
		public Keyframe LastKeyframe { get; private set; }

		public IEnumerator<Keyframe> GetEnumerator() => Keyframes.ToList().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public Keyframe GetAt(int index) => Keyframes[index];

		public KeyframeLink Add(Keyframe keyframe)
		{
			return new KeyframeLink(linkedValue, Keyframes.Add(keyframe));
		}

		public KeyframeLink Remove(Keyframe keyframe)
		{
			return new KeyframeLink(linkedValue, Keyframes.Remove(keyframe));
		}

		public void CalculateBorderKeyframes()
		{
			if (Keyframes.Length == 0)
			{
				FirstKeyframe = default;
				LastKeyframe = default;
			}
			else if (Keyframes.Length == 1)
			{
				FirstKeyframe = Keyframes[0];
				LastKeyframe = Keyframes[0];
			}
			else
			{
				FirstKeyframe = Keyframes.First();
				LastKeyframe = Keyframes.Last();
			}
		}
	}
	public enum InterpolationType : byte
	{
		Lineal, Squared, InverseSquared, BounceIn, BounceOut, BounceInOut, ElasticIn, ElasticOut, ElasticInOut, SmoothStep, Cubed, InverseCubed, CubedSmoothStep
	}
}