using System;
using System.Globalization;
using System.IO;

namespace snake_game.Game
{
    /// <summary>Loads and stores the best score under %LOCALAPPDATA%\LoopSnake.</summary>
    internal static class HighScoreStore
    {
        private static string FilePath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoopSnake");
            return Path.Combine(folder, "best.txt");
        }

        public static int Load()
        {
            try
            {
                string path = FilePath();
                if (!File.Exists(path)) return 0;
                int value;
                if (int.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    if (value < 0) return 0;
                    return value;
                }
            }
            catch
            {
                // A missing or locked save file must never break the game.
            }
            return 0;
        }

        public static void Save(int score)
        {
            try
            {
                string path = FilePath();
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(path, score.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                // Ignore write failures; the score still shows for the current session.
            }
        }
    }
}
