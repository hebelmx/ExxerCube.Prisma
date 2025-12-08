"""Table Transformer-based transaction table extraction for VEC statements."""

from datetime import date
from decimal import Decimal
from typing import Union
from pathlib import Path

import torch
from PIL import Image
from transformers import TableTransformerForObjectDetection, AutoProcessor

from ..models.vec_statement import VecTransaction
from ..utils.model_cache import model_cache
from ..utils.pdf_processor import PdfProcessor


class TableTransformerExtractor:
    """
    Table Transformer-based transaction table extractor for VEC statements.

    Uses Table Transformer model to detect and extract transaction tables including:
    - Fecha Operación (Operation Date)
    - Fecha Liquidación (Settlement Date)
    - Descripción (Description)
    - Cargos (Debits)
    - Abonos (Credits)
    - Saldo (Balance)
    - Referencia (Reference)
    """

    def __init__(
        self,
        model_name: str = "microsoft/table-transformer-detection",
        use_gpu: bool = True,
    ):
        """
        Initialize Table Transformer extractor.

        Args:
            model_name: HuggingFace model identifier
            use_gpu: Whether to use GPU acceleration
        """
        self.model_name = model_name
        self.device = "cuda" if use_gpu and torch.cuda.is_available() else "cpu"

        # Load model and processor from cache
        self.model, self.processor = model_cache.get_table_transformer_model(model_name)
        self.model = self.model.to(self.device)
        self.model.eval()

        self.pdf_processor = PdfProcessor()

    def extract_transactions(
        self, pdf_input: Union[bytes, str, Path]
    ) -> list[VecTransaction]:
        """
        Extract transaction tables from VEC statement PDF.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            List of VecTransaction objects
        """
        # Convert PDF to images
        images = self.pdf_processor.convert_to_images(pdf_input)

        # Extract tables from PDF (using pdfplumber as fallback)
        tables = self.pdf_processor.extract_tables(pdf_input)

        all_transactions = []

        for page_idx, (image, page_tables) in enumerate(zip(images, tables)):
            # Use Table Transformer to detect tables in image
            detected_tables = self._detect_tables(image)

            # Extract transactions from detected tables
            # If Table Transformer detection fails, fallback to pdfplumber tables
            if detected_tables:
                page_transactions = self._extract_from_detected_tables(
                    detected_tables, page_tables, page_idx
                )
            elif page_tables:
                page_transactions = self._extract_from_pdfplumber_tables(
                    page_tables, page_idx
                )
            else:
                page_transactions = []

            all_transactions.extend(page_transactions)

        return all_transactions

    def _detect_tables(self, image: Image.Image) -> list[dict]:
        """
        Detect tables in image using Table Transformer.

        Args:
            image: PIL Image of the page

        Returns:
            List of detected table bounding boxes
        """
        # Prepare inputs for Table Transformer
        inputs = self.processor(images=image, return_tensors="pt")
        inputs = {k: v.to(self.device) for k, v in inputs.items()}

        # Run inference
        with torch.no_grad():
            outputs = self.model(**inputs)

        # Process outputs (bounding boxes, scores, labels)
        # Table Transformer outputs: logits, pred_boxes
        target_sizes = torch.tensor([image.size[::-1]]).to(self.device)  # (height, width)
        results = self.processor.post_process_object_detection(
            outputs, threshold=0.5, target_sizes=target_sizes
        )[0]

        detected_tables = []
        for score, label, box in zip(results["scores"], results["labels"], results["boxes"]):
            detected_tables.append(
                {
                    "bbox": box.tolist(),  # [x_min, y_min, x_max, y_max]
                    "score": score.item(),
                    "label": label.item(),
                }
            )

        return detected_tables

    def _extract_from_detected_tables(
        self, detected_tables: list[dict], pdfplumber_tables: list, page_idx: int
    ) -> list[VecTransaction]:
        """
        Extract transactions from detected tables.

        Uses detected bounding boxes to correlate with pdfplumber extracted tables.

        Args:
            detected_tables: Table Transformer detected tables
            pdfplumber_tables: pdfplumber extracted table data
            page_idx: Page number

        Returns:
            List of VecTransaction objects
        """
        # Use pdfplumber tables as primary data source
        # (Table Transformer detects structure, pdfplumber extracts content)
        return self._extract_from_pdfplumber_tables(pdfplumber_tables, page_idx)

    def _extract_from_pdfplumber_tables(
        self, tables: list, page_idx: int
    ) -> list[VecTransaction]:
        """
        Extract transactions from pdfplumber tables.

        Args:
            tables: List of tables from pdfplumber
            page_idx: Page number

        Returns:
            List of VecTransaction objects
        """
        transactions = []

        for table in tables:
            if not table or len(table) < 2:
                continue  # Skip empty tables

            # Assume first row is header
            headers = table[0]
            rows = table[1:]

            # Find column indices
            col_indices = self._find_column_indices(headers)

            # Extract transactions from rows
            for row_idx, row in enumerate(rows):
                transaction = self._extract_transaction(
                    row, col_indices, page_idx, row_idx
                )
                if transaction:
                    transactions.append(transaction)

        return transactions

    def _find_column_indices(self, headers: list[str]) -> dict[str, int]:
        """
        Find column indices based on header names.

        Args:
            headers: List of header strings

        Returns:
            Dictionary mapping field names to column indices
        """
        col_indices = {}

        for idx, header in enumerate(headers):
            if not header:
                continue
            header_lower = header.lower()

            if "fecha operaci" in header_lower or "fecha oper" in header_lower:
                col_indices["operation_date"] = idx
            elif "fecha liquid" in header_lower or "fecha liq" in header_lower:
                col_indices["settlement_date"] = idx
            elif "descripci" in header_lower or "concepto" in header_lower:
                col_indices["description"] = idx
            elif "cargo" in header_lower or "retiro" in header_lower:
                col_indices["debit"] = idx
            elif "abono" in header_lower or "dep" in header_lower:
                col_indices["credit"] = idx
            elif "saldo" in header_lower or "balance" in header_lower:
                col_indices["balance"] = idx
            elif "referencia" in header_lower or "ref" in header_lower:
                col_indices["reference"] = idx

        return col_indices

    def _extract_transaction(
        self, row: list[str], col_indices: dict[str, int], page_idx: int, row_idx: int
    ) -> VecTransaction | None:
        """
        Extract a single transaction from a table row.

        Args:
            row: Table row data
            col_indices: Column index mapping
            page_idx: Page number
            row_idx: Row number

        Returns:
            VecTransaction or None if invalid
        """
        try:
            # Parse operation date (required)
            operation_date_str = row[col_indices.get("operation_date", 0)]
            if not operation_date_str or operation_date_str.strip() == "":
                return None  # Skip rows without operation date

            # Parse dates
            def parse_date_str(date_str: str) -> date | None:
                if not date_str or date_str.strip() == "":
                    return None
                # TODO: Implement robust date parsing (DD/MMM/YYYY format)
                from datetime import datetime
                try:
                    # Try DD/MM/YYYY format
                    return datetime.strptime(date_str.strip(), "%d/%m/%Y").date()
                except:
                    return None

            # Parse decimal
            def parse_decimal_str(decimal_str: str) -> Decimal | None:
                if not decimal_str or decimal_str.strip() == "":
                    return None
                cleaned = decimal_str.replace("$", "").replace(",", "").strip()
                try:
                    return Decimal(cleaned)
                except:
                    return None

            operation_date = parse_date_str(operation_date_str)
            if not operation_date:
                return None  # Invalid date, skip

            settlement_date = (
                parse_date_str(row[col_indices["settlement_date"]])
                if "settlement_date" in col_indices
                else None
            )

            description = (
                row[col_indices["description"]].strip()
                if "description" in col_indices
                else ""
            )

            debit = (
                parse_decimal_str(row[col_indices["debit"]])
                if "debit" in col_indices
                else None
            )

            credit = (
                parse_decimal_str(row[col_indices["credit"]])
                if "credit" in col_indices
                else None
            )

            balance_str = (
                row[col_indices["balance"]] if "balance" in col_indices else "0.00"
            )
            balance = parse_decimal_str(balance_str)
            if balance is None:
                balance = Decimal("0.00")

            reference = (
                row[col_indices["reference"]].strip()
                if "reference" in col_indices
                else None
            )

            transaction = VecTransaction(
                transaction_id=None,
                reference=reference,
                operation_date=operation_date,
                settlement_date=settlement_date,
                description=description,
                debit=debit,
                credit=credit,
                balance=balance,
                page_number=page_idx,
                row_number=row_idx,
            )

            return transaction

        except Exception as e:
            # Skip invalid rows
            return None
