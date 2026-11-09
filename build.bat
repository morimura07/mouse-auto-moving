@echo off
REM Compiles MouseBot.cs into a standalone Windows desktop app (MouseBot.exe).
REM Uses the C# compiler that ships with the .NET Framework - nothing to install.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

"%CSC%" /target:winexe /out:MouseBot.exe ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  MouseBot.cs

if exist MouseBot.exe (
  echo.
  echo Build succeeded:  MouseBot.exe
) else (
  echo.
  echo Build FAILED.
)
