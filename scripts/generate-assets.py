#!/usr/bin/env python3
"""Generate the repository preview and illustrated demo from deterministic primitives."""

from pathlib import Path

from PIL import Image, ImageDraw


ASSET_DIR = Path(__file__).resolve().parents[1] / "docs" / "assets"
BACKGROUND = "#081426"
PANEL = "#10243d"
PANEL_ALT = "#153352"
TEXT = "#edf5ff"
MUTED = "#9db2cc"
CYAN = "#22d3ee"
GREEN = "#4ade80"
AMBER = "#fbbf24"
RED = "#fb7185"

# Self-authored 5x7 bitmap glyphs. No external font file is used or redistributed.
GLYPHS = {
    " ": ["00000"] * 7,
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "B": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "C": ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "F": ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
    "G": ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
    "H": ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    "I": ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    "J": ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
    "K": ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    "L": ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "N": ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
    "O": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "P": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "Q": ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
    "R": ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    "T": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    "U": ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
    "V": ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
    "W": ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
    "X": ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
    "Y": ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
    "Z": ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
    "0": ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
    "1": ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
    "2": ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
    "3": ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
    "4": ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    "5": ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
    "6": ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
    "7": ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
    "8": ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
    "9": ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
    ".": ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
    ":": ["00000", "01100", "01100", "00000", "01100", "01100", "00000"],
    "-": ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    "/": ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
    "|": ["00100", "00100", "00100", "00100", "00100", "00100", "00100"],
    "+": ["00000", "00100", "00100", "11111", "00100", "00100", "00000"],
}


def text_size(value: str, scale: int) -> tuple[int, int]:
    return max(0, len(value) * 6 * scale - scale), 7 * scale


def draw_text(
    draw: ImageDraw.ImageDraw,
    position: tuple[int, int],
    value: str,
    scale: int,
    color: str = TEXT,
) -> None:
    x, y = position
    for character in value.upper():
        glyph = GLYPHS.get(character, GLYPHS[" "])
        for row, bits in enumerate(glyph):
            for column, bit in enumerate(bits):
                if bit == "1":
                    left = x + column * scale
                    top = y + row * scale
                    draw.rectangle(
                        (left, top, left + scale - 1, top + scale - 1),
                        fill=color,
                    )
        x += 6 * scale


