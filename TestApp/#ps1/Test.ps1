# Set-ExecutionPolicy RemoteSigned

cd ../

# Build
dotnet build
$app = ".\bin\Debug\net5.0\TestApp.exe"

Write-Output "" "SimpleCommandLine test" ""

function Test-Do {
    Write-Output "Input: $args"
    $cmd = $app + " " + $args
    invoke-expression $cmd
    Write-Output ""
}

Test-Do "-help"
Test-Do "-version"
Test-Do "-v"
Test-Do "-Version"
Test-Do "test -help"
Test-Do "help"

Read-Host
