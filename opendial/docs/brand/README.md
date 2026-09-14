# The MIL marks

Source drawings for the identity study of **MIL-X** (exploration, today's OpenDIAL) and **MIL-Q**
(quantification, today's OpenQuant). Nothing here is wired into the build: the rename has not been
decided, and these are the files a decision would start from.

| File | What it is |
| --- | --- |
| `mil-x-front.svg` | the cow's head, face on — MIL-X |
| `mil-q-front.svg` | the same head inside a ring — MIL-Q |
| `mil-x-profile.svg` | the whole animal, side on — MIL-X |
| `mil-q-profile.svg` | the same animal inside a ring — MIL-Q |

All four are drawn on the same 64-unit grid, as one silhouette with the patch, the eyes and the
muzzle knocked out through a mask — so they carry real transparency and sit on any background.
The colour is an explicit hex on each shape (`#234B8C` for X, `#10685C` for Q); replace it to
recolour, and use `#FFFFFF` for the version that sits on a coloured tile.

**Which mark for which place.** The front view holds down to 16 px and is the one for the
application icon, the document icon and the favicon. The profile stops reading as a *cow*
somewhere below 32 px — it becomes a generic quadruped — so it belongs in landscape space: a
header lockup beside the wordmark, a title slide, a figure in a paper. They are the same animal
in two postures, not two identities.

The study that explains the naming, the palette and the type is an artifact:
https://claude.ai/code/artifact/fb5b2be4-6a8d-4311-ab26-f71574e8f81a
