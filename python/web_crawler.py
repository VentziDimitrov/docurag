#!/usr/bin/env python3
"""
Web Crawler for Technical Documentation
Uses BeautifulSoup4 for simple crawling with depth control
"""

import argparse
import json
import sys
from urllib.parse import urljoin, urlparse
from collections import deque
import time
from typing import List, Dict, Set

# Check for required dependencies
try:
    import requests
except ImportError:
    print("ERROR: 'requests' module not found. Please install it:", file=sys.stderr)
    print("  pip install requests", file=sys.stderr)
    sys.exit(1)

try:
    from bs4 import BeautifulSoup
except ImportError:
    print("ERROR: 'beautifulsoup4' module not found. Please install it:", file=sys.stderr)
    print("  pip install beautifulsoup4", file=sys.stderr)
    sys.exit(1)

class DocumentationCrawler:
    def __init__(self, base_url: str, max_depth: int = 3, max_pages: int = 200):
        self.base_url = base_url
        self.max_depth = max_depth
        self.max_pages = max_pages
        self.visited: Set[str] = set()
        self.documents: List[Dict] = []
        self.base_domain = urlparse(base_url).netloc
        
        # Headers to mimic browser
        self.headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
        }

    def is_valid_url(self, url: str) -> bool:
        """Check if URL should be crawled"""
        parsed = urlparse(url)
        
        # Only crawl same domain
        if parsed.netloc != self.base_domain:
            return False
        
        # Skip common non-documentation files
        skip_extensions = ['.pdf', '.zip', '.jpg', '.png', '.gif', '.css', '.js']
        if any(url.lower().endswith(ext) for ext in skip_extensions):
            return False
        
        # Skip anchors
        if '#' in url:
            url = url.split('#')[0]
        
        return url not in self.visited

    def split_text_on_words(self, text: str, max_length: int = 5000) -> str:
        """Split text into chunks at word boundaries, joining with \\n\\n"""
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

    def extract_text_content(self, soup: BeautifulSoup) -> str:
        """Extract main text content from page, structured by headings"""
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
            return self.split_text_on_words(content, max_length=5000)

        sections = []

        for heading in headings:
            # Get heading text
            heading_text = heading.get_text(strip=True)

            # Get content between this heading and the next one
            content_elements = []
            for sibling in heading.next_siblings:
                # Stop at next heading
                if sibling.name and sibling.name in ['h1', 'h2', 'h3', 'h4', 'h5', 'h6']:
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

        # Split if content is longer than 5000 characters
        return self.split_text_on_words(content, max_length=5000)

    def extract_code_blocks(self, soup: BeautifulSoup) -> List[str]:
        """Extract all code examples from the page (handles nested elements)"""
        code_blocks = []

        # First, extract from <pre> tags (which may contain nested <code>)
        for pre in soup.find_all('pre'):
            code_text = pre.get_text(strip=True)
            if code_text:
                code_blocks.append(code_text)

        # Then, extract from <code> tags that are NOT inside <pre>
        for code in soup.find_all('code'):
            # Skip if this code is inside a pre tag (already extracted)
            if code.find_parent('pre'):
                continue
            code_text = code.get_text(strip=True)
            if code_text:
                code_blocks.append(code_text)

        return code_blocks

    def crawl_page(self, url: str, depth: int) -> None:
        """Crawl a single page and extract content"""
        if depth > self.max_depth or len(self.visited) >= self.max_pages:
            return
        
        if url in self.visited:
            return
        
        try:
            #print(f"Crawling [{depth}]: {url}")
            self.visited.add(url)
            
            response = requests.get(url, headers=self.headers, timeout=10)
            response.raise_for_status()
            
            soup = BeautifulSoup(response.content, 'html.parser')
            # print(f"RAW page {url}: {soup}", file=sys.stdout)
            
            # Extract content
            title = soup.find('title')            
            title_text = title.get_text(strip=True) if title else url
            #print(f"RAW page title: {title_text}", file=sys.stdout)
            
            content = self.extract_text_content(soup)
            # print(f"RAW page content: {content}", file=sys.stdout)
            code_blocks = self.extract_code_blocks(soup)
            
            # Store document
            document = {
                'url': url,
                'title': title_text,
                'content': content,
                'metadata': {
                    'code_blocks': code_blocks,
                    'depth': depth,
                    'crawled_at': time.strftime('%Y-%m-%d %H:%M:%S')
                }
            }
            
            self.documents.append(document)
            
            # Find and queue links
            links = soup.find_all('a', href=True)
            #print(f"RAW links found on {url}: {[link['href'] for link in links]}")
            sub_string = url[:20]
            for link in links:
                if (link['href'].startswith(sub_string) or link['href'].startswith('/')):
                    next_url = link['href']                    
                    if next_url.startswith('/'):
                        next_url = urljoin(self.base_url, next_url)
                        
                    #print(f"RAW next_url: {next_url}")
                    #print(f"Crawling depth: {depth + 1}")
                    self.crawl_page(next_url, depth + 1)
                        
            
            # Be polite - rate limiting
            time.sleep(0.5)
            
        except requests.RequestException as e:
            print(f"Error crawling {url}: {e}", file=sys.stderr)
        except Exception as e:
            print(f"Unexpected error on {url}: {e}", file=sys.stderr)

    def crawl(self) -> List[Dict]:
        """Start crawling from base URL"""
        
        self.crawl_page(self.base_url, 0)
        
        return self.documents


def main():
    parser = argparse.ArgumentParser(description='Crawl technical documentation websites')
    parser.add_argument('--url', required=True, help='Base URL to start crawling')
    parser.add_argument('--output', required=True, help='Output JSON file path')
    parser.add_argument('--max-depth', type=int, default=3, help='Maximum crawl depth')
    parser.add_argument('--max-pages', type=int, default=100, help='Maximum pages to crawl')
    
    args = parser.parse_args()
    
    try:
        crawler = DocumentationCrawler(
            base_url=args.url,
            max_depth=args.max_depth,
            max_pages=args.max_pages
        )
        
        documents = crawler.crawl()
        
        # Write output
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(documents, f, ensure_ascii=False, indent=2)
        
        json_value = {
            "docs": documents,
            "status": "success"
        }
        
        #print(f"Success! Crawled {len(documents)} documents", file=sys.stdout)
        print(json.dumps(json_value, ensure_ascii=False, indent=2), file=sys.stdout)
        sys.exit(0)
        
    except Exception as e:
        print(f"Fatal error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()