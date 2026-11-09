# Mouse Mover Bot

A small Windows desktop app that **physically moves the mouse cursor** at a
regular interval so the PC does not go to sleep, lock the screen, or auto log
out while you are idle.

## How it works

It calls the Win32 **`SendInput`** API with `MOUSEEVENTF_MOVE`. This injects a
real mouse-input event into the OS input queue — Windows handles it exactly like
movement from a physical mouse: the cursor actually moves on screen and the
system idle timer is reset. It is *not* a hidden/background workaround.

Each cycle the cursor **glides to a new random position** (it does not return to
where it started) over a configurable duration using an ease-in / ease-out curve
(slow start, faster middle, slow stop), so it looks exactly like a person
dragging the mouse around the screen rather than an instant jump. Targets are
clamped to stay on the screen the cursor is currently on.

## Files

| File          | Purpose                                            |
|---------------|----------------------------------------------------|
| `MouseBot.cs` | Source code (C# / WinForms)                        |
| `build.bat`   | Compiles the source into `MouseBot.exe`            |
| `MouseBot.exe`| The ready-to-run desktop app                       |

## Run

Double-click **`MouseBot.exe`**. No installation and no dependencies required
(it uses the .NET Framework already built into Windows).

### Controls
- **Move every (seconds)** — interval between movements (default 30s; the system
  lock/sleep timeout is usually 2-3 minutes, so anything under ~60s is safe).
- **Max step distance (px)** — the largest jump per cycle; each move picks a
  random direction and a random distance up to this value (default 250px).
- **Drag duration (ms)** — how long the glide takes; larger = slower, smoother
  (default 1000ms = 1 second).
- **Start / Stop** — turn the bot on and off.
- Closing/minimizing the window sends it to the **system tray**; right-click the
  tray icon to Start/Stop, Show/Hide, or Exit.

## Rebuild from source

Run `build.bat`, or:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe ^
  /out:MouseBot.exe /reference:System.dll /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll MouseBot.cs
```
