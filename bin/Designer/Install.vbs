Const SQLiteVersion = "1.0.22.0"

Main

Sub Main()

   Dim WshShell
   Set WshShell = WScript.CreateObject("WScript.Shell")

   Dim GacPath
   Dim oExec
   GacPath = WshShell.RegRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\sdkInstallRootv2.0")
   
   Set oExec = WshShell.Exec(GacPath & "\bin\gacutil.exe -u System.Data.SQLite")
   Do While oExec.Status = 0
     WScript.Sleep(100)
   Loop
   
   Set oExec = WshShell.Exec(GacPath & "\bin\gacutil.exe -if ..\System.Data.SQLite.DLL")
   Do While oExec.Status = 0
     WScript.Sleep(100)
   Loop

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

   Set oExec = WshShell.Exec("regedit /s """ & myDir & "\SQLiteDesigner.gen.reg""")
   Do While oExec.Status = 0
      WScript.Sleep(100)
   Loop

   fso.DeleteFile(myDir & "\SQLiteDesigner.gen.reg")

   Dim machineConfigFile
   Dim machineConfig
   
   machineConfigFile = fso.GetSpecialFolder(WindowsFolder).Path & "\Microsoft.NET\Framework\v2.0.50727\CONFIG\machine.config"
   Set machineConfig = CreateObject("Microsoft.XMLDOM")
   machineConfig.load machineConfigFile
   
   Dim xmlNode
   Dim xmlParent
   
   Set xmlNode = machineConfig.selectSingleNode("configuration/system.data/DbProviderFactories/add[@invariant=""System.Data.SQLite""]")
   If xmlNode Is Nothing Then
     Set xmlParent = machineConfig.selectSingleNode("configuration/system.data/DbProviderFactories")
     Set xmlNode = machineConfig.createNode(1, "add", "")
     xmlNode.attributes.setNamedItem(machineConfig.createAttribute("name"))
     xmlNode.attributes.setNamedItem(machineConfig.createAttribute("invariant"))
     xmlNode.attributes.setNamedItem(machineConfig.createAttribute("description"))
     xmlNode.attributes.setNamedItem(machineConfig.createAttribute("type"))
     xmlParent.appendChild xmlNode
   End If
   
   xmlNode.attributes.getNamedItem("name").value = "SQLite Data Provider"
   xmlNode.attributes.getNamedItem("invariant").value = "System.Data.SQLite"
   xmlNode.attributes.getNamedItem("description").value = ".Net Framework Data Provider for SQLite"
   xmlNode.attributes.getNamedItem("type").value = "System.Data.SQLite.SQLiteFactory, System.Data.SQLite, Version=" & SQLiteVersion & ", Culture=neutral, PublicKeyToken=db937bc2d44ff139"

   machineConfig.save machineConfigFile
End Sub
