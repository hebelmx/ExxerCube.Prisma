# Certificate Files

**⚠️ IMPORTANT: Keep certificates secure!**

This directory contains SSL/TLS certificates for production deployment.

---

## 📂 Files in This Directory

### Required Files

- **ca.crt** - Certificate Authority public certificate
  - Safe to commit to git
  - Installed on all production machines once
  - Trusted root for signing app certificates

- **prisma.pfx** - Application certificate (PFX/PKCS12 format)
  - ⚠️ Contains private key - KEEP SECURE
  - ⚠️ Should be .gitignored
  - Password-protected
  - Used by Kestrel for HTTPS

### Optional Files (For Reference)

- **prisma.crt** - Application certificate (PEM format)
- **prisma.key** - Private key (PEM format)
- **ca-key.pem** - CA private key (⚠️ KEEP VERY SECURE - store offline)

---

## 🔐 .gitignore Configuration

These files should be git-ignored for security:

```gitignore
# Certificate private keys and PFX files
*.pfx
*.key
*.p12
*-key.pem
ca-key.pem

# Allow CA public certificate
!ca.crt
```

---

## 🏭 Generating Certificates

### Option 1: Internal Certificate Authority (Recommended)

#### Step 1: Create CA (One-Time, Offline)

```bash
# Generate CA private key (keep this VERY secure!)
openssl genrsa -aes256 -out ca-key.pem 4096

# Generate CA certificate (valid 20 years)
openssl req -new -x509 -days 7300 -key ca-key.pem -sha256 -out ca.crt \
  -subj "/CN=ExxerCube Internal CA/O=ExxerCube/OU=Security/C=MX/ST=Estado de Mexico/L=Ciudad"

# ⚠️ BACKUP ca-key.pem to secure offline storage (USB in safe, HSM, etc.)
# ⚠️ Delete ca-key.pem from online machines after backup
```

#### Step 2: Generate Application Certificate

```bash
# Generate application private key
openssl genrsa -out prisma-key.pem 4096

# Create certificate signing request (CSR)
openssl req -new -key prisma-key.pem -out prisma.csr \
  -subj "/CN=prisma.internal.exxercube/O=ExxerCube/OU=CNBV RegTech/C=MX"

# Create SAN (Subject Alternative Names) configuration
cat > prisma-san.cnf <<'EOF'
[req]
distinguished_name = req_distinguished_name
req_extensions = v3_req

[req_distinguished_name]

[v3_req]
subjectAltName = @alt_names

[alt_names]
DNS.1 = prisma.internal.exxercube
DNS.2 = prisma.local
DNS.3 = localhost
DNS.4 = prisma.cnbv.local
IP.1 = 10.0.0.100
IP.2 = 192.168.1.100
IP.3 = 127.0.0.1
EOF

# Sign certificate with CA (valid 5 years)
openssl x509 -req -in prisma.csr -CA ca.crt -CAkey ca-key.pem \
  -CAcreateserial -out prisma.crt -days 1825 -sha256 \
  -extfile prisma-san.cnf -extensions v3_req

# Convert to PFX for .NET (Windows/Kestrel)
openssl pkcs12 -export -out prisma.pfx \
  -inkey prisma-key.pem \
  -in prisma.crt \
  -certfile ca.crt \
  -password pass:YourStrongPasswordHere

# Clean up temporary files
rm prisma.csr prisma-san.cnf
```

#### Step 3: Secure Storage

```bash
# Keep these files:
# - ca.crt (commit to git, install on all machines)
# - prisma.pfx (deploy with app, git-ignore)

# Backup securely:
# - ca-key.pem (USB in physical safe, only for signing new certs)
# - prisma-key.pem (optional, can regenerate from PFX if needed)

# Delete from online machines:
rm ca-key.pem  # Only keep in offline backup
```

---

### Option 2: Self-Signed Certificate (Quick, Less Secure)

```bash
# Generate self-signed certificate (valid 10 years)
openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 \
  -nodes -keyout prisma-key.pem -out prisma.crt \
  -subj "/CN=prisma.local/O=ExxerCube/C=MX" \
  -addext "subjectAltName=DNS:prisma.local,DNS:localhost,IP:127.0.0.1,IP:10.0.0.100"

# Convert to PFX
openssl pkcs12 -export -out prisma.pfx \
  -inkey prisma-key.pem \
  -in prisma.crt \
  -password pass:YourStrongPasswordHere

# For self-signed, ca.crt is the same as prisma.crt
cp prisma.crt ca.crt
```

⚠️ **Limitation:** Each machine must trust `prisma.crt` individually. With CA approach (Option 1), machines only trust CA once.

---

