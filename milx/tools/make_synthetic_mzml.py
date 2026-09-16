#!/usr/bin/env python3
"""
Synthetic LC-MS/MS (DDA) dataset generator for MIL-X / MS-DIAL testing.

Creates:
  <out>/<name>.mzML         indexed mzML 1.1.0, centroid, MS1 + MS2 (DDA, top-N)
  <out>/library.msp         reference MS/MS library matching the simulated compounds
  <out>/method_lcms_dda.txt MS-DIAL console method file (LC-MS, positive, metabolomics)

The generator writes real binary data arrays (base64, optionally zlib), a valid
<indexList> with byte offsets and a SHA-1 fileChecksum, so the files exercise the
same code paths as msconvert output.

Usage:
  python3 make_synthetic_mzml.py --out /path/to/dir [--samples 3] [--no-zlib] [--seed 7]
"""
import argparse
import base64
import hashlib
import math
import os
import random
import struct
import zlib

import numpy as np

PROTON = 1.00727646688
NA_ADDUCT = 22.989218   # [M+Na]+ - [M+H]+ = 21.98194

# name, neutral monoisotopic mass, formula, RT (min), base intensity, fragments (mz, rel int)
COMPOUNDS = [
    ("Caffeine",        194.080376, "C8H10N4O2",  1.20, 8.0e5, [(138.0662, 100), (110.0713, 35), (123.0427, 20), (83.0604, 10)]),
    ("Tryptophan",      204.089878, "C11H12N2O2", 1.55, 6.0e5, [(188.0706, 100), (146.0600, 60), (118.0651, 45), (159.0917, 15)]),
    ("Phenylalanine",   165.078979, "C9H11NO2",   1.35, 5.0e5, [(120.0808, 100), (103.0542, 30), (149.0597, 10)]),
    ("Adenosine",       267.096754, "C10H13N5O4", 1.80, 4.5e5, [(136.0618, 100), (119.0352, 8)]),
    ("Riboflavin",      376.138286, "C17H20N4O6", 2.40, 3.0e5, [(243.0877, 100), (359.1117, 25), (172.0869, 15)]),
    ("Kynurenine",      208.084792, "C10H12N2O3", 1.65, 3.5e5, [(192.0655, 100), (146.0600, 70), (94.0651, 40)]),
    ("Hippuric acid",   179.058243, "C9H9NO3",    2.10, 4.0e5, [(105.0335, 100), (77.0386, 30), (162.0550, 20)]),
    ("Nicotinamide",    122.048013, "C6H6N2O",    0.95, 7.0e5, [(80.0495, 100), (106.0651, 20), (96.0444, 10)]),
    ("Cortisol",        362.209324, "C21H30O5",   3.20, 2.5e5, [(121.0648, 100), (327.1955, 60), (309.1849, 45), (267.1743, 30)]),
    ("Testosterone",    288.208930, "C19H28O2",   3.60, 2.0e5, [(97.0648, 100), (109.0648, 80), (253.1951, 20)]),
    ("Acetylcarnitine", 203.115758, "C9H17NO4",   1.05, 9.0e5, [(85.0284, 100), (145.0495, 30), (60.0808, 25)]),
    ("Palmitoylcarnitine", 399.334859, "C23H45NO4", 4.30, 1.5e5, [(85.0284, 100), (341.3050, 30), (239.2369, 20)]),
]

# carbon count is used for the isotope pattern (M+1 ~ 1.1% per carbon)
def carbon_count(formula):
    import re
    m = re.match(r"C(\d*)", formula)
    if not m:
        return 0
    return int(m.group(1)) if m.group(1) else 1


def gaussian(t, mu, sigma):
    return math.exp(-0.5 * ((t - mu) / sigma) ** 2)


def encode_array(values, dtype, use_zlib):
    arr = np.asarray(values, dtype=dtype)
    raw = arr.tobytes()  # little endian on all supported platforms
    if use_zlib:
        raw = zlib.compress(raw)
    return base64.b64encode(raw).decode("ascii")


