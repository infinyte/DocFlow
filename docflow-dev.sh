#!/bin/bash
# Run DocFlow CLI from local build (for development)

# Find dotnet - check common locations
if command -v dotnet &> /dev/null; then
    DOTNET="dotnet"
elif [ -f "/root/.dotnet/dotnet" ]; then
    DOTNET="/root/.dotnet/dotnet"
elif [ -f "$HOME/.dotnet/dotnet" ]; then
    DOTNET="$HOME/.dotnet/dotnet"
else
    echo "Error: dotnet not found"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
$DOTNET run --project "$SCRIPT_DIR/src/DocFlow.CLI/DocFlow.CLI.csproj" -- "$@"
