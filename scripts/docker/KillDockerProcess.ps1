Step 1: Kill all Docker processes (preserves data)
  # Close all Docker Desktop windows first, then:
  taskkill /F /IM "Docker Desktop.exe"
  taskkill /F /IM "com.docker.backend.exe"
  taskkill /F /IM "com.docker.build.exe"
  taskkill /F /IM "Docker Desktop Installer.exe"