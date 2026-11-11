  Get-Process | Where-Object {$_.Name -like "*docker*"} | Stop-Process -Force



    wsl --shutdown
  wsl --unregister docker-desktop
  wsl --unregister docker-desktop-data
