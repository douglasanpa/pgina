"""Refreshes the translation catalogs (Languages/<lang>.po) of every C# project.

Collects the user-visible English texts of each project -- designer captions, tooltips
and column headers, .resx texts, and the Language.T / Language.Format calls in code --
and writes them to the project's catalog. Translations already present in any catalog
of that language are kept and reused, so a text shared by several projects only has to
be translated once. Texts no longer used are dropped.

usage: python tools/i18n/update-po.py [--lang pt-BR] [--check]

--check  only report untranslated texts; exits with 1 if there are any.
"""
import argparse
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

LIT = r'"((?:[^"\\]|\\.)*)"'
# a literal, or several joined with + (the designer splits long texts)
LITS = r'((?:"(?:[^"\\]|\\.)*"\s*\+\s*)*"(?:[^"\\]|\\.)*")'
DESIGNER = [
    re.compile(r'\.(?:Text|HeaderText|ToolTipText|Caption)\s*=\s*' + LITS),
    re.compile(r'SetToolTip\(this\.\w+,\s*' + LITS + r'\)'),
]
CODE = re.compile(r'Language\.(?:T|Format)\(\s*' + LITS)


def join_lits(s):
    return "".join(re.findall(LIT, s))


def unescape(s):
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == "\\" and i + 1 < len(s):
            n = s[i + 1]
            out.append({"n": "\n", "r": "\r", "t": "\t", "0": "\0"}.get(n, n))
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def wanted(s):
    # skip empty texts and things like "...", "%", "->"
    return bool(re.search(r"[A-Za-z]{2}", s.strip()))


def skip(path):
    parts = path.split(os.sep)
    return "obj" in parts or "bin" in parts


def project_dirs():
    for p in sorted(glob.glob(os.path.join(REPO, "**", "*.csproj"), recursive=True)):
        if not skip(p):
            yield os.path.dirname(p)


def files(d, pattern):
    for f in sorted(glob.glob(os.path.join(d, "**", pattern), recursive=True)):
        if not skip(f):
            yield f


def collect(d):
    """English texts of one project, in source order."""
    found = []
    for f in files(d, "*.Designer.cs"):
        src = open(f, encoding="utf-8-sig").read()
        for rx in DESIGNER:
            for m in rx.finditer(src):
                found.append(unescape(join_lits(m.group(1))))
    for f in files(d, "*.resx"):
        try:
            root = ET.parse(f).getroot()
        except ET.ParseError:
            continue
        for data in root.findall("data"):
            name = data.get("name", "")
            if name.startswith(">>") or data.get("type") or data.get("mimetype"):
                continue
            if name.endswith((".Text", ".ToolTip", ".ToolTipText")):
                v = data.find("value")
                if v is not None and v.text:
                    found.append(v.text.replace("\r\n", "\n"))
    for f in files(d, "*.cs"):
        if f.endswith(".Designer.cs"):
            continue
        src = open(f, encoding="utf-8-sig").read()
        for m in CODE.finditer(src):
            found.append(unescape(join_lits(m.group(1))))
    seen, result = set(), []
    for s in found:
        if wanted(s) and s not in seen:
            seen.add(s)
            result.append(s)
    return result


# --- PO files -------------------------------------------------------------

def po_unquote(line):
    return unescape(line.strip()[1:-1])


def read_po(path):
    """msgid -> msgstr of one catalog (header and empty translations left out)."""
    result, key, cur, field = {}, None, [], None

    def flush():
        if field == "msgstr" and key and "".join(cur):
            result[key] = "".join(cur)

    for line in open(path, encoding="utf-8-sig"):
        line = line.rstrip("\r\n")
        if line.startswith("msgid "):
            flush()
            field, cur = "msgid", [po_unquote(line[6:])]
        elif line.startswith("msgstr "):
            key = "".join(cur)
            field, cur = "msgstr", [po_unquote(line[7:])]
        elif line.startswith('"'):
            cur.append(po_unquote(line))
    flush()
    return result


def po_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"').replace("\t", "\\t").replace("\r", "\\r").replace("\n", "\\n")


def po_quote(s):
    if "\n" in s.rstrip("\n"):
        parts = s.split("\n")
        lines = ['""']
        for i, p in enumerate(parts):
            if i < len(parts) - 1:
                lines.append('"%s"' % po_escape(p + "\n"))
            elif p:
                lines.append('"%s"' % po_escape(p))
        return "\n".join(lines)
    return '"%s"' % po_escape(s)


HEADER = '''msgid ""
msgstr ""
"Project-Id-Version: pGina\\n"
"Language: %s\\n"
"MIME-Version: 1.0\\n"
"Content-Type: text/plain; charset=UTF-8\\n"
"Content-Transfer-Encoding: 8bit\\n"
'''


def write_po(path, lang, strings, tr):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out = [HEADER % lang.replace("-", "_")]
    for s in strings:
        out.append("msgid %s\nmsgstr %s\n" % (po_quote(s), po_quote(tr.get(s, ""))))
    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("\n".join(out))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--lang", default="pt-BR")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    projects = [(d, collect(d)) for d in project_dirs()]
    projects = [(d, s) for d, s in projects if s]

    # every translation of this language, from all catalogs
    memory = {}
    for d, _ in projects:
        po = os.path.join(d, "Languages", args.lang + ".po")
        if os.path.exists(po):
            memory.update(read_po(po))

    missing = set()
    for d, strings in projects:
        todo = [s for s in strings if not memory.get(s)]
        missing.update(todo)
        if not args.check:
            write_po(os.path.join(d, "Languages", args.lang + ".po"), args.lang, strings, memory)
        print("%4d texts, %3d untranslated  %s" % (len(strings), len(todo), os.path.relpath(d, REPO)))

    if missing:
        print("\nuntranslated:")
        for s in sorted(missing):
            print("  " + json.dumps(s, ensure_ascii=False))
        if args.check:
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
