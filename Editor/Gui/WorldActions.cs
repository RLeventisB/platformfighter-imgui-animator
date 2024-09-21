using Editor.Graphics;
using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;

namespace Editor.Gui
{
	public static class WorldActions
	{
		public static TextureEntity HoveredEntity; // not intended for external use btw yes i need this note to remember

		public static void Draw()
		{
			bool popupOpen = ImGui.IsPopupOpen("EntityContextMenuPopup");
			if (!popupOpen)
				HoveredEntity = null;
			foreach (TextureEntity entity in EditorApplication.State.GraphicEntities.Values)
			{
				bool isBeingHovered = entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe);

				if (!isBeingHovered)
					continue;

				EditorApplication.hoveredEntityName = entity.Name;
				if (!popupOpen)
				{
					HoveredEntity = entity;

				}
				
				break;
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
					EditorApplication.SetDragAction(new DelegateDragAction("MoveWorldObject",
						delegate
						{
							TextureEntity selectedTextureEntity = EditorApplication.State.GraphicEntities[name];
							selectedTextureEntity.Position.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, selectedTextureEntity.Position.CachedValue + Input.MouseWorldDelta);
						}));
				}
			}

			bool hovered = EntityActions.DoActions(EditorApplication.selectedEntityName);

			if (ImGui.IsWindowFocused() && ImGui.IsWindowHovered())
			{
				if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !hovered)
				{
					EditorApplication.selectedEntityName = string.Empty;

					foreach (IEntity v in EditorApplication.State.Animator.GetAllEntities())
					{
						if (v.Name == EditorApplication.selectedEntityName || !v.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe))
							continue;

						EditorApplication.selectedEntityName = v.Name;

						break;
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