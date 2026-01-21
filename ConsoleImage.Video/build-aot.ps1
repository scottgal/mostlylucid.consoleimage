$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\Launch-VsDevShell.ps1"
dotnet publish E:\source\vectorascii\ConsoleImage\ConsoleImage.Video\ConsoleImage.Video.csproj -c Release -r win-x64
