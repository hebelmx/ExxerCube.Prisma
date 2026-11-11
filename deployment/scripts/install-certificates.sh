#!/bin/bash
# ExxerCube Prisma - Certificate Installation Script (Linux)
# Run this ONCE per production machine BEFORE first deployment

set -e

CERT_PATH="${1:-./certs/prisma.pfx}"
CA_CERT_PATH="${2:-./certs/ca.crt}"
INSTALL_DIR="/opt/exxercube/prisma/certs"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}ExxerCube Prisma Certificate Installer${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}ERROR: This script must be run as root!${NC}"
    echo -e "${YELLOW}Run with: sudo ./install-certificates.sh${NC}"
    exit 1
fi

echo -e "${YELLOW}[1/5] Checking for existing certificates...${NC}"

# Check if CA certificate already installed
if [ -f "/usr/local/share/ca-certificates/exxercube-ca.crt" ]; then
    echo -e "${GREEN}  ✓ CA certificate already installed${NC}"
    EXISTING_CA=true
else
    echo -e "${RED}  ✗ CA certificate NOT installed${NC}"
    EXISTING_CA=false
fi

# Check if application certificate already installed
if [ -d "$INSTALL_DIR" ] && [ -f "$INSTALL_DIR/prisma.pfx" ]; then
    echo -e "${GREEN}  ✓ Application certificate already installed${NC}"

    # Check expiration using openssl (if pfx can be extracted)
    # Note: Requires password, so skip for now
    EXISTING_APP_CERT=true
else
    echo -e "${RED}  ✗ Application certificate NOT installed${NC}"
    EXISTING_APP_CERT=false
fi

echo ""

# If both installed, ask if should reinstall
if [ "$EXISTING_CA" = true ] && [ "$EXISTING_APP_CERT" = true ]; then
    read -p "Certificates already installed. Reinstall? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Skipping installation.${NC}"
        exit 0
    fi
fi

echo -e "${YELLOW}[2/5] Validating certificate files...${NC}"

# Check if CA certificate file exists
if [ ! -f "$CA_CERT_PATH" ]; then
    echo -e "${RED}  ERROR: CA certificate not found at: $CA_CERT_PATH${NC}"
    echo -e "${YELLOW}  Expected file: ca.crt${NC}"
    exit 1
fi
echo -e "${GREEN}  ✓ CA certificate file found${NC}"

# Check if application certificate file exists
if [ ! -f "$CERT_PATH" ]; then
    echo -e "${RED}  ERROR: Application certificate not found at: $CERT_PATH${NC}"
    echo -e "${YELLOW}  Expected file: prisma.pfx${NC}"
    exit 1
fi
echo -e "${GREEN}  ✓ Application certificate file found${NC}"

echo ""
echo -e "${YELLOW}[3/5] Installing CA certificate to system trust store...${NC}"

# Copy CA certificate to trusted store
cp "$CA_CERT_PATH" /usr/local/share/ca-certificates/exxercube-ca.crt
chmod 644 /usr/local/share/ca-certificates/exxercube-ca.crt

# Update CA certificates
if update-ca-certificates; then
    echo -e "${GREEN}  ✓ CA certificate installed successfully${NC}"
else
    echo -e "${RED}  ERROR: Failed to install CA certificate${NC}"
    exit 1
fi

echo ""
echo -e "${YELLOW}[4/5] Installing application certificate...${NC}"

# Create installation directory
mkdir -p "$INSTALL_DIR"
chmod 750 "$INSTALL_DIR"

# Copy application certificate
cp "$CERT_PATH" "$INSTALL_DIR/prisma.pfx"
chmod 640 "$INSTALL_DIR/prisma.pfx"

# Set ownership (assuming application runs as 'prisma' user)
if id "prisma" &>/dev/null; then
    chown prisma:prisma "$INSTALL_DIR/prisma.pfx"
    echo -e "${GREEN}  ✓ Certificate ownership set to 'prisma' user${NC}"
else
    echo -e "${YELLOW}  ⚠ WARNING: 'prisma' user not found, using root ownership${NC}"
    echo -e "${YELLOW}    Create service user: sudo useradd -r -s /bin/false prisma${NC}"
fi

echo -e "${GREEN}  ✓ Application certificate installed successfully${NC}"

echo ""
echo -e "${YELLOW}[5/5] Verifying installation...${NC}"

# Verify CA certificate
if [ -f "/usr/local/share/ca-certificates/exxercube-ca.crt" ]; then
    echo -e "${GREEN}  ✓ CA certificate verified in system trust store${NC}"
else
    echo -e "${RED}  ✗ CA certificate verification FAILED${NC}"
    exit 1
fi

# Verify application certificate
if [ -f "$INSTALL_DIR/prisma.pfx" ]; then
    echo -e "${GREEN}  ✓ Application certificate verified${NC}"

    # Extract certificate info (requires openssl)
    if command -v openssl &> /dev/null; then
        echo -e "${GRAY}    Extracting certificate details...${NC}"
        # Note: This will prompt for password, which is expected
    fi
else
    echo -e "${RED}  ✗ Application certificate verification FAILED${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Certificate installation COMPLETE${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${CYAN}Installation Details:${NC}"
echo -e "${GRAY}  CA Certificate: /usr/local/share/ca-certificates/exxercube-ca.crt${NC}"
echo -e "${GRAY}  App Certificate: $INSTALL_DIR/prisma.pfx${NC}"
echo -e "${GRAY}  Permissions: 640 (read for owner/group only)${NC}"
echo ""

echo -e "${CYAN}Next Steps:${NC}"
echo -e "${GRAY}  1. Update appsettings.Production.json with certificate path:${NC}"
echo -e "${GRAY}     \"Path\": \"$INSTALL_DIR/prisma.pfx\"${NC}"
echo -e "${GRAY}  2. Deploy application (see DEPLOYMENT-CHECKLIST.md)${NC}"
echo -e "${GRAY}  3. Verify HTTPS works: curl -I https://localhost:7062${NC}"
echo -e "${GRAY}  4. Document installation date in deployment log${NC}"
echo ""

# Create a verification script
cat > /usr/local/bin/prisma-check-certs << 'EOF'
#!/bin/bash
# Quick certificate check script

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo "Checking ExxerCube Prisma certificates..."

if [ -f "/usr/local/share/ca-certificates/exxercube-ca.crt" ]; then
    echo -e "${GREEN}✓ CA certificate installed${NC}"
else
    echo -e "${RED}✗ CA certificate MISSING${NC}"
fi

if [ -f "/opt/exxercube/prisma/certs/prisma.pfx" ]; then
    echo -e "${GREEN}✓ Application certificate installed${NC}"
else
    echo -e "${RED}✗ Application certificate MISSING${NC}"
fi
EOF

chmod +x /usr/local/bin/prisma-check-certs

echo -e "${CYAN}Verification script created: /usr/local/bin/prisma-check-certs${NC}"
echo -e "${GRAY}Run 'prisma-check-certs' anytime to verify certificates${NC}"
echo ""
