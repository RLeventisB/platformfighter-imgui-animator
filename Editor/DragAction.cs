using Editor.Graphics;
using Editor.Gui;
using Editor.Objects;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System;
using System.Linq;

namespace Editor
{
	public class ChangeHitboxAngleAction : DragAction
	{
		public Vector2 CenterToMeasure { get; init; }
		public Action<float> SetNewAngle { get; init; }
		public float InitialAngle;

		public ChangeHitboxAngleAction(Vector2 center, float initialAngle, Action<float> setNewAngle) : base("ChangeHitboxAngleAction", 0f, true)
		{
			CenterToMeasure = center;
			SetNewAngle = setNewAngle;
			InitialAngle = initialAngle;
			EditorApplication.State.Animator.Stop();
		}

		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
		{
			Vector2 diff = Input.MousePos - CenterToMeasure;
			SetNewAngle.Invoke(MathHelper.ToDegrees(MathF.Atan2(diff.Y, diff.X)));
		}

		public override void OnRelease()
		{
			Vector2 diff = Input.MousePos - CenterToMeasure;
			SetNewAngle.Invoke(MathHelper.ToDegrees(MathF.Atan2(diff.Y, diff.X)));
		}

		public override void OnCancel()
		{
			SetNewAngle.Invoke(InitialAngle);
		}
	}
	public class HitboxMoveSizeDragAction : DragAction
	{
		public HitboxLine SelectedLine { get; }
		public HitboxAnimationObject HitboxAnimationObjectReference { get; }
		public readonly Vector2 OldPosition, OldSize;

		public HitboxMoveSizeDragAction(HitboxLine selectedLine, HitboxAnimationObject hitboxAnimationObject) : base("ResizeHitboxObject", 1, true)
		{
			SelectedLine = selectedLine;
			HitboxAnimationObjectReference = hitboxAnimationObject;
			OldPosition = hitboxAnimationObject.Position.CachedValue;
			OldSize = hitboxAnimationObject.Size.CachedValue;
			EditorApplication.selectedData.Set(HitboxAnimationObjectReference); // just in case you click out of the box (todo: this doesnt work)
		}

		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
		{
			Vector2 topLeft = HitboxAnimationObjectReference.Position.CachedValue - HitboxAnimationObjectReference.Size.CachedValue / 2;
			Vector2 bottomRight = HitboxAnimationObjectReference.Position.CachedValue + HitboxAnimationObjectReference.Size.CachedValue / 2;

			switch (SelectedLine)
			{
				case HitboxLine.Top:
					topLeft.Y = Math.Min(Input.MouseWorld.Y, bottomRight.Y);

					break;
				case HitboxLine.Right:
					bottomRight.X = Math.Max(Input.MouseWorld.X, topLeft.X);

					break;
				case HitboxLine.Bottom:
					bottomRight.Y = Math.Max(Input.MouseWorld.Y, topLeft.Y);

					break;
				case HitboxLine.Left:
					topLeft.X = Math.Min(Input.MouseWorld.X, bottomRight.X);

					break;
			}

			HitboxAnimationObjectReference.Size.SetKeyframeValue(null, bottomRight - topLeft);
			HitboxAnimationObjectReference.Position.SetKeyframeValue(null, topLeft + HitboxAnimationObjectReference.Size.CachedValue / 2);
		}

		public override void OnCancel()
		{
			HitboxAnimationObjectReference.Position.SetKeyframeValue(null, OldPosition);
			HitboxAnimationObjectReference.Size.SetKeyframeValue(null, OldSize);
		}
	}
	public class MoveAnimationObjectPositionAction : DragAction
	{
		public (Vector2KeyframeValue value, Vector2 startPos)[] Values { get; init; }
		public Vector2 accumulatedDifference;
		public bool affectAllKeyframes;

		public MoveAnimationObjectPositionAction(Vector2KeyframeValue[] positionValue) : base("MoveAnimObjectPosition", 1f / Camera.Zoom, true)
		{
			Values = positionValue.Select(v => (v, v.CachedValue)).ToArray();
			affectAllKeyframes = ImGui.IsKeyDown(ImGuiKey.ModShift) && positionValue.Length == 1;
			EditorApplication.State.Animator.Stop();
		}

		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
		{
			accumulatedDifference += worldDifference;

			foreach ((Vector2KeyframeValue value, Vector2 startPos) in Values)
			{
				value.SetKeyframeValue(null, startPos + accumulatedDifference, true);
			}
		}

		public override void OnRelease()
		{
			if (affectAllKeyframes)
			{
				foreach ((Vector2KeyframeValue value, Vector2 startPos) in Values)
				{
					foreach (Keyframe keyframe in value.keyframes)
					{
						if (keyframe.Frame == EditorApplication.State.Animator.CurrentKeyframe)
						{
							keyframe.Value = startPos + accumulatedDifference;
						}
						else
						{
							keyframe.Value = (Vector2)keyframe.Value + accumulatedDifference;
						}
					}

					value.InvalidateCachedValue();
				}
			}
			else
			{
				foreach ((Vector2KeyframeValue value, Vector2 startPos) in Values)
				{
					value.SetKeyframeValue(null, startPos + accumulatedDifference);
				}
			}
		}