def binary_array(values, is_mz, use_zlib):
    if is_mz:
        dtype = "<f8"
        precision_cv = '<cvParam cvRef="MS" accession="MS:1000523" name="64-bit float" value=""/>'
        kind_cv = '<cvParam cvRef="MS" accession="MS:1000514" name="m/z array" value="" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>'
    else:
        dtype = "<f4"
        precision_cv = '<cvParam cvRef="MS" accession="MS:1000521" name="32-bit float" value=""/>'
        kind_cv = '<cvParam cvRef="MS" accession="MS:1000515" name="intensity array" value="" unitCvRef="MS" unitAccession="MS:1000131" unitName="number of detector counts"/>'
    comp_cv = ('<cvParam cvRef="MS" accession="MS:1000574" name="zlib compression" value=""/>' if use_zlib
               else '<cvParam cvRef="MS" accession="MS:1000576" name="no compression" value=""/>')
    b64 = encode_array(values, dtype, use_zlib)
    return (f'<binaryDataArray encodedLength="{len(b64)}">'
            f'{precision_cv}{comp_cv}{kind_cv}'
            f'<binary>{b64}</binary></binaryDataArray>')


def spectrum_xml(index, scan_no, rt_sec, ms_level, mzs, ints, precursor=None, use_zlib=True):
    mzs = np.asarray(mzs, dtype=float)
    ints = np.asarray(ints, dtype=float)
    order = np.argsort(mzs)
    mzs, ints = mzs[order], ints[order]
    n = len(mzs)
    tic = float(ints.sum()) if n else 0.0
    if n:
        bp = int(np.argmax(ints))
        bp_mz, bp_int = float(mzs[bp]), float(ints[bp])
        lo, hi = float(mzs.min()), float(mzs.max())
    else:
        bp_mz = bp_int = lo = hi = 0.0
    level_cv = ('<cvParam cvRef="MS" accession="MS:1000579" name="MS1 spectrum" value=""/>' if ms_level == 1
                else '<cvParam cvRef="MS" accession="MS:1000580" name="MSn spectrum" value=""/>')
    xml = [f'<spectrum index="{index}" id="controllerType=0 controllerNumber=1 scan={scan_no}" defaultArrayLength="{n}">',
           level_cv,
           f'<cvParam cvRef="MS" accession="MS:1000511" name="ms level" value="{ms_level}"/>',
           '<cvParam cvRef="MS" accession="MS:1000130" name="positive scan" value=""/>',
           '<cvParam cvRef="MS" accession="MS:1000127" name="centroid spectrum" value=""/>',
           f'<cvParam cvRef="MS" accession="MS:1000504" name="base peak m/z" value="{bp_mz:.6f}" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>',
           f'<cvParam cvRef="MS" accession="MS:1000505" name="base peak intensity" value="{bp_int:.2f}" unitCvRef="MS" unitAccession="MS:1000131" unitName="number of detector counts"/>',
           f'<cvParam cvRef="MS" accession="MS:1000285" name="total ion current" value="{tic:.2f}"/>',
           f'<cvParam cvRef="MS" accession="MS:1000528" name="lowest observed m/z" value="{lo:.6f}" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>',
           f'<cvParam cvRef="MS" accession="MS:1000527" name="highest observed m/z" value="{hi:.6f}" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>',
           '<scanList count="1"><cvParam cvRef="MS" accession="MS:1000795" name="no combination" value=""/>',
           '<scan>',
           f'<cvParam cvRef="MS" accession="MS:1000016" name="scan start time" value="{rt_sec:.4f}" unitCvRef="UO" unitAccession="UO:0000010" unitName="second"/>',
           '<scanWindowList count="1"><scanWindow>',
           '<cvParam cvRef="MS" accession="MS:1000501" name="scan window lower limit" value="50.0" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>',
           '<cvParam cvRef="MS" accession="MS:1000500" name="scan window upper limit" value="1000.0" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>',
           '</scanWindow></scanWindowList></scan></scanList>']
    if precursor is not None:
        pmz, ce = precursor
        xml.append('<precursorList count="1"><precursor>'
                   '<isolationWindow>'
                   f'<cvParam cvRef="MS" accession="MS:1000827" name="isolation window target m/z" value="{pmz:.4f}" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>'
                   '<cvParam cvRef="MS" accession="MS:1000828" name="isolation window lower offset" value="0.7" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>'
                   '<cvParam cvRef="MS" accession="MS:1000829" name="isolation window upper offset" value="0.7" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>'
                   '</isolationWindow>'
                   '<selectedIonList count="1"><selectedIon>'
                   f'<cvParam cvRef="MS" accession="MS:1000744" name="selected ion m/z" value="{pmz:.4f}" unitCvRef="MS" unitAccession="MS:1000040" unitName="m/z"/>'
                   '<cvParam cvRef="MS" accession="MS:1000041" name="charge state" value="1"/>'
                   '</selectedIon></selectedIonList>'
                   '<activation>'
                   '<cvParam cvRef="MS" accession="MS:1000422" name="beam-type collision-induced dissociation" value=""/>'
                   f'<cvParam cvRef="MS" accession="MS:1000045" name="collision energy" value="{ce:.1f}" unitCvRef="UO" unitAccession="UO:0000266" unitName="electronvolt"/>'
                   '</activation></precursor></precursorList>')
    xml.append('<binaryDataArrayList count="2">')
    xml.append(binary_array(mzs, True, use_zlib))
    xml.append(binary_array(ints, False, use_zlib))
    xml.append('</binaryDataArrayList></spectrum>')
    return "".join(xml)


