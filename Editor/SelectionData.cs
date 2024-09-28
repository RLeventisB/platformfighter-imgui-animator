using Editor.Gui;
using Editor.Model;

using System.Runtime.CompilerServices;

namespace Editor
{
	public record SelectionData
	{
		public SelectionData()
		{
			Empty();
		}

		public SelectionData(IAnimationObject animationObject) : this(animationObject.Name, animationObject)
		{
			Hierarchy.PivotViewerZoom = 1;
			Hierarchy.PivotViewerOffset = Vector2.Zero;
			ResetSavedInput();
		}

		public SelectionData(TextureFrame frame) : this(frame.Name, frame)
		{
		}

		public SelectionData(string name, IAnimationObject reference)
		{
			Name = name;
			Reference = reference;
			ObjectSelectionType = reference switch
			{
				TextureAnimationObject => SelectionType.Graphic,
				TextureFrame => SelectionType.Texture,
				_ => SelectionType.Hitbox
			};
		}

		public string Name { get; private set; }
		public IAnimationObject Reference { get; private set; }
		public SelectionType ObjectSelectionType { get; private set; }

		public void Deconstruct(out string name, out IAnimationObject reference)
		{
			name = Name;
			reference = Reference;
		}

		public bool IsOf(IAnimationObject obj)
		{
			return obj == Reference;
		}

		public void Empty()
		{
			Name = string.Empty;
			Reference = null;
			ObjectSelectionType = SelectionType.None;
		}

		public bool IsNotButSameType(TextureAnimationObject animationObject)
		{
			return ObjectSelectionType == SelectionType.Graphic && animationObject.Name != Name;
		}

		public bool IsNotButSameType(HitboxAnimationObject animationObject)
		{
			return ObjectSelectionType == SelectionType.Hitbox && animationObject.Name != Name;
		}

		public bool IsNotButSameType(TextureFrame entity)
		{
			return ObjectSelectionType == SelectionType.Texture && entity.Name != Name;
		}

		public bool GetValue(out IAnimationObject obj)
		{
			if (!string.IsNullOrEmpty(Name))
				switch (ObjectSelectionType)
				{
					case SelectionType.Graphic:
						bool exists = EditorApplication.State.GraphicEntities.TryGetValue(Name, out TextureAnimationObject textureEntity);
						obj = exists ? textureEntity : null;

						return exists;
					case SelectionType.Hitbox:
						exists = EditorApplication.State.HitboxEntities.TryGetValue(Name, out HitboxAnimationObject hitboxEntity);
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

		public bool TryGetValue<T>(out T animationObject) where T : class, IAnimationObject
		{
			if (!string.IsNullOrEmpty(Name) && Reference is not null)
				switch (ObjectSelectionType)
				{
					case SelectionType.Graphic:
						bool exists = EditorApplication.State.GraphicEntities.TryGetValue(Name, out TextureAnimationObject textureEntity);
						animationObject = exists ? Unsafe.As<T>(textureEntity) : null;

						if (!exists)
							Empty();

						return exists && typeof(T) == typeof(TextureAnimationObject);
					case SelectionType.Hitbox:
						exists = EditorApplication.State.HitboxEntities.TryGetValue(Name, out HitboxAnimationObject hitboxEntity);
						animationObject = exists ? Unsafe.As<T>(hitboxEntity) : null;

						if (!exists)
							Empty();

						return exists && typeof(T) == typeof(HitboxAnimationObject);
					case SelectionType.Texture:
						exists = EditorApplication.State.Textures.TryGetValue(Name, out TextureFrame textureFrame);
						animationObject = exists ? Unsafe.As<T>(textureFrame) : null;

						if (!exists)
							Empty();

						return exists && typeof(T) == typeof(TextureFrame);
				}

			animationObject = null;

			return false;
		}

		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(Name) || Reference is null;
		}
	}
	public enum SelectionType
	{
		Texture, Graphic, Hitbox, None
	}
}