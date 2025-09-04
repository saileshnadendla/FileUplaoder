function Main {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $myInstscript = (Get-Item $PSCommandPath ).Basename
    Write-Host "Start Command Invoked from $myInstscript"

    Write-Host "Applying Redis..."
    kubectl apply -k "$scriptDir\..\Artifacts\Redis"

    Write-Host "Waiting for Redis pod to be up"
    do{
        $pod = kubectl get pods -n file-uploader -l app=fileuploader-redis -o json | ConvertFrom-Json
        if ($pod.items.Count -eq 0) { continue }
        $status = $pod.items[0].status.phase
    } while ($status -ne "Running")

    Write-Host "Applying Server..."
    kubectl apply -k "$scriptDir\..\Artifacts\Server"

    Write-Host "Waiting for Server pod to be up"
    do{
        $pod = kubectl get pods -n file-uploader -l app=fileuploader-api -o json | ConvertFrom-Json
        if ($pod.items.Count -eq 0) { continue }
        $status = $pod.items[0].status.phase
    } while ($status -ne "Running")

    Write-Host "Applying Worker..."
    kubectl apply -k "$scriptDir\..\Artifacts\Worker"

    Write-Host "Waiting for Worker pod to be up"
    do{
        $pod = kubectl get pods -n file-uploader -l app=fileuploader-worker -o json | ConvertFrom-Json
        if ($pod.items.Count -eq 0) { continue }
        $status = $pod.items[0].status.phase
    } while ($status -ne "Running")



    Start-Process powershell -ArgumentList "kubectl port-forward deployment/fileuploader-api 5000:5000 -n file-uploader"
    Start-Process powershell -ArgumentList "kubectl port-forward deployment/fileuploader-redis 6379:6379 -n file-uploader"



    Write-Host "All done!"
}

Main
