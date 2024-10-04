using Editor.Objects;

using ImGuiNET;

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Editor
{
	public record SelectionData : IEnumerable<SelectedObject>
	{
		private readonly List<SelectedObject> _selectedObjects = new List<SelectedObject>();
		public SelectionType Type = SelectionType.None;

		public void AddToSelection(IAnimationObject obj)
		{
			SelectionType typeOfObject = GetTypeOfObject(obj);

			if (typeOfObject != Type)
			{
				Empty();
				Type = typeOfObject;
			}

			_selectedObjects.Add(new SelectedObject(obj));
		}

		public void Set(IAnimationObject obj)
		{
			Empty();
			Type = GetTypeOfObject(obj);
			_selectedObjects.Add(new SelectedObject(obj));
		}

		public bool IsOnlyThis(IAnimationObject obj)
		{
			return IsLone() && GetLoneData().IsOf(obj);
		}

		public bool SetOrAdd(IAnimationObject obj)
		{
			if (ImGui.IsKeyDown(ImGuiKey.ModCtrl))
			{
				AddToSelection(obj);

				return true;
			}

			Set(obj);

			return false;
		}

		public bool IsLone()
		{
			return _selectedObjects.Count == 1;
		}

		public SelectedObject GetLoneData()
		{
			return _selectedObjects[0];
		}

		public IAnimationObject GetLoneObject()
		{
			return _selectedObjects[0].AnimationObject;
		}

		public int Count => _selectedObjects.Count;

		public bool Deselect(IAnimationObject obj)
		{
			int index = _selectedObjects.FindIndex(v => v.IsOf(obj));

			if (index < 0 || index >= _selectedObjects.Count)
				return false;

			_selectedObjects.RemoveAt(index);
			if (_selectedObjects.Count == 0)
				Type = SelectionType.None;

			return true;
		}

		public bool Contains(IAnimationObject obj)
		{
			return _selectedObjects.Any(v => v.IsOf(obj));
		}

		public void Empty()
		{
			Type = SelectionType.None;
			_selectedObjects.Clear();
		}

		public bool IsEmpty()
		{
			return _selectedObjects.Count == 0;
		}

		public IEnumerator<SelectedObject> GetEnumerator() => _selectedObjects.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public static SelectionType GetTypeOfObject(IAnimationObject animationObject)
		{
			return animationObject switch
			{
				TextureAnimationObject => SelectionType.Graphic,
				TextureFrame => SelectionType.Texture,
				_ => SelectionType.Hitbox
			};
		}
	}
	public record SelectedObject(string Name, IAnimationObject AnimationObject)
	{
		public SelectedObject(IAnimationObject animationObject) : this(animationObject.Name, animationObject)
		{
		}

		public bool IsOf(IAnimationObject animationObject)
		{
			return ReferenceEquals(animationObject, AnimationObject);
		}
	}

	public enum SelectionType
	{
		Texture, Graphic, Hitbox, None
	}
}