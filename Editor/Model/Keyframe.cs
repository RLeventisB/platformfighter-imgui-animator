#region
using Editor.Gui;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
#endregion

namespace Editor.Model
{
	[DebuggerDisplay("Frame = {Frame}, Value = {Value}")]
	public class Keyframe : IComparable<Keyframe>
	{
		public KeyframeableValue ContainingValue;
		private int _frame;
		private object _value;
		[JsonInclude]
		public KeyframeLink ContainingLink;

		[JsonConstructor]
		public Keyframe()
		{
			Frame = -1;
			Value = null;
		}
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
	public class KeyframeLink : ICollection<Keyframe>
	{
		private List<Keyframe> _keyframes;
		public IReadOnlyList<Keyframe> Keyframes => _keyframes;
		public KeyframeableValue ContainingValue;
		private InterpolationType _interpolationType;
		public bool UseRelativeProgressCalculation = true;

		[JsonConstructor]
		public KeyframeLink()
		{
			_keyframes = new List<Keyframe>();
			InterpolationType = InterpolationType.Lineal;
		}
		public KeyframeLink(KeyframeableValue containingValue, IEnumerable<Keyframe> keyframes) : this()
		{
			ContainingValue = containingValue;
			AddRange(keyframes);
			_keyframes.Sort();

			InterpolationType = InterpolationType.Lineal;
		}

		private void AddRange(IEnumerable<Keyframe> keyframes)
		{
			_keyframes.AddRange(keyframes);
			_keyframes.Sort();
		}

		public Keyframe this[int index] => _keyframes[index];
		[JsonIgnore]
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
		public int Count => Keyframes.Count;
		public Keyframe FirstKeyframe => Keyframes.FirstOrDefault(-1);
		public Keyframe LastKeyframe => Keyframes.LastOrDefault(1);

		public Keyframe GetKeyframeClamped(int index) => Count== 0 ? null : Keyframes[Math.Clamp(index, 0, Count - 1)];
		public IEnumerator<Keyframe> GetEnumerator() => Keyframes.ToList().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public Keyframe GetAt(int index) => ContainingValue.GetKeyframeReferenceAt(index);

		public void Clear()
		{
			throw new NotSupportedException();
		}

		public bool Contains(Keyframe item) => throw new NotSupportedException();

		public void CopyTo(Keyframe[] array, int arrayIndex)
		{
			_keyframes.CopyTo(array, arrayIndex);
		}

		public bool IsReadOnly => false;

		public void Add(Keyframe item)
		{
			_keyframes.Add(item);
			_keyframes.Sort();
		}

		public bool Remove(Keyframe item)
		{
			bool remove = _keyframes.Remove(item);
			_keyframes.Sort();
			return remove;
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