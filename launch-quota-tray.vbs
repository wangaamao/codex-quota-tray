' Purpose: Launch the compiled quota tray without opening a console window.
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
base = fso.GetParentFolderName(WScript.ScriptFullName)
exe = base & "\dist\CodexQuotaTray.exe"
If fso.FileExists(exe) Then
  shell.Run """" & exe & """", 0, False
Else
  MsgBox "CodexQuotaTray.exe was not found. Run build.cmd first.", 48, "Codex Quota Tray"
End If
