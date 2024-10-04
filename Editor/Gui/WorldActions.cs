using Editor.Graphics;
using Editor.Objects;

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

			if (!EditorApplication.selectedData.IsEmpty() && EditorApplication.selectedData.Type is SelectionType.Graphic or SelectionType.Hitbox)
			{
				selectedObjectIsBeingDragged = EditorApplication.currentDragAction is not null && EditorApplication.currentDragAction.ActionName is "MoveGraphicEntityObject" or "MoveHitboxEntityObject";

				if (EditorApplication.selectedData.IsLone())
				{
					IAnimationObject obj = EditorApplication.selectedData.GetLoneObject();
					bool isThisObjectHovered = obj.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe);

					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
					{
						switch (obj)
						{
							case HitboxAnimationObject hitboxObject:
							{
								HitboxLine selectedLine = hitboxObject.GetSelectedLine(Input.MouseWorld);

								if (selectedLine == HitboxLine.None)
								{
									if (isThisObjectHovered)
									{
										EditorApplication.SetDragAction(new MoveAnimationObjectPositionAction([hitboxObject.Position]));
									}
								}
								else
								{
									EditorApplication.SetDragAction(new HitboxMoveSizeDragAction(selectedLine, hitboxObject));
									isThisObjectHovered = true;
								}

								break;
							}
							case TextureAnimationObject textureObject:
								if (isThisObjectHovered)
									EditorApplication.SetDragAction(new MoveAnimationObjectPositionAction([textureObject.Position]));

								break;
						}
					}

					// check if the current object is selected!!

					selectedObjectOrActionsWasHovered = EntityActions.DoGraphicEntityActions(EditorApplication.selectedData) || isThisObjectHovered;
				}
				else
				{
					foreach (SelectedObject obj in EditorApplication.selectedData)
					{
						bool isThisObjectHovered = obj.AnimationObject.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe);
						selectedObjectOrActionsWasHovered |= isThisObjectHovered;
					}

					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && selectedObjectOrActionsWasHovered)
					{
						switch (EditorApplication.selectedData.Type)
						{
							case SelectionType.Graphic:
								EditorApplication.SetDragAction(new MoveAnimationObjectPositionAction(EditorApplication.selectedData.Select(v => ((TextureAnimationObject)v.AnimationObject).Position).ToArray()));

								break;
							case SelectionType.Hitbox:
								EditorApplication.SetDragAction(new MoveAnimationObjectPositionAction(EditorApplication.selectedData.Select(v => ((HitboxAnimationObject)v.AnimationObject).Position).ToArray()));

								break;
						}
					}
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
						if (!entity.IsBeingHovered(Input.MouseWorld, null))
							continue;

						if (canSelectNewObject)
							EditorApplication.selectedData.SetOrAdd(entity);

						break;
					}
				}
				else
				{
					foreach (TextureAnimationObject entity in EditorApplication.State.Animator.RegisteredGraphics.OrderByDescending(v => v.ZIndex.CachedValue))
					{
						if (!entity.IsBeingHovered(Input.MouseWorld, null))
							continue;

						if (!selectedObjectIsBeingDragged && !selectedObjectOrActionsWasHovered)
						{
							EditorApplication.hoveredEntityName = entity.Name;
						}

						if (canSelectNewObject)
							EditorApplication.selectedData.SetOrAdd(entity);

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