def simulate_sample(name, rng, sample_scale, rt_shift, use_zlib, run_minutes=5.0, cycle_sec=0.6, top_n=2):
    """Return list of (rt_sec, ms_level, mzs, ints, precursor) for one sample."""
    spectra = []
    t = 0.0
    last_ms2_time = {}  # compound index -> last time an MS2 was triggered (dynamic exclusion)
    while t < run_minutes * 60.0:
        rt_min = t / 60.0
        # ---- MS1 ----
        mzs, ints = [], []
        # background noise
        n_noise = int(rng.integers(120, 200))
        mzs.extend(rng.uniform(60.0, 950.0, n_noise))
        ints.extend(rng.uniform(40.0, 400.0, n_noise))
        candidates = []
        for ci, (cname, mass, formula, rt, base, frags) in enumerate(COMPOUNDS):
            sigma = 0.045 + 0.01 * (ci % 3)
            g = gaussian(rt_min, rt + rt_shift, sigma)
            if g < 1e-3:
                continue
            inten = base * sample_scale * g * (1.0 + rng.normal(0, 0.03))
            mh = mass + PROTON
            ncarbon = carbon_count(formula)
            iso1 = inten * 0.0107 * ncarbon
            iso2 = inten * ((0.0107 * ncarbon) ** 2 / 2.0 + 0.002 * (formula.count("O") + 1))
            jitter = lambda: rng.normal(0, 2e-6) * mh  # ~2 ppm
            mzs.extend([mh + jitter(), mh + 1.003355 + jitter(), mh + 2.00671 + jitter()])
            ints.extend([inten, iso1, iso2])
            # sodium adduct for every second compound
            if ci % 2 == 0:
                mzs.append(mh + (NA_ADDUCT - PROTON) + jitter())
                ints.append(inten * 0.12)
            if inten > 2.0e4:
                candidates.append((inten, ci, mh))
        spectra.append((t, 1, np.array(mzs), np.array(ints), None))
        # ---- MS2 (DDA top-N with 3 s dynamic exclusion) ----
        candidates.sort(reverse=True)
        n_sel = 0
        for inten, ci, mh in candidates:
            if n_sel >= top_n:
                break
            if t - last_ms2_time.get(ci, -1e9) < 3.0:
                continue
            last_ms2_time[ci] = t
            n_sel += 1
            frags = COMPOUNDS[ci][5]
            fm, fi = [], []
            scale = inten * 0.15
            for fmz, rel in frags:
                fm.append(fmz + rng.normal(0, 2e-6) * fmz)
                fi.append(scale * rel / 100.0 * (1.0 + rng.normal(0, 0.05)))
            # residual precursor
            fm.append(mh)
            fi.append(scale * 0.2)
            # MS2 noise
            n_noise2 = int(rng.integers(10, 25))
            fm.extend(rng.uniform(50.0, mh - 1.0, n_noise2))
            fi.extend(rng.uniform(5.0, 60.0, n_noise2))
            spectra.append((t + 0.2 * n_sel, 2, np.array(fm), np.array(fi), (mh, 30.0)))
        t += cycle_sec
    return spectra


