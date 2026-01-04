#!/bin/bash
# DocFlow Installation Script
# Installs DocFlow as a global .NET tool

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_PROJECT="$SCRIPT_DIR/src/DocFlow.CLI"

echo ""
echo "  ____              _____ _"
echo " |  _ \  ___   ___ |  ___| | _____      __"
echo " | | | |/ _ \ / __|| |_  | |/ _ \ \ /\ / /"
echo " | |_| | (_) | (__ |  _| | | (_) \ V  V /"
echo " |____/ \___/ \___||_|   |_|\___/ \_/\_/"
echo ""
echo " Intelligent Documentation and Modeling Toolkit"
echo ""

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 8.0 or later."
    echo "   https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✓ Found .NET SDK: $DOTNET_VERSION"

# Build the project
echo ""
echo "📦 Building DocFlow..."
dotnet build "$SCRIPT_DIR" -c Release --nologo -v q

# Pack the CLI tool
echo ""
echo "📦 Packing CLI tool..."
dotnet pack "$CLI_PROJECT" -c Release --nologo -v q -o "$SCRIPT_DIR/nupkg"

# Uninstall existing version if present
echo ""
echo "🔄 Checking for existing installation..."
if dotnet tool list -g | grep -q "docflow"; then
    echo "   Removing existing version..."
    dotnet tool uninstall -g DocFlow.CLI 2>/dev/null || true
fi

# Install globally
echo ""
echo "🚀 Installing DocFlow globally..."
dotnet tool install -g DocFlow.CLI --add-source "$SCRIPT_DIR/nupkg" --no-cache

echo ""
echo "✅ DocFlow installed successfully!"
echo ""

# Check if ~/.dotnet/tools is in PATH
if [[ ":$PATH:" != *":$HOME/.dotnet/tools:"* ]]; then
    echo "⚠️  Note: Add ~/.dotnet/tools to your PATH:"
    echo "   export PATH=\"\$PATH:\$HOME/.dotnet/tools\""
    echo ""
fi

# API Key Configuration
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🔑 API Key Configuration (for whiteboard scanning)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "The 'docflow scan' command requires an Anthropic API key."
echo "Get your key at: https://console.anthropic.com/settings/keys"
echo ""

read -p "Would you like to configure your API key now? (y/N): " CONFIGURE_KEY

if [[ "$CONFIGURE_KEY" =~ ^[Yy]$ ]]; then
    echo ""
    read -sp "Enter your Anthropic API key: " API_KEY
    echo ""
    
    if [ -n "$API_KEY" ]; then
        echo ""
        echo "How would you like to store the key?"
        echo "  1) Environment variable (added to ~/.bashrc)"
        echo "  2) Config file (~/.docflow/config.json)"
        echo "  3) Both"
        echo ""
        read -p "Choose option (1/2/3): " STORAGE_OPTION
        
        case $STORAGE_OPTION in
            1)
                echo "" >> ~/.bashrc
                echo "# DocFlow API Key" >> ~/.bashrc
                echo "export ANTHROPIC_API_KEY='$API_KEY'" >> ~/.bashrc
                echo ""
                echo "✓ API key added to ~/.bashrc"
                echo "  Run 'source ~/.bashrc' or restart your terminal."
                ;;
            2)
                mkdir -p ~/.docflow
                cat > ~/.docflow/config.json << EOF
{
  "anthropicApiKey": "$API_KEY"
}
EOF
                chmod 600 ~/.docflow/config.json
                echo ""
                echo "✓ API key saved to ~/.docflow/config.json"
                ;;
            3)
                # Environment variable
                echo "" >> ~/.bashrc
                echo "# DocFlow API Key" >> ~/.bashrc
                echo "export ANTHROPIC_API_KEY='$API_KEY'" >> ~/.bashrc
                
                # Config file
                mkdir -p ~/.docflow
                cat > ~/.docflow/config.json << EOF
{
  "anthropicApiKey": "$API_KEY"
}
EOF
                chmod 600 ~/.docflow/config.json
                echo ""
                echo "✓ API key added to ~/.bashrc"
                echo "✓ API key saved to ~/.docflow/config.json"
                echo "  Run 'source ~/.bashrc' or restart your terminal."
                ;;
            *)
                echo "Skipped. You can configure later."
                ;;
        esac
    fi
else
    echo ""
    echo "You can configure your API key later:"
    echo ""
    echo "  Option 1: Environment variable"
    echo "    export ANTHROPIC_API_KEY='your-key'"
    echo ""
    echo "  Option 2: Config file (~/.docflow/config.json)"
    echo '    {"anthropicApiKey": "your-key"}'
    echo ""
    echo "  Option 3: Project config (./docflow.json in your project)"
    echo '    {"anthropicApiKey": "your-key"}'
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Usage:"
echo "  docflow diagram <file.cs>     Generate Mermaid from C#"
echo "  docflow codegen <file.mmd>    Generate C# from Mermaid"
echo "  docflow scan <image>          Scan whiteboard photo"
echo "  docflow --help                Show all commands"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
