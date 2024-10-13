using System;

namespace Editor
{
	public static class Program
	{
		[STAThread]
		public static void Main()
		{
			using (EditorApplication game = new EditorApplication())
				game.Run();
		}
	}
}