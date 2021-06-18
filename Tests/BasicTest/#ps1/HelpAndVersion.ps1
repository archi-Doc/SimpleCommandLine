# Set-ExecutionPolicy RemoteSigned

cd ../

# Build
dotnet build
$app = ".\bin\Debug\net5.0\BasicTest.exe"

Write-Output "" "BasicTest" ""

function Test-Do {
    Write-Output "Input: $args"
    $cmd = $app + " " + $args
    invoke-expression $cmd
    Write-Output ""
}

Test-Do "-help"
Test-Do "help"
Test-Do "-version"
Test-Do "-v"
Test-Do "-Version"
Test-Do "test -help"
Test-Do "test help"
Test-Do "help test"
Test-Do "help invalid"
Test-Do "invalid version"
Test-Do "invalid -version"
Test-Do "invalid help"
Test-Do "invalid -help"

Read-Host
