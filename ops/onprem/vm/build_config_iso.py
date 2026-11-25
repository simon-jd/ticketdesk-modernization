#!/usr/bin/env python3
"""Build the unattend config ISO without root (pycdlib, ISO9660 + Joliet).

Usage: build_config_iso.py OUT.iso SRC_DIR
Every file under SRC_DIR is added with its relative path; Windows Setup reads the
Joliet names, so autounattend.xml must sit at SRC_DIR/autounattend.xml.
"""
import os
import re
import sys

import pycdlib


def iso9660_name(name: str, is_dir: bool) -> str:
    stem, dot, ext = name.upper().partition(".")
    clean = lambda s: re.sub(r"[^A-Z0-9_]", "_", s)
    if is_dir:
        return clean(name.upper())[:31]
    stem, ext = clean(stem)[:24], clean(ext)[:6]
    return f"{stem}.{ext};1" if ext else f"{stem}.;1"


def main() -> int:
    out, src = sys.argv[1], sys.argv[2]
    iso = pycdlib.PyCdlib()
    iso.new(interchange_level=3, joliet=3, vol_ident="TDUNATTEND")
    for root, dirs, files in os.walk(src):
        dirs.sort()
        rel = os.path.relpath(root, src)
        parts = [] if rel == "." else rel.split(os.sep)
        iso_dir = "/" + "/".join(iso9660_name(p, True) for p in parts) if parts else ""
        jol_dir = "/" + "/".join(parts) if parts else ""
        if parts:
            iso.add_directory(iso_dir, joliet_path=jol_dir)
        for f in sorted(files):
            iso.add_file(os.path.join(root, f),
                         iso_path=f"{iso_dir}/{iso9660_name(f, False)}",
                         joliet_path=f"{jol_dir}/{f}")
    iso.write(out)
    iso.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
