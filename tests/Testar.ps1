$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $compilador = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $fontes = @('PainelPicoFido.cs', 'PicoCard.cs', 'PicoOath.cs', 'PicoSettings.cs', 'HardwareViews.cs')
    $referencias = @('/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Management.dll')
    & $compilador /nologo /codepage:65001 /target:exe /main:DiagnosticoTestes /out:tests\DiagnosticoTestes.exe @referencias @fontes tests\DiagnosticoTestes.cs
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar os testes.' }
    & ./tests/DiagnosticoTestes.exe
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes do diagnóstico.' }
}
finally {
    Pop-Location
}
