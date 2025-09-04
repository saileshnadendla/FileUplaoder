function Main {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $myInstscript = (Get-Item $PSCommandPath ).Basename
    Write-Host "Stop Command Invoked from $myInstscript"

    $kubectlProcesses = Get-Process -Name "kubectl" -ErrorAction SilentlyContinue
    $kubectlProcesses | ForEach-Object {
        Write-Host "Stopping kubectl process with PID:" $_.Id
        Stop-Process -Id $_.Id -Force
    }


     Write-Host "Deleting Server..."
     kubectl delete -k "$scriptDir\..\Artifacts\Server"

    Write-Host "Waiting for Server pod to be deleted"
    do{
         $pod = kubectl get pods -n file-uploader -l app=fileuploader-api -o json | ConvertFrom-Json
    } while ($pod.items.Count -eq 0)



    Write-Host "Deleting Worker..."
    kubectl delete -k "$scriptDir\..\Artifacts\Worker"

    Write-Host "Waiting for Worker pod to be down"
    do{
         $pod = kubectl get pods -n file-uploader -l app=fileuploader-worker -o json | ConvertFrom-Json
    } while ($pod.items.Count -eq 0)


    Write-Host "Deleting Redis..."
    kubectl delete -k "$scriptDir\..\Artifacts\Redis"

    Write-Host "Waiting for Redis pod to be deleted"
    do{
        $pod = kubectl get pods -n file-uploader -l app=fileuploader-redis -o json | ConvertFrom-Json
    } while ($pod.items.Count -eq 0)

    Write-Host "All done!"
}

Main
