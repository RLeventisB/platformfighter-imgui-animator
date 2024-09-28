using ImGuiNET;

using System;
using System.Linq;

namespace Editor.Gui
{
	public partial class ImGuiEx
	{
		public static int selectedTextureOnEntityCreator;
		public static string entityName = string.Empty;

		public static void DoEntityCreatorReset()
		{
			selectedTextureOnEntityCreator = 0;
			entityName = "Sprite" + EditorApplication.State.GraphicEntities.Count;
		}

		public static void DoEntityCreatorModal(Action<string, string> onCreatePressed)
		{
			bool open_create_sprite = true;
			NVector2 ch = ImGui.GetContentRegionAvail();
			float frameHeight = ch.Y - (ImGui.GetTextLineHeight() + ImGui.GetStyle().WindowPadding.Y * 1.5f);
			string[] textureNames = EditorApplication.State.Textures.Keys.ToArray();

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

							if (ImGui.Selectable(textureName + "##select", ref selected, ImGuiSelectableFlags.AllowDoubleClick))
							{
								selectedTextureOnEntityCreator = j;

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

				if (ImGui.Button("Create entity##button creator"))
				{
					onCreatePressed?.Invoke(entityName, textureNames[selectedTextureOnEntityCreator]);

					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}
		}
	}
}