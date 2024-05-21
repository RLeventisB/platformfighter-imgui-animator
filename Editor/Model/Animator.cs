using System;
using System.Collections.Generic;
using Editor.Gui;
using Editor.Model.Interpolators;
using ImGuiNET;

namespace Editor.Model
{
    public class Animator
    {
        private readonly Dictionary<Type, IInterpolator> _interpolators;
        private readonly Dictionary<string, HashSet<int>> _groups;
        private readonly Dictionary<int, AnimationTrack> _tracks;

        public Action OnKeyframeChanged;

        private int _currentKeyframe;
        private int _framesPerSecond = 120;
        private float _frameTime = 1.0f / 120f;
        private float _frameTimer = 0f;
        private bool _isPlayingBackward;
        private bool _isPlayingForward;
        private bool _isLooping;

        public int CurrentKeyframe
        {
            get => _currentKeyframe;
            set
            {
                if (value != _currentKeyframe)
                {
                    _currentKeyframe = value;
                    OnKeyframeChanged?.Invoke();
                }
            }
        }

        public int FPS
        {
            get => _framesPerSecond;
            set
            {
                _framesPerSecond = value;
                _frameTime = 1.0f / _framesPerSecond;
            }
        }

        public bool Looping => _isLooping;

        public bool Playing => _isPlayingBackward || _isPlayingForward;

        public bool PlayingBackward => _isPlayingBackward;

        public bool PlayingForward => _isPlayingForward;

        public Animator()
        {
            _groups = new Dictionary<string, HashSet<int>>();
            _tracks = new Dictionary<int, AnimationTrack>();
            _interpolators = new Dictionary<Type, IInterpolator>(8);
            FPS = 120;
        }

        public void AddInterpolator<T>(Func<float, T, T, T> interpolator)
        {
            var type = typeof(T);
            if (_interpolators.ContainsKey(type))
                throw new Exception($"Interpolator for type {type.Name} already exists");

            _interpolators[type] = new DelegatedInterpolator<T>(interpolator);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _groups.Keys.GetEnumerator();
        }

        public IEnumerable<int> EnumerateGroupTrackIds(string groupName)
        {
            return _groups[groupName];
        }

        public AnimationTrack GetTrack(int trackId)
        {
            return _tracks[trackId];
        }

        public void PlayBackward()
        {
            if (_isPlayingForward)
                _isPlayingForward = false;

            _isPlayingBackward = !_isPlayingBackward;
        }

        public void PlayForward()
        {
            if (_isPlayingBackward)
                _isPlayingBackward = false;

            _isPlayingForward = !_isPlayingForward;
        }

        public void Stop()
        {
            // _frameTime = 0.0f;
            _isPlayingForward = _isPlayingBackward = false;
        }

        public void ToggleLooping()
        {
            _isLooping = !_isLooping;
        }

        public int GetFirstFrame()
        {
            int firstFrame = int.MaxValue;
            foreach (var track in _tracks.Values)
            {
                if (track.HasKeyframes() && track[0].Frame < firstFrame)
                    firstFrame = track[0].Frame;
            }

            return firstFrame == int.MaxValue ? 0 : firstFrame;
        }

        public int GetLastFrame()
        {
            int lastFrame = int.MinValue;
            foreach (var track in _tracks.Values)
            {
                if (track.HasKeyframes())
                {
                    var lastIndex = track.Count - 1;
                    if (track[lastIndex].Frame > lastFrame)
                        lastFrame = track[lastIndex].Frame;
                }
            }

            return lastFrame == int.MinValue ? 0 : lastFrame;
        }

        public int GetPreviousFrame(int? frame = null)
        {
            var f = frame ?? _currentKeyframe;
            var previousFrame = GetFirstFrame();
            foreach (var track in _tracks.Values)
            {
                if (!track.HasKeyframes())
                    continue;

                var index = track.GetBestIndex(f) - 1;
                if (index < 0)
                    index = 0;
                else if (index >= track.Count)
                    index = track.Count - 1;

                var kf = track[index];

                if (kf.Frame > previousFrame)
                    previousFrame = kf.Frame;
            }

            return f < previousFrame ? f : previousFrame;
        }

