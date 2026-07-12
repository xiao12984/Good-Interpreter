param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$RootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$FrontendDir = Join-Path $RootDir "frontend"
$BackendDir = Join-Path $RootDir "backend"
$LauncherDir = Join-Path $RootDir "launcher\GoodInterpreter.Launcher"
$InstallerBuildDir = Join-Path $PSScriptRoot "build"
$LauncherPublishDir = Join-Path $InstallerBuildDir "launcher"
$BackendPublishDir = Join-Path $InstallerBuildDir "backend"
$PyInstallerWorkDir = Join-Path $InstallerBuildDir "pyinstaller-work"
$InnoScriptPath = Join-Path $PSScriptRoot "GoodInterpreter.iss"

function Invoke-Step {
    param(
        [string]$Title,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
}

function Invoke-Native {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FileName failed with exit code $LASTEXITCODE."
    }
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw $Message
    }
}

function Get-InnoCompilerPath {
    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup compiler ISCC.exe was not found. Install Inno Setup 6 or add ISCC.exe to PATH."
}

Invoke-Step "Check build tools" {
    Invoke-Native "dotnet" @("--version")
    Invoke-Native "node" @("--version")
    Invoke-Native "npm" @("--version")
    Invoke-Native "py" @("-3", "--version")
    Invoke-Native "py" @("-3", "-m", "pip", "--version")
    Invoke-Native "py" @("-3", "-m", "PyInstaller", "--version")
}

Invoke-Step "Install backend Python dependencies" {
    Invoke-Native "py" @(
        "-3",
        "-m", "pip",
        "install",
        "-r", (Join-Path $BackendDir "requirements.txt")
    )

    Invoke-Native "py" @(
        "-3",
        "-c",
        "import aiohttp, websockets, dotenv, google.protobuf; print('backend dependencies import OK')"
    )
}

Invoke-Step "Build frontend static files: frontend/dist" {
    Push-Location $FrontendDir
    try {
        Invoke-Native "npm" @("run", "build")
    }
    finally {
        Pop-Location
    }
}

Invoke-Step "Build bundled backend: GoodInterpreter.Backend.exe" {
    New-Item -ItemType Directory -Force -Path $BackendPublishDir | Out-Null
    New-Item -ItemType Directory -Force -Path $PyInstallerWorkDir | Out-Null

    Invoke-Native "py" @(
        "-3",
        "-m", "PyInstaller",
        "--noconfirm",
        "--clean",
        "--onefile",
        "--console",
        "--name", "GoodInterpreter.Backend",
        "--paths", $BackendDir,
        "--collect-submodules", "google.protobuf",
        "--distpath", $BackendPublishDir,
        "--workpath", $PyInstallerWorkDir,
        "--specpath", $InstallerBuildDir,
        (Join-Path $BackendDir "run_backend.py")
    )
}

Invoke-Step "Publish launcher: GoodInterpreter.Launcher.exe" {
    New-Item -ItemType Directory -Force -Path $LauncherPublishDir | Out-Null

    Push-Location $LauncherDir
    try {
        Invoke-Native "dotnet" @(
            "publish",
            "-c", $Configuration,
            "-r", "win-x64",
            "--self-contained", "true",
            "-o", $LauncherPublishDir,
            "-p:PublishSingleFile=true",
            "-p:EnableCompressionInSingleFile=true"
        )
    }
    finally {
        Pop-Location
    }
}

Invoke-Step "Build installer: Good-Interpreter-Setup.exe" {
    Assert-PathExists (Join-Path $LauncherPublishDir "GoodInterpreter.Launcher.exe") "Launcher publish output was not found."
    Assert-PathExists (Join-Path $BackendPublishDir "GoodInterpreter.Backend.exe") "Backend executable was not found."
    Assert-PathExists (Join-Path $FrontendDir "dist\index.html") "Frontend dist was not found."

    $isccPath = Get-InnoCompilerPath
    Invoke-Native $isccPath @($InnoScriptPath)
}

Write-Host ""
Write-Host "Package complete: dist\Good-Interpreter-Setup.exe" -ForegroundColor Green
