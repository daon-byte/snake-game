using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace snake_game.Game
{
    /// <summary>
    /// Neon renderer, input handler and frame loop for Loop Snake. The element draws the
    /// whole screen itself (board, snake, particles, HUD and menu overlays) with a fast
    /// fixed palette so the game keeps 60 fps even on a software renderer.
    /// </summary>
    public sealed class GameView : FrameworkElement
    {
        private const int AlignLeft = 0;
        private const int AlignCenter = 1;
        private const int AlignRight = 2;

        private static readonly Color BgTopColor = Color.FromRgb(5, 8, 18);
        private static readonly Color BgBottomColor = Color.FromRgb(12, 21, 40);
        private static readonly Color GridColor = Color.FromArgb(34, 62, 104, 168);
        private static readonly Color BrightColor = Color.FromRgb(233, 242, 255);
        private static readonly Color DimColor = Color.FromRgb(122, 143, 178);
        private static readonly Color AccentColor = Color.FromRgb(88, 240, 200);
        private static readonly Color HeadNormalColor = Color.FromRgb(170, 255, 216);
        private static readonly Color TailNormalColor = Color.FromRgb(22, 122, 255);
        private static readonly Color HeadFrenzyColor = Color.FromRgb(255, 240, 140);
        private static readonly Color TailFrenzyColor = Color.FromRgb(255, 92, 44);
        private static readonly Color HeadDashColor = Color.FromRgb(235, 255, 255);
        private static readonly Color TailDashColor = Color.FromRgb(64, 210, 255);
        private static readonly Color AppleColor = Color.FromRgb(255, 78, 106);
        private static readonly Color GoldenColor = Color.FromRgb(255, 206, 92);
        private static readonly Color ShrinkColor = Color.FromRgb(96, 192, 255);
        private static readonly Color DangerColor = Color.FromRgb(255, 82, 92);

        private static readonly Typeface FontRegular = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        private static readonly Typeface FontBold = new Typeface(
            new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private readonly GameEngine _engine = new GameEngine();
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<Popup> _popups = new List<Popup>();
        private readonly Dictionary<int, SolidColorBrush> _brushes = new Dictionary<int, SolidColorBrush>();
        private readonly Random _rng = new Random();

        private readonly LinearGradientBrush _backgroundBrush;
        private readonly RadialGradientBrush _vignetteBrush;
        private readonly SolidColorBrush _boardFillBrush;

        private double _time;
        private double _lastRenderingTime = -1;
        private double _pixelsPerDip = 1.0;
        private double _cell = 24;
        private double _margin = 26;
        private double _hudTop = 26;
        private double _hudHeight = 104;
        private Rect _board = Rect.Empty;
        private Rect _footer = Rect.Empty;
        private bool _attached;

        private double _shakeTime;
        private double _shakeAmplitude;
        private double _flashTime;
        private double _flashDuration = 1;
        private Color _flashColor;

        private DrawingGroup _gridDrawing;
        private double _gridLeft = double.NaN;
        private double _gridTop = double.NaN;
        private double _gridWidth = double.NaN;
        private double _gridHeight = double.NaN;
        private double _gridCell = double.NaN;

        public GameView()
        {
            Focusable = true;
            ClipToBounds = true;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            _backgroundBrush = new LinearGradientBrush(BgTopColor, BgBottomColor, 90);
            _backgroundBrush.Freeze();

            _vignetteBrush = new RadialGradientBrush();
            _vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
            _vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55));
            _vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(130, 0, 0, 0), 1.0));
            _vignetteBrush.RadiusX = 0.75;
            _vignetteBrush.RadiusY = 0.75;
            _vignetteBrush.Freeze();

            _boardFillBrush = new SolidColorBrush(Color.FromArgb(150, 8, 14, 28));
            _boardFillBrush.Freeze();

            _engine.HighScore = HighScoreStore.Load();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            MouseLeftButtonDown += OnMouseDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                if (_pixelsPerDip <= 0) _pixelsPerDip = 1.0;
            }
            catch
            {
                _pixelsPerDip = 1.0;
            }

            if (!_attached)
            {
                CompositionTarget.Rendering += OnRendering;
                _attached = true;
            }
            _lastRenderingTime = -1;
            Focus();
            InvalidateVisual();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_attached)
            {
                CompositionTarget.Rendering -= OnRendering;
                _attached = false;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _gridDrawing = null;
            InvalidateVisual();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if (_engine.Status == GameStatus.Ready || _engine.Status == GameStatus.GameOver) StartGame();
            e.Handled = true;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            RenderingEventArgs renderingArgs = e as RenderingEventArgs;
            double now = renderingArgs != null ? renderingArgs.RenderingTime.TotalSeconds : Environment.TickCount / 1000.0;
            if (_lastRenderingTime < 0)
            {
                _lastRenderingTime = now;
                return;
            }

            double dt = now - _lastRenderingTime;
            _lastRenderingTime = now;
            if (dt <= 0) return;
            if (dt > 0.1) dt = 0.1;

            _time += dt;
            _engine.Update(dt);
            HandleEvents();
            UpdateEffects(dt);
            InvalidateVisual();
        }

        /// <summary>Routes a key from the hosting window into the game.</summary>
        public bool HandleKey(Key key)
        {
            switch (key)
            {
                case Key.Left:
                case Key.A:
                    return QueueDirection(-1, 0);
                case Key.Right:
                case Key.D:
                    return QueueDirection(1, 0);
                case Key.Up:
                case Key.W:
                    return QueueDirection(0, -1);
                case Key.Down:
                case Key.S:
                    return QueueDirection(0, 1);
                case Key.Space:
                    if (_engine.Status == GameStatus.Ready || _engine.Status == GameStatus.GameOver) StartGame();
                    else _engine.TryDash();
                    return true;
                case Key.Enter:
                    if (_engine.Status == GameStatus.Ready || _engine.Status == GameStatus.GameOver) StartGame();
                    else if (_engine.Status == GameStatus.Paused) _engine.TogglePause();
                    return true;
                case Key.P:
                case Key.Escape:
                    if (_engine.Status == GameStatus.Running || _engine.Status == GameStatus.Paused) _engine.TogglePause();
                    return true;
                case Key.M:
                    Sfx.Muted = !Sfx.Muted;
                    return true;
                default:
                    return false;
            }
        }

        public void StartGame()
        {
            _engine.StartNewGame();
            _particles.Clear();
            _popups.Clear();
            _shakeTime = 0;
            _flashTime = 0;
            Sfx.Play(SoundId.Start);
        }

        public void PauseIfRunning()
        {
            if (_engine.Status == GameStatus.Running) _engine.TogglePause();
        }

        private bool QueueDirection(int dx, int dy)
        {
            if (_engine.Status == GameStatus.Ready || _engine.Status == GameStatus.GameOver) StartGame();
            if (_engine.Status != GameStatus.Running) return true;
            _engine.SetDirection(dx, dy);
            return true;
        }

        private void HandleEvents()
        {
            if (_engine.Events.Count == 0) return;

            for (int i = 0; i < _engine.Events.Count; i++)
            {
                GameEvent gameEvent = _engine.Events[i];
                switch (gameEvent.Kind)
                {
                    case GameEventKind.NewGame:
                        _particles.Clear();
                        _popups.Clear();
                        break;
                    case GameEventKind.EatApple:
                        AddBurst(gameEvent.At, AppleColor, 14, 5.5);
                        AddPopup(gameEvent.At, "+" + gameEvent.Value, AppleColor, 13);
                        if (_engine.Combo >= 2)
                        {
                            AddPopup(new Cell(gameEvent.At.X, gameEvent.At.Y - 1), "x" + _engine.Combo, AccentColor, 11);
                        }
                        Sfx.Play(SoundId.Eat);
                        break;
                    case GameEventKind.EatGolden:
                        AddBurst(gameEvent.At, GoldenColor, 30, 9.0);
                        AddPopup(gameEvent.At, "FRENZY +" + gameEvent.Value, GoldenColor, 15);
                        SetFlash(GoldenColor, 0.32);
                        Sfx.Play(SoundId.Golden);
                        break;
                    case GameEventKind.EatShrink:
                        AddBurst(gameEvent.At, ShrinkColor, 18, 7.0);
                        AddPopup(gameEvent.At, "SHRINK +" + gameEvent.Value, ShrinkColor, 13);
                        Sfx.Play(SoundId.Shrink);
                        break;
                    case GameEventKind.Dash:
                        AddBurst(gameEvent.At, HeadDashColor, 16, 8.0);
                        AddPopup(gameEvent.At, "DASH!", HeadDashColor, 15);
                        Sfx.Play(SoundId.Dash);
                        break;
                    case GameEventKind.LevelUp:
                        AddPopup(_engine.Snake[0], "SPEED LV " + gameEvent.Value, GoldenColor, 13);
                        Sfx.Play(SoundId.LevelUp);
                        break;
                    case GameEventKind.SeamSurge:
                        AddBurst(gameEvent.At, AccentColor, 26, 9.0);
                        AddPopup(gameEvent.At, "SEAM SURGE +" + gameEvent.Value, AccentColor, 14);
                        Sfx.Play(SoundId.Golden);
                        break;
                    case GameEventKind.FoodExpired:
                        AddBurst(gameEvent.At, Color.FromRgb(120, 140, 180), 8, 4.0);
                        break;
                    case GameEventKind.Death:
                        AddBurst(gameEvent.At, DangerColor, 46, 12.0);
                        AddBurst(_engine.Snake[0], Color.FromRgb(255, 224, 208), 18, 6.0);
                        _shakeTime = 0.6;
                        _shakeAmplitude = 16;
                        SetFlash(DangerColor, 0.4);
                        HighScoreStore.Save(_engine.HighScore);
                        Sfx.Play(SoundId.Death);
                        break;
                }
            }
            _engine.Events.Clear();
        }

        private void UpdateEffects(double dt)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle particle = _particles[i];
                particle.Life -= dt;
                if (particle.Life <= 0)
                {
                    _particles.RemoveAt(i);
                    continue;
                }
                particle.X += particle.VX * dt;
                particle.Y += particle.VY * dt;
                particle.VX -= particle.VX * 2.2 * dt;
                particle.VY -= particle.VY * 2.2 * dt;
                particle.VY += 1.2 * dt;
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];
                popup.Life -= dt;
                if (popup.Life <= 0)
                {
                    _popups.RemoveAt(i);
                    continue;
                }
                popup.Y -= dt * 1.1;
            }

            if (_shakeTime > 0)
            {
                _shakeTime -= dt;
                if (_shakeTime < 0) _shakeTime = 0;
            }
            if (_flashTime > 0)
            {
                _flashTime -= dt;
                if (_flashTime < 0) _flashTime = 0;
            }
        }

        private void AddBurst(Cell at, Color color, int count, double speed)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = _rng.NextDouble() * Math.PI * 2;
                double velocity = speed * (0.35 + _rng.NextDouble() * 0.9);
                Particle particle = new Particle();
                particle.X = at.X + 0.5 + (_rng.NextDouble() - 0.5) * 0.4;
                particle.Y = at.Y + 0.5 + (_rng.NextDouble() - 0.5) * 0.4;
                particle.VX = Math.Cos(angle) * velocity;
                particle.VY = Math.Sin(angle) * velocity;
                particle.LifeMax = 0.45 + _rng.NextDouble() * 0.5;
                particle.Life = particle.LifeMax;
                particle.Size = _cell * (0.05 + _rng.NextDouble() * 0.09);
                particle.Color = Color.FromArgb((byte)(170 + _rng.Next(70)), color.R, color.G, color.B);
                _particles.Add(particle);
            }
        }

        private void AddPopup(Cell at, string text, Color color, double size)
        {
            Popup popup = new Popup();
            popup.X = at.X + 0.5;
            popup.Y = at.Y + 0.5;
            popup.Text = text;
            popup.Color = color;
            popup.Size = size;
            popup.LifeMax = 0.95;
            popup.Life = 0.95;
            _popups.Add(popup);
        }

        private void SetFlash(Color color, double duration)
        {
            _flashColor = color;
            _flashTime = duration;
            _flashDuration = duration;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 700 : availableSize.Height;
            return new Size(width, height);
        }

        private void Layout()
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 80 || height < 80) return;

            _margin = Math.Max(16, Math.Min(30, width * 0.024));
            _hudHeight = Math.Max(82, Math.Min(104, height * 0.115));
            _hudTop = _margin;

            double footerHeight = height > 640 ? 22 : 0;
            double footerGap = footerHeight > 0 ? 8 : 0;
            double availableWidth = width - _margin * 2;
            double availableHeight = height - _margin * 2 - _hudHeight - footerHeight - footerGap;
            if (availableHeight < 120) availableHeight = 120;

            double cell = Math.Floor(Math.Min(availableWidth / GameEngine.Cols, availableHeight / GameEngine.Rows));
            if (cell < 8) cell = 8;
            _cell = cell;

            double boardWidth = cell * GameEngine.Cols;
            double boardHeight = cell * GameEngine.Rows;
            double boardX = Math.Floor((width - boardWidth) / 2);
            double boardY = _margin + _hudHeight;
            double maxBottom = height - _margin - footerHeight - footerGap;
            if (boardY + boardHeight > maxBottom)
            {
                boardY = Math.Max(_margin + _hudHeight * 0.5, maxBottom - boardHeight);
            }

            _board = new Rect(boardX, boardY, boardWidth, boardHeight);
            _footer = new Rect(_margin, _board.Bottom + footerGap + 4, width - _margin * 2, footerHeight);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Layout();
            if (ActualWidth < 80 || ActualHeight < 80) return;

            dc.DrawRectangle(_backgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.DrawRectangle(_vignetteBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            double shakeX = 0;
            double shakeY = 0;
            if (_shakeTime > 0)
            {
                double strength = _shakeTime / 0.6;
                if (strength > 1) strength = 1;
                strength *= strength;
                shakeX = (_rng.NextDouble() * 2 - 1) * _shakeAmplitude * strength;
                shakeY = (_rng.NextDouble() * 2 - 1) * _shakeAmplitude * strength;
            }

            dc.PushTransform(new TranslateTransform(shakeX, shakeY));
            DrawBoard(dc);
            dc.Pop();

            DrawHud(dc);
            DrawFooter(dc);
            DrawScreens(dc);
        }

        private void DrawBoard(DrawingContext dc)
        {
            dc.DrawRoundedRectangle(_boardFillBrush, null, _board, 18, 18);

            EnsureGrid();
            if (_gridDrawing != null) dc.DrawDrawing(_gridDrawing);

            dc.PushClip(new RectangleGeometry(_board, 18, 18));
            DrawStars(dc);
            DrawSeamMarkers(dc);
            DrawFoods(dc);
            DrawSnake(dc);
            DrawParticles(dc);
            DrawPopups(dc);
            DrawFlash(dc);
            dc.Pop();

            Pen border = new Pen(BrushFor(Color.FromArgb(85, 70, 200, 255)), 1.4);
            dc.DrawRoundedRectangle(null, border, _board, 18, 18);
        }

        private void EnsureGrid()
        {
            if (_gridDrawing != null &&
                _gridLeft == _board.Left && _gridTop == _board.Top &&
                _gridWidth == _board.Width && _gridHeight == _board.Height &&
                _gridCell == _cell)
            {
                return;
            }

            DrawingGroup group = new DrawingGroup();
            using (DrawingContext context = group.Open())
            {
                Pen linePen = new Pen(BrushFor(GridColor), 1);
                for (int x = 1; x < GameEngine.Cols; x++)
                {
                    double px = _board.Left + x * _cell;
                    context.DrawLine(linePen, new Point(px, _board.Top), new Point(px, _board.Bottom));
                }
                for (int y = 1; y < GameEngine.Rows; y++)
                {
                    double py = _board.Top + y * _cell;
                    context.DrawLine(linePen, new Point(_board.Left, py), new Point(_board.Right, py));
                }
            }
            group.Freeze();

            _gridDrawing = group;
            _gridLeft = _board.Left;
            _gridTop = _board.Top;
            _gridWidth = _board.Width;
            _gridHeight = _board.Height;
            _gridCell = _cell;
        }

        private void DrawStars(DrawingContext dc)
        {
            for (int i = 0; i < 46; i++)
            {
                double sx = Hash01(i * 7 + 3);
                double sy = Hash01(i * 13 + 11);
                double phase = Hash01(i * 5 + 1) * Math.PI * 2;
                double speed = 0.6 + Hash01(i * 11 + 9) * 1.4;
                double twinkle = 0.5 + 0.5 * Math.Sin(_time * speed + phase);
                byte alpha = (byte)(14 + 52 * twinkle);
                double radius = 0.5 + Hash01(i * 3 + 7) * 1.3;
                Color color = Color.FromArgb(alpha, 120, 170, 255);
                dc.DrawEllipse(BrushFor(color), null, new Point(_board.Left + sx * _board.Width, _board.Top + sy * _board.Height), radius, radius);
            }
        }

        private void DrawSeamMarkers(DrawingContext dc)
        {
            double pulse = 0.5 + 0.5 * Math.Sin(_time * 1.6);
            Color seamColor = Color.FromArgb((byte)(50 + 40 * pulse), 90, 220, 255);
            Pen seamPen = new Pen(BrushFor(seamColor), 1.5);
            seamPen.DashStyle = new DashStyle(new double[] { 2, 3 }, 0);
            Rect inset = _board;
            inset.Inflate(-3, -3);
            dc.DrawRoundedRectangle(null, seamPen, inset, 15, 15);

            double midX = _board.Left + _board.Width / 2;
            double midY = _board.Top + _board.Height / 2;
            double arrow = Math.Max(4, _cell * 0.30);
            Color arrowColor = Color.FromArgb((byte)(85 + 65 * pulse), 90, 220, 255);
            DrawChevron(dc, new Point(_board.Left + _cell * 0.6, midY), arrow, -1, 0, arrowColor);
            DrawChevron(dc, new Point(_board.Right - _cell * 0.6, midY), arrow, 1, 0, arrowColor);
            DrawChevron(dc, new Point(midX, _board.Top + _cell * 0.6), arrow, 0, -1, arrowColor);
            DrawChevron(dc, new Point(midX, _board.Bottom - _cell * 0.6), arrow, 0, 1, arrowColor);
        }

        private static void DrawChevron(DrawingContext dc, Point center, double size, int dirX, int dirY, Color color)
        {
            Pen pen = new Pen(BrushForStatic(color), 1.6);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            Point apex = new Point(center.X + dirX * size, center.Y + dirY * size);
            double perpendicularX = -dirY * size;
            double perpendicularY = dirX * size;
            Point baseCenter = new Point(center.X - dirX * size, center.Y - dirY * size);
            dc.DrawLine(pen, new Point(baseCenter.X + perpendicularX, baseCenter.Y + perpendicularY), apex);
            dc.DrawLine(pen, apex, new Point(baseCenter.X - perpendicularX, baseCenter.Y - perpendicularY));
        }

        private void DrawFoods(DrawingContext dc)
        {
            List<Food> foods = _engine.Foods;
            for (int i = 0; i < foods.Count; i++)
            {
                Food food = foods[i];
                Color core;
                if (food.Kind == FoodKind.Apple) core = AppleColor;
                else if (food.Kind == FoodKind.Golden) core = GoldenColor;
                else core = ShrinkColor;

                double pulse = 0.5 + 0.5 * Math.Sin(_time * 6.0 + food.Seed);
                double baseRadius = _cell * (food.Kind == FoodKind.Golden ? 0.30 : 0.27);
                double radius = baseRadius * (0.92 + 0.14 * pulse);
                double half = _cell * 0.5;
                double lifeFraction = food.LifeMax > 0 ? Clamp01(food.LifeLeft / food.LifeMax) : 1.0;
                double fx = food.Cell.X;
                double fy = food.Cell.Y;

                ForEachWrapped(fx, fy, delegate(Point center)
                {
                    dc.DrawEllipse(BrushFor(Color.FromArgb(28, core.R, core.G, core.B)), null, center, radius * 2.3, radius * 2.3);
                    dc.DrawEllipse(BrushFor(Color.FromArgb(62, core.R, core.G, core.B)), null, center, radius * 1.45, radius * 1.45);

                    if (food.Kind == FoodKind.Golden)
                    {
                        DrawStar(dc, center, radius * 1.55, _time * 1.4 + food.Seed, core, 180);
                        DrawStar(dc, center, radius * 0.9, -_time * 1.4 + food.Seed, Color.FromArgb(255, 255, 250, 220), 235);
                    }
                    else if (food.Kind == FoodKind.Shrink)
                    {
                        dc.DrawEllipse(BrushFor(core), null, center, radius, radius);
                        dc.DrawEllipse(BrushFor(Color.FromArgb(165, 8, 24, 46)), null, center, radius * 0.55, radius * 0.55);
                        dc.DrawRoundedRectangle(
                            BrushFor(Color.FromArgb(225, 232, 250, 255)),
                            null,
                            new Rect(center.X - radius * 0.32, center.Y - radius * 0.075, radius * 0.64, radius * 0.15),
                            radius * 0.08,
                            radius * 0.08);
                    }
                    else
                    {
                        dc.DrawEllipse(BrushFor(core), null, center, radius, radius);
                        dc.DrawEllipse(
                            BrushFor(Color.FromArgb(225, 255, 208, 220)),
                            null,
                            new Point(center.X - radius * 0.33, center.Y - radius * 0.33),
                            radius * 0.24,
                            radius * 0.24);
                        Pen stemPen = new Pen(BrushFor(Color.FromArgb(220, 120, 210, 140)), Math.Max(1.2, _cell * 0.055));
                        dc.DrawLine(stemPen, new Point(center.X, center.Y - radius), new Point(center.X + radius * 0.18, center.Y - radius * 1.5));
                        dc.DrawEllipse(BrushFor(Color.FromArgb(210, 100, 220, 140)), null, new Point(center.X + radius * 0.52, center.Y - radius * 1.42), radius * 0.26, radius * 0.16);
                    }

                    double ringRadius = half * 0.88;
                    dc.DrawEllipse(null, new Pen(BrushFor(Color.FromArgb(30, 255, 255, 255)), 1.0), center, ringRadius, ringRadius);
                    if (lifeFraction < 0.995)
                    {
                        DrawArc(
                            dc,
                            center,
                            ringRadius,
                            -Math.PI / 2,
                            Math.PI * 2 * lifeFraction,
                            new Pen(BrushFor(Color.FromArgb(150, core.R, core.G, core.B)), 1.7));
                    }
                });
            }
        }

        private static void DrawStar(DrawingContext dc, Point center, double radius, double rotation, Color color, byte alpha)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                for (int i = 0; i < 10; i++)
                {
                    double r = (i % 2 == 0) ? radius : radius * 0.44;
                    double angle = rotation + i * Math.PI / 5.0 - Math.PI / 2.0;
                    Point point = new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r);
                    if (i == 0) context.BeginFigure(point, true, true);
                    else context.LineTo(point, true, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(BrushForStatic(Color.FromArgb(alpha, color.R, color.G, color.B)), null, geometry);
            dc.DrawGeometry(null, new Pen(BrushForStatic(Color.FromArgb(120, 255, 255, 255)), 1), geometry);
        }

        private static void DrawArc(DrawingContext dc, Point center, double radius, double startAngle, double sweep, Pen pen)
        {
            if (sweep <= 0.001 || radius <= 0.5) return;
            if (sweep > Math.PI * 2) sweep = Math.PI * 2;
            Point start = new Point(center.X + Math.Cos(startAngle) * radius, center.Y + Math.Sin(startAngle) * radius);
            Point end = new Point(center.X + Math.Cos(startAngle + sweep) * radius, center.Y + Math.Sin(startAngle + sweep) * radius);

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.ArcTo(end, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        private void DrawSnake(DrawingContext dc)
        {
            List<Cell> snake = _engine.Snake;
            List<Cell> previous = _engine.PrevSnake;
            double progress = Clamp01(_engine.MoveProgress);
            int count = snake.Count;

            for (int i = count - 1; i >= 0; i--)
            {
                double fx = snake[i].X;
                double fy = snake[i].Y;
                if (i < previous.Count)
                {
                    double dx = snake[i].X - previous[i].X;
                    double dy = snake[i].Y - previous[i].Y;
                    if (dx > GameEngine.Cols / 2.0) dx -= GameEngine.Cols;
                    else if (dx < -GameEngine.Cols / 2.0) dx += GameEngine.Cols;
                    if (dy > GameEngine.Rows / 2.0) dy -= GameEngine.Rows;
                    else if (dy < -GameEngine.Rows / 2.0) dy += GameEngine.Rows;
                    fx = previous[i].X + dx * progress;
                    fy = previous[i].Y + dy * progress;
                }

                bool head = i == 0;
                double t = count <= 1 ? 0 : i / (double)(count - 1);
                double taper = head ? 0.96 : 0.88 - 0.34 * t;
                double size = _cell * taper;
                if (size < 2) size = 2;
                Color color = SnakeColor(i, count, head);

                ForEachWrapped(fx, fy, delegate(Point center)
                {
                    double half = size / 2;
                    dc.DrawRoundedRectangle(
                        BrushFor(Color.FromArgb(24, color.R, color.G, color.B)),
                        null,
                        new Rect(center.X - half * 1.75, center.Y - half * 1.75, size * 1.75, size * 1.75),
                        size * 0.7,
                        size * 0.7);
                    dc.DrawRoundedRectangle(
                        BrushFor(color),
                        null,
                        new Rect(center.X - half, center.Y - half, size, size),
                        size * 0.36,
                        size * 0.36);
                    if (head) DrawEyes(dc, center, size);
                });
            }
        }

        private Color SnakeColor(int index, int count, bool head)
        {
            double t = count <= 1 ? 0 : index / (double)(count - 1);
            t = Clamp01(t);
            t = Math.Round(t * 12) / 12.0;
            if (_engine.IsFrenzy) return LerpColor(HeadFrenzyColor, TailFrenzyColor, t);
            if (_engine.IsDashing) return LerpColor(HeadDashColor, TailDashColor, t);
            if (head) return HeadNormalColor;
            return LerpColor(HeadNormalColor, TailNormalColor, t);
        }

        private void DrawEyes(DrawingContext dc, Point center, double size)
        {
            double dirX = _engine.DirX;
            double dirY = _engine.DirY;
            double eyeRadius = size * 0.17;
            double pupilRadius = size * 0.08;
            double forward = size * 0.15;
            double sideways = size * 0.20;
            double perpendicularX = -dirY;
            double perpendicularY = dirX;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                Point eye = new Point(
                    center.X + dirX * forward + perpendicularX * sideways * sign,
                    center.Y + dirY * forward + perpendicularY * sideways * sign);
                dc.DrawEllipse(BrushFor(Color.FromArgb(240, 250, 255, 255)), null, eye, eyeRadius, eyeRadius);
                dc.DrawEllipse(
                    BrushFor(Color.FromArgb(235, 10, 18, 30)),
                    null,
                    new Point(eye.X + dirX * eyeRadius * 0.32, eye.Y + dirY * eyeRadius * 0.32),
                    pupilRadius,
                    pupilRadius);
            }
        }

        private void DrawParticles(DrawingContext dc)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                Particle particle = _particles[i];
                double life = Clamp01(particle.Life / particle.LifeMax);
                byte alpha = (byte)(particle.Color.A * life);
                double x = _board.Left + particle.X * _cell;
                double y = _board.Top + particle.Y * _cell;
                double radius = particle.Size * (0.5 + 0.5 * life);
                dc.DrawEllipse(BrushFor(Color.FromArgb(alpha, particle.Color.R, particle.Color.G, particle.Color.B)), null, new Point(x, y), radius, radius);
            }
        }

        private void DrawPopups(DrawingContext dc)
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup popup = _popups[i];
                double life = Clamp01(popup.Life / popup.LifeMax);
                byte alpha = (byte)(255 * Math.Min(1.0, life * 1.6));
                double x = _board.Left + popup.X * _cell;
                double y = _board.Top + popup.Y * _cell;
                DrawText(dc, popup.Text, x + 1.2, y + 1.2, popup.Size, Color.FromArgb((byte)(alpha * 0.7), 0, 0, 0), true, AlignCenter, false);
                DrawText(dc, popup.Text, x, y, popup.Size, Color.FromArgb(alpha, popup.Color.R, popup.Color.G, popup.Color.B), true, AlignCenter, false);
            }
        }

        private void DrawFlash(DrawingContext dc)
        {
            if (_flashTime <= 0) return;
            double strength = Clamp01(_flashTime / _flashDuration);
            byte alpha = (byte)(120 * strength);
            dc.DrawRectangle(BrushFor(Color.FromArgb(alpha, _flashColor.R, _flashColor.G, _flashColor.B)), null, _board);
        }

        private void DrawHud(DrawingContext dc)
        {
            double left = _margin;
            double right = ActualWidth - _margin;
            double top = _hudTop;
            double center = ActualWidth / 2;
            double bottom = _hudTop + _hudHeight;

            DrawText(dc, "SCORE", left, top, 10.5, DimColor, false, AlignLeft, false);
            DrawText(dc, _engine.Score.ToString(CultureInfo.InvariantCulture), left, top + 13, 32, BrightColor, true, AlignLeft, true);

            DrawText(dc, "BEST", right, top, 10.5, DimColor, false, AlignRight, false);
            int best = Math.Max(_engine.HighScore, _engine.Score);
            DrawText(dc, best.ToString(CultureInfo.InvariantCulture), right, top + 13, 32, Color.FromArgb(210, 150, 200, 255), true, AlignRight, false);

            if (_engine.Combo >= 2)
            {
                Color comboColor = _engine.IsFrenzy ? GoldenColor : AccentColor;
                DrawText(dc, "COMBO", center, top, 10.5, DimColor, false, AlignCenter, false);
                double pulse = 0.5 + 0.5 * Math.Sin(_time * 7.0);
                Color comboText = Color.FromArgb((byte)(200 + 55 * pulse), comboColor.R, comboColor.G, comboColor.B);
                DrawText(dc, "x" + _engine.Combo, center, top + 8, 30, comboText, true, AlignCenter, true);

                double barWidth = Math.Min(200, _board.Width * 0.24);
                Rect track = new Rect(center - barWidth / 2, top + 51, barWidth, 4);
                dc.DrawRoundedRectangle(BrushFor(Color.FromArgb(45, 255, 255, 255)), null, track, 2, 2);
                double fillWidth = track.Width * _engine.ComboFraction;
                if (fillWidth > 0.5)
                {
                    dc.DrawRoundedRectangle(BrushFor(comboColor), null, new Rect(track.X, track.Y, fillWidth, track.Height), 2, 2);
                }
            }
            else
            {
                DrawText(dc, "CHAIN BONUS - EAT FAST", center, top + 15, 10.5, DimColor, false, AlignCenter, false);
            }

            if (_engine.IsFrenzy)
            {
                double pulse = 0.5 + 0.5 * Math.Sin(_time * 9.0);
                byte alpha = (byte)(160 + 90 * pulse);
                DrawText(dc, "FRENZY x2", center, top + 63, 10.5, Color.FromArgb(alpha, GoldenColor.R, GoldenColor.G, GoldenColor.B), true, AlignCenter, false);
            }

            string info = "LENGTH " + _engine.Snake.Count + "   |   SPEED LV " + _engine.Level + "   |   WRAPS " + _engine.WrapCount;
            DrawText(dc, info, left, bottom - 20, 11.5, DimColor, false, AlignLeft, false);

            bool dashReady = _engine.DashEnergy >= 0.999;
            Color dashColor = _engine.IsDashing ? HeadDashColor : (dashReady ? AccentColor : Color.FromArgb(120, 120, 205, 255));
            string dashLabel = _engine.IsDashing ? "DASHING" : (dashReady ? "DASH READY [SPACE]" : "DASH ENERGY");
            DrawText(dc, dashLabel, right, bottom - 34, 10, dashColor, dashReady || _engine.IsDashing, AlignRight, false);

            double barWidth2 = 150;
            Rect dashTrack = new Rect(right - barWidth2, bottom - 16, barWidth2, 6);
            dc.DrawRoundedRectangle(BrushFor(Color.FromArgb(50, 255, 255, 255)), null, dashTrack, 3, 3);
            double dashRatio = Clamp01(_engine.DashEnergy);
            if (dashRatio > 0.01)
            {
                dc.DrawRoundedRectangle(BrushFor(dashColor), null, new Rect(dashTrack.X, dashTrack.Y, dashTrack.Width * dashRatio, dashTrack.Height), 3, 3);
            }
        }

        private void DrawFooter(DrawingContext dc)
        {
            if (_footer.Height <= 4) return;
            string hint = "ARROWS / WASD MOVE   |   SPACE DASH   |   P PAUSE   |   ENTER NEW GAME   |   M SOUND: "
                        + (Sfx.Muted ? "OFF" : "ON");
            DrawText(dc, hint, _footer.Left + _footer.Width / 2, _footer.Top, 11.5, Color.FromArgb(150, 118, 142, 182), false, AlignCenter, false);
        }

        private void DrawScreens(DrawingContext dc)
        {
            if (_board.IsEmpty) return;
            switch (_engine.Status)
            {
                case GameStatus.Ready:
                    DrawReadyScreen(dc);
                    break;
                case GameStatus.Paused:
                    DrawPausedScreen(dc);
                    break;
                case GameStatus.GameOver:
                    DrawGameOverScreen(dc);
                    break;
            }
        }

        private void DrawOverlayVeil(DrawingContext dc, byte alpha)
        {
            dc.DrawRoundedRectangle(BrushFor(Color.FromArgb(alpha, 4, 7, 14)), null, _board, 18, 18);
        }

        private void DrawReadyScreen(DrawingContext dc)
        {
            DrawOverlayVeil(dc, 212);

            double centerX = _board.Left + _board.Width / 2;
            double centerY = _board.Top + _board.Height / 2;
            double boardHeight = _board.Height;
            double titleSize = Math.Max(32, Math.Min(58, _board.Width * 0.058));
            double titleY = centerY - boardHeight * 0.30;
            double subtitleY = titleY + titleSize + 8;

            DrawText(dc, "LOOP SNAKE", centerX, titleY, titleSize, BrightColor, true, AlignCenter, true);
            DrawText(dc, "INFINITE WRAP MAP  -  COMBO CHAINS  -  FRENZY  -  DASH", centerX, subtitleY, 12.5, AccentColor, false, AlignCenter, false);

            string[] featureLines = new string[]
            {
                "The map is a torus: every edge warps you to the opposite side.",
                "Chain apples before the combo timer runs out for bigger scores.",
                "Gold stars trigger FRENZY: x2 points, faster growth, 6 seconds.",
                "Blue orbs shorten your tail. Fill the bar, then SPACE to dash."
            };
            double featureY = subtitleY + 40;
            for (int i = 0; i < featureLines.Length; i++)
            {
                DrawText(dc, featureLines[i], centerX, featureY + i * 22, 12.5, DimColor, false, AlignCenter, false);
            }

            double promptY = featureY + featureLines.Length * 22 + 26;
            double pulse = 0.5 + 0.5 * Math.Sin(_time * 3.2);
            byte promptAlpha = (byte)(140 + 115 * pulse);
            DrawText(dc, "PRESS ENTER TO START", centerX, promptY, 17, Color.FromArgb(promptAlpha, AccentColor.R, AccentColor.G, AccentColor.B), true, AlignCenter, true);

            DrawDemoSnake(dc, Math.Min(_board.Bottom - 26, promptY + 46));
        }

        private void DrawDemoSnake(DrawingContext dc, double pixelY)
        {
            double demoY = (pixelY - _board.Top) / _cell - 0.5;
            double headX = ((_time * 3.2) % (GameEngine.Cols + 10)) - 5;
            for (int i = 7; i >= 0; i--)
            {
                double fx = headX - i;
                double t = i / 7.0;
                double size = _cell * (0.62 - t * 0.30);
                Color color = LerpColor(AccentColor, Color.FromRgb(60, 130, 255), t);
                ForEachWrapped(fx, demoY, delegate(Point center)
                {
                    dc.DrawEllipse(BrushFor(Color.FromArgb(26, color.R, color.G, color.B)), null, center, size * 0.75, size * 0.75);
                    dc.DrawEllipse(BrushFor(color), null, center, size * 0.5, size * 0.5);
                });
            }
        }

        private void DrawPausedScreen(DrawingContext dc)
        {
            DrawOverlayVeil(dc, 196);
            double centerX = _board.Left + _board.Width / 2;
            double centerY = _board.Top + _board.Height / 2;

            DrawText(dc, "PAUSED", centerX, centerY - 66, Math.Max(30, Math.Min(48, _board.Width * 0.05)), BrightColor, true, AlignCenter, true);
            DrawText(dc, "SCORE " + _engine.Score + "   |   LENGTH " + _engine.Snake.Count + "   |   LEVEL " + _engine.Level, centerX, centerY - 4, 13, DimColor, false, AlignCenter, false);
            double pulse = 0.5 + 0.5 * Math.Sin(_time * 3.0);
            DrawText(dc, "PRESS P OR ESC TO RESUME", centerX, centerY + 40, 15, Color.FromArgb((byte)(150 + 100 * pulse), AccentColor.R, AccentColor.G, AccentColor.B), true, AlignCenter, false);
            DrawText(dc, "ENTER STARTS A FRESH RUN", centerX, centerY + 70, 11.5, DimColor, false, AlignCenter, false);
        }

        private void DrawGameOverScreen(DrawingContext dc)
        {
            DrawOverlayVeil(dc, 216);
            double centerX = _board.Left + _board.Width / 2;
            double centerY = _board.Top + _board.Height / 2;
            double titleSize = Math.Max(32, Math.Min(54, _board.Width * 0.052));

            DrawText(dc, "GAME OVER", centerX, centerY - 170, titleSize, DangerColor, true, AlignCenter, true);

            string[] labels = new string[] { "SCORE", "BEST", "LENGTH", "APPLES", "MAX COMBO" };
            string[] values = new string[]
            {
                _engine.Score.ToString(CultureInfo.InvariantCulture),
                _engine.HighScore.ToString(CultureInfo.InvariantCulture),
                _engine.Snake.Count.ToString(CultureInfo.InvariantCulture),
                _engine.ApplesEaten.ToString(CultureInfo.InvariantCulture),
                "x" + _engine.MaxCombo
            };

            double statsY = centerY - 92;
            for (int i = 0; i < labels.Length; i++)
            {
                DrawText(dc, labels[i], centerX - 22, statsY + i * 29, 10.5, DimColor, false, AlignRight, false);
                DrawText(dc, values[i], centerX + 22, statsY + i * 29 - 6, 18, BrightColor, true, AlignLeft, false);
            }

            if (_engine.NewRecord)
            {
                double pulse = 0.5 + 0.5 * Math.Sin(_time * 6.0);
                DrawText(dc, "NEW BEST SCORE!", centerX, centerY + 76, 17, Color.FromArgb((byte)(150 + 105 * pulse), GoldenColor.R, GoldenColor.G, GoldenColor.B), true, AlignCenter, true);
            }
            else
            {
                DrawText(dc, "BEST " + _engine.HighScore, centerX, centerY + 78, 13, DimColor, false, AlignCenter, false);
            }

            double promptPulse = 0.5 + 0.5 * Math.Sin(_time * 3.2 + 1.0);
            DrawText(dc, "PRESS ENTER TO RESTART", centerX, centerY + 118, 16, Color.FromArgb((byte)(140 + 110 * promptPulse), AccentColor.R, AccentColor.G, AccentColor.B), true, AlignCenter, false);
        }

        private void ForEachWrapped(double fx, double fy, Action<Point> draw)
        {
            double cell = _cell;
            double baseX = _board.Left + (fx + 0.5) * cell;
            double baseY = _board.Top + (fy + 0.5) * cell;
            for (int kx = -1; kx <= 1; kx++)
            {
                double x = baseX + kx * GameEngine.Cols * cell;
                if (x < _board.Left - cell * 2 || x > _board.Right + cell * 2) continue;
                for (int ky = -1; ky <= 1; ky++)
                {
                    double y = baseY + ky * GameEngine.Rows * cell;
                    if (y < _board.Top - cell * 2 || y > _board.Bottom + cell * 2) continue;
                    draw(new Point(x, y));
                }
            }
        }

        private Size DrawText(DrawingContext dc, string text, double x, double y, double size, Color color, bool bold, int align, bool glow)
        {
            Typeface typeface = bold ? FontBold : FontRegular;
            FormattedText formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                BrushFor(color),
                _pixelsPerDip);

            double drawX = x;
            if (align == AlignCenter) drawX = x - formatted.Width / 2;
            else if (align == AlignRight) drawX = x - formatted.Width;

            if (glow)
            {
                Color glowColor = Color.FromArgb(95, color.R, color.G, color.B);
                FormattedText glowText = new FormattedText(
                    text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    size,
                    BrushFor(glowColor),
                    _pixelsPerDip);
                double offset = Math.Max(1.0, size * 0.07);
                dc.DrawText(glowText, new Point(drawX - offset, y));
                dc.DrawText(glowText, new Point(drawX + offset, y + offset * 0.5));
            }

            dc.DrawText(formatted, new Point(drawX, y));
            return new Size(formatted.Width, formatted.Height);
        }

        private SolidColorBrush BrushFor(Color color)
        {
            int key = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
            SolidColorBrush brush;
            if (_brushes.TryGetValue(key, out brush)) return brush;

            brush = new SolidColorBrush(color);
            brush.Freeze();
            if (_brushes.Count > 700) _brushes.Clear();
            _brushes[key] = brush;
            return brush;
        }

        private static SolidColorBrush BrushForStatic(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color LerpColor(Color from, Color to, double t)
        {
            t = Clamp01(t);
            return Color.FromArgb(
                (byte)(from.A + (to.A - from.A) * t),
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private static double Hash01(int seed)
        {
            unchecked
            {
                uint hash = (uint)seed * 2654435761u;
                hash ^= hash >> 13;
                hash *= 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0xFFFF) / 65535.0;
            }
        }

        private sealed class Particle
        {
            public double X;
            public double Y;
            public double VX;
            public double VY;
            public double Life;
            public double LifeMax;
            public double Size;
            public Color Color;
        }

        private sealed class Popup
        {
            public double X;
            public double Y;
            public string Text;
            public Color Color;
            public double Size;
            public double Life;
            public double LifeMax;
        }
    }
}
