---
title: Re-integrating and splitting peaks
section: Reviewing
order: 23
summary: Redrawing a peak's integration window by hand in one injection or all of them, splitting a feature that holds two compounds, and how the edit is saved.
---

# Re-integrating and splitting peaks

Automatic integration gets shoulders, tailing and split peaks wrong often enough that a result is
not finished until the worst of them have been redrawn. The **integrate** strip sits above the peak
grid in the Peaks tab of the [[evidence-panels]].

![re-integrating a feature](images/review-integrate.png)

## Drawing the window

Two ways to set it:

- `⇧`-drag across any panel of the grid. The panel you dragged on becomes the injection in focus, and the two retention times land in the **from** and **to** boxes.
- Type the two retention times, in minutes, into the boxes.

**Reset** puts the current peak's own boundaries back in the boxes. The hint beside the strip says
what will happen next.

## Applying it

**Apply to all** re-integrates the feature over the window in every injection. **This sample**
does it only in the injection in focus — the one whose panel was dragged, or whose row is selected
in the Samples tab. The grid loads chromatograms one injection at a time; an injection that has not
been drawn yet is read first, so the edit never lands on a stale boundary.

## What is recomputed

Inside the window, for each injection: the **height** becomes the apex, the **area** the trapezoid
under the trace (in intensity × seconds, MS-DIAL's unit), and the baseline-corrected area subtracts
the straight line joining the two edges. The start, apex and end are moved to the first, highest
and last scan inside the window. These are the definitions the run itself used, so a redrawn peak
stays comparable with the ones beside it.

The feature's own numbers follow: its mean height, fill percentage, mean retention time and mean
peak width. It is marked **manually modified for quantification**, which is the flag MS-DIAL
exports and the reviewed table writes as *Manually quantified*, and it now passes the
**Hand-edited** filter. The status bar reports what changed: `Re-integrated 1.812–1.902 min over
all samples: 8 changed, 0 skipped · mean height 1,234,567 · fill 100 %`.

## Splitting co-eluting isomers

When one aligned feature holds two compounds, **Split isomer** copies it in place. The copy sits
right after the original with its own id, carries a comment saying where it came from
(`split from #123`), and starts as an exact duplicate, marked as manually modified. Give each copy
its own integration window with the strip, then name them from the ion table or the candidate
list. This is MS-DIAL's "duplicate peak spot" workflow.

## Saving

Both edits change the alignment result itself, not a sidecar. **Save review** writes the container
back through MS-DIAL's own serialiser, so the edited result opens in MS-DIAL and feeds the
exporters unchanged. The files as they were before the first edit of the session are kept beside
them with a `.before-curation` suffix — the alignment container, its peak properties, its drift
spots — so the edit can be undone by renaming them back. See [[projects-and-files]].

## When it is not possible

A result opened from an `.mdalign` export rather than the binary alignment files has no container
to edit; the status bar says so, and tagging still works. The same is true of a results folder
whose `.arf2` files are missing.
