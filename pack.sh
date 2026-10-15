#!/usr/bin/env bash
set -e

VERSION="${1:-0.1.0}"
OUTPUT_DIR="${2:-./artifacts}"

echo "=================================================="
echo " Packaging FryWorld.Sdk v${VERSION}"
echo "=================================================="

echo "1. Running automated test suite..."
dotnet test tests/FryWorld.Sdk.Tests/FryWorld.Sdk.Tests.csproj -c Release --verbosity normal

echo "2. Building & Packaging NuGet package..."
mkdir -p "${OUTPUT_DIR}"
dotnet pack src/FryWorld.Sdk/FryWorld.Sdk.csproj \
  -c Release \
  -p:PackageVersion="${VERSION}" \
  -p:Version="${VERSION}" \
  --output "${OUTPUT_DIR}"

echo "3. Verifying generated packages..."
ls -lh "${OUTPUT_DIR}"/*.nupkg "${OUTPUT_DIR}"/*.snupkg

echo "=================================================="
echo " Successfully generated packages in ${OUTPUT_DIR}"
echo "=================================================="
