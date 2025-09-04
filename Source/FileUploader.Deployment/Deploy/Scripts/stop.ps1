function Main {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $myInstscript = (Get-Item $PSCommandPath ).Basename
    Write-Host "Stop Command Invoked from $myInstscript"


    Write-Host "Deleting Redis..."
    kubectl delete -k "$scriptDir\..\Artifacts\Redis"

    Write-Host "Waiting for Redis pod to be deleted"
    do{
        $pod = kubectl get pods -n file-uploader -l app=redis -o json | ConvertFrom-Json
    } while ($pod.items.Count -eq 0)

    Write-Host "All done!"
}

Main
