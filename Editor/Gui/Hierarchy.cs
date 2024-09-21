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
			NVector2 toolbarSize = NVector2.UnitY * (ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2);
			ImGui.Text($"{IcoMoon.HammerIcon} Actions");
			ImGui.BeginChild(1, toolbarSize, ImGuiChildFlags.FrameStyle);
			{
				if (nextFrameSave)
				{
					OpenFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly().Location, "Save", ".anim");
					ImGui.OpenPopup("Save project");
					nextFrameSave = false;
				}

				if (DelegateButton("New project", $"{IcoMoon.HammerIcon}", "New project"))
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

				bool popupOpen = true;
				ImGui.SetNextWindowSize(new NVector2(400, 140));

				if (ImGui.BeginPopupModal("Confirmar nuevo proyecto", ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
				{
					ImGui.Text("EsTAS seGURO de HACER un NUEVO proYECTO ?\neste menu es horrible");

					if (ImGui.Button("si wn deja de gritar"))
					{
						EditorApplication.ResetEditor();
						ImGui.CloseCurrentPopup();
					}

					// ImGui.SameLine();
					if (ImGui.Button("deja guardar >:("))
					{
						ImGui.CloseCurrentPopup();
						nextFrameSave = true;
					}

					// ImGui.SameLine();
					if (ImGui.Button("jaja no"))
					{
						ImGui.CloseCurrentPopup();
					}

					ImGui.EndPopup();
				}

				ImGui.SameLine();

				if (DelegateButton("Save project", $"{IcoMoon.FloppyDiskIcon}", "Save project"))
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

				if (DelegateButton("Open project", $"{IcoMoon.FolderOpenIcon}", "Open project"))
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

				ImGui.SetNextWindowContentSize(NVector2.One * 600);

				SettingsManager.DrawSettingsPopup();
			}

			ImGui.EndChild();
		}

		private static void DrawUiHierarchyFrame()
		{
			NVector2 size = ImGui.GetContentRegionAvail();
			NVector2 itemSpacing = ImGui.GetStyle().ItemSpacing + NVector2.UnitY * 8;
			ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);

			ImGui.Text($"{IcoMoon.ListIcon} Hierarchy");
			ImGui.BeginChild(2, size - NVector2.UnitY * 256, ImGuiChildFlags.FrameStyle);
			{
				// create entity
				bool itemHovered = false;
				ImGui.AlignTextToFramePadding();
				ImGui.Text($"{IcoMoon.ImagesIcon} Entities");
				ImGui.SameLine();

				if (EditorApplication.State.Textures.Count > 0)
				{
					if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##CreateEntity"))
					{
						ImGui.OpenPopup("Create entity");
						DoEntityCreatorReset();
					}
				}
				else
				{
					DisabledButton($"{IcoMoon.PlusIcon}");
				}

				DoEntityCreatorModal(EditorApplication.State.Textures.Keys.ToArray(), (name, selectedTexture) =>
				{
					TextureEntity textureEntity = new TextureEntity(name, selectedTexture);

					EditorApplication.State.GraphicEntities[name] = textureEntity;

					EditorApplication.selectedEntityName = name;
				});

				// show all created entities
				ImGui.Indent();
				bool onGrid = !ImGui.GetIO().WantCaptureMouse && !ImGui.GetIO().WantCaptureKeyboard;
				bool clickedOnGrid = onGrid && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
				bool hasSelected = false;
				string oldSelectedEntityId = EditorApplication.selectedEntityName;

				foreach (TextureEntity entity in EditorApplication.State.GraphicEntities.Values)
				{
					if (!hasSelected && entity.IsBeingHovered(Input.MouseWorld, EditorApplication.State.Animator.CurrentKeyframe) && clickedOnGrid)
					{
						if (EditorApplication.selectedEntityName != entity.Name)
						{
							hasSelected = true;
						}
					}

					bool selected = EditorApplication.selectedEntityName == entity.Name;
					ImGui.Selectable(entity.Name, ref selected);

					if (selected)
					{
						EditorApplication.selectedTextureName = string.Empty;
						EditorApplication.selectedEntityName = entity.Name;
					}

					if (!ImGui.IsItemHovered())
						continue;

					itemHovered = true;
					EditorApplication.hoveredEntityName = entity.Name;
				}

				if (oldSelectedEntityId != EditorApplication.selectedEntityName)
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
					OpenFdDefinition = CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
						.Location, "Open", ".png");

					ImGui.OpenPopup("Load texture");
				}

				DoPopup("Load texture", ref OpenFdDefinition, () =>
				{
					string key = Path.GetFileNameWithoutExtension(OpenFdDefinition.SelectedFileName);

					if (!EditorApplication.State.Textures.ContainsKey(key))
					{
						string path = OpenFdDefinition.SelectedRelativePath;
						Texture2D texture = Texture2D.FromFile(EditorApplication.Graphics, path);

						EditorApplication.State.Textures[key] = new TextureFrame(texture, path,
							new Point(texture.Width, texture.Height),
							new NVector2(texture.Width / 2f, texture.Height / 2f));

						EditorApplication.selectedTextureName = key;
					}
				});

				// show all loaded textures
				ImGui.Indent();

				foreach (string texture in EditorApplication.State.Textures.Keys)
				{
					bool selected = EditorApplication.selectedTextureName == texture;
					ImGui.Selectable(texture, ref selected);

					if (ImGui.BeginPopupContextItem())
					{
						if (ImGui.Button($"{IcoMoon.MinusIcon} Remove"))
						{
							EditorApplication.State.Textures.Remove(texture);
							if (EditorApplication.selectedTextureName == texture)
								EditorApplication.selectedTextureName = string.Empty;
						}

						ImGui.EndPopup();
					}

					if (selected)
					{
						EditorApplication.selectedEntityName = string.Empty;
						EditorApplication.selectedTextureName = texture;
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
			ImGui.Text($"{IcoMoon.EqualizerIcon} Properties");
			ImGui.BeginChild(3, NVector2.UnitY * 208, ImGuiChildFlags.FrameStyle);
			int currentKeyframe = EditorApplication.State.Animator.CurrentKeyframe;

			if (!string.IsNullOrEmpty(EditorApplication.selectedEntityName))
			{
				TextureEntity selectedTextureEntity = EditorApplication.State.GraphicEntities[EditorApplication.selectedEntityName];

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

				if (EditorApplication.State.Animator.RegisteredGraphics.EntityHasKeyframeAtFrame(EditorApplication.selectedEntityName, currentKeyframe)) //TODO: select interpolation type in menu
				{
					// ImGui.ListBox("")
				}
			}
			else if (!string.IsNullOrEmpty(EditorApplication.selectedTextureName))
			{
				const float scale = 2f;
				TextureFrame selectedTexture = EditorApplication.State.GetTexture(EditorApplication.selectedTextureName);
				Point currentFrameSize = selectedTexture.FrameSize;
				NVector2 currentPivot = selectedTexture.Pivot;

				unsafe
				{
					GCHandle handle = GCHandle.Alloc(currentFrameSize, GCHandleType.Pinned); // why isnt imgui.dragint2 using ref Point as a parameter :(((((((((
					ImGui.DragScalarN("Framesize", ImGuiDataType.S32, handle.AddrOfPinnedObject(), 2);
					currentFrameSize.X = ((int*)handle.AddrOfPinnedObject().ToPointer())[0];
					currentFrameSize.Y = ((int*)handle.AddrOfPinnedObject().ToPointer())[1];
					handle.Free();
				}

				ImGui.DragFloat2("Pivot", ref currentPivot);

				selectedTexture.FrameSize = currentFrameSize;
				selectedTexture.Pivot = currentPivot;

				NVector2 scaledFrameSize = new NVector2(currentFrameSize.X * scale, currentFrameSize.Y * scale);
				NVector2 scaledPivot = currentPivot * scale;

				ImGui.BeginChild(2, NVector2.UnitY * 154f, ImGuiChildFlags.FrameStyle);

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