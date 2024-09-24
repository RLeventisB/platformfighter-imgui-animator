using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Editor.Gui
{
	public static class Hierarchy
	{
		public const int WindowWidth = 300;
		public static bool nextFrameSave;
		public static FilePickerDefinition OpenFdDefinition;
		public static float PivotViewerZoom = 2f;
		public static Vector2 PivotViewerOffset = Vector2.Zero;

		public static void Draw()
		{
			ImGui.SetNextWindowPos(new NVector2(EditorApplication.Graphics.Viewport.Width - WindowWidth, 0), ImGuiCond.FirstUseEver);
			ImGui.SetNextWindowSize(new NVector2(WindowWidth, EditorApplication.Graphics.Viewport.Height), ImGuiCond.FirstUseEver);

			ImGui.Begin("Management", SettingsManager.ToolsWindowFlags);
			{
				DrawUiActions();
				DrawUiHierarchyFrame();
				DrawUiProperties();
			}

			ImGui.End();
		}

		private static void DrawUiActions()
		{
			NVector2 toolbarSize = NVector2.UnitY * (ImGui.GetTextLineHeightWithSpacing() * 2 + ImGui.GetStyle().ItemSpacing.Y * 2);
			ImGui.BeginChild("Management actions", toolbarSize, ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
			{
				ImGui.Text($"{IcoMoon.HammerIcon} Actions");

				if (nextFrameSave)
				{
					OpenFdDefinition = CreateFilePickerDefinition("Guardar proyecto", "*.anim");
					ImGui.OpenPopup("Save project");
					nextFrameSave = false;
				}

				if (ImGui.Button($"{IcoMoon.HammerIcon}##New project"))
				{
					if (SettingsManager.ConfirmOnNewProject)
					{
						ImGui.OpenPopup("Confirmar nuevo proyecto");
					}
					else
					{
						EditorApplication.State = new State();
						EditorApplication.ResetEditor();
					}
				}

				ImGui.SetItemTooltip("Nuevo proyecto");

				bool popupOpen = true;
				ImGui.SetNextWindowSize(new NVector2(500, 200));

				if (ImGui.BeginPopupModal("Confirmar nuevo proyecto", ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
				{
					ImGui.Text("EsTAS seGURO de HACER un NUEVO proYECTO ?\neste menu es horrible");
					ImGui.SetCursorPosY(200 - ImGui.GetTextLineHeight() - ImGui.GetStyle().IndentSpacing);

					if (ImGui.Button("si wn deja de gritar"))
					{
						EditorApplication.ResetEditor();
						ImGui.CloseCurrentPopup();
					}

					ImGui.SameLine();

					if (ImGui.Button("deja guardar >:("))
					{
						ImGui.CloseCurrentPopup();
						nextFrameSave = true;
					}

					ImGui.SameLine();

					if (ImGui.Button("jaja no"))
					{
						ImGui.CloseCurrentPopup();
					}

					ImGui.EndPopup();
				}

				ImGui.SameLine();

				if (DelegateButton("Save project", $"{IcoMoon.FloppyDiskIcon}", "Guardar proyecto"))
				{
					OpenFdDefinition = CreateFilePickerDefinition("Guardar proyecto", "*.anim");

					ImGui.OpenPopup("Save project");
				}

				DoPopup("Save project", ref OpenFdDefinition, SettingsManager.SaveProject);

				ImGui.SameLine();

				if (DelegateButton("Open project", $"{IcoMoon.FolderOpenIcon}", "Abrir proyecto"))
				{
					OpenFdDefinition = CreateFilePickerDefinition("Abrir proyecto", "*.anim");

					ImGui.OpenPopup("Open project");
				}

				DoPopup("Open project", ref OpenFdDefinition, SettingsManager.LoadProject);

				ImGui.SameLine();

				if (DelegateButton("Settings", $"{IcoMoon.SettingsIcon}", "Ve los ajust :)))"))
				{
					ImGui.OpenPopup("Settings");
				}

				SettingsManager.DrawSettingsPopup();

				ImGui.SameLine();

				if (ImGui.Checkbox("##Hitbox viewer mode", ref Timeline.HitboxMode))
				{
					if (EditorApplication.selectedData.ObjectSelectionType is SelectionType.Graphic or SelectionType.Hitbox)
						EditorApplication.selectedData.Empty();
				}

				ImGui.SetItemTooltip("Cambiar a editor de hitboxes");
			}

			ImGui.EndChild();
		}

		private static void DrawUiHierarchyFrame()
		{
			NVector2 size = ImGui.GetContentRegionAvail();
			NVector2 itemSpacing = ImGui.GetStyle().ItemSpacing + NVector2.UnitY * 8;
			ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);

			ImGui.BeginChild("Project objects", size - NVector2.UnitY * 600, ImGuiChildFlags.FrameStyle);
			{
				ImGui.Text($"{IcoMoon.ListIcon} Hierarchy");

				// create entity
				bool itemHovered = false;
				ImGui.AlignTextToFramePadding();
				ImGui.Text($"{IcoMoon.ImagesIcon} Entities");
				ImGui.SameLine();

				ImGui.BeginDisabled(EditorApplication.State.Textures.Count == 0);

				if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##CreateEntity"))
				{
					ImGui.OpenPopup("Create entity");
					DoEntityCreatorReset();
				}

				ImGui.EndDisabled();

				DoEntityCreatorModal(EditorApplication.State.Textures.Keys.ToArray(), (name, selectedTexture) =>
				{
					TextureEntity textureEntity = new TextureEntity(name, selectedTexture);

					EditorApplication.State.GraphicEntities[name] = textureEntity;

					EditorApplication.selectedData = new SelectionData(textureEntity);
				});

				// show all created entities
				ImGui.Indent();
				bool changedSelectedObject = false;

				foreach (TextureEntity entity in EditorApplication.State.GraphicEntities.Values)
				{
					bool selected = EditorApplication.selectedData.IsOf(entity);

					if (ImGui.Selectable(entity.Name, ref selected) && selected)
					{
						changedSelectedObject = EditorApplication.selectedData.IsNotButSameType(entity);
						EditorApplication.selectedData = new SelectionData(entity);
					}

					if (ImGui.BeginPopupContextItem())
					{
						if (ImGui.Button($"{IcoMoon.MinusIcon} Remove"))
						{
							EditorApplication.State.GraphicEntities.Remove(entity.Name);
							if (EditorApplication.selectedData.IsOf(entity))
								EditorApplication.selectedData.Empty();

							continue;
						}

						ImGui.EndPopup();
					}

					if (!ImGui.IsItemHovered())
						continue;

					itemHovered = true;
					EditorApplication.hoveredEntityName = entity.Name;
				}

				if (changedSelectedObject)
				{
					ResetSavedInput();
				}

				ImGui.Unindent();

				if (!itemHovered)
					EditorApplication.hoveredEntityName = string.Empty;

				// hitboxes!!!
				ImGui.AlignTextToFramePadding();
				ImGui.Text($"{IcoMoon.TargetIcon} Hitboxes");
				ImGui.SameLine();

				if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##CreateHitbox"))
				{
					entityName = "Hitbox " + EditorApplication.State.Animator.RegisteredHitboxes.registry.Count;
					ImGui.OpenPopup("Create hitbox entity");
				}

				ImGui.SetNextWindowSize(new NVector2(350, 100), ImGuiCond.Always);

				if (ImGui.BeginPopupModal("Create hitbox entity"))
				{
					ImGui.InputText("Hitbox name", ref entityName, 64);

					if (ImGui.Button("Create hitbox"))
					{
						HitboxEntity hitboxEntity = new HitboxEntity(entityName);

						EditorApplication.State.HitboxEntities[entityName] = hitboxEntity;

						EditorApplication.selectedData = new SelectionData(hitboxEntity);

						ImGui.CloseCurrentPopup();
					}

					ImGui.SameLine();

					if (ImGui.Button("Cancel"))
					{
						ImGui.CloseCurrentPopup();
					}

					ImGui.EndPopup();
				}

				// show all created ~~entities~~ NUH UH hitboxes
				ImGui.Indent();

				changedSelectedObject = false;

				foreach (HitboxEntity entity in EditorApplication.State.HitboxEntities.Values)
				{
					bool selected = EditorApplication.selectedData.IsOf(entity);

					if (ImGui.Selectable(entity.Name, ref selected) && selected)
					{
						changedSelectedObject = EditorApplication.selectedData.IsNotButSameType(entity);
						EditorApplication.selectedData = new SelectionData(entity);
					}

					if (ImGui.BeginPopupContextItem())
					{
						if (ImGui.Button($"{IcoMoon.MinusIcon} Remove"))
						{
							EditorApplication.State.HitboxEntities.Remove(entity.Name);
							if (EditorApplication.selectedData.IsOf(entity))
								EditorApplication.selectedData.Empty();

							continue;
						}

						ImGui.EndPopup();
					}

					if (!ImGui.IsItemHovered())
						continue;

					itemHovered = true;
				}

				if (changedSelectedObject)
				{
					ResetSavedInput();
				}

				ImGui.Unindent();

				if (!itemHovered)
					EditorApplication.hoveredEntityName = string.Empty;

				// Add textures
				ImGui.AlignTextToFramePadding();
				ImGui.Text($"{IcoMoon.TextureIcon} Textures");
				ImGui.SameLine();

				if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##CreateTexture"))
				{
					OpenFdDefinition = CreateFilePickerDefinition("Seleccionar textura", "*.png");

					ImGui.OpenPopup("Load texture");
				}

				DoPopup("Load texture", ref OpenFdDefinition, path =>
				{
					string key = Path.GetFileNameWithoutExtension(path);

					if (EditorApplication.State.Textures.ContainsKey(key))
						return;

					Texture2D texture = Texture2D.FromFile(EditorApplication.Graphics, path);

					TextureFrame frame = new TextureFrame(key, texture, path,
						new Point(texture.Width, texture.Height),
						null,
						new NVector2(texture.Width / 2f, texture.Height / 2f));

					EditorApplication.State.Textures[key] = frame;

					EditorApplication.selectedData = new SelectionData(frame);
				});

				// show all loaded textures
				ImGui.Indent();

				foreach (TextureFrame texture in EditorApplication.State.Textures.Values)
				{
					bool selected = EditorApplication.selectedData.IsOf(texture);

					if (ImGui.Selectable(texture.Name, ref selected) && selected)
					{
						EditorApplication.selectedData = new SelectionData(texture);
					}

					if (ImGui.BeginPopupContextItem())
					{
						if (ImGui.Button($"{IcoMoon.MinusIcon} Remove"))
						{
							ImGui.EndPopup();
							EditorApplication.State.Textures.Remove(texture.Name);
							if (EditorApplication.selectedData.IsOf(texture))
								EditorApplication.selectedData.Empty();
						}

						if (ImGui.Button($"{IcoMoon.HammerIcon} Rename"))
						{
							ImGui.OpenPopup("Renombrar textura");
							ResetSavedInput(texture.Name);
							EditorApplication.selectedData = new SelectionData(texture);
						}

						ImGui.EndPopup();
					}
				}

				bool popupOpen = true;

				if (ImGui.BeginPopupModal("Renombrar textura", ref popupOpen, ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
				{
					ImGui.SetWindowSize(new NVector2(400, 100));
					string input = SavedInput("Nuevo nombre:", string.Empty);

					ImGui.SetNextItemShortcut(ImGuiKey.Enter);

					if (ImGui.Button("Renombrar"))
					{
						if (EditorApplication.selectedData.ObjectSelectionType == SelectionType.Texture)
						{
							TextureFrame frame = (TextureFrame)EditorApplication.selectedData.Reference;
							EditorApplication.RenameTexture(frame, input);
						}
					}

					ImGui.SetNextItemShortcut(ImGuiKey.Escape);
					ImGui.SameLine();

					if (ImGui.Button("Cancelar"))
					{
						ImGui.CloseCurrentPopup();
					}

					ImGui.EndPopup();
				}

				ImGui.Unindent();

				ImGui.TreePop();
			}

			ImGui.EndChild();
			ImGui.PopStyleVar();
		}

		private static void DrawUiProperties()
		{
			ImGui.BeginChild("Selected Entity Properties", NVector2.UnitY * 350, ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
			ImGui.Text($"{IcoMoon.EqualizerIcon} Properties");
			bool isSelectedDataValid = EditorApplication.selectedData.GetValue(out object obj);

			if (!isSelectedDataValid)
			{
				ImGui.EndChild();

				return;
			}

			switch (EditorApplication.selectedData.ObjectSelectionType)
			{
				case SelectionType.Graphic:
				{
					TextureEntity selectedTextureEntity = (TextureEntity)obj;

					string tempEntityName = SavedInput(string.Empty, selectedTextureEntity.Name);
					ImGui.SameLine();

					if (ImGui.Button("Rename") && !EditorApplication.State.GraphicEntities.ContainsKey(tempEntityName))
					{
						EditorApplication.RenameEntity(selectedTextureEntity, tempEntityName);
						ResetSavedInput();
					}

					ImGui.Separator();

					ImGui.Columns(2);
					ImGui.SetColumnWidth(0, 28);

					ImGui.NextColumn();
					ImGui.Text("All properties");
					ImGui.Separator();
					ImGui.NextColumn();

					int keyframeButtonId = 0;

					foreach (KeyframeableValue keyframeableValue in selectedTextureEntity.EnumerateKeyframeableValues())
					{
						ImGui.PushID(keyframeButtonId++);

						ImGui.PopID();

						ImGui.NextColumn();

						switch (keyframeableValue.Name)
						{
							case ScaleProperty:
							case PositionProperty:
								Vector2 vector2 = ((Vector2KeyframeValue)keyframeableValue).CachedValue;

								NVector2 newVector2 = new NVector2(vector2.X, vector2.Y);

								if (ImGui.DragFloat2(keyframeableValue.Name, ref newVector2))
									keyframeableValue.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, (Vector2)newVector2);

								break;
							case FrameIndexProperty:
								int frameIndex = ((IntKeyframeValue)keyframeableValue).CachedValue;

								TextureFrame texture = EditorApplication.State.GetTexture(selectedTextureEntity.TextureName);
								int framesX = texture.Width / texture.FrameSize.X;
								int framesY = texture.Height / texture.FrameSize.Y;

								if (ImGui.SliderInt(keyframeableValue.Name, ref frameIndex, 0, framesX * framesY - 1))
									keyframeableValue.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, frameIndex);

								break;
							case RotationProperty:
								float rotation = ((FloatKeyframeValue)keyframeableValue).CachedValue;
								rotation = MathHelper.ToDegrees(rotation);

								if (ImGui.DragFloat(keyframeableValue.Name, ref rotation, 1f, -360, 360, "%.0f deg", ImGuiSliderFlags.NoRoundToFormat))
									keyframeableValue.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, MathHelper.ToRadians(rotation));

								break;
							case TransparencyProperty:
								float transparency = ((FloatKeyframeValue)keyframeableValue).CachedValue;

								if (ImGui.DragFloat(keyframeableValue.Name, ref transparency, 0.001f, 0, 1f, "%f%", ImGuiSliderFlags.NoRoundToFormat))
									keyframeableValue.SetKeyframeValue(EditorApplication.State.Animator.CurrentKeyframe, transparency);

								break;
						}

						ImGui.NextColumn();
					}

					break;
				}
				case SelectionType.Texture:
				{
					TextureFrame selectedTexture = (TextureFrame)obj;
					Point currentFrameSize = selectedTexture.FrameSize;
					Point currentFramePosition = selectedTexture.FramePosition;
					NVector2 currentPivot = selectedTexture.Pivot;

					unsafe
					{
						ImGui.Text("Frame size");
						GCHandle handle = GCHandle.Alloc(currentFrameSize, GCHandleType.Pinned); // why isnt imgui.dragint2 using ref Point as a parameter :(((((((((
						ImGui.DragScalarN("##Frame size slider", ImGuiDataType.S32, handle.AddrOfPinnedObject(), 2);
						currentFrameSize.X = ((int*)handle.AddrOfPinnedObject().ToPointer())[0];
						currentFrameSize.Y = ((int*)handle.AddrOfPinnedObject().ToPointer())[1];
						handle.Free();

						ImGui.Text("Frame position");
						handle = GCHandle.Alloc(currentFramePosition, GCHandleType.Pinned); // why isnt imgui.dragint2 using ref Point as a parameter :(((((((((
						ImGui.DragScalarN("##Frame position slider", ImGuiDataType.S32, handle.AddrOfPinnedObject(), 2);
						currentFramePosition.X = ((int*)handle.AddrOfPinnedObject().ToPointer())[0];
						currentFramePosition.Y = ((int*)handle.AddrOfPinnedObject().ToPointer())[1];
						handle.Free();
					}

					ImGui.BeginGroup();
					ImGui.Text("Pivot");
					ImGui.DragFloat2("##Pivot x", ref currentPivot);

					if (ImGui.Button("Center to frame size"))
					{
						currentPivot = new NVector2(currentFrameSize.X / 2f, currentFrameSize.Y / 2f);
					}

					ImGui.SameLine();

					if (ImGui.Button("Reset"))
					{
						currentPivot = new NVector2(selectedTexture.Width / 2f, selectedTexture.Height / 2f);
						currentFrameSize = selectedTexture.Size;
						currentFramePosition = Point.Zero;
					}

					ImGui.EndGroup();

					selectedTexture.FramePosition = currentFramePosition;
					selectedTexture.FrameSize = currentFrameSize;
					selectedTexture.Pivot = currentPivot;

					NVector2 scaledFramePosition = new NVector2(currentFramePosition.X * PivotViewerZoom, currentFramePosition.Y * PivotViewerZoom);
					NVector2 scaledFrameSize = new NVector2(currentFrameSize.X * PivotViewerZoom, currentFrameSize.Y * PivotViewerZoom);
					NVector2 scaledPivot = currentPivot * PivotViewerZoom;

					ImGui.SetNextItemWidth(60);
					ImGui.DragFloat("Zoom", ref PivotViewerZoom, 0.1f, 0.1f, 10f);
					ImGui.BeginChild("Pivot renderer", NVector2.UnitY * 154f, ImGuiChildFlags.FrameStyle);
					{
						NVector2 contentSize = ImGui.GetContentRegionAvail();
						NVector2 cursorPos = ImGui.GetCursorScreenPos();
						NVector2 center = cursorPos + contentSize * 0.5f;
						NVector2 frameStart = center - scaledFrameSize * 0.5f;
						
						ImDrawListPtr drawList = ImGui.GetWindowDrawList();

						if(ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
						{
							PivotViewerOffset += Input.MousePosDelta / PivotViewerZoom;
						}

						// draw texture for reference
						NVector2 size = selectedTexture.Size.ToVector2().ToNumerics();
						NVector2 scaledSize = size * PivotViewerZoom;

						NVector2 relativeTopLeft = -scaledFramePosition - (scaledPivot - scaledFrameSize / 2);
						NVector2 relativeBottomRight = relativeTopLeft + scaledSize;

						NVector2 textureInFrameTopLeft = NVector2.Zero;
						NVector2 textureInFrameBottomRight = scaledFrameSize;
						
						NVector2 uvMin = InverseLerp(textureInFrameTopLeft, relativeTopLeft, relativeBottomRight);
						NVector2 uvMax = InverseLerp(textureInFrameBottomRight, relativeTopLeft, relativeBottomRight);

						if (uvMin.X < 0)
						{
							textureInFrameTopLeft.X -= uvMin.X * scaledSize.X;
							textureInFrameTopLeft.X = MathF.Min(textureInFrameTopLeft.X, textureInFrameBottomRight.X);
							uvMin.X = 0;
						}

						if (uvMin.Y < 0)
						{
							textureInFrameTopLeft.Y -= uvMin.Y * scaledSize.Y;
							textureInFrameTopLeft.Y = MathF.Min(textureInFrameTopLeft.Y, textureInFrameBottomRight.Y);
							uvMin.Y = 0;
						}
						
						if (uvMax.X > 1)
						{
							textureInFrameBottomRight.X -= (uvMax.X - 1) * scaledSize.X;
							textureInFrameBottomRight.X = MathF.Max(textureInFrameTopLeft.X, textureInFrameBottomRight.X);
							uvMax.X = 1;
						}

						if (uvMax.Y > 1)
						{
							textureInFrameBottomRight.Y -= (uvMax.Y - 1) * scaledSize.Y;
							textureInFrameBottomRight.Y = MathF.Max(textureInFrameTopLeft.Y, textureInFrameBottomRight.Y);
							uvMax.Y = 1;
						}
						
						// drawList.AddCircleFilled(frameStart + textureInFrameTopLeft, 3, Color.Red.PackedValue);
						// drawList.AddCircleFilled(frameStart + textureInFrameBottomRight, 3, Color.Red.PackedValue);
						drawList.AddImage(selectedTexture.TextureId,
							frameStart + relativeTopLeft,
							frameStart + relativeBottomRight, NVector2.Zero, NVector2.One, Color.DimGray.PackedValue);

						drawList.AddImage(selectedTexture.TextureId,
							frameStart + textureInFrameTopLeft,
							frameStart + textureInFrameBottomRight, uvMin, uvMax);

						// draw frame size
						drawList.AddRect(frameStart, frameStart + scaledFrameSize, Color.GreenYellow.PackedValue);

						// horizontal line
						drawList.AddLine(center - NVector2.UnitX * scaledFrameSize * 0.5f,
							center + NVector2.UnitX * scaledFrameSize * 0.5f,
							Color.ForestGreen.PackedValue);

						// vertical line
						drawList.AddLine(center - NVector2.UnitY * scaledFrameSize * 0.5f,
							center + NVector2.UnitY * scaledFrameSize * 0.5f,
							Color.ForestGreen.PackedValue);

						// draw pivot
						drawList.AddCircleFilled(frameStart + scaledPivot, 4, Color.White.PackedValue);

						ImGui.EndChild();
					}

					break;
				}
			}

			ImGui.EndChild();
		}

		private static void DoPopup(string id, ref FilePickerDefinition fpd, Action<string> onDone)
		{
			bool popupOpen = true;

			if (fpd.CurrentFolderInfo == null || !fpd.CurrentFolderInfo.Exists)
			{
				ImGui.CloseCurrentPopup();

				return;
			}

			ImGui.SetNextWindowContentSize(NVector2.One * 400);

			if (ImGui.BeginPopupModal(id, ref popupOpen, ImGuiWindowFlags.NoResize))
			{
				ImGui.Text("Selecciona arhchivos.,");
				bool result = false;

				NVector2 ch = ImGui.GetContentRegionAvail();
				float frameHeight = ch.Y - (ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().WindowPadding.Y * 3.5f);

				if (ImGui.BeginChild("##Directory Viewer", new NVector2(0, frameHeight), ImGuiChildFlags.FrameStyle, ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.NoResize))
				{
					DirectoryInfo currentFolder = fpd.CurrentFolderInfo;

					// show folders
					ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow.PackedValue);

					if (currentFolder.Parent != null)
					{
						if (ImGui.Selectable("../", false, ImGuiSelectableFlags.NoAutoClosePopups))
							fpd.SelectFolder(currentFolder.Parent);
					}

					foreach (DirectoryInfo directoryInfo in fpd.FolderFolders)
					{
						if (ImGui.Selectable(directoryInfo.Name + "/", false, ImGuiSelectableFlags.NoAutoClosePopups))
							fpd.SelectFolder(directoryInfo);
					}

					ImGui.PopStyleColor();

					// show files
					foreach (FileInfo fileInfo in fpd.FolderFiles)
					{
						string name = fileInfo.Name;
						bool isSelected = fpd.SelectedFileInfo == fileInfo;

						if (ImGui.Selectable(name, ref isSelected, ImGuiSelectableFlags.NoAutoClosePopups))
						{
							fpd.SelectFile(fileInfo);
						}

						if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && isSelected)
						{
							result = true;
							ImGui.CloseCurrentPopup();
						}
					}
				}

				ImGui.EndChild();

				ImGui.SetNextItemWidth(ch.X);
				string fileName = fpd.SelectedFileName ?? string.Empty;

				if (ImGui.InputText(string.Empty, ref fileName, 64))
				{
					FileInfo writtenFileInfo = new FileInfo(fpd.CurrentFolderPath + fileName);

					if (writtenFileInfo.Exists)
					{
						fpd.SelectFile(writtenFileInfo);
					}
				}

				ImGui.SetNextItemShortcut(ImGuiKey.Escape);

				if (ImGui.Button("Cancel"))
				{
					result = false;
					ImGui.CloseCurrentPopup();
				}

				if (fpd.SelectedFileInfo != null && fpd.SelectedFileInfo.Exists)
				{
					ImGui.SameLine();

					if (ImGui.Button(fpd.ActionButtonLabel))
					{
						result = true;
						ImGui.CloseCurrentPopup();
					}
				}

				if (result)
					onDone?.Invoke(fpd.SelectedFileFullPath);

				ImGui.EndPopup();
			}
		}
	}
}