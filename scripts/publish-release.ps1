param([string]$NotesPath = '')
$ErrorActionPreference = 'Stop'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
if (git status --porcelain) { throw 'Commit the completed source changes before publishing a release.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/DeliberateInteract.csproj')
$Version = [string]$project.Project.PropertyGroup.Version
$archive = Join-Path $projectRoot "dist/DeliberateInteract-$Version.zip"
if (-not (Test-Path -LiteralPath $archive)) { throw 'Build archive is missing.' }
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$archiveSize = (Get-Item -LiteralPath $archive).Length
$notes = if ($NotesPath) { [IO.File]::ReadAllText([IO.Path]::GetFullPath($NotesPath)) } else {
    "Deliberate Interact $Version. Import the attached ZIP as a local mod in r2modman. Restart Valheim to load the update. Shift + E edits signs and portal tags. Hold the item's hotbar key (1-8) to mount it, or Hold E to remove it."
}
$notes += "`r`n`r`nSHA-256: $archiveHash"
$head = (git rev-parse HEAD).Trim()
$reference = "refs/tags/v$Version"
$remote = (git ls-remote origin $reference "$reference^{}")
$remoteHeads = @($remote | ForEach-Object { ($_ -split '\s+')[0] })
if ($LASTEXITCODE -ne 0 -or $remoteHeads -notcontains $head) { throw 'Push the matching release commit/tag before publishing.' }
$credentialFields = @{}
if ($env:GITHUB_ACTIONS -eq 'true' -and $env:GITHUB_TOKEN) {
    if ($env:GITHUB_REPOSITORY -cne 'jps60/valheim-deliberate-interact') { throw 'Unexpected CI repository.' }
    $credentialFields.password = $env:GITHUB_TOKEN
} else {
    $credentialResult = "protocol=https`nhost=github.com`n`n" | git -c credential.interactive=never credential fill 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'GitHub authentication is unavailable.' }
    foreach ($line in $credentialResult) {
        if ($line -match '^([^=]+)=(.*)$') { $credentialFields[$matches[1]] = $matches[2] }
    }
}
if (-not $credentialFields.password) { throw 'GitHub authentication did not supply a credential.' }
$headers = @{ Authorization = ('Bearer ' + $credentialFields.password); Accept = 'application/vnd.github+json'; 'User-Agent' = 'DeliberateInteract-release'; 'X-GitHub-Api-Version' = '2022-11-28' }
$repoApi = 'https://api.github.com/repos/jps60/valheim-deliberate-interact'
if ($env:GITHUB_ACTIONS -ne 'true') {
    $account = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers
    if ($account.login -ne 'jps60') { throw 'Unexpected authenticated GitHub account.' }
}
$tag = 'v' + $Version
$release = $null
try { $release = Invoke-RestMethod -Uri ($repoApi + '/releases/tags/' + $tag) -Headers $headers }
catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
if (-not $release) {
    $existingReleases = Invoke-RestMethod -Uri ($repoApi + '/releases?per_page=100') -Headers $headers
    $release = @($existingReleases | Where-Object { $_.tag_name -eq $tag }) | Select-Object -First 1
}
if (-not $release) {
    $title = "Deliberate Interact $Version"
    $body = @{ tag_name = $tag; target_commitish = $head; name = $title; body = $notes; draft = $true; prerelease = $false } | ConvertTo-Json -Depth 5
    $release = Invoke-RestMethod -Method Post -Uri ($repoApi + '/releases') -Headers $headers -ContentType 'application/json' -Body $body
}
$assetName = [IO.Path]::GetFileName($archive)
$asset = @($release.assets | Where-Object { $_.name -eq $assetName }) | Select-Object -First 1
if (-not $asset) {
    $uploadBase = $release.upload_url -replace '\{.*$', ''
    $uploadUri = $uploadBase + '?name=' + [Uri]::EscapeDataString($assetName)
    $asset = Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -ContentType 'application/zip' -InFile $archive
}
if ($asset.size -ne $archiveSize) { throw 'Uploaded archive size does not match the local build.' }
if ($asset.digest -and $asset.digest -ne ('sha256:' + $archiveHash.ToLowerInvariant())) { throw 'Uploaded archive digest does not match the local build.' }
if ($release.draft) {
    $publication = @{ draft = $false; make_latest = 'true' } | ConvertTo-Json
    $release = Invoke-RestMethod -Method Patch -Uri ($repoApi + '/releases/' + $release.id) -Headers $headers -ContentType 'application/json' -Body $publication
}
$publicHeaders = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'DeliberateInteract-public-release-check' }
$published = Invoke-RestMethod -Uri ($repoApi + '/releases/tags/' + $tag) -Headers $publicHeaders
$publicAsset = @($published.assets | Where-Object { $_.name -eq $assetName }) | Select-Object -First 1
if ($published.draft -or -not $publicAsset) { throw 'Published asset is not publicly visible.' }
$verifyRoot = Join-Path $projectRoot '.tools/release-verification'
New-Item -ItemType Directory -Force $verifyRoot | Out-Null
$downloadPath = Join-Path $verifyRoot ('verified-' + $assetName)
Invoke-WebRequest -UseBasicParsing -Uri $publicAsset.browser_download_url -OutFile $downloadPath
if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Public download checksum mismatch.' }
Write-Output ('Published release: ' + $published.html_url)
Write-Output ('Verified public ZIP: ' + $publicAsset.browser_download_url)
Write-Output ('SHA-256: ' + $archiveHash)
$credentialFields.Clear()
Remove-Variable credentialResult,headers -ErrorAction SilentlyContinue
