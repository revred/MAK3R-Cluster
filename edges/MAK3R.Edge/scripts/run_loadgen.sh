#!/usr/bin/env bash
set -euo pipefail
export Edge__LoadGen__Enabled=true
export Edge__LoadGen__Machines=${1:-1000}
export Edge__LoadGen__RatePerMachineHz=${2:-0.5}
dotnet run --project src/Mak3r.Edge/Mak3r.Edge.csproj
