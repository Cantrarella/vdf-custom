param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$version = '1.23.2'
$modelHash = '3afdc8bc63b50558d6e5770f5b799bb82455c2311183a2de43803f343a29d917'
$target = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'ai'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('vdf-bundle-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $archive = Join-Path $temporary 'runtime.zip'
    Invoke-WebRequest "https://github.com/microsoft/onnxruntime/releases/download/v$version/onnxruntime-win-x64-$version.zip" -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $temporary
    $runtime = Join-Path $temporary "onnxruntime-win-x64-$version"
    Copy-Item -Path (Join-Path $runtime 'lib/*.dll') -Destination $target
    Copy-Item -LiteralPath (Join-Path $runtime 'LICENSE') -Destination (Join-Path $target 'ONNX-RUNTIME-LICENSE.txt')
    $model = Join-Path $temporary 'model.onnx'
    try {
        Invoke-WebRequest 'https://github.com/0x90d/videoduplicatefinder/releases/download/ai-models-v1/dinov2-small-int8.onnx' -OutFile $model
    } catch {
        Invoke-WebRequest 'https://huggingface.co/Xenova/dinov2-small/resolve/main/onnx/model_quantized.onnx' -OutFile $model
    }
    if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash.ToLowerInvariant() -ne $modelHash) {
        throw 'AI model SHA256 verification failed.'
    }
    Copy-Item -LiteralPath $model -Destination (Join-Path $target 'dinov2-small-int8.onnx')
    Invoke-WebRequest 'https://raw.githubusercontent.com/facebookresearch/dinov2/main/LICENSE' -OutFile (Join-Path $target 'DINOv2-LICENSE.txt')
    [IO.File]::WriteAllText((Join-Path $target 'runtime.version'), $version)
    Write-Host 'AI runtime and verified model bundled.'
} finally {
    # Only remove this invocation's explicitly created temporary directory.
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedTemporary.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($resolvedTemporary)).StartsWith('vdf-bundle-')) {
        throw 'Unexpected temporary directory; refusing cleanup.'
    }
    Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
}
