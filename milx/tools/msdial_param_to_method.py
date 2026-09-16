#!/usr/bin/env python3
"""Converts an MS-DIAL parameter export into an MIL-X method file.

MS-DIAL writes "<Project>_param_<stamp>.txt" next to a processed dataset. Most of its keys are
the ones MIL-X's method parser already understands, so reproducing a Windows MS-DIAL run on
macOS is a matter of carrying them across verbatim. Keys MS-DIAL does not export were left at
their defaults by MS-DIAL, so they are left out here too — writing them would silently change
the run.

    python3 tools/msdial_param_to_method.py <param.txt> --out method.txt [--msp <library.msp>]
"""
import argparse
import re

# Keys copied straight through, in the order MIL-X's method files use.
SECTIONS = [
    ("#Data type", [
        "MS1 data type", "MS2 data type", "Ion mode", "Target omics", "Machine category",
    ]),
    ("#Data collection parameters", [
        "Retention time begin", "Retention time end",
        "MS1 mass range begin", "MS1 mass range end",
        "MS2 mass range begin", "MS2 mass range end",
    ]),
    ("#Centroid parameters", [
        "MS1 tolerance for centroid", "MS2 tolerance for centroid",
    ]),
    ("#Peak detection parameters", [
        "Smoothing method", "Smoothing level", "Minimum peak width", "Minimum peak height",
        "Average peak width", "Mass slice width", "Accuracy type", "Max charge number",
    ]),
    ("#Deconvolution parameters", [
        "Sigma window value", "Amplitude cut off", "Relative amplitude cut off",
        "Keep isotope range", "Exclude after precursor", "Keep original precursor isotopes",
        "Target CE",
    ]),
    ("#Adduct list", [
        "Searched adduct ions",
    ]),
    ("#Annotation", [
        "Solvent type", "Searched lipid class",
    ]),
    ("#Alignment parameters setting", [
        "Alignment reference file ID",
        "Retention time tolerance for alignment", "Retention time factor for alignment",
        "Spectrum similarity tolerance for alignment", "Spectrum similarity factor for alignment",
        "MS1 tolerance for alignment", "MS1 factor for alignment",
        "Force insert peaks in gap filling",
    ]),
    ("#Filtering", [
        "Peak count filter", "N percent detected in one group",
        "Remove feature based on peak height fold-change", "Blank filtering",
        "Sample max / blank average", "Sample average / blank average",
        "Keep reference matched metabolites", "Keep suggested metabolites",
        "Keep removable features and assigned tag for checking",
        "Replace true zero values with 1/2 of minimum peak height over all samples",
    ]),
    ("#Retention time correction", [
        "Execute RT correction",
    ]),
    ("#Isotope tracking", [
        "Tracking isotope label",
    ]),
    ("#CorrDec", [
        "CorrDec execute", "CorrDec MS2 tolerance", "CorrDec minimum MS2 peak height",
        "CorrDec minimum number of detected samples", "CorrDec exclude highly correlated spots",
        "CorrDec minimum correlation coefficient (MS2)", "CorrDec margin 1 (target precursor)",
        "CorrDec margin 2 (coeluted precursor)", "CorrDec minimum detected rate",
        "CorrDec minimum MS2 relative intensity", "CorrDec remove peaks larger than precursor",
    ]),
    ("#Export", [
        "Height matrix export",
    ]),
    ("#Process", [
        "Number of threads",
    ]),
]


def read_params(path):
    values = {}
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line or line.startswith("#") or ":" not in line:
                continue
            key, _, value = line.partition(":")
            key, value = key.strip(), value.strip()
            # "File ID=0" style repeated keys are per-file settings, not method parameters
            if re.match(r"^(File ID|Class name)", key):
                continue
            values.setdefault(key, value)
    return values


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("param")
    ap.add_argument("--out", required=True)
    ap.add_argument("--msp", help="library to annotate with (MS-DIAL exports an empty Msp file path when it used a cached library)")
    ap.add_argument("--acquisition", default="DDA")
    a = ap.parse_args()

    values = read_params(a.param)
    out, missing = [], []
    for header, keys in SECTIONS:
        block = []
        for key in keys:
            if key in values and values[key] != "":
                block.append(f"{key}: {values[key]}")
            else:
                missing.append(key)
        if header == "#Data type":
            block.append(f"Acquisition type: {a.acquisition}")
        if header == "#Annotation" and a.msp:
            block.append(f"MSP file path: {a.msp}")
        if block:
            out.append(header)
            out.extend(block)
            out.append("")

    with open(a.out, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out).rstrip() + "\n")
    print(f"[method] {a.out}: {sum(1 for l in out if ':' in l)} parameters")
    if missing:
        print("[method] not present in the export (left at the MS-DIAL default): " + ", ".join(missing))


if __name__ == "__main__":
    main()
