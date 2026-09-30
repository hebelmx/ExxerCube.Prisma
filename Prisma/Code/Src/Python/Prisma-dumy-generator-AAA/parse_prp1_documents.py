#!/usr/bin/env python3
"""
Utility script to parse the restored PRP1 fixture documents.

It extracts:
  - Plain XML content
  - DOCX paragraph text (by reading document.xml directly)
  - XLSX worksheet values (by decoding shared strings + first worksheet)
  - PDF text when PyPDF2 is available (otherwise reports a placeholder)
"""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
from typing import Dict, Iterable, List, Optional
from zipfile import ZipFile


def extract_docx_text(path: Path) -> List[str]:
    """Return DOCX paragraphs as strings without requiring python-docx."""
    namespaces = {
        "w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
    }
    with ZipFile(path) as zf:
        document_xml = zf.read("word/document.xml")
    root = ET.fromstring(document_xml)
    paragraphs: List[str] = []
    for para in root.findall(".//w:p", namespaces):
        texts = [
            node.text.strip()
            for node in para.findall(".//w:t", namespaces)
            if node.text and node.text.strip()
        ]
        if texts:
            paragraphs.append(" ".join(texts))
    return paragraphs


def extract_xlsx_rows(path: Path) -> List[List[str]]:
    """Return first worksheet rows decoded via shared strings."""
    namespaces = {
        "a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
    }
    with ZipFile(path) as zf:
        shared_strings: Dict[int, str] = {}
        if "xl/sharedStrings.xml" in zf.namelist():
            sst_xml = ET.fromstring(zf.read("xl/sharedStrings.xml"))
            for idx, si in enumerate(sst_xml.findall(".//a:si", namespaces)):
                # Shared string may contain multiple runs
                parts = [
                    t.text or ""
                    for t in si.findall(".//a:t", namespaces)
                ]
                shared_strings[idx] = "".join(parts)

        sheet_name = "xl/worksheets/sheet1.xml"
        if sheet_name not in zf.namelist():
            # fallback to first worksheet in archive
            sheet_name = next(
                (n for n in zf.namelist() if n.startswith("xl/worksheets/")), None
            )
            if not sheet_name:
                return []
        sheet_xml = ET.fromstring(zf.read(sheet_name))

    rows: List[List[str]] = []
    for row in sheet_xml.findall(".//a:row", namespaces):
        current_row: List[str] = []
        for cell in row.findall("a:c", namespaces):
            value = ""
            cell_type = cell.attrib.get("t")
            if cell_type == "s" and shared_strings:
                idx_text = cell.findtext("a:v", default="", namespaces=namespaces)
                if idx_text.isdigit():
                    value = shared_strings.get(int(idx_text), "")
            else:
                value = cell.findtext("a:v", default="", namespaces=namespaces)
            current_row.append(value or "")
        rows.append(current_row)
    return rows


def extract_pdf_text(path: Path) -> Optional[List[str]]:
    """Extract PDF text if PyPDF2 is installed; otherwise return None."""
    try:
        import PyPDF2  # type: ignore
    except ImportError:
        return None

    texts: List[str] = []
    with path.open("rb") as fh:
        reader = PyPDF2.PdfReader(fh)
        for page in reader.pages:
            content = page.extract_text() or ""
            if content:
                texts.append(content.strip())
    return texts


def parse_xml_file(path: Path) -> str:
    """Return XML pretty string."""
    tree = ET.parse(path)
    root = tree.getroot()
    return ET.tostring(root, encoding="unicode")


def scan_directory(folder: Path) -> Dict[str, Dict[str, Iterable[str]]]:
    """Parse all supported documents within the folder."""
    results: Dict[str, Dict[str, Iterable[str]]] = {}
    for file in sorted(folder.glob("*")):
        suffix = file.suffix.lower()
        if suffix == ".xml":
            results[file.name] = {"type": "xml", "path": str(file), "content": parse_xml_file(file)}
        elif suffix == ".docx":
            results[file.name] = {
                "type": "docx",
                "paragraphs": extract_docx_text(file),
            }
        elif suffix == ".xlsx":
            results[file.name] = {
                "type": "xlsx",
                "rows": extract_xlsx_rows(file),
            }
        elif suffix == ".pdf":
            pdf_text = extract_pdf_text(file)
            if pdf_text is not None:
                results[file.name] = {"type": "pdf", "pages": pdf_text}
            else:
                results[file.name] = {
                    "type": "pdf",
                    "error": "PyPDF2 not installed; install it for PDF text extraction.",
                }
    return results


