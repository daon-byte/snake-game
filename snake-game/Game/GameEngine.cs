using System;
using System.Collections.Generic;

namespace snake_game.Game
{
    public enum GameStatus
    {
        Ready,
        Running,
        Paused,
        GameOver
    }

    public enum FoodKind
    {
        Apple,
        Golden,
        Shrink
    }

    public enum GameEventKind
    {
        NewGame,
        EatApple,
        EatGolden,
        EatShrink,
        FoodExpired,
        Dash,
        SeamSurge,
        LevelUp,
        Death
    }

    public struct Cell : IEquatable<Cell>
    {
        public int X;
        public int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Cell other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is Cell && Equals((Cell)obj);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        public override string ToString()
        {
            return X + "," + Y;
        }
    }

    public sealed class Food
    {
        public Cell Cell;
        public FoodKind Kind;
        public double LifeLeft;
        public double LifeMax;
        public double Age;
        public double Seed;
    }

    public sealed class GameEvent
    {
        public GameEventKind Kind;
        public Cell At;
        public int Value;
    }

    /// <summary>
    /// Pure simulation of the wrap-around snake. No WPF types are referenced here so the
    /// rules can be exercised headlessly by SelfTest.
    ///
    /// Board model: a torus of Cols x Rows cells. Any step that leaves one edge re-enters
    /// from the opposite edge, which is what makes the map effectively infinite.
    /// </summary>
    public sealed class GameEngine
    {
        public const int Cols = 40;
        public const int Rows = 30;
        public const int StartLength = 4;
        public const int MinLength = 3;
        public const double ComboWindow = 3.0;
        public const double FrenzyDuration = 6.0;
        public const double DashDuration = 1.3;

        private readonly List<Cell> _snake = new List<Cell>();
        private readonly List<Cell> _prevSnake = new List<Cell>();
        private readonly List<Food> _foods = new List<Food>();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private readonly List<Cell> _queuedDirections = new List<Cell>();

        private double _goldenTimer;
        private double _shrinkTimer;
        private int _wrapCount;

        public GameEngine()
        {
            Rng = new Random();
            HighScore = 0;
            Reset();
        }

        /// <summary>Random source; replaceable from tests for deterministic runs.</summary>
        public Random Rng { get; set; }

        public GameStatus Status { get; private set; }
        public int Score { get; private set; }
        public int HighScore { get; set; }
        public int Level { get; private set; }
        public int ApplesEaten { get; private set; }
        public int MaxCombo { get; private set; }
        public int Combo { get; private set; }
        public int WrapCount { get { return _wrapCount; } }
        public double ComboTimeLeft { get; private set; }
        public double DashEnergy { get; private set; }
        public double DashTimeLeft { get; private set; }
        public double FrenzyTimeLeft { get; private set; }
        public double MoveProgress { get; private set; }
        public bool NewRecord { get; private set; }
        public int DirX { get; private set; }
        public int DirY { get; private set; }

        public List<Cell> Snake { get { return _snake; } }
        public List<Cell> PrevSnake { get { return _prevSnake; } }
        public List<Food> Foods { get { return _foods; } }
        public List<GameEvent> Events { get { return _events; } }

        public bool IsRunning { get { return Status == GameStatus.Running; } }
        public bool IsFrenzy { get { return FrenzyTimeLeft > 0; } }
        public bool IsDashing { get { return DashTimeLeft > 0; } }

        public double ComboFraction
        {
            get
            {
                return Clamp01(ComboTimeLeft / ComboWindow);
            }
        }

        /// <summary>Seconds per grid step. Faster every few apples, faster again while dashing.</summary>
        public double StepInterval
        {
            get
            {
                double interval = 0.132 - (Level - 1) * 0.0065;
                if (interval < 0.060) interval = 0.060;
                if (DashTimeLeft > 0) interval *= 0.55;
                return interval;
            }
        }

