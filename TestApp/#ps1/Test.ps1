# Set-ExecutionPolicy RemoteSigned

cd ../

# Build
dotnet build
$app = ".\bin\Debug\net5.0\TestApp.exe"

Write-Output "" "SimpleCommandLine test" ""

function Test-Do {
    Write-Output "" $args
    $cmd = $app + " " + $args
    Write-Output $cmd
    invoke-expression $cmd
}

Test-Do '-help'

Read-Host
