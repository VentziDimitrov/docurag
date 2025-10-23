"""
Web Crawler Package for Technical Documentation

This package provides tools for crawling and extracting content from
technical documentation websites.
"""

from .docu_crawler import DocumentationCrawler
from .text_extractor import TextExtractor

__all__ = ['DocumentationCrawler', 'TextExtractor']
__version__ = '1.0.0'
