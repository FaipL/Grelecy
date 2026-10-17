#!/usr/bin/env python3
# SPDX-License-Identifier: MPL-2.0
"""Check generated pages for missing local resources, anchors and language links."""

from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1] / "_site"


class Page(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.path = path
        self.ids = set()
        self.references = []
        self.language = None
        self.headings = 0
        self.current_languages = 0
        self.feed(path.read_text(encoding="utf-8"))

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if "id" in values:
            if values["id"] in self.ids:
                raise ValueError(f"Duplicate ID in {self.path}: {values['id']}")
            self.ids.add(values["id"])
        if tag == "html":
            self.language = values.get("lang")
        if tag == "h1":
            self.headings += 1
        if tag == "a" and values.get("aria-current") == "page" and values.get("hreflang"):
            self.current_languages += 1
        for attribute in ("href", "src"):
            if attribute in values:
                self.references.append(values[attribute])


def check():
    paths = (ROOT / "index.html", ROOT / "en" / "index.html")
    pages = {path: Page(path) for path in paths}
    count = 0
    for path, page in pages.items():
        expected_language = "en" if path.parent.name == "en" else "fr"
        if page.language != expected_language or page.headings != 1 or page.current_languages != 1:
            raise ValueError(f"Invalid language, main heading or selected language in {path}")
        for reference in page.references:
            url = urlsplit(reference)
            if url.scheme or url.netloc:
                if url.scheme != "https":
                    raise ValueError(f"Unexpected non-HTTPS link in {path}: {reference}")
                continue
            target = (path.parent / unquote(url.path)).resolve() if url.path else path
            if not target.is_relative_to(ROOT):
                raise ValueError(f"Local resource escapes the built site: {reference}")
            if target.is_dir():
                target /= "index.html"
            if not target.is_file():
                raise ValueError(f"Missing local resource in {path}: {reference}")
            if url.fragment and target in pages and unquote(url.fragment) not in pages[target].ids:
                raise ValueError(f"Missing anchor in {path}: {reference}")
            count += 1
    print(f"PASS: 2 localized pages, unique main headings, language selection and {count} local references")


if __name__ == "__main__":
    check()
