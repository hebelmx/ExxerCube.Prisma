"""LayoutLMv3-based header field extraction for VEC statements."""

from datetime import date
from decimal import Decimal
from typing import Any, Optional, Union
from pathlib import Path

import torch
from PIL import Image
from transformers import LayoutLMv3ForTokenClassification, LayoutLMv3Processor

from ..models.vec_statement import ProductType, VecStatementHeader
from ..utils.model_cache import model_cache
from ..utils.pdf_processor import PdfProcessor


class LayoutLMv3Extractor:
    """
    LayoutLMv3-based header field extractor for VEC statements.

    Uses fine-tuned LayoutLMv3 model to extract header fields including:
    - Account information
    - Statement period
    - Product classification
    - Financial summary
    - Interest information
    """

    # Field labels expected from fine-tuned model
    FIELD_LABELS = [
        "O",  # Outside any field
        "ACCOUNT_NUMBER",
        "ACCOUNT_HOLDER",
        "CONTRACT_NUMBER",
        "PERIOD_START",
        "PERIOD_END",
        "STATEMENT_DATE",
        "PRODUCT_TYPE",
        "PRODUCT_NAME",
        "INITIAL_BALANCE",
        "FINAL_BALANCE",
        "TOTAL_DEPOSITS",
        "TOTAL_WITHDRAWALS",
        "INTEREST_RATE",
        "INTEREST_GROSS",
        "ISR_TAX",
        "INTEREST_NET",
        "MATURITY_DATE",
        "UDI_VALUE",
        "NAV_VALUE",
        "MARKET_VALUE",
    ]

    def __init__(
        self,
        model_name: str = "microsoft/layoutlmv3-base",
        use_gpu: bool = True,
    ):
        """
        Initialize LayoutLMv3 extractor.

        Args:
            model_name: HuggingFace model identifier (use fine-tuned model in production)
            use_gpu: Whether to use GPU acceleration
        """
        self.model_name = model_name
        self.device = "cuda" if use_gpu and torch.cuda.is_available() else "cpu"

        # Load model and processor from cache
        self.model, self.processor = model_cache.get_layoutlmv3_model(model_name)
        self.model = self.model.to(self.device)
        self.model.eval()

        self.pdf_processor = PdfProcessor()

    def extract_header(
        self, pdf_input: Union[bytes, str, Path]
    ) -> VecStatementHeader:
        """
        Extract header fields from VEC statement PDF.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            VecStatementHeader with extracted fields
        """
        # Convert first page to image (header typically on page 1)
        images = self.pdf_processor.convert_to_images(pdf_input)
        if not images:
            raise ValueError("PDF has no pages")

        first_page = images[0]

        # Extract text from first page (for OCR text input)
        text_pages = self.pdf_processor.extract_text(pdf_input)
        first_page_text = text_pages[0] if text_pages else ""

        # Extract fields using LayoutLMv3
        extracted_fields = self._extract_fields_from_page(first_page, first_page_text)

        # Map extracted fields to VecStatementHeader
        header = self._map_to_header(extracted_fields)

        return header

    def _extract_fields_from_page(
        self, image: Image.Image, text: str
    ) -> dict[str, str]:
        """
        Extract fields from a page using LayoutLMv3.

        Args:
            image: PIL Image of the page
            text: Extracted text from the page

        Returns:
            Dictionary mapping field names to extracted values
        """
        # Prepare inputs for LayoutLMv3
        encoding = self.processor(
            image, text=text, return_tensors="pt", truncation=True, max_length=512
        )
        encoding = {k: v.to(self.device) for k, v in encoding.items()}

        # Run inference
        with torch.no_grad():
            outputs = self.model(**encoding)
            predictions = outputs.logits.argmax(-1).squeeze().tolist()

        # Extract predicted labels (token classification)
        tokens = self.processor.tokenizer.convert_ids_to_tokens(
            encoding["input_ids"].squeeze().tolist()
        )

        # Group tokens by field label
        extracted_fields = {}
        current_field = None
        current_tokens = []

        for token, prediction in zip(tokens, predictions):
            if token in ["[CLS]", "[SEP]", "[PAD]"]:
                continue

            label = self.FIELD_LABELS[prediction] if prediction < len(self.FIELD_LABELS) else "O"

            if label == "O":
                # Save accumulated field if any
                if current_field and current_tokens:
                    extracted_fields[current_field] = self.processor.tokenizer.convert_tokens_to_string(
                        current_tokens
                    )
                current_field = None
                current_tokens = []
            else:
                if label != current_field:
                    # Save previous field
                    if current_field and current_tokens:
                        extracted_fields[current_field] = self.processor.tokenizer.convert_tokens_to_string(
                            current_tokens
                        )
                    current_field = label
                    current_tokens = [token]
                else:
                    current_tokens.append(token)

        # Save last field
        if current_field and current_tokens:
            extracted_fields[current_field] = self.processor.tokenizer.convert_tokens_to_string(
                current_tokens
            )

        return extracted_fields

    def _map_to_header(self, extracted_fields: dict[str, str]) -> VecStatementHeader:
        """
        Map extracted fields to VecStatementHeader Pydantic model.

        Args:
            extracted_fields: Dictionary of extracted field values

        Returns:
            VecStatementHeader instance
        """
        # Parse dates
        def parse_date(date_str: Optional[str]) -> date:
            if not date_str:
                return date(2025, 1, 1)  # Default
            # TODO: Implement robust date parsing (handle DD/MMM/YYYY format)
            # For now, return default
            from datetime import datetime
            try:
                return datetime.strptime(date_str.strip(), "%d/%m/%Y").date()
            except:
                return date(2025, 1, 1)

        # Parse decimals
        def parse_decimal(decimal_str: Optional[str]) -> Decimal:
            if not decimal_str:
                return Decimal("0.00")
            # Remove currency symbols and commas
            cleaned = decimal_str.replace("$", "").replace(",", "").strip()
            try:
                return Decimal(cleaned)
            except:
                return Decimal("0.00")

        # Parse product type
        def parse_product_type(product_str: Optional[str]) -> ProductType:
            if not product_str:
                return ProductType.VISTA
            product_lower = product_str.lower()
            if "vista" in product_lower:
                return ProductType.VISTA
            elif "recompra" in product_lower:
                return ProductType.RECOMPRA
            elif "reporto" in product_lower:
                return ProductType.REPORTO
            elif "cede" in product_lower:
                return ProductType.CEDE
            elif "pagaré" in product_lower or "pagare" in product_lower:
                return ProductType.PAGARE
            elif "udibono" in product_lower:
                return ProductType.UDIBONO
            elif "fondos" in product_lower:
                return ProductType.FONDOS
            elif "acciones" in product_lower:
                return ProductType.ACCIONES
            else:
                return ProductType.VISTA

        # Create VecStatementHeader
        header = VecStatementHeader(
            account_number=extracted_fields.get("ACCOUNT_NUMBER", "VEC-000000-00"),
            account_holder=extracted_fields.get("ACCOUNT_HOLDER", "UNKNOWN"),
            contract_number=extracted_fields.get("CONTRACT_NUMBER"),
            statement_period_start=parse_date(extracted_fields.get("PERIOD_START")),
            statement_period_end=parse_date(extracted_fields.get("PERIOD_END")),
            statement_date=parse_date(extracted_fields.get("STATEMENT_DATE")),
            product_type=parse_product_type(extracted_fields.get("PRODUCT_TYPE")),
            product_name=extracted_fields.get("PRODUCT_NAME", "Unknown Product"),
            initial_balance=parse_decimal(extracted_fields.get("INITIAL_BALANCE")),
            final_balance=parse_decimal(extracted_fields.get("FINAL_BALANCE")),
            total_deposits=parse_decimal(extracted_fields.get("TOTAL_DEPOSITS")),
            total_withdrawals=parse_decimal(extracted_fields.get("TOTAL_WITHDRAWALS")),
            interest_rate_annual=parse_decimal(extracted_fields.get("INTEREST_RATE"))
            if extracted_fields.get("INTEREST_RATE")
            else None,
            interest_earned_gross=parse_decimal(extracted_fields.get("INTEREST_GROSS"))
            if extracted_fields.get("INTEREST_GROSS")
            else None,
            isr_tax_withheld=parse_decimal(extracted_fields.get("ISR_TAX"))
            if extracted_fields.get("ISR_TAX")
            else None,
            interest_earned_net=parse_decimal(extracted_fields.get("INTEREST_NET"))
            if extracted_fields.get("INTEREST_NET")
            else None,
            maturity_date=parse_date(extracted_fields.get("MATURITY_DATE"))
            if extracted_fields.get("MATURITY_DATE")
            else None,
            udi_value=parse_decimal(extracted_fields.get("UDI_VALUE"))
            if extracted_fields.get("UDI_VALUE")
            else None,
            nav_value=parse_decimal(extracted_fields.get("NAV_VALUE"))
            if extracted_fields.get("NAV_VALUE")
            else None,
            market_value=parse_decimal(extracted_fields.get("MARKET_VALUE"))
            if extracted_fields.get("MARKET_VALUE")
            else None,
        )

        return header
