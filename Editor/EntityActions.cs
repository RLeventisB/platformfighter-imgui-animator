using Editor.Graphics;
using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System;

namespace Editor
{
	public static class EntityActions
	{
		public static bool DoActions(string entityName)
		{
			if (string.IsNullOrEmpty(entityName) || !EditorApplication.State.GraphicEntities.TryGetValue(entityName, out TextureEntity entity))
				return false;

			Vector2 worldPos = entity.Position.CachedValue;

			float rotation = entity.Rotation.CachedValue;
			Vector2 scale = entity.Scale.CachedValue * Camera.Zoom;
			Point textureSize = EditorApplication.State.GetTexture(entity.TextureId).FrameSize;

			(float sin, float cos) = MathF.SinCos(rotation);

			worldPos.X += cos * (16f + textureSize.X / 2f * scale.X);
			worldPos.Y -= sin * (16f + textureSize.X / 2f * scale.X);

			Vector2 screenPos = Camera.WorldToScreen(worldPos);
			ImGui.SetCursorPosX(screenPos.X - 12);
			ImGui.SetCursorPosY(screenPos.Y - 12);

			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, 0);
			ImGui.PushItemFlag(ImGuiItemFlags.ButtonRepeat, true);
			ImGui.Button(IcoMoon.RotateIcon.ToString(), NVector2.One * 24);

			if (ImGui.IsItemActive())
			{
				Vector2 diff = Input.MouseWorld - entity.Position.CachedValue;
				float atan2 = MathF.Atan2(-diff.Y, diff.X);
				entity.Rotation.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, atan2);

				ImGui.SetTooltip("Rotacion actual:\n" + MathHelper.ToDegrees(atan2));
			}
			else
			{
				ImGui.SetItemTooltip("Manten para rotar");
			}

			ImGui.PopItemFlag();
			ImGui.PopStyleVar();

			return ImGui.IsItemHovered();
		}
	}
}