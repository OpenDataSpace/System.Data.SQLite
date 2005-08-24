Const SQLiteVersion = "1.0.16.0"

Main

Sub Main()

   Dim WshShell
   Set WshShell = WScript.CreateObject("WScript.Shell")

   Dim fso
   Set fso = WScript.CreateObject("Scripting.FileSystemObject")

   Dim myDir
   myDir = fso.GetParentFolderName(WScript.ScriptFullName)

   Dim regRoot
   regRoot = WScript.Arguments(0)
   If Right(regRoot, 1) = "\" Then
      regRoot = Left(regRoot, Len(regRoot) - 1)
   End If

   Dim xmlPath
   xmlPath = fso.GetAbsolutePathName(WScript.Arguments(1))
   If Right(xmlPath, 1) = "\" Then
      xmlPath = Left(xmlPath, Len(xmlPath) - 1)
   End If

   Dim regFile
   Dim genRegFile
   Dim regFileContents
   Set regFile = fso.OpenTextFile(myDir & "\SQLiteDesigner.reg", 1)
   Set genRegFile = fso.CreateTextFile(myDir & "\SQLiteDesigner.gen.reg", true)
   regFileContents = regFile.ReadAll()
   regFileContents = Replace(regFileContents, "%REGROOT%", regRoot)
   regFileContents = Replace(regFileContents, "%XMLPATH%", Replace(xmlPath, "\", "\\"))
   genRegFile.Write(regFileContents)
   genRegFile.Close()
   regFile.Close()

   Dim oExec
   Set oExec = WshShell.Exec("regedit /s """ & myDir & "\SQLiteDesigner.gen.reg""")
   Do While oExec.Status = 0
      WScript.Sleep(100)
   Loop

   fso.DeleteFile(myDir & "\SQLiteDesigner.gen.reg")

   Dim machineConfig
   Dim machineConfigFile
   Dim machineConfigPath
   
   machineConfigPath = fso.GetSpecialFolder(WindowsFolder).Path & "\Microsoft.NET\Framework\v2.0.50215\CONFIG"   
   Set machineConfigFile = fso.OpenTextFile(machineConfigPath & "\machine.config")
   machineConfig = machineConfigFile.ReadAll()
   machineConfigFile.Close()
   
   Dim n
   Dim x
   
   n = InStr(1, machineConfig, "System.Data.SQLite, Version=1", 1)
   
   If (n = 0) Then
     n = InStr(1, machineConfig, "</DbProviderFactories>", 1)
     If n > 0 Then
       n = InStrRev(machineConfig, vbCrLf, n, 1)
       If n > 0 Then
         machineConfig = Left(machineConfig, n + 1) & "      <add name=""SQLite Data Provider"" invariant=""System.Data.SQLite"" description="".Net Framework Data Provider for SQLite"" type=""System.Data.SQLite.SQLiteFactory, System.Data.SQLite, Version=" & SQLiteVersion & ", Culture=neutral, PublicKeyToken=db937bc2d44ff139"" />" & vbCrLf & Mid(machineConfig, n + 2)
       End If
     End If
   Else
     n = n + 27
     x = InStr(n, machineConfig, ",", 1)
     If x > 0 Then
       machineConfig = Left(machineConfig, n) & SQLiteVersion & Mid(machineConfig, x)
     End If
   End If
   
   Set machineConfigFile = fso.CreateTextFile(machineConfigPath & "\machine.config", true)
   machineConfigFile.Write(machineConfig)
   machineConfigFile.Close()
   
End Sub
