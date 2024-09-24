using Editor.Gui;
using Editor.Model;

namespace Editor
{
	public record SelectionData
	{
		public SelectionData()
		{
			Empty();
		}

		public SelectionData(IEntity entity) : this(entity.Name, entity)
		{
			Hierarchy.PivotViewerZoom = 1;
			Hierarchy.PivotViewerOffset = Vector2.Zero;
			ResetSavedInput();
		}

		public SelectionData(TextureFrame frame) : this(frame.Name, frame)
		{
		}

		public SelectionData(string name, object reference)
		{
			Name = name;
			Reference = reference;
			ObjectSelectionType = reference switch
			{
				TextureEntity => SelectionType.Graphic,
				TextureFrame => SelectionType.Texture,
				_ => SelectionType.Hitbox
			};
		}

		public string Name { get; private set; }
		public object Reference { get; private set; }
		public SelectionType ObjectSelectionType { get; private set; }

		public void Deconstruct(out string name, out object reference)
		{
			name = Name;
			reference = Reference;
		}

		public bool IsOf(object obj)
		{
			return obj == Reference;
		}

		public void Empty()
		{
			Name = string.Empty;
			Reference = null;
			ObjectSelectionType = SelectionType.None;
		}

		public bool IsNotButSameType(TextureEntity entity)
		{
			return ObjectSelectionType == SelectionType.Graphic && entity.Name != Name;
		}

		public bool IsNotButSameType(HitboxEntity entity)
		{
			return ObjectSelectionType == SelectionType.Hitbox && entity.Name != Name;
		}

		public bool IsNotButSameType(TextureFrame entity)
		{
			return ObjectSelectionType == SelectionType.Texture && entity.Name != Name;
		}

		public bool GetValue(out object obj)
		{
			if (!string.IsNullOrEmpty(Name))
				switch (ObjectSelectionType)
				{
					case SelectionType.Graphic:
						bool exists = EditorApplication.State.GraphicEntities.TryGetValue(Name, out TextureEntity textureEntity);
						obj = exists ? textureEntity : null;

						return exists;
					case SelectionType.Hitbox:
						exists = EditorApplication.State.HitboxEntities.TryGetValue(Name, out HitboxEntity hitboxEntity);
						obj = exists ? hitboxEntity : null;

						return exists;
					case SelectionType.Texture:
						exists = EditorApplication.State.Textures.TryGetValue(Name, out TextureFrame textureFrame);
						obj = exists ? textureFrame : null;

						return exists;
				}

			obj = null;

			return false;
		}
	}
	public enum SelectionType
	{
		Texture, Graphic, Hitbox, None
	}
}