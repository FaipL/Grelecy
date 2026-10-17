#!/usr/bin/env python3
# SPDX-License-Identifier: MPL-2.0
"""Build both static languages using only the Python standard library."""

import argparse
import json
import shutil
from html import escape
from pathlib import Path
from string import Template
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "website"
OUTPUT = ROOT / "_site"
DEFAULT_URL = "https://faipl.github.io/Grelecy"


def build(base_url: str) -> None:
    base_url = base_url.rstrip("/")
    parsed = urlsplit(base_url)
    if parsed.scheme != "https" or not parsed.netloc or parsed.query or parsed.fragment:
        raise ValueError("The public base URL must be an absolute HTTPS URL without query or fragment")

    template = Template((SOURCE / "index.template.html").read_text(encoding="utf-8"))
    locales = {
        lang: json.loads((SOURCE / "locales" / f"{lang}.json").read_text(encoding="utf-8"))
        for lang in ("fr", "en")
    }
    if locales["fr"].keys() != locales["en"].keys():
        raise ValueError("French and English must provide the same translation keys")

    for language, strings in locales.items():
        directory = OUTPUT if language == "fr" else OUTPUT / "en"
        directory.mkdir(parents=True, exist_ok=True)
        values = {key: escape(value, quote=True) for key, value in strings.items()}
        values.update({
            "language": language,
            "assets": "./assets" if language == "fr" else "../assets",
            "home": "./",
            "fr_href": "./" if language == "fr" else "../",
            "en_href": "./en/" if language == "fr" else "./",
            "fr_current": 'aria-current="page"' if language == "fr" else "",
            "en_current": 'aria-current="page"' if language == "en" else "",
            "canonical": escape(base_url + ("/" if language == "fr" else "/en/"), quote=True),
            "french_url": escape(base_url + "/", quote=True),
            "english_url": escape(base_url + "/en/", quote=True),
            "social_image": escape(base_url + "/assets/brand/grelecy-banner.png", quote=True),
        })
        (directory / "index.html").write_text(template.substitute(values), encoding="utf-8")

    brand_dir = OUTPUT / "assets" / "brand"
    brand_dir.mkdir(parents=True, exist_ok=True)
    for name in ("grelecy-logo.png", "grelecy-banner.png"):
        shutil.copyfile(ROOT / "assets" / "brand" / name, brand_dir / name)
    shutil.copyfile(SOURCE / "site.css", OUTPUT / "assets" / "site.css")
    shutil.copytree(SOURCE / "fonts", OUTPUT / "assets" / "fonts", dirs_exist_ok=True)
    (OUTPUT / ".nojekyll").touch()
    (OUTPUT / "robots.txt").write_text(
        f"User-agent: *\nAllow: /\nSitemap: {base_url}/sitemap.xml\n", encoding="utf-8"
    )
    (OUTPUT / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
        + "".join(f"  <url><loc>{escape(base_url + path)}</loc></url>\n" for path in ("/", "/en/"))
        + "</urlset>\n",
        encoding="utf-8",
    )
    print(f"Built French and English pages in {OUTPUT}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default=DEFAULT_URL, help="Public site URL (for canonical and social metadata)")
    build(parser.parse_args().base_url)