def write_mzml(path, name, spectra, use_zlib):
    head = ('<?xml version="1.0" encoding="utf-8"?>\n'
            '<indexedmzML xmlns="http://psi.hupo.org/ms/mzml" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" '
            'xsi:schemaLocation="http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.2_idx.xsd">\n'
            f'<mzML xmlns="http://psi.hupo.org/ms/mzml" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" '
            'xsi:schemaLocation="http://psi.hupo.org/ms/mzml http://psidev.info/files/ms/mzML/xsd/mzML1.1.0.xsd" '
            f'id="{name}" version="1.1.0">\n'
            '<cvList count="2">'
            '<cv id="MS" fullName="Proteomics Standards Initiative Mass Spectrometry Ontology" version="4.1.0" URI="https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo"/>'
            '<cv id="UO" fullName="Unit Ontology" version="09:04:2014" URI="https://raw.githubusercontent.com/bio-ontology-research-group/unit-ontology/master/unit.obo"/>'
            '</cvList>\n'
            '<fileDescription><fileContent>'
            '<cvParam cvRef="MS" accession="MS:1000579" name="MS1 spectrum" value=""/>'
            '<cvParam cvRef="MS" accession="MS:1000580" name="MSn spectrum" value=""/>'
            '<cvParam cvRef="MS" accession="MS:1000127" name="centroid spectrum" value=""/>'
            '</fileContent>'
            '<sourceFileList count="1">'
            f'<sourceFile id="RAW1" name="{name}.synthetic" location="file:///synthetic">'
            '<cvParam cvRef="MS" accession="MS:1000768" name="Thermo nativeID format" value=""/>'
            '</sourceFile></sourceFileList></fileDescription>\n'
            '<sampleList count="1"><sample id="sample1" name="' + name + '"/></sampleList>\n'
            '<softwareList count="1"><software id="opendial_synth" version="1.0">'
            '<cvParam cvRef="MS" accession="MS:1000799" name="custom unreleased software tool" value="MIL-X synthetic generator"/>'
            '</software></softwareList>\n'
            '<instrumentConfigurationList count="1"><instrumentConfiguration id="IC1">'
            '<cvParam cvRef="MS" accession="MS:1000031" name="instrument model" value=""/>'
            '</instrumentConfiguration></instrumentConfigurationList>\n'
            '<dataProcessingList count="1"><dataProcessing id="dp1"><processingMethod order="0" softwareRef="opendial_synth">'
            '<cvParam cvRef="MS" accession="MS:1000544" name="Conversion to mzML" value=""/>'
            '</processingMethod></dataProcessing></dataProcessingList>\n'
            f'<run id="{name}" defaultInstrumentConfigurationRef="IC1" startTimeStamp="2026-01-01T00:00:00Z" defaultSourceFileRef="RAW1">\n'
            f'<spectrumList count="{len(spectra)}" defaultDataProcessingRef="dp1">\n')
    buf = bytearray(head.encode("utf-8"))
    offsets = []
    for i, (rt_sec, level, mzs, ints, prec) in enumerate(spectra):
        scan_no = i + 1
        sx = spectrum_xml(i, scan_no, rt_sec, level, mzs, ints, prec, use_zlib)
        offsets.append((f"controllerType=0 controllerNumber=1 scan={scan_no}", len(buf)))
        buf.extend(sx.encode("utf-8"))
        buf.extend(b"\n")
    buf.extend(b"</spectrumList>\n</run>\n</mzML>\n")
    index_offset = len(buf)
    idx = ['<indexList count="1">', '<index name="spectrum">']
    for sid, off in offsets:
        idx.append(f'<offset idRef="{sid}">{off}</offset>')
    idx.append('</index></indexList>')
    buf.extend("\n".join(idx).encode("utf-8"))
    buf.extend(f"\n<indexListOffset>{index_offset}</indexListOffset>\n<fileChecksum>".encode("utf-8"))
    sha1 = hashlib.sha1(bytes(buf)).hexdigest()
    buf.extend(f"{sha1}</fileChecksum>\n</indexedmzML>\n".encode("utf-8"))
    with open(path, "wb") as f:
        f.write(buf)


