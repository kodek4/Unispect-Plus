# build.ps1 - A script to prepare Unispect for release.

# Exit immediately if a command fails
$ErrorActionPreference = "Stop"

# --- Configuration ---
$SolutionFile = "Unispect.sln"
$ReleaseConfiguration = "Release"
$Architecture = "win-x64"
$RootPath = Get-Location
$ReleasePath = Join-Path -Path $RootPath -ChildPath "Release"

# --- Paths for each project ---
$GuiProject = "Unispect/Unispect.csproj"
$CliProject = "Unispect.CLI/Unispect.CLI.csproj"
$SdkProject = "Unispect.SDK/Unispect.SDK.csproj"

# --- Output Directories ---
$GuiPublishPath = Join-Path -Path $RootPath -ChildPath "Unispect/bin/$ReleaseConfiguration/net9.0-windows/$Architecture/publish"
$CliPublishPath = Join-Path -Path $RootPath -ChildPath "Unispect.CLI/bin/$ReleaseConfiguration/net9.0-windows/$Architecture/publish"
$SdkBuildPath = Join-Path -Path $RootPath -ChildPath "Unispect.SDK/bin/$ReleaseConfiguration/net9.0-windows"

# --- Staging Directories ---
$GuiStagePath = Join-Path -Path $ReleasePath -ChildPath "Unispect-GUI"
$CliStagePath = Join-Path -Path $ReleasePath -ChildPath "Unispect-CLI"
$SdkStagePath = Join-Path -Path $ReleasePath -ChildPath "Unispect-SDK"


# --- Main ---

# 1. Clean up previous release directory
Write-Host "Cleaning up previous release..."
if (Test-Path $ReleasePath) {
    Remove-Item -Path $ReleasePath -Recurse -Force
}
New-Item -Path $ReleasePath -ItemType Directory | Out-Null
New-Item -Path $GuiStagePath -ItemType Directory | Out-Null
New-Item -Path $CliStagePath -ItemType Directory | Out-Null
New-Item -Path $SdkStagePath -ItemType Directory | Out-Null

# 2. Build and Publish all projects
Write-Host "Building and publishing solution..."
dotnet clean $SolutionFile
# Publish the GUI as framework-dependent for a smaller footprint.
# This requires the user to have the .NET Desktop Runtime installed.
dotnet publish $GuiProject -c $ReleaseConfiguration -r $Architecture --self-contained false
# The CLI will remain self-contained for maximum portability.
dotnet publish $CliProject -c $ReleaseConfiguration -r $Architecture --self-contained true /p:PublishSingleFile=true
dotnet build $SdkProject -c $ReleaseConfiguration

# 3. Stage GUI files
Write-Host "Staging GUI application..."
Copy-Item -Path "$GuiPublishPath\*" -Destination $GuiStagePath -Recurse

# 4. Stage CLI file
Write-Host "Staging CLI application..."
Copy-Item -Path (Join-Path $CliPublishPath "unispect-cli.exe") -Destination $CliStagePath

# 5. Stage SDK files
Write-Host "Staging SDK..."
Copy-Item -Path (Join-Path $SdkBuildPath "Unispect.SDK.dll") -Destination $SdkStagePath
Copy-Item -Path (Join-Path $SdkBuildPath "Unispect.SDK.deps.json") -Destination $SdkStagePath

# 6. Clean up staged GUI folder before zipping
Write-Host "Cleaning up GUI package..."
# Remove debug symbols. createdump.exe is not included in framework-dependent builds.
Get-ChildItem -Path $GuiStagePath -Include *.pdb -Recurse | Remove-Item -ErrorAction SilentlyContinue

# 7. Create Zip Archives
Write-Host "Creating release archives..."
Compress-Archive -Path "$GuiStagePath\*" -DestinationPath (Join-Path $ReleasePath "Unispect-GUI.zip")
Compress-Archive -Path "$CliStagePath\*" -DestinationPath (Join-Path $ReleasePath "Unispect-CLI.zip")
Compress-Archive -Path "$SdkStagePath\*" -DestinationPath (Join-Path $ReleasePath "Unispect-SDK.zip")

# 8. Final Cleanup
Write-Host "Cleaning up staging directories..."
Remove-Item -Path $GuiStagePath -Recurse -Force
Remove-Item -Path $CliStagePath -Recurse -Force
Remove-Item -Path $SdkStagePath -Recurse -Force

Write-Host "Build complete! Release packages are in the '$ReleasePath' directory."
Write-Host "You can now upload the .zip files from this directory to your GitHub Release." 