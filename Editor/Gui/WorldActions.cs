using Editor.Graphics;
using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;

namespace Editor.Gui
{
	public static class WorldActions
	{
		public static IEntity HoveredEntity; // not intended for external use btw yes i need this note to remember

		public static void Draw()
		{
			bool popupOpen = ImGui.IsPopupOpen("EntityContextMenuPopup");
			if (!popupOpen)
				HoveredEntity = null;

			if (Timeline.HitboxMode)
			{
				foreach (HitboxEntity entity in EditorApplication.State.HitboxEntities.Values)
				{
					if (!entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
						continue;

					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
						EditorApplication.selectedData = new SelectionData(entity);

					if (!popupOpen)
					{
						HoveredEntity = entity;
					}

					break;
				}
			}
			else
			{
				foreach (TextureEntity entity in EditorApplication.State.GraphicEntities.Values)
				{
					if (!entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
						continue;

					EditorApplication.hoveredEntityName = entity.Name;
					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
						EditorApplication.selectedData = new SelectionData(entity);

					if (!popupOpen)
					{
						HoveredEntity = entity;
					}

					break;
				}
			}

			if (ImGui.IsWindowFocused())
			{
				if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
				{
					Camera.Move(new Vector3(-Input.MouseWorldDelta.X, -Input.MouseWorldDelta.Y, 0));
				}

				if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && HoveredEntity != null) // for some reasn ismouseclicked doesnt function on the first frame
				{
					string name = HoveredEntity.Name;

					if (HoveredEntity is TextureEntity)
					{
						EditorApplication.SetDragAction(new DelegateDragAction("MoveGraphicEntityObject",
							delegate
							{
								TextureEntity selectedTextureEntity = EditorApplication.State.GraphicEntities[name];
								selectedTextureEntity.Position.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, selectedTextureEntity.Position.CachedValue + Input.MouseWorldDelta);
							}));
					}
					else if (HoveredEntity is HitboxEntity hitboxEntity)
					{
						HitboxLine selectedLine = hitboxEntity.GetSelectedLine(Input.MouseWorld);

						if (selectedLine == HitboxLine.None)
						{
							EditorApplication.SetDragAction(new DelegateDragAction("MoveHitboxEntityObject",
								delegate
								{
									HitboxEntity selectedTextureEntity = EditorApplication.State.HitboxEntities[name];
									selectedTextureEntity.Position += Input.MouseWorldDelta;
								}));
						}
						else
						{
							EditorApplication.SetDragAction(new HitboxMoveSizeDragAction(selectedLine, hitboxEntity));
						}
					}
				}
			}

			bool hovered = EntityActions.DoGraphicEntityActions(EditorApplication.selectedData);

			if (ImGui.IsWindowFocused() && ImGui.IsWindowHovered())
			{
				if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !hovered)
				{
					EditorApplication.selectedData.Empty();

					if (Timeline.HitboxMode)
					{
						foreach (HitboxEntity entity in EditorApplication.State.Animator.RegisteredHitboxes)
						{
							if (!entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
								continue;

							EditorApplication.selectedData = new SelectionData(entity);

							break;
						}
					}
					else
					{
						foreach (TextureEntity entity in EditorApplication.State.Animator.RegisteredGraphics)
						{
							if (!entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
								continue;

							EditorApplication.selectedData = new SelectionData(entity);

							break;
						}
					}
				}
			}

			if (HoveredEntity == null)
				return;

			if (ImGui.BeginPopupContextWindow("EntityContextMenuPopup", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.AnyPopup))
			{
				ImGui.Text("cuidado");
				ImGui.EndPopup();
			}
		}
	}
}