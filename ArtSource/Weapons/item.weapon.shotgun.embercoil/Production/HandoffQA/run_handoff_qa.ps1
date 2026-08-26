$ErrorActionPreference = "Stop"

$qaRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$productionRoot = Split-Path -Parent $qaRoot
$emissionScript = Join-Path $productionRoot "build_emission_mask.py"
$textureScript = Join-Path $qaRoot "build_unity_handoff_textures.py"
$renderScript = Join-Path $qaRoot "render_fbx_handoff_qa.py"
$finalizeScript = Join-Path $qaRoot "finalize_handoff_qa.py"

$pythonCommand = Get-Command python -ErrorAction Stop
$blenderCandidates = @(
    "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
    "C:\Program Files\Blender Foundation\Blender 5.0\blender.exe",
    "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe"
)
$blenderPath = $blenderCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $blenderPath) {
    throw "Blender 5.1/5.0/4.5 was not found in the expected installation folders."
}

& $pythonCommand.Source $emissionScript
if ($LASTEXITCODE -ne 0) { throw "Production global RGB emission mask regeneration failed." }

& $pythonCommand.Source $textureScript
if ($LASTEXITCODE -ne 0) { throw "Unity texture/emission validation failed." }

& $blenderPath --background --python $renderScript
if ($LASTEXITCODE -ne 0) { throw "FBX handoff render/geometry validation failed." }

& $pythonCommand.Source $finalizeScript
if ($LASTEXITCODE -ne 0) { throw "Handoff manifest/contact sheet finalization failed." }

Write-Output "Ember Coil handoff QA regenerated successfully: $qaRoot"
