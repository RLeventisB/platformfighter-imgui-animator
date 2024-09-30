using Editor.Gui;
using Editor.Model;

using ImGuiNET;

using System;

namespace Editor
{
	public class ChangeHitboxAngleAction : DragAction
	{
		public Vector2 CenterToMeasure { get; init; }
		public Action<float> SetNewAngle {get; init;}
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
			Vector2 diff = (CenterToMeasure - Input.MousePos);
			SetNewAngle.Invoke(MathF.Atan2(diff.Y, diff.X));
		}

		public override void OnRelease()
		{
			Vector2 diff = (CenterToMeasure - Input.MousePos);
			SetNewAngle.Invoke(MathF.Atan2(diff.Y, diff.X));
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
			EditorApplication.selectedData = new SelectionData(HitboxAnimationObjectReference); // just in case you click out of the box
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

			HitboxAnimationObjectReference.Size.SetKeyframeValue(null,  bottomRight - topLeft);
			HitboxAnimationObjectReference.Position.SetKeyframeValue(null,  topLeft + HitboxAnimationObjectReference.Size.CachedValue / 2);
		}

		public override void OnCancel()
		{
			HitboxAnimationObjectReference.Position.SetKeyframeValue(null,  OldPosition);
			HitboxAnimationObjectReference.Size.SetKeyframeValue(null, OldSize);
		}
	}
	public class MoveAnimationObjectPositionAction : DragAction
	{
		public Vector2KeyframeValue PositionValue { get; init; }
		public Vector2 oldCachedValue, accumulatedDifference;
		public bool affectAllKeyframes;

		public MoveAnimationObjectPositionAction(Vector2KeyframeValue positionValue) : base("MoveAnimObjectPosition", 3f, true)
		{
			PositionValue = positionValue;
			oldCachedValue = positionValue.CachedValue;
			affectAllKeyframes = ImGui.IsKeyDown(ImGuiKey.ModShift);
			EditorApplication.State.Animator.Stop();
		}
		
		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
		{
			accumulatedDifference += worldDifference;
			PositionValue.SetKeyframeValue(null, oldCachedValue + accumulatedDifference, true);
		}

		public override void OnRelease()
		{
			if (affectAllKeyframes)
			{
				foreach (Keyframe keyframe in PositionValue.keyframes)
				{
					keyframe.Value = (Vector2)keyframe.Value + accumulatedDifference;
				}
				PositionValue.InvalidateCachedValue();
			}
			else
			{
				PositionValue.SetKeyframeValue(null, oldCachedValue + accumulatedDifference);
			}
		}

		public override void OnCancel()
		{
			PositionValue.SetKeyframeValue(null, oldCachedValue, true);
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
				Keyframe keyframe = value.GetKeyframeReferenceAt(index);
				value.RemoveAt(index);
				keyframe.Frame = hoveringFrame;
				value.Add(keyframe);
				value.SortFrames();
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

		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
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
			HasStartedMoving = distanceForMove == 0;
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

			if (!HasStartedMoving && Vector2.DistanceSquared(cursorPos, StartPos) > DistanceToStartMoving) // waiting for big movement
			{
				OnMoveDrag(cursorPos - StartPos, worldCursorPos - StartPosWorld);
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