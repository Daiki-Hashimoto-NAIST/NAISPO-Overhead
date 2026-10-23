param(
    [string]$SdkPath,
    [switch]$Download
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$modelName = 'dnn_model_2_0_op11.onnx'
$modelBytes = 166778023L
$modelSha256 = '40BF149C8E3DC4CBA34CFF4F5C3DB6F5D5D4E6559D525DF1AA02C2E6C80F7D2D'
$packageName = 'microsoft.azure.kinect.bodytracking.1.1.2.nupkg'
$packageUri = 'https://api.nuget.org/v3-flatcontainer/microsoft.azure.kinect.bodytracking/1.1.2/microsoft.azure.kinect.bodytracking.1.1.2.nupkg'
$packageBytes = 198516805L
$packageSha256 = '6151FF7E01A623FA2C15F01D9DFC22457C1353C484939D560A6F57A481AD83BF'
$destination = Join-Path $PSScriptRoot ('Assets\StreamingAssets\AzureKinect\' + $modelName)
$cacheDirectory = Join-Path $PSScriptRoot 'Setup\BodyTracking'

function Test-ExpectedFile {
    param([string]$Path, [long]$Length, [string]$Sha256)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $Path).Length -ne $Length) { return $false }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha256
}

function Find-Model {
    param([string]$Root)
    if (-not $Root) { return $null }
    $candidates = @($Root)
    foreach ($relative in @(
        $modelName,
        ('tools\' + $modelName),
        ('sdk\windows-desktop\amd64\release\bin\' + $modelName),
        ('Azure Kinect Body Tracking SDK\tools\' + $modelName),
        ('Assets\StreamingAssets\AzureKinect\' + $modelName)
    )) {
        $candidates += Join-Path $Root $relative
    }
    foreach ($candidate in $candidates) {
        if (Test-ExpectedFile $candidate $modelBytes $modelSha256) {
            return (Get-Item -LiteralPath $candidate).FullName
        }
    }
    return $null
}

try {
    if (Test-ExpectedFile $destination $modelBytes $modelSha256) {
        Write-Host 'Body Tracking 1.1.2 model is already ready (SHA-256 verified).'
        Write-Host $destination
        exit 0
    }

    $modelSource = $null
    if ($SdkPath) {
        $modelSource = Find-Model $SdkPath
        if (-not $modelSource) {
            throw 'SdkPath does not contain the expected Body Tracking 1.1.2 model. Use an SDK folder or the full ONNX file path.'
        }
    } else {
        $parentDirectory = Split-Path -Parent $PSScriptRoot
        $searchRoots = @(
            (Join-Path $parentDirectory 'Naispo_shadow-main'),
            (Join-Path $parentDirectory 'Naispo_shadow-Arima'),
            (Join-Path $parentDirectory 'Setup\BodyTracking'),
            (Join-Path $PSScriptRoot 'Setup\BodyTracking')
        )
        if ($env:ProgramFiles) {
            $searchRoots += Join-Path $env:ProgramFiles 'Azure Kinect Body Tracking SDK'
            $searchRoots += Join-Path $env:ProgramFiles 'Azure Kinect Body Tracking SDK 1.1.2'
        }
        foreach ($root in $searchRoots) {
            $modelSource = Find-Model $root
            if ($modelSource) { break }
        }
    }

    if (-not $modelSource) {
        if (-not $Download) {
            throw 'Model not found locally. Run Setup-BodyTracking.cmd, or this script with -Download, to obtain it from the official Microsoft NuGet package. You can also pass -SdkPath to an installed/extracted SDK.'
        }
        New-Item -ItemType Directory -Path $cacheDirectory -Force | Out-Null
        $packagePath = Join-Path $cacheDirectory $packageName
        if (-not (Test-ExpectedFile $packagePath $packageBytes $packageSha256)) {
            Write-Host 'Downloading Microsoft.Azure.Kinect.BodyTracking 1.1.2 from NuGet.org (about 189 MiB)...'
            $partialPath = $packagePath + '.download'
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri $packageUri -OutFile $partialPath -TimeoutSec 600
            if (-not (Test-ExpectedFile $partialPath $packageBytes $packageSha256)) {
                throw 'The downloaded SDK package did not match the expected size/SHA-256. No model was installed.'
            }
            Move-Item -LiteralPath $partialPath -Destination $packagePath -Force
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
        $modelSource = Join-Path $cacheDirectory $modelName
        try {
            $entry = $archive.GetEntry('content/' + $modelName)
            if ($null -eq $entry -or $entry.Length -ne $modelBytes) {
                throw 'The SDK package does not contain the expected model.'
            }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $modelSource, $true)
        } finally {
            $archive.Dispose()
        }
    }

    if (-not (Test-ExpectedFile $modelSource $modelBytes $modelSha256)) {
        throw 'The model did not match the expected size/SHA-256. No model was installed.'
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $modelSource -Destination $destination -Force
    if (-not (Test-ExpectedFile $destination $modelBytes $modelSha256)) {
        throw 'Model verification failed after copying.'
    }
    Write-Host 'Body Tracking 1.1.2 model is ready (SHA-256 verified).'
    Write-Host $destination
    Write-Host 'Rebuild the Windows player in Unity to include this model.'
    exit 0
} catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 1
}
