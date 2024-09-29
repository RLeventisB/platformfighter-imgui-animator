using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor.Gui
{
	using ToolbarButton = (string name, char icon, Predicate<Animator> state, ImGuiKey shortcut, Action<Animator> onPress);

	public static class Timeline
	{
		public const int MinimalLegendWidth = 196;
		public const int LineStartOffset = 8;

		public const int PixelsPerFrame = 10;
		public const int MajorLinePerLines = 5;
		public const int TimelineVerticalHeight = 250;

		public static NVector2 timelineRegionMin;
		public static NVector2 timelineRegionMax;

		public static DampedValue TimelineZoom = 1;
		public static int currentLegendWidth;

		public static int visibleStartingFrame;
		public static int visibleEndingFrame;

		public static float accumalatedPanningDeltaX;
		public static bool HitboxMode;

		private static readonly ToolbarButton[] toolbarButtonDefinitions =
		[
			("First keyframe", IcoMoon.FirstIcon, null, ImGuiKey.Home, v =>
			{
				v.CurrentKeyframe = v.GetFirstFrame();

				if (v.CurrentKeyframe <= visibleStartingFrame) // move timeline to see starting frame
					visibleStartingFrame = v.CurrentKeyframe;
				else if (v.CurrentKeyframe >= visibleEndingFrame)
					visibleStartingFrame += v.CurrentKeyframe - visibleEndingFrame + 1;
			}),
			("Previous keyframe", IcoMoon.PreviousIcon, null, ImGuiKey.ModShift | ImGuiKey.LeftArrow, v =>
			{
				v.CurrentKeyframe = v.GetPreviousFrame();
			}),
			("Previous frame", IcoMoon.PreviousArrowIcon, null, ImGuiKey.LeftArrow, v =>
			{
				int firstFrame = v.GetFirstFrame();
				int lastFrame = v.GetLastFrame();

				v.CurrentKeyframe--;

				if (v.Looping && v.HasKeyframes() && v.CurrentKeyframe < firstFrame)
					v.CurrentKeyframe = lastFrame;
			}),
			("Backward", IcoMoon.BackwardIcon, v => v.PlayingBackward, ImGuiKey.ModShift | ImGuiKey.Enter, v =>
			{
				v.PlayBackward();
			}),
			("Forward", IcoMoon.ForwardIcon, v => v.PlayingForward, ImGuiKey.Enter, v =>
			{
				v.PlayForward();
			}),
			("Next frame", IcoMoon.NextArrowIcon, null, ImGuiKey.RightArrow, v =>
			{
				int firstFrame = v.GetFirstFrame();
				int lastFrame = v.GetLastFrame();

				v.CurrentKeyframe++;

				if (v.Looping && v.HasKeyframes() && v.CurrentKeyframe >= lastFrame)
					v.CurrentKeyframe = firstFrame;
			}),
			("Next keyframe", IcoMoon.NextIcon, null, ImGuiKey.ModShift | ImGuiKey.RightArrow, v =>
			{
				v.CurrentKeyframe = v.GetNextFrame();
			}),
			("Last keyframe", IcoMoon.LastIcon, null, ImGuiKey.End, v =>
			{
				v.CurrentKeyframe = v.GetLastFrame();

				if (v.CurrentKeyframe <= visibleStartingFrame)
					visibleStartingFrame = v.CurrentKeyframe;
				else if (v.CurrentKeyframe >= visibleEndingFrame)
					visibleStartingFrame += v.CurrentKeyframe - visibleEndingFrame + 1; // does this need a +=????
			}),
			("Loop", IcoMoon.LoopIcon, v => v.Looping, ImGuiKey.L, v =>
			{
				v.ToggleLooping();
			})
		];
		private static (string trackId, int linkId, float accumulation) linkInterpolationData = (string.Empty, -1, 0);
		public static SelectedLinkData selectedLink;
		public static CreationLinkData newLinkCreationData;
		private static Keyframe keyframeToClone;
		private static bool cloneKeyframePopupOpen;

		public static void DrawUiTimeline(Animator animator)
		{
			ImGui.SetNextWindowPos(new NVector2(0, EditorApplication.Graphics.Viewport.Height - TimelineVerticalHeight), ImGuiCond.FirstUseEver);
			ImGui.SetNextWindowSize(new NVector2(EditorApplication.Graphics.Viewport.Width - Hierarchy.WindowWidth, TimelineVerticalHeight), ImGuiCond.FirstUseEver);

			ImGui.Begin("Timeline", SettingsManager.ToolsWindowFlags);
			{
				ImGui.AlignTextToFramePadding();
				ImGui.Text("Current:");
				ImGui.SameLine();
				ImGui.SetNextItemWidth(48f);

				int ckf = animator.CurrentKeyframe;
				ImGui.DragInt("##CurrentFrameValue", ref ckf);
				animator.CurrentKeyframe = ckf;

				ImGui.SameLine();
				ImGui.Text("FPS:");
				ImGui.SameLine();
				ImGui.SetNextItemWidth(48f);

				int fps = animator.FPS;
				ImGui.DragInt("##FpsValue", ref fps, 1, 1, 40000);
				animator.FPS = fps;

				ImGui.SameLine();
				ImGui.Text("Zoom:");
				ImGui.SameLine();
				ImGui.SetNextItemWidth(48f);

				float zoomTarget = TimelineZoom.Target;
				if (ImGui.DragFloat("##ZoomValue", ref zoomTarget, 0.1f, 0.1f, 5f, null, ImGuiSliderFlags.AlwaysClamp))
					TimelineZoom.Target = zoomTarget;

				ImGui.BeginChild("KeyframeViewer");
				{
					ImGuiStylePtr style = ImGui.GetStyle();
					NVector2 toolbarSize = DrawToolbarButtons(animator);

					currentLegendWidth = (int)(toolbarSize.X >= MinimalLegendWidth ? toolbarSize.X : MinimalLegendWidth);
					currentLegendWidth += (int)(style.ItemSpacing.Y + style.ItemSpacing.X * 2);

					ImGui.SameLine(currentLegendWidth);

					float oldTimelineZoomTarget = zoomTarget; // im lazy
					float headerHeightOrsmting = ImGui.GetItemRectSize().Y;
					float endingFrame = visibleStartingFrame + (visibleEndingFrame - visibleStartingFrame) / TimelineZoom;
					bool isSelectedEntityValid = EditorApplication.selectedData.GetValue(out IAnimationObject selectedEntity) && EditorApplication.selectedData.ObjectSelectionType is SelectionType.Graphic or SelectionType.Hitbox;

					DrawTimeline(animator, isSelectedEntityValid, style.ItemSpacing.Y, headerHeightOrsmting);

					ImGui.BeginChild("##content", NVector2.Zero);
					{
						if (isSelectedEntityValid)
						{
							// small hack to draw first keyframe correctly
							ImGui.SetCursorPosY(ImGui.GetCursorPos().Y + 4);

							ImGui.Columns(2, "##legend", false);
							ImGui.SetColumnWidth(0, currentLegendWidth);

							switch (HitboxMode)
							{
								case true when EditorApplication.selectedData.ObjectSelectionType == SelectionType.Hitbox:
									RenderSelectedHitboxData((HitboxAnimationObject)selectedEntity, animator, endingFrame, headerHeightOrsmting, oldTimelineZoomTarget);

									break;
								case false when EditorApplication.selectedData.ObjectSelectionType == SelectionType.Graphic:
									RenderSelectedEntityKeyframes((TextureAnimationObject)selectedEntity, animator, endingFrame, headerHeightOrsmting, oldTimelineZoomTarget);

									break;
							}
						}

						ImGui.EndChild();
					}

					ImGui.EndChild();
				}

				ImGui.EndChild();
			}
		}

		private static void RenderSelectedHitboxData(HitboxAnimationObject selectedAnimationObject, Animator animator, float endingFrame, float headerHeightOrsmting, float oldTimelineZoomTarget)
		{
			ImGui.Text("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
		}

		private static void RenderSelectedEntityKeyframes(TextureAnimationObject selectedAnimationObject, Animator animator, float endingFrame, float headerHeightOrsmting, float oldTimelineZoomTarget)
		{
			bool open = ImGui.TreeNodeEx(selectedAnimationObject.Name, ImGuiTreeNodeFlags.DefaultOpen);

			ImGui.NextColumn();

			// draw entity keyframes
			for (int frame = visibleStartingFrame; frame < endingFrame; frame++)
			{
				if (animator.EntityHasKeyframeAtFrame(selectedAnimationObject.Name, frame))
				{
					DrawMainKeyframe(frame);
				}
			}

			ImGui.NextColumn();

			if (open)
			{
				bool scrollingOnLink = false;
				bool clickedLeft = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

				foreach (KeyframeableValue value in selectedAnimationObject.EnumerateKeyframeableValues())
				{
					ImGui.Text(value.Name);
					ImGui.NextColumn();

					int vStartIndex = value.GetIndexOrNext(visibleStartingFrame);
					int vEndIndex = value.GetIndexOrNext((Keyframe)endingFrame);
					float mouseWheel = ImGui.GetIO().MouseWheel;
					string linkTooltip = string.Empty, keyframeTooltip = string.Empty;

					for (int linkIndex = 0; linkIndex < value.links.Count; linkIndex++)
					{
						KeyframeLink link = value.links[linkIndex];
						DrawLink(link, ImGui.GetCursorPosY(),
							selectedLink != null && selectedLink.link == link ? Color.SlateGray.PackedValue : Color.DarkGray.PackedValue,
							headerHeightOrsmting, out NVector2 min, out NVector2 max);

						if (ImGui.IsMouseHoveringRect(min, max))
						{
							scrollingOnLink = true;

							if (mouseWheel != 0)
							{
								if (linkInterpolationData.trackId != value.Name || linkInterpolationData.linkId != linkIndex)
								{
									linkInterpolationData.trackId = value.Name;
									linkInterpolationData.linkId = linkIndex;
									linkInterpolationData.accumulation = 0;
								}

								linkInterpolationData.accumulation += mouseWheel;

								while (Math.Abs(linkInterpolationData.accumulation) >= 1) // cursed but works
								{
									int difference = (int)(linkInterpolationData.accumulation > 0 ? Math.Floor(linkInterpolationData.accumulation) : Math.Ceiling(linkInterpolationData.accumulation));
									int newValue = (int)link.InterpolationType + difference;
									link.InterpolationType = (InterpolationType)Modulas(newValue, Enum.GetValues<InterpolationType>().Length);

									linkInterpolationData.accumulation -= difference;
								}
							}

							if (ImGui.IsKeyDown(ImGuiKey.Delete))
							{
								value.RemoveLink(link);
							}

							if (clickedLeft)
							{
								EditorApplication.SelectLink(link);
							}

							linkTooltip = $"Link {{{string.Join(", ", link.Keyframes.Select(v => v.ToString()))}}}\n({link.InterpolationType})";
						}
					}

					List<Keyframe> list = value.GetRange(vStartIndex, vEndIndex - vStartIndex);

					for (int i = 0; i < list.Count; i++)
					{
						Keyframe keyframe = list[i];
						Color color = Color.ForestGreen;

						if (newLinkCreationData != null && newLinkCreationData.Contains(value, keyframe))
							color = Color.LimeGreen;

						DrawKeyFrame(keyframe.Frame, color, out NVector2 min, out NVector2 max);

						if (ImGui.IsMouseHoveringRect(min, max))
						{
							int index = value.GetIndexOrNext(keyframe.Frame);

							if (clickedLeft)
							{
								animator.CurrentKeyframe = keyframe.Frame;
								animator.Stop();
								if (keyframe.ContainingLink is not null)
									EditorApplication.SelectLink(keyframe.ContainingLink);

								EditorApplication.SetDragAction(new MoveKeyframeDelegateAction([(value, index)]));
							}

							if (clickedLeft && ImGui.GetIO().KeyCtrl)
							{
								if (newLinkCreationData == null)
									newLinkCreationData = new CreationLinkData(value, i, 0);
							}

							if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
							{
								ResetSavedInput(keyframe.Frame.ToString());
								cloneKeyframePopupOpen = true;
								keyframeToClone = keyframe;
							}

							if (ImGui.IsKeyDown(ImGuiKey.Delete))
							{
								value.RemoveAt(index);
							}

							if (value.Name == RotationProperty)
								keyframeTooltip = $"{(float)keyframe.Value * 180 / MathHelper.Pi}";
							else
								keyframeTooltip = $"{keyframe.Value}";
						}
					}

					bool validLink = !string.IsNullOrEmpty(linkTooltip);
					bool validKeyframe = !string.IsNullOrEmpty(keyframeTooltip);

					if (validLink || validKeyframe)
					{
						ImGui.BeginTooltip();

						if (validLink)
						{
							ImGui.Text(linkTooltip);
						}

						if (validKeyframe)
						{
							ImGui.Text(keyframeTooltip);
						}

						ImGui.EndTooltip();
					}

					ImGui.NextColumn();
				}

				if (scrollingOnLink)
				{
					TimelineZoom.Target = oldTimelineZoomTarget;
				}
				else
				{
					linkInterpolationData.trackId = string.Empty;
					linkInterpolationData.linkId = -1;
					linkInterpolationData.accumulation = 0;
				}

				ImGui.TreePop();
			}

			if (cloneKeyframePopupOpen)
			{
				ImGui.OpenPopup("Clonar keyframe a", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.AnyPopup | ImGuiPopupFlags.NoReopen);
			}

			if (ImGui.BeginPopupModal("Clonar keyframe a", ref cloneKeyframePopupOpen, ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
			{
				if (keyframeToClone is not null)
				{
					string input = SavedInput("Frame de destino:", string.Empty, out bool changed);

					if (changed)
						ResetSavedInput(string.Concat(input.Where(char.IsNumber)));

					ImGui.SetNextItemShortcut(ImGuiKey.Enter);

					bool validFrame = int.TryParse(input, out int finalFrame);

					ImGui.BeginDisabled(!validFrame);

					if (ImGui.Button("Clonar") && validFrame)
					{
						cloneKeyframePopupOpen = false;
						keyframeToClone.ContainingValue?.SetKeyframeValue(finalFrame, keyframeToClone.Value);
						keyframeToClone = null;

						ImGui.CloseCurrentPopup();
					}

					ImGui.EndDisabled();

					ImGui.SetNextItemShortcut(ImGuiKey.Escape);
					ImGui.SameLine();

					if (ImGui.Button("Cancelar"))
					{
						keyframeToClone = null;
						cloneKeyframePopupOpen = false;
						ImGui.CloseCurrentPopup();
					}
				}

				ImGui.EndPopup();
			}

			void DrawMainKeyframe(int frame)
			{
				DrawKeyFrame(frame, Color.LightGray, out NVector2 min, out NVector2 max);

				if (ImGui.IsMouseHoveringRect(min, max))
				{
					ImGui.BeginTooltip();
					ImGui.Text($"Frame: {frame}");
					ImGui.EndTooltip();

					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
					{
						animator.CurrentKeyframe = frame;
						animator.Stop();

						List<(KeyframeableValue, int)> keyframesToMove = new List<(KeyframeableValue, int)>();

						foreach (KeyframeableValue value in selectedAnimationObject.EnumerateKeyframeableValues())
						{
							Keyframe keyframe = value.GetKeyframe(frame);

							if (keyframe != null)
								keyframesToMove.Add((value, value.IndexOfKeyframe(keyframe)));
						}

						EditorApplication.SetDragAction(new MoveKeyframeDelegateAction(keyframesToMove.ToArray()));
					}

					if (ImGui.IsKeyDown(ImGuiKey.Delete))
					{
						foreach (KeyframeableValue value in selectedAnimationObject.EnumerateKeyframeableValues())
						{
							value.RemoveKeyframe(frame);
						}
					}
				}
			}
		}

		private static NVector2 DrawToolbarButtons(Animator animator)
		{
			ImGui.BeginGroup();
			ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, NVector2.One / 2);
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, 0);

			foreach ((string name, char icon, Predicate<Animator> state, ImGuiKey shortcut, Action<Animator> onPress) in toolbarButtonDefinitions)
			{
				ToolbarButton(animator, name, icon, state, shortcut, onPress);

				ImGui.SameLine();
			}

			ImGui.PopStyleVar(2);
			ImGui.EndGroup();

			return ImGui.GetItemRectSize();
		}

		public static void ToolbarButton(Animator animator, string name, char icon, Predicate<Animator> state, ImGuiKey shortcut, Action<Animator> onPress)
		{
			bool isActive = state != null && state.Invoke(animator);

			if (isActive) // yes i know checkboxes are made for this but i want to make things harder for myself :>( 
				ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));

			if (!ImGui.GetIO().WantCaptureKeyboard && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
				ImGui.SetNextItemShortcut(shortcut, ImGuiInputFlags.Repeat);

			if (ImGui.Button(icon.ToString(), new NVector2(24, 22)))
				onPress?.Invoke(animator);

			if (ImGui.BeginItemTooltip())
			{
				ImGui.Text(name);
				ImGui.Text("Shortcut: " + GetKeyChordName(shortcut));
				ImGui.EndTooltip();
			}

			if (isActive)
				ImGui.PopStyleColor();
		}

		private static void DrawTimeline(Animator animator, bool isSelectedEntityValid, float headerYPadding, float headerHeight = 24f)
		{
			bool isPanningTimeline = false;
			ImDrawListPtr drawList = ImGui.GetWindowDrawList();
			ImGuiStylePtr style = ImGui.GetStyle();
			NVector2 contentRegion = ImGui.GetContentRegionAvail();
			NVector2 headerSize = NVector2.Zero;
			headerSize.X = contentRegion.X - style.ScrollbarSize;
			headerSize.Y = headerHeight + headerYPadding;

			visibleEndingFrame = GetFrameForTimelinePos(headerSize.X);

			// create rectangle for total timeline header area
			timelineRegionMin = ImGui.GetCursorScreenPos();
			timelineRegionMax = timelineRegionMin + headerSize;
			timelineRegionMax.Y = timelineRegionMin.Y + contentRegion.Y;
			ImGui.PushClipRect(timelineRegionMin, timelineRegionMax, false);
			{
				ImGui.InvisibleButton("##header-region", headerSize);

				// set frame
				bool hovered = ImGui.IsItemHovered();

				if (hovered)
				{
					int hoveringFrame = GetFrameForTimelinePos((ImGui.GetMousePos().X - timelineRegionMin.X) / TimelineZoom);
					ImGui.BeginTooltip();
					ImGui.Text(hoveringFrame.ToString());
					ImGui.EndTooltip();

					if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
					{
						animator.CurrentKeyframe = hoveringFrame;

						if (SettingsManager.PlayOnKeyframeSelect)
							animator.PlayForward();
						else
							animator.Stop();
					}
				}

				// panning the timeline
				if (ImGui.IsMouseHoveringRect(timelineRegionMin, timelineRegionMax, false))
				{
					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !hovered)
						selectedLink = null;

					if (ImGui.IsMouseDragging(ImGuiMouseButton.Right, 0))
					{
						accumalatedPanningDeltaX += Input.MousePosDelta.X;

						// focus window if not panning before
						if (!ImGui.IsWindowFocused())
							ImGui.SetWindowFocus();

						int framesToMove = (int)Math.Floor(accumalatedPanningDeltaX / PixelsPerFrame);

						if (framesToMove != 0)
						{
							isPanningTimeline = true;
							accumalatedPanningDeltaX -= framesToMove * PixelsPerFrame;
							visibleStartingFrame -= framesToMove;
						}
					}
					else
					{
						TimelineZoom.Target += (int)ImGui.GetIO().MouseWheel * 0.3f;
						TimelineZoom.Target = Math.Clamp(TimelineZoom.Target, 0.1f, 5);
						accumalatedPanningDeltaX = 0f;
					}
				}

				// draw all timeline lines
				float frames = (visibleEndingFrame - visibleStartingFrame) / TimelineZoom;

				for (int f = 0; f < frames; f++)
				{
					int frame = f + visibleStartingFrame;
					NVector2 lineStart = timelineRegionMin;
					lineStart.X += LineStartOffset + f * PixelsPerFrame * TimelineZoom;
					NVector2 lineEnd = lineStart + NVector2.UnitY * headerSize.Y;

					bool isMajorLine = frame % MajorLinePerLines == 0;

					if (isMajorLine)
					{
						string numberString = frame.ToString();
						float frameTextOffset = (float)Math.Floor(ImGui.CalcTextSize(numberString).X / 2);

						drawList.AddText(lineStart - NVector2.UnitX * frameTextOffset,
							Color.White.PackedValue, numberString);

						if (isSelectedEntityValid)
						{
							lineEnd.Y += timelineRegionMax.Y - headerSize.Y;
							lineStart.Y += headerSize.Y * 0.5f;
							drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(ImGuiCol.Border));
						}
					}
					else
					{
						lineStart.Y += headerSize.Y * 0.65f;
						drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(ImGuiCol.Border));
					}
				}

				// draw currentFrame line if within range
				if (animator.CurrentKeyframe >= visibleStartingFrame && animator.CurrentKeyframe <= visibleEndingFrame / TimelineZoom)
				{
					NVector2 frameLineStart = timelineRegionMin;
					frameLineStart.X += GetTimelinePosForFrame(animator.CurrentKeyframe);
					frameLineStart.Y += headerSize.Y * 0.5f;

					NVector2 frameLineEnd = frameLineStart;
					frameLineEnd.Y += (timelineRegionMax.Y - timelineRegionMin.Y) * (isSelectedEntityValid ? 1 : 0.4f);

					drawList.AddLine(frameLineStart, frameLineEnd, Color.Pink.PackedValue);

					const int radius = 5;
					frameLineStart.Y += radius;
					drawList.AddCircleFilled(frameLineStart, radius, Color.Pink.PackedValue);
				}

				if (!isSelectedEntityValid)
				{
					drawList.AddText(timelineRegionMin + NVector2.UnitY * headerSize.Y * 3,
						Color.White.PackedValue, "Selecciona una entidad para mostrar los fotogramas clave >:(");
				}

				if (newLinkCreationData != null)
				{
					DrawLink(newLinkCreationData.value.keyframes[newLinkCreationData.LowerBorder].Frame,
						newLinkCreationData.value.keyframes[newLinkCreationData.UpperBorder].Frame,
						3 + 18 * (newLinkCreationData.value.Owner.EnumerateKeyframeableValues().IndexOf(newLinkCreationData.value) + 1),
						0x66666666, headerHeight, out _, out _);

					ImGui.BeginTooltip();
					List<Keyframe> keyframes = newLinkCreationData.value.keyframes;
					ImGui.Text($"Creando enlace desde frame {keyframes[newLinkCreationData.LowerBorder].Frame} hasta {keyframes[newLinkCreationData.UpperBorder].Frame}\nEn track {newLinkCreationData.value.Owner.Name}/{newLinkCreationData.value.Name}");

					if (newLinkCreationData.Length > 1)
						ImGui.Text("Apreta enter para crear!!!");

					ImGui.EndTooltip();
				}
			}

			ImGui.PopClipRect();

			// draw separator
			float separatorY = timelineRegionMin.Y + headerSize.Y;

			drawList.AddLine(new NVector2(ImGui.GetWindowPos().X, separatorY),
				new NVector2(timelineRegionMin.X + contentRegion.X, separatorY),
				ImGui.GetColorU32(ImGuiCol.Border));

			if (isPanningTimeline)
			{
				// draw shadow for panning
				NVector2 start = timelineRegionMin - NVector2.UnitY * style.WindowPadding.Y;
				NVector2 size = new NVector2(LineStartOffset + 8, timelineRegionMin.Y + contentRegion.Y + style.ItemSpacing.Y * 2);

				drawList.AddRectFilledMultiColor(start, start + size,
					0xFF000000, 0u, 0u, 0xFF000000);
			}
		}

		private static void DrawKeyFrame(int frame, Color color, out NVector2 min, out NVector2 max)
		{
			NVector2 cursorPos = ImGui.GetCursorScreenPos();
			const float keyframeSize = 7f;
			float halfKeyFrameWidth = keyframeSize / 2;

			// 12 seems to be the offset from start of timelime region, dont know why it happens with columns
			cursorPos.X += GetTimelinePosForFrame(frame) - keyframeSize - halfKeyFrameWidth;
			cursorPos.Y -= 2;

			NVector2 size = new NVector2(keyframeSize, keyframeSize + 4);

			ImGui.GetWindowDrawList().AddRectFilled(min = cursorPos, max = cursorPos + size, color.PackedValue);
		}

		private static void DrawLink(KeyframeLink link, float menuY, uint color, float headerHeight, out NVector2 min, out NVector2 max)
		{
			DrawLink(link.FirstKeyframe.Frame, link.LastKeyframe.Frame, menuY, color, headerHeight, out min, out max);
		}

		private static void DrawLink(int minFrame, int maxFrame, float menuY, uint color, float headerHeight, out NVector2 min, out NVector2 max)
		{
			NVector2 position = new NVector2(timelineRegionMin.X, timelineRegionMin.Y + menuY + headerHeight + 9);

			float minX = GetTimelinePosForFrame(minFrame), maxX = GetTimelinePosForFrame(maxFrame);
			position.X += minX;

			NVector2 size = new NVector2(maxX - minX, 5f);

			ImGui.GetWindowDrawList().AddRectFilled(min = position, max = position + size, color);
		}

		public static int GetFrameForTimelinePos(float x) => (int)(Math.Floor((x - LineStartOffset / TimelineZoom) / PixelsPerFrame + 0.5f) + visibleStartingFrame);

		public static float GetTimelinePosForFrame(int frame) => (frame - visibleStartingFrame) * PixelsPerFrame * TimelineZoom + LineStartOffset;
	}

	public class CreationLinkData
	{
		public readonly int start;
		public readonly KeyframeableValue value;
		public int offset;

		public CreationLinkData(KeyframeableValue value, int start, int offset)
		{
			this.value = value;
			this.start = start;
			this.offset = offset;
		}

		public int Border => start + offset;
		public int LowerBorder => Math.Min(start, Border);
		public int UpperBorder => Math.Max(start, Border);
		public int Length => UpperBorder - LowerBorder + 1;

		public void CreateLink()
		{
			value.AddLink(new KeyframeLink(value, value.keyframes.GetRange(LowerBorder, Length)));
		}

		public bool Contains(KeyframeableValue value, Keyframe keyframe) => this.value == value && keyframe.Frame >= LowerBorder && keyframe.Frame <= UpperBorder;
	}
}