#!/usr/bin/env python3
"""Builds the OpenDIAL manual as one HTML file and one PDF.

The pages under docs/manual are the same ones the application embeds in its Help window; this
compiles them in table-of-contents order into a single document, turns the [[wikilinks]] into
internal links, renders the HTML with pandoc and prints the PDF with Google Chrome.

    opendial/scripts/build-manual.py [--out DIR]     -> dist/OpenDIAL-manual-<version>.html and .pdf

Needs pandoc; the PDF additionally needs Google Chrome (or Chromium) and is skipped with a note
when neither is installed.
"""

from __future__ import annotations

import argparse
import html
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MANUAL = os.path.join(ROOT, "docs", "manual")
IMAGES = os.path.join(ROOT, "docs", "images")

SECTION_ORDER = ["Start", "Workspaces", "Reviewing", "Data and files", "Reference", "Under the hood", "Help"]
# what the sections and the fixed strings are called in each language the manual exists in
LANGUAGES = {
    "en": {"folder": MANUAL, "sections": {s: s for s in SECTION_ORDER}, "subtitle": "The manual", "tracks": "tracks MS-DIAL 5.5.260817",
           "referenced": "Referenced by", "title": "OpenDIAL manual"},
    "pt": {"folder": os.path.join(MANUAL, "pt"),
           "sections": {"Start": "Início", "Workspaces": "Áreas de trabalho", "Reviewing": "Revisão", "Data and files": "Dados e arquivos",
                        "Reference": "Referência", "Under the hood": "Por dentro", "Help": "Ajuda"},
           "subtitle": "O manual", "tracks": "acompanha o MS-DIAL 5.5.260817", "referenced": "Referenciado por", "title": "Manual do OpenDIAL"},
}

FRONT = re.compile(r"\A---\s*\n(.*?)\n---\s*\n", re.S)
WIKI = re.compile(r"\[\[([^\]\|#]+)(?:#([^\]\|]*))?(?:\|([^\]]*))?\]\]")
HEADING = re.compile(r"^(#{1,6})\s+(.+?)\s*$", re.M)

CHROMES = [
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/Applications/Chromium.app/Contents/MacOS/Chromium",
    "google-chrome", "chromium", "chromium-browser",
]

CSS = """
@page { size: A4; margin: 18mm 16mm 20mm 16mm; }
html { font-family: -apple-system, "Inter", "Helvetica Neue", Helvetica, Arial, sans-serif; font-size: 10.5pt; line-height: 1.5; color: #1a1d21; }
body { max-width: 180mm; margin: 0 auto; padding: 0 4mm; }
h1 { font-size: 22pt; font-weight: 600; margin: 0 0 6pt; page-break-before: always; letter-spacing: -0.2pt; }
h1.first, h1#toc-title { page-break-before: auto; }
h2 { font-size: 14.5pt; font-weight: 600; margin: 18pt 0 6pt; border-bottom: 1px solid #e3e5ea; padding-bottom: 3pt; }
h3 { font-size: 11.5pt; font-weight: 600; margin: 14pt 0 4pt; }
p { margin: 0 0 8pt; }
p.summary { color: #6b7280; font-style: italic; margin-bottom: 12pt; }
p.section { color: #6b7280; font-size: 8.5pt; letter-spacing: 1.2pt; text-transform: uppercase; margin: 0 0 2pt; }
a { color: #234b8c; text-decoration: none; }
code { font-family: "JetBrains Mono", Menlo, "SF Mono", Consolas, monospace; font-size: 9pt; background: #f5f5f7; padding: 0 3px; border-radius: 3px; }
pre { background: #f5f5f7; border: 1px solid #e3e5ea; border-radius: 6px; padding: 8pt 10pt; font-size: 8.5pt; line-height: 1.4; overflow-x: auto; white-space: pre-wrap; }
pre code { background: none; padding: 0; font-size: inherit; }
table { border-collapse: collapse; width: 100%; margin: 4pt 0 12pt; font-size: 9.5pt; page-break-inside: auto; }
th { text-align: left; background: #fafafc; color: #6b7280; font-weight: 600; border-bottom: 1px solid #cfd3da; padding: 4pt 6pt; }
td { border-bottom: 1px solid #e3e5ea; padding: 4pt 6pt; vertical-align: top; }
tr { page-break-inside: avoid; }
blockquote { border-left: 3px solid #e7edf7; margin: 0 0 8pt; padding: 2pt 0 2pt 10pt; color: #6b7280; }
img { max-width: 100%; border: 1px solid #e3e5ea; border-radius: 6px; margin: 4pt 0 10pt; page-break-inside: avoid; }
ul, ol { margin: 0 0 8pt; padding-left: 20pt; }
li { margin-bottom: 2pt; }
hr { border: 0; border-top: 1px solid #e3e5ea; margin: 12pt 0; }
#TOC { page-break-after: always; }
#TOC ul { list-style: none; padding-left: 0; }
#TOC > ul > li { margin-bottom: 4pt; font-weight: 600; }
#TOC > ul > li > ul { font-weight: 400; padding-left: 14pt; }
.cover { text-align: center; padding-top: 60mm; page-break-after: always; }
.cover .wordmark { font-family: Didot, "Bodoni 72", Georgia, serif; font-size: 44pt; font-weight: 300; letter-spacing: 3pt; }
.cover .sub { font-size: 13pt; color: #6b7280; margin-top: 8pt; }
.cover .version { font-size: 10.5pt; color: #9aa1ac; margin-top: 30pt; }
.referenced { color: #6b7280; font-size: 9pt; margin-top: 14pt; }
"""


