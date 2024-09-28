using Editor.Gui;
using Editor.Model;

using ImGuiNET;

using System;

namespace Editor
{
	public class HitboxMoveSizeDragAction : DragAction
	{
		public HitboxLine SelectedLine { get; }
		public HitboxAnimationObject HitboxAnimationObjectReference { get; }
		public readonly Vector2 OldPosition, OldSize;

		public HitboxMoveSizeDragAction(HitboxLine selectedLine, HitboxAnimationObject hitboxAnimationObject) : base("ResizeHitboxObject", 1, true)
		{
			SelectedLine = selectedLine;
			HitboxAnimationObjectReference = hitboxAnimationObject;
			OldPosition = hitboxAnimationObject.Position;
			OldSize = hitboxAnimationObject.Size;
			EditorApplication.selectedData = new SelectionData(HitboxAnimationObjectReference); // just in case you click out of the box
		}

		public override void OnMoveDrag(Vector2 worldDifference, Vector2 screenDifference)
		{
			Vector2 topLeft = HitboxAnimationObjectReference.Position - HitboxAnimationObjectReference.Size / 2;
			Vector2 bottomRight = HitboxAnimationObjectReference.Position + HitboxAnimationObjectReference.Size / 2;

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

			HitboxAnimationObjectReference.Size = bottomRight - topLeft;
			HitboxAnimationObjectReference.Position = topLeft + HitboxAnimationObjectReference.Size / 2;
		}

		public override void OnCancel()
		{
			HitboxAnimationObjectReference.Position = OldPosition;
			HitboxAnimationObjectReference.Size = OldSize;
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
				value[index].Frame = hoveringFrame;
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