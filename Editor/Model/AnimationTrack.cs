using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Editor.Model
{
    public class AnimationTrack : IReadOnlyList<Keyframe>
    {
        public Type Type { get; }
        public string PropertyId { get; set; }
        public string EntityId { get; set; }
        public int Count => _keyframes.Count;
        public Keyframe this[int index] => _keyframes[index];
        private readonly List<Keyframe> _keyframes;
        public readonly List<KeyframeLink> links;
        public AnimationTrack(Type type, string propertyId, string entityId)
        {
            Type = type;
            PropertyId = propertyId;
            EntityId = entityId;
            _keyframes = new List<Keyframe>();
            links = new List<KeyframeLink>();
        }

        public IEnumerator<Keyframe> GetEnumerator()
        {
            return _keyframes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(Keyframe value)
        {
            _keyframes.Add(value);
        }
        public void AddLink(KeyframeLink link)
        {
            foreach (var keyframe in link)
            {
                keyframe.ContainingLink = link;
            }

            links.Add(link);
        }
        public void RemoveLink(KeyframeLink link)
        {
            foreach (var keyframe in link)
            {
                keyframe.ContainingLink = null;
            }
            links.Remove(link);
        }
        public void Insert(int index, Keyframe value)
        {
            _keyframes.Insert(index, value);
        }

        public void RemoveAt(int index)
        {
            _keyframes[index].ContainingLink?.Remove(_keyframes[index]);
            _keyframes.RemoveAt(index);
        }

        public List<Keyframe> GetRange(int start, int count)
        {
            return _keyframes.GetRange(start, count);
        }

        public bool HasKeyframes()
        {
            return _keyframes.Count > 0;
        }

        public bool HasKeyframeAtFrame(int frame)
        {
            return GetExactIndex(frame) >= 0;
        }

        public int GetExactIndex(Keyframe value)
        {
            return _keyframes.BinarySearch(value);
        }

        public int GetBestIndex(Keyframe value)
        {
            var foundIndex = GetExactIndex(value);
            return foundIndex >= 0 ? foundIndex : ~foundIndex;
        }
        public ref Keyframe GetKeyframeReferenceAt(int index)
        {
            return ref CollectionsMarshal.AsSpan(_keyframes)[index];
        }
    }
}