"""
Sample Python module for text processing operations.
This module demonstrates text manipulation and NLP-like operations.
"""

import time
import re
import logging
from typing import Dict, List, Any, Optional
from collections import Counter

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def analyze_text(text: str) -> Dict[str, Any]:
    """
    Analyze text and return various statistics.
    
    Args:
        text: Input text to analyze
        
    Returns:
        Dictionary with text analysis results
    """
    logger.info(f"Analyzing text of length {len(text)}")
    
    if not text:
        return {
            "operation": "text_analysis",
            "error": "Text is empty",
            "success": False,
            "timestamp": time.time()
        }
    
    # Basic statistics
    words = text.split()
    sentences = re.split(r'[.!?]+', text)
    sentences = [s.strip() for s in sentences if s.strip()]
    
    # Character analysis
    char_counter = Counter(text.lower())
    
    # Word analysis
    word_counter = Counter(word.lower().strip('.,!?;:"()[]') for word in words)
    
    result = {
        "operation": "text_analysis",
        "inputs": {"text_length": len(text)},
        "statistics": {
            "character_count": len(text),
            "word_count": len(words),
            "sentence_count": len(sentences),
            "paragraph_count": text.count('\n\n') + 1,
            "average_word_length": sum(len(word) for word in words) / len(words) if words else 0,
            "average_sentence_length": len(words) / len(sentences) if sentences else 0
        },
        "character_frequency": dict(char_counter.most_common(10)),
        "word_frequency": dict(word_counter.most_common(10)),
        "most_common_word": word_counter.most_common(1)[0] if word_counter else None,
        "unique_words": len(word_counter),
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Text analysis completed: {result['statistics']['word_count']} words, {result['statistics']['sentence_count']} sentences")
    return result


def clean_text(text: str, options: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """
    Clean text by removing unwanted characters and normalizing.
    
    Args:
        text: Input text to clean
        options: Cleaning options dictionary
        
    Returns:
        Dictionary with cleaned text and operations performed
    """
    if options is None:
        options = {}
        
    logger.info(f"Cleaning text of length {len(text)}")
    
    original_length = len(text)
    cleaned_text = text
    operations = []
    
    # Remove extra whitespace
    if options.get('remove_extra_whitespace', True):
        cleaned_text = re.sub(r'\s+', ' ', cleaned_text)
        operations.append('removed_extra_whitespace')
    
    # Remove special characters
    if options.get('remove_special_chars', False):
        cleaned_text = re.sub(r'[^\w\s]', '', cleaned_text)
        operations.append('removed_special_chars')
    
    # Convert to lowercase
    if options.get('to_lowercase', False):
        cleaned_text = cleaned_text.lower()
        operations.append('converted_to_lowercase')
    
    # Remove numbers
    if options.get('remove_numbers', False):
        cleaned_text = re.sub(r'\d+', '', cleaned_text)
        operations.append('removed_numbers')
    
    # Strip whitespace
    cleaned_text = cleaned_text.strip()
    
    result = {
        "operation": "text_cleaning",
        "inputs": {
            "original_length": original_length,
            "options": options
        },
        "original_text": text,
        "cleaned_text": cleaned_text,
        "operations_performed": operations,
        "reduction_percentage": ((original_length - len(cleaned_text)) / original_length * 100) if original_length > 0 else 0,
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Text cleaning completed: {len(operations)} operations, {result['reduction_percentage']:.1f}% reduction")
    return result


def extract_keywords(text: str, top_n: int = 10) -> Dict[str, Any]:
    """
    Extract keywords from text based on frequency and length.
    
    Args:
        text: Input text to extract keywords from
        top_n: Number of top keywords to return
        
    Returns:
        Dictionary with extracted keywords
    """
    logger.info(f"Extracting top {top_n} keywords from text")
    
    if not text:
        return {
            "operation": "keyword_extraction",
            "error": "Text is empty",
            "success": False,
            "timestamp": time.time()
        }
    
    # Common stop words (simplified list)
    stop_words = {
        'the', 'a', 'an', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for', 
        'of', 'with', 'by', 'is', 'are', 'was', 'were', 'be', 'been', 'have', 
        'has', 'had', 'do', 'does', 'did', 'will', 'would', 'could', 'should',
        'this', 'that', 'these', 'those', 'i', 'you', 'he', 'she', 'it', 'we', 
        'they', 'me', 'him', 'her', 'us', 'them'
    }
    
    # Extract words
    words = re.findall(r'\b[a-zA-Z]{3,}\b', text.lower())
    
    # Filter out stop words and count frequency
    word_counter = Counter(word for word in words if word not in stop_words)
    
    # Calculate keyword scores (frequency * length)
    keyword_scores = {}
    for word, freq in word_counter.items():
        keyword_scores[word] = freq * len(word)
    
    # Get top keywords
    top_keywords = sorted(keyword_scores.items(), key=lambda x: x[1], reverse=True)[:top_n]
    
    result = {
        "operation": "keyword_extraction",
        "inputs": {
            "text_length": len(text),
            "top_n": top_n
        },
        "keywords": [{"word": word, "score": score} for word, score in top_keywords],
        "total_unique_words": len(word_counter),
        "stop_words_filtered": len(words) - len(word_counter),
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Extracted {len(top_keywords)} keywords from {result['total_unique_words']} unique words")
    return result


def generate_summary(text: str, max_sentences: int = 3) -> Dict[str, Any]:
    """
    Generate a simple extractive summary of the text.
    
    Args:
        text: Input text to summarize
        max_sentences: Maximum number of sentences in summary
        
    Returns:
        Dictionary with summary and metadata
    """
    logger.info(f"Generating summary with max {max_sentences} sentences")
    
    if not text:
        return {
            "operation": "text_summarization",
            "error": "Text is empty",
            "success": False,
            "timestamp": time.time()
        }
    
    # Split into sentences
    sentences = re.split(r'[.!?]+', text)
    sentences = [s.strip() for s in sentences if s.strip()]
    
    if len(sentences) <= max_sentences:
        summary_sentences = sentences
    else:
        # Simple extractive summarization: select sentences with highest word diversity
        sentence_scores = []
        
        for i, sentence in enumerate(sentences):
            words = set(word.lower().strip('.,!?;:"()[]') for word in sentence.split())
            score = len(words)  # Simple scoring based on unique words
            sentence_scores.append((i, sentence, score))
        
        # Sort by score and select top sentences
        sentence_scores.sort(key=lambda x: x[2], reverse=True)
        top_sentences = sentence_scores[:max_sentences]
        
        # Sort selected sentences by original order
        top_sentences.sort(key=lambda x: x[0])
        summary_sentences = [s[1] for s in top_sentences]
    
    summary = '. '.join(summary_sentences) + '.'
    
    result = {
        "operation": "text_summarization",
        "inputs": {
            "original_length": len(text),
            "original_sentences": len(sentences),
            "max_sentences": max_sentences
        },
        "summary": summary,
        "summary_length": len(summary),
        "sentences_used": len(summary_sentences),
        "compression_ratio": len(summary) / len(text) if len(text) > 0 else 0,
        "timestamp": time.time(),
        "success": True
    }
    
    logger.info(f"Summary generated: {len(summary_sentences)} sentences, {result['compression_ratio']:.2%} of original length")
    return result


def validate_format(text: str, format_type: str) -> Dict[str, Any]:
    """
    Validate text format (email, phone, URL, etc.).
    
    Args:
        text: Text to validate
        format_type: Type of format to validate ('email', 'phone', 'url', 'json')
        
    Returns:
        Dictionary with validation results
    """
    logger.info(f"Validating {format_type} format for text: {text[:50]}...")
    
    patterns = {
        'email': r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$',
        'phone': r'^\+?1?[-.\s]?\(?[0-9]{3}\)?[-.\s]?[0-9]{3}[-.\s]?[0-9]{4}$',
        'url': r'^https?://[^\s/$.?#].[^\s]*$',
        'json': None  # Special case for JSON validation
    }
    
    result = {
        "operation": "format_validation",
        "inputs": {
            "text": text,
            "format_type": format_type
        },
        "timestamp": time.time()
    }
    
    try:
        if format_type == 'json':
            import json
            json.loads(text)
            result["is_valid"] = True
            result["success"] = True
        elif format_type in patterns:
            pattern = patterns[format_type]
            is_valid = re.match(pattern, text.strip()) is not None
            result["is_valid"] = is_valid
            result["pattern_used"] = pattern
            result["success"] = True
        else:
            result["is_valid"] = False
            result["error"] = f"Unknown format type: {format_type}"
            result["success"] = False
            
    except Exception as e:
        result["is_valid"] = False
        result["error"] = str(e)
        result["success"] = False
    
    logger.info(f"Validation completed: {result.get('is_valid', False)}")
    return result


def process_batch_texts(texts: List[str], operation: str = "analyze") -> Dict[str, Any]:
    """
    Process multiple texts in batch.
    
    Args:
        texts: List of texts to process
        operation: Operation to perform ('analyze', 'clean', 'keywords')
        
    Returns:
        Dictionary with batch processing results
    """
    logger.info(f"Processing batch of {len(texts)} texts with operation: {operation}")
    
    if not texts:
        return {
            "operation": "batch_text_processing",
            "error": "No texts provided",
            "success": False,
            "timestamp": time.time()
        }
    
    results = []
    failed_count = 0
    
    for i, text in enumerate(texts):
        try:
            if operation == "analyze":
                result = analyze_text(text)
            elif operation == "clean":
                result = clean_text(text)
            elif operation == "keywords":
                result = extract_keywords(text, top_n=5)
            else:
                result = {
                    "error": f"Unknown operation: {operation}",
                    "success": False
                }
            
            result["batch_index"] = i
            results.append(result)
            
            if not result.get("success", False):
                failed_count += 1
                
        except Exception as e:
            results.append({
                "batch_index": i,
                "error": str(e),
                "success": False
            })
            failed_count += 1
    
    batch_result = {
        "operation": "batch_text_processing",
        "inputs": {
            "text_count": len(texts),
            "operation": operation
        },
        "results": results,
        "summary": {
            "total_processed": len(texts),
            "successful": len(texts) - failed_count,
            "failed": failed_count,
            "success_rate": (len(texts) - failed_count) / len(texts) if texts else 0
        },
        "timestamp": time.time(),
        "success": failed_count < len(texts)  # Success if at least one succeeded
    }
    
    logger.info(f"Batch processing completed: {batch_result['summary']['successful']}/{len(texts)} successful")
    return batch_result