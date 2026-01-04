# DocFlow Installation Script (Windows)
# Installs DocFlow as a global .NET tool

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$CliProject = Join-Path $ScriptDir "src\DocFlow.CLI"

Write-Host ""
Write-Host "  ____              _____ _" -ForegroundColor Cyan
Write-Host " |  _ \  ___   ___ |  ___| | _____      __" -ForegroundColor Cyan
Write-Host " | | | |/ _ \ / __|| |_  | |/ _ \ \ /\ / /" -ForegroundColor Cyan
Write-Host " | |_| | (_) | (__ |  _| | | (_) \ V  V /" -ForegroundColor Cyan
Write-Host " |____/ \___/ \___||_|   |_|\___/ \_/\_/" -ForegroundColor Cyan
Write-Host ""
Write-Host " Intelligent Documentation and Modeling Toolkit" -ForegroundColor Gray
Write-Host ""

# Check for .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ Found .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK not found. Please install .NET 8.0 or later." -ForegroundColor Red
    Write-Host "   https://dotnet.microsoft.com/download"
    exit 1
}

# Build the project
Write-Host ""
Write-Host "📦 Building DocFlow..." -ForegroundColor Yellow
dotnet build $ScriptDir -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Pack the CLI tool
Write-Host ""
Write-Host "📦 Packing CLI tool..." -ForegroundColor Yellow
$nupkgDir = Join-Path $ScriptDir "nupkg"
dotnet pack $CliProject -c Release --nologo -v q -o $nupkgDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Uninstall existing version if present
Write-Host ""
Write-Host "🔄 Checking for existing installation..." -ForegroundColor Yellow
$existingTool = dotnet tool list -g | Select-String "docflow"
if ($existingTool) {
    Write-Host "   Removing existing version..." -ForegroundColor Gray
    dotnet tool uninstall -g DocFlow.CLI 2>$null
}

# Install globally
Write-Host ""
Write-Host "🚀 Installing DocFlow globally..." -ForegroundColor Yellow
dotnet tool install -g DocFlow.CLI --add-source $nupkgDir --no-cache
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "✅ DocFlow installed successfully!" -ForegroundColor Green
Write-Host ""

# Check PATH
$toolsPath = Join-Path $env:USERPROFILE ".dotnet\tools"
if ($env:PATH -notlike "*$toolsPath*") {
    Write-Host "⚠️  Note: You may need to restart your terminal or add to PATH:" -ForegroundColor Yellow
    Write-Host "   $toolsPath" -ForegroundColor Gray
    Write-Host ""
}

# API Key Configuration
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host "🔑 API Key Configuration (for whiteboard scanning)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host ""
Write-Host "The 'docflow scan' command requires an Anthropic API key."
Write-Host "Get your key at: https://console.anthropic.com/settings/keys" -ForegroundColor Blue
Write-Host ""

$configureKey = Read-Host "Would you like to configure your API key now? (y/N)"

if ($configureKey -eq 'y' -or $configureKey -eq 'Y') {
    Write-Host ""
    $apiKey = Read-Host "Enter your Anthropic API key" -AsSecureString
    $apiKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey))
    
    if ($apiKeyPlain) {
        Write-Host ""
        Write-Host "How would you like to store the key?"
        Write-Host "  1) Environment variable (user-level, persistent)"
        Write-Host "  2) Config file (~/.docflow/config.json)"
        Write-Host "  3) Both"
        Write-Host ""
        $storageOption = Read-Host "Choose option (1/2/3)"
        
        switch ($storageOption) {
            "1" {
                [Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", $apiKeyPlain, "User")
                $env:ANTHROPIC_API_KEY = $apiKeyPlain
                Write-Host ""
                Write-Host "✓ API key saved as user environment variable" -ForegroundColor Green
                Write-Host "  Restart your terminal for changes to take effect." -ForegroundColor Gray
            }
            "2" {
                $configDir = Join-Path $env:USERPROFILE ".docflow"
                if (-not (Test-Path $configDir)) {
                    New-Item -ItemType Directory -Path $configDir | Out-Null
                }
                $configFile = Join-Path $configDir "config.json"
                $configContent = @{
                    anthropicApiKey = $apiKeyPlain
                } | ConvertTo-Json
                $configContent | Out-File -FilePath $configFile -Encoding UTF8
                Write-Host ""
                Write-Host "✓ API key saved to $configFile" -ForegroundColor Green
            }
            "3" {
                # Environment variable
                [Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", $apiKeyPlain, "User")
                $env:ANTHROPIC_API_KEY = $apiKeyPlain
                
                # Config file
                $configDir = Join-Path $env:USERPROFILE ".docflow"
                if (-not (Test-Path $configDir)) {
                    New-Item -ItemType Directory -Path $configDir | Out-Null
                }
                $configFile = Join-Path $configDir "config.json"
                $configContent = @{
                    anthropicApiKey = $apiKeyPlain
                } | ConvertTo-Json
                $configContent | Out-File -FilePath $configFile -Encoding UTF8
                
                Write-Host ""
                Write-Host "✓ API key saved as user environment variable" -ForegroundColor Green
                Write-Host "✓ API key saved to $configFile" -ForegroundColor Green
                Write-Host "  Restart your terminal for env changes to take effect." -ForegroundColor Gray
            }
            default {
                Write-Host "Skipped. You can configure later." -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host ""
    Write-Host "You can configure your API key later:" -ForegroundColor White
    Write-Host ""
    Write-Host "  Option 1: Environment variable" -ForegroundColor Gray
    Write-Host '    $env:ANTHROPIC_API_KEY = "your-key"' -ForegroundColor DarkGray
    Write-Host "    Or set permanently via System Properties > Environment Variables" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Option 2: Config file (~/.docflow/config.json)" -ForegroundColor Gray
    Write-Host '    {"anthropicApiKey": "your-key"}' -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Option 3: Project config (./docflow.json in your project)" -ForegroundColor Gray
    Write-Host '    {"anthropicApiKey": "your-key"}' -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host "Usage:" -ForegroundColor White
Write-Host "  docflow diagram <file.cs>     Generate Mermaid from C#" -ForegroundColor Gray
Write-Host "  docflow codegen <file.mmd>    Generate C# from Mermaid" -ForegroundColor Gray
Write-Host "  docflow scan <image>          Scan whiteboard photo" -ForegroundColor Gray
Write-Host "  docflow --help                Show all commands" -ForegroundColor Gray
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host ""
