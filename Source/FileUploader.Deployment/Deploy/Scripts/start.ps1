function Main {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $myInstscript = (Get-Item $PSCommandPath ).Basename
    Write-Host "Start Command Invoked from $myInstscript"

    Write-Host "Applying Redis..."
    kubectl apply -k "$scriptDir\..\Artifacts\Redis"

    Write-Host "Waiting for Redis pod to be up"
    do{
        $pod = kubectl get pods -n file-uploader -l app=redis -o json | ConvertFrom-Json
        if ($pod.items.Count -eq 0) { continue }
        $status = $pod.items[0].status.phase
    } while ($status -ne "Running")


    Write-Host "All done!"
}

Main