        public void Reset()
        {
            _snake.Clear();
            int centerX = Cols / 2;
            int centerY = Rows / 2;
            for (int i = 0; i < StartLength; i++)
            {
                _snake.Add(new Cell(centerX - i, centerY));
            }

            _prevSnake.Clear();
            _prevSnake.AddRange(_snake);
            _queuedDirections.Clear();
            _foods.Clear();
            _events.Clear();

            DirX = 1;
            DirY = 0;
            Score = 0;
            ApplesEaten = 0;
            Level = 1;
            Combo = 0;
            ComboTimeLeft = 0;
            MaxCombo = 0;
            DashEnergy = 0;
            DashTimeLeft = 0;
            FrenzyTimeLeft = 0;
            MoveProgress = 0;
            _goldenTimer = 10;
            _shrinkTimer = 14;
            _wrapCount = 0;
            NewRecord = false;
            Status = GameStatus.Ready;
        }

        public void StartNewGame()
        {
            Reset();
            Status = GameStatus.Running;
            SpawnFood(FoodKind.Apple, 15.0);
            Raise(GameEventKind.NewGame, _snake[0], 0);
        }

        public bool SetDirection(int dx, int dy)
        {
            if (Status != GameStatus.Running) return false;
            if (dx == 0 && dy == 0) return false;

            int lastX = DirX;
            int lastY = DirY;
            if (_queuedDirections.Count > 0)
            {
                Cell last = _queuedDirections[_queuedDirections.Count - 1];
                lastX = last.X;
                lastY = last.Y;
            }

            if (dx == lastX && dy == lastY) return false;
            if (dx == -lastX && dy == -lastY) return false;
            if (_queuedDirections.Count >= 2) return false;

            _queuedDirections.Add(new Cell(dx, dy));
            return true;
        }

        public bool TogglePause()
        {
            if (Status == GameStatus.Running)
            {
                Status = GameStatus.Paused;
                return true;
            }
            if (Status == GameStatus.Paused)
            {
                Status = GameStatus.Running;
                return true;
            }
            return false;
        }

        public bool TryDash()
        {
            if (Status != GameStatus.Running) return false;
            if (DashEnergy < 0.999) return false;
            DashEnergy = 0;
            DashTimeLeft = DashDuration;
            Raise(GameEventKind.Dash, _snake[0], 0);
            return true;
        }

        /// <summary>Advances timers and movement; called once per rendered frame by the view.</summary>
        public void Update(double dt)
        {
            if (Status != GameStatus.Running || dt <= 0) return;

            AdvanceEffects(dt);

            MoveProgress += dt / StepInterval;
            int guard = 0;
            while (MoveProgress >= 1.0 && Status == GameStatus.Running && guard < 12)
            {
                MoveProgress -= 1.0;
                Step();
                guard++;
            }
            if (Status != GameStatus.Running) MoveProgress = 0;
        }

        /// <summary>
        /// Advances only the time based effects (combo decay, food lifetime, special spawns,
        /// level up). Kept public so SelfTest can verify rules without moving the snake.
        /// </summary>
        public void AdvanceEffects(double dt)
        {
            if (dt <= 0) return;

            if (ComboTimeLeft > 0)
            {
                ComboTimeLeft -= dt;
                if (ComboTimeLeft < 0) ComboTimeLeft = 0;
                if (ComboTimeLeft <= 0) Combo = 0;
            }
            if (DashTimeLeft > 0)
            {
                DashTimeLeft -= dt;
                if (DashTimeLeft < 0) DashTimeLeft = 0;
            }
            if (FrenzyTimeLeft > 0)
            {
                FrenzyTimeLeft -= dt;
                if (FrenzyTimeLeft < 0) FrenzyTimeLeft = 0;
            }

            for (int i = _foods.Count - 1; i >= 0; i--)
            {
                Food food = _foods[i];
                food.Age += dt;
                food.LifeLeft -= dt;
                if (food.LifeLeft > 0) continue;

                _foods.RemoveAt(i);
                Raise(GameEventKind.FoodExpired, food.Cell, 0);
                if (food.Kind == FoodKind.Apple)
                {
                    Combo = 0;
                    ComboTimeLeft = 0;
                    SpawnFood(FoodKind.Apple, 15.0);
                }
            }

            bool hasApple = false;
            for (int i = 0; i < _foods.Count; i++)
            {
                if (_foods[i].Kind == FoodKind.Apple)
                {
                    hasApple = true;
                    break;
                }
            }
            if (!hasApple) SpawnFood(FoodKind.Apple, 15.0);

            _goldenTimer -= dt;
            if (_goldenTimer <= 0)
            {
                if (ApplesEaten >= 2 && CountFood(FoodKind.Golden) == 0) SpawnFood(FoodKind.Golden, 7.0);
                _goldenTimer = 15 + Rng.NextDouble() * 8;
            }

            _shrinkTimer -= dt;
            if (_shrinkTimer <= 0)
            {
                if (_snake.Count >= 7 && CountFood(FoodKind.Shrink) == 0) SpawnFood(FoodKind.Shrink, 10.0);
                _shrinkTimer = 16 + Rng.NextDouble() * 10;
            }

            UpdateLevel();
        }

