using System;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace Editor.Gui
{
    public static partial class ImGuiEx
    {
        public static T Log<T>(this T obj)
        {
            Console.WriteLine(obj);
            return obj;
        }
        public static void Write(this BinaryWriter writer, NVector2 vector2)
        {
            writer.Write(vector2.X);
            writer.Write(vector2.Y);
        }
        public static void Write(this BinaryWriter writer, Vector2 vector2)
        {
            writer.Write(vector2.X);
            writer.Write(vector2.Y);
        }
        public static NVector2 ReadNVector2(this BinaryReader reader)
        {
            return new NVector2(reader.ReadSingle(), reader.ReadSingle());
        }
        public static Vector2 ReadVector2(this BinaryReader reader)
        {
            return new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        private static string savingInputString;

        public static class IcoMoon
        {
            public const char RotateIcon = '\ue984';

            public const char BackwardIcon = '\uea1f';
            public const char ForwardIcon = '\uea20';

            public const char PreviousIcon = '\uea23';
            public const char NextIcon = '\uea24';

            public const char FirstIcon = '\uea21';
            public const char LastIcon = '\uea22';

            public const char LoopIcon = '\uea2d';

            public const char TextureIcon = '\ueacd';
            public const char ImageIcon = '\ue90d';
            public const char ImagesIcon = '\ue90e';

            public const char BranchesIcon = '\ue9bc';
            public const char ListIcon = '\ue9ba';
            public const char EqualizerIcon = '\ue992';
            public const char SettingsIcon = '\ue994';
            public const char HammerIcon = '\ue996';
            public const char KeyIcon = '\ue98d';

            public const char FloppyDiskIcon = '\ue962';
            public const char FolderOpenIcon = '\ue930';

            public const char PlusIcon = '\uea0a';
            public const char MinusIcon = '\uea0b';
            public static ImFontPtr font;

            public static unsafe void AddIconsToDefaultFont(float fontSize)
            {
                const string fontFilePath = "IcoMoon-Free.ttf";
                ushort* rangesIntPtr = (ushort*)NativeMemory.AllocZeroed(new nuint(4));
                rangesIntPtr[0] = '\ue900';
                rangesIntPtr[1] = '\ueaea';

                ImFontConfig* config = ImGuiNative.ImFontConfig_ImFontConfig();
                config->MergeMode = 1;
                config->OversampleH = 3;
                config->OversampleV = 3;
                config->GlyphOffset = NVector2.UnitY * 2;
                config->FontDataOwnedByAtlas = 1;
                config->PixelSnapH = 1;
                config->GlyphMaxAdvanceX = float.MaxValue;
                config->RasterizerMultiply = 1.0f;
                font = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, fontSize, config, (nint)rangesIntPtr);
                // NativeMemory.Free(rangesIntPtr);
            }
        }

        public static string SavedInput(string id, string defaultInput)
        {
            if (string.IsNullOrEmpty(savingInputString))
                savingInputString = defaultInput;

            ImGui.InputText(id, ref savingInputString, 64);

            return savingInputString;
        }

        public static void ResetSavedInput()
        {
            savingInputString = string.Empty;
        }

        public static bool ToggleButton(string id, string descr, ref bool toggled)
        {
            uint backgroundColor = toggled ? ImGui.GetColorU32(ImGuiCol.ButtonActive)
                : ImGui.GetColorU32(ImGuiCol.Button);

            ImGui.PushStyleColor(ImGuiCol.Button, backgroundColor);
            var pressed = ImGui.Button(id);
            if (pressed)
                toggled = !toggled;

            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(descr) && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(descr);
                ImGui.EndTooltip();
            }

            return pressed;
        }

        public static bool RoundedToggleButton(string id, ref bool toggled)
        {
            ImGui.Text(id);
            ImGui.SameLine();

            var screenCursor = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var pressed = false;

            var height = ImGui.GetFrameHeight();
            var width = height * 1.55f;
            var radius = height * 0.5f;

            if (ImGui.InvisibleButton(id, new NVector2(width, height)))
            {
                toggled = !toggled;
                pressed = true;
            }

            uint backgroundColor;
            if (ImGui.IsItemHovered())
            {
                backgroundColor = ImGui.GetColorU32(ImGuiCol.ChildBg);
            }
            else
            {
                backgroundColor = toggled
                    ? ImGui.GetColorU32(ImGuiCol.ButtonHovered)
                    : ImGui.GetColorU32(ImGuiCol.Button);
            }

            drawList.AddRectFilled(screenCursor, new NVector2(screenCursor.X + width, screenCursor.Y + height),
                backgroundColor, radius);

            var centre = toggled ? new NVector2(screenCursor.X + width - radius, screenCursor.Y + radius)
                : new NVector2(screenCursor.X + radius, screenCursor.Y + radius);
            drawList.AddCircleFilled(centre, radius - 1.5f, Color.White.PackedValue);

            return pressed;
        }

        public static bool DelegateButton(string id, string text, string descr = null, Action<string> callback = null)
        {
            var buttonPressed = ImGui.Button(text);
            if (!string.IsNullOrEmpty(descr) && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(descr);
                ImGui.EndTooltip();
            }

            if (buttonPressed)
                callback?.Invoke(id);

            return buttonPressed;
        }

        public static void DisabledButton(string id)
        {
            NVector2 pos = ImGui.GetCursorScreenPos();
            var style = ImGui.GetStyle();
            var textSize = ImGui.CalcTextSize(id);
            textSize.X += style.FramePadding.X * 2;
            // textSize.Y += style.FramePadding.Y * 2;

            ImGui.GetWindowDrawList()
                .AddRectFilled(pos, pos + textSize, ImGui.GetColorU32(ImGuiCol.FrameBg));

            ImGui.SetCursorScreenPos(pos + NVector2.UnitX * style.FramePadding.X);
            ImGui.TextDisabled(id);
        }
    }
}