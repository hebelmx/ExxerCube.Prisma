#!/bin/bash
#
# 🧬 ExxerAI Helix OCR Dependencies Installer
# Cross-platform installation script for Tesseract OCR + OpenCV
# Supports: Ubuntu, Debian, RHEL, CentOS, Fedora, Arch Linux
#
# Usage:
#   sudo ./scripts/install-ocr-dependencies.sh
#
# Or with options:
#   sudo ./scripts/install-ocr-dependencies.sh --languages "eng spa ita deu por rus"
#

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default language codes (ISO 639-3)
DEFAULT_LANGUAGES="eng spa ita deu por rus chi_sim chi_tra jpn hin ben"

# Parse command line arguments
LANGUAGES="${DEFAULT_LANGUAGES}"
while [[ $# -gt 0 ]]; do
    case $1 in
        --languages)
            LANGUAGES="$2"
            shift 2
            ;;
        --help)
            echo "Usage: sudo $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --languages LANGS    Space-separated list of language codes (default: eng spa ita deu por rus chi_sim chi_tra jpn hin ben)"
            echo "  --help              Show this help message"
            echo ""
            echo "Language Codes:"
            echo "  eng = English       spa = Spanish      ita = Italian"
            echo "  deu = German        por = Portuguese   rus = Russian"
            echo "  chi_sim = Chinese (Simplified)        chi_tra = Chinese (Traditional)"
            echo "  jpn = Japanese      hin = Hindi        ben = Bengali"
            echo ""
            echo "Full list: https://github.com/tesseract-ocr/tessdata"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  ExxerAI Helix OCR Dependencies${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Detect Linux distribution
if [ -f /etc/os-release ]; then
    . /etc/os-release
    DISTRO=$ID
    VERSION=$VERSION_ID
else
    echo -e "${RED}ERROR: Cannot detect Linux distribution${NC}"
    exit 1
fi

echo -e "${GREEN}✓${NC} Detected: $PRETTY_NAME"
echo ""

# Function to install on Debian/Ubuntu
install_debian_ubuntu() {
    echo -e "${YELLOW}Installing for Debian/Ubuntu...${NC}"

    # Update package list
    echo -e "${BLUE}→${NC} Updating package list..."
    apt-get update -qq

    # Install Tesseract OCR
    echo -e "${BLUE}→${NC} Installing Tesseract OCR..."
    apt-get install -y -qq tesseract-ocr libtesseract-dev

    # Install language data
    echo -e "${BLUE}→${NC} Installing Tesseract language data..."
    for lang in $LANGUAGES; do
        pkg="tesseract-ocr-${lang}"
        if apt-cache show "$pkg" >/dev/null 2>&1; then
            echo -e "  ${GREEN}→${NC} Installing $lang..."
            apt-get install -y -qq "$pkg" 2>/dev/null || echo -e "  ${YELLOW}⚠${NC} Package $pkg not available, skipping..."
        else
            echo -e "  ${YELLOW}⚠${NC} Package $pkg not available, skipping..."
        fi
    done

    # Install OpenCV
    echo -e "${BLUE}→${NC} Installing OpenCV..."
    apt-get install -y -qq libopencv-dev python3-opencv

    echo -e "${GREEN}✓${NC} Installation complete!"
}

# Function to install on RHEL/CentOS/Fedora
install_redhat() {
    echo -e "${YELLOW}Installing for RHEL/CentOS/Fedora...${NC}"

    # Determine package manager
    if command -v dnf &> /dev/null; then
        PKG_MGR="dnf"
    else
        PKG_MGR="yum"
    fi

    # Install EPEL repository (for RHEL/CentOS)
    if [[ "$DISTRO" == "rhel" || "$DISTRO" == "centos" ]]; then
        echo -e "${BLUE}→${NC} Enabling EPEL repository..."
        $PKG_MGR install -y -q epel-release
    fi

    # Install Tesseract OCR
    echo -e "${BLUE}→${NC} Installing Tesseract OCR..."
    $PKG_MGR install -y -q tesseract tesseract-devel

    # Install language data
    echo -e "${BLUE}→${NC} Installing Tesseract language data..."
    for lang in $LANGUAGES; do
        pkg="tesseract-langpack-${lang}"
        echo -e "  ${GREEN}→${NC} Installing $lang..."
        $PKG_MGR install -y -q "$pkg" 2>/dev/null || echo -e "  ${YELLOW}⚠${NC} Package $pkg not available, skipping..."
    done

    # Install OpenCV
    echo -e "${BLUE}→${NC} Installing OpenCV..."
    $PKG_MGR install -y -q opencv opencv-devel

    echo -e "${GREEN}✓${NC} Installation complete!"
}

# Function to install on Arch Linux
install_arch() {
    echo -e "${YELLOW}Installing for Arch Linux...${NC}"

    # Update package database
    echo -e "${BLUE}→${NC} Updating package database..."
    pacman -Sy --noconfirm

    # Install Tesseract OCR
    echo -e "${BLUE}→${NC} Installing Tesseract OCR..."
    pacman -S --noconfirm tesseract

    # Install language data
    echo -e "${BLUE}→${NC} Installing Tesseract language data..."
    for lang in $LANGUAGES; do
        pkg="tesseract-data-${lang}"
        echo -e "  ${GREEN}→${NC} Installing $lang..."
        pacman -S --noconfirm "$pkg" 2>/dev/null || echo -e "  ${YELLOW}⚠${NC} Package $pkg not available, skipping..."
    done

    # Install OpenCV
    echo -e "${BLUE}→${NC} Installing OpenCV..."
    pacman -S --noconfirm opencv

    echo -e "${GREEN}✓${NC} Installation complete!"
}

# Install based on distribution
case "$DISTRO" in
    ubuntu|debian)
        install_debian_ubuntu
        ;;
    rhel|centos|fedora|rocky|almalinux)
        install_redhat
        ;;
    arch|manjaro)
        install_arch
        ;;
    *)
        echo -e "${RED}ERROR: Unsupported distribution: $DISTRO${NC}"
        echo ""
        echo "Supported distributions:"
        echo "  - Ubuntu/Debian"
        echo "  - RHEL/CentOS/Fedora/Rocky/AlmaLinux"
        echo "  - Arch/Manjaro"
        echo ""
        echo "Please install manually:"
        echo "  - Tesseract OCR (tesseract)"
        echo "  - Tesseract language data (eng, spa, ita, deu, por, rus, chi_sim, chi_tra, jpn, hin, ben)"
        echo "  - OpenCV (libopencv-dev or opencv-devel)"
        exit 1
        ;;
esac

# Verify installation
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Verifying Installation${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check Tesseract
if command -v tesseract &> /dev/null; then
    TESS_VERSION=$(tesseract --version 2>&1 | head -n 1)
    echo -e "${GREEN}✓${NC} Tesseract: $TESS_VERSION"
else
    echo -e "${RED}✗${NC} Tesseract: NOT FOUND"
fi

# Check Tesseract data path
if [ -d "/usr/share/tesseract-ocr" ]; then
    TESSDATA_PREFIX="/usr/share/tesseract-ocr"
elif [ -d "/usr/share/tessdata" ]; then
    TESSDATA_PREFIX="/usr/share/tessdata"
else
    TESSDATA_PREFIX="NOT FOUND"
fi
echo -e "${GREEN}✓${NC} TESSDATA_PREFIX: $TESSDATA_PREFIX"

# Check installed languages
if [ "$TESSDATA_PREFIX" != "NOT FOUND" ]; then
    INSTALLED_LANGS=$(find "$TESSDATA_PREFIX" -name "*.traineddata" 2>/dev/null | wc -l)
    echo -e "${GREEN}✓${NC} Installed languages: $INSTALLED_LANGS"
fi

# Check OpenCV
if pkg-config --exists opencv4 2>/dev/null; then
    OPENCV_VERSION=$(pkg-config --modversion opencv4)
    echo -e "${GREEN}✓${NC} OpenCV: $OPENCV_VERSION"
elif pkg-config --exists opencv 2>/dev/null; then
    OPENCV_VERSION=$(pkg-config --modversion opencv)
    echo -e "${GREEN}✓${NC} OpenCV: $OPENCV_VERSION"
else
    echo -e "${YELLOW}⚠${NC} OpenCV: pkg-config not found (but libraries may still be installed)"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Installation Complete! 🎉${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Next steps:"
echo "  1. Set TESSDATA_PREFIX environment variable (if needed):"
echo "     export TESSDATA_PREFIX=$TESSDATA_PREFIX"
echo ""
echo "  2. Run Helix tests:"
echo "     cd code/src"
echo "     dotnet test tests/04AdapterTests/ExxerAI.Helix.Adapter.Tests/"
echo ""