def version() -> str:
    props = os.path.join(ROOT, "Directory.Build.props")
    match = re.search(r"<Version>([^<]+)</Version>", open(props, encoding="utf-8").read())
    return match.group(1) if match else "0.0.0"


def slugify(text: str) -> str:
    text = re.sub(r"[^\w\s-]", "", text.lower())
    return re.sub(r"[\s_]+", "-", text).strip("-")


def read_pages(folder: str = MANUAL) -> list[dict]:
    pages = []
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".md"):
            continue
        slug = name[:-3]
        text = open(os.path.join(folder, name), encoding="utf-8").read().replace("\r\n", "\n")
        meta = {"title": slug, "section": "Reference", "order": 999, "summary": ""}
        match = FRONT.match(text)
        body = text
        if match:
            body = text[match.end():]
            for line in match.group(1).split("\n"):
                if ":" in line:
                    key, value = line.split(":", 1)
                    meta[key.strip().lower()] = value.strip().strip('"')
        meta["order"] = int(meta["order"])
        pages.append({"slug": slug, "body": body, **meta})
    order = {s: i for i, s in enumerate(SECTION_ORDER)}
    pages.sort(key=lambda p: (order.get(p["section"], len(order)), p["order"], p["title"]))
    return pages


def compile_markdown(pages: list[dict], images_dir: str, lang: str = "en") -> str:
    words = LANGUAGES[lang]
    by_slug = {p["slug"]: p for p in pages}
    by_title = {p["title"].lower(): p for p in pages}
    links_to: dict[str, list[str]] = {p["slug"]: [] for p in pages}

    def resolve(name: str):
        name = name.strip()
        return by_slug.get(name) or by_title.get(name.lower()) or by_slug.get(name.replace(" ", "-"))

    for page in pages:
        for m in WIKI.finditer(page["body"]):
            target = resolve(m.group(1))
            if target and target["slug"] != page["slug"] and page["slug"] not in links_to[target["slug"]]:
                links_to[target["slug"]].append(page["slug"])

    out = [f'<div class="cover"><div class="wordmark">OpenDIAL</div><div class="sub">{words["subtitle"]}</div>'
           f'<div class="version">{"versão" if lang == "pt" else "version"} {version()} · {words["tracks"]}</div></div>\n']
    for page in pages:
        body = page["body"]

        def wiki(m: re.Match) -> str:
            target = resolve(m.group(1))
            anchor = (m.group(2) or "").strip()
            label = m.group(3) if m.group(3) is not None else (f"{m.group(1).strip()} › {anchor}" if anchor else (target["title"] if target else m.group(1).strip()))
            if target is None:
                return label
            href = "#" + target["slug"] + (("-" + slugify(anchor)) if anchor else "")
            return f"[{label}]({href})"

        body = WIKI.sub(wiki, body)
        # every page opens with its own H1; the rest of its headings are demoted one level, and every
        # heading gets an id derived from the page so anchors are unique across the document
        lines = []
        first = True
        for line in body.split("\n"):
            m = HEADING.match(line)
            if m:
                level = len(m.group(1))
                text = m.group(2)
                if first and level == 1:
                    first = False
                    lines.append(f'<p class="section">{html.escape(words["sections"].get(page["section"], page["section"]))}</p>')
                    lines.append(f'# {text} {{#{page["slug"]}}}')
                    if page["summary"]:
                        lines.append("")
                        lines.append(f'<p class="summary">{html.escape(page["summary"])}</p>')
                    continue
                anchor = page["slug"] + "-" + slugify(re.sub(r"[*`]", "", text))
                lines.append(f'{"#" * min(6, level + 1)} {text} {{#{anchor}}}')
            else:
                lines.append(line)
        body = "\n".join(lines)
        body = body.replace("](images/", f"]({images_dir}/")
        if links_to[page["slug"]]:
            refs = ", ".join(f'[{by_slug[s]["title"]}](#{s})' for s in links_to[page["slug"]])
            body += f'\n\n<p class="referenced">{words["referenced"]}: {refs}</p>\n'
        out.append(body)
        out.append("\n")
    return "\n".join(out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=os.path.join(ROOT, "dist"), help="output folder (default: dist)")
    parser.add_argument("--no-pdf", action="store_true", help="only the HTML")
    parser.add_argument("--lang", default="en", choices=sorted(LANGUAGES), help="which manual: en (default) or pt")
    args = parser.parse_args()

    if shutil.which("pandoc") is None:
        print("pandoc is needed: brew install pandoc", file=sys.stderr)
        return 2
    os.makedirs(args.out, exist_ok=True)
    pages = read_pages(LANGUAGES[args.lang]["folder"])
    stem = f"OpenDIAL-manual-{version()}" if args.lang == "en" else f"OpenDIAL-manual-{args.lang}-{version()}"
    html_path = os.path.join(args.out, stem + ".html")
    pdf_path = os.path.join(args.out, stem + ".pdf")

    with tempfile.TemporaryDirectory() as work:
        markdown = compile_markdown(pages, IMAGES, args.lang)
        md_path = os.path.join(work, "manual.md")
        css_path = os.path.join(work, "manual.css")
        open(md_path, "w", encoding="utf-8").write(markdown)
        open(css_path, "w", encoding="utf-8").write(CSS)
        subprocess.run([
            "pandoc", md_path, "-f", "markdown+pipe_tables+fenced_code_blocks+raw_html+header_attributes+implicit_figures",
            "-t", "html5", "--standalone", "--toc", "--toc-depth=2", "--embed-resources",
            "--metadata", f"title={LANGUAGES[args.lang]['title']} {version()}", "--metadata", f"lang={args.lang}",
            "-c", css_path, "-o", html_path,
        ], check=True)
        # pandoc's own title block duplicates the cover; drop it
        text = open(html_path, encoding="utf-8").read()
        text = re.sub(r'<header id="title-block-header">.*?</header>', "", text, flags=re.S)
        open(html_path, "w", encoding="utf-8").write(text)
    print(f"html: {html_path} ({len(pages)} pages)")

    if args.no_pdf:
        return 0
    chrome = next((c for c in CHROMES if os.path.exists(c) or shutil.which(c)), None)
    if chrome is None:
        print("no Chrome or Chromium found; the PDF was not built", file=sys.stderr)
        return 0
    result = subprocess.run([
        chrome, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
        f"--print-to-pdf={pdf_path}", "file://" + html_path,
    ], capture_output=True, text=True)
    if result.returncode != 0 or not os.path.exists(pdf_path):
        print("the PDF could not be printed: " + (result.stderr.strip() or result.stdout.strip()), file=sys.stderr)
        return 1
    print(f"pdf:  {pdf_path} ({os.path.getsize(pdf_path) / 1024 / 1024:.1f} MB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
