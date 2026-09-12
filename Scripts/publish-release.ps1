param(
    [string]$TagName = "",
    [string]$ReleaseTitle = "",
    [string]$RemoteName = "origin",
    [string]$BranchName = "main",
    [string]$ChangesFile = "Docs\RELEASE_CHANGES.md",
    [switch]$Publish,
    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$SkipBuild,
    [switch]$AllowDirty,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$releasePublicDir = Join-Path $repoRoot "Release\Public"
$releaseRoot = Join-Path $repoRoot "Release\GitHub"

function Resolve-ToolPath {
    param(
        [string]$Name,
        [string[]]$FallbackPaths
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($path in $FallbackPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    throw "$Name was not found. Install it first or restart the terminal so PATH is refreshed."
}

function Assert-ChildPath {
    param(
        [string]$Path,
        [string]$Parent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (!$resolvedPath.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped expected parent. Path: $resolvedPath, Parent: $resolvedParent"
    }
}

function Get-SafeTagPart {
    param([string]$Value)

    $safe = $Value.Trim()
    foreach ($invalid in [System.IO.Path]::GetInvalidFileNameChars()) {
        $safe = $safe.Replace($invalid, '-')
    }

    return $safe.Replace('/', '-').Replace('\', '-')
}


function Get-Utf8Text {
    param([string]$Value)

    return [System.Text.RegularExpressions.Regex]::Unescape($Value)
}

function Write-ReleaseNotes {
    param(
        [string]$Path,
        [string]$Git,
        [string]$Tag,
        [string]$Title,
        [string]$ArtifactName,
        [string]$Sha256,
        [string[]]$Files,
        [string]$Changes,
        [bool]$Dirty
    )

    $commit = (& $Git rev-parse --short HEAD).Trim()

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine("# $Title")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## " + (Get-Utf8Text "\uC694\uC57D"))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- " + (Get-Utf8Text "\uD0DC\uADF8") + ": ``$Tag``")
    [void]$builder.AppendLine("- " + (Get-Utf8Text "\uCEE4\uBC0B") + ": ``$commit``")
    [void]$builder.AppendLine("- " + (Get-Utf8Text "\uBC30\uD3EC\u0020\uD30C\uC77C") + ": ``$ArtifactName``")
    [void]$builder.AppendLine("- SHA256: ``$Sha256``")
    if ($Dirty) {
        [void]$builder.AppendLine("- Source state: **UNCOMMITTED WORKING TREE - PREPARE ONLY.** The commit above is the base, not the complete artifact source.")
    }
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## " + (Get-Utf8Text "\uD3EC\uD568\u0020\uD30C\uC77C"))
    [void]$builder.AppendLine()
    foreach ($file in $Files) {
        [void]$builder.AppendLine("- ``$file``")
    }

    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## " + (Get-Utf8Text "\uBCC0\uACBD\u0020\uC0AC\uD56D"))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine($Changes)

    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## " + (Get-Utf8Text "\uBC30\uD3EC\u0020\uC804\u0020\uD655\uC778"))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- " + (Get-Utf8Text "\uB9B4\uB9AC\uC988\u0020\uBE4C\uB4DC\uAC00\u0020\uACBD\uACE0\u0020\u0030\uAC1C\u002C\u0020\uC624\uB958\u0020\u0030\uAC1C\uB85C\u0020\uC644\uB8CC\uB418\uC5C8\uB294\uC9C0\u0020\uD655\uC778\uD569\uB2C8\uB2E4\u002E"))
    [void]$builder.AppendLine("- " + (Get-Utf8Text "\u0047\u0069\u0074\u0048\u0075\u0062\u0020\u0052\u0065\u006C\u0065\u0061\u0073\u0065\u0020\uC790\uC0B0\uC740") + " ``FFXIVKoreanPatch.exe`` " + (Get-Utf8Text "\uD558\uB098\uC778\uC9C0\u0020\uD655\uC778\uD569\uB2C8\uB2E4\u002E"))
    [void]$builder.AppendLine("- " + (Get-Utf8Text "\uC0AC\uC804\u0020\uC810\uAC80\u002C\u0020\uC804\uCCB4\u0020\uD328\uCE58\u002C\u0020\uD3F0\uD2B8\u0020\uD328\uCE58\u002C\u0020\uD328\uCE58\u0020\uC81C\uAC70\u0020\uD750\uB984\uC744\u0020\uC810\uAC80\uD569\uB2C8\uB2E4\u002E"))

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $builder.ToString(), $utf8NoBom)
}

function Test-GitClean {
    param([string]$Git)

    $status = & $Git status --porcelain
    return [string]::IsNullOrWhiteSpace(($status -join [Environment]::NewLine))
}

$git = Resolve-ToolPath "git" @(
    "C:\Program Files\Git\cmd\git.exe",
    "C:\Program Files\Git\bin\git.exe"
)

Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($TagName)) {
        $TagName = "v" + (Get-Date -Format "yyyy.MM.dd.HHmm")
    }

    if ([string]::IsNullOrWhiteSpace($ReleaseTitle)) {
        $ReleaseTitle = (Get-Utf8Text "\u0046\u0046\u0058\u0049\u0056\u0020\uD55C\uAE00\u0020\uD328\uCE58") + " $TagName"
    }

    if (!(Test-GitClean $git) -and ($Publish -or !$AllowDirty)) {
        throw "Working tree is not clean. Commit or stash changes first, or pass -AllowDirty for prepare-only runs."
    }

    $changesPath = if ([System.IO.Path]::IsPathRooted($ChangesFile)) {
        $ChangesFile
    } else {
        Join-Path $repoRoot $ChangesFile
    }
    if (!(Test-Path -LiteralPath $changesPath -PathType Leaf)) {
        throw "Release change list was not found: $changesPath"
    }
    $changes = [System.IO.File]::ReadAllText($changesPath, [System.Text.Encoding]::UTF8).Trim()
    if ([string]::IsNullOrWhiteSpace($changes)) {
        throw "Release change list is empty: $changesPath"
    }

    if (!$SkipBuild) {
        & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-release.ps1")
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    if (!(Test-Path $releasePublicDir)) {
        throw "Release\Public was not found. Run Scripts\build-release.ps1 first or omit -SkipBuild."
    }

    $requiredFiles = @(
        "FFXIVKoreanPatch.exe"
    )

    $missingRequired = @()
    foreach ($file in $requiredFiles) {
        if (!(Test-Path (Join-Path $releasePublicDir $file))) {
            $missingRequired += $file
        }
    }

    if ($missingRequired.Count -gt 0) {
        throw "Release\Public is missing required files: $($missingRequired -join ', ')"
    }

    New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
    $safeTag = Get-SafeTagPart $TagName
    $stagingDir = Join-Path $releaseRoot $safeTag
    Assert-ChildPath $stagingDir $releaseRoot

    if (Test-Path $stagingDir) {
        if (!$Force) {
            throw "Release staging directory already exists: $stagingDir. Pass -Force to recreate it."
        }

        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
    $artifactName = "FFXIVKoreanPatch.exe"
    $artifactPath = Join-Path $stagingDir $artifactName
    Copy-Item -LiteralPath (Join-Path $releasePublicDir $artifactName) -Destination $artifactPath -Force

    $hash = Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256
    $shaPath = Join-Path $stagingDir "$artifactName.sha256.txt"
    Set-Content -LiteralPath $shaPath -Value "$($hash.Hash)  $artifactName" -Encoding ASCII

    $notesPath = Join-Path $stagingDir "release-notes.md"
    Write-ReleaseNotes -Path $notesPath -Git $git -Tag $TagName -Title $ReleaseTitle -ArtifactName $artifactName -Sha256 $hash.Hash -Files @($artifactName) -Changes $changes -Dirty (!(Test-GitClean $git))

    Write-Host "Release asset prepared:"
    Write-Host "  Tag:      $TagName"
    Write-Host "  Exe:      $artifactPath"
    Write-Host "  SHA:      $shaPath"
    Write-Host "  Notes:    $notesPath"
    Write-Host "  Changes:  $changesPath"

    if (!$Publish) {
        Write-Host ""
        Write-Host "Prepare-only mode. No git tag or GitHub Release was created."
        Write-Host "To publish:"
        Write-Host "  .\Scripts\publish-release.ps1 -TagName $TagName -ChangesFile '$($ChangesFile.Replace("'", "''"))' -Publish -Force"
        return
    }

    $gh = Resolve-ToolPath "gh" @(
        "C:\Program Files\GitHub CLI\gh.exe"
    )

    & $gh auth status | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not logged in. Run 'gh auth login' first."
    }

    if (!(Test-GitClean $git)) {
        throw "Working tree changed during release preparation. Commit or stash changes before publishing."
    }

    $currentBranch = (& $git rev-parse --abbrev-ref HEAD).Trim()
    if ($currentBranch -ne $BranchName) {
        throw "Current branch is $currentBranch, expected $BranchName."
    }

    $head = (& $git rev-parse HEAD).Trim()
    $remoteHeadLine = & $git ls-remote $RemoteName "refs/heads/$BranchName"
    $remoteHead = if ($remoteHeadLine) { $remoteHeadLine.Split("`t")[0].Trim() } else { "" }
    if ([string]::IsNullOrWhiteSpace($remoteHead) -or $head -ne $remoteHead) {
        throw "Local HEAD is not equal to $RemoteName/$BranchName. Push source changes before publishing a release."
    }

    & $git rev-parse -q --verify "refs/tags/$TagName" *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "Local tag already exists: $TagName"
    }

    $remoteTag = & $git ls-remote --tags $RemoteName "refs/tags/$TagName"
    if ($remoteTag) {
        throw "Remote tag already exists: $TagName"
    }

    & $git tag -a $TagName -m $ReleaseTitle
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & $git push $RemoteName $TagName
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $releaseArgs = @(
        "release", "create", $TagName,
        $artifactPath,
        "--title", $ReleaseTitle,
        "--notes-file", $notesPath
    )

    if ($Draft) {
        $releaseArgs += "--draft"
    }

    if ($Prerelease) {
        $releaseArgs += "--prerelease"
    }

    & $gh @releaseArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host "GitHub Release published for $TagName"
}
finally {
    Pop-Location
}
