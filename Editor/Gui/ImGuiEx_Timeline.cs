using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Editor.Model;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace Editor.Gui
{
    public static partial class ImGuiEx
    {
        private const int MinimalLegendwidth = 196;
        private const int LineStartOffset = 8;

        private const int pixelsPerFrame = 10;
        private const int majorLinePerLines = 5;

        private static NVector2 timelineRegionMin;
        private static NVector2 timelineRegionMax;

        private static float timelineZoom = 1, timelineZoomTarget = 1;
        private static int currentLegendWidth = 0;

        public static int visibleStartingFrame = 0;
        public static int visibleEndingFrame = 0;

        private static float accumalatedPanningDeltaX = 0f;
        private static bool isPanningTimeline = false;

        private static (string id, string text)[] toolbarButtonDefinitions =
        {
            ("First (Home)", IcoMoon.FirstIcon.ToString()),
            ("Previous", IcoMoon.PreviousIcon.ToString()),
            ("Backward", IcoMoon.BackwardIcon.ToString()),
            ("Forward (Enter)", IcoMoon.ForwardIcon.ToString()),
            ("Next", IcoMoon.NextIcon.ToString()),
            ("Last (End)", IcoMoon.LastIcon.ToString()),
            ("Loop (L)", IcoMoon.LoopIcon.ToString())
        };
        private static (int trackId, int keyframe, float accumulation) interpolationValue = (-1, -1, 0);
        private static List<Keyframe> keyframesToMove = new List<Keyframe>();
        private static NVector2 selectKeyframePos;
        private static bool movingKeyframe;

        public static void DrawUiTimeline(Animator animator)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Current:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(48f);

            var ckf = animator.CurrentKeyframe;
            ImGui.DragInt("##1", ref ckf);
            animator.CurrentKeyframe = ckf;

            ImGui.SameLine();
            ImGui.Text("FPS:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(48f);

            var fps = animator.FPS;
            ImGui.DragInt("##4", ref fps);
            animator.FPS = fps;

            ImGui.SameLine();
            ImGui.Text("Zoom:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(48f);

            // var zoom = timelineZoomTarget;
            ImGui.DragFloat("##5", ref timelineZoomTarget, 0.1f, 0.1f, 5f);
            timelineZoomTarget = MathHelper.Clamp(timelineZoomTarget, 0.1f, 5f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                keyframesToMove.Clear();
                movingKeyframe = false;
            }
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && keyframesToMove.Any() && Vector2.DistanceSquared(ImGui.GetMousePos(), selectKeyframePos) > 16)
            {
                movingKeyframe = true;
            }

            ImGui.BeginChild(1, NVector2.Zero);
            {
                var style = ImGui.GetStyle();
                var toolbarSize = DrawToolbar(animator, OnToolbarPressed);

                currentLegendWidth = (int)(toolbarSize.X >= MinimalLegendwidth ? toolbarSize.X : MinimalLegendwidth);
                currentLegendWidth += (int)(style.ItemSpacing.Y + style.ItemSpacing.X * 2);

                ImGui.SameLine(currentLegendWidth);

                float oldTimelineZoomTarget = timelineZoomTarget; // im lazy
                DrawTimeline(animator, style.ItemSpacing.Y, ImGui.GetItemRectSize().Y);

                ImGui.BeginChild("##content", NVector2.Zero);
                {
                    // small hack to draw first keyframe correctly
                    ImGui.SetCursorPosY(ImGui.GetCursorPos().Y + 4);

                    ImGui.Columns(2, "##legend", false);
                    ImGui.SetColumnWidth(0, currentLegendWidth);
                    foreach (var group in animator)
                    {
                        if (!animator.GroupHasKeyframes(group))
                            continue;

                        bool open = ImGui.TreeNodeEx(group, ImGuiTreeNodeFlags.DefaultOpen);

                        ImGui.NextColumn();

                        // draw group keyframes
                        for (int i = visibleStartingFrame; i < visibleEndingFrame / timelineZoom; i++)
                        {
                            if (animator.GroupHasKeyframeAtFrame(group, i))
                            {
                                DrawKeyFrame(i, Color.LightGray, out NVector2 min, out NVector2 max);
                                if (ImGui.IsMouseHoveringRect(min, max))
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.Text($"Entity: {group}\nFrame: {i}");
                                    ImGui.EndTooltip();

                                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                    {
                                        foreach (var trackId in animator.EnumerateGroupTrackIds(group))
                                        {
                                            AnimationTrack track = animator.GetTrack(trackId);
                                            if (track.HasKeyframeAtFrame(i))
                                                keyframesToMove.Add(track[track.GetBestIndex(i)]);
                                        }
                                        selectKeyframePos = ImGui.GetMousePos();
                                    }

                                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                                    {
                                        GameApplication.State.Animator.CurrentKeyframe = i;
                                        GameApplication.State.Animator.Stop();
                                        GameApplication.Instance.selectedEntityId = group;
                                        GameApplication.Instance.selectedEntityId = group;
                                    }

                                    if (ImGui.IsKeyDown(ImGuiKey.Delete))
                                    {
                                        foreach (var trackId in animator.EnumerateGroupTrackIds(group))
                                        {
                                            animator.RemoveKeyframe(trackId, i);
                                        }
                                    }
                                }
                            }
                        }

                        ImGui.NextColumn();

                        if (open)
                        {
                            if (movingKeyframe && keyframesToMove != null)
                            {
                                var hoveringFrame = GetFrameForTimelinePos((ImGui.GetMousePos().X - timelineRegionMin.X) / timelineZoom);
                                foreach (var selectedKeyframe in keyframesToMove)
                                {
                                    selectedKeyframe.Frame = hoveringFrame;
                                }
                                ImGui.BeginTooltip();
                                if (keyframesToMove.Count > 1)
                                    ImGui.SetTooltip($"Moviendo {keyframesToMove.Count} keyframes\nFrame nuevo: {hoveringFrame}");
                                else
                                    ImGui.SetTooltip($"Frame nuevo: {hoveringFrame}");
                                ImGui.EndTooltip();

                            }
                            bool hovered = false;
                            foreach (var trackId in animator.EnumerateGroupTrackIds(group))
                            {
                                var track = animator.GetTrack(trackId);
                                if (!track.HasKeyframes())
                                    continue;

                                ImGui.Text(track.Id);
                                ImGui.NextColumn();

                                var vStartIndex = track.GetBestIndex(visibleStartingFrame);
                                var vEndIndex = track.GetBestIndex((Keyframe)((visibleEndingFrame + 1) / timelineZoom));
                                float mouseWheel = ImGui.GetIO().MouseWheel;

                                foreach (var frame in track.GetRange(vStartIndex, vEndIndex - vStartIndex))
                                {
                                    DrawKeyFrame(frame.Frame, Color.ForestGreen, out NVector2 min, out NVector2 max);
                                    if (!movingKeyframe && ImGui.IsMouseHoveringRect(min, max))
                                    {
                                        hovered = true;
                                        var index = track.GetBestIndex(frame.Frame);

                                        if (interpolationValue.trackId != trackId && interpolationValue.keyframe != frame.Frame)
                                        {
                                            interpolationValue.trackId = trackId;
                                            interpolationValue.keyframe = frame.Frame;
                                            interpolationValue.accumulation = 0;
                                        }
                                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                        {
                                            keyframesToMove.Add(frame);
                                            selectKeyframePos = ImGui.GetMousePos();
                                        }
                                        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                                        {
                                            GameApplication.State.Animator.CurrentKeyframe = frame.Frame;
                                            GameApplication.State.Animator.Stop();
                                            GameApplication.Instance.selectedEntityId = group;
                                            GameApplication.Instance.selectedEntityId = group;
                                        }
                                        if (ImGui.IsKeyDown(ImGuiKey.Delete))
                                        {
                                            track.RemoveAt(index);
                                        }
                                        if (mouseWheel != 0)
                                        {
                                            interpolationValue.accumulation += mouseWheel;
                                            if (interpolationValue.accumulation >= 1)
                                            {
                                                track[index].InterpolationType++;
                                                if ((byte)track[index].InterpolationType > Enum.GetValues<InterpolationType>().Length - 1)
                                                {
                                                    track[index].InterpolationType = 0;
                                                }
                                            }
                                            if (interpolationValue.accumulation <= -1)
                                            {
                                                track[index].InterpolationType--;
                                                if ((byte)track[index].InterpolationType == byte.MaxValue)
                                                {
                                                    track[index].InterpolationType = Enum.GetValues<InterpolationType>().Last();
                                                }

                                            }

                                        }
                                        ImGui.BeginTooltip();
                                        if (track.Id == GameApplication.ROTATION_PROPERTY)
                                            ImGui.Text($"{((float)frame.Value) * 180 / MathHelper.Pi} ({frame.InterpolationType})");
                                        else
                                            ImGui.Text($"{frame.Value} ({frame.InterpolationType})");
                                        ImGui.EndTooltip();
                                    }
                                }

                                ImGui.NextColumn();
                            }
                            if (hovered)
                            {
                                timelineZoomTarget = oldTimelineZoomTarget;
                            }
                            else
                            {
                                interpolationValue.trackId = -1;
                                interpolationValue.keyframe = -1;
                                interpolationValue.accumulation = 0;
                            }
                            ImGui.TreePop();
                        }
                    }

                    ImGui.EndChild();
                }

            }
            ImGui.EndChild();
        }

        private static void OnToolbarPressed(string @event, Animator animator)
        {
            switch (@event)
            {
                case "Forward (Enter)":
                    animator.PlayForward();
                    break;
                case "Backward":
                    animator.PlayBackward();
                    break;

                case "First (Home)":
                    animator.CurrentKeyframe = animator.GetFirstFrame();
                    if (animator.CurrentKeyframe <= visibleStartingFrame)
                        visibleStartingFrame = animator.CurrentKeyframe;
                    else if (animator.CurrentKeyframe >= visibleEndingFrame)
                        visibleStartingFrame += (animator.CurrentKeyframe - visibleEndingFrame) + 1;
                    break;
                case "Last (End)":
                    animator.CurrentKeyframe = animator.GetLastFrame();
                    if (animator.CurrentKeyframe <= visibleStartingFrame)
                        visibleStartingFrame = animator.CurrentKeyframe;
                    else if (animator.CurrentKeyframe >= visibleEndingFrame)
                        visibleStartingFrame += (animator.CurrentKeyframe - visibleEndingFrame) + 1;
                    break;

                case "Previous":
                    animator.CurrentKeyframe = animator.GetPreviousFrame();
                    break;

                case "Next":
                    animator.CurrentKeyframe = animator.GetNextFrame();
                    break;

                case "Loop (L)":
                    animator.ToggleLooping();
                    break;
            }
        }

        private static bool GetToggleButtonCondition(string id, Animator animator)
        {
            switch (id)
            {
                case "Backward":
                    return animator.PlayingBackward;
                case "Forward (Enter)":
                    return animator.PlayingForward;
                default:
                    return animator.Looping;
            }
        }

        private static NVector2 DrawToolbar(Animator animator, Action<string, Animator> callback)
        {
            ImGui.BeginGroup();
            {
                for (var index = 0; index < toolbarButtonDefinitions.Length; index++)
                {
                    var (id, text) = toolbarButtonDefinitions[index];
                    if (index > 0)
                        ImGui.SameLine();

                    if (id.Equals("Backward") || id.Equals("Forward (Enter)") || id.Equals("Loop (L)"))
                    {
                        var toggeld = GetToggleButtonCondition(id, animator);
                        if (ToggleButton(text, id, ref toggeld))
                            callback?.Invoke(id, animator);
                    }
                    else
                        DelegateButton(id, text, id, s => callback?.Invoke(s, animator));

                }
            }
            ImGui.EndGroup();

            return ImGui.GetItemRectSize();
        }

        private static void DrawTimeline(Animator animator, float headerYPadding, float headerHeight = 24f)
        {
            var drawList = ImGui.GetWindowDrawList();
            var style = ImGui.GetStyle();
            var contentRegion = ImGui.GetContentRegionAvail();
            var headerSize = NVector2.Zero;
            headerSize.X = contentRegion.X - style.ScrollbarSize;
            headerSize.Y = headerHeight + headerYPadding;

            visibleEndingFrame = GetFrameForTimelinePos(headerSize.X);

            // create rectangle for total timeline header area
            timelineRegionMin = ImGui.GetCursorScreenPos();
            timelineRegionMax = timelineRegionMin + headerSize;
            timelineRegionMax.Y = timelineRegionMin.Y + contentRegion.Y;
            timelineZoom = Math.Clamp(MathHelper.Lerp(timelineZoom, timelineZoomTarget, 0.1f), 0.1f, 5f);
            ImGui.PushClipRect(timelineRegionMin, timelineRegionMax, false);
            {
                ImGui.InvisibleButton("##header-region", headerSize);

                // set frame
                if (ImGui.IsItemHovered())
                {
                    var hoveringFrame = GetFrameForTimelinePos((ImGui.GetMousePos().X - timelineRegionMin.X) / timelineZoom);
                    ImGui.BeginTooltip();
                    ImGui.Text(hoveringFrame.ToString());
                    ImGui.EndTooltip();

                    if (ImGui.IsMouseDown(0))
                        animator.CurrentKeyframe = hoveringFrame;
                }

                // panning the timeline
                if (ImGui.IsMouseHoveringRect(timelineRegionMin, timelineRegionMax, false))
                {
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Right, 0))
                    {
                        accumalatedPanningDeltaX += ImGui.GetIO().MouseDelta.X;

                        // focus window if not panning before
                        if (!ImGui.IsWindowFocused())
                            ImGui.SetWindowFocus();

                        var framesToMove = (int)Math.Floor(accumalatedPanningDeltaX / pixelsPerFrame);
                        if (framesToMove != 0)
                        {
                            isPanningTimeline = true;
                            accumalatedPanningDeltaX -= framesToMove * pixelsPerFrame;
                            visibleStartingFrame -= framesToMove;
                        }
                    }
                    else
                    {
                        timelineZoomTarget += (int)ImGui.GetIO().MouseWheel * 0.3f;
                        timelineZoomTarget = Math.Clamp(timelineZoomTarget, 0, 5);
                        isPanningTimeline = false;
                        accumalatedPanningDeltaX = 0f;
                    }
                }


                // draw all timeline lines
                var frames = (visibleEndingFrame - visibleStartingFrame) / timelineZoom;
                for (int f = 0; f < frames; f++)
                {
                    var frame = f + visibleStartingFrame;
                    var lineStart = timelineRegionMin;
                    lineStart.X += LineStartOffset + f * pixelsPerFrame * timelineZoom;
                    var lineEnd = lineStart + NVector2.UnitY * headerSize.Y;

                    if (frame % majorLinePerLines == 0)
                    {
                        var numberString = frame.ToString();
                        var frameTextOffset = (float)Math.Floor(ImGui.CalcTextSize(numberString).X / 2);

                        drawList.AddText(lineStart - NVector2.UnitX * frameTextOffset,
                            Color.White.PackedValue, numberString);

                        lineEnd.Y += timelineRegionMax.Y - headerSize.Y;
                        lineStart.Y += headerSize.Y * 0.5f;
                        drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(ImGuiCol.Border));
                    }
                    else
                    {
                        lineStart.Y += headerSize.Y * 0.65f;
                        drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(ImGuiCol.Border));
                    }
                }

                // draw currentFrame line if within range
                if (animator.CurrentKeyframe >= visibleStartingFrame && animator.CurrentKeyframe <= visibleEndingFrame / timelineZoom)
                {
                    var frameLineStart = timelineRegionMin;
                    frameLineStart.X += GetTimelinePosForFrame(animator.CurrentKeyframe);
                    frameLineStart.Y += headerSize.Y * 0.5f;

                    var frameLineEnd = frameLineStart;
                    frameLineEnd.Y += timelineRegionMax.Y;

                    drawList.AddLine(frameLineStart, frameLineEnd, Color.Pink.PackedValue);

                    var radius = 5;
                    frameLineStart.Y += radius;
                    drawList.AddCircleFilled(frameLineStart, radius, Color.Pink.PackedValue);
                }
            }
            ImGui.PopClipRect();

            // draw separator
            var separatorY = timelineRegionMin.Y + headerSize.Y;
            drawList.AddLine(new NVector2(ImGui.GetWindowPos().X, separatorY),
                new NVector2(timelineRegionMin.X + contentRegion.X, separatorY),
                ImGui.GetColorU32(ImGuiCol.Border));

            if (isPanningTimeline)
            {
                // draw shadow for panning
                var start = timelineRegionMin - NVector2.UnitY * style.WindowPadding.Y;
                var size = new NVector2(LineStartOffset + 8, timelineRegionMin.Y + contentRegion.Y + style.ItemSpacing.Y * 2);

                drawList.AddRectFilledMultiColor(start, start + size,
                    0xFF000000, 0u, 0u, 0xFF000000);
            }
        }

        private static void DrawKeyFrame(int frame, Color color, out NVector2 min, out NVector2 max)
        {
            var cursorPos = ImGui.GetCursorScreenPos();
            const float keyframeSize = 7f;
            var halfKeyFrameWidth = (int)Math.Floor(keyframeSize / 2);

            // 12 seems to be the offset from start of timelime region, dont know why it happens with columns
            cursorPos.X += GetTimelinePosForFrame(frame) - (halfKeyFrameWidth + 12);
            cursorPos.Y -= 2;

            var size = new NVector2(keyframeSize, keyframeSize + 4);

            min = cursorPos;
            max = min + size;

            ImGui.GetWindowDrawList().AddRectFilled(cursorPos,
                cursorPos + size, color.PackedValue);
        }

        private static int GetFrameForTimelinePos(float x)
        {
            return (int)(Math.Floor((x - LineStartOffset / timelineZoom) / pixelsPerFrame + 0.5f) + visibleStartingFrame);
        }

        private static float GetTimelinePosForFrame(int frame)
        {
            return (frame - visibleStartingFrame) * pixelsPerFrame * timelineZoom + LineStartOffset;
        }
    }
}