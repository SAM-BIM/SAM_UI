"""Guard rails for the icon redesign diff.

  python check_source.py [base]      (default: merge-base of origin/sow/2026-Q3 and HEAD, so upstream moves are ignored)

1. Vendored design-system files are byte-identical to SAM-BIM/SAM#166 @ cf4d924a (SHA-256).
2. Every changed line in a component .cs file is an icon-token swap inside an `Icon` getter
   (`<holder>.OLD` -> `<holder>.SAM_GH_*`); generated Resources.Designer.cs / holder blocks are excluded.
   The only other allowed change is the repository SPDX policy header (2 lines + blank) added at the top of a file.
3. The set of ComponentGuid values, names, nicknames, categories and exposures is unchanged vs base.
"""
import json
import pathlib
import hashlib
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))

VENDORED = {  # file -> SHA-256 (LF line endings) of SAM/design/grasshopper-icons/tools/<source> at SAM#166 head cf4d924a
    "icons.py": "5ca1bfee3c0f19e55babff06120eee62c4a7468b141b608e8c386deaa998f0e7",
    "render.py": "8a87f66c45e0bb3e04cc613f2b0f28a8cb8df951a7daad95bdec8e3851060a3f",
    "sam_classify.py": "317064816c964e2268e9406aee9ca5f7d82c2bdb34da9c3225a344ca2464b864",  # SAM classify.py
}
TOKEN = re.compile(r"\b(?:\w+\.)+\w+")  # any dotted chain, e.g. Properties.Resources.SAM_Small3
SPDX_HEADER = ("// SPDX-License-Identifier: LGPL-3.0-or-later",
               "// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors")
KEEP_RE = re.compile(r'ComponentGuid|base\(\s*"|GH_Exposure|Category|NickName|new Guid\(|new\("')


def git(*args):
    return subprocess.run(["git", "-C", REPO, *args], check=True, capture_output=True, text=True, encoding="utf-8").stdout


def main(base):
    base = git("merge-base", base, "HEAD").strip()
    ok = True
    for f, h in VENDORED.items():
        got = hashlib.sha256(open(os.path.join(HERE, f), "rb").read().replace(b"\r\n", b"\n")).hexdigest()  # autocrlf-safe
        print(f"vendored {f}: {'OK' if got == h else 'MODIFIED'}")
        ok &= got == h
    files = [f for f in git("diff", "--name-only", base, "--", "*.cs").split("\n") if f and not f.startswith("design/")]
    ui_manifest = pathlib.Path(REPO) / "design/ui-icons/manifest.json"
    ui_rows = json.loads(ui_manifest.read_text()) if ui_manifest.exists() else []
    ui_sources = {r.get("source") for r in ui_rows if r.get("source")}
    swaps, bad, headers = 0, [], 0
    for f in files:
        if f.endswith("Resources.Designer.cs") or f.startswith("design/"):
            continue
        if f in ui_sources:
            import difflib
            from collections import Counter
            old = git("show", base + ":" + f).lstrip("\ufeff")
            now = open(os.path.join(REPO, f), encoding="utf-8-sig").read()
            strip_header = lambda s: s.removeprefix("\n".join(SPDX_HEADER) + "\n\n")
            allowed = Counter(("Resources." + r["old_resource"], "Resources." + r["resource"]) for r in ui_rows if r.get("source") == f)
            a, b = strip_header(old).splitlines(), strip_header(now).splitlines()
            for op, i, j, k, l in difflib.SequenceMatcher(a=a, b=b).get_opcodes():
                if op == "equal": continue
                if j-i != l-k:
                    bad.append((f, "UI line additions/removals")); continue
                for x, y in zip(a[i:j], b[k:l]):
                    if x == y: continue
                    before = re.findall(r"Resources\.\w+", x)
                    after = re.findall(r"Resources\.\w+", y)
                    pair = (before[0], after[0]) if len(before)==len(after)==1 else None
                    if pair and allowed[pair] and x.replace(pair[0], "ICON") == y.replace(pair[1], "ICON"):
                        allowed[pair] -= 1; swaps += 1
                    else: bad.append((f, "UI change exceeds manifest", x, y))
            continue
        import difflib
        old = git("show", base + ":" + f).lstrip("\ufeff")
        now = open(os.path.join(REPO, f), encoding="utf-8-sig").read()
        header = "\n".join(SPDX_HEADER) + "\n\n"
        if now.startswith(header) and not old.startswith(header): headers += 1
        a, b = old.removeprefix(header).splitlines(), now.removeprefix(header).splitlines()
        getters = [m.end() for m in re.finditer(r"Bitmap\s+Icon\b", now)]
        def in_icon_getter(line):
            i = now.find(line.strip())
            return i >= 0 and any(0 <= i-g <= 300 for g in getters)
        for op, i, j, k, l in difflib.SequenceMatcher(a=a, b=b).get_opcodes():
            if op == "equal": continue
            if not "".join(a[i:j]).strip() and not "".join(b[k:l]).strip(): continue
            if j-i != l-k:
                bad.append((f, "added/removed nonblank lines")); continue
            for x, y in zip(a[i:j], b[k:l]):
                if x == y: continue
                if (not re.search(r"Bitmap\s+Icon\b", x) and not in_icon_getter(y)) or TOKEN.sub("X", x) != TOKEN.sub("X", y) or "SAM_GH_" not in y or KEEP_RE.search(x):
                    bad.append((f, x.strip(), y.strip()))
                else: swaps += 1
    print(f"component .cs files changed: {len(files)}; icon-token swaps: {swaps}; SPDX headers added: {headers}; "
          f"non-icon changes: {len(bad)}")
    for b in bad[:20]:
        print("  NON-ICON CHANGE:", b)
    ok &= not bad

    def guids(rev):
        out = set()
        listing = git("ls-tree", "-r", "--name-only", rev) if rev else git("ls-files")
        for f in listing.split("\n"):
            if f.endswith(".cs") and not f.startswith("design/"):
                try:
                    t = git("show", f"{rev}:{f}") if rev else open(os.path.join(REPO, f), encoding="utf-8-sig").read()
                except (subprocess.CalledProcessError, FileNotFoundError):
                    continue
                out |= {(f, m.group(0)) for m in re.finditer(r'ComponentGuid[^"\n]*"[0-9a-fA-F-]+"', t)}
        return out
    g0, g1 = guids(base), guids(None)
    print(f"ComponentGuid declarations: base {len(g0)}, now {len(g1)} -> {'UNCHANGED' if g0 == g1 else 'CHANGED'}")
    ok &= g0 == g1
    print("RESULT:", "OK" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "origin/sow/2026-Q3"))