        public int GetNextFrame(int? frame = null)
        {
            var f = frame ?? _currentKeyframe;
            var nextFrame = GetLastFrame();
            foreach (var track in _tracks.Values)
            {
                if (!track.HasKeyframes())
                    continue;

                var index = track.GetExactIndex(f);
                if (index < 0)
                    index = ~index;
                else
                    index++;

                if (index < 0)
                    index = 0;
                else if (index >= track.Count)
                    index = track.Count - 1;

                var kf = track[index];

                if (kf.Frame == _currentKeyframe)
                    continue;

                if (kf.Frame < nextFrame)
                    nextFrame = kf.Frame;
            }

            return f > nextFrame ? f : nextFrame;
        }

        public void Update(float deltaTime)
        {
            if (!Playing)
                return;

            _frameTimer += deltaTime;

            var lastFrame = GetLastFrame();
            var firstFrame = GetFirstFrame();

            while (_frameTimer >= _frameTime)
            {
                _frameTimer -= _frameTime;

                if (_isPlayingBackward)
                    CurrentKeyframe--;
                else if (_isPlayingForward)
                    CurrentKeyframe++;

                if (_isLooping && HasKeyframes())
                {
                    if (CurrentKeyframe > lastFrame)
                        CurrentKeyframe = firstFrame;
                    else if (CurrentKeyframe < firstFrame)
                        CurrentKeyframe = lastFrame;

                }
            }
        }
        public void UpdateTimelineInputs()
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                PlayForward();
            }
            if (ImGui.IsKeyPressed(ImGuiKey.L))
            {
                ToggleLooping();
            }
            var lastFrame = GetLastFrame();
            var firstFrame = GetFirstFrame();

            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
            {
                CurrentKeyframe--;
                if (_isLooping && HasKeyframes() && CurrentKeyframe < firstFrame)
                    CurrentKeyframe = lastFrame;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
            {
                CurrentKeyframe++;
                if (_isLooping && HasKeyframes() && CurrentKeyframe > lastFrame)
                    CurrentKeyframe = firstFrame;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Home))
            {
                CurrentKeyframe = GetFirstFrame();
                if (CurrentKeyframe <= ImGuiEx.visibleStartingFrame)
                    ImGuiEx.visibleStartingFrame = CurrentKeyframe;
                else if (CurrentKeyframe >= ImGuiEx.visibleEndingFrame)
                    ImGuiEx.visibleStartingFrame += (CurrentKeyframe - ImGuiEx.visibleEndingFrame) + 1;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.End))
            {
                CurrentKeyframe = GetLastFrame();
                if (CurrentKeyframe <= ImGuiEx.visibleStartingFrame)
                    ImGuiEx.visibleStartingFrame = CurrentKeyframe;
                else if (CurrentKeyframe >= ImGuiEx.visibleEndingFrame)
                    ImGuiEx.visibleStartingFrame += (CurrentKeyframe - ImGuiEx.visibleEndingFrame) + 1;

            }
        }
        /*public bool Interpolate(int trackId, out object value)
        {
            value = null;
            if (!_tracks.ContainsKey(trackId))
                return false;

            var track = _tracks[trackId];
            if (track.Count <= 0)
                return false;

            var interpolator = _interpolators[track.Type];
            var keyFrameIndex = track.GetExactIndex(_currentKeyframe);
            Keyframe firstKf, secondKf;
            // in between 2 frames
            if (keyFrameIndex >= 0)
            {
                if (keyFrameIndex == track.Count - 1 || keyFrameIndex == 0 && track.Count == 1)
                {
                    value = track[keyFrameIndex].Value;
                    return true;
                }
                else
                {
                    firstKf = track[keyFrameIndex];
                    secondKf = track[keyFrameIndex + 1];
                }
            }
            else // before or after frames
            {
                var newIndex = ~keyFrameIndex;
                if (newIndex == track.Count)
                {
                    value = track[newIndex - 1].Value;
                    return true;
                }
                else if (newIndex == 0)
                {
                    value = track[newIndex].Value;
                    return true;
                }
                else
                {
                    firstKf = track[newIndex - 1];
                    secondKf = track[newIndex];
                }
            }
            var progress = (_currentKeyframe - firstKf.Frame) / (float)(secondKf.Frame - firstKf.Frame);
            float lerpValue = progress;
            switch (firstKf.InterpolationType)
            {
                case InterpolationType.Squared:
                    lerpValue *= lerpValue;
                    break;
                case InterpolationType.InverseSquared:
                    lerpValue = 1 - (1 - progress) * (1 - progress);
                    break;
                case InterpolationType.SmoothStep:
                    // lerpValue = MathHelper.Lerp(progress * progress, 1 - (1 - progress) * (1 - progress), progress);
                    lerpValue = Easing.Quadratic.InOut(progress);
                    break;
                case InterpolationType.Cubed:
                    lerpValue *= lerpValue * lerpValue;
                    break;
                case InterpolationType.InverseCubed:
                    lerpValue = 1 - (1 - progress) * (1 - progress) * (1 - progress);
                    break;
                case InterpolationType.CubedSmoothStep:
                    // lerpValue = MathHelper.Lerp(progress * progress * progress, 1 - (1 - progress) * (1 - progress) * (1 - progress), progress);
                    lerpValue = Easing.Cubic.InOut(progress);
                    break;
                case InterpolationType.ElasticOut:
                    lerpValue = Easing.Elastic.Out(progress)
                    //  1 + MathF.Sin(6.2831f - 13f * (1 + progress) * MathHelper.PiOver2) * MathF.Pow(2, -6f * progress);
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
            // $"{progress} -> {lerpValue}".Log();
            value = interpolator.Interpolate(lerpValue, firstKf.Value, secondKf.Value);

            return true;
        }*/
        public bool Interpolate(int trackId, out object value)
        {
            value = null;
            if (!_tracks.ContainsKey(trackId))
                return false;

            var track = _tracks[trackId];
            if (track.Count <= 0)
                return false;

            var interpolator = _interpolators[track.Type];
            var keyFrameIndex = track.GetExactIndex(_currentKeyframe);
            KeyframeLink link;
            if (keyFrameIndex >= 0)
            {
                link = track[keyFrameIndex].ContainingLink;
            }
            else // son las 2 de la mañana no voy a implementar frames negativos
            {
                return false;
                var newIndex = ~keyFrameIndex;
                if (newIndex == track.Count)
                {
                    value = track[newIndex - 1].Value;
                    return true;
                }
                else if (newIndex == 0)
                {
                    value = track[newIndex].Value;
                    return true;
                }
                else
                {
                    link = track[newIndex - 1].ContainingLink;
                }
            }
            var progress = (_currentKeyframe - link.FirstKeyframe.Frame) / (float)(link.LastKeyframe.Frame - link.FirstKeyframe.Frame);
            float lerpValue = progress;
            switch (InterpolationType.Lineal)
            {
                case InterpolationType.Squared:
                    lerpValue *= lerpValue;
                    break;
                case InterpolationType.InverseSquared:
                    lerpValue = 1 - (1 - progress) * (1 - progress);
                    break;
                case InterpolationType.SmoothStep:
                    // lerpValue = MathHelper.Lerp(progress * progress, 1 - (1 - progress) * (1 - progress), progress);
                    lerpValue = Easing.Quadratic.InOut(progress);
                    break;
                case InterpolationType.Cubed:
                    lerpValue *= lerpValue * lerpValue;
                    break;
                case InterpolationType.InverseCubed:
                    lerpValue = 1 - (1 - progress) * (1 - progress) * (1 - progress);
                    break;
                case InterpolationType.CubedSmoothStep:
                    // lerpValue = MathHelper.Lerp(progress * progress * progress, 1 - (1 - progress) * (1 - progress) * (1 - progress), progress);
                    lerpValue = Easing.Cubic.InOut(progress);
                    break;
                case InterpolationType.ElasticOut:
                    lerpValue = Easing.Elastic.Out(progress)/*  1 + MathF.Sin(6.2831f - 13f * (1 + progress) * MathHelper.PiOver2) * MathF.Pow(2, -6f * progress) */;
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
            // $"{progress} -> {lerpValue}".Log();
            int i = (int)(link.Keyframes.Length * progress);
            if (i >= link.Keyframes.Length)
                value = link.LastKeyframe.Value;
            else
                value = interpolator.Interpolate(lerpValue, link.Keyframes[i], link.Keyframes[i + 1]);

            return true;
        }

        public void AddTrack(string groupName, AnimationTrack track)
        {
            var trackId = GetTrackKey(groupName, track.Id);
            _tracks[trackId] = track;

            if (!_groups.ContainsKey(groupName))
                _groups[groupName] = new HashSet<int> { trackId };
            else
                _groups[groupName].Add(trackId);
        }

        /// <summary>
        /// Returns the track id
        /// </summary>
        public int CreateTrack(Type type, string groupName, string trackName)
        {
            var trackId = GetTrackKey(groupName, trackName);
            if (!_tracks.ContainsKey(trackId))
                _tracks[trackId] = new AnimationTrack(type, trackName);

            if (!_groups.ContainsKey(groupName))
                _groups[groupName] = new HashSet<int> { trackId };
            else
                _groups[groupName].Add(trackId);

            return trackId;
        }

        public void InsertKeyframe(int trackId, object value, int? keyframe = null)
        {
            var kf = keyframe ?? _currentKeyframe;
            var track = _tracks[trackId];

            var index = track.GetBestIndex(kf);
            if (track.HasKeyframeAtFrame(kf))
                track[index].Value = value;
            else
                track.Insert(index, new Keyframe(kf, value));
        }

        public void RemoveKeyframe(int trackId, int? keyframe = null)
        {
            var kf = keyframe ?? _currentKeyframe;
            var track = _tracks[trackId];

            var index = track.GetBestIndex(kf);
            if (track.HasKeyframeAtFrame(kf))
                track.RemoveAt(index);
        }

        public bool HasKeyframes()
        {
            bool hasKeyframes = false;
            foreach (var groupsKey in _groups.Keys)
            {
                hasKeyframes = hasKeyframes || GroupHasKeyframes(groupsKey);
            }

            return hasKeyframes;
        }

        public bool GroupHasKeyframes(string groupName)
        {
            var hasGroup = _groups.ContainsKey(groupName);
            if (!hasGroup)
                return false;

            var hasKeyframes = false;
            foreach (var trackId in _groups[groupName])
            {
                var track = _tracks[trackId];
                hasKeyframes = hasKeyframes || track.HasKeyframes();
            }

            return hasKeyframes;
        }

        public bool GroupHasKeyframeAtFrame(string groupName, int frame)
        {
            var hasGroup = _groups.ContainsKey(groupName);
            if (!hasGroup)
                return false;

            foreach (var trackId in _groups[groupName])
            {
                var track = _tracks[trackId];
                if (track.HasKeyframeAtFrame(frame))
                    return true;
            }

            return false;
        }

        public int GetTrackKey(string groupName, string trackName)
        {
            return $"{groupName}_{trackName}".GetHashCode();
        }

        public bool ChangeGroupName(string oldName, string newName)
        {
            if (_groups.ContainsKey(oldName))
            {
                var trackIds = _groups[oldName];
                _groups.Remove(oldName);
                _groups[newName] = trackIds;

                return true;
            }

            return false;
        }

        public void ChangeTrackId(string groupName, string trackName, int oldId)
        {
            if (_groups.ContainsKey(groupName) && _tracks.ContainsKey(oldId))
            {
                var newId = GetTrackKey(groupName, trackName);
                var trackIds = _groups[groupName];
                trackIds.Remove(oldId);
                trackIds.Add(newId);

                var track = _tracks[oldId];
                track.Id = trackName;
                _tracks.Remove(oldId);
                _tracks[newId] = track;
            }
        }
    }
}