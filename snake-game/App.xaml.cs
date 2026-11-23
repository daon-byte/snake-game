using System;
using System.Windows;

namespace snake_game
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool selfTest = false;
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (string.Equals(e.Args[i], "--selftest", StringComparison.OrdinalIgnoreCase)) selfTest = true;
            }

            if (selfTest)
            {
                int failures = Game.SelfTest.Run();
                Shutdown(failures > 0 ? 1 : 0);
                return;
            }

            MainWindow window = new MainWindow();
            window.Show();
        }
    }
}
