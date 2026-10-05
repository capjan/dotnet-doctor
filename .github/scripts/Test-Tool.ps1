param(
    [Parameter(Mandatory)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
$ExecutablePath = (Resolve-Path $ExecutablePath).Path
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "dotnet-doctor-ci-$([Guid]::NewGuid().ToString('N'))"
$repository = Join-Path $temporaryDirectory 'repository with spaces'
New-Item -ItemType Directory -Path $repository | Out-Null
$env:GIT_CONFIG_NOSYSTEM = '1'
$env:GIT_CONFIG_GLOBAL = Join-Path $temporaryDirectory 'gitconfig'
$env:GIT_CONFIG_COUNT = '0'
Set-Content -Path $env:GIT_CONFIG_GLOBAL -Value ''

Push-Location $repository
try {
    git init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the test repository.' }

    $version = & $ExecutablePath --version
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^dotnet-doctor \d+\.\d+\.\d+$') {
        throw 'The version command failed.'
    }

    & $ExecutablePath --help
    if ($LASTEXITCODE -ne 0) { throw 'The help command failed.' }

    $expectedExitCode = if ($IsWindows) { 1 } else { 0 }
    & $ExecutablePath fix
    if ($LASTEXITCODE -ne $expectedExitCode) { throw 'The repair command failed.' }

    $defaultOutput = & $ExecutablePath
    if ($LASTEXITCODE -ne $expectedExitCode) { throw 'The default diagnostic failed.' }
    $allOutput = & $ExecutablePath diagnose all
    if ($LASTEXITCODE -ne $expectedExitCode) { throw 'The all diagnostic failed.' }
    if (($defaultOutput -join "`n") -ne ($allOutput -join "`n")) {
        throw 'The default command and diagnose all returned different output.'
    }

    & $ExecutablePath diagnose commit-msg
    if ($LASTEXITCODE -ne 0) { throw 'The installed Conventional Commits hook failed.' }
}
finally {
    Pop-Location
    Remove-Item -Path $temporaryDirectory -Recurse -Force
}
