$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$iss = Get-Content (Join-Path $root 'installer\LyricsX.iss') -Raw

function Assert-Contains([string]$Needle) {
    if ($iss -notmatch [regex]::Escape($Needle)) {
        throw "Installer script missing: $Needle"
    }
}

function Assert-NotContains([string]$Needle) {
    if ($iss -match [regex]::Escape($Needle)) {
        throw "Installer script must not contain: $Needle"
    }
}

Assert-Contains 'PrivilegesRequired=lowest'
Assert-Contains '{localappdata}\Programs\LyricsX'
Assert-Contains 'AppId={{85B19E84-0D47-4B36-BDAF-D9AE1B1E7E24}'
Assert-Contains 'MinVersion=10.0.22000'
Assert-NotContains 'runas'
Assert-NotContains '[UninstallDelete]'
Assert-Contains 'DELETEUSERDATA'
Assert-Contains 'DelTree(ExpandConstant(''{localappdata}\LyricsX'')'
