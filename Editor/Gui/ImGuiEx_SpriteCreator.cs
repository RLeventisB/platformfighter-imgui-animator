using System;
using ImGuiNET;

namespace Editor.Gui
{
    public partial class ImGuiEx
    {
        private static int selectedTexture = 0;
        private static string spriteName = string.Empty;

        public static void DoEntityCreatorReset()
        {
            selectedTexture = 0;
            spriteName = "Sprite" + new Random().Next();
        }

        public static void DoEntityCreatorModal(string[] textureNames, Action<string, string> onCreatePressed)
        {
            var open_create_sprite = true;
            var ch = ImGui.GetContentRegionAvail();
            var frameHeight = ch.Y - (ImGui.GetTextLineHeight() + ImGui.GetStyle().WindowPadding.Y * 1.5f);
            if (ImGui.BeginPopupModal("Create entity", ref open_create_sprite, ImGuiWindowFlags.NoResize))
            {
                ImGui.BeginChild(1337, NVector2.UnitX * 400 + NVector2.UnitY * frameHeight, ImGuiChildFlags.FrameStyle);
                ImGui.InputText("Entity name", ref spriteName, 64);

                if (ImGui.BeginListBox("listbox 1"))
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
                                onCreatePressed?.Invoke(spriteName, textureName);

                                ImGui.CloseCurrentPopup();
                                break;
                            }
                        }
                    }
                    ImGui.EndListBox();
                }

                ImGui.EndChild();

                if (ImGui.Button("Create entity##2"))
                {
                    onCreatePressed?.Invoke(spriteName, textureNames[selectedTexture]);

                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }
    }
}