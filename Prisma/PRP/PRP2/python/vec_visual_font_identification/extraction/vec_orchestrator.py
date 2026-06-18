"""VEC extraction orchestrator - main coordinator for the extraction pipeline."""

from datetime import datetime
from typing import Union
from pathlib import Path

from ..models.vec_statement import (
    VecExtractionResult,
    VecStatementData,
    VecStatementHeader,
    VecTransaction,
    ProductType,
)
from ..utils.pdf_processor import PdfProcessor
from ..utils.model_cache import model_cache
from .layoutlmv3_extractor import LayoutLMv3Extractor
from .table_extractor import TableTransformerExtractor


class VecExtractionOrchestrator:
    """
    Main orchestrator for VEC statement extraction.

    Coordinates LayoutLMv3, Table Transformer, and OCR models to extract
    complete structured data from VEC PDF statements.
    """

    def __init__(
        self,
        layoutlmv3_model: str = "microsoft/layoutlmv3-base",
        table_transformer_model: str = "microsoft/table-transformer-detection",
        use_gpu: bool = True,
    ):
        """
        Initialize the VEC extraction orchestrator.

        Args:
            layoutlmv3_model: HuggingFace model ID for LayoutLMv3
            table_transformer_model: HuggingFace model ID for Table Transformer
            use_gpu: Whether to use GPU acceleration
        """
        self.layoutlmv3_model = layoutlmv3_model
        self.table_transformer_model = table_transformer_model
        self.use_gpu = use_gpu

        # Initialize PDF processor
        self.pdf_processor = PdfProcessor()

        # Initialize extractors
        self.layoutlmv3_extractor = LayoutLMv3Extractor(
            model_name=layoutlmv3_model, use_gpu=use_gpu
        )
        self.table_extractor = TableTransformerExtractor(
            model_name=table_transformer_model, use_gpu=use_gpu
        )

        # Models will be loaded on-demand via model_cache
        self._model_cache = model_cache

    def extract_vec_statement(
        self, pdf_input: Union[bytes, str, Path], document_id: str
    ) -> VecExtractionResult:
        """
        Extract complete VEC statement data from PDF.

        This is the main entry point for CSnakes integration.

        Args:
            pdf_input: PDF as bytes, file path, or Path object
            document_id: Unique identifier for this document

        Returns:
            VecExtractionResult with success flag and extracted data or error
        """
        try:
            # Get PDF metadata
            total_pages = self.pdf_processor.get_page_count(pdf_input)

            # Extract header fields using LayoutLMv3
            header = self._extract_header(pdf_input)

            # Extract transactions using Table Transformer
            transactions = self._extract_transactions(pdf_input)

            # Create VEC statement data
            statement_data = VecStatementData(
                document_id=document_id,
                pdf_filename=str(pdf_input) if isinstance(pdf_input, (str, Path)) else "uploaded.pdf",
                processing_timestamp=datetime.utcnow().isoformat() + "Z",
                header=header,
                transactions=transactions,
                total_pages=total_pages,
                extraction_confidence=0.95,  # Will be calculated from model outputs
                model_version=f"layoutlmv3-{self.layoutlmv3_model}",
                logo_detected=False,  # Will be populated by visual module
                image_quality_score=1.0,  # Will be populated by visual module
                font_compliance=True,  # Will be populated by font module
            )

            return VecExtractionResult(
                success=True,
                data=statement_data,
                error_code=None,
                error_message=None,
            )

        except Exception as e:
            return VecExtractionResult(
                success=False,
                data=None,
                error_code="EXT-001",
                error_message=f"VEC extraction failed: {str(e)}",
            )

    def _extract_header(self, pdf_input: Union[bytes, str, Path]) -> VecStatementHeader:
        """
        Extract header fields using LayoutLMv3.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            VecStatementHeader with extracted fields
        """
        return self.layoutlmv3_extractor.extract_header(pdf_input)

    def _extract_transactions(
        self, pdf_input: Union[bytes, str, Path]
    ) -> list[VecTransaction]:
        """
        Extract transactions using Table Transformer.

        Args:
            pdf_input: PDF as bytes, file path, or Path object

        Returns:
            List of VecTransaction objects
        """
        return self.table_extractor.extract_transactions(pdf_input)


def extract_vec_statement_csnakes(pdf_bytes: bytes, document_id: str = None) -> dict:
    """
    CSnakes-compatible entry point for VEC statement extraction.

    This function is called from C# via CSnakes.Runtime.

    Args:
        pdf_bytes: PDF file as bytes
        document_id: Optional document identifier (auto-generated if not provided)

    Returns:
        Dictionary representation of VecExtractionResult (Pydantic model_dump())
    """
    if document_id is None:
        document_id = f"VEC-{datetime.utcnow().strftime('%Y%m%d%H%M%S')}"

    orchestrator = VecExtractionOrchestrator()
    result = orchestrator.extract_vec_statement(pdf_bytes, document_id)

    return result.model_dump()
