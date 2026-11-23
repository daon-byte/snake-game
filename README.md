# Loop Snake

[한국어 README](README.ko.md)

A neon WPF snake game whose map is a torus: every edge wraps you to the opposite side, so
the playfield never ends. Built with Visual Studio 2022 (WPF, .NET Framework 4.7.2) and
verified through the Visual Studio MCP automation server (build, debug, screenshots).

## Gameplay

- **Infinite wrap map** - drive off any edge and you reappear on the opposite side. The
  renderer draws the split snake on both sides while it crosses a seam, and the score
  rewards every edge crossing (2 points each).
- **Seam surge** - every 6th wrap pays a 30 point bonus and fires a particle burst.
- **Combo chains** - eat apples in quick succession to raise the multiplier (x2 ... x8).
  The combo ring drains in 3 seconds, so keep moving.
- **FRENZY** - gold stars appear on a timer. Eating one grants 3 extra segments, 75 x combo
  points, and 6 seconds of double apple points with gold snake colors.
- **Shrink orbs** - blue orbs remove 3 segments (never below 3), useful when the tail is in
  the way. They also award a small score.
- **Dash** - eating fills the dash bar. When full, press SPACE for a 1.3 s speed burst.
- **Speed levels** - the snake gets faster every 6 apples.
- **Timed food** - each food has a shrinking lifetime ring. A missed apple breaks the combo.
- **Polish** - particle bursts, floating score text, screen shake on death, flash effects,
  high score persistence, synthesized sound effects (M toggles).

## Controls

| Key | Action |
| --- | --- |
| Arrow keys / WASD | Steer |
| SPACE | Dash when the bar is full |
| P / ESC | Pause / resume |
| ENTER | Start a run, restart after game over, resume while paused |
| M | Toggle sound |
| Mouse click | Focus the window / start a run |

The game auto-pauses when the window loses focus.

## Build and run

Open `snake-game.sln` in Visual Studio 2022 and press F5, or:

```powershell
msbuild snake-game\snake-game.csproj /p:Configuration=Debug
.\snake-game\bin\Debug\snake-game.exe
```

## Headless rule tests

The simulation is engine-only code, so the rules can be checked without a window:

```powershell
.\snake-game\bin\Debug\snake-game.exe --selftest
```

Exit code is 0 when all checks pass. The report is written to
`%TEMP%\loop-snake-selftest.txt` and covers wrap-around, reverse rejection, combo growth
and decay, self collision, food placement and expiry, dash rules, shrink floor, frenzy
scoring, seam surge, level speed and high score handling.

## Project layout

| File | Purpose |
| --- | --- |
| `Game/GameEngine.cs` | Pure simulation: torus grid, snake, food, combo, dash, frenzy, scoring |
| `Game/GameView.cs` | Custom FrameworkElement: frame loop, neon rendering, particles, HUD, menus, input |
| `Game/SelfTest.cs` | Headless engine checks used by `--selftest` |
| `Game/Sfx.cs` | Small synthesized WAV sound effects with safe fallbacks |
| `Game/HighScoreStore.cs` | Best score persistence under `%LOCALAPPDATA%\LoopSnake\best.txt` |
| `MainWindow.xaml(.cs)` | Hosts the game view and forwards keyboard input |
| `App.xaml(.cs)` | Startup and `--selftest` handling |

## Screenshots

The `../screenshots` folder holds captures of the title screen, gameplay, frenzy mode,
pause screen and game over screen taken from the running build.