def summarize_xml_fields(path: Path) -> Dict[str, object]:
    tree = ET.parse(path)
    root = tree.getroot()
    namespace = ""
    if root.tag.startswith("{"):
        namespace = root.tag.split("}")[0][1:]
    ns = {"ns": namespace} if namespace else {}

    def field_name(tag: str) -> str:
        return tag.split("}")[-1]

    fields = set()
    sample_values = {}
    for elem in root.iter():
        tag = field_name(elem.tag)
        if elem.text and elem.text.strip():
            sample_values[tag] = elem.text.strip()
        fields.add(tag)

    aseguramiento = root.findtext("ns:TieneAseguramiento", default="false", namespaces=ns).lower() == "true"
    mandatory_fields = ["plazoDias"]
    if aseguramiento:
        mandatory_fields.append("aseguramiento")

    profile = {
        "id": path.stem,
        "authority": root.findtext("ns:AutoridadNombre", default="Autoridad", namespaces=ns),
        "requirement_type": root.findtext("ns:Cnbv_AreaDescripcion", default="Información Financiera", namespaces=ns),
        "subtype": root.findtext("ns:SolicitudEspecifica/ns:Caracter", default=None, namespaces=ns),
        "mandatory_fields": mandatory_fields,
        "sla_days": [
            int(root.findtext("ns:Cnbv_DiasPlazo", default="5", namespaces=ns)),
            int(root.findtext("ns:Cnbv_DiasPlazo", default="5", namespaces=ns)),
        ],
        "aseguramiento": aseguramiento,
        "hints": [
            (root.findtext("ns:SolicitudEspecifica/ns:InstruccionesCuentasPorConocer", default="", namespaces=ns) or "")[
                :240
            ]
        ],
        "error_bias": ["omisión", "abreviac", "acentuación apresurada"],
        "fields": sorted(fields),
    }
    return profile


KEYWORD_TO_TYPE = {
    "bloqueo": "Bloqueo de Cuentas",
    "desbloqueo": "Liberación de Fondos",
    "transferencia": "Transferencia de Fondos",
    "información": "Información Financiera",
    "documentación": "Documentación Complementaria",
}


def summarize_excel_rows(rows: List[List[str]], file_name: str) -> List[Dict[str, object]]:
    profiles: List[Dict[str, object]] = []
    for idx, row in enumerate(rows):
        if not row or len(row) < 3:
            continue
        description = " ".join(cell for cell in row if isinstance(cell, str))
        normalized = description.lower()
        match = next(
            (req_type for keyword, req_type in KEYWORD_TO_TYPE.items() if keyword in normalized),
            None,
        )
        if not match:
            continue
        profiles.append(
            {
                "id": f"{file_name}-row{idx}",
                "authority": "CNBV/IMSS (Excel)",
                "requirement_type": match,
                "subtype": None,
                "mandatory_fields": [],
                "sla_days": [3, 10],
                "aseguramiento": "bloqueo" in normalized,
                "hints": [description[:200]],
                "error_bias": ["nota rápida", "siglas"],
            }
        )
    return profiles


def build_summary(folder: Path) -> Dict[str, object]:
    summary: Dict[str, object] = {
        "requirement_profiles": [],
        "metadata_fields": {},
    }
    xml_profiles: List[Dict[str, object]] = []
    field_counter: Counter[str] = Counter()
    for xml_file in folder.glob("*.xml"):
        profile = summarize_xml_fields(xml_file)
        xml_profiles.append(profile)
        field_counter.update(profile.get("fields", []))

    excel_profiles: List[Dict[str, object]] = []
    for excel_file in folder.glob("*.xlsx"):
        rows = extract_xlsx_rows(excel_file)
        excel_profiles.extend(summarize_excel_rows(rows, excel_file.stem))

    summary["requirement_profiles"] = xml_profiles + excel_profiles
    summary["metadata_fields"] = dict(field_counter)
    return summary


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Parse restored PRP1 fixtures (XML, DOCX, XLSX, PDF)."
    )
    parser.add_argument(
        "--folder",
        default="Prisma/Fixtures/PRP1",
        help="Folder containing the PRP1 documents",
    )
    parser.add_argument("--output", default=None, help="Optional JSON file to store raw parsed listing")
    parser.add_argument(
        "--summary-output",
        default="Prisma/Fixtures/PRP1/prp1_summary.json",
        help="JSON file to store derived metadata summary",
    )
    args = parser.parse_args()

    folder = Path(args.folder)
    if not folder.exists():
        print(f"Folder {folder} does not exist", file=sys.stderr)
        sys.exit(1)

    parsed = scan_directory(folder)

    if args.output:
        Path(args.output).write_text(json.dumps(parsed, ensure_ascii=False, indent=2))
        print(f"Saved parsed output to {args.output}")
    else:
        print(json.dumps(parsed, ensure_ascii=False, indent=2))

    summary = build_summary(folder)
    if args.summary_output:
        Path(args.summary_output).write_text(json.dumps(summary, ensure_ascii=False, indent=2))
        print(f"Saved PRP1 summary to {args.summary_output}")


if __name__ == "__main__":
    main()
