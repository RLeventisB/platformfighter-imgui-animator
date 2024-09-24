using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Editor.Gui
{
	public static partial class ImGuiEx
	{
		public struct FilePickerDefinition
		{
			private DirectoryInfo _currentFolderInfo = null;
			public string ActionButtonLabel;
			public readonly string StartingFolder;
			public string FolderModifications;
			public string AssemblyPath;
			public string SelectedFileName;
			public string SearchFilter;
			public FileInfo SelectedFileInfo;
			public List<FileInfo> FolderFiles = new List<FileInfo>();
			public List<DirectoryInfo> FolderFolders = new List<DirectoryInfo>(); // lol

			public FilePickerDefinition(string actionButtonLabel, string searchFilter)
			{
				ActionButtonLabel = actionButtonLabel;
				SearchFilter = searchFilter;
				StartingFolder = AppContext.BaseDirectory;
				FolderModifications = ".";
				AssemblyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
				SelectFolder(new DirectoryInfo(StartingFolder));
			}

			public void SelectFolder(DirectoryInfo directoryInfo)
			{
				if (directoryInfo == null || !directoryInfo.Exists)
					return;

				_currentFolderInfo = directoryInfo;
				FolderFiles = _currentFolderInfo.GetFiles(SearchFilter).ToList();
				FolderFolders = _currentFolderInfo.GetDirectories("*.*").ToList();
			}

			public void SelectFile(FileInfo fileInfo)
			{
				if (fileInfo is null)
					return;

				SelectedFileInfo = fileInfo;
				SelectedFileName = fileInfo.Name;
			}

			public string SelectedFileFullPath => SelectedFileInfo.FullName;
			public DirectoryInfo CurrentFolderInfo => _currentFolderInfo;
			public string CurrentFolderPath => _currentFolderInfo.FullName;
		}

		public static FilePickerDefinition CreateFilePickerDefinition(string actionLabel, string searchFilter = "*.*")
		{
			FilePickerDefinition fp = new FilePickerDefinition(actionLabel, searchFilter);

			return fp;
		}

		/*public static bool DoFilePicker(ref FilePickerDefinition fpDef)
		{
			ImGui.Text("Selecciona arhchivos.,");
			bool result = false;

			NVector2 ch = ImGui.GetContentRegionAvail();
			float frameHeight = ch.Y - (ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().WindowPadding.Y * 3.5f);

			if (ImGui.BeginChild("##Directory Viewer", new NVector2(0, frameHeight), ImGuiChildFlags.FrameStyle,
				ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.NoResize))
			{
				DirectoryInfo di = new DirectoryInfo(fpDef.FolderModifications);

				if (di.Exists)
				{
					if (di.Parent != null && fpDef.FolderModifications != fpDef.StartingFolder)
					{
						ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow.PackedValue);
						if (ImGui.Selectable("../", false, ImGuiSelectableFlags.NoAutoClosePopups))
							fpDef.FolderModifications = di.Parent.FullName;

						ImGui.PopStyleColor();
					}

					List<string> fileSystemEntries = GetFileSystemEntries(ref fpDef, di.FullName);

					foreach (string fse in fileSystemEntries)
					{
						if (Directory.Exists(fse))
						{
							string name = Path.GetFileName(fse);
							ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow.PackedValue);
							if (ImGui.Selectable(name + "/", false, ImGuiSelectableFlags.NoAutoClosePopups))
								fpDef.FolderModifications = fse;

							ImGui.PopStyleColor();
						}
						else
						{
							string name = Path.GetFileName(fse);
							bool isSelected = fpDef.SelectedAbsolutePath == fse;
							if (ImGui.Selectable(name, isSelected, ImGuiSelectableFlags.NoAutoClosePopups))
								fpDef.SelectedAbsolutePath = fse;

							if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && ImGui.IsItemHovered())
							{
								result = true;
								ImGui.CloseCurrentPopup();
							}
						}
					}
				}
			}

			ImGui.EndChild();

			if (fpDef.OnlyAllowFolders)
			{
				fpDef.SelectedAbsolutePath = fpDef.FolderModifications;
				fpDef.SelectedRelativePath = fpDef.SelectedAbsolutePath.Substring(fpDef.AssemblyPath.Length + 1);
			}
			else
			{
				if (!string.IsNullOrEmpty(fpDef.SelectedAbsolutePath))
				{
					fpDef.SelectedRelativePath = fpDef.SelectedAbsolutePath.Substring(fpDef.AssemblyPath.Length + 1);
					fpDef.SelectedFileName = Path.GetFileName(fpDef.SelectedAbsolutePath);
				}

				ImGui.SetNextItemWidth(ch.X);
				string fileName = fpDef.SelectedFileName ?? string.Empty;
				ImGui.InputText(string.Empty, ref fileName, 64);

				if (!string.IsNullOrEmpty(fileName))
				{
					fpDef.SelectedAbsolutePath = Path.Combine(fpDef.FolderModifications, fileName);
					fpDef.SelectedRelativePath = Path.Combine(Path.GetDirectoryName(fpDef.SelectedRelativePath ?? fpDef.FolderModifications), fileName);
				}
			}

			if (ImGui.Button("Cancel") || ImGui.IsKeyPressed(ImGuiKey.Escape))
			{
				result = false;
				ImGui.CloseCurrentPopup();
			}

			if (fpDef.SelectedAbsolutePath != null)
			{
				ImGui.SameLine();

				if (ImGui.Button(fpDef.ActionButtonLabel))
				{
					result = true;
					ImGui.CloseCurrentPopup();
				}
			}

			return result;
		}

		private static List<string> GetFileSystemEntries(ref FilePickerDefinition fpDef, string fullName)
		{
			List<string> files = new List<string>();
			List<string> dirs = new List<string>();

			foreach (string fse in Directory.GetFileSystemEntries(fullName, ""))
			{
				if (Directory.Exists(fse))
				{
					dirs.Add(fse);
				}
				else if (!fpDef.OnlyAllowFolders)
				{
					if (fpDef.AllowedExtensions != null)
					{
						string ext = Path.GetExtension(fse);
						if (fpDef.AllowedExtensions.Contains(ext))
							files.Add(fse);
					}
					else
					{
						files.Add(fse);
					}
				}
			}

			List<string> ret = new List<string>(dirs);
			ret.AddRange(files);

			return ret;
		}*/
	}
}