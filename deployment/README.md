# ExxerCube Prisma - Deployment Documentation

**Environment:** Air-Gapped Production Machines (CNBV RegTech)
**Team Status:** Solo operator (Dev + Deployment + Support)

---

## 📁 Directory Structure

```
deployment/
├── README.md                        ← You are here
├── QUICK-DEPLOYMENT-GUIDE.md        ← Print this! 1-page reference
├── DEPLOYMENT-CHECKLIST.md          ← Full detailed checklist
├── scripts/
│   ├── install-certificates.ps1     ← Windows certificate installer
│   ├── install-certificates.sh      ← Linux certificate installer
│   └── (future: generate-certificates.sh)
├── certs/
│   ├── prisma.pfx                   ← Application certificate (git-ignored)
│   ├── ca.crt                       ← CA certificate (safe to commit)
│   └── README.md                    ← Certificate generation instructions
└── config/
    └── appsettings.Production.json  ← Production configuration template
```

---

## 🚀 Quick Start

### First Time Deployment to New Machine

1. **Install certificates** (⚠️ DON'T FORGET THIS!)
   ```powershell
   # Windows
   cd deployment\scripts
   .\install-certificates.ps1
   ```

2. **Follow deployment guide**
   - See `QUICK-DEPLOYMENT-GUIDE.md` for step-by-step

3. **Verify everything works**
   - Open browser, check for green padlock
   - No certificate warnings should appear

### Subsequent Deployments (Same Machine)

Certificates already installed, just deploy new version:
1. Build and package
2. Transfer to machine
3. Stop service
4. Extract files
5. Start service
6. Smoke test

---

## 📚 Documentation Files

### 1. QUICK-DEPLOYMENT-GUIDE.md
**Use for:** Day-to-day deployments
**Content:**
- Quick certificate check
- 30-minute deployment process
- Emergency fixes
- Pre-flight checklist (printable)

**Recommended:** Print this and keep near production machines.

### 2. DEPLOYMENT-CHECKLIST.md
**Use for:** Detailed step-by-step deployment
**Content:**
- Complete pre-deployment checks
- Full deployment procedure
- Common issues and solutions
- Certificate renewal instructions

**Recommended:** Use when training someone or for complex deployments.

---

## 🔐 Certificate Management

### Why Certificates Matter

**Problem:** Development certificates don't work in production
- Browser shows "Your connection is not private"
- Users can't access application
- Fails security compliance

**Solution:** Install proper certificates ONCE per machine
- Application certificate (`prisma.pfx`)
- CA certificate (`ca.crt`)

### Certificate Files

**Location:** `deployment/certs/`

- **prisma.pfx** - Application certificate (private key included)
  - ⚠️ Keep secure! Contains private key
  - ⚠️ Git-ignored by default
  - Password-protected

- **ca.crt** - CA certificate (public key only)
  - Safe to commit to git
  - Install on all production machines

### Installation Scripts

**Windows:** `scripts/install-certificates.ps1`
- Installs CA to Trusted Root
- Installs app cert to Personal store
- Validates certificate chain
- Must run as Administrator

**Linux:** `scripts/install-certificates.sh`
- Installs CA to `/usr/local/share/ca-certificates/`
- Copies app cert to `/opt/exxercube/prisma/certs/`
- Updates system trust store
- Must run as root

### When to Reinstall Certificates

- [ ] First deployment to new machine (always)
- [ ] Certificate expired (check annually)
- [ ] Certificate compromised (security incident)
- [ ] Switching from dev cert to production cert

---

## 🎯 Common Scenarios

### Scenario 1: Brand New Production Machine
1. Install OS
2. Install .NET 8 runtime
3. **Run certificate installation script** ← KEY STEP
4. Create service/systemd unit
5. Deploy application
6. Configure firewall
7. Smoke test

### Scenario 2: Updating Existing Deployment
1. ~~Install certificates~~ (already done)
2. Stop service
3. Backup current version
4. Deploy new version
5. Start service
6. Smoke test
7. Rollback if needed

### Scenario 3: Support Call - "Certificate Error"
**Symptom:** Browser shows "Your connection is not private"
**Diagnosis:** Certificates not installed or expired
**Fix:**
```powershell
cd deployment\scripts
.\install-certificates.ps1
```
Restart browser, problem solved.

### Scenario 4: Certificate Expiring Soon
**Check expiration:**
```powershell
Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"} | Select Subject, NotAfter
```

**If < 30 days:**
1. Regenerate certificates (see `certs/README.md`)
2. Test on dev machine
3. Deploy to production machines (run install script)
4. Update calendar reminder for next year

---

## 🆘 Troubleshooting

### "Script won't run" (PowerShell)
**Error:** `execution of scripts is disabled on this system`
**Fix:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### "Permission denied" (Linux)
**Error:** `Permission denied: ./install-certificates.sh`
**Fix:**
```bash
chmod +x deployment/scripts/*.sh
sudo ./deployment/scripts/install-certificates.sh
```

### "Certificate not found"
**Error:** Script can't find `prisma.pfx` or `ca.crt`
**Fix:**
- Check you're in correct directory
- Verify files exist in `deployment/certs/`
- Copy from backup USB if needed

### "Wrong password"
**Error:** `Failed to install application certificate: Incorrect password`
**Fix:**
- Check password in password manager
- Contact previous admin who created certificates
- If lost, regenerate certificates (see `certs/README.md`)

---

## 📋 Pre-Deployment Checklist (Quick)

Before leaving your desk:

```
[ ] Built in Release mode
[ ] All tests passing
[ ] Certificates in deployment package
[ ] USB drive ready
[ ] QUICK-DEPLOYMENT-GUIDE.md printed (if first time)
```

At production machine:

```
[ ] ⚠️ Certificates installed (if new machine)
[ ] Service stopped
[ ] Files deployed
[ ] Service started
[ ] Smoke test passed (green padlock!)
```

---

## 🔄 Annual Maintenance

**Set calendar reminder:**

- **Check certificate expiration** (monthly)
- **Renew certificates** (when < 30 days remaining)
- **Update documentation** (as processes change)
- **Review deployment logs** (quarterly)

---

## 📞 Support (Solo Operator Notes)

**When stuck:**

1. Check logs:
   - Windows: `C:\ProgramData\ExxerCube\Prisma\logs`
   - Linux: `/var/log/exxercube/prisma`

2. Verify certificates:
   ```powershell
   # Windows
   Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"}

   # Linux
   ls -la /opt/exxercube/prisma/certs/
   sudo /usr/local/bin/prisma-check-certs
   ```

3. Check service status:
   ```powershell
   # Windows
   Get-Service -Name "ExxerCubePrisma"

   # Linux
   sudo systemctl status exxercube-prisma
   ```

4. **90% of issues are certificates** - Run install script again

---

## 🎓 Lessons Learned (From Support Calls)

1. **Certificate installation is ALWAYS forgotten**
   - Added to pre-flight checklist
   - Created automated script
   - Printed reminder on wall

2. **Stakeholders don't understand "certificate warning"**
   - They think app is broken
   - Actually just forgot to install certs
   - Prevention: Don't skip certificate step

3. **Emergency deployments skip steps**
   - Pressure from stakeholders
   - Result: More support calls later
   - Solution: Follow checklist even when rushed

4. **Documentation saves solo operators**
   - Can't remember everything
   - Future you needs these notes
   - Write down the gotchas

---

## 📄 Related Documentation

- **Browser Automation MVP:** `docs/mvp/browser-automation-demo-guide.md`
- **Lessons Learned:** `docs/lessons-learned/2025-11-25-browser-automation-e2e-ui.md`
- **Architecture:** `docs/architecture/` (if exists)

---

**Last Updated:** 2025-11-25
**Maintainer:** Solo operator (you!)
**Status:** Active - Update as process changes

---

## 💡 Final Words

You're doing dev + deployment + support solo. This documentation is your safety net.

**Key principle:** Slow is smooth, smooth is fast.
- Follow the checklist
- Don't skip the certificate step
- Document what you learn

You've got this. 🚀
