using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace snake_game.Game
{
    /// <summary>
    /// Headless rule tests for GameEngine. Run the game with --selftest to execute them.
    /// The report goes to %TEMP%\loop-snake-selftest.txt and to the debug output.
    /// </summary>
    public static class SelfTest
    {
        private static readonly StringBuilder Log = new StringBuilder();
        private static int _passed;
        private static int _failed;

        public static int Run()
        {
            _passed = 0;
            _failed = 0;
            Log.Length = 0;
            Log.AppendLine("Loop Snake engine self test");
            Log.AppendLine("------------------------------------------------");

            TestWrap();
            TestNoReverse();
            TestEatAndCombo();
            TestComboExpires();
            TestSelfCollision();
            TestFoodNotOnSnake();
            TestFoodExpiry();
            TestDash();
            TestShrinkFloor();
            TestFrenzy();
            TestSeamSurge();
            TestLevelSpeed();
            TestHighScore();

            Log.AppendLine("------------------------------------------------");
            Log.AppendLine("RESULT: " + _passed + " passed, " + _failed + " failed");

            string report = Log.ToString();
            string path = Path.Combine(Path.GetTempPath(), "loop-snake-selftest.txt");
            try { File.WriteAllText(path, report); } catch { }
            try { Debug.WriteLine(report); } catch { }
            return _failed;
        }

        private static void Check(string name, bool condition, string detail)
        {
            if (condition)
            {
                _passed++;
                Log.AppendLine("[PASS] " + name);
            }
            else
            {
                _failed++;
                Log.AppendLine("[FAIL] " + name + " :: " + detail);
            }
        }

        private static GameEngine NewGame()
        {
            GameEngine engine = new GameEngine();
            engine.Rng = new Random(12345);
            engine.StartNewGame();
            engine.Foods.Clear();
            return engine;
        }

        private static void AddFood(GameEngine engine, Cell cell, FoodKind kind, double life)
        {
            Food food = new Food();
            food.Cell = cell;
            food.Kind = kind;
            food.LifeLeft = life;
            food.LifeMax = life;
            engine.Foods.Add(food);
        }

        private static void TurnToLeft(GameEngine engine)
        {
            engine.SetDirection(0, 1);
            engine.StepOnce();
            engine.SetDirection(-1, 0);
            engine.StepOnce();
        }

        private static void SetSnake(GameEngine engine, params Cell[] cells)
        {
            engine.Snake.Clear();
            for (int i = 0; i < cells.Length; i++) engine.Snake.Add(cells[i]);
        }

        private static void TestWrap()
        {
            GameEngine horizontal = NewGame();
            TurnToLeft(horizontal);
            SetSnake(horizontal, new Cell(0, 15), new Cell(1, 15), new Cell(2, 15), new Cell(3, 15));
            horizontal.StepOnce();
            Check("wrap left edge to right edge",
                horizontal.Snake[0].X == GameEngine.Cols - 1 && horizontal.Snake[0].Y == 15,
                "head=" + horizontal.Snake[0]);

            GameEngine vertical = NewGame();
            vertical.SetDirection(0, -1);
            vertical.StepOnce();
            SetSnake(vertical, new Cell(12, 0), new Cell(12, 1), new Cell(12, 2), new Cell(12, 3));
            vertical.StepOnce();
            Check("wrap top edge to bottom edge",
                vertical.Snake[0].Y == GameEngine.Rows - 1 && vertical.Snake[0].X == 12,
                "head=" + vertical.Snake[0]);
        }

        private static void TestNoReverse()
        {
            GameEngine engine = NewGame();
            Check("reverse direction rejected", !engine.SetDirection(-1, 0), "left should be rejected while moving right");
            Check("perpendicular direction accepted", engine.SetDirection(0, 1), "down should be accepted");
            Check("reverse of queued direction rejected", !engine.SetDirection(0, -1), "up should be rejected after queuing down");
        }

        private static void TestEatAndCombo()
        {
            GameEngine engine = NewGame();
            int startLength = engine.Snake.Count;
            AddFood(engine, new Cell(21, 15), FoodKind.Apple, 15);
            engine.StepOnce();
            Check("apple grows the snake", engine.Snake.Count == startLength + 1, "length=" + engine.Snake.Count);
            Check("first apple scores 10", engine.Score == 10, "score=" + engine.Score);
            Check("first apple sets combo 1", engine.Combo == 1, "combo=" + engine.Combo);

            engine.Foods.Clear();
            AddFood(engine, new Cell(22, 15), FoodKind.Apple, 15);
            engine.StepOnce();
            Check("second apple scores 20", engine.Score == 30, "score=" + engine.Score);
            Check("combo rises to 2", engine.Combo == 2, "combo=" + engine.Combo);
        }

        private static void TestComboExpires()
        {
            GameEngine engine = NewGame();
            AddFood(engine, new Cell(21, 15), FoodKind.Apple, 15);
            engine.StepOnce();
            Check("combo active after eating", engine.Combo == 1, "combo=" + engine.Combo);
            for (int i = 0; i < 40; i++) engine.AdvanceEffects(0.1);
            Check("combo decays after the combo window",
                engine.Combo == 0 && engine.ComboTimeLeft == 0,
                "combo=" + engine.Combo + " timer=" + engine.ComboTimeLeft);
        }

        private static void TestSelfCollision()
        {
            GameEngine engine = NewGame();
            SetSnake(engine, new Cell(5, 5), new Cell(6, 5), new Cell(6, 6), new Cell(5, 6), new Cell(4, 6));
            engine.SetDirection(0, 1);
            engine.StepOnce();
            Check("running into the body ends the game", engine.Status == GameStatus.GameOver, "status=" + engine.Status);
        }

        private static void TestFoodNotOnSnake()
        {
            bool ok = true;
            string detail = "";
            GameEngine engine = new GameEngine();
            engine.Rng = new Random(777);
            for (int round = 0; round < 120 && ok; round++)
            {
                engine.StartNewGame();
                for (int f = 0; f < engine.Foods.Count; f++)
                {
                    Cell foodCell = engine.Foods[f].Cell;
                    for (int s = 0; s < engine.Snake.Count; s++)
                    {
                        if (foodCell.X == engine.Snake[s].X && foodCell.Y == engine.Snake[s].Y)
                        {
                            ok = false;
                            detail = "round " + round + " food " + foodCell;
                        }
                    }
                }
            }
            Check("food never spawns on the snake", ok, detail);
        }

        private static void TestFoodExpiry()
        {
            GameEngine engine = NewGame();
            AddFood(engine, new Cell(21, 15), FoodKind.Apple, 15);
            engine.StepOnce();
            engine.Foods.Clear();
            AddFood(engine, new Cell(30, 20), FoodKind.Apple, 0.05);
            engine.AdvanceEffects(0.1);
            Check("apple expiry resets the combo", engine.Combo == 0, "combo=" + engine.Combo);
            int apples = 0;
            for (int i = 0; i < engine.Foods.Count; i++)
            {
                if (engine.Foods[i].Kind == FoodKind.Apple) apples++;
            }
            Check("apple expiry respawns a fresh apple", apples == 1, "apples=" + apples);
        }

        private static void TestDash()
        {
            GameEngine engine = NewGame();
            Check("dash blocked without energy", !engine.TryDash(), "TryDash should fail at zero energy");
            double baseInterval = engine.StepInterval;
            engine.ChargeDash();
            Check("dash fires when charged", engine.TryDash(), "TryDash should succeed when charged");
            Check("dash drains the energy bar", engine.DashEnergy == 0, "energy=" + engine.DashEnergy);
            Check("dash speeds up stepping", engine.StepInterval < baseInterval,
                "interval=" + engine.StepInterval + " base=" + baseInterval);
        }

        private static void TestShrinkFloor()
        {
            GameEngine engine = NewGame();
            AddFood(engine, new Cell(21, 15), FoodKind.Shrink, 10);
            engine.StepOnce();
            Check("shrink never removes the head", engine.Snake.Count >= GameEngine.MinLength,
                "length=" + engine.Snake.Count);
            Check("shrink keeps the game running", engine.Status == GameStatus.Running, "status=" + engine.Status);
        }

        private static void TestFrenzy()
        {
            GameEngine engine = NewGame();
            AddFood(engine, new Cell(21, 15), FoodKind.Golden, 7);
            engine.StepOnce();
            Check("golden food triggers frenzy", engine.FrenzyTimeLeft > 0, "frenzy=" + engine.FrenzyTimeLeft);
            Check("golden food scores 75", engine.Score == 75, "score=" + engine.Score);

            engine.Foods.Clear();
            AddFood(engine, new Cell(22, 15), FoodKind.Apple, 15);
            engine.StepOnce();
            Check("apple scores double during frenzy", engine.Score == 115, "score=" + engine.Score);
        }

        private static void TestSeamSurge()
        {
            GameEngine engine = NewGame();
            TurnToLeft(engine);
            int startScore = engine.Score;
            for (int i = 0; i < 6; i++)
            {
                SetSnake(engine, new Cell(0, 15), new Cell(1, 15), new Cell(2, 15), new Cell(3, 15));
                engine.StepOnce();
                if (engine.Status != GameStatus.Running) break;
            }
            Check("edge crossings are counted", engine.WrapCount == 6, "wraps=" + engine.WrapCount);
            Check("seam surge bonus applied", engine.Score - startScore == 42, "scoreDelta=" + (engine.Score - startScore));
        }

        private static void TestLevelSpeed()
        {
            GameEngine engine = NewGame();
            double baseInterval = engine.StepInterval;
            for (int i = 0; i < 6; i++)
            {
                engine.Foods.Clear();
                int headX = engine.Snake[0].X;
                int headY = engine.Snake[0].Y;
                AddFood(engine, new Cell((headX + 1) % GameEngine.Cols, headY), FoodKind.Apple, 15);
                engine.StepOnce();
            }
            Check("six apples raise the level", engine.Level == 2, "level=" + engine.Level);
            Check("level up speeds up the snake", engine.StepInterval < baseInterval,
                "interval=" + engine.StepInterval + " base=" + baseInterval);
        }

        private static void TestHighScore()
        {
            GameEngine beaten = NewGame();
            beaten.HighScore = 500;
            SetSnake(beaten, new Cell(5, 5), new Cell(6, 5), new Cell(6, 6), new Cell(5, 6), new Cell(4, 6));
            beaten.SetDirection(0, 1);
            beaten.StepOnce();
            Check("low score is not a record",
                beaten.Status == GameStatus.GameOver && !beaten.NewRecord && beaten.HighScore == 500,
                "status=" + beaten.Status + " newRecord=" + beaten.NewRecord + " best=" + beaten.HighScore);

            GameEngine record = NewGame();
            record.HighScore = 0;
            AddFood(record, new Cell(21, 15), FoodKind.Apple, 15);
            record.StepOnce();
            int earned = record.Score;
            SetSnake(record, new Cell(5, 5), new Cell(6, 5), new Cell(6, 6), new Cell(5, 6), new Cell(4, 6));
            record.SetDirection(0, 1);
            record.StepOnce();
            Check("beating the best score sets a record",
                record.NewRecord && record.HighScore == earned,
                "newRecord=" + record.NewRecord + " best=" + record.HighScore + " earned=" + earned);
        }
    }
}
