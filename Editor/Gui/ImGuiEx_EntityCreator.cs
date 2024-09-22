using ImGuiNET;

using System;

namespace Editor.Gui
{
	public partial class ImGuiEx
	{
		private static int selectedTexture;
		public static string entityName = string.Empty;

		public static void DoEntityCreatorReset()
		{
			selectedTexture = 0;
			entityName = "Sprite" + EditorApplication.State.GraphicEntities.Count;
		}

		public static void DoEntityCreatorModal(string[] textureNames, Action<string, string> onCreatePressed)
		{
			bool open_create_sprite = true;
			NVector2 ch = ImGui.GetContentRegionAvail();
			float frameHeight = ch.Y - (ImGui.GetTextLineHeight() + ImGui.GetStyle().WindowPadding.Y * 1.5f);

			if (ImGui.BeginPopupModal("Create entity", ref open_create_sprite, ImGuiWindowFlags.NoResize))
			{
				ImGui.BeginChild("New entity data", NVector2.UnitX * 400 + NVector2.UnitY * frameHeight, ImGuiChildFlags.FrameStyle);
				{
					ImGui.InputText("Entity name", ref entityName, 64);

					if (ImGui.BeginListBox("Available\ntextures"))
					{
						for (int j = 0; j < textureNames.Length; j++)
						{
							bool selected = false;
							string textureName = textureNames[j];

							if (ImGui.Selectable(textureName, ref selected, ImGuiSelectableFlags.AllowDoubleClick))
							{
								selectedTexture = j;

								if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
								{
									onCreatePressed?.Invoke(entityName, textureName);

									ImGui.CloseCurrentPopup();

									break;
								}
							}
						}

						ImGui.EndListBox();
					}

					ImGui.EndChild();
				}

				if (ImGui.Button("Create entity##2"))
				{
					onCreatePressed?.Invoke(entityName, textureNames[selectedTexture]);

					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}
		}
	}
}