        private void UpdateLevel()
        {
            int targetLevel = 1 + ApplesEaten / 6;
            if (targetLevel != Level)
            {
                Level = targetLevel;
                Raise(GameEventKind.LevelUp, _snake[0], Level);
            }
        }

        /// <summary>Test hook: advances exactly one grid step, ignoring the time accumulator.</summary>
        public void StepOnce()
        {
            if (Status != GameStatus.Running) return;
            Step();
        }

        /// <summary>Test hook: fills the dash energy bar so dash rules can be checked.</summary>
        public void ChargeDash()
        {
            DashEnergy = 1.0;
        }

        private void Step()
        {
            if (_queuedDirections.Count > 0)
            {
                Cell direction = _queuedDirections[0];
                _queuedDirections.RemoveAt(0);
                if (!(direction.X == -DirX && direction.Y == -DirY))
                {
                    DirX = direction.X;
                    DirY = direction.Y;
                }
            }

            _prevSnake.Clear();
            _prevSnake.AddRange(_snake);

            Cell head = _snake[0];
            int rawX = head.X + DirX;
            int rawY = head.Y + DirY;
            bool wrapped = rawX < 0 || rawX >= Cols || rawY < 0 || rawY >= Rows;
            int nextX = Mod(rawX, Cols);
            int nextY = Mod(rawY, Rows);
            Cell next = new Cell(nextX, nextY);

            Food eaten = FindFoodAt(next);
            int bodyCount = _snake.Count - (eaten != null ? 0 : 1);
            for (int i = 0; i < bodyCount; i++)
            {
                if (_snake[i].X == nextX && _snake[i].Y == nextY)
                {
                    Die();
                    return;
                }
            }

            _snake.Insert(0, next);
            if (eaten == null) _snake.RemoveAt(_snake.Count - 1);

            if (wrapped)
            {
                _wrapCount++;
                AddScore(2);
                DashEnergy = Clamp01(DashEnergy + 0.01);
                if (_wrapCount % 6 == 0)
                {
                    AddScore(30);
                    Raise(GameEventKind.SeamSurge, next, 30);
                }
            }

            if (eaten != null) ApplyFood(eaten);

            UpdateLevel();
        }

