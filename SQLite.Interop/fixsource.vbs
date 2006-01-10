' VBScript source code
Main

Sub Main()
  Dim WshShell
  Set WshShell = WScript.CreateObject("WScript.Shell")
  
  Dim fso
  Set fso = WScript.CreateObject("Scripting.FileSystemObject")
  
  Dim srcFile
  Dim srcFileContents
  dim newFileContents
  
  Set srcFile = fso.OpenTextFile("src\select.c", 1)
  
  srcFileContents = srcFile.ReadAll()
  srcFile.Close()
  
  newFileContents = Replace(srcFileContents, "static void generateColumnNames(", "static void _generateColumnNames(")
  
  If (newFileContents <> srcFileContents) Then
    WScript.StdOut.WriteLine "Updating select.c"
    Set srcFile = fso.CreateTextFile("src\select.c", true)
    srcFile.Write(newFileContents)
    srcFile.Close()
  End If
  
  Set srcFile = fso.OpenTextFile("src\tokenize.c", 1)
  
  srcFileContents = srcFile.ReadAll()
  srcFile.Close()
  
  newFileContents = Replace(srcFileContents, "    case ':': {", "    case '@': case ':': {")

  If (newFileContents <> srcFileContents) Then
    WScript.StdOut.WriteLine "Updating tokenize.c"
    Set srcFile = fso.CreateTextFile("src\tokenize.c", true)
    srcFile.Write(newFileContents)
    srcFile.Close()
  End If

End Sub
  