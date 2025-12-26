#!/bin/bash
# Script to run QuicFlowClient on macOS with libmsquic support

# Ensure libmsquic is found by .NET
export DYLD_LIBRARY_PATH=/opt/homebrew/lib:$DYLD_LIBRARY_PATH

echo "Starting QuicFlowClient with libmsquic support..."
dotnet run
