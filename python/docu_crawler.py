"""
Documentation crawler for extracting content from technical documentation websites.
"""

import logging
import time
from typing import List, Set
from urllib.parse import urljoin, urlparse

from bs4 import BeautifulSoup
import requests

try:
    from .text_extractor import TextExtractor
    from .models import CrawlerConfig, CrawledDocument
except ImportError:
    from text_extractor import TextExtractor
    from models import CrawlerConfig, CrawledDocument

# Configure logging
logger = logging.getLogger(__name__)

# Constants
SKIP_EXTENSIONS = ('.pdf', '.zip', '.jpg', '.png', '.gif', '.css', '.js')
HEADING_TAGS = ('h1', 'h2', 'h3', 'h4', 'h5', 'h6')


class DocumentationCrawler:
    """
    Crawler for extracting content from technical documentation websites.

    This crawler recursively visits pages within a domain, extracts text content
    and code blocks, and respects rate limits.
    """

    def __init__(self, config: CrawlerConfig):
        """
        Initialize the documentation crawler.

        Args:
            config: Configuration object containing crawl parameters
        """
        self.config = config
        self.base_url = config.base_url
        self.max_depth = config.max_depth
        self.max_pages = config.max_pages
        self.visited: Set[str] = set()
        self.documents: List[CrawledDocument] = []
        self.base_domain = urlparse(config.base_url).netloc

        # Use a session for better performance
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': config.user_agent
        })

        logger.info(f"Initialized crawler for {self.base_url} (max_depth={self.max_depth}, max_pages={self.max_pages})")

    def is_valid_url(self, url: str) -> bool:
        """
        Check if URL should be crawled.

        Args:
            url: The URL to validate

        Returns:
            True if URL should be crawled, False otherwise
        """
        parsed = urlparse(url)

        # Only crawl same domain
        if parsed.netloc != self.base_domain:
            logger.debug(f"Skipping {url}: different domain")
            return False

        # Skip common non-documentation files
        if any(url.lower().endswith(ext) for ext in SKIP_EXTENSIONS):
            logger.debug(f"Skipping {url}: excluded extension")
            return False

        # Skip anchors
        if '#' in url:
            url = url.split('#')[0]

        return url not in self.visited

    def crawl_page(self, url: str, depth: int) -> None:
        """
        Crawl a single page and extract content.

        Args:
            url: The URL to crawl
            depth: Current depth in the crawl tree
        """
        if depth > self.max_depth or len(self.visited) >= self.max_pages:
            logger.debug(f"Stopping crawl: depth={depth}, visited={len(self.visited)}")
            return

        if url in self.visited:
            return

        try:
            logger.info(f"Crawling [{depth}]: {url}")
            self.visited.add(url)

            response = self.session.get(url, timeout=self.config.timeout)
            response.raise_for_status()

            soup = BeautifulSoup(response.content, 'html.parser')

            # Extract content
            title = soup.find('title')
            title_text = title.get_text(strip=True) if title else url
            logger.debug(f"Page title: {title_text}")

            content = TextExtractor.extract_text_content(soup)
            code_blocks = TextExtractor.extract_code_blocks(soup)

            # Create document using dataclass
            document = CrawledDocument(
                url=url,
                title=title_text,
                content=content,
                code_blocks=code_blocks,
                depth=depth,
                crawled_at=time.strftime('%Y-%m-%d %H:%M:%S')
            )

            self.documents.append(document)
            logger.info(f"Extracted {len(code_blocks)} code blocks from {url}")

            # Find and queue links
            links = soup.find_all('a', href=True)
            logger.debug(f"Found {len(links)} links on {url}")

            sub_string = url[:20]
            for link in links:
                href = link['href']
                if href.startswith(sub_string) or href.startswith('/'):
                    next_url = href
                    if next_url.startswith('/'):
                        next_url = urljoin(self.base_url, next_url)

                    self.crawl_page(next_url, depth + 1)

            # Be polite - rate limiting
            time.sleep(self.config.rate_limit_delay)

        except requests.RequestException as e:
            logger.error(f"Error crawling {url}: {e}")
        except Exception as e:
            logger.error(f"Unexpected error on {url}: {e}", exc_info=True)

    def crawl(self) -> List[CrawledDocument]:
        """
        Start crawling from the base URL.

        Returns:
            List of crawled documents
        """
        logger.info(f"Starting crawl from {self.base_url}")
        self.crawl_page(self.base_url, 0)
        logger.info(f"Crawl completed. Visited {len(self.visited)} pages, extracted {len(self.documents)} documents")
        return self.documents
