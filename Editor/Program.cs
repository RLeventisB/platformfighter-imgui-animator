using System;
using Editor;

namespace game
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new EditorApplication())
                game.Run();
        }
    }
}