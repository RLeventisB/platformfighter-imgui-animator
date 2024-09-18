using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor.Gui
{
	public static class Timeline
	{
		public const int MinimalLegendwidth = 196;
		public const int LineStartOffset = 8;

		public const int PixelsPerFrame = 10;
		public const int MajorLinePerLines = 5;

		public static NVector2 timelineRegionMin;
		public static NVector2 timelineRegionMax;

		public static float timelineZoom = 1, timelineZoomTarget = 1;
		public static int currentLegendWidth;

		public static int visibleStartingFrame;
		public static int visibleEndingFrame;

		public static float accumalatedPanningDeltaX;
		public static bool isPanningTimeline;

		private static readonly (string id, string text)[] toolbarButtonDefinitions =
		{
			("First (Home)", IcoMoon.FirstIcon.ToString()),
			("Previous", IcoMoon.PreviousIcon.ToString()),
			("Backward", IcoMoon.BackwardIcon.ToString()),
			("Forward (Enter)", IcoMoon.ForwardIcon.ToString()),
			("Next", IcoMoon.NextIcon.ToString()),
			("Last (End)", IcoMoon.LastIcon.ToString()),
			("Loop (L)", IcoMoon.LoopIcon.ToString())
		};
		private static (string trackId, int linkId, float accumulation) interpolationValue = (string.Empty, -1, 0);
		public static SelectedLinkData selectedLink;
		public static CreationLinkData newLinkCreationData;

		public static void DrawUiTimeline(Animator animator)
		{
			ImGui.AlignTextToFramePadding();
			ImGui.Text("Current:");
			ImGui.SameLine();
			ImGui.SetNextItemWidth(48f);

			int ckf = animator.CurrentKeyframe;
			ImGui.DragInt("##1", ref ckf);
			animator.CurrentKeyframe = ckf;

			ImGui.SameLine();
			ImGui.Text("FPS:");
			ImGui.SameLine();
			ImGui.SetNextItemWidth(48f);

			int fps = animator.FPS;
			ImGui.DragInt("##4", ref fps, 1, 1, 40000);
			animator.FPS = fps;

			ImGui.SameLine();
			ImGui.Text("Zoom:");
			ImGui.SameLine();
			ImGui.SetNextItemWidth(48f);

			// var zoom = timelineZoomTarget;
			ImGui.DragFloat("##5", ref timelineZoomTarget, 0.1f, 0.1f, 5f);
			timelineZoomTarget = MathHelper.Clamp(timelineZoomTarget, 0.1f, 5f);

			ImGui.BeginChild(1, NVector2.Zero);
			{
				ImGuiStylePtr style = ImGui.GetStyle();
				NVector2 toolbarSize = DrawToolbar(animator, OnToolbarPressed);

				currentLegendWidth = (int)(toolbarSize.X >= MinimalLegendwidth ? toolbarSize.X : MinimalLegendwidth);
				currentLegendWidth += (int)(style.ItemSpacing.Y + style.ItemSpacing.X * 2);

				ImGui.SameLine(currentLegendWidth);

				float oldTimelineZoomTarget = timelineZoomTarget; // im lazy
				float headerHeightOrsmting = ImGui.GetItemRectSize().Y;
				float endingFrame = visibleStartingFrame + (visibleEndingFrame - visibleStartingFrame) / timelineZoom;
				bool isSelectedEntityValid = animator.RegisteredGraphics.TryGetValue(EditorApplication.Instance.selectedEntityId, out TextureEntity selectedEntity);

				DrawTimeline(animator, isSelectedEntityValid, style.ItemSpacing.Y, headerHeightOrsmting);

				ImGui.BeginChild("##content", NVector2.Zero);

				if (isSelectedEntityValid)
				{
					// small hack to draw first keyframe correctly
					ImGui.SetCursorPosY(ImGui.GetCursorPos().Y + 4);

					ImGui.Columns(2, "##legend", false);
					ImGui.SetColumnWidth(0, currentLegendWidth);

					RenderSelectedEntityKeyframes(selectedEntity, animator, endingFrame, headerHeightOrsmting, oldTimelineZoomTarget);

					ImGui.EndChild();
				}
			}

			ImGui.EndChild();
		}

		private static void RenderSelectedEntityKeyframes(TextureEntity selectedEntity, Animator animator, float endingFrame, float headerHeightOrsmting, float oldTimelineZoomTarget)
		{
			bool open = ImGui.TreeNodeEx(selectedEntity.Name, ImGuiTreeNodeFlags.DefaultOpen);

			ImGui.NextColumn();

			// draw entity keyframes
			for (int frame = visibleStartingFrame; frame < endingFrame; frame++)
			{
				if (animator.RegisteredGraphics.EntityHasKeyframeAtFrame(selectedEntity.Name, frame))
				{
					DrawMainKeyframe(frame);
				}
			}

			ImGui.NextColumn();

			if (open)
			{
				bool scrollingOnLink = false;
				bool clickedLeft = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

				foreach (KeyframeableValue value in selectedEntity.EnumerateKeyframeableValues())
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
								if (interpolationValue.trackId != value.Name || interpolationValue.linkId != linkIndex)
								{
									interpolationValue.trackId = value.Name;
									interpolationValue.linkId = linkIndex;
									interpolationValue.accumulation = 0;
								}

								interpolationValue.accumulation += mouseWheel;

								if (interpolationValue.accumulation >= 1)
								{
									link.InterpolationType++;

									if ((byte)link.InterpolationType > Enum.GetValues<InterpolationType>().Length - 1)
									{
										link.InterpolationType = 0;
									}
								}

								if (interpolationValue.accumulation <= -1)
								{
									link.InterpolationType--;

									if ((byte)link.InterpolationType == byte.MaxValue)
									{
										link.InterpolationType = Enum.GetValues<InterpolationType>().Last();
									}
								}
							}

							if (ImGui.IsKeyDown(ImGuiKey.Delete))
							{
								value.RemoveLink(link);
							}

							if (clickedLeft)
							{
								EditorApplication.SelectLink(link);

								/*ImDrawListPtr drawListPtr = ImGui.GetWindowDrawList();
								ImDrawVert* ptr = drawListPtr._VtxWritePtr.NativePtr;
								ptr -= 4;
								ptr[0].col = Color.SlateGray.PackedValue;
								ptr[1].col = Color.SlateGray.PackedValue;
								ptr[2].col = Color.SlateGray.PackedValue;
								ptr[3].col = Color.SlateGray.PackedValue; me when i want to change the color in the same frame (nobody will notice)
								*/
							}

							linkTooltip = $"Link {{{string.Join(", ", link.Keyframes.Select(v => v.Frame.ToString()))}}}\n({link.InterpolationType})";
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

								EditorApplication.SetDragAction(new MoveKeyframeDelegateAction([(value, i)]));
							}

							if ((clickedLeft || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) && ImGui.GetIO().KeyCtrl)
							{
								if (newLinkCreationData == null)
									newLinkCreationData = new CreationLinkData(value, keyframe.Frame, 0);
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
					timelineZoomTarget = oldTimelineZoomTarget;
				}
				else
				{
					interpolationValue.trackId = string.Empty;
					interpolationValue.linkId = -1;
					interpolationValue.accumulation = 0;
				}

				ImGui.TreePop();
			}

			void DrawMainKeyframe(int frame)
			{
				DrawKeyFrame(frame, Color.LightGray, out NVector2 min, out NVector2 max);

				if (ImGui.IsMouseHoveringRect(min, max))
				{
					ImGui.BeginTooltip();
					ImGui.Text($"Entity: {selectedEntity}\nFrame: {frame}");
					ImGui.EndTooltip();

					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
					{
						animator.CurrentKeyframe = frame;
						animator.Stop();

						List<(KeyframeableValue, int)> keyframesToMove = new List<(KeyframeableValue, int)>();

						foreach (KeyframeableValue value in selectedEntity.EnumerateKeyframeableValues())
						{
							Keyframe keyframe = value.GetKeyframe(frame);

							if (keyframe != null)
								keyframesToMove.Add((value, value.IndexOfKeyframe(keyframe)));
						}

						EditorApplication.SetDragAction(new MoveKeyframeDelegateAction(keyframesToMove.ToArray()));
					}

					if (ImGui.IsKeyDown(ImGuiKey.Delete))
					{
						foreach (KeyframeableValue value in selectedEntity.EnumerateKeyframeableValues())
						{
							value.RemoveKeyframe(frame);
						}
					}
				}
			}
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
						visibleStartingFrame += animator.CurrentKeyframe - visibleEndingFrame + 1;

					break;
				case "Last (End)":
					animator.CurrentKeyframe = animator.GetLastFrame();

					if (animator.CurrentKeyframe <= visibleStartingFrame)
						visibleStartingFrame = animator.CurrentKeyframe;
					else if (animator.CurrentKeyframe >= visibleEndingFrame)
						visibleStartingFrame += animator.CurrentKeyframe - visibleEndingFrame + 1;

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
				for (int index = 0; index < toolbarButtonDefinitions.Length; index++)
				{
					(string id, string text) = toolbarButtonDefinitions[index];

					if (index > 0)
						ImGui.SameLine();

					if (id.Equals("Backward") || id.Equals("Forward (Enter)") || id.Equals("Loop (L)"))
					{
						bool toggeld = GetToggleButtonCondition(id, animator);

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

		private static void DrawTimeline(Animator animator, bool isSelectedEntityValid, float headerYPadding, float headerHeight = 24f)
		{
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
			timelineZoom = Math.Clamp(MathHelper.Lerp(timelineZoom, timelineZoomTarget, 0.1f), 0.1f, 5f);
			ImGui.PushClipRect(timelineRegionMin, timelineRegionMax, false);

			{
				ImGui.InvisibleButton("##header-region", headerSize);

				// set frame
				if (ImGui.IsItemHovered())
				{
					int hoveringFrame = GetFrameForTimelinePos((ImGui.GetMousePos().X - timelineRegionMin.X) / timelineZoom);
					ImGui.BeginTooltip();
					ImGui.Text(hoveringFrame.ToString());
					ImGui.EndTooltip();

					if (ImGui.IsMouseDown(0))
					{
						animator.CurrentKeyframe = hoveringFrame;
						animator.Stop();
					}
				}

				// panning the timeline
				if (ImGui.IsMouseHoveringRect(timelineRegionMin, timelineRegionMax, false))
				{
					if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
						selectedLink = null;

					if (ImGui.IsMouseDragging(ImGuiMouseButton.Right, 0))
					{
						accumalatedPanningDeltaX += ImGui.GetIO().MouseDelta.X;

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
						timelineZoomTarget += (int)ImGui.GetIO().MouseWheel * 0.3f;
						timelineZoomTarget = Math.Clamp(timelineZoomTarget, 0, 5);
						isPanningTimeline = false;
						accumalatedPanningDeltaX = 0f;
					}
				}

				// draw all timeline lines
				float frames = (visibleEndingFrame - visibleStartingFrame) / timelineZoom;

				for (int f = 0; f < frames; f++)
				{
					int frame = f + visibleStartingFrame;
					NVector2 lineStart = timelineRegionMin;
					lineStart.X += LineStartOffset + f * PixelsPerFrame * timelineZoom;
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
				if (animator.CurrentKeyframe >= visibleStartingFrame && animator.CurrentKeyframe <= visibleEndingFrame / timelineZoom)
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
					DrawLink(newLinkCreationData.LowerBorder, newLinkCreationData.UpperBorder,
						timelineRegionMin.Y + ImGui.GetStyle().ItemSpacing.Y * newLinkCreationData.value.Owner.EnumerateKeyframeableValues().IndexOf(newLinkCreationData.value),
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
			int halfKeyFrameWidth = (int)Math.Floor(keyframeSize / 2);

			// 12 seems to be the offset from start of timelime region, dont know why it happens with columns
			cursorPos.X += GetTimelinePosForFrame(frame) - (halfKeyFrameWidth + 12);
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

		public static int GetFrameForTimelinePos(float x) => (int)(Math.Floor((x - LineStartOffset / timelineZoom) / PixelsPerFrame + 0.5f) + visibleStartingFrame);

		public static float GetTimelinePosForFrame(int frame) => (frame - visibleStartingFrame) * PixelsPerFrame * timelineZoom + LineStartOffset;
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