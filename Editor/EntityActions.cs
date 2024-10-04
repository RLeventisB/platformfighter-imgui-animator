using Editor.Graphics;
using Editor.Objects;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System;

namespace Editor
{
	public static class EntityActions
	{
		public static bool DoGraphicEntityActions(SelectionData data)
		{
			if (data.Type != SelectionType.Graphic || !data.IsLone())
				return false;

			TextureAnimationObject entity = (TextureAnimationObject)data.GetLoneObject();
			Vector2 arrowMovement = Vector2.Zero;

			if (ImGui.IsWindowFocused())
			{
				if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow, true))
				{
					arrowMovement.X += -0.1f;
				}

				if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true))
				{
					arrowMovement.Y += -0.1f;
				}

				if (ImGui.IsKeyPressed(ImGuiKey.RightArrow, true))
				{
					arrowMovement.X += 0.1f;
				}

				if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true))
				{
					arrowMovement.Y += 0.1f;
				}
			}

			if (arrowMovement != Vector2.Zero)
				entity.Position.SetKeyframeValue(null, entity.Position.CachedValue + arrowMovement);

			Vector2 worldPos = entity.Position.CachedValue;

			float rotation = entity.Rotation.CachedValue;
			Vector2 scale = entity.Scale.CachedValue;
			scale.X = MathF.Abs(scale.X);
			scale.Y = MathF.Abs(scale.Y);
			Point textureSize = EditorApplication.State.GetTexture(entity.TextureName).FrameSize;

			(float sin, float cos) = MathF.SinCos(rotation);

			worldPos.X += cos * (16f + textureSize.X / 2f * scale.X);
			worldPos.Y += sin * (16f + textureSize.X / 2f * scale.X);

			Vector2 screenPos = Camera.WorldToScreen(worldPos);
			ImGui.SetCursorPosX(screenPos.X - 12);
			ImGui.SetCursorPosY(screenPos.Y - 12);

			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, 0);
			ImGui.PushItemFlag(ImGuiItemFlags.ButtonRepeat, true);

			ImGui.Button(IcoMoon.RotateIcon.ToString(), NVector2.One * 24);

			if (ImGui.IsItemActive())
			{
				Vector2 diff = Input.MouseWorld - entity.Position.CachedValue;
				float atan2 = MathF.Atan2(diff.Y, diff.X);
				entity.Rotation.SetKeyframeValue(null, atan2);

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