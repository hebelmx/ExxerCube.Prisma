# ExxerCube Prisma - Production Deployment Checklist

**Environment:** Air-Gapped Production Machines
**Team:** Solo (Dev + Deployment + Support)

---

## Pre-Deployment (Once Per Machine)

### 🔐 Certificate Installation (DON'T FORGET THIS!)

- [ ] **Check if certificate is already installed**
  ```powershell
  # Windows
  Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"}
  Get-ChildItem Cert:\LocalMachine\Root | Where-Object {$_.Subject -like "*ExxerCube*"}
  ```

- [ ] **If NOT installed, run certificate setup**
  ```powershell
  # Windows
  .\deployment\scripts\install-certificates.ps1
  ```
  ```bash
  # Linux
  sudo ./deployment/scripts/install-certificates.sh
  ```

- [ ] **Verify certificate trust**
  ```powershell
  # Windows - Should return "Valid"
  Test-Certificate -Cert (Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"})
  ```

---

## Deployment Steps

### 1. Pre-Flight Checks

- [ ] All tests passing locally (run `dotnet test`)
- [ ] Git commit created with changes
- [ ] Version number updated in `Directory.Build.props`
- [ ] Build succeeds: `dotnet build -c Release`

### 2. Package Application

- [ ] Publish application:
  ```bash
  dotnet publish Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj -c Release -o publish
  ```

- [ ] Copy certificates to publish folder:
  ```bash
  cp deployment/certs/prisma.pfx publish/certs/
  ```

- [ ] Copy appsettings.Production.json:
  ```bash
  cp deployment/config/appsettings.Production.json publish/
  ```

- [ ] Create deployment package:
  ```bash
  cd publish
  tar -czf ../prisma-deployment-$(date +%Y%m%d-%H%M%S).tar.gz *
  cd ..
  ```

### 3. Transfer to Air-Gapped Machine

- [ ] Copy deployment package to USB drive
- [ ] Copy `deployment/scripts/install-certificates.*` to USB drive
- [ ] Transfer to production machine

### 4. On Production Machine

- [ ] **STOP EXISTING SERVICE** (if running)
  ```powershell
  # Windows
  Stop-Service -Name "ExxerCubePrisma"
  ```
  ```bash
  # Linux
  sudo systemctl stop exxercube-prisma
  ```

- [ ] **CHECK CERTIFICATES** (most forgotten step!)
  ```powershell
  # If not installed, run:
  .\install-certificates.ps1
  ```

- [ ] Extract deployment package:
  ```bash
  tar -xzf prisma-deployment-*.tar.gz -C /opt/exxercube/prisma
  ```

- [ ] Verify certificate path in appsettings.Production.json:
  ```json
  {
    "Kestrel": {
      "Endpoints": {
        "Https": {
          "Certificate": {
            "Path": "/opt/exxercube/prisma/certs/prisma.pfx",
            "Password": "REDACTED"
          }
        }
      }
    }
  }
  ```

- [ ] **START SERVICE**
  ```powershell
  # Windows
  Start-Service -Name "ExxerCubePrisma"
  ```
  ```bash
  # Linux
  sudo systemctl start exxercube-prisma
  ```

### 5. Smoke Tests

- [ ] Service is running:
  ```powershell
  Get-Service -Name "ExxerCubePrisma" | Select Status
  ```

- [ ] HTTPS endpoint responds:
  ```bash
  curl -k https://localhost:7062/
  ```

- [ ] **Certificate is valid (NO BROWSER WARNING)**:
  - Open `https://localhost:7062` in browser
  - Should see green padlock (no certificate warning)
  - If warning appears: **CERTIFICATES NOT INSTALLED CORRECTLY**

- [ ] Login works
- [ ] Upload document works
- [ ] OCR pipeline processes document

### 6. Post-Deployment

- [ ] Document deployment in log:
  ```bash
  echo "$(date): Deployed version X.Y.Z to production" >> /var/log/prisma-deployments.log
  ```

- [ ] Notify stakeholders (if needed)

---

## Common Issues (Support Calls)

### Issue: Browser shows "Your connection is not private"

**Cause:** Forgot to install certificates
**Fix:**
```powershell
.\deployment\scripts\install-certificates.ps1
```
Then restart browser and clear SSL state:
- Chrome: `chrome://restart`
- Edge: `edge://restart`

### Issue: Application won't start (Certificate error in logs)

**Symptom:** Logs show `Unable to load certificate from...`
**Cause:** Certificate file missing or wrong password
**Fix:**
1. Check certificate exists: `Test-Path certs/prisma.pfx`
2. Verify password in appsettings.Production.json
3. Check file permissions

### Issue: Application starts but HTTPS doesn't work

**Cause:** Firewall blocking port 7062
**Fix:**
```powershell
New-NetFirewallRule -DisplayName "Prisma HTTPS" -Direction Inbound -LocalPort 7062 -Protocol TCP -Action Allow
```

---

## Emergency Rollback

If deployment fails:

1. Stop service
2. Restore previous version:
   ```bash
   cp -r /opt/exxercube/prisma-backup/* /opt/exxercube/prisma/
   ```
3. Start service
4. Verify smoke tests pass

---

## Certificate Renewal (Annual Task)

Certificates expire! Set calendar reminder.

- [ ] Check certificate expiration:
  ```powershell
  Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"} | Select Subject, NotAfter
  ```

- [ ] If expiring in < 30 days, regenerate:
  ```bash
  ./deployment/scripts/generate-certificates.sh
  ```

- [ ] Deploy new certificate to all machines (repeat checklist)

---

## Notes

- **Solo operator:** This checklist is your safety net. Follow it sequentially.
- **Certificate step is #1 forgotten step:** Check it BEFORE deployment, not during support call.
- **Keep USB with certificates:** Label it "Prisma Certs" and keep in secure location.
- **Document everything:** Future you (or your replacement) will thank you.

---

**Last Updated:** 2025-11-25
**Next Certificate Renewal:** [SET DATE]
