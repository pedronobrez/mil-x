# The MIL marks

Source drawings for the identity study of **MIL-X** (exploration, today's OpenDIAL) and **MIL-Q**
(quantification, today's OpenQuant). Nothing here is wired into the build: the rename has not been
decided, and these are the files a decision would start from.

| File | What it is |
| --- | --- |
| `mil-x-front.svg` | the cow's head, face on — MIL-X |
| `mil-q-front.svg` | the same head inside a ring — MIL-Q |

Both are drawn on a 64-unit grid: the cow's head face on, as one solid silhouette with the patch,
the eyes and the muzzle knocked out through a mask, so they carry real transparency and sit on any
background. MIL-Q is the same head inside a ring — a targeted assay is not a different animal. The
mark holds down to 16 px, which is what an application icon, a document icon and a favicon need.

The colour is an explicit hex on each shape (`#234B8C` for X, `#10685C` for Q); replace it to
recolour, and use `#FFFFFF` for the version that sits on a coloured tile.

A full-body profile was drawn and set aside: it read well large but stopped being a *cow* below
about 32 px, and a second posture is a second thing to maintain for no gain. One mark.

The study that explains the naming, the palette and the type is an artifact:
https://claude.ai/code/artifact/fb5b2be4-6a8d-4311-ab26-f71574e8f81a
