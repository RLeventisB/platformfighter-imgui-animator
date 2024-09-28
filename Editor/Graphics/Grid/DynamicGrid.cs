using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rune.MonoGame;

namespace Editor.Graphics.Grid
{
	public class DynamicGrid
	{
		private readonly DynamicGridSettings _settings;
		public const int GridSize = 32;

		public DynamicGrid(DynamicGridSettings settings)
		{
			_settings = settings;
		}

		/// <summary>
		/// The grid is rendered in the x,y plane by default
		/// </summary>
		/// <param name="batch">The renderbatch to render this grid</param>
		/// <param name="transform">The transformation to transform to a different space</param>
		public void Render(PrimitiveBatch batch, Matrix transform)
		{
			Viewport viewport = EditorApplication.Graphics.Viewport;
			float finalGridSize = GridSize;

			Point topLeftLine = ((Camera.ScreenToWorld(new Vector2(0, 0)) + new Vector2(-finalGridSize, -finalGridSize)) / finalGridSize).ToPoint();
			Point topRightLine = ((Camera.ScreenToWorld(new Vector2(viewport.Width, 0)) + new Vector2(finalGridSize, -finalGridSize)) / finalGridSize).ToPoint();
			Point bottomRightLine = ((Camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height)) + new Vector2(finalGridSize, finalGridSize)) / finalGridSize).ToPoint();
			Point bottomLeftLine = ((Camera.ScreenToWorld(new Vector2(0, viewport.Height)) + new Vector2(-finalGridSize, finalGridSize)) / finalGridSize).ToPoint();

			// the grid lines are ordered as minor, major, origin
			for (int lineType = 0; lineType < 3; lineType++)
			{
				Color lineColor = _settings.MinorGridColor;
				if (lineType == 1)
					lineColor = _settings.MajorGridColor;
				else if (lineType == 2)
					lineColor = _settings.OriginGridColor;

				// draw horizontal lines
				for (int i = topLeftLine.Y; i <= bottomRightLine.Y; ++i)
				{
					// skip any line that don't match the line type we're adding
					if (lineType == 0 && (i == 0 || i % _settings.MajorLineEvery == 0))
						continue;

					if (lineType == 1 && (i == 0 || i % _settings.MajorLineEvery != 0))
						continue;

					if (lineType == 2 && i != 0)
						continue;

					Vector3 from = default;
					Vector3 to = default;
					to.X = topLeftLine.X * finalGridSize;
					from.X = topRightLine.X * finalGridSize;
					from.Y = to.Y = i * finalGridSize;
					from.Z = 0;
					to.Z = 0;

					Vector3.Transform(ref to, ref transform, out to);
					Vector3.Transform(ref from, ref transform, out from);

					batch.DrawLine(from, to, lineColor);
				}

				// draw vertical lines
				for (int i = topLeftLine.X; i <= topRightLine.X; ++i)
				{
					// skip any line that don't match the line type we're adding
					if (lineType == 0 && (i == 0 || i % _settings.MajorLineEvery == 0))
						continue;

					if (lineType == 1 && (i == 0 || i % _settings.MajorLineEvery != 0))
						continue;

					if (lineType == 2 && i != 0)
						continue;

					Vector3 from = default;
					Vector3 to = default;
					to.Y = topLeftLine.Y * finalGridSize;
					from.Y = bottomLeftLine.Y * finalGridSize;
					from.X = to.X = i * finalGridSize;
					from.Z = 0;
					to.Z = 0;

					Vector3.Transform(ref to, ref transform, out to);
					Vector3.Transform(ref from, ref transform, out from);

					batch.DrawLine(from, to, lineColor);
				}
			}
		}
	}
}