		public override void OnCancel()
		{
			foreach ((Vector2KeyframeValue value, Vector2 startPos) in Values)
			{
				value.SetKeyframeValue(null, startPos, true);
			}
		}
	}
	public class MoveKeyframeDelegateAction : DragAction
	{
		private readonly (KeyframeableValue value, int index)[] _dataPairs;

		public MoveKeyframeDelegateAction((KeyframeableValue value, int index)[] dataPairs) : base("MoveKeyframes", 10, true)
		{
			_dataPairs = dataPairs;
		}

		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
		{
			int hoveringFrame = GetHoveringFrame();
			ImGui.BeginTooltip();
			ImGui.SetTooltip(_dataPairs.Length > 1 ? $"Moviendo {_dataPairs.Length} keyframes\nFrame nuevo: {hoveringFrame}" : $"Frame nuevo: {hoveringFrame}");

			ImGui.EndTooltip();
		}

		private static int GetHoveringFrame() => Timeline.GetFrameForTimelinePos((Input.MousePos.X - Timeline.timelineRegionMin.X) / Timeline.TimelineZoom);

		public override void OnRelease()
		{
			if (!HasStartedMoving)
				return;

			int hoveringFrame = GetHoveringFrame();

			foreach ((KeyframeableValue value, int index) in _dataPairs)
			{
				Keyframe keyframe = value.keyframes[index];
				
				KeyframeLink link = KeyframeableValue.FindContainingLink(keyframe.ContainingValue, keyframe);
				
				if (value.HasKeyframeAtFrame(hoveringFrame))
				{
					value.RemoveKeyframe(hoveringFrame, false);
				}
				
				link?.Add(hoveringFrame);

				link?.Remove(keyframe);

				keyframe.Frame = hoveringFrame;
				
				value.SortFrames();
				value.InvalidateCachedValue();
			}
		}
	}
	public class DelegateDragAction : DragAction
	{
		public delegate void OnCancelDragAction(Vector2 screenStartPos);
		public delegate void OnMoveDragAction(Vector2 worldDifference, Vector2 difference);
		public delegate void OnReleaseDragAction(bool didAnything);

		public static readonly OnReleaseDragAction DoNothingRelease = _ => { };
		public static readonly OnCancelDragAction DoNothingCancel = _ => { };
		public readonly OnCancelDragAction OnCancelAction;
		public readonly OnMoveDragAction OnMoveAction;
		public readonly OnReleaseDragAction OnReleaseAction;

		public DelegateDragAction(string name, OnMoveDragAction onMoveAction, float distanceForMove = 0) : this(name, onMoveAction, DoNothingRelease, DoNothingCancel, distanceForMove)
		{
		}

		public DelegateDragAction(string name, OnMoveDragAction onMoveAction, OnReleaseDragAction onReleaseAction, float distanceForMove = 0) : this(name, onMoveAction, onReleaseAction, DoNothingCancel, distanceForMove)
		{
		}

		public DelegateDragAction(string name, OnMoveDragAction onMoveAction, OnReleaseDragAction onReleaseAction, OnCancelDragAction onCancelAction, float distanceForMove = 0) : base(name, distanceForMove)
		{
			OnReleaseAction = onReleaseAction;
			OnMoveAction = onMoveAction;
			OnCancelAction = onCancelAction;
		}

		public override void OnMoveDrag(Vector2 screenDifference, Vector2 worldDifference)
		{
			OnMoveAction?.Invoke(worldDifference, screenDifference);
		}

		public override void OnRelease()
		{
			OnReleaseAction?.Invoke(HasStartedMoving);
		}

		public override void OnCancel()
		{
			OnCancelAction?.Invoke(StartPos);
		}
	}
	public abstract class DragAction
	{
		public readonly string ActionName;
		public readonly float DistanceToStartMoving;
		public bool HasStartedMoving, CancellableWithEscape;
		public Vector2 StartPos, StartPosWorld;

		public DragAction(string name, float distanceForMove = 0, bool cancellableWithEscape = false)
		{
			DistanceToStartMoving = distanceForMove * distanceForMove;
			ActionName = name;
			CancellableWithEscape = cancellableWithEscape;
			StartPos = Input.MousePos;
			StartPosWorld = Input.MouseWorld;
		}

		public void Update()
		{
			if (CancellableWithEscape && ImGui.IsKeyPressed(ImGuiKey.Escape))
			{
				OnCancel();
				EditorApplication.currentDragAction = null;

				return;
			}

			Vector2 cursorPos = Input.MousePos;
			Vector2 oldCursorPos = Input.PreviousMousePos;
			Vector2 worldCursorPos = Input.MouseWorld;
			Vector2 oldWorldCursorPos = Input.PreviousMouseWorld;

			if (!HasStartedMoving && Vector2.DistanceSquared(cursorPos, StartPos) >= DistanceToStartMoving) // waiting for big movement
			{
				OnMoveDrag(worldCursorPos - StartPosWorld, cursorPos - StartPos);
				HasStartedMoving = true;
			}
			else if (HasStartedMoving) // has started moving
			{
				Vector2 diff = cursorPos - oldCursorPos;
				Vector2 worldDiff = worldCursorPos - oldWorldCursorPos;

				if (diff.X != 0 || diff.Y != 0)
					OnMoveDrag(worldDiff, diff);
			}

			if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
			{
				OnRelease();
				EditorApplication.currentDragAction = null;
			}
		}

		public virtual void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference) { }

		public virtual void OnRelease() { }

		public virtual void OnCancel() { }
	}
}