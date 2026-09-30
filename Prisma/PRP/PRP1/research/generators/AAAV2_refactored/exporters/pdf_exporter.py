"""PDF exporter using Chrome/Edge headless (proven AAAV2 approach)."""

import os
import subprocess
from pathlib import Path
from typing import Optional


class PDFExporter:
    """Export HTML to PDF using Chrome/Edge headless browser."""

    # Possible Chrome/Edge installation paths
    CHROME_PATHS = [
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "/usr/bin/google-chrome",
        "/usr/bin/chromium-browser",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    ]

    def __init__(self, chrome_path: Optional[str] = None):
        """Initialize PDF exporter.

        Args:
            chrome_path: Path to Chrome/Edge executable (auto-detect if None)
        """
        self.chrome_path = chrome_path or self._find_chrome()

        if not self.chrome_path:
            raise RuntimeError(
                "Chrome or Edge not found. Please install Chrome/Edge or provide chrome_path."
            )

    def _find_chrome(self) -> Optional[str]:
        """Auto-detect Chrome or Edge installation.

        Returns:
            Path to Chrome/Edge executable or None
        """
        for path in self.CHROME_PATHS:
            if os.path.exists(path):
                return path
        return None

    def export(self, html_content: str, output_path: Path,
               html_temp_path: Optional[Path] = None) -> Path:
        """Convert HTML to PDF using Chrome headless.

        Args:
            html_content: HTML content string
            output_path: Path for output PDF file
            html_temp_path: Optional temp path for HTML (auto-generated if None)

        Returns:
            Path to generated PDF file
        """
        # Create temp HTML file if not provided
        if html_temp_path is None:
            html_temp_path = output_path.with_suffix('.html')

        # Write HTML to temp file
        html_temp_path.parent.mkdir(parents=True, exist_ok=True)
        with open(html_temp_path, 'w', encoding='utf-8') as f:
            f.write(html_content)

        # Convert to PDF using Chrome headless
        self._html_to_pdf_chrome(html_temp_path, output_path)

        return output_path

    def export_from_file(self, html_path: Path, output_path: Path) -> Path:
        """Convert HTML file to PDF.

        Args:
            html_path: Path to HTML file
            output_path: Path for output PDF file

        Returns:
            Path to generated PDF file
        """
        self._html_to_pdf_chrome(html_path, output_path)
        return output_path

    def _html_to_pdf_chrome(self, html_path: Path, pdf_path: Path) -> None:
        """Use Chrome/Edge to convert HTML to PDF.

        Args:
            html_path: Path to HTML file
            pdf_path: Path for output PDF
        """
        # Ensure output directory exists
        pdf_path.parent.mkdir(parents=True, exist_ok=True)

        # Get absolute paths
        html_abs = os.path.abspath(html_path)
        pdf_abs = os.path.abspath(pdf_path)

        # Chrome headless command
        cmd = [
            self.chrome_path,
            "--headless",
            "--disable-gpu",
            "--no-pdf-header-footer",  # KEY: Remove browser headers/footers
            "--print-to-pdf=" + pdf_abs,
            html_abs
        ]

        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=30
            )

            if not os.path.exists(pdf_abs) or os.path.getsize(pdf_abs) == 0:
                raise RuntimeError(
                    f"PDF generation failed. Chrome returned: {result.stderr}"
                )

        except subprocess.TimeoutExpired:
            raise RuntimeError("PDF generation timeout (>30 seconds)")

        except Exception as e:
            raise RuntimeError(f"PDF generation error: {e}")

    def is_available(self) -> bool:
        """Check if Chrome/Edge is available.

        Returns:
            True if Chrome/Edge found, False otherwise
        """
        return self.chrome_path is not None

    def get_chrome_version(self) -> str:
        """Get Chrome/Edge version.

        Returns:
            Version string
        """
        if not self.chrome_path:
            return "Not installed"

        try:
            result = subprocess.run(
                [self.chrome_path, "--version"],
                capture_output=True,
                text=True,
                timeout=5
            )
            return result.stdout.strip()
        except:
            return "Unknown version"
