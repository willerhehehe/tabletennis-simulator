#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
if command -v dotnet >/dev/null 2>&1; then
    runner="$(command -v dotnet)"
elif [ -x .tools/dotnet/dotnet ]; then
    runner="$(pwd)/.tools/dotnet/dotnet"
else
    echo '需要 .NET 8 SDK，或在 Unity 中选择「乒乓球 → 运行物理检查」。' >&2
    exit 1
fi
mkdir -p Artifacts .tools/dotnet-home
export DOTNET_CLI_HOME="$(pwd)/.tools/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export NUGET_PACKAGES="$(pwd)/.tools/nuget"
"$runner" run --project Checks/Checks.csproj > Artifacts/physics-checks.txt
cat Artifacts/physics-checks.txt
