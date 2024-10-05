using System;

namespace Editor
{
	public class Program
	{
		[STAThread]
		public static void Main()
		{
				using (EditorApplication game = new EditorApplication())
					game.Run();
		}
	}
}