"""
Text extraction utilities for HTML content.

This module provides utilities for extracting and processing text content
from HTML documents, including handling of code blocks and text chunking.
"""

from typing import List
from bs4 import BeautifulSoup


class TextExtractor:
    """
    Helper class for extracting and processing text content from HTML.

    This class provides static methods for extracting text content,
    code blocks, and splitting long text into manageable chunks.
    """

    @staticmethod
    def split_text_on_words(text: str, max_length: int = 5000) -> str:
        """
        Split text into chunks at word boundaries.

        Args:
            text: The text to split
            max_length: Maximum length of each chunk

        Returns:
            Text split into chunks joined with '\\n\\n'
        """
        if len(text) <= max_length:
            return text

        chunks = []
        current_chunk = ""

        # Split by words
        words = text.split()

        for word in words:
            # If adding this word would exceed the limit
            if len(current_chunk) + len(word) + 1 > max_length:
                if current_chunk:
                    chunks.append(current_chunk.strip())
                    current_chunk = word
                else:
                    # Single word is too long, add it anyway
                    chunks.append(word)
            else:
                if current_chunk:
                    current_chunk += " " + word
                else:
                    current_chunk = word

        # Add the last chunk
        if current_chunk:
            chunks.append(current_chunk.strip())

        # Join chunks with '\n\n'
        return '\n\n'.join(chunks)

    @staticmethod
    def extract_text_content(soup: BeautifulSoup, max_length: int = 5000) -> str:
        """
        Extract main text content from page, structured by headings.

        Args:
            soup: BeautifulSoup parsed HTML document
            max_length: Maximum length for text chunks

        Returns:
            Extracted text content, potentially split into chunks
        """
        # Remove script and style elements
        for script in soup(["script", "style", "nav", "footer", "header"]):
            script.decompose()

        # Try to find main content areas
        main_content = soup.find('main') or soup.find('article') or soup.find('div', class_='content')
        if not main_content:
            main_content = soup

        # Find all heading tags
        headings = main_content.find_all(['h1', 'h2', 'h3', 'h4', 'h5', 'h6'])

        if not headings:
            # No headings found, return all text
            text = main_content.get_text(separator='\n', strip=True)
            lines = [line.strip() for line in text.splitlines() if line.strip()]
            content = '\n\n' + '\n'.join(lines)
            return TextExtractor.split_text_on_words(content, max_length=max_length)

        sections = []

        for heading in headings:
            # Get heading text
            heading_text = heading.get_text(strip=True)

            # Get content between this heading and the next one
            content_elements = []
            for sibling in heading.next_siblings:
                # Stop at next heading
                if sibling.name and sibling.name in ['h1', 'h2', 'h3', 'h4']:
                    break
                # Get text from sibling if it has text
                if hasattr(sibling, 'get_text'):
                    text = sibling.get_text(strip=True)
                    if text:
                        content_elements.append(text)
                elif isinstance(sibling, str):
                    text = sibling.strip()
                    if text:
                        content_elements.append(text)

            # Join content
            content_text = '\n'.join(content_elements)

            # Create section: '\n\n' + heading + '\n' + content
            if heading_text or content_text:
                section = '\n\n' + heading_text
                if content_text:
                    section += '\n' + content_text
                sections.append(section)

        # Join all sections
        content = ''.join(sections) if sections else '\n\n'

        # Split if content is longer than max_length
        return TextExtractor.split_text_on_words(content, max_length=max_length)

    @staticmethod
    def extract_code_blocks(soup: BeautifulSoup) -> List[str]:
        """
        Extract all code examples from the page.

        Handles nested elements like <span> tags used for syntax highlighting.

        Args:
            soup: BeautifulSoup parsed HTML document

        Returns:
            List of code block strings
        """
        code_blocks = []

        # First, extract from <pre> tags (which may contain nested <code> and <span> elements)
        for pre in soup.find_all('pre'):
            # Use get_text() to extract all text from nested elements
            # separator='' preserves natural spacing, strip=False keeps internal whitespace
            code_text = pre.get_text().strip()
            if code_text:
                code_blocks.append(code_text)

        # Then, extract from <code> tags that are NOT inside <pre>
        for code in soup.find_all('code'):
            # Skip if this code is inside a pre tag (already extracted)
            if code.find_parent('pre'):
                continue
            # For inline code, extract text with natural spacing
            code_text = code.get_text().strip()
            if code_text:
                code_blocks.append(code_text)

        return code_blocks