def centered_text(
    draw: ImageDraw.ImageDraw,
    canvas_width: int,
    y: int,
    value: str,
    scale: int,
    color: str = TEXT,
) -> None:
    width, _ = text_size(value, scale)
    draw_text(draw, ((canvas_width - width) // 2, y), value, scale, color)


def draw_grid(draw: ImageDraw.ImageDraw, width: int, height: int, spacing: int = 40) -> None:
    for x in range(0, width, spacing):
        draw.line((x, 0, x, height), fill="#0d2138", width=1)
    for y in range(0, height, spacing):
        draw.line((0, y, width, y), fill="#0d2138", width=1)


def draw_box(
    draw: ImageDraw.ImageDraw,
    bounds: tuple[int, int, int, int],
    label: str,
    accent: str,
) -> None:
    draw.rounded_rectangle(bounds, radius=14, fill=PANEL, outline=accent, width=3)
    left, top, right, bottom = bounds
    label_width, label_height = text_size(label, 3)
    draw_text(
        draw,
        ((left + right - label_width) // 2, (top + bottom - label_height) // 2),
        label,
        3,
        TEXT,
    )


def generate_social_preview() -> None:
    image = Image.new("RGB", (1280, 640), BACKGROUND)
    draw = ImageDraw.Draw(image)
    draw_grid(draw, 1280, 640)
    draw.rectangle((0, 0, 18, 640), fill=CYAN)
    draw_text(draw, (72, 64), "WORKOPS.PLATFORM", 8, TEXT)
    draw_text(draw, (76, 142), "TENANT-SAFE .NET BACKEND", 4, CYAN)

    boxes = [
        ((76, 245, 286, 345), "OIDC", GREEN),
        ((350, 245, 600, 345), "API", CYAN),
        ((664, 245, 914, 345), "POSTGRES", AMBER),
        ((978, 245, 1190, 345), "REDIS", RED),
        ((350, 405, 600, 505), "OUTBOX", CYAN),
        ((664, 405, 914, 505), "RABBITMQ", GREEN),
    ]
    for bounds, label, accent in boxes:
        draw_box(draw, bounds, label, accent)

    for start, end in [
        ((286, 295), (350, 295)),
        ((600, 295), (664, 295)),
        ((914, 295), (978, 295)),
        ((475, 345), (475, 405)),
        ((600, 455), (664, 455)),
    ]:
        draw.line((*start, *end), fill=MUTED, width=4)

    draw_text(draw, (76, 572), "106 TESTS | MIT LICENSE | LOCAL EVIDENCE", 3, MUTED)
    image.save(ASSET_DIR / "workops-social-preview.png", optimize=True)


def scenario_frame(index: int, title: str, detail: str, accent: str) -> Image.Image:
    image = Image.new("RGB", (960, 540), BACKGROUND)
    draw = ImageDraw.Draw(image)
    draw_grid(draw, 960, 540, 36)
    draw.rectangle((0, 0, 960, 12), fill=accent)
    draw_text(draw, (42, 40), "ILLUSTRATED GOLDEN SCENARIO", 4, MUTED)
    draw_text(draw, (42, 112), f"STEP {index}/6", 3, accent)
    centered_text(draw, 960, 188, title, 5, TEXT)
    centered_text(draw, 960, 270, detail, 3, MUTED)

    for step in range(1, 7):
        left = 92 + (step - 1) * 132
        color = GREEN if step < index else accent if step == index else PANEL_ALT
        draw.rounded_rectangle((left, 380, left + 96, 404), radius=12, fill=color)
    draw_text(draw, (92, 444), "SYNTHETIC LOCAL DATA | NO HOSTED CLAIM", 3, MUTED)
    return image


def generate_demo_gif() -> None:
    steps = [
        ("AUTHENTICATING USERS", "LOCAL OIDC TOKENS", CYAN),
        ("CHECKING ROLE DENIAL", "VIEWER WRITE RETURNS 403", RED),
        ("CHECKING STALE WRITE", "OLD VERSION RETURNS 409", AMBER),
        ("CHECKING TENANT BOUNDARY", "FOREIGN READ RETURNS 404", CYAN),
        ("CHECKING AUDIT TRAIL", "EXPECTED EVENT IS PRESENT", GREEN),
        ("CHECKING NOTIFICATION", "DUPLICATE EFFECT IS PREVENTED", GREEN),
    ]
    frames = [
        scenario_frame(index, title, detail, accent)
        for index, (title, detail, accent) in enumerate(steps, start=1)
    ]

    summary = Image.new("RGB", (960, 540), BACKGROUND)
    draw = ImageDraw.Draw(summary)
    draw_grid(draw, 960, 540, 36)
    draw.rectangle((0, 0, 960, 12), fill=GREEN)
    centered_text(draw, 960, 80, "ILLUSTRATED SCENARIO", 5, MUTED)
    centered_text(draw, 960, 174, "GOLDEN FLOW EXERCISED", 5, GREEN)
    centered_text(draw, 960, 276, "TOKENS ARE NOT INTENTIONALLY", 3, TEXT)
    centered_text(draw, 960, 314, "PRINTED OR PERSISTED", 3, TEXT)
    centered_text(draw, 960, 410, "HOSTED EVIDENCE PENDING", 3, AMBER)
    frames.append(summary)

    frames[0].save(
        ASSET_DIR / "workops-demo.gif",
        save_all=True,
        append_images=frames[1:],
        duration=[1100] * 6 + [2200],
        loop=0,
        optimize=False,
        disposal=2,
    )


if __name__ == "__main__":
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    generate_social_preview()
    generate_demo_gif()
