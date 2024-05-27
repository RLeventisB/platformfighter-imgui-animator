using System;
using System.Collections.Generic;
using System.Linq;
using Editor.Gui;
using Editor.Model.Interpolators;
using ImGuiNET;

namespace Editor.Model
{
    public class Animator
    {
        private readonly Dictionary<Type, IInterpolator> _interpolators;
        public readonly Dictionary<string, HashSet<int>> registeredEntities;
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
            registeredEntities = new Dictionary<string, HashSet<int>>();
            _tracks = new Dictionary<int, AnimationTrack>();
            _interpolators = new Dictionary<Type, IInterpolator>(8);
            FPS = 120;
        }

        public void AddInterpolator<T>(Func<float, T, T, T> pairInterpolator, Func<float, T[], T> arrayInterpolator)
        {
            var type = typeof(T);
            if (_interpolators.ContainsKey(type))
                throw new Exception($"Interpolator for type {type.Name} already exists");

            _interpolators[type] = new DelegatedInterpolator<T>(pairInterpolator, arrayInterpolator);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return registeredEntities.Keys.GetEnumerator();
        }

        public IEnumerable<int> EnumerateEntityTrackIds(string entityName)
        {
            return registeredEntities[entityName];
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
            if (ImGuiEx.keyframesToMove.Count > 0)
                Stop();
            bool creatingLink = ImGuiEx.temporaryLink.Length > 0;
            if (ImGuiEx.temporaryLink.Length > 1 && ImGui.IsKeyChordPressed(ImGuiKey.Enter))
            {
                ImGuiEx.temporaryLink.track.AddLink(ImGuiEx.temporaryLink.ExtractToNewLink());
            }
            if (creatingLink && ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                ImGuiEx.temporaryLink.Clear();
            }
            if (!creatingLink && ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                PlayForward();
            }
            if (!creatingLink && ImGui.IsKeyPressed(ImGuiKey.L))
            {
                ToggleLooping();
            }
            var lastFrame = GetLastFrame();
            var firstFrame = GetFirstFrame();

            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) && !creatingLink)
            {
                CurrentKeyframe--;
                if (_isLooping && HasKeyframes() && CurrentKeyframe < firstFrame)
                    CurrentKeyframe = lastFrame;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow) && !creatingLink)
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
                    ImGuiEx.visibleStartingFrame += CurrentKeyframe - ImGuiEx.visibleEndingFrame + 1;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.End))
            {
                CurrentKeyframe = GetLastFrame();
                if (CurrentKeyframe <= ImGuiEx.visibleStartingFrame)
                    ImGuiEx.visibleStartingFrame = CurrentKeyframe;
                else if (CurrentKeyframe >= ImGuiEx.visibleEndingFrame)
                    ImGuiEx.visibleStartingFrame += CurrentKeyframe - ImGuiEx.visibleEndingFrame + 1;

            }
        }
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
            Keyframe keyframe;
            if (keyFrameIndex >= 0)
            {
                keyframe = track[keyFrameIndex];
            }
            else // esto no es para frames negativos!!!! soy extremadamente estupido!!!!!!!
            {
                keyFrameIndex = ~keyFrameIndex;
                if (keyFrameIndex == track.Count || keyFrameIndex == 0)
                    return false;
                keyframe = track[keyFrameIndex - 1];

            }
            link = keyframe.ContainingLink;
            if (link is null || link.Keyframes.Count == 1)
            {
                value = keyframe.Value;
                return true;
            }

            var progress = (_currentKeyframe - link.FirstKeyframe.Frame) / (float)(link.LastKeyframe.Frame - link.FirstKeyframe.Frame);
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
                value = interpolator.Interpolate(lerpValue, link.Keyframes.Select(v => v.Value).ToArray());

            return true;
        }
        public void AddTrack(string entityName, AnimationTrack track)
        {
            var trackId = GetTrackKey(entityName, track.PropertyId);
            _tracks[trackId] = track;

            if (!registeredEntities.ContainsKey(entityName))
                registeredEntities[entityName] = new HashSet<int> { trackId };
            else
                registeredEntities[entityName].Add(trackId);
        }

        /// <summary>
        /// Returns the track id
        /// </summary>
        public int CreateTrack(Type type, string entityName, string trackName)
        {
            var trackId = GetTrackKey(entityName, trackName);
            if (!_tracks.ContainsKey(trackId))
                _tracks[trackId] = new AnimationTrack(type, trackName, entityName);

            if (registeredEntities.TryGetValue(entityName, out HashSet<int> value))
                value.Add(trackId);
            else
                registeredEntities[entityName] = new HashSet<int> { trackId };

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
            {
                track.RemoveAt(index);
            }
        }

        public bool HasKeyframes()
        {
            bool hasKeyframes = false;
            foreach (var entityKey in registeredEntities.Keys)
            {
                hasKeyframes = hasKeyframes || EntityHasKeyframes(entityKey);
            }

            return hasKeyframes;
        }

        public bool EntityHasKeyframes(string entityName)
        {
            var hasEntity = registeredEntities.ContainsKey(entityName);
            if (!hasEntity)
                return false;

            var hasKeyframes = false;
            foreach (var trackId in registeredEntities[entityName])
            {
                var track = _tracks[trackId];
                hasKeyframes = hasKeyframes || track.HasKeyframes();
            }

            return hasKeyframes;
        }

        public bool EntityHasKeyframeAtFrame(string entityName, int frame)
        {
            var hasEntity = registeredEntities.TryGetValue(entityName, out var tracks);
            if (!hasEntity)
                return false;

            foreach (var trackId in registeredEntities[entityName])
            {
                var track = _tracks[trackId];
                if (track.HasKeyframeAtFrame(frame))
                    return true;
            }

            return false;
        }

        public int GetTrackKey(string entityName, string trackName)
        {
            return $"{entityName}_{trackName}".GetHashCode();
        }

        public bool ChangeEntityName(string oldName, string newName)
        {
            if (registeredEntities.ContainsKey(oldName))
            {
                var trackIds = registeredEntities[oldName];
                registeredEntities.Remove(oldName);
                registeredEntities[newName] = trackIds;

                return true;
            }

            return false;
        }

        public void ChangeTrackId(string entityName, string trackName, int oldId)
        {
            if (registeredEntities.ContainsKey(entityName) && _tracks.ContainsKey(oldId))
            {
                var newId = GetTrackKey(entityName, trackName);
                var trackIds = registeredEntities[entityName];
                trackIds.Remove(oldId);
                trackIds.Add(newId);

                var track = _tracks[oldId];
                track.PropertyId = trackName;
                _tracks.Remove(oldId);
                _tracks[newId] = track;
            }
        }
    }
}