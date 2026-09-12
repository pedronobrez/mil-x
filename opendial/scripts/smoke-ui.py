#!/usr/bin/env python3
"""Drives the installed OpenDIAL and checks it actually works.

The headless tests drive view models and the visual ones compare rendered frames. Neither runs the
packaged application, so a defect that only appears there passes both: an asset that did not make it
into the bundle, a font that stopped resolving, a plugin that fails to load, a shortcut that never
fires. The last one is not hypothetical — every workspace shortcut had been dead since the day it
was written, and nothing in the suite noticed.

So this launches the real bundle through LaunchServices, the way a person does, and then asserts on
what the window says it is showing. It reads that from the probe file the application writes when
OPENDIAL_UI_PROBE names one, rather than guessing from pixels.

Two modes, which can be combined:

    opendial/scripts/smoke-ui.py --project ~/…/Project-2609071200.mdproject
        opens a processed project and checks the shell: title, ion table, shortcuts, a click.

    opendial/scripts/smoke-ui.py --process ~/…/raw-folder --library ~/…/library.msp
        writes a project from the raw files in the folder, presses Cmd+R and waits for the run,
        then does the work a reviewer does: filters the table by typing, re-integrates a peak by
        typing a window and clicking Apply, saves the review, and exports the reviewed table — the
        one step that goes through the system's save panel is handed over by the command channel.

Exits non-zero on the first failure and says what it expected. Needs a logged-in graphical session;
clicks and typing additionally need cliclick (brew install cliclick), and are skipped with a warning
if it is not there.
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

APP_PROCESS = "OpenDIAL"

# the digit keys, which is how a shortcut has to be sent: a character event never reaches one
KEY_CODES = {1: 18, 2: 19, 3: 20, 4: 21, 5: 23}
KEY_R = 15
KEY_A = 0

WORKSPACES = ["Explorer", "Analytics", "Method", "Samples", "Statistics"]

RAW_EXTENSIONS = (".wiff", ".mzml", ".raw", ".d", ".abf", ".ibf", ".cdf")


class Failed(Exception):
    pass


def say(message: str) -> None:
    print(message, flush=True)


def osascript(script: str, check: bool = False) -> str:
    result = subprocess.run(["osascript", "-e", script], capture_output=True, text=True)
    if check and result.returncode != 0:
        raise Failed(f"osascript failed: {result.stderr.strip()}")
    return result.stdout.strip()


def running() -> bool:
    return bool(subprocess.run(["pgrep", "-f", "OpenDIAL.app/Contents/MacOS"],
                               capture_output=True).stdout.strip())


def quit_running() -> None:
    """Makes sure nothing is left running.

    It has to be thorough: `open --env` cannot apply an environment to an instance that already
    exists, and fails with a bare "-600" instead of saying so. And a polite quit can be refused —
    the window asks before discarding an unsaved project — so this escalates.
    """
    if not running():
        return
    osascript(f'tell application "{APP_PROCESS}" to quit')
    for _ in range(10):
        if not running():
            return
        time.sleep(0.5)
    subprocess.run(["pkill", "-f", "OpenDIAL.app/Contents/MacOS"], capture_output=True)
    for _ in range(10):
        if not running():
            return
        time.sleep(0.5)
    subprocess.run(["pkill", "-9", "-f", "OpenDIAL.app/Contents/MacOS"], capture_output=True)
    time.sleep(1)


def frontmost_process() -> str:
    """The application the window server is sending keys to right now."""
    return osascript('tell application "System Events" to name of first process whose frontmost is true')


def front(patience: float = 6.0) -> None:
    """Brings the window forward, and makes sure it got there before anything is typed at it.

    A keystroke is not addressed to an application: System Events hands it to whatever is frontmost.
    Asking for the front and typing straight away is a race — the terminal the script runs in, a
    notification, or the application still coming up can hold it for a moment — and a key that
    lands in another application is gone, so no amount of waiting afterwards brings it back. This
    asks, checks, and asks again.
    """
    deadline = time.time() + patience
    while True:
        osascript(f'tell application "System Events" to set frontmost of process "{APP_PROCESS}" to true')
        time.sleep(0.4)
        if frontmost_process() == APP_PROCESS:
            return
        if time.time() >= deadline:
            say(f"       warning: {frontmost_process()!r} is holding the front, so a key may not arrive")
            return


def press(key_code: int, modifier: str | None = "command") -> None:
    front()
    using = f" using {modifier} down" if modifier else ""
    osascript(f'tell application "System Events" to key code {key_code}{using}', check=True)


def press_with(key_code: int, modifiers: str) -> None:
    """A key with several modifiers, e.g. "command down, shift down"."""
    front()
    osascript(f'tell application "System Events" to key code {key_code} using {{{modifiers}}}', check=True)
    # System Events can leave the modifiers logically down, and the next click then arrives as a
    # shift-click or a command-click that a button ignores; let them go explicitly
    if shutil.which("cliclick"):
        subprocess.run(["cliclick", "ku:cmd,shift,ctrl,alt"], capture_output=True)


def window_frame() -> tuple[float, float, float, float]:
    """The main window's frame in screen points: where it is, and how big including its title bar.

    Asked for by name rather than as "window 1". A tooltip is a window of its own, and the system
    lists it before the real one, so a pointer resting over a button with a tooltip used to turn
    every click that followed into a click at the tooltip's corner — hundreds of points away from
    what the script meant to press, and only when the pointer happened to be somewhere with a
    tooltip, which is what made it come and go.
    """
    return window_frame_named(APP_PROCESS)


def window_frame_named(title_prefix: str) -> tuple[float, float, float, float]:
    """The frame of the window whose title starts so; the main window is not always window 1."""
    where = (f'tell application "System Events" to tell process "{APP_PROCESS}" to '
             f'get {{position, size}} of (first window whose name starts with "{title_prefix}")')
    raw = osascript(where)
    try:
        x, y, w, h = [float(v) for v in raw.split(", ")]
    except ValueError as error:
        raise Failed(f"could not read the frame of the '{title_prefix}' window: {raw!r}") from error
    return x, y, w, h


def drag_hold(x: float, y: float, path: list[tuple[float, float]]) -> None:
    """Presses at a point and drags along a path, leaving the button down; drag_release ends it."""
    front()
    steps = ["w:150", f"dd:{x:.0f},{y:.0f}", "w:200"]
    for px, py in path:
        steps += [f"dm:{px:.0f},{py:.0f}", "w:120"]
    subprocess.run(["cliclick", *steps], check=True, capture_output=True)


def drag_release(x: float, y: float) -> None:
    subprocess.run(["cliclick", f"du:{x:.0f},{y:.0f}", "w:300"], check=True, capture_output=True)


def to_screen(state: dict, control: dict) -> tuple[float, float]:
    """Turns a place inside the window into a place on the screen.

    The application reports where its controls sit inside its own content, and refuses to guess at
    the rest; the frame and the height of the title bar belong to the window manager. Both are read
    here, and the difference between the frame and the content is the title bar.
    """
    frame_x, frame_y, frame_w, frame_h = window_frame()
    client = state.get("client") or {}
    client_w = float(client.get("width") or frame_w)
    client_h = float(client.get("height") or frame_h)
    title_bar = max(0.0, frame_h - client_h)
    border = max(0.0, (frame_w - client_w) / 2)
    return frame_x + border + float(control["x"]), frame_y + title_bar + float(control["y"])


def click(x: float, y: float) -> None:
    front()
    origin = subprocess.run(["cliclick", "p"], capture_output=True, text=True).stdout.strip()
    subprocess.run(["cliclick", "w:150", f"c:{x:.0f},{y:.0f}", "w:400"], check=True, capture_output=True)
    if origin:
        subprocess.run(["cliclick", f"m:{origin}"], capture_output=True)


def type_text(text: str) -> None:
    """Types into whatever has the focus, the way a keyboard would."""
    front()
    subprocess.run(["cliclick", "w:100", f"t:{text}", "w:200"], check=True, capture_output=True)


def click_control(state: dict, name: str, what: str) -> None:
    spot = state["controls"].get(name)
    check(spot is not None, f"the window reports where {what} is")
    front()   # a click on a window that is not frontmost only brings it forward
    x, y = to_screen(state, spot)
    say(f"       clicking {what} at {x:.0f}, {y:.0f} on screen")
    click(x, y)


def click_until(probe: str, name: str, what: str, changed, timeout: float) -> dict:
    """Clicks a control and waits for the window to answer; one more click when the first was swallowed.

    A page that has just been shown can take the first click as focus rather than as a press, so
    the click is repeated once, after a short wait, before the wait is called a failure.
    """
    state = read_probe(probe)
    click_control(state, name, what)
    try:
        return wait_for(probe, changed, f"{what} to answer", min(timeout, 10))
    except Failed:
        say(f"       {what} did not answer the first click; clicking again")
        state = read_probe(probe)
        click_control(state, name, what)
        return wait_for(probe, changed, f"{what} to answer", timeout)


def press_until(probe: str, key_code: int, changed, what: str, timeout: float,
                modifier: str | None = "command", modifiers: str | None = None,
                patience: float = 6.0) -> dict:
    """Presses a shortcut and waits for the window to answer; presses again when nothing happened.

    The one failure this rides over is a key that never arrived. A shortcut goes to whichever
    application is frontmost when it is sent, and the front can be taken in the fraction of a
    second between asking for it and the key going down; that key is then lost, and waiting the
    whole timeout out only turns a lost keystroke into a failed test. Pressing again recovers it.
    The wait between presses is longer than the window ever needs to answer one, so a press is not
    repeated on top of one that did land — which matters for a key that toggles.
    """
    deadline = time.time() + timeout
    attempt = 0
    while True:
        attempt += 1
        if modifiers:
            press_with(key_code, modifiers)
        else:
            press(key_code, modifier)
        try:
            return wait_for(probe, changed, what, max(1.0, min(patience, deadline - time.time())))
        except Failed:
            if time.time() >= deadline:
                raise Failed(f"nothing happened when waiting for {what}, after {attempt} press(es) over "
                             f"{timeout:.0f} s; the front is {frontmost_process()!r} and the window was "
                             f"reporting {json.dumps({k: v for k, v in read_probe(probe).items() if k != 'controls'}, indent=2)}")
            say(f"       {what}: nothing yet; pressing again")


def replace_text(state: dict, name: str, what: str, text: str) -> None:
    """Clicks a text box, selects what is in it, and types over it."""
    click_control(state, name, what)
    press(KEY_A)   # Cmd+A
    type_text(text)


def read_probe(path: str) -> dict:
    for _ in range(5):
        try:
            with open(path, encoding="utf-8") as handle:
                return json.load(handle)
        except (FileNotFoundError, json.JSONDecodeError):
            time.sleep(0.2)
    raise Failed(f"no readable probe at {path}")


def wait_for(path: str, predicate, what: str, timeout: float, report=None) -> dict:
    """Waits for the window to report something, and says what it was still saying if it never did."""
    deadline = time.time() + timeout
    last: dict = {}
    next_report = time.time() + 15
    while time.time() < deadline:
        try:
            last = read_probe(path)
        except Failed:
            time.sleep(0.5)
            continue
        if predicate(last):
            return last
        if report is not None and time.time() >= next_report:
            report(last)
            next_report = time.time() + 15
        time.sleep(0.5)
    raise Failed(f"timed out after {timeout:.0f} s waiting for {what}; the window was reporting "
                 f"{json.dumps({k: v for k, v in last.items() if k not in ('controls',)}, indent=2)}")


def send_command(probe: str, command: dict, timeout: float = 120) -> dict:
    """Hands one command to the application and waits for it to say it is done."""
    command = dict(command)
    command["id"] = f"{time.time():.3f}"
    with open(probe + ".commands.tmp", "w", encoding="utf-8") as handle:
        json.dump(command, handle)
    os.replace(probe + ".commands.tmp", probe + ".commands")
    state = wait_for(probe, lambda s: (s.get("lastCommand") or {}).get("id") == command["id"],
                     f"the {command['action']} command to complete", timeout)
    result = state["lastCommand"]
    if not result.get("ok"):
        raise Failed(f"{command['action']} failed: {result.get('message')}")
    say(f"       {command['action']}: {result.get('message')}")
    return state


def check(condition: bool, message: str) -> None:
    if not condition:
        raise Failed(message)
    say(f"  ok  {message}")


def classify(name: str) -> tuple[str, str]:
    """A guess at what an injection is from its name, good enough for a batch nobody labelled."""
    lower = name.lower()
    if "bk" in lower or "blank" in lower or "blk" in lower:
        return "Blank", "blank"
    if "qc" in lower or "eq" in lower or "pool" in lower:
        return "QC", "qc"
    return "Sample", "sample"


def write_project(folder: str, raw_folder: str, method: str | None, library: str | None, limit: int) -> tuple[str, int]:
    """Writes an .odproj the way the New project wizard would, from the raw files in a folder."""
    raws = sorted(p for p in glob.glob(os.path.join(raw_folder, "*")) if p.lower().endswith(RAW_EXTENSIONS)
                  and not p.lower().endswith(".wiff.scan"))
    # a .wiff2 beside a .wiff is the same acquisition in the newer container; the .wiff is the one read natively
    stems = {os.path.splitext(p)[0] for p in raws if p.lower().endswith(".wiff")}
    raws = [p for p in raws if not (p.lower().endswith(".wiff2") and os.path.splitext(p)[0] in stems)]
    if limit > 0:
        raws = raws[:limit]
    if not raws:
        raise Failed(f"no raw files in {raw_folder}")
    method_text = open(method, encoding="utf-8").read() if method else ""
    if library:
        lines = [l for l in method_text.splitlines() if not l.lower().startswith("msp file path")]
        lines.append(f"MSP file path: {os.path.abspath(library)}")
        method_text = "\n".join(lines) + "\n"
    samples = []
    for order, path in enumerate(raws, start=1):
        name = os.path.splitext(os.path.basename(path))[0]
        sample_type, cls = classify(name)
        samples.append({
            "Path": os.path.abspath(path), "Name": name, "Class": cls, "SampleType": sample_type,
            "Acquisition": "DDA", "AnalyticalOrder": order, "Batch": 1, "Dilution": 1,
            "Comment": "", "Included": True, "SampleIndex": 0,
        })
    project = {
        "Version": 1, "Name": "smoke-cycle", "Mode": "LCMS",
        "OutputFolder": os.path.join(folder, "results"), "MdprojectPath": None,
        "MethodText": method_text, "Samples": samples,
    }
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, "smoke-cycle.odproj")
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(project, handle, indent=2)
    return path, len(samples)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--bundle", default="/Applications/OpenDIAL.app",
                        help="the application to drive (default: the installed one)")
    parser.add_argument("--project", help="a processed project to open; checks the shell around it")
    parser.add_argument("--process", metavar="FOLDER",
                        help="a folder of raw files: writes a project, processes it, and reviews the result")
    parser.add_argument("--method", help="method file for --process (default: the application's LC-MS defaults)")
    parser.add_argument("--library", help="MSP library for --process")
    parser.add_argument("--limit", type=int, default=0, help="process only the first N raw files of the folder")
    parser.add_argument("--shots", help="directory to save screenshots into")
    parser.add_argument("--open-timeout", type=float, default=600,
                        help="seconds to allow for a project to load (default: 600)")
    parser.add_argument("--process-timeout", type=float, default=2400,
                        help="seconds to allow for the run (default: 2400)")
    parser.add_argument("--keep", action="store_true", help="leave the application running at the end")
    args = parser.parse_args()

    if sys.platform != "darwin":
        say("skipped: this drives a macOS application")
        return 0
    # `open -a` only takes an absolute path, and says "unable to find" for anything else
    args.bundle = os.path.abspath(args.bundle)
    if not os.path.isdir(args.bundle):
        say(f"FAIL: no application at {args.bundle}; build and install it first "
            f"(opendial/scripts/make-app-bundle.sh)")
        return 1
    # the application resolves its launch argument against its own working directory, which is "/"
    if args.project:
        args.project = os.path.abspath(args.project)
    if args.process:
        args.process = os.path.abspath(args.process)
    if args.project and not os.path.exists(args.project):
        say(f"FAIL: no project at {args.project}")
        return 1
    if args.process and not os.path.isdir(args.process):
        say(f"FAIL: no folder at {args.process}")
        return 1
    if not args.project and not args.process:
        say("note: neither --project nor --process given; only the empty window is checked")

    can_click = shutil.which("cliclick") is not None
    if not can_click:
        say("note: cliclick is not installed, so the click and typing checks are skipped (brew install cliclick)")

    work = tempfile.mkdtemp(prefix="opendial-smoke-")
    probe = os.path.join(work, "probe.json")
    out_log = os.path.join(work, "stdout.log")
    err_log = os.path.join(work, "stderr.log")
    settings = os.path.join(work, "settings")
    shots = args.shots or os.path.join(work, "shots")
    os.makedirs(shots, exist_ok=True)
    os.makedirs(settings, exist_ok=True)

    launch_path = args.project
    expected_samples = 0
    if args.process:
        launch_path, expected_samples = write_project(os.path.join(work, "cycle"), args.process, args.method, args.library, args.limit)
        say(f"wrote a project with {expected_samples} sample(s) at {launch_path}")

    say(f"driving {args.bundle}")
    say(f"  probe {probe}")
    quit_running()

    # the settings live in the work folder, so the run neither reads nor rewrites the person's own
    launch = ["open", "-a", args.bundle, "--env", f"OPENDIAL_UI_PROBE={probe}",
              "--env", f"OPENDIAL_SETTINGS_DIR={settings}",
              "--stdout", out_log, "--stderr", err_log]
    if launch_path:
        launch += ["--args", launch_path]
    # LaunchServices can still be holding the instance that was just quit, and answers a launch with
    # a bare -600 for a second or two after the process is gone; that is worth waiting out
    for attempt in range(10):
        result = subprocess.run(launch, capture_output=True, text=True)
        if result.returncode == 0:
            break
        if attempt == 0:
            say("       the launch was refused; the system may still be putting the last instance away")
        time.sleep(2)
    if result.returncode != 0:
        say(f"FAIL: could not launch it: {result.stderr.strip() or result.stdout.strip()}")
        if running():
            say("      an instance is already running, and `open --env` cannot attach to one")
        return 1

    failures = []

    def shot(name: str) -> None:
        path = os.path.join(shots, name + ".png")
        front()
        subprocess.run(["screencapture", "-x", "-o", path], capture_output=True)
        say(f"  shot {path}")

    def progress(state: dict) -> None:
        run = state.get("run") or {}
        say(f"       … {run.get('stage')} {run.get('progress')} % {run.get('file') or ''}")

    try:
        say("the window comes up")
        # a freshly installed bundle may sit behind the system's "access files in Documents"
        # prompt before its window is up; a person has to answer that, so this waits as long as
        # opening the project is allowed to take
        state = wait_for(probe, lambda s: bool(s.get("title")), "the window to report itself (answer the privacy prompt if one is showing)", max(90, args.open_timeout))
        check(APP_PROCESS in state["title"], f"the title reads {state['title']!r}")
        check(bool(state.get("controls")), "the window can say where its workspace tabs are")
        check(bool(state.get("version")), f"the build calls itself version {state.get('version')}")

        if args.project:
            say("the project opens")
            expected = os.path.basename(os.path.dirname(os.path.abspath(args.project)))
            state = wait_for(probe, lambda s: s.get("hasResults") and s.get("ionRows", 0) > 0,
                             "the results to load", args.open_timeout)
            check(expected in state["title"], f"the title names the project: {state['title']!r}")
            check(state["ionRows"] > 0, f"the ion table holds {state['ionRows']} features")
            check(state["workspaceName"] == "Analytics",
                  "opening a processed project lands on the review workspace")
            shot("opened")

        if args.process:
            say("the unprocessed project opens on the batch")
            state = wait_for(probe, lambda s: s.get("hasProject") and s.get("samples", 0) == expected_samples,
                             f"the {expected_samples} samples to load", 60)
            check(state["workspaceName"] == "Samples", "a project with no results lands on the Samples workspace")
            check(not state["hasResults"], "and has no results yet")

            say("Cmd+R processes the batch")
            state = press_until(probe, KEY_R, lambda s: (s.get("run") or {}).get("running") or s.get("hasResults"),
                                "the run to start", 30)
            check(state["workspaceName"] == "Analytics", "processing switches to the review workspace")
            started = time.time()
            state = wait_for(probe, lambda s: s.get("hasResults") and s.get("ionRows", 0) > 0 and not (s.get("run") or {}).get("running"),
                             "the run to finish", args.process_timeout, report=progress)
            elapsed = time.time() - started
            run = state["run"]
            check(run.get("error") is None, f"the run finished without an error in {elapsed:.0f} s")
            check(state["ionRows"] > 0, f"the alignment holds {state['ionRows']} features")
            if any(p.lower().endswith(".wiff") for p in glob.glob(os.path.join(args.process, "*"))):
                check(run.get("nativeReads", 0) == expected_samples,
                      f"the SCIEX plugin in the bundle read {run.get('nativeReads')} of {expected_samples} .wiff files natively")
            out_folder = state.get("outputFolder") or ""
            check(os.path.exists(os.path.join(out_folder, "opendial_method.txt")), "the method was written beside the results")
            aligns = glob.glob(os.path.join(out_folder, "*.mdalign"))
            check(len(aligns) == 1, f"one alignment table was exported: {os.path.basename(aligns[0]) if aligns else 'none'}")
            check(bool(glob.glob(os.path.join(out_folder, "*.mdproject"))), "an MS-DIAL project was saved with it")
            with open(launch_path, encoding="utf-8") as handle:
                saved = json.load(handle)
            check(bool(saved.get("MdprojectPath")), "the OpenDIAL project now points at the MS-DIAL project")
            shot("processed")

            if can_click:
                say("the ion table answers the keyboard: filtering by typing")
                state = read_probe(probe)
                target = state["selected"]
                check(target is not None, f"a feature is selected: #{target['id']} {target['name']}")
                click_control(state, "control.FilterBox", "the filter box")
                # the m/z to four decimals is the one text that names a single feature; a short id
                # such as "0" is a substring of half the table's m/z values
                needle = target.get("mzText") or f"{target['mz']:.4f}"
                type_text(needle)
                state = wait_for(probe, lambda s: s.get("ionRows") == 1 and (s.get("review") or {}).get("filter") == needle,
                                 f"the filter to narrow the table to the one feature at m/z {needle}", 15)
                check(state["selected"]["id"] == target["id"], f"the table narrowed to feature #{target['id']} and kept it selected")

                say("a peak is re-integrated by typing a window and clicking Apply to all")
                peaks = [p for p in state["selected"]["samples"] if p.get("height") and p.get("left") is not None]
                check(len(peaks) > 0, f"the feature has a detected peak in {len(peaks)} sample(s)")
                first = peaks[0]
                width = max(0.02, (first["right"] - first["left"]) / 4.0)
                new_from = round(first["rt"] - width, 3)
                new_to = round(first["rt"] + width, 3)
                heights_before = {p["file"]: p["height"] for p in state["selected"]["samples"]}
                replace_text(state, "control.IntegrateFrom", "the integrate-from box", f"{new_from:.3f}")
                replace_text(state, "control.IntegrateTo", "the integrate-to box", f"{new_to:.3f}")
                state = wait_for(probe, lambda s: (s.get("review") or {}).get("integrationFrom") == f"{new_from:.3f}"
                                 and (s.get("review") or {}).get("integrationTo") == f"{new_to:.3f}",
                                 "the typed window to reach the boxes", 15)
                check(True, f"the boxes read {new_from:.3f}–{new_to:.3f} min")
                click_control(state, "control.ApplyToAll", "the Apply to all button")
                state = wait_for(probe, lambda s: (s.get("review") or {}).get("peaksEdited") is True
                                 and (s.get("selected") or {}).get("manuallyQuantified") is True,
                                 "the re-integration to be applied", 120)
                after = state["selected"]["samples"]
                inside = [p for p in after if p.get("left") is not None and p["left"] >= new_from - 1e-6 and p["right"] <= new_to + 1e-6]
                check(len(inside) == len([p for p in after if p.get("left") is not None]),
                      f"every integrated sample now sits inside {new_from:.3f}–{new_to:.3f} min ({len(inside)} of {len(after)})")
                changed = sum(1 for p in after if p.get("height") is not None and heights_before.get(p["file"]) != p["height"])
                check(changed >= 1, f"{changed} sample height(s) changed with the narrower window")
                shot("reintegrated")

                say("Save review writes the edit back")
                click_control(state, "control.SaveReview", "the Save review button")
                state = wait_for(probe, lambda s: (s.get("review") or {}).get("peaksEdited") is False,
                                 "the review to be saved", 60)
                arf = glob.glob(os.path.join(out_folder, "AlignResult-*.arf2"))
                tags = glob.glob(os.path.join(out_folder, "AlignResult-*_tags.xml"))
                backups = glob.glob(os.path.join(out_folder, "*.before-curation"))
                check(len(tags) >= 1, f"the tag file MS-DIAL reads exists: {os.path.basename(tags[0]) if tags else 'none'}")
                check(len(backups) >= 1, f"{len(backups)} file(s) kept as they were before the edit")
                check(len(arf) >= 1, "the alignment container was rewritten in place")

                say("the reviewed table is exported (the save panel is the system's, so the path is handed over)")
                reviewed = os.path.join(work, "cycle", "reviewed.txt")
                send_command(probe, {"action": "exportReviewed", "path": reviewed})
                check(os.path.exists(reviewed), "the reviewed table exists")
                with open(reviewed, encoding="utf-8") as handle:
                    lines = [l.rstrip("\n").split("\t") for l in handle if l.strip()]
                header, classes, rows = lines[0], lines[1], lines[2:]
                check(len(rows) == 1, f"it holds the {len(rows)} feature the filter was showing")
                quant = header.index("Manually quantified")
                check(rows[0][0] == str(target["id"]) and rows[0][quant] == "True",
                      f"feature #{rows[0][0]} is marked as quantified by hand")
                check(classes[0] == "Class" and len(classes) == len(header), "the second line names each sample's class")

                components = os.path.join(work, "cycle", "components.csv")
                send_command(probe, {"action": "exportOpenQuant", "path": components})
                check(os.path.exists(components) and len(open(components, encoding="utf-8").read().splitlines()) >= 1,
                      "the OpenQuant component list exists")

                say("the manual opens from the keyboard and answers a search")
                state = press_until(probe, 122, lambda s: (s.get("help") or {}).get("open") is True,
                                    "F1 to open the help window", 30, modifier=None)
                check(state["help"]["page"] == "analytics-workspace",
                      f"F1 on the review workspace opens its page: {state['help']['title']!r}")
                send_command(probe, {"action": "openHelp", "page": "index", "query": "drift correction"})
                state = wait_for(probe, lambda s: (s.get("help") or {}).get("results", 0) > 0, "the search to find pages", 15)
                check(state["help"]["results"] > 0, f"searching 'drift correction' finds {state['help']['results']} page(s)")
                # the same page in Portuguese, and back: the switch keeps the page and the search
                send_command(probe, {"action": "helpLanguage", "language": "pt"})
                state = wait_for(probe, lambda s: (s.get("help") or {}).get("language") == "pt", "the manual in Portuguese", 15)
                check(state["help"]["page"] == "index" and state["help"]["title"].startswith("Manual do OpenDIAL"),
                      f"Português shows the same page, titled {state['help']['title']!r}")
                check(state["help"]["results"] > 0, f"the search still finds {state['help']['results']} page(s) in Portuguese")
                send_command(probe, {"action": "helpLanguage", "language": "en"})
                state = wait_for(probe, lambda s: (s.get("help") or {}).get("language") == "en", "the manual back in English", 15)
                check(state["help"]["title"] == "OpenDIAL manual", "English brings the English page back")
                send_command(probe, {"action": "closeHelp"})

                say("the filter is cleared again")
                state = read_probe(probe)
                click_control(state, "control.FilterBox", "the filter box")
                press(KEY_A)
                osascript('tell application "System Events" to key code 51', check=True)   # delete
                state = wait_for(probe, lambda s: s.get("ionRows", 0) > 1, "the table to fill again", 15)
                check(state["ionRows"] > 1, f"the table shows all {state['ionRows']} features again")

        # a key goes to whatever is frontmost, and something else on the desk can take the front for a
        # moment; press_until presses again rather than waiting out a keystroke that went elsewhere
        SHORTCUT_WAIT = 30
        say("every workspace shortcut goes where it says")
        for number, code in KEY_CODES.items():
            state = press_until(probe, code, lambda s, n=number: s.get("workspace") == n - 1,
                                f"Cmd+{number} to select {WORKSPACES[number - 1]}", SHORTCUT_WAIT)
            check(state["workspaceName"] == WORKSPACES[number - 1],
                  f"Cmd+{number} selects {WORKSPACES[number - 1]}")

        say("and so does the control key, for a keyboard without a command key")
        press_until(probe, KEY_CODES[2], lambda s: s.get("workspace") == 1,
                    "Ctrl+2 to select Analytics", SHORTCUT_WAIT, modifier="control")
        check(True, "Ctrl+2 selects Analytics")

        if can_click:
            say("the workspace tabs answer a click, where the window says they are")
            press_until(probe, KEY_CODES[1], lambda s: s.get("workspace") == 0, "the Explorer", SHORTCUT_WAIT)
            state = read_probe(probe)
            spot = state["controls"].get("workspace.Statistics")
            check(spot is not None, "the window reports where the Statistics tab is")
            x, y = to_screen(state, spot)
            say(f"       the tab is at {x:.0f}, {y:.0f} on screen")
            click(x, y)
            state = wait_for(probe, lambda s: s.get("workspace") == 4, "the click to select Statistics", SHORTCUT_WAIT)
            check(state["workspaceName"] == "Statistics", "clicking the Statistics tab selects it")
            if args.project or args.process:
                check(state["statistics"]["hasResults"], "the statistics workspace has the batch")
            shot("statistics")

            if args.project or args.process:
                say("the one-factor pages answer, and a chart leaves as SVG and PNG")
                stats = state["statistics"]
                say(f"       source: {stats.get('source')} · {stats.get('features')} feature(s) · {stats.get('standards')}")
                check(stats.get("features", 0) > 1, "the analysis dataset has features")
                for page in ("Volcano plot", "Principal components", "Heatmap", "Lipid enrichment"):
                    send_command(probe, {"action": "selectStatisticsPage", "page": page})
                    state = wait_for(probe, lambda s, p=page: (s.get("statistics") or {}).get("page") == p, f"the {page} page", 15)
                    check(state["statistics"]["page"] == page, f"the {page} page is showing")
                    shot("statistics-" + page.lower().replace(" ", "-"))
                if can_click:
                    say("the heatmap is built and the enrichment computed by clicking their buttons")
                    send_command(probe, {"action": "selectStatisticsPage", "page": "Heatmap"})
                    state = wait_for(probe, lambda s: (s.get("statistics") or {}).get("page") == "Heatmap", "the Heatmap page", 15)
                    click_control(state, "control.HeatmapBuild", "the Build button")
                    state = wait_for(probe, lambda s: (s.get("statistics") or {}).get("heatmapRows", 0) > 0, "the heatmap to be built", 60)
                    check(state["statistics"]["heatmapRows"] > 0, f"the heatmap holds {state['statistics']['heatmapRows']} feature row(s): {state['statistics']['heatmap']}")
                    shot("statistics-heatmap-built")

                    send_command(probe, {"action": "selectStatisticsPage", "page": "Lipid enrichment"})
                    state = wait_for(probe, lambda s: (s.get("statistics") or {}).get("page") == "Lipid enrichment", "the Lipid enrichment page", 15)
                    before = state["statistics"].get("enrichment")
                    click_control(state, "control.EnrichmentCompute", "the Compute button")
                    state = wait_for(probe, lambda s: (s.get("statistics") or {}).get("enrichment") not in (before, "", None),
                                     "the enrichment to be computed", 60)
                    stats = state["statistics"]
                    # with one injection per class there is no comparison to enrich, and the page must say so
                    check(bool(stats.get("enrichment")), f"the enrichment answered: {stats.get('enrichment')} ({stats.get('enrichmentSets')} set(s))")
                    shot("statistics-enrichment-computed")

                    send_command(probe, {"action": "selectStatisticsPage", "page": "Pathways"})
                    state = wait_for(probe, lambda s: (s.get("statistics") or {}).get("page") == "Pathways", "the Pathways page", 15)
                    before = state["statistics"].get("pathways")
                    state = click_until(probe, "control.PathwaysCompute", "the pathways' Compute button",
                                        lambda s: (s.get("statistics") or {}).get("pathways") not in (before, "", None), 60)
                    stats = state["statistics"]
                    check(bool(stats.get("pathways")), f"the pathway analysis answered: {stats.get('pathways')} ({stats.get('reactions')} reaction(s) tested)")
                    shot("statistics-pathways")

                    send_command(probe, {"action": "selectStatisticsPage", "page": "Two factors"})
                    state = wait_for(probe, lambda s: (s.get("statistics") or {}).get("page") == "Two factors", "the Two factors page", 15)
                    before = state["statistics"].get("twoFactor")
                    state = click_until(probe, "control.TwoFactorCompute", "the two-factor Compute button",
                                        lambda s: (s.get("statistics") or {}).get("twoFactor") not in (before, "", None, "Fitting every feature, then partitioning the matrix…"), 120)
                    check(bool(state["statistics"].get("twoFactor")), f"the two-factor page answered: {state['statistics'].get('twoFactor')}")
                    shot("statistics-two-factor")

                for fmt in ("svg", "png"):
                    chart = os.path.join(work, f"volcano.{fmt}")
                    result = send_command(probe, {"action": "exportChart", "page": "Volcano plot", "index": 0, "format": fmt, "scale": 2, "path": chart})["lastCommand"]
                    ok = os.path.exists(chart) and os.path.getsize(chart) > 500
                    if ok and fmt == "svg":
                        ok = open(chart, encoding="utf-8").read().lstrip().startswith("<svg")
                    check(ok, f"the volcano plot was written as {fmt}: {(result or {}).get('message')}")

                say("a figure comes out in the theme it was asked for, not the window's")
                for theme, paper in (("Light", "#ffffff"), ("Dark", "#1e2124")):
                    chart = os.path.join(work, f"volcano-{theme.lower()}.svg")
                    send_command(probe, {"action": "exportChart", "page": "Volcano plot", "index": 0,
                                         "format": "svg", "theme": theme, "path": chart})
                    first = re.search(r'<rect[^>]*fill="(#[0-9a-f]{6})"', open(chart, encoding="utf-8").read())
                    check(first is not None and first.group(1) == paper,
                          f"a {theme.lower()} figure is drawn on {paper}, whatever the window's theme")
                clear = os.path.join(work, "volcano-clear.png")
                send_command(probe, {"action": "exportChart", "page": "Volcano plot", "index": 0,
                                     "format": "png", "scale": 2, "theme": "Light", "background": "Transparent", "path": clear})
                check(os.path.getsize(clear) > 500, "and one with nothing behind it was written as PNG")

            if args.project:
                say("the review keys tag the selected feature from wherever the focus is")
                state = press_until(probe, KEY_CODES[2], lambda s: s.get("workspace") == 1, "Analytics", SHORTCUT_WAIT)
                before = (state.get("review") or {}).get("confirmed", 0)
                state = press_until(probe, KEY_CODES[1], lambda s: (s.get("review") or {}).get("confirmed") == before + 1,
                                    "⌘⇧1 to tag the selected feature Confirmed", SHORTCUT_WAIT,
                                    modifiers="command down, shift down", patience=10)
                check(True, f"⌘⇧1 tags the selected feature: {before} → {before + 1} confirmed")
                press_until(probe, KEY_CODES[1], lambda s: (s.get("review") or {}).get("confirmed") == before,
                            "⌘⇧1 again to take the tag off", SHORTCUT_WAIT,
                            modifiers="command down, shift down", patience=10)
                check(True, "and ⌘⇧1 again takes it off, so the project is left as it was")

                say("the ion table tears off, and docks back when its window is dragged over the main one")
                state = click_until(probe, "control.ToggleIonTableWindow", "the Open in a window button",
                                    lambda s: (s.get("review") or {}).get("ionTableDetached") is True, 20)
                check(True, "the ion table opened in its own window")
                time.sleep(0.8)
                shot("ion-table-detached")
                ix, iy, iw, ih = window_frame_named("Ion table")
                mx, my, mw, mh = window_frame_named("OpenDIAL")
                grip = (ix + iw / 2, iy + 12)
                target = (mx + mw * 0.2, my + mh * 0.5)
                say(f"       dragging the window's title bar from {grip[0]:.0f}, {grip[1]:.0f} to {target[0]:.0f}, {target[1]:.0f}")
                # the path goes past the zone's edge in steps, the way a hand does
                path = [(grip[0] + (target[0] - grip[0]) * k / 6, grip[1] + (target[1] - grip[1]) * k / 6) for k in range(1, 7)]
                drag_hold(grip[0], grip[1], path)
                try:
                    state = wait_for(probe, lambda s: (s.get("review") or {}).get("dockPreview") is True,
                                     "the main window to show where the table will land", 10)
                    check(True, "the drop place lights up in the main window while the window is held over it")
                    shot("ion-table-dock-preview")
                finally:
                    drag_release(target[0], target[1])
                state = wait_for(probe, lambda s: (s.get("review") or {}).get("ionTableDetached") is False,
                                 "the table to dock back", 20)
                check((state.get("review") or {}).get("dockPreview") is False, "the drop place goes away once the table is docked")
                time.sleep(0.6)
                shot("ion-table-docked-back")

                # the --process form opens the manual from the keyboard; here it is opened by command,
                # so the language switch is exercised on a project that loads in seconds
                say("the manual reads in both languages")
                send_command(probe, {"action": "openHelp", "page": "statistics-workspace"})
                state = wait_for(probe, lambda s: (s.get("help") or {}).get("page") == "statistics-workspace", "the manual to open", 15)
                send_command(probe, {"action": "helpLanguage", "language": "pt"})
                state = wait_for(probe, lambda s: (s.get("help") or {}).get("language") == "pt", "the manual in Portuguese", 15)
                check(state["help"]["page"] == "statistics-workspace" and "Statistics" in state["help"]["title"],
                      f"Português keeps the page: {state['help']['title']!r}")
                shot("help-pt")
                send_command(probe, {"action": "helpLanguage", "language": "en"})
                state = wait_for(probe, lambda s: (s.get("help") or {}).get("language") == "en", "the manual back in English", 15)
                check(state["help"]["title"] == "The Statistics workspace", "English brings the English page back")
                send_command(probe, {"action": "closeHelp"})

        say("nothing fell over")
        alive = subprocess.run(["pgrep", "-f", "OpenDIAL.app/Contents/MacOS"], capture_output=True)
        check(bool(alive.stdout.strip()), "the application is still running")
        for name, path in (("stdout", out_log), ("stderr", err_log)):
            if not os.path.exists(path):
                continue
            text = open(path, encoding="utf-8", errors="replace").read()
            bad = [line for line in text.splitlines()
                   if "Unhandled exception" in line or "System.NullReferenceException" in line]
            check(not bad, f"nothing unhandled on {name}" + (f": {bad[:2]}" if bad else ""))

    except Failed as failure:
        failures.append(str(failure))
        shot("failure")
    finally:
        if not args.keep:
            quit_running()

    if failures:
        say("")
        for failure in failures:
            say("FAIL: " + failure)
        say(f"logs and screenshots under {work}")
        return 1

    say("")
    if args.process:
        say("the installed application processes a batch of real acquisitions, re-integrates a peak from the keyboard, "
            "saves the review, exports it, and opens its manual")
    else:
        say("the installed application opens, loads a real project, and answers both the keyboard and the mouse")
    say(f"work folder kept at {work}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
