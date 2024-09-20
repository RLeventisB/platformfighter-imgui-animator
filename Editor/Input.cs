using Editor.Graphics;

using Microsoft.Xna.Framework.Input;

namespace Editor
{
	public static class Input
	{
		public static Vector2 PreviousMousePos, MousePos;
		public static Vector2 PreviousMouseWorld, MouseWorld, MouseWorldDelta;
		private static MouseState previousMouseState;

		public static void Update()
		{
			MouseState newMouseState = Mouse.GetState();
			PreviousMousePos = previousMouseState.Position.ToVector2();
			PreviousMouseWorld = Camera.ScreenToWorld(PreviousMousePos);

			MousePos = newMouseState.Position.ToVector2();
			MouseWorld = Camera.ScreenToWorld(MousePos);

			MouseWorldDelta = MouseWorld - PreviousMouseWorld;

			previousMouseState = newMouseState;
		}
	}
}