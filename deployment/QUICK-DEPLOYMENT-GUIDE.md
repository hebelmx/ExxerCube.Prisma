# 🚀 Prisma Quick Deployment Guide (Air-Gapped)

**FOR SOLO OPERATOR** - Print this and keep near production machines

---

## ⚠️ CERTIFICATE CHECK - DO THIS FIRST!

**Before EVERY deployment to a NEW machine:**

### Windows:
```powershell
# Run as Administrator
cd deployment\scripts
.\install-certificates.ps1
```

### Linux:
```bash
# Run as root
cd deployment/scripts
sudo ./install-certificates.sh
```

**Signs certificates are missing:**
- Browser shows "Your connection is not private"
- Application logs show certificate errors
- HTTPS doesn't work

---

## 📦 Standard Deployment (30 Minutes)

### Step 1: Build & Package (Dev Machine)
```bash
# Build
dotnet build -c Release

# Publish
dotnet publish Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj -c Release -o publish

# Copy certificates
cp deployment/certs/prisma.pfx publish/certs/

# Package
cd publish
tar -czf ../prisma-deployment.tar.gz *
```

### Step 2: Transfer to Production
- Copy `prisma-deployment.tar.gz` to USB
- Copy `deployment/scripts/install-certificates.*` to USB
- Transfer to production machine

### Step 3: On Production Machine

**3.1 Check Certificates** ⚠️ **MOST FORGOTTEN STEP**
```powershell
# Windows
.\install-certificates.ps1

# Linux
sudo ./install-certificates.sh
```

**3.2 Stop Service**
```powershell
# Windows
Stop-Service -Name "ExxerCubePrisma"

# Linux
sudo systemctl stop exxercube-prisma
```

**3.3 Deploy Files**
```bash
# Extract
tar -xzf prisma-deployment.tar.gz -C /opt/exxercube/prisma

# Set permissions (Linux only)
sudo chown -R prisma:prisma /opt/exxercube/prisma
```

**3.4 Start Service**
```powershell
# Windows
Start-Service -Name "ExxerCubePrisma"

# Linux
sudo systemctl start exxercube-prisma
```

**3.5 Smoke Test** (2 minutes)
```bash
# Check service running
curl -k https://localhost:7062/

# Open browser - should see green padlock (no warning!)
# If warning appears: CERTIFICATES NOT INSTALLED
```

---

## 🔥 Emergency Fixes (Support Calls)

### "Your connection is not private"
**Cause:** Forgot certificates
**Fix:**
```powershell
cd deployment\scripts
.\install-certificates.ps1
```
Restart browser.

### "Application won't start"
**Cause:** Certificate file missing
**Fix:**
```bash
# Check file exists
ls /opt/exxercube/prisma/certs/prisma.pfx

# If missing, copy from USB:
cp /media/usb/certs/prisma.pfx /opt/exxercube/prisma/certs/
```

### "Port already in use"
**Cause:** Old process still running
**Fix:**
```powershell
# Windows
Get-Process -Name "ExxerCube.Prisma.Web.UI" | Stop-Process -Force

# Linux
sudo killall -9 ExxerCube.Prisma.Web.UI
```

---

## 📋 Pre-Flight Checklist

Print this and check off before each deployment:

```
[ ] All tests passing locally
[ ] Git commit created
[ ] Built in Release mode
[ ] Certificates copied to publish folder
[ ] Package created successfully
[ ] Copied to USB drive
[ ] USB drive in hand before leaving desk

--- AT PRODUCTION MACHINE ---

[ ] ⚠️ CERTIFICATES INSTALLED (run script!)
[ ] Service stopped
[ ] Files extracted
[ ] Permissions set (Linux)
[ ] Service started
[ ] Smoke test passed (green padlock!)
[ ] Login works
[ ] Upload test document
```

---

## 📅 Monthly Tasks

**Set calendar reminders:**

- [ ] Check certificate expiration:
  ```powershell
  Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"} | Select Subject, NotAfter
  ```

- [ ] If < 30 days, regenerate certificates

---

## 🆘 Emergency Contacts

**When you get stuck:**

1. Check `DEPLOYMENT-CHECKLIST.md` (full details)
2. Check application logs:
   - Windows: `C:\ProgramData\ExxerCube\Prisma\logs`
   - Linux: `/var/log/exxercube/prisma`
3. Check this guide
4. If certificates are the issue (90% of the time): Run `install-certificates.ps1`

---

## 💡 Pro Tips (From Experience)

1. **ALWAYS check certificates first** - It's the #1 forgotten step
2. **Label your USB drives** - "Prisma Certs" and "Prisma Deployment"
3. **Keep backup certificates** - On labeled USB in safe
4. **Document deployment date** - In a log file or notebook
5. **Test on staging first** - If you have a staging environment
6. **Don't rush** - Follow checklist even if stakeholders are waiting

---

## 📂 File Locations (Quick Reference)

**Dev Machine:**
- Certificates: `deployment/certs/prisma.pfx`
- CA Certificate: `deployment/certs/ca.crt`
- Scripts: `deployment/scripts/`
- Published app: `publish/`

**Production (Windows):**
- App: `C:\Program Files\ExxerCube\Prisma`
- Certs: `C:\Program Files\ExxerCube\Prisma\certs`
- Logs: `C:\ProgramData\ExxerCube\Prisma\logs`

**Production (Linux):**
- App: `/opt/exxercube/prisma`
- Certs: `/opt/exxercube/prisma/certs`
- Logs: `/var/log/exxercube/prisma`
- Trusted CA: `/usr/local/share/ca-certificates/exxercube-ca.crt`

---

**Last Updated:** 2025-11-25
**Solo Operator:** You've got this! Follow the checklist.
