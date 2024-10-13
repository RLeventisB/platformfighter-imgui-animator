using Editor.Objects;

using ImGuiNET;

using Microsoft.Xna.Framework;

using System;
using System.Collections.Generic;
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
		private static NVector2 projectActionsPositionOffset;
		private static float projectActionsKeyframeMult = 1;
		private static IAnimationObject objectToRename;
		private static int projectActionsKeyframeToClone = -1, projectActionsKeyframeStart;
		private static int projectActionsKeyframeMove = 0;
		private static int projectActionsNewLinkType;
		private static string projectActionsCloneKeyframePath;
		private static int projectActionsFrameToCloneFromFile, projectActionsClonedFrameFromFileDest;

		public static void Draw()
		{
			ImGui.SetNextWindowPos(new NVector2(EditorApplication.Graphics.Viewport.Width - WindowWidth, 0), ImGuiCond.FirstUseEver);
			ImGui.SetNextWindowSize(new NVector2(WindowWidth, EditorApplication.Graphics.Viewport.Height), ImGuiCond.FirstUseEver);

			ImGui.Begin("Management", SettingsManager.ToolsWindowFlags | ImGuiWindowFlags.AlwaysAutoResize);
			{
				DrawUiActions();
				DrawUiHierarchyFrame();
				DrawUiProperties();
			}

			ImGui.End();
		}

		private static void DrawUiActions()
		{
			ImGui.BeginChild("Management actions", NVector2.Zero, ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize);
			{
				ImGui.Text($"{IcoMoon.HammerIcon} Actions");

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

				if (ImGui.BeginPopupModal("Confirmar nuevo proyecto", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
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

				ImGui.BeginDisabled(SettingsManager.lastProjectSavePath is null || !EditorApplication.State.HasAnyEntity);

				if (ImGui.Button($"{IcoMoon.FloppyDiskIcon}") || ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.S)) // changed to iskeychord because the item shortcut only works when item is visible, yes this also made me lose 1 hour of progress
				{
					SettingsManager.SaveProject(SettingsManager.lastProjectSavePath);
				}

				ImGui.SetItemTooltip("Guardar proyecto\nShortcut: " + GetKeyChordName(ImGuiKey.ModCtrl | ImGuiKey.S));

				ImGui.EndDisabled();

				ImGui.SameLine();

				ImGui.SetNextItemShortcut(ImGuiKey.ModCtrl | ImGuiKey.ModShift | ImGuiKey.S);
				ImGui.BeginDisabled(!EditorApplication.State.HasAnyEntity);

				if (DelegateButton("Save as", $"{IcoMoon.EmptyFileIcon}", "Guardar como") || nextFrameSave)
				{
					OpenFdDefinition = CreateFilePickerDefinition("Guardar proyecto", ".anim", "*.anim");

					ImGui.OpenPopup("Save as");
				}

				ImGui.EndDisabled();
				DoPopup("Save as", ref OpenFdDefinition, SettingsManager.SaveProject);

				ImGui.SameLine();

				if (DelegateButton("Open project", $"{IcoMoon.FolderOpenIcon}", "Abrir proyecto"))
				{
					OpenFdDefinition = CreateFilePickerDefinition("Abrir proyecto", null, "*.anim");

					ImGui.OpenPopup("Open project");
				}

				DoPopup("Open project", ref OpenFdDefinition, SettingsManager.LoadProject);

				ImGui.SameLine();

				ImGui.SetNextItemShortcut(ImGuiKey.ModAlt | ImGuiKey.S, ImGuiInputFlags.RouteAlways);

				if (ImGui.Button($"{IcoMoon.SettingsIcon}"))
				{
					ImGui.OpenPopup("Settings");
				}

				ImGui.SetItemTooltip("Ve los ajust :)))\nShortcut: Alt + S");

				SettingsManager.DrawSettingsPopup();

				ImGui.SameLine();

				ImGui.SetNextItemShortcut(ImGuiKey.ModAlt | ImGuiKey.H, ImGuiInputFlags.RouteAlways);

				if (ImGui.Checkbox("##Hitbox viewer mode", ref Timeline.HitboxMode))
				{
					if (EditorApplication.selectedData.Type is SelectionType.Graphic or SelectionType.Hitbox)
					{
						EditorApplication.selectedData.Empty();
					}
				}

				ImGui.SetItemTooltip("Cambiar a editor de hitboxes");

				ImGui.SameLine();

				ImGui.SetNextItemShortcut(ImGuiKey.ModAlt | ImGuiKey.P, ImGuiInputFlags.RouteAlways);

				if (ImGui.Button($"{IcoMoon.EqualizerIcon}"))
				{
					ImGui.OpenPopup("Project actions");
				}

				ImGui.SetItemTooltip("Acciones del proyecto\nShortcut: Alt + P");

				popupOpen = true;

				if (ImGui.BeginPopupModal("Project actions", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoDocking))
				{
					ImGui.SeparatorText("Informacion del archivo");
					if (SettingsManager.lastProjectSavePath is not null)
						ImGui.Text("Nombre del proyecto: " + Path.GetFileName(SettingsManager.lastProjectSavePath));
					else
						ImGui.Text("Proyecto sin guardar");

					ImGui.SeparatorText("Acciones globales");

					ImGui.InputFloat2("Ajustar todas las posiciones por:", ref projectActionsPositionOffset);

					ImGui.SetItemTooltip("Esto añade un valor constante a todas las posiciones de los objetos en el mundo.");

					ImGui.BeginDisabled(projectActionsPositionOffset.LengthSquared() == 0);

					if (ImGui.Button("Cambiar posiciones"))
					{
						foreach (TextureAnimationObject textureobject in EditorApplication.State.Animator.RegisteredGraphics)
						{
							foreach (Keyframe keyframe in textureobject.Position.keyframes)
							{
								keyframe.Value = (Vector2)keyframe.Value + projectActionsPositionOffset;
							}

							textureobject.Position.CacheValue(null);
						}

						foreach (HitboxAnimationObject hitboxObject in EditorApplication.State.Animator.RegisteredHitboxes)
						{
							foreach (Keyframe keyframe in hitboxObject.Position.keyframes)
							{
								keyframe.Value = (Vector2)keyframe.Value + projectActionsPositionOffset;
							}

							hitboxObject.Position.CacheValue(null);
						}

						projectActionsPositionOffset = NVector2.Zero;
					}

					ImGui.EndDisabled();

					ImGui.Separator();

					ImGui.InputFloat("Multiplicar tiempo global por:", ref projectActionsKeyframeMult);

					ImGui.BeginDisabled(projectActionsKeyframeMult is 0 or 1);

					if (ImGui.Button("Multiplicar tiempo"))
					{
						foreach (TextureAnimationObject textureobject in EditorApplication.State.Animator.RegisteredGraphics)
						{
							foreach (KeyframeableValue value in textureobject.EnumerateKeyframeableValues())
							{
								foreach (Keyframe keyframe in value.keyframes)
								{
									keyframe.Frame = (int)(keyframe.Frame * projectActionsKeyframeMult);
								}

								foreach (KeyframeLink link in value.links)
								{
									int[] frames = new int[link.Count];
									link.CopyTo(frames, 0);

									link.Clear();

									foreach (int frame in frames)
									{
										link.Add((int)(frame * projectActionsKeyframeMult));
									}
								}

								value.CacheValue(null);
							}
						}

						foreach (HitboxAnimationObject hitboxObject in EditorApplication.State.Animator.RegisteredHitboxes)
						{
							foreach (KeyframeableValue value in hitboxObject.EnumerateKeyframeableValues())
							{
								foreach (Keyframe keyframe in value.keyframes)
								{
									keyframe.Frame = (int)(keyframe.Frame * projectActionsKeyframeMult);
								}

								foreach (KeyframeLink link in value.links)
								{
									int[] frames = new int[link.Count];
									link.CopyTo(frames, 0);

									link.Clear();

									foreach (int frame in frames)
									{
										link.Add((int)(frame * projectActionsKeyframeMult));
									}
								}

								value.CacheValue(null);
							}

							hitboxObject.FrameDuration = (ushort)(hitboxObject.FrameDuration * projectActionsKeyframeMult);
							hitboxObject.SpawnFrame = (ushort)(hitboxObject.SpawnFrame * projectActionsKeyframeMult);
						}

						projectActionsKeyframeMult = 1;
					}

					ImGui.EndDisabled();

					ImGui.SetItemTooltip("Esto multiplica todas las variables relacionadas con el tiempo en el mundo por un valor.\nEsto incluye posicion de los fotograma claves, y las duraciones de las hitboxes.\nTener en cuenta que esto redondea valores, haciendolo \"lossy\"");

					ImGui.Separator();

					ImGui.InputInt("Fotograma para inicio:", ref projectActionsKeyframeToClone);

					ImGui.InputInt("Fotograma de destino:", ref projectActionsKeyframeStart);

					ImGui.BeginDisabled(projectActionsKeyframeToClone == -1 || projectActionsKeyframeStart < 0);

					if (ImGui.Button("Usar fotograma como base"))
					{
						foreach (TextureAnimationObject textureobject in EditorApplication.State.Animator.RegisteredGraphics)
						{
							foreach (KeyframeableValue value in textureobject.EnumerateKeyframeableValues())
							{
								if (!KeyframeableValue.Interpolate(value, projectActionsKeyframeToClone, KeyframeableValue.ResolveInterpolator(value), out object storedValue))
								{
									storedValue = value.DefaultValue;
								}

								value.keyframes.Clear();
								value.links.Clear();

								value.Add(new Keyframe(value, projectActionsKeyframeStart, storedValue));
							}
						}

						foreach (HitboxAnimationObject hitboxObject in EditorApplication.State.Animator.RegisteredHitboxes)
						{
							foreach (KeyframeableValue value in hitboxObject.EnumerateKeyframeableValues())
							{
								if (!KeyframeableValue.Interpolate(value, projectActionsKeyframeToClone, KeyframeableValue.ResolveInterpolator(value.DefaultValue.GetType()), out object storedValue))
								{
									storedValue = value.DefaultValue;
								}

								value.keyframes.Clear();
								value.links.Clear();

								value.Add(new Keyframe(value, projectActionsKeyframeStart, storedValue));
							}
						}

						projectActionsKeyframeStart = 0;
						projectActionsKeyframeToClone = -1;
					}

					ImGui.EndDisabled();

					ImGui.SetItemTooltip("Borra todos los datos, menos los datos en el fotograma especificado.\nEstos datos seran utilizados como la base de la \"nueva animacion\"");

					ImGui.Separator();
					string[] names = Enum.GetNames<InterpolationType>();
					ImGui.Combo("Tipo de enlace nuevo", ref projectActionsNewLinkType, names, names.Length);

					if (ImGui.Button("Enlazar todos los fotogramas vacios"))
					{
						if (Timeline.HitboxMode)
						{
							foreach (HitboxAnimationObject hitboxObject in EditorApplication.State.Animator.RegisteredHitboxes)
							{
								foreach (KeyframeableValue value in hitboxObject.EnumerateKeyframeableValues())
								{
									IEnumerable<Keyframe> loneKeyframes = value.keyframes.Where(v => KeyframeableValue.FindContainingLink(v.ContainingValue, v) is null);

									if (loneKeyframes.Count() > 1)
									{
										value.AddLink(new KeyframeLink(value, loneKeyframes)
										{
											InterpolationType = (InterpolationType)projectActionsNewLinkType
										});

										value.CacheValue(null);
									}
								}
							}
						}
						else
						{
							foreach (TextureAnimationObject textureobject in EditorApplication.State.Animator.RegisteredGraphics)
							{
								foreach (KeyframeableValue value in textureobject.EnumerateKeyframeableValues())
								{
									IEnumerable<Keyframe> loneKeyframes = value.keyframes.Where(v => KeyframeableValue.FindContainingLink(v.ContainingValue, v) is null);

									if (loneKeyframes.Count() > 1)
									{
										value.AddLink(new KeyframeLink(value, loneKeyframes)
										{
											InterpolationType = (InterpolationType)projectActionsNewLinkType
										});

										value.CacheValue(null);
									}
								}
							}
						}

						projectActionsNewLinkType = 0;
					}

					ImGui.SetItemTooltip("Añade un enlace predeterminado que incluye todos los fotogramas que no tienen algun fotograma, mientras hayan mas de 2.");

					ImGui.Separator();

					if (projectActionsCloneKeyframePath is null)
						projectActionsCloneKeyframePath = string.Empty;

					ImGui.InputText("Archivo", ref projectActionsCloneKeyframePath, 300);

					ImGui.InputInt("Fotograma a copiar:", ref projectActionsFrameToCloneFromFile);

					ImGui.InputInt("Fotograma destino:", ref projectActionsClonedFrameFromFileDest);

					ImGui.BeginDisabled(!File.Exists(projectActionsCloneKeyframePath));

					if (ImGui.Button("Clonar fotogramas de objetos similares"))
					{
						try
						{
							JsonData data = JsonData.LoadFromPath(projectActionsCloneKeyframePath);
							data.Fixup();

							// key is the current project's animation object, values are the file's animation object
							Dictionary<TextureAnimationObject, TextureAnimationObject> graphicLinks = new Dictionary<TextureAnimationObject, TextureAnimationObject>();
							Dictionary<HitboxAnimationObject, HitboxAnimationObject> hitboxLinks = new Dictionary<HitboxAnimationObject, HitboxAnimationObject>();

							foreach (TextureAnimationObject animationObjectFromFile in data.graphicObjects)
							{
								if (graphicLinks.ContainsValue(animationObjectFromFile))
									continue;

								foreach (TextureAnimationObject animationObject in EditorApplication.State.GraphicEntities.Values)
								{
									if (graphicLinks.ContainsKey(animationObject))
										continue;

									if (animationObjectFromFile.Name == animationObject.Name &&
									    animationObjectFromFile.TextureName == animationObject.TextureName)
									{
										graphicLinks.Add(animationObject, animationObjectFromFile);
									}
								}
							}

							foreach (HitboxAnimationObject hitboxObjectFromFile in data.hitboxObjects)
							{
								if (hitboxLinks.ContainsValue(hitboxObjectFromFile))
									continue;

								foreach (HitboxAnimationObject hitboxObject in EditorApplication.State.HitboxEntities.Values)
								{
									if (hitboxLinks.ContainsKey(hitboxObject))
										continue;

									if (hitboxObjectFromFile.Name == hitboxObject.Name &&
									    hitboxObjectFromFile.Type == hitboxObject.Type)
									{
										hitboxLinks.Add(hitboxObject, hitboxObjectFromFile);
									}
								}
							}

							KeyframeableValue.CacheValueOnInterpolate = false;

							foreach (KeyValuePair<TextureAnimationObject, TextureAnimationObject> pair in graphicLinks)
							{
								List<KeyframeableValue> listToClone = pair.Value.EnumerateKeyframeableValues();
								List<KeyframeableValue> listToPaste = pair.Key.EnumerateKeyframeableValues();

								CloneKeyframeValues(listToClone, listToPaste);
							}

							foreach (KeyValuePair<HitboxAnimationObject, HitboxAnimationObject> pair in hitboxLinks)
							{
								List<KeyframeableValue> listToClone = pair.Value.EnumerateKeyframeableValues();
								List<KeyframeableValue> listToPaste = pair.Key.EnumerateKeyframeableValues();

								CloneKeyframeValues(listToClone, listToPaste);
							}

							KeyframeableValue.CacheValueOnInterpolate = true;

							projectActionsFrameToCloneFromFile = -1;
							projectActionsClonedFrameFromFileDest = -1;
							projectActionsCloneKeyframePath = string.Empty;

							void CloneKeyframeValues(List<KeyframeableValue> listToClone, List<KeyframeableValue> listToPaste)
							{
								for (int index = 0; index < listToClone.Count; index++)
								{
									KeyframeableValue valueToClone = listToClone[index];
									KeyframeableValue valueToPaste = listToPaste[index];

									KeyframeableValue.Interpolate(valueToClone, projectActionsFrameToCloneFromFile, KeyframeableValue.ResolveInterpolator(valueToClone), out object keyframeData);

									valueToPaste.SetKeyframeValue(projectActionsClonedFrameFromFileDest, keyframeData);
								}
							}
						}
						catch (Exception e)
						{
							Console.WriteLine(e);
						}
					}
					
					ImGui.EndDisabled();

					ImGui.Separator();

					if (ImGui.Button("Dividir enlaces"))
					{
						foreach (TextureAnimationObject animationObject in EditorApplication.State.GraphicEntities.Values)
						{
							SplitLinks(animationObject);
						}

						foreach (HitboxAnimationObject hitboxObject in EditorApplication.State.HitboxEntities.Values)
						{
							SplitLinks(hitboxObject);
						}

						void SplitLinks(IAnimationObject animationObject)
						{
							foreach (KeyframeableValue value in animationObject.EnumerateKeyframeableValues())
							{
								for (int i = 0; i < value.links.Count; i++)
								{
									KeyframeLink link = value.links[i];

									if (link.Count <= 2)
										continue;

									List<IEnumerable<int>> smallerLinks = new List<IEnumerable<int>>();

									for (int index = 0; index < link.Frames.Count - 1; index++)
									{
										int frame = link.Frames[index];
										int frame2 = link.Frames[index + 1];
										smallerLinks.Add([frame, frame2]);
									}

									value.RemoveLink(link);

									foreach (IEnumerable<int> framesToLink in smallerLinks)
									{
										value.AddLink(new KeyframeLink(value, framesToLink));
									}
								}
							}
						}
					}
					ImGui.EndPopup();
				}
			}

			ImGui.EndChild();
		}

		private static void DrawUiHierarchyFrame()
		{
			NVector2 itemSpacing = ImGui.GetStyle().ItemSpacing + NVector2.UnitY * 8;
			ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);

			ImGui.BeginChild("Project objects", new NVector2(WindowWidth, 0), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeY);
			{
				ImGui.Text($"{IcoMoon.ListIcon} Hierarchy");

				bool hoveredItem = false;

				hoveredItem |= DrawGraphicEntitiesHierarchy();

				hoveredItem |= DrawHitboxEntitiesHierarchy();

				DrawTextureHierarchy();

				bool popupOpen = true;

				if (objectToRename is not null && ImGui.BeginPopupModal("Rename object", ref popupOpen, ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
				{
					ImGui.SetWindowSize(new NVector2(400, 100));
					string input = SavedInput("Nuevo nombre:", string.Empty, out _);

					ImGui.SetNextItemShortcut(ImGuiKey.Enter);

					if (ImGui.Button("Renombrar"))
					{
						if (EditorApplication.selectedData.Type == SelectionType.Hitbox)
						{
							EditorApplication.RenameEntity(objectToRename, input);
							objectToRename = null;
						}

						ImGui.CloseCurrentPopup();
					}

					ImGui.SetNextItemShortcut(ImGuiKey.Escape);
					ImGui.SameLine();

					if (ImGui.Button("Cancelar"))
					{
						ImGui.CloseCurrentPopup();
						objectToRename = null;
					}

					ImGui.EndPopup();
				}

				if (!hoveredItem)
					EditorApplication.hoveredEntityName = string.Empty;

				ImGui.Unindent();
			}

			ImGui.EndChild();
			ImGui.PopStyleVar();
		}

		private static void DrawTextureHierarchy()
		{
			// Add textures
			ImGui.AlignTextToFramePadding();
			ImGui.Text($"{IcoMoon.TextureIcon} Textures");
			ImGui.SameLine();

			if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##CreateTexture"))
			{
				OpenFdDefinition = CreateFilePickerDefinition("Seleccionar textura", null, "*.png");

				ImGui.OpenPopup("Load texture");
			}

			DoPopup("Load texture", ref OpenFdDefinition, path =>
			{
				string name = Path.GetFileNameWithoutExtension(path);

				while (EditorApplication.State.Textures.ContainsKey(name))
				{
					name += Random.Shared.Next(0, 10);
				}

				TextureFrame frame = new TextureFrame(name, path);

				EditorApplication.State.Textures[name] = frame;

				EditorApplication.selectedData.Set(frame);
			});

			// show all loaded textures
			ImGui.Indent();

			for (int i = 0; i < EditorApplication.State.Textures.Count; i++)
			{
				TextureFrame texture = EditorApplication.State.Textures.Values.ToArray()[i];
				bool selected = EditorApplication.selectedData.IsOnlyThis(texture);

				if (ImGui.Selectable(texture.Name + "##texture", ref selected))
				{
					EditorApplication.selectedData.Set(texture);
				}

				if (ImGui.BeginPopupContextItem(texture.Name, ImGuiPopupFlags.NoReopen | ImGuiPopupFlags.MouseButtonRight))
				{
					if (ImGui.Button($"{IcoMoon.MinusIcon} Borrar"))
					{
						texture.Remove();

						ImGui.CloseCurrentPopup();
						ImGui.EndPopup();
					}

					else if (ImGui.Button($"{IcoMoon.HammerIcon} Renombrar"))
					{
						ResetSavedInput(texture.Name);
						ImGui.CloseCurrentPopup();
						ImGui.EndPopup();

						ImGui.OpenPopup("Renombrar textura");
					}

					else if (ImGui.Button($"{IcoMoon.FolderOpenIcon} Duplicar"))
					{
						string name = texture.Name;
						TextureFrame textureToDuplicate = EditorApplication.State.Textures[name];

						while (EditorApplication.State.Textures.ContainsKey(name))
						{
							name += Random.Shared.Next(0, 10);
						}

						TextureFrame duplicatedFrame = new TextureFrame(name, textureToDuplicate.Path,
							textureToDuplicate.FrameSize,
							textureToDuplicate.FramePosition,
							textureToDuplicate.Pivot);

						EditorApplication.State.Textures[name] = duplicatedFrame;

						EditorApplication.selectedData.Set(duplicatedFrame);

						ImGui.CloseCurrentPopup();
						ImGui.EndPopup();
					}
					else
						ImGui.EndPopup();
				}
			}

			bool popupOpen = true;

			if (ImGui.BeginPopupModal("Renombrar textura", ref popupOpen, ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
			{
				ImGui.SetWindowSize(new NVector2(400, 100));
				string input = SavedInput("Nuevo nombre:", string.Empty, out _);

				ImGui.SetNextItemShortcut(ImGuiKey.Enter);

				if (ImGui.Button("Renombrar"))
				{
					if (EditorApplication.selectedData.Type == SelectionType.Texture)
					{
						TextureFrame frame = (TextureFrame)EditorApplication.selectedData.GetLoneObject();
						EditorApplication.RenameTexture(frame, input);
					}

					ImGui.CloseCurrentPopup();
				}

				ImGui.SetNextItemShortcut(ImGuiKey.Escape);
				ImGui.SameLine();

				if (ImGui.Button("Cancelar"))
				{
					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}
		}

		private static bool DrawHitboxEntitiesHierarchy()
		{
			ImGui.BeginDisabled(!Timeline.HitboxMode);
			bool itemHovered = false;

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
					HitboxAnimationObject hitboxAnimationObject = new HitboxAnimationObject(entityName);
					hitboxAnimationObject.SpawnFrame = (ushort)EditorApplication.State.Animator.CurrentKeyframe;

					EditorApplication.State.HitboxEntities[entityName] = hitboxAnimationObject;

					EditorApplication.selectedData.Set(hitboxAnimationObject);

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

			bool changedSelectedObject = false;

			for (int i = 0; i < EditorApplication.State.HitboxEntities.Count; i++)
			{
				HitboxAnimationObject entity = EditorApplication.State.HitboxEntities.Values.ToArray()[i];
				bool selected = EditorApplication.selectedData.Contains(entity);

				if (ImGui.Selectable(entity.Name + "##hitbox", ref selected) && selected)
				{
					changedSelectedObject = !EditorApplication.selectedData.IsOnlyThis(entity);
					EditorApplication.selectedData.SetOrAdd(entity);
				}

				if (ImGui.BeginPopupContextItem())
				{
					if (ImGui.Button($"{IcoMoon.MinusIcon} Remove"))
					{
						EditorApplication.State.HitboxEntities.Remove(entity.Name);
						if (EditorApplication.selectedData.Contains(entity))
							EditorApplication.selectedData.Deselect(entity);

						continue;
					}

					if (ImGui.Button($"{IcoMoon.FolderOpenIcon} Duplicar"))
					{
						string name = entity.Name;

						while (EditorApplication.State.HitboxEntities.ContainsKey(name))
						{
							name += Random.Shared.Next(0, 10);
						}

						HitboxAnimationObject newObject = new HitboxAnimationObject(entity);
						newObject.Name = name;

						EditorApplication.State.HitboxEntities.Add(name, newObject);
						EditorApplication.selectedData.Set(newObject);

						ImGui.CloseCurrentPopup();

						continue;
					}

					if (ImGui.Button($"{IcoMoon.HammerIcon} Renombrar"))
					{
						ResetSavedInput(entity.Name);
						ImGui.CloseCurrentPopup();
						ImGui.EndPopup();

						OpenRenamePopup(entity);

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

			ImGui.EndDisabled();

			return itemHovered;
		}

		public static void OpenRenamePopup(IAnimationObject obj)
		{
			objectToRename = obj;
		}

		private static bool DrawGraphicEntitiesHierarchy()
		{
			ImGui.BeginDisabled(EditorApplication.State.Textures.Count == 0 || Timeline.HitboxMode);

			bool itemHovered = false;
			ImGui.AlignTextToFramePadding();
			ImGui.Text($"{IcoMoon.ImagesIcon} Entities");
			ImGui.SameLine();

			// create entity

			if (ImGui.SmallButton($"{IcoMoon.PlusIcon}##CreateEntity"))
			{
				ImGui.OpenPopup("Create entity");
				DoEntityCreatorReset();
			}

			DoEntityCreatorModal((name, selectedTexture) =>
			{
				TextureAnimationObject textureAnimationObject = new TextureAnimationObject(name, selectedTexture);

				EditorApplication.State.GraphicEntities[name] = textureAnimationObject;

				EditorApplication.selectedData.Set(textureAnimationObject);
			});

			// show all created entities
			ImGui.Indent();
			bool changedSelectedObject = false;

			for (int i = 0; i < EditorApplication.State.GraphicEntities.Count; i++)
			{
				TextureAnimationObject entity = EditorApplication.State.GraphicEntities.Values.ToArray()[i];
				bool selected = EditorApplication.selectedData.Contains(entity);

				if (ImGui.Selectable(entity.Name + "##graphic", ref selected) && selected)
				{
					changedSelectedObject = !EditorApplication.selectedData.IsOnlyThis(entity);
					EditorApplication.selectedData.SetOrAdd(entity);
				}

				if (ImGui.BeginPopupContextItem())
				{
					if (ImGui.Button($"{IcoMoon.MinusIcon} Remove"))
					{
						EditorApplication.State.GraphicEntities.Remove(entity.Name);
						if (EditorApplication.selectedData.Contains(entity))
							EditorApplication.selectedData.Deselect(entity);

						continue;
					}

					if (ImGui.Button($"{IcoMoon.FolderOpenIcon} Duplicar"))
					{
						string name = entity.Name;

						while (EditorApplication.State.GraphicEntities.ContainsKey(name))
						{
							name += Random.Shared.Next(0, 10);
						}

						TextureAnimationObject newObject = new TextureAnimationObject(name, entity.TextureName);

						List<KeyframeableValue> newEntityValues = newObject.EnumerateKeyframeableValues();
						List<KeyframeableValue> originalEntityValues = entity.EnumerateKeyframeableValues();

						for (int index = 0; index < newEntityValues.Count; index++)
						{
							newEntityValues[index].CloneKeyframeDataFrom(originalEntityValues[index]);
						}

						EditorApplication.State.GraphicEntities.Add(name, newObject);
						EditorApplication.selectedData.Set(newObject);

						ImGui.CloseCurrentPopup();

						continue;
					}

					ImGui.EndPopup();
				}

				if (!ImGui.IsItemHovered())
					continue;

				itemHovered = true;
				EditorApplication.hoveredEntityName = entity.Name;
			}

			ImGui.EndDisabled();

			if (changedSelectedObject)
			{
				ResetSavedInput();
			}

			ImGui.Unindent();

			return itemHovered;
		}

		private static void DrawUiProperties()
		{
			ImGui.BeginChild("Selected Entity Properties", new NVector2(WindowWidth, 0), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.ResizeY, ImGuiWindowFlags.AlwaysAutoResize);
			ImGui.Text($"{IcoMoon.EqualizerIcon} Properties");
			bool isSelectedDataValid = EditorApplication.selectedData.IsLone();
			GCHandle handle;

			if (!isSelectedDataValid)
			{
				ImGui.EndChild();

				return;
			}

			switch (EditorApplication.selectedData.Type)
			{
				case SelectionType.Graphic:
					TextureAnimationObject textureObject = (TextureAnimationObject)EditorApplication.selectedData.GetLoneObject();

					string tempEntityName = SavedInput(string.Empty, textureObject.Name, out _);
					ImGui.SameLine();

					if (ImGui.Button("Renombrar") && !EditorApplication.State.GraphicEntities.ContainsKey(tempEntityName))
					{
						EditorApplication.RenameEntity(textureObject, tempEntityName);
						ResetSavedInput();
					}

					if (ImGui.Button("Seleccionar otra textura"))
					{
						selectedTextureOnEntityCreator = -1;
						ImGui.OpenPopup("Cambiar grafico de entidad");
						ResetSavedInput();
					}

					if (ImGui.Button("Seleccionar textura"))
					{
						EditorApplication.selectedData.Set(EditorApplication.State.Textures[textureObject.TextureName]);
					}

					if (ImGui.BeginPopupModal("Cambiar grafico de entidad", ImGuiWindowFlags.AlwaysAutoResize))
					{
						string[] textureNames = EditorApplication.State.Textures.Keys.ToArray();

						if (ImGui.BeginListBox("Texturas\ndisponibles\n!!!"))
						{
							for (int j = 0; j < textureNames.Length; j++)
							{
								bool selected = false;
								string textureName = textureNames[j];

								if (ImGui.Selectable(textureName + "##select", ref selected, ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
								{
									selectedTextureOnEntityCreator = j;

									textureObject.TextureName = textureName;

									ImGui.CloseCurrentPopup();

									break;
								}
							}

							ImGui.EndListBox();
						}

						if (selectedTextureOnEntityCreator >= 0 && selectedTextureOnEntityCreator < textureNames.Length && ImGui.Button("Cambiar textura##popup"))
						{
							textureObject.TextureName = textureNames[selectedTextureOnEntityCreator];
						}

						ImGui.SetNextItemShortcut(ImGuiKey.Escape);

						if (ImGui.Button("Cancelar"))
						{
							ImGui.CloseCurrentPopup();
						}

						ImGui.EndPopup();
					}

					ImGui.Separator();

					ImGui.Columns(2);
					ImGui.SetColumnWidth(0, 28);

					ImGui.NextColumn();
					ImGui.Text("All properties");
					ImGui.Separator();
					ImGui.NextColumn();

					int keyframeButtonId = 0;

					foreach (KeyframeableValue keyframeableValue in textureObject.EnumerateKeyframeableValues())
					{
						ImGui.PushID(keyframeButtonId++);

						if (ImGui.Button($"{IcoMoon.KeyIcon}"))
						{
							keyframeableValue.SetKeyframeValue(null, keyframeableValue.cachedValue.value);
						}

						ImGui.PopID();

						ImGui.NextColumn();

						switch (keyframeableValue.Name)
						{
							case PropertyNames.ScaleProperty:
							case PropertyNames.PositionProperty:
								NVector2 vector2 = ((Vector2KeyframeValue)keyframeableValue).CachedValue.ToNumerics();

								if (ImGui.DragFloat2(keyframeableValue.Name, ref vector2))
									keyframeableValue.SetKeyframeValue(null, (Vector2)vector2);

								break;
							case PropertyNames.FrameIndexProperty:
								int frameIndex = ((IntKeyframeValue)keyframeableValue).CachedValue;

								TextureFrame texture = EditorApplication.State.GetTexture(textureObject.TextureName);
								int framesX = (texture.Width - texture.FramePosition.X) / texture.FrameSize.X;
								int framesY = (texture.Height - texture.FramePosition.Y) / texture.FrameSize.Y;

								if (ImGui.SliderInt(keyframeableValue.Name, ref frameIndex, 0, framesX * framesY - 1))
									keyframeableValue.SetKeyframeValue(null, frameIndex);

								break;
							case PropertyNames.RotationProperty:
								float rotation = ((FloatKeyframeValue)keyframeableValue).CachedValue;
								rotation = MathHelper.ToDegrees(rotation);

								if (ImGui.DragFloat(keyframeableValue.Name, ref rotation, 1f, float.MinValue, float.MaxValue, "%.0f deg", ImGuiSliderFlags.NoRoundToFormat))
									keyframeableValue.SetKeyframeValue(null, MathHelper.ToRadians(rotation));

								break;
							case PropertyNames.TransparencyProperty:
								float transparency = ((FloatKeyframeValue)keyframeableValue).CachedValue;

								if (ImGui.DragFloat(keyframeableValue.Name, ref transparency, 0.001f, 0, 1f, "%f", ImGuiSliderFlags.NoRoundToFormat))
									keyframeableValue.SetKeyframeValue(null, transparency);

								break;
							case PropertyNames.ZIndexProperty:
								float zIndex = ((FloatKeyframeValue)keyframeableValue).CachedValue;

								ImGui.SetNextItemWidth(125);

								if (ImGui.DragFloat("##" + keyframeableValue.Name, ref zIndex, 0.01f, 0, float.MaxValue, "%f", ImGuiSliderFlags.AlwaysClamp))
								{
									keyframeableValue.SetKeyframeValue(null, zIndex);
								}

								ImGui.SameLine();

								if (ImGui.Button("+"))
								{
									keyframeableValue.SetKeyframeValue(null, Math.Min(1, zIndex + 0.01f));
								}

								ImGui.SameLine();

								if (ImGui.Button("-"))
								{
									keyframeableValue.SetKeyframeValue(null, Math.Max(0, zIndex - 0.01f));
								}

								ImGui.SameLine();
								ImGui.Text("Z Index");

								break;
						}

						ImGui.NextColumn();
					}

					break;
				case SelectionType.Hitbox:
					HitboxAnimationObject hitboxObject = (HitboxAnimationObject)EditorApplication.selectedData.GetLoneObject();

					ImGui.Columns(2);
					ImGui.SetColumnWidth(0, 28);

					ImGui.NextColumn();
					ImGui.Text("All properties");
					ImGui.Separator();
					ImGui.NextColumn();

					keyframeButtonId = 0;

					foreach (KeyframeableValue keyframeableValue in hitboxObject.EnumerateKeyframeableValues())
					{
						ImGui.PushID(keyframeButtonId++);

						if (ImGui.Button($"{IcoMoon.KeyIcon}"))
						{
							keyframeableValue.SetKeyframeValue(null, keyframeableValue.cachedValue.value);
						}

						ImGui.PopID();

						ImGui.NextColumn();

						switch (keyframeableValue.Name)
						{
							case PropertyNames.SizeProperty:
							case PropertyNames.PositionProperty:
								NVector2 vector2 = ((Vector2KeyframeValue)keyframeableValue).CachedValue.ToNumerics();

								if (ImGui.DragFloat2(keyframeableValue.Name, ref vector2))
									keyframeableValue.SetKeyframeValue(null, (Vector2)vector2);

								break;
						}

						ImGui.NextColumn();
					}

					ImGui.Columns(1);

					ImGui.PushItemWidth(WindowWidth * 0.35f);
					DragUshort("Frame start", ref hitboxObject.SpawnFrame);
					DragUshort("Frame duration", ref hitboxObject.FrameDuration);

					ImGui.SeparatorText("Propiedades");

					string[] names = Enum.GetNames(typeof(HitboxType));

					int index = (int)hitboxObject.Type;

					if (ImGui.Combo("Hitbox type", ref index, names, names.Length))
					{
						hitboxObject.Type = (HitboxType)index;
					}

					ImGui.SeparatorText("Caracteristicas de golpe");

					index = (int)hitboxObject.Conditions;
					ImGui.CheckboxFlags("Unblockable", ref index, (int)HitboxConditions.Unblockable);
					ImGui.CheckboxFlags("Grazable", ref index, (int)HitboxConditions.CanBeGrazed);
					ImGui.CheckboxFlags("Only hits aerial", ref index, (int)HitboxConditions.AerialOnly);
					ImGui.CheckboxFlags("Only hits grounded", ref index, (int)HitboxConditions.GroundedOnly);

					ImGui.SeparatorText("Animacion al golpear");

					names = Enum.GetNames(typeof(LaunchType));

					index = (int)hitboxObject.LaunchType;

					if (ImGui.Combo("Launch type", ref index, names, names.Length))
					{
						hitboxObject.LaunchType = (LaunchType)index;
					}

					ImGui.SeparatorText("Datos");

					switch (hitboxObject.Type)
					{
						case HitboxType.Hitbox:
							ImGui.DragFloat("Damage", ref hitboxObject.Damage);
							ImGui.DragFloat("Rate", ref hitboxObject.Rate);

							DragUshort("Hitstun", ref hitboxObject.Hitstun);
							ImGui.DragFloat("Hitstun Growth", ref hitboxObject.HitstunGrowth);
							DragUshort("MaxHitstun", ref hitboxObject.MaxHitstun);

							DragAngleWithWidget("Launch Angle", ref hitboxObject.LaunchAngle, f => hitboxObject.LaunchAngle = f);
							ImGui.DragFloat("Launch Potency", ref hitboxObject.LaunchPotency);
							ImGui.DragFloat("Launch Potency Growth", ref hitboxObject.LaunchPotencyGrowth);
							ImGui.DragFloat("Launch Potency Max", ref hitboxObject.LaunchPotencyMax);

							DragUshort("ShieldStun", ref hitboxObject.ShieldStun);
							DragAngleWithWidget("Shield Launch Angle", ref hitboxObject.ShieldLaunchAngle, f => hitboxObject.ShieldLaunchAngle = f);
							ImGui.DragFloat("ShieldPotency", ref hitboxObject.ShieldPotency);

							DragUshort("DuelGameLag", ref hitboxObject.DuelGameLag);
							DragUshort("AttackId", ref hitboxObject.AttackId);
							DragUshort("ImmunityAfterHit", ref hitboxObject.ImmunityAfterHit);

							break;
						case HitboxType.Windbox:

							DragAngleWithWidget("Launch Angle", ref hitboxObject.LaunchAngle, f => hitboxObject.LaunchAngle = f);
							ImGui.DragFloat("Launch Potency", ref hitboxObject.LaunchPotency);
							ImGui.DragFloat("Launch Potency Growth", ref hitboxObject.LaunchPotencyGrowth);
							ImGui.DragFloat("Launch Potency Max", ref hitboxObject.LaunchPotencyMax);

							break;
					}

					DragUshort("Priority", ref hitboxObject.Priority);

					ImGui.PopItemWidth();

					break;
				case SelectionType.Texture:
				{
					TextureFrame selectedTexture = (TextureFrame)EditorApplication.selectedData.GetLoneObject();
					Point currentFrameSize = selectedTexture.FrameSize;
					Point currentFramePosition = selectedTexture.FramePosition;
					NVector2 currentPivot = selectedTexture.Pivot;

					unsafe
					{
						ImGui.Text("Frame position");
						handle = GCHandle.Alloc(currentFramePosition, GCHandleType.Pinned); // why isnt imgui.dragint2 using ref Point as a parameter :(((((((((
						ImGui.DragScalarN("##Frame position slider", ImGuiDataType.S32, handle.AddrOfPinnedObject(), 2, 0.5f);
						currentFramePosition.X = ((int*)handle.AddrOfPinnedObject().ToPointer())[0];
						currentFramePosition.Y = ((int*)handle.AddrOfPinnedObject().ToPointer())[1];
						handle.Free();

						ImGui.Text("Frame size");
						handle = GCHandle.Alloc(currentFrameSize, GCHandleType.Pinned); // why isnt imgui.dragint2 using ref Point as a parameter :(((((((((
						ImGui.DragScalarN("##Frame size slider", ImGuiDataType.S32, handle.AddrOfPinnedObject(), 2, 0.5f);
						currentFrameSize.X = ((int*)handle.AddrOfPinnedObject().ToPointer())[0];
						currentFrameSize.Y = ((int*)handle.AddrOfPinnedObject().ToPointer())[1];
						handle.Free();
					}

					ImGui.BeginGroup();
					ImGui.Text("Pivot");
					ImGui.DragFloat2("##Pivot", ref currentPivot);

					if (ImGui.Button("Center pivot to current frame"))
					{
						currentPivot = new NVector2(currentFrameSize.X / 2f, currentFrameSize.Y / 2f);
					}

					if (ImGui.Button("Reset all texture settings\nand data"))
					{
						currentPivot = new NVector2(selectedTexture.Width / 2f, selectedTexture.Height / 2f);
						currentFrameSize = selectedTexture.Size;
						currentFramePosition = Point.Zero;

						PivotViewerZoom = 1f;
						PivotViewerOffset = Vector2.Zero;
					}

					ImGui.EndGroup();

					selectedTexture.FramePosition = currentFramePosition;
					selectedTexture.FrameSize = currentFrameSize;
					selectedTexture.Pivot = currentPivot;

					ImGui.SetNextItemWidth(60);

					float oldZoom = PivotViewerZoom;

					if (ImGui.DragFloat("Zoom", ref PivotViewerZoom, 0.1f, 0.1f, 10f))
					{
						PivotViewerOffset += PivotViewerOffset / oldZoom - PivotViewerOffset / PivotViewerZoom;
					}

					NVector2 scaledFramePosition = new NVector2(currentFramePosition.X * PivotViewerZoom, currentFramePosition.Y * PivotViewerZoom);
					NVector2 scaledFrameSize = new NVector2(currentFrameSize.X * PivotViewerZoom, currentFrameSize.Y * PivotViewerZoom);
					NVector2 scaledPivot = currentPivot * PivotViewerZoom;

					ImGui.BeginChild("Pivot renderer", new NVector2(WindowWidth * 0.7f), ImGuiChildFlags.FrameStyle);
					{
						if (ImGui.IsWindowHovered())
						{
							ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY); // this doesnt work :(

							if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
							{
								PivotViewerOffset += Input.MousePosDelta * PivotViewerZoom;
							}
						}

						NVector2 effectiveOffset = PivotViewerOffset.ToNumerics() / PivotViewerZoom;

						NVector2 contentSize = new NVector2(WindowWidth * 0.7f);
						NVector2 cursorPos = ImGui.GetCursorScreenPos();
						NVector2 center = cursorPos + contentSize * 0.5f + effectiveOffset;
						NVector2 frameStart = center - scaledFrameSize * 0.5f;

						ImDrawListPtr drawList = ImGui.GetWindowDrawList();

						// draw texture for reference
						NVector2 size = selectedTexture.Size.ToVector2().ToNumerics();
						NVector2 scaledSize = size * PivotViewerZoom;

						NVector2 relativeTopLeft = -scaledFramePosition;
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

						drawList.AddImage(selectedTexture.TextureId,
							frameStart + relativeTopLeft,
							frameStart + relativeBottomRight, NVector2.Zero, NVector2.One, Color.DimGray.PackedValue);

						drawList.AddImage(selectedTexture.TextureId,
							frameStart + textureInFrameTopLeft,
							frameStart + textureInFrameBottomRight, uvMin, uvMax);

						if (PivotViewerZoom > 2.5f)
						{
							// TODO draw t h e  g r i d 
							// drawList.AddLine();
						}

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
					FileInfo writtenFileInfo = new FileInfo(Path.Combine(fpd.CurrentFolderPath, fileName));

					if (writtenFileInfo.Exists)
					{
						fpd.SelectFile(writtenFileInfo);
					}
					else if (fpd.DefaultExtensionOnCreate is not null)
					{
						writtenFileInfo = new FileInfo(Path.Combine(fpd.CurrentFolderPath, Path.ChangeExtension(fileName, fpd.DefaultExtensionOnCreate)));
						fpd.CreateDummyFile(writtenFileInfo);
					}
				}

				ImGui.SetNextItemShortcut(ImGuiKey.Escape);

				if (ImGui.Button("Cancel"))
				{
					result = false;
					ImGui.CloseCurrentPopup();
				}

				if (fpd.SelectedFileInfo != null)
				{
					if (fpd.SelectedFileInfo.Exists)
					{
						ImGui.SameLine();

						if (ImGui.Button(fpd.ActionButtonLabel))
						{
							result = true;
							ImGui.CloseCurrentPopup();
						}
					}
					else if (fpd.DefaultExtensionOnCreate is not null)
					{
						ImGui.SameLine();

						if (ImGui.Button("Create!"))
						{
							fpd.SelectedFileInfo.Create().Dispose();
							result = true;
							ImGui.CloseCurrentPopup();
						}
					}
				}

				if (result)
					onDone?.Invoke(fpd.SelectedFileFullPath);

				ImGui.EndPopup();
			}
		}
	}
}