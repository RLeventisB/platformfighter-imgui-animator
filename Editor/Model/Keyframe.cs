using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Editor.Model
{
    public class Keyframe : IComparable<Keyframe>
    {
        public KeyframeLink ContainingLink = null;
        public int Frame { get; set; }
        public object Value { get; set; }
        public static implicit operator Keyframe(int value) => new Keyframe(value, null);
        public Keyframe(int frame, object data)
        {
            Frame = frame;
            Value = data;
        }
        public int CompareTo(Keyframe other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            return Frame.CompareTo(other.Frame);
        }
        public override int GetHashCode()
        {
            return Value.GetType().GetHashCode() ^ Frame;
        }
    }
    public class KeyframeLink : IEnumerable<Keyframe>
    {
        public InterpolationType InterpolationType { get; set; }
        private readonly List<Keyframe> keyframes = new List<Keyframe>();
        public int Length => Keyframes.Count;
        public float menuY;
        public AnimationTrack track;
        public IReadOnlyList<Keyframe> Keyframes
        {
            get => keyframes;
        }
        public Keyframe FirstKeyframe { get; private set; }
        public Keyframe LastKeyframe { get; private set; }
        public KeyframeLink(IEnumerable<Keyframe> keyframes)
        {
            foreach (var keyframe in keyframes)
            {
                Add(keyframe, false);
            }
            CalculateBorderKeyframes();
            InterpolationType = InterpolationType.Lineal;
        }
        public IEnumerator<Keyframe> GetEnumerator()
        {
            return Keyframes.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Clear()
        {
            foreach (var keyframe in keyframes)
            {
                keyframe.ContainingLink = null;
            }
            keyframes.Clear();
        }
        public void Remove(Keyframe frame)
        {
            frame.ContainingLink = null;
            keyframes.Remove(frame);

            if (keyframes.Count == 0)
                FirstKeyframe = LastKeyframe = null;
            else
                CalculateBorderKeyframes();
        }
        public void CalculateBorderKeyframes()
        {
            if (keyframes.Count == 0)
            {
                FirstKeyframe = null;
                LastKeyframe = null;
            }
            else if (keyframes.Count == 1)
            {
                FirstKeyframe = keyframes[0];
                LastKeyframe = keyframes[0];
            }
            else
            {
                keyframes.Sort();
                FirstKeyframe = keyframes.First();
                LastKeyframe = keyframes.Last();
            }
        }
        public void Add(Keyframe frame, bool sort = true)
        {
            if (frame.ContainingLink != this)
            {
                frame.ContainingLink = this;
                keyframes.Add(frame);
                if (sort)
                    CalculateBorderKeyframes();
            }
        }

        public KeyframeLink ExtractToNewLink()
        {
            KeyframeLink newLink = new KeyframeLink(Keyframes)
            {
                InterpolationType = InterpolationType,
                menuY = menuY,
                track = track
            };
            foreach (var keyframe in Keyframes)
            {
                keyframe.ContainingLink = newLink;
            }
            menuY = float.NaN;
            track = null;
            keyframes.Clear();
            FirstKeyframe = null;
            LastKeyframe = null;
            return newLink;
        }

    }
    public enum InterpolationType : byte
    {
        Lineal, Squared, InverseSquared, BounceIn, BounceOut, BounceInOut, ElasticIn, ElasticOut, ElasticInOut, SmoothStep, Cubed, InverseCubed, CubedSmoothStep
    }
}