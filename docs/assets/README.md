# Visual asset provenance

Both repository visuals are deterministic, source-controlled portfolio graphics created for Adnan
Alloh on 2026-08-02. The complete production source is
[`scripts/generate-assets.py`](../../scripts/generate-assets.py).

## Production method

- Runtime used for the committed outputs: Python 3.12.13 and Pillow 12.2.0.
- Drawing method: solid colors, lines, rounded rectangles, and a self-authored 5x7 bitmap glyph set
  embedded in the generator source.
- AI image generation: none for the current committed assets.
- Post-processing: none beyond PNG optimization and GIF encoding performed by Pillow.
- Fonts, icons, logos, stock images, screenshots, and external art: none.
- Input data: repository-specific labels and synthetic scenario statements only. No employer,
  client, customer, browser, token, or private-service data is used.
- Editable source: the Python generator is the canonical source; running it rewrites both outputs.

Pillow is used as a local rasterization and encoding tool and is not redistributed in this
repository. The committed source, layout, glyph definitions, and generated arrangement contain no
third-party visual element requiring a separate asset license. The repository copyright notice
names Adnan Alloh, and these source-controlled outputs are distributed under the repository MIT
License.

## Asset inventory

| Asset | Purpose | Evidence wording | SHA-256 |
|---|---|---|---|
| `workops-social-preview.png` | 1280x640 repository social preview | Architecture-oriented portfolio illustration | `32f2a527a1fb7c70ea4d883e938108cf99df56e6d6fb1ab46a200e65564ea68f` |
| `workops-demo.gif` | Seven-frame 960x540 golden-flow illustration | Illustrated scenario; not terminal or hosted-run evidence | `0bd25ec52f0300240dcbb2f3a8db318dcc560fe485b7fee54ea4573b9ab18ef1` |

## Reproduction

```bash
python3 -m pip install Pillow==12.2.0
python3 scripts/generate-assets.py
shasum -a 256 docs/assets/workops-social-preview.png docs/assets/workops-demo.gif
```

The generator does not depend on a system font, network service, hidden prompt, or editable binary
source. Different Pillow builds should preserve the content and dimensions, while encoder metadata
or compression details may affect byte-for-byte hashes.
