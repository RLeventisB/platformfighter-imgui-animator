using Editor.Gui;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;

namespace Editor.Graphics
{
	public static class Camera
	{
		public static bool isViewDirty = true;
		private static Quaternion _orientation = Quaternion.Identity;
		private static Matrix _projection;
		private static Vector2 _translation = Vector2.Zero;
		private static Matrix _view;
		public static Vector2 lastSize = Vector2.Zero;
		public static Action OnDirty;
		public static DampedValue Zoom = 2f;

		public static Viewport viewport => EditorApplication.Instance.GraphicsDevice.Viewport;
		public static Matrix Projection
		{
			get
			{
				Vector2 size = new Vector2(viewport.Width, viewport.Height);

				if (lastSize == size)
					return _projection;

				lastSize = size;
				isViewDirty = true;
				_projection = Matrix.CreateOrthographicOffCenter(0, size.X, size.Y, 0, 0.0f, -1f);

				return _projection;
			}
		}

		public static Vector2 Position
		{
			get => _translation;
			set
			{
				isViewDirty = true;
				_translation = value;
			}
		}
		public static Matrix View
		{
			get
			{
				if (!isViewDirty)
					return _view;

				_view = Matrix.CreateFromQuaternion(_orientation) * Matrix.CreateTranslation(_translation.X, _translation.Y, 0) * Matrix.CreateScale(Zoom, Zoom, 1) * Matrix.CreateTranslation(lastSize.X / 2, lastSize.Y / 2, 0);
				isViewDirty = false;
				OnDirty?.Invoke();

				return _view;
			}
		}

		public static Vector2 ScreenToWorld(Vector2 mousePosition)
		{
			Matrix matrix = Matrix.Invert(View);
			Vector2 result = Vector2.Transform(mousePosition, matrix);

			return result;
		}

		public static Vector2 WorldToScreen(Vector2 worldPosition)
		{
			Matrix matrix = View;
			Vector2 result = Vector2.Transform(worldPosition, matrix);

			return result;
		}

		public static void MoveLocal(Vector2 movement)
		{
			_translation += Vector2.Transform(movement, _orientation);
			isViewDirty = true;
		}

		public static void Rotate(Vector3 axis, float angle)
		{
			float radians = MathHelper.ToRadians(angle);
			_orientation = Quaternion.CreateFromAxisAngle(axis, radians) * _orientation;
			_orientation.Normalize();
			isViewDirty = true;
		}

		public static void RotateLocal(Vector3 axis, float angle)
		{
			float radians = MathHelper.ToRadians(angle);
			_orientation *= Quaternion.CreateFromAxisAngle(axis, radians);
			_orientation.Normalize();
			isViewDirty = true;
		}
	}
}