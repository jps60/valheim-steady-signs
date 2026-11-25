param([Parameter(Mandatory=$true)][string]$ValheimDirectory, [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $managed = Join-Path (Resolve-Path -LiteralPath $ValheimDirectory).Path 'valheim_Data/Managed'
    $projectPath = 'src/DeliberateInteract.csproj'
    [xml]$project = Get-Content $projectPath
    $version = [string]$project.Project.PropertyGroup.Version
    $author = [string]$project.Project.PropertyGroup.Authors
    if (!(Select-String -LiteralPath src/Plugin.cs -SimpleMatch -Pattern ('private const string Version = "' + $version + '";')) -or
        !(Select-String -LiteralPath README.md -SimpleMatch -Pattern "# Steady Signs $version")) {
        throw 'Plugin, project, and README versions must match.'
    }
    & $Dotnet restore $projectPath --locked-mode "-p:ValheimManaged=$managed"
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $Dotnet build $projectPath -c Release --no-restore -warnaserror "-p:ValheimManaged=$managed"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    $package = New-Item -ItemType Directory -Force 'dist/package'
    $dll = (Resolve-Path 'src/bin/Release/net462/DeliberateInteract.dll').Path
    if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $version) {
        throw 'Assembly and package versions differ.'
    }
    Copy-Item -LiteralPath $dll,'README.md','LICENSE','icon.png' -Destination $package.FullName
    $manifest = [ordered]@{
        name = 'SteadySigns'
        author = $author
        version_number = $version
        website_url = 'https://github.com/jps60/valheim-deliberate-interact'
        description = 'Shift + E edits signs and portal tags. Hold 1-8 to mount stand items and Hold E to remove them.'
        dependencies = @('denikson-BepInExPack_Valheim-5.4.2360')
    }
    [IO.File]::WriteAllText((Join-Path $package.FullName 'manifest.json'),
        ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $files = @('DeliberateInteract.dll','README.md','LICENSE','icon.png','manifest.json')
    $archive = Join-Path (Get-Location) "dist/SteadySigns-$version.zip"
    Compress-Archive -LiteralPath @($files | ForEach-Object { Join-Path $package.FullName $_ }) -DestinationPath $archive -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        if (Compare-Object ($files | Sort-Object) ($zip.Entries.FullName | Sort-Object)) {
            throw 'Package contents differ from the expected files.'
        }
    } finally { $zip.Dispose() }
    Write-Output "Package: $archive"
} finally { Pop-Location }
