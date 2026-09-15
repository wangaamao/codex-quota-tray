@echo off
rem Purpose: Stop the running Codex Quota Tray process for the current user.
taskkill /FI "IMAGENAME eq CodexQuotaTray.exe" >nul 2>nul