def fake_inchikey(name):
    h = hashlib.sha1(name.encode()).hexdigest().upper()
    letters = "".join(chr(ord('A') + int(c, 16) % 26) for c in h)
    return f"{letters[:14]}-{letters[14:24]}-N"


# ---------------------------------------------------------------------------
# GC-MS (EI) mode: MS1-only, fragment-rich spectra, no precursor/MS2
# name, RT (min), base intensity, EI fragments (nominal m/z, rel int)
GC_COMPOUNDS = [
    ("Alanine 2TMS",      6.10, 6.0e5, [(116, 100), (73, 60), (147, 25), (190, 8), (218, 3)]),
    ("Valine 2TMS",       7.40, 5.0e5, [(144, 100), (73, 70), (218, 20), (246, 5)]),
    ("Leucine 2TMS",      8.05, 4.5e5, [(158, 100), (73, 65), (232, 15), (260, 4)]),
    ("Glycine 3TMS",      8.60, 7.0e5, [(174, 100), (73, 80), (147, 30), (248, 10), (276, 3)]),
    ("Serine 3TMS",       9.20, 4.0e5, [(204, 100), (73, 75), (218, 60), (306, 5)]),
    ("Succinic acid 2TMS", 9.75, 3.5e5, [(147, 100), (73, 70), (247, 40), (262, 5)]),
    ("Malic acid 3TMS",  11.30, 3.0e5, [(73, 100), (147, 80), (233, 60), (335, 15)]),
    ("Glucose 5TMS",     14.20, 8.0e5, [(204, 100), (73, 60), (191, 45), (217, 20), (319, 10), (435, 2)]),
    ("Citric acid 4TMS", 15.05, 2.5e5, [(273, 100), (73, 90), (147, 50), (347, 30), (375, 10)]),
    ("Palmitic acid TMS", 17.40, 3.0e5, [(117, 100), (73, 70), (313, 45), (328, 20), (132, 15)]),
    ("Myo-inositol 6TMS", 17.90, 2.0e5, [(305, 100), (73, 60), (217, 40), (318, 30), (432, 5)]),
    ("Stearic acid TMS", 19.10, 2.8e5, [(117, 100), (73, 65), (341, 40), (356, 15)]),
]


def simulate_gcms_sample(rng, sample_scale, rt_shift, run_minutes=21.0, scan_sec=0.25):
    """EI GC-MS: one MS1 scan every 0.25 s, nominal-mass fragment spectra."""
    spectra = []
    t = 0.0
    while t < run_minutes * 60.0:
        rt_min = t / 60.0
        mzs, ints = [], []
        n_noise = int(rng.integers(30, 60))
        mzs.extend(np.round(rng.uniform(50.0, 500.0, n_noise)) + rng.normal(0, 0.003, n_noise))
        ints.extend(rng.uniform(20.0, 200.0, n_noise))
        # column bleed
        for bleed in (207.0, 281.0, 355.0):
            mzs.append(bleed + rng.normal(0, 0.003)); ints.append(400.0 + 100.0 * rt_min / run_minutes)
        for cname, rt, base, frags in GC_COMPOUNDS:
            g = gaussian(rt_min, rt + rt_shift, 0.035)
            if g < 1e-3:
                continue
            inten = base * sample_scale * g * (1.0 + rng.normal(0, 0.03))
            for fmz, rel in frags:
                mzs.append(fmz + rng.normal(0, 0.003))
                ints.append(inten * rel / 100.0)
                # M+1 isotope of the fragment (~10 %)
                mzs.append(fmz + 1.0034 + rng.normal(0, 0.003))
                ints.append(inten * rel / 100.0 * 0.1)
        spectra.append((t, 1, np.array(mzs), np.array(ints), None))
        t += scan_sec
    return spectra


