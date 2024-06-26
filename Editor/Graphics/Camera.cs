using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Editor.Graphics
{
    public class Camera
    {
        public static Viewport viewport => EditorApplication.Instance.GraphicsDevice.Viewport;
        private Quaternion _orientation = Quaternion.Identity;
        private Vector3 _translation = Vector3.Zero;
        private bool _isDirty;
        public float Zoom = 2f;
        public Vector2 lastSize = Vector2.Zero;
        private Matrix _view;
        private Matrix _projection;
        public Matrix Projection
        {
            get
            {
                Vector2 size = new Vector2(viewport.Width / Zoom, viewport.Height / Zoom);
                if (lastSize != size)
                {
                    lastSize = size;
                    _projection = Matrix.CreateOrthographic(size.X, size.Y, -1, 1);

                }
                return _projection;
            }
        }


        public Vector3 Position
        {
            get => _translation;
            set
            {
                _isDirty = true;
                _translation = value;
            }
        }

        public Matrix View
        {
            get
            {
                if (!_isDirty)
                    return _view;

                _view = Matrix.Invert(Matrix.CreateFromQuaternion(_orientation) * Matrix.CreateTranslation(_translation.X, _translation.Y, 0));
                return _view;
            }
        }

        public Camera()
        {
        }

        public Vector2 ScreenToWorld(Vector2 mousePosition)
        {
            Matrix matrix = Matrix.Invert(Matrix.Multiply(Matrix.Multiply(Matrix.Identity, View), Projection));
            mousePosition.X = (mousePosition.X - viewport.X) / viewport.Width * 2f - 1f;
            mousePosition.Y = 0f - ((mousePosition.Y - viewport.Y) / viewport.Height * 2f - 1f);
            float z = (0 - viewport.MinDepth) / (viewport.MaxDepth - viewport.MinDepth);
            Vector2 result = Vector2.Transform(mousePosition, matrix);
            float num = mousePosition.X * matrix.M14 + mousePosition.Y * matrix.M24 + z * matrix.M34 + matrix.M44;
            if (-1E-45f <= num - 1f && num - 1f <= float.Epsilon)
            {
                result.X /= num;
                result.Y /= num;
            }

            return result;

            // return viewport.Unproject(new Vector3(mousePosition, near ? 0 : 1), Projection, View, Matrix.Identity);
        }

        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            Matrix matrix = Matrix.Multiply(Matrix.Multiply(Matrix.Identity, View), Projection);
            Vector2 result = Vector2.Transform(worldPosition, matrix);
            float num = worldPosition.X * matrix.M14 + worldPosition.Y * matrix.M24;
            if (-1E-45f <= num - 1f && num - 1f <= float.Epsilon)
            {
                result.X /= num;
                result.Y /= num;
            }

            result.X = (result.X + 1f) * 0.5f * viewport.Width + viewport.X;
            result.Y = (0f - result.Y + 1f) * 0.5f * viewport.Height + viewport.Y;
            return result;

            // return viewport.Project(worldPosition, Projection, View, Matrix.Identity);
        }

        public void Move(Vector3 movement)
        {
            _translation.X += movement.X;
            _translation.Y += movement.Y;
            _translation.Z += movement.Z;
            _isDirty = true;
        }

        public void MoveLocal(Vector3 movement)
        {
            _translation += Vector3.Transform(movement, _orientation);
            _isDirty = true;
        }

        public void Rotate(Vector3 axis, float angle)
        {
            var radians = MathHelper.ToRadians(angle);
            _orientation = Quaternion.CreateFromAxisAngle(axis, radians) * _orientation;
            _orientation.Normalize();
            _isDirty = true;
        }

        public void RotateLocal(Vector3 axis, float angle)
        {
            var radians = MathHelper.ToRadians(angle);
            _orientation *= Quaternion.CreateFromAxisAngle(axis, radians);
            _orientation.Normalize();
            _isDirty = true;
        }
    }
}