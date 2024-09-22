using Editor.Model;

using ImGuiNET;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Editor.Gui
{
	public static class Hierarchy
	{
		public const int WindowWidth = 300;
		public static bool nextFrameSave;
		public static FilePickerDefinition OpenFdDefinition;

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
			ImGui.BeginChild("Management actions", toolbarSize, ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AutoResizeY);
			{
				ImGui.Text($"{IcoMoon.HammerIcon} Actions");

				if (nextFrameSave)
				{
					OpenFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly().Location, "Save", ".anim");
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
					OpenFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
						.Location, "Save", ".anim");

					ImGui.OpenPopup("Save project");
				}

				DoPopup("Save project", ref OpenFdDefinition, () =>
				{
					if (!Path.HasExtension(OpenFdDefinition.SelectedRelativePath))
					{
						OpenFdDefinition.SelectedRelativePath += ".anim";
					}

					SettingsManager.SaveProject();
				});

				ImGui.SameLine();

				if (DelegateButton("Open project", $"{IcoMoon.FolderOpenIcon}", "Abrir proyecto"))
				{
					OpenFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
						.Location, "Open", ".anim");

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
					OpenFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly().Location, "Open", ".png");

					ImGui.OpenPopup("Load texture");
				}

				DoPopup("Load texture", ref OpenFdDefinition, () =>
				{
					string key = Path.GetFileNameWithoutExtension(OpenFdDefinition.SelectedFileName);

					if (EditorApplication.State.Textures.ContainsKey(key))
						return;

					string path = OpenFdDefinition.SelectedRelativePath;
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
							EditorApplication.State.Textures.Remove(texture.Name);
							if (EditorApplication.selectedData.IsOf(texture))
								EditorApplication.selectedData.Empty();
						}

						ImGui.EndPopup();
					}
				}

				ImGui.Unindent();

				ImGui.TreePop();
			}

			ImGui.EndChild();
			ImGui.PopStyleVar();
		}

		private static void DrawUiProperties()
		{
			ImGui.BeginChild("Selected Entity Properties", ImGui.GetContentRegionAvail().Y * NVector2.UnitY, ImGuiChildFlags.FrameStyle);
			ImGui.Text($"{IcoMoon.EqualizerIcon} Properties");
			int currentKeyframe = EditorApplication.State.Animator.CurrentKeyframe;
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

					string tempEntityName = SavedInput(string.Empty, EditorApplication.selectedData.Name);
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

								TextureFrame texture = EditorApplication.State.GetTexture(selectedTextureEntity.TextureId);
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
					const float scale = 2f;
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
					ImGui.EndGroup();

					selectedTexture.FramePosition = currentFramePosition;
					selectedTexture.FrameSize = currentFrameSize;
					selectedTexture.Pivot = currentPivot;

					NVector2 scaledFrameSize = new NVector2(currentFrameSize.X * scale, currentFrameSize.Y * scale);
					NVector2 scaledPivot = currentPivot * scale;

					ImGui.BeginChild("Pivot renderer", NVector2.UnitY * 154f, ImGuiChildFlags.FrameStyle);

					NVector2 contentSize = ImGui.GetContentRegionAvail();
					NVector2 center = ImGui.GetCursorScreenPos() + contentSize * 0.5f;
					NVector2 frameStart = center - scaledFrameSize * 0.5f;

					// draw frame size
					ImDrawListPtr drawList = ImGui.GetWindowDrawList();
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

					break;
				}
			}

			ImGui.EndChild();
		}

		private static void DoPopup(string id, ref FilePickerDefinition fpd, Action onDone)
		{
			bool popupOpen = true;
			ImGui.SetNextWindowContentSize(NVector2.One * 400);

			if (ImGui.BeginPopupModal(id, ref popupOpen, ImGuiWindowFlags.NoResize))
			{
				if (DoFilePicker(ref fpd))
					onDone?.Invoke();

				ImGui.EndPopup();
			}
		}
	}
}