def write_gc_msp(path):
    with open(path, "w") as f:
        for cname, rt, base, frags in GC_COMPOUNDS:
            f.write(f"NAME: {cname}\n")
            f.write(f"RETENTIONTIME: {rt:.2f}\n")
            f.write("IONMODE: Positive\n")
            f.write("INSTRUMENTTYPE: GC-EI-Q\n")
            f.write("COMMENT: MIL-X synthetic EI reference\n")
            f.write(f"Num Peaks: {len(frags)}\n")
            for fmz, rel in frags:
                f.write(f"{fmz}\t{rel}\n")
            f.write("\n")


def write_gc_method(path, msp_path):
    lines = [
        "#Data type",
        "MS1 data type: Centroid",
        "Ion mode: Positive",
        "Target omics: Metabolomics",
        "Machine category: GCMS",
        "Accuracy type: IsAccurate",
        "",
        "#Data collection parameters",
        "Retention time begin: 0",
        "Retention time end: 100",
        "MS1 mass range begin: 50",
        "MS1 mass range end: 600",
        "",
        "#Peak detection parameters",
        "Smoothing method: LinearWeightedMovingAverage",
        "Smoothing level: 3",
        "Average peak width: 20",
        "Minimum peak height: 5000",
        "Mass slice width: 0.5",
        "Mass accuracy: 0.05",
        "",
        "#Deconvolution parameters",
        "Sigma window value: 0.5",
        "Amplitude cut off: 10",
        "",
        "#Identification",
        f"MSP file path: {msp_path}",
        "Retention type: RT",
        "RI compound type: Alkanes",
        "RT tolerance for identification: 0.5",
        "RI tolerance for identification: 20",
        "EI similarity library tolerance: 70",
        "Identification score cut off: 70",
        "Use retention information for scoring: True",
        "Use retention information for filtering: False",
        "Only report top hit for MSP-based annotation: True",
        "",
        "#Alignment",
        "Alignment index type: RT",
        "Retention time tolerance for alignment: 0.1",
        "Retention index tolerance for alignment: 20",
        "EI similarity tolerance for alignment: 70",
        "Retention time factor for alignment: 0.5",
        "EI similarity factor for alignment: 0.5",
        "Peak count filter: 0",
        "Together with alignment: True",
        "Is height matrix export: True",
    ]
    with open(path, "w") as f:
        f.write("\n".join(lines) + "\n")


def write_msp(path):
    with open(path, "w") as f:
        for cname, mass, formula, rt, base, frags in COMPOUNDS:
            mh = mass + PROTON
            f.write(f"NAME: {cname}\n")
            f.write(f"PRECURSORMZ: {mh:.5f}\n")
            f.write("PRECURSORTYPE: [M+H]+\n")
            f.write(f"FORMULA: {formula}\n")
            f.write("ONTOLOGY: Synthetic\n")
            f.write(f"INCHIKEY: {fake_inchikey(cname)}\n")
            f.write("SMILES: C\n")
            f.write(f"RETENTIONTIME: {rt:.2f}\n")
            f.write("IONMODE: Positive\n")
            f.write("COLLISIONENERGY: 30\n")
            f.write("INSTRUMENTTYPE: Q-TOF\n")
            f.write("COMMENT: MIL-X synthetic reference\n")
            f.write(f"Num Peaks: {len(frags)}\n")
            for fmz, rel in frags:
                f.write(f"{fmz:.4f}\t{rel}\n")
            f.write("\n")


