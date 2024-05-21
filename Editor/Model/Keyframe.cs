using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Editor.Model
{
    public class Keyframe : IComparable<Keyframe>
    {
        public InterpolationType InterpolationType { get; set; }
        public KeyframeLink ContainingLink = null;
        public int Frame { get; set; }
        public object Value { get; set; }
        public static implicit operator Keyframe(int value) => new Keyframe(value, null);
        public Keyframe(int frame, object data)
        {
            Frame = frame;
            Value = data;
            InterpolationType = InterpolationType.Lineal;
        }

        public int CompareTo(Keyframe other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Frame.CompareTo(other.Frame);
        }
    }
    public class KeyframeLink : IEnumerable<Keyframe>
    {
        private Keyframe[] keyframes;

        public Keyframe[] Keyframes
        {
            get => keyframes;
            set
            {
                IEnumerable<Keyframe> orderedKeyframes = value.OrderBy(x => x.Frame);
                keyframes = orderedKeyframes.ToArray();
                FirstKeyframe = orderedKeyframes.First();
                LastKeyframe = orderedKeyframes.Last();
            }
        }
        public Keyframe FirstKeyframe { get; set; }
        public Keyframe LastKeyframe { get; set; }
        public KeyframeLink(Keyframe[] keyframes)
        {
            Keyframes = keyframes;
        }
        public IEnumerator<Keyframe> GetEnumerator()
        {
            return ((IEnumerable<Keyframe>)Keyframes).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }
    public enum InterpolationType : byte
    {
        Lineal, Squared, InverseSquared, BounceIn, BounceOut, BounceInOut, ElasticIn, ElasticOut, ElasticInOut, SmoothStep, Cubed, InverseCubed, CubedSmoothStep
    }
}