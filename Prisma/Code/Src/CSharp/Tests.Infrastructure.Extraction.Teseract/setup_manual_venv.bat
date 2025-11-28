@echo off
REM Manual venv setup for GOT-OCR2 Tests
REM Lesson learned: CSnakes doesn't create environment correctly, must do manually

echo ============================================
echo GOT-OCR2 Manual Venv Setup
echo ============================================
echo.

REM Get the bin output directory
set BIN_DIR=..\..\..\bin\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2\net10.0
set VENV_DIR=%BIN_DIR%\.venv_gotor2_tests

echo Target directory: %BIN_DIR%
echo Venv directory: %VENV_DIR%
echo.

REM Navigate to bin directory
cd /d "%BIN_DIR%"
if errorlevel 1 (
    echo ERROR: Could not navigate to bin directory
    echo Please run: dotnet build first
    pause
    exit /b 1
)

echo Current directory: %CD%
echo.

REM Remove old venv if exists
if exist ".venv_gotor2_tests" (
    echo Removing old virtual environment...
    rmdir /s /q ".venv_gotor2_tests"
)

echo.
echo Step 1: Creating virtual environment...
python -m venv .venv_gotor2_tests
if errorlevel 1 (
    echo ERROR: Failed to create virtual environment
    echo Make sure Python 3.10+ is installed and in PATH
    pause
    exit /b 1
)
echo   DONE

echo.
echo Step 2: Activating virtual environment...
call .venv_gotor2_tests\Scripts\activate.bat
echo   DONE

echo.
echo Step 3: Upgrading pip...
python -m pip install --upgrade pip
echo   DONE

echo.
echo Step 4: Installing PyTorch with CUDA 13.0 support...
echo   (This may take a while - ~2GB download)
pip install torch==2.9.1 --index-url https://download.pytorch.org/whl/cu130
if errorlevel 1 (
    echo WARNING: Failed to install torch with CUDA, trying CPU version...
    pip install torch==2.9.1
)
echo   DONE

echo.
echo Step 5: Installing torchvision (CRITICAL dependency)...
pip install torchvision==0.24.1 --index-url https://download.pytorch.org/whl/cu130
if errorlevel 1 (
    echo WARNING: Failed to install torchvision with CUDA, trying CPU version...
    pip install torchvision==0.24.1
)
echo   DONE

echo.
echo Step 6: Installing numpy...
pip install numpy==2.3.5
echo   DONE

echo.
echo Step 7: Installing transformers...
echo   (This downloads GOT-OCR2 dependencies)
pip install transformers==4.57.1
echo   DONE

echo.
echo Step 8: Installing Pillow...
pip install Pillow==12.0.0
echo   DONE

echo.
echo Step 9: Installing accelerate...
pip install accelerate==1.12.0
echo   DONE

echo.
echo Step 10: Installing huggingface-hub...
pip install huggingface-hub==0.36.0
echo   DONE

echo.
echo Step 11: Installing safetensors...
pip install safetensors==0.7.0
echo   DONE

echo.
echo Step 11: Installing safetensors...
pip install PyMuPDF==1.26.6 
echo   DONE

echo.
echo ============================================
echo VERIFICATION
echo ============================================
echo.

echo Verifying torch installation...
python -c "import torch; print(f'  torch version: {torch.__version__}')"
if errorlevel 1 (
    echo ERROR: torch import failed
    pause
    exit /b 1
)

echo Verifying torchvision installation...
python -c "import torchvision; print(f'  torchvision version: {torchvision.__version__}')"
if errorlevel 1 (
    echo ERROR: torchvision import failed
    pause
    exit /b 1
)

echo Verifying transformers installation...
python -c "from transformers import AutoProcessor; print('  AutoProcessor: OK')"
if errorlevel 1 (
    echo ERROR: transformers import failed
    pause
    exit /b 1
)

echo Verifying CUDA support...
python -c "import torch; print(f'  CUDA available: {torch.cuda.is_available()}')"

echo.
echo ============================================
echo SUCCESS!
echo ============================================
echo.
echo Virtual environment created and packages installed.
echo.
echo Environment location: %CD%\.venv_gotor2_tests
echo.
echo To activate manually:
echo   cd %CD%
echo   .venv_gotor2_tests\Scripts\activate
echo.
echo To run tests:
echo   cd ..\..\..
echo   dotnet test Tests.Infrastructure.Extraction.GotOcr2
echo.
echo IMPORTANT: On first test run, GOT-OCR2 model will download (~3-5GB)
echo            This may take 10-15 minutes depending on internet speed.
echo.
pause
