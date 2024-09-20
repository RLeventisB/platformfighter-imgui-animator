using Editor.Graphics;
using Editor.Model;

using ImGuiNET;

namespace Editor.Gui
{
	public static class ContextMenu
	{
		public static bool IsOpen, IsHitbox;
		public static string SelectedEntityName = null;
		public static IEntity Entity;

		public static void Select(IEntity entity)
		{
			Entity = entity;
			IsOpen = true;
			IsHitbox = entity is HitboxEntity;
		}

		public static void Draw()
		{
			if (!IsOpen)
				return;

			Vector2 screenPos = Camera.WorldToScreen(Entity.Position.CachedValue);
			ImGui.SetCursorPosX(screenPos.X);
			ImGui.SetCursorPosY(screenPos.Y);

			if (ImGui.BeginPopupContextWindow("EntityContextMenu"))
			{
				ImGui.Text("cuidado");
				ImGui.EndPopup();
			}
		}
	}
}