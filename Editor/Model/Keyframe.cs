#region
using Editor.Gui;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
#endregion

namespace Editor.Model
{
	[DebuggerDisplay("Frame = {Frame}, Value = {Value}")]
	public class Keyframe : IComparable<Keyframe>
	{
		public readonly KeyframeableValue ContainingValue;
		private int _frame;
		private object _value;
		public KeyframeLink ContainingLink;

		public Keyframe(KeyframeableValue containingValue, int frame, object data)
		{
			ContainingValue = containingValue;
			Frame = frame;
			Value = data;
		}

		public int Frame
		{
			get => _frame;
			set
			{
				_frame = value;
				ContainingLink?.ChangedFrame(this);
			}
		}
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
		public readonly ImmutableSortedSet<Keyframe> Keyframes;
		public readonly KeyframeableValue linkedValue;
		private InterpolationType _interpolationType;
		public bool UseRelativeProgressCalculation = true;

		public KeyframeLink(KeyframeableValue linkedValue, IEnumerable<Keyframe> keyframes)
		{
			this.linkedValue = linkedValue;
			Keyframes = keyframes.ToImmutableSortedSet(); // ????

			InterpolationType = InterpolationType.Lineal;
		}

		public Keyframe this[int index] => GetAt(index);
		public InterpolationType InterpolationType
		{
			get => _interpolationType;
			set
			{
				_interpolationType = value;

				if (Timeline.selectedLink != null && Timeline.selectedLink.link == this)
				{
					Timeline.selectedLink.CalculateExtraData();
				}
			}
		}
		public int Length => Keyframes.Count;
		public Keyframe FirstKeyframe => Keyframes.FirstOrDefault((Keyframe)null);
		public Keyframe LastKeyframe => Keyframes.LastOrDefault((Keyframe)null);

		public Keyframe GetKeyframeClamped(int index) => Length== 0 ? null : Keyframes[Math.Clamp(index, 0, Length - 1)];
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

		public void ChangedFrame(Keyframe keyframe)
		{
		}
	}
	public enum InterpolationType : byte
	{
		Lineal, 
		Squared, InverseSquared,
		BounceIn, BounceOut, BounceInOut,
		ElasticIn, ElasticOut, ElasticInOut,
		SmoothStep,
		Cubed, InverseCubed, CubedSmoothStep, 
		SineIn, SineOut, SineInOut, 
		ExponentialIn, ExponentialOut, ExponentialInOut,
		CircularIn, CircularOut, CircularInOut,
		BackIn, BackOut, BackInOut
	}
}