<#
.SYNOPSIS
    Start, stop, build, or test the Credit Plus CRM.

.DESCRIPTION
    The API holds a lock on its own executable while running, so a plain
    `dotnet build` fails with MSB3027 whenever the app is up. This script stops
    the API first, which is the only reliable way round it on Windows.

.EXAMPLE
    ./dev.ps1 start     # run the API and the web app
    ./dev.ps1 stop      # stop both
    ./dev.ps1 build     # build everything (stops the API first)
    ./dev.ps1 test      # run the test suite (stops the API first)
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet('start', 'stop', 'build', 'test', 'restart')]
    [string]$Command = 'start'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$api = Join-Path $root 'src/CreditPlus.Crm.Api'
$web = Join-Path $root 'src/crm-web'

function Stop-Api {
    $running = Get-Process -Name 'CreditPlus.Crm.Api' -ErrorAction SilentlyContinue
    if ($running) {
        $running | Stop-Process -Force
        # Windows releases the file handle a moment after the process exits.
        Start-Sleep -Seconds 2
        Write-Host '  API stopped.' -ForegroundColor DarkGray
    }
}

function Stop-Web {
    # The dev server is a node process holding port 4200.
    $owners = Get-NetTCPConnection -LocalPort 4200 -State Listen -ErrorAction SilentlyContinue
    foreach ($owner in $owners) {
        Stop-Process -Id $owner.OwningProcess -Force -ErrorAction SilentlyContinue
    }
    if ($owners) { Write-Host '  Web server stopped.' -ForegroundColor DarkGray }
}

switch ($Command) {
    'stop' {
        Write-Host 'Stopping...' -ForegroundColor Cyan
        Stop-Api
        Stop-Web
    }

    'build' {
        Write-Host 'Building...' -ForegroundColor Cyan
        Stop-Api
        dotnet build (Join-Path $root 'CreditPlus.Crm.slnx')
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Push-Location $web
        try { npx ng build } finally { Pop-Location }
    }

    'test' {
        Write-Host 'Testing...' -ForegroundColor Cyan
        Stop-Api
        dotnet test (Join-Path $root 'CreditPlus.Crm.slnx') --nologo
    }

    { $_ -in 'start', 'restart' } {
        # Only the API is started here. Launching the Angular dev server through
        # Start-Process proved unreliable on this machine -- it exits before the
        # port opens -- so it is left to a terminal of your own rather than
        # pretending to have started something that has not.
        Write-Host 'Starting the API...' -ForegroundColor Cyan
        Stop-Api

        Start-Process -FilePath 'dotnet' -ArgumentList 'run', '--launch-profile', 'http' `
            -WorkingDirectory $api -WindowStyle Minimized

        Write-Host '  API : http://localhost:5233' -ForegroundColor Green
        Write-Host ''
        Write-Host '  Now start the web app in its own terminal:' -ForegroundColor Yellow
        Write-Host '      cd src/crm-web && npx ng serve' -ForegroundColor White
        Write-Host ''
        Write-Host '  Then open http://localhost:4200' -ForegroundColor Green
        Write-Host '  Sign in with admin@creditplus.local / Admin123!' -ForegroundColor DarkGray
    }
}