### Option 3: Using .NET Dev Certs (Development Only)

```bash
# Generate dev certificate
dotnet dev-certs https -ep prisma.pfx -p YourPassword --trust

# Extract CA certificate (for Linux machines)
openssl pkcs12 -in prisma.pfx -cacerts -nokeys -out ca.crt -password pass:YourPassword
```

⚠️ **Not recommended for production:** Dev certs expire in 1 year and aren't proper enterprise certificates.

---

## 📋 Certificate Information Template

When generating certificates, document the details:

```
Certificate Details
===================
Generated: 2025-11-25
Expires: 2030-11-25 (5 years)
Subject: CN=prisma.internal.exxercube, O=ExxerCube, OU=CNBV RegTech, C=MX
Issuer: CN=ExxerCube Internal CA, O=ExxerCube, C=MX
Password: [STORE IN PASSWORD MANAGER]
Key Algorithm: RSA 4096-bit
Signature: SHA-256

Subject Alternative Names:
- DNS: prisma.internal.exxercube
- DNS: prisma.local
- DNS: localhost
- IP: 10.0.0.100
- IP: 192.168.1.100

CA Certificate:
- Location: deployment/certs/ca.crt
- Trusted on machines: [LIST HOSTNAMES]

Private Key Backup:
- Location: USB drive in safe, labeled "ExxerCube CA Key 2025"
- Last verified: 2025-11-25
```

---

## 🔄 Certificate Renewal Process

Certificates should be renewed before expiration (recommend 30 days before).

### Annual Check (Set Calendar Reminder)

```powershell
# Windows - Check expiration
Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"} | Select Subject, NotAfter

# Linux - Check expiration
openssl pkcs12 -in prisma.pfx -nokeys -passin pass:YourPassword | openssl x509 -noout -dates
```

### Renewal Steps

1. **Retrieve CA private key** from secure offline storage
2. **Generate new application certificate** (see Step 2 above)
3. **Test on development machine**
4. **Deploy to production machines** (run install-certificates script)
5. **Verify no browser warnings**
6. **Return CA key to secure offline storage**
7. **Document renewal date**
8. **Set reminder for next renewal** (4 years from now)

---

## 🛠️ Troubleshooting

### "openssl: command not found"

**Windows:**
- Install Git for Windows (includes OpenSSL)
- Or install OpenSSL from: https://slproweb.com/products/Win32OpenSSL.html

**Linux:**
```bash
# Ubuntu/Debian
sudo apt-get install openssl

# RHEL/CentOS
sudo yum install openssl
```

### "Cannot load CA private key"

**Symptom:** `unable to load Private Key`
**Cause:** CA key is encrypted (recommended)
**Solution:** Provide password when prompted

### "SAN not included in certificate"

**Symptom:** Browser shows "NET::ERR_CERT_COMMON_NAME_INVALID"
**Cause:** Forgot `-extfile prisma-san.cnf` when signing
**Solution:** Regenerate certificate with SAN configuration

---

## 📝 Password Management

**Certificate password should be:**
- Stored in secure password manager
- Documented in deployment documentation
- Known to backup operators (if any)
- Changed if compromised

**Recommended storage:**
- Production appsettings.json (encrypted at rest)
- Password manager (1Password, Bitwarden, etc.)
- Secure documentation (encrypted)

**DO NOT:**
- Commit passwords to git (even in .env files)
- Email passwords in plain text
- Write on sticky notes

---

## 🔒 Security Best Practices

1. **CA Private Key**
   - Generate on offline machine
   - Store on USB drive in physical safe
   - Only retrieve when signing new certificates
   - Never store on networked machines

2. **Application Private Key**
   - Password-protect PFX files
   - Git-ignore all private keys
   - Restrict file permissions (640 or 600)
   - Rotate if compromised

3. **Certificate Distribution**
   - Use secure transfer methods (encrypted USB, not email)
   - Verify checksums after transfer
   - Delete from transfer media after installation

4. **Audit Trail**
   - Document all certificate generations
   - Log installations on production machines
   - Track expiration dates
   - Review access logs quarterly

---

## ✅ Installation Checklist

After generating certificates:

- [ ] `ca.crt` created and copied to this directory
- [ ] `prisma.pfx` created and copied to this directory
- [ ] Certificate details documented (see template above)
- [ ] Password stored in password manager
- [ ] CA private key backed up to secure offline storage
- [ ] CA private key deleted from online machines
- [ ] Tested on development machine
- [ ] Installation script tested (`install-certificates.ps1`)
- [ ] Calendar reminder set for renewal (4-5 years)

---

**Last Updated:** 2025-11-25
**Certificate Management:** Solo operator
**Next Review:** Annual (check expiration)
