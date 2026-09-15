@echo off
rem Purpose: Compile the Windows Forms source into a small standalone executable.
setlocal
cd /d "%~dp0"
set "QUOTA_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%QUOTA_CSC%" set "QUOTA_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%QUOTA_CSC%" exit /b 1
if not exist dist mkdir dist
"%QUOTA_CSC%" /nologo /target:winexe /optimize+ /out:dist\CodexQuotaTray.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll src\Program.cs
if errorlevel 1 exit /b 1
echo Built dist\CodexQuotaTray.exe
