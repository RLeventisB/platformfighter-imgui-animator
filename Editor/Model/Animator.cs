#region
using Editor.Gui;

using ImGuiNET;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace Editor.Model
{
	public class Animator
	{
		public readonly EntityList<TextureEntity> RegisteredGraphics;
		public readonly EntityList<HitboxEntity> RegisteredHitboxes;

		private int _currentKeyframe;
		private int _framesPerSecond = 120;
		private float _frameTime = 1.0f / 120f;
		private float _frameTimer;

		public Action OnKeyframeChanged;

		public Animator(Dictionary<string, TextureEntity> graphicEntities, Dictionary<string, HitboxEntity> hitboxEntities)
		{
			RegisteredHitboxes = new EntityList<HitboxEntity>(hitboxEntities);
			RegisteredGraphics = new EntityList<TextureEntity>(graphicEntities);
			FPS = 120;
		}

		public int CurrentKeyframe
		{
			get => _currentKeyframe;
			set
			{
				if (value == _currentKeyframe)
					return;

				_currentKeyframe = value;
				OnKeyframeChanged?.Invoke();
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

		public bool Looping { get; private set; }

		public bool Playing => PlayingBackward || PlayingForward;

		public bool PlayingBackward { get; private set; }

		public bool PlayingForward { get; private set; }

		public void PlayBackward()
		{
			if (PlayingForward)
				PlayingForward = false;

			PlayingBackward = !PlayingBackward;
		}

		public void PlayForward()
		{
			if (PlayingBackward)
				PlayingBackward = false;

			PlayingForward = !PlayingForward;
		}

		public void Stop()
		{
			PlayingForward = PlayingBackward = false;
		}

		public void ToggleLooping()
		{
			Looping = !Looping;
		}

		public int GetFirstFrame()
		{
			int firstFrame = int.MaxValue;

			foreach (IEntity entity in GetAllEntities())
			{
				foreach (KeyframeableValue value in entity.EnumerateKeyframeableValues())
				{
					if (value.HasKeyframes() && value[0].Frame < firstFrame)
						firstFrame = value[0].Frame;
				}
			}

			return firstFrame == int.MaxValue ? 0 : firstFrame;
		}

		public int GetLastFrame()
		{
			int lastFrame = int.MinValue;

			foreach (IEntity entity in GetAllEntities())
			{
				foreach (KeyframeableValue value in entity.EnumerateKeyframeableValues())
				{
					if (value.HasKeyframes())
					{
						int lastIndex = value.KeyframeCount - 1;

						if (value[lastIndex].Frame > lastFrame)
							lastFrame = value[lastIndex].Frame;
					}
				}
			}

			return lastFrame == int.MinValue ? 0 : lastFrame;
		}

		public int GetPreviousFrame(int? frame = null)
		{
			int f = frame ?? _currentKeyframe;
			int previousFrame = GetFirstFrame();

			foreach (IEntity entity in GetAllEntities())
			{
				foreach (KeyframeableValue value in entity.EnumerateKeyframeableValues())
				{
					if (!value.HasKeyframes())
						continue;

					int index = value.GetIndexOrNext(f) - 1;

					if (index < 0)
						index = 0;
					else if (index >= value.KeyframeCount)
						index = value.KeyframeCount - 1;

					Keyframe kf = value[index];

					if (kf.Frame > previousFrame)
						previousFrame = kf.Frame;
				}
			}

			return f < previousFrame ? f : previousFrame;
		}

		public int GetNextFrame(int? frame = null)
		{
			int f = frame ?? _currentKeyframe;
			int nextFrame = GetLastFrame();

			foreach (IEntity entity in GetAllEntities())
			{
				foreach (KeyframeableValue value in entity.EnumerateKeyframeableValues())
				{
					if (!value.HasKeyframes())
						continue;

					int index = value.FindIndexByKeyframe(f);

					if (index < 0)
						index = ~index;
					else
						index++;

					if (index < 0)
						index = 0;
					else if (index >= value.KeyframeCount)
						index = value.KeyframeCount - 1;

					Keyframe kf = value[index];

					if (kf.Frame == _currentKeyframe)
						continue;

					if (kf.Frame < nextFrame)
						nextFrame = kf.Frame;
				}
			}

			return f > nextFrame ? f : nextFrame;
		}

		public void Update(float deltaTime)
		{
			if (!Playing)
				return;

			_frameTimer += deltaTime;

			int lastFrame = GetLastFrame();
			int firstFrame = GetFirstFrame();

			while (_frameTimer >= _frameTime)
			{
				_frameTimer -= _frameTime;

				if (PlayingBackward)
					CurrentKeyframe--;
				else if (PlayingForward)
					CurrentKeyframe++;

				if (Looping && HasKeyframes())
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
			bool creatingLink = Timeline.newLinkCreationData != null;

			if (creatingLink)
			{
				HandleNewLinkInputs();

				return;
			}

			if (ImGui.IsKeyPressed(ImGuiKey.Enter))
			{
				PlayForward();
			}

			if (ImGui.IsKeyPressed(ImGuiKey.L))
			{
				ToggleLooping();
			}

			int lastFrame = GetLastFrame();
			int firstFrame = GetFirstFrame();

			if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
			{
				CurrentKeyframe--;

				if (Looping && HasKeyframes() && CurrentKeyframe < firstFrame)
					CurrentKeyframe = lastFrame;
			}

			if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
			{
				CurrentKeyframe++;

				if (Looping && HasKeyframes() && CurrentKeyframe > lastFrame)
					CurrentKeyframe = firstFrame;
			}

			if (ImGui.IsKeyPressed(ImGuiKey.Home))
			{
				CurrentKeyframe = GetFirstFrame();

				if (CurrentKeyframe <= Timeline.visibleStartingFrame)
					Timeline.visibleStartingFrame = CurrentKeyframe;
				else if (CurrentKeyframe >= Timeline.visibleEndingFrame)
					Timeline.visibleStartingFrame += CurrentKeyframe - Timeline.visibleEndingFrame + 1;
			}

			if (ImGui.IsKeyPressed(ImGuiKey.End))
			{
				CurrentKeyframe = GetLastFrame();

				if (CurrentKeyframe <= Timeline.visibleStartingFrame)
					Timeline.visibleStartingFrame = CurrentKeyframe;
				else if (CurrentKeyframe >= Timeline.visibleEndingFrame)
					Timeline.visibleStartingFrame += CurrentKeyframe - Timeline.visibleEndingFrame + 1;
			}
		}

		private void HandleNewLinkInputs()
		{
			Stop();

			ImGuiIOPtr io = ImGui.GetIO();

			if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.LeftArrow) && Timeline.newLinkCreationData.Border > 0)
			{
				Timeline.newLinkCreationData.offset--;
			}

			if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.RightArrow) && Timeline.newLinkCreationData.Border < Timeline.newLinkCreationData.value.KeyframeCount - 1)
			{
				Timeline.newLinkCreationData.offset++;
			}

			if (ImGui.IsKeyPressed(ImGuiKey.Escape))
			{
				Timeline.newLinkCreationData = null;
			}

			if (Timeline.newLinkCreationData != null && Timeline.newLinkCreationData.Length > 1 && ImGui.IsKeyPressed(ImGuiKey.Enter))
			{
				Timeline.newLinkCreationData.CreateLink();
				Timeline.newLinkCreationData = null;
			}
		}

		public bool HasKeyframes()
		{
			return GetAllEntities().Any(EntityHasKeyframes);
		}

		public static bool EntityHasKeyframes(IEntity entity)
		{
			foreach (KeyframeableValue value in entity.EnumerateKeyframeableValues())
			{
				if (value.HasKeyframes())
					return true;
			}

			return false;
		}

		public int GetTrackKey(string entityName, string trackName) => $"{entityName}_{trackName}".GetHashCode();

		public List<IEntity> GetAllEntities()
		{
			List<IEntity> entityList = RegisteredGraphics.ListEntitiesBase();
			entityList.AddRange(RegisteredHitboxes.ListEntitiesBase());

			return entityList;
		}
	}
	public class EntityList<T> : IEnumerable<T> where T : IEntity
	{
		public readonly Dictionary<string, T> registry;

		public EntityList(Dictionary<string, T> registry)
		{
			this.registry = registry;
		}

		public T this[string name] => registry[name];

		public IEnumerator<T> GetEnumerator() => registry.Values.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public List<T> ListEntities() => registry.Values.ToList();

		public bool TryGetValue(string name, out T value)
		{
			return registry.TryGetValue(name, out value);
		}

		public List<IEntity> ListEntitiesBase() => registry.Values.Cast<IEntity>().ToList();

		public bool ChangeEntityName(string oldName, string newName)
		{
			if (registry.ContainsKey(oldName))
			{
				T entity = registry[oldName];
				registry.Remove(oldName);
				entity.Name = newName;
				registry[newName] = entity;

				return true;
			}

			return false;
		}

		public bool EntityHasKeyframeAtFrame(string entityName, int frame)
		{
			if (!registry.TryGetValue(entityName, out T entity))
				return false;

			foreach (KeyframeableValue value in entity.EnumerateKeyframeableValues())
			{
				if (value.HasKeyframeAtFrame(frame))
					return true;
			}

			return false;
		}
	}
}