        private void ApplyFood(Food food)
        {
            _foods.Remove(food);

            switch (food.Kind)
            {
                case FoodKind.Apple:
                {
                    ApplesEaten++;
                    AddCombo();
                    int gained = 10 * Combo * (IsFrenzy ? 2 : 1);
                    AddScore(gained);
                    DashEnergy = Clamp01(DashEnergy + 0.24);
                    SpawnFood(FoodKind.Apple, 15.0);
                    Raise(GameEventKind.EatApple, food.Cell, gained);
                    break;
                }
                case FoodKind.Golden:
                {
                    AddCombo();
                    int gained = 75 * Combo;
                    AddScore(gained);
                    FrenzyTimeLeft = FrenzyDuration;
                    DashEnergy = Clamp01(DashEnergy + 0.45);
                    Grow(2);
                    _goldenTimer = 15 + Rng.NextDouble() * 8;
                    Raise(GameEventKind.EatGolden, food.Cell, gained);
                    break;
                }
                default:
                {
                    int remove = Math.Min(3, _snake.Count - MinLength);
                    if (remove < 0) remove = 0;
                    for (int i = 0; i < remove; i++) _snake.RemoveAt(_snake.Count - 1);
                    int gained = 20 + 5 * Level;
                    AddScore(gained);
                    DashEnergy = Clamp01(DashEnergy + 0.15);
                    _shrinkTimer = 16 + Rng.NextDouble() * 10;
                    Raise(GameEventKind.EatShrink, food.Cell, gained);
                    break;
                }
            }
        }

        private void AddCombo()
        {
            if (ComboTimeLeft > 0) Combo++;
            else Combo = 1;

            int cap = IsFrenzy ? 12 : 8;
            if (Combo > cap) Combo = cap;
            if (Combo > MaxCombo) MaxCombo = Combo;
            ComboTimeLeft = ComboWindow;
        }

        private void Grow(int extraSegments)
        {
            for (int i = 0; i < extraSegments; i++)
            {
                if (_snake.Count > 0) _snake.Add(_snake[_snake.Count - 1]);
            }
        }

        private void AddScore(int value)
        {
            Score += value;
        }

        private void Die()
        {
            Status = GameStatus.GameOver;
            MoveProgress = 0;
            NewRecord = Score > HighScore;
            if (NewRecord) HighScore = Score;
            Raise(GameEventKind.Death, _snake[0], Score);
        }

        private void SpawnFood(FoodKind kind, double life)
        {
            Cell cell;
            if (!TryFindFreeCell(out cell)) return;

            Food food = new Food();
            food.Cell = cell;
            food.Kind = kind;
            food.LifeLeft = life;
            food.LifeMax = life;
            food.Age = 0;
            food.Seed = Rng.NextDouble() * Math.PI * 2;
            _foods.Add(food);
        }

        private bool TryFindFreeCell(out Cell cell)
        {
            cell = new Cell(0, 0);
            int total = Cols * Rows;
            if (_snake.Count + _foods.Count >= total) return false;

            for (int attempt = 0; attempt < 400; attempt++)
            {
                Cell candidate = new Cell(Rng.Next(Cols), Rng.Next(Rows));
                if (IsSnake(candidate) || IsFood(candidate)) continue;
                cell = candidate;
                return true;
            }

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    Cell candidate = new Cell(x, y);
                    if (IsSnake(candidate) || IsFood(candidate)) continue;
                    cell = candidate;
                    return true;
                }
            }
            return false;
        }

        private bool IsSnake(Cell cell)
        {
            for (int i = 0; i < _snake.Count; i++)
            {
                if (_snake[i].X == cell.X && _snake[i].Y == cell.Y) return true;
            }
            return false;
        }

        private bool IsFood(Cell cell)
        {
            for (int i = 0; i < _foods.Count; i++)
            {
                if (_foods[i].Cell.X == cell.X && _foods[i].Cell.Y == cell.Y) return true;
            }
            return false;
        }

        private Food FindFoodAt(Cell cell)
        {
            for (int i = 0; i < _foods.Count; i++)
            {
                if (_foods[i].Cell.X == cell.X && _foods[i].Cell.Y == cell.Y) return _foods[i];
            }
            return null;
        }

        private int CountFood(FoodKind kind)
        {
            int count = 0;
            for (int i = 0; i < _foods.Count; i++)
            {
                if (_foods[i].Kind == kind) count++;
            }
            return count;
        }

        private void Raise(GameEventKind kind, Cell at, int value)
        {
            GameEvent gameEvent = new GameEvent();
            gameEvent.Kind = kind;
            gameEvent.At = at;
            gameEvent.Value = value;
            _events.Add(gameEvent);
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            if (result < 0) result += modulus;
            return result;
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}
