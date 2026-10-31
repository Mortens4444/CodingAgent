<#
.SYNOPSIS
    Installs the toolchains the coding agent uses to build, test and validate projects.

.DESCRIPTION
    Uses winget (Windows 10/11). Tools already on PATH are skipped. After installing, open a new
    terminal so the updated PATH is picked up, then start the agent again.

.EXAMPLE
    ./scripts/setup-dev-env.ps1 -Languages C,Cpp,JavaScript
.EXAMPLE
    ./scripts/setup-dev-env.ps1 -Languages All -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('All', 'CSharp', 'Python', 'C', 'Cpp', 'Java', 'JavaScript', 'TypeScript', 'Html', 'Assembly', 'Pascal', 'Lisp')]
    [string[]] $Languages = @('All'),

    # Also install optional tools such as CMake and make.
    [switch] $IncludeOptional
)

$ErrorActionPreference = 'Stop'

# Keep in sync with Services/DevEnvironment/ToolchainCatalog.cs.
$tools = @{
    dotnet = @{ Commands = @('dotnet');               Id = 'Microsoft.DotNet.SDK.10' }
    python = @{ Commands = @('python', 'py');         Id = 'Python.Python.3.12' }
    gcc    = @{ Commands = @('gcc', 'g++');           Id = 'BrechtSanders.WinLibs.POSIX.UCRT' }
    make   = @{ Commands = @('make', 'mingw32-make'); Id = 'ezwinports.make';  Optional = $true }
    cmake  = @{ Commands = @('cmake');                Id = 'Kitware.CMake';    Optional = $true }
    jdk    = @{ Commands = @('javac');                Id = 'Microsoft.OpenJDK.21' }
    node   = @{ Commands = @('node', 'npm');          Id = 'OpenJS.NodeJS.LTS' }
    nasm   = @{ Commands = @('nasm');                 Id = 'NASM.NASM' }
    fpc    = @{ Commands = @('fpc');                  Id = 'FreePascal.FreePascalCompiler' }
    sbcl   = @{ Commands = @('sbcl');                 Id = 'SBCL.SBCL' }
}

$languageTools = @{
    CSharp     = @('dotnet')
    Python     = @('python')
    C          = @('gcc', 'make', 'cmake')
    Cpp        = @('gcc', 'make', 'cmake')
    Java       = @('jdk')
    JavaScript = @('node')
    TypeScript = @('node')
    Html       = @('node')
    Assembly   = @('nasm', 'gcc')
    Pascal     = @('fpc')
    Lisp       = @('sbcl')
}

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw 'winget was not found. Install "App Installer" from the Microsoft Store, or install the tools manually (see docs/DEV_ENVIRONMENT.md).'
}

$selected = if ($Languages -contains 'All') { $languageTools.Keys } else { $Languages }
$wanted = $selected | ForEach-Object { $languageTools[$_] } | Sort-Object -Unique

$installed = @()
foreach ($name in $wanted) {
    $tool = $tools[$name]
    if ($tool.Optional -and -not $IncludeOptional) {
        Write-Host "skip     $name (optional; use -IncludeOptional)"
        continue
    }

    $found = $tool.Commands | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
    if ($found) {
        Write-Host "present  $name ($((Get-Command $found).Source))"
        continue
    }

    if ($PSCmdlet.ShouldProcess($tool.Id, 'winget install')) {
        Write-Host "install  $name -> $($tool.Id)"
        winget install --id $tool.Id -e --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -ne 0) { Write-Warning "winget install $($tool.Id) failed with exit code $LASTEXITCODE" }
        else { $installed += $name }
    }
}

Write-Host ''
Write-Host 'Not installed by this script (per project, or no winget package):'
Write-Host '  Maven/Gradle  use the project wrapper (mvnw / gradlew), or "scoop install maven gradle"'
Write-Host '  TypeScript    npm install --save-dev typescript   (the agent falls back to npx -p typescript)'
Write-Host '  htmlhint      npm install --save-dev htmlhint      (optional; a built-in tag check is used without it)'
Write-Host '  pytest        python -m pip install pytest         (optional; unittest is used without it)'

if ($installed.Count -gt 0) {
    Write-Host ''
    Write-Host "Installed: $($installed -join ', '). Open a new terminal so PATH includes them."
}