def write_method(path, msp_path):
    lines = [
        "#Data type",
        "MS1 data type: Centroid",
        "MS2 data type: Centroid",
        "Ion mode: Positive",
        "Target omics: Metabolomics",
        "Acquisition type: DDA",
        "Machine category: LCMS",
        "",
        "#Data collection parameters",
        "Retention time begin: 0",
        "Retention time end: 100",
        "MS1 mass range begin: 50",
        "MS1 mass range end: 1000",
        "MS2 mass range begin: 50",
        "MS2 mass range end: 1000",
        "",
        "#Centroid parameters",
        "MS1 tolerance for centroid: 0.01",
        "MS2 tolerance for centroid: 0.025",
        "",
        "#Peak detection parameters",
        "Smoothing method: LinearWeightedMovingAverage",
        "Smoothing level: 3",
        "Minimum peak width: 5",
        "Minimum peak height: 5000",
        "Mass slice width: 0.05",
        "Mass accuracy: 0.01",
        "Max charge number: 2",
        "",
        "#Deconvolution parameters",
        "Sigma window value: 0.5",
        "Amplitude cut off: 0",
        "Keep isotope range: 0.5",
        "Exclude after precursor: True",
        "",
        "#Adduct list",
        "Searched adduct ions: [M+H]+,[M+Na]+,[M+NH4]+",
        "",
        "#MSP file and MS/MS identification setting",
        f"MSP file path: {msp_path}",
        "RT tolerance for MSP-based annotation: 0.5",
        "Mass range begin for MSP-based annotation: 0",
        "Mass range end for MSP-based annotation: 2000",
        "Relative amplitude cutoff for MSP-based annotation: 0",
        "Absolute amplitude cutoff for MSP-based annotation: 0",
        "Weighted dot product cutoff for MSP-based annotation: 0.4",
        "Simple dot product cutoff for MSP-based annotation: 0.4",
        "Reverse dot product cutoff for MSP-based annotation: 0.4",
        "Matched peaks percentage cutoff for MSP-based annotation: 0.2",
        "Minimum spectrum match for MSP-based annotation: 1",
        "Total score cutoff for MSP-based annotation: 60",
        "MS1 tolerance for MSP-based annotation: 0.01",
        "MS2 tolerance for MSP-based annotation: 0.05",
        "Use retention information for MSP-based annotation scoring: True",
        "Use retention information for MSP-based annotation filtering: False",
        "Only report top hit for MSP-based annotation: True",
        "",
        "#Alignment parameters setting",
        "Alignment reference file ID: 0",
        "Retention time tolerance for alignment: 0.1",
        "MS1 tolerance for alignment: 0.015",
        "Retention time factor for alignment: 0.5",
        "MS1 factor for alignment: 0.5",
        "Peak count filter: 0",
        "N percent detected in one group: 0",
        "Gap filling by compulsion: True",
        "Together with alignment: True",
        "",
        "#Export",
        "Is height matrix export: True",
    ]
    with open(path, "w") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--samples", type=int, default=3)
    ap.add_argument("--no-zlib", action="store_true")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--minutes", type=float, default=5.0)
    ap.add_argument("--mode", choices=["lcms", "gcms"], default="lcms")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    rng = np.random.default_rng(args.seed)
    use_zlib = not args.no_zlib
    for s in range(args.samples):
        group = "A" if s % 2 == 0 else "B"
        name = f"Sample{group}{s + 1:02d}"
        scale = 1.0 + 0.15 * s if group == "A" else 0.7 + 0.1 * s
        rt_shift = 0.01 * s   # ~0.6 s drift per sample
        if args.mode == "gcms":
            spectra = simulate_gcms_sample(rng, scale, rt_shift, run_minutes=max(args.minutes, 21.0) if args.minutes == 5.0 else args.minutes)
        else:
            spectra = simulate_sample(name, rng, scale, rt_shift, use_zlib, run_minutes=args.minutes)
        path = os.path.join(args.out, name + ".mzML")
        write_mzml(path, name, spectra, use_zlib)
        n_ms1 = sum(1 for sp in spectra if sp[1] == 1)
        print(f"wrote {path}: {len(spectra)} spectra ({n_ms1} MS1, {len(spectra) - n_ms1} MS2), {os.path.getsize(path) / 1e6:.1f} MB")
    msp = os.path.join(args.out, "library.msp")
    if args.mode == "gcms":
        write_gc_msp(msp)
        method = os.path.join(args.out, "method_gcms_ei.txt")
        write_gc_method(method, os.path.abspath(msp))
    else:
        write_msp(msp)
        method = os.path.join(args.out, "method_lcms_dda.txt")
        write_method(method, os.path.abspath(msp))
    print("wrote", msp)
    print("wrote", method)


if __name__ == "__main__":
    main()
