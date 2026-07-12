; Good-Interpreter Windows 一键安装包脚本
; 使用方式：先执行 installer\package.ps1 生成前端、后端和启动器产物，再用 Inno Setup 编译本文件。

#define MyAppName "Good-Interpreter"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Good-Interpreter"
#define MyAppExeName "GoodInterpreter.Launcher.exe"
#define MyBackendExeName "GoodInterpreter.Backend.exe"
#define SourceRoot ".."
#define LauncherPublishDir "build\launcher"
#define BackendPublishDir "build\backend"

[Setup]
AppId={{6A8D4C5B-76E2-4C0F-9D65-4C5A74A6F7E2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Good-Interpreter
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Good-Interpreter-Setup
SetupIconFile=..\launcher\GoodInterpreter.Launcher\Assets\GoodInterpreter.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
; 启动器发布产物。
Source: "{#LauncherPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; PyInstaller 后端 exe，目标电脑无需安装 Python。
Source: "{#BackendPublishDir}\{#MyBackendExeName}"; DestDir: "{app}"; Flags: ignoreversion

; 火山 AST protobuf 资源，后端 exe 运行时从安装目录加载。
Source: "{#SourceRoot}\backend\ast_python\*"; DestDir: "{app}\backend\ast_python"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "__pycache__\*,*.pyc,output\*,test_audio.wav"

; 前端静态产物，由后端 3100 端口直接托管。
Source: "{#SourceRoot}\frontend\dist\*"; DestDir: "{app}\frontend\dist"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Good-Interpreter"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Good-Interpreter"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 Good-Interpreter"; Flags: nowait postinstall skipifsilent
