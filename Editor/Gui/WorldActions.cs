using Editor.Graphics;
using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System.Linq;

namespace Editor.Gui
{
	public static class WorldActions
	{
		public static void Draw()
		{
			bool windowFocused = ImGui.IsWindowHovered();
			bool selectedObjectOrActionsWasHovered = false;
			bool selectedObjectIsBeingDragged = false;

			if (windowFocused)
			{
				if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
				{
					Camera.Position += Input.MouseWorldDelta;
				}

				if (ImGui.GetIO().MouseWheel != 0)
				{
					Camera.Zoom.Target += ImGui.GetIO().MouseWheel;
					Camera.Zoom.Target = MathHelper.Clamp(Camera.Zoom.Target, 0.1f, 30f);
				}
			}

			if (!EditorApplication.selectedData.IsEmpty())
			{
				// check if the current object is selected!!
				if (EditorApplication.selectedData.ObjectSelectionType is SelectionType.Graphic or SelectionType.Hitbox)
				{
					selectedObjectIsBeingDragged = EditorApplication.currentDragAction is not null && EditorApplication.currentDragAction.ActionName is "MoveGraphicEntityObject" or "MoveHitboxEntityObject";
					bool selectedObjectBeingHovered = EditorApplication.selectedData.Reference.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe);

					if ((ImGui.IsMouseDragging(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Left)) && selectedObjectBeingHovered && windowFocused)
					{
						switch (EditorApplication.selectedData.Reference)
						{
							case TextureAnimationObject textureObject:
								EditorApplication.SetDragAction(new MoveAnimationObjectPositionAction(textureObject.Position));

								break;
							case HitboxAnimationObject hitboxEntity:
							{
								HitboxLine selectedLine = hitboxEntity.GetSelectedLine(Input.MouseWorld);

								if (selectedLine == HitboxLine.None)
								{
									EditorApplication.SetDragAction(new MoveAnimationObjectPositionAction(hitboxEntity.Position));
								}
								else
								{
									EditorApplication.SetDragAction(new HitboxMoveSizeDragAction(selectedLine, hitboxEntity));
								}

								break;
							}
						}
					}

					selectedObjectOrActionsWasHovered = EntityActions.DoGraphicEntityActions(EditorApplication.selectedData) || selectedObjectBeingHovered;
				}
			}

			if (windowFocused)
			{
				bool canSelectNewObject = ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !selectedObjectOrActionsWasHovered;

				if (canSelectNewObject) // search for new selected object, didnt wanna do two loops though
				{
					EditorApplication.selectedData.Empty();
				}

				if (Timeline.HitboxMode)
				{
					foreach (HitboxAnimationObject entity in EditorApplication.State.Animator.RegisteredHitboxes)
					{
						if (!entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
							continue;

						if (canSelectNewObject)
							EditorApplication.selectedData = new SelectionData(entity);

						break;
					}
				}
				else
				{
					bool foundHoveredEntity = false;

					foreach (TextureAnimationObject entity in EditorApplication.State.Animator.RegisteredGraphics.OrderByDescending(v => v.ZIndex.CachedValue))
					{
						if (!entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
							continue;

						if (canSelectNewObject)
							EditorApplication.selectedData = new SelectionData(entity);

						if (!selectedObjectIsBeingDragged && !selectedObjectOrActionsWasHovered)
						{
							EditorApplication.hoveredEntityName = entity.Name;
						}

						break;
					}
				}
			}

			if (ImGui.IsWindowFocused() && ImGui.BeginPopupContextWindow("EntityContextMenuPopup", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.AnyPopup))
			{
				ImGui.Text("cuidado");
				ImGui.EndPopup();
			}
		}
	}
}