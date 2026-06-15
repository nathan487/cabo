from pathlib import Path
import json
import shutil
from collections import deque

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
CHAR_ROOT = ROOT / "unity dev" / "New Client_Unity_Base_Cli" / "Assets" / "Art" / "Characters"
POMELO_PARTS = CHAR_ROOT / "pomelo" / "Parts"
LAYOUT_PATH = CHAR_ROOT / "pomelo" / "Source" / "parts_layout.json"

VARIANTS = {
    "strawberry": {
        "sheet": "xiaomei_rig_sheet_cyan.png",
        "brow": (111, 43, 35, 255),
    },
    "oat": {
        "sheet": "amai_rig_sheet_cyan.png",
        "brow": (103, 67, 40, 255),
    },
    "bean": {
        "sheet": "doudou_rig_sheet_cyan.png",
        "brow": (67, 86, 45, 255),
    },
}

GENERATED_PARTS = {
    "head",
    "body",
    "eye_open_left",
    "eye_open_right",
    "left_leg",
    "right_leg",
    "bag",
}


def remove_chroma(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32).copy()
    border = np.concatenate(
        [rgba[0, :, :3], rgba[-1, :, :3], rgba[:, 0, :3], rgba[:, -1, :3]], axis=0
    )
    key = np.median(border, axis=0)
    distance = np.linalg.norm(rgba[:, :, :3] - key, axis=2)
    matte = np.clip((distance - 16.0) / 72.0, 0.0, 1.0)
    safe_matte = np.maximum(matte, 0.05)[:, :, None]
    recovered = (rgba[:, :, :3] - key[None, None, :] * (1.0 - matte[:, :, None])) / safe_matte
    rgba[:, :, :3] = np.clip(recovered, 0.0, 255.0)
    rgba[:, :, 3] = rgba[:, :, 3] * matte

    cyan_line = (
        (rgba[:, :, 1] - rgba[:, :, 0] > 38.0)
        & (rgba[:, :, 2] - rgba[:, :, 0] > 38.0)
        & (rgba[:, :, 1] > 90.0)
        & (rgba[:, :, 2] > 90.0)
    )
    rgba[:, :, 3][cyan_line] = 0.0
    rgba[:, :, 3][matte < 0.03] = 0.0
    return Image.fromarray(np.uint8(np.clip(rgba, 0, 255)), "RGBA")


def create_brow(path: Path, color, mirror: bool) -> None:
    image = Image.new("RGBA", (80, 32), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    points = [(8, 22), (18, 15), (31, 11), (46, 11), (62, 16), (72, 21)]
    if mirror:
        points = [(79 - x, y) for x, y in reversed(points)]
    draw.line(points, fill=color, width=5, joint="curve")
    draw.line([(x, y + 1) for x, y in points], fill=(color[0], color[1], color[2], 150), width=2)
    image.save(path)


def resize_crop(sheet: Image.Image, rect, target_size, part_name: str) -> Image.Image:
    crop = sheet.crop((rect["x"], rect["y"], rect["x"] + rect["w"], rect["y"] + rect["h"]))
    result = keep_center_component(crop.resize(target_size, Image.Resampling.LANCZOS))
    if part_name == "body":
        rgba = np.asarray(result).copy()
        rgba[:, int(rgba.shape[1] * 0.90):, 3] = 0
        result = Image.fromarray(rgba, "RGBA")
    return result


def keep_center_component(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA")).copy()
    mask = rgba[:, :, 3] > 18
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    components = []

    for y in range(height):
        for x in range(width):
            if not mask[y, x] or visited[y, x]:
                continue
            queue = deque([(x, y)])
            visited[y, x] = True
            pixels = []
            while queue:
                px, py = queue.popleft()
                pixels.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if 0 <= nx < width and 0 <= ny < height and mask[ny, nx] and not visited[ny, nx]:
                        visited[ny, nx] = True
                        queue.append((nx, ny))
            components.append(pixels)

    if not components:
        return image

    center_x = (width - 1) * 0.5
    center_y = (height - 1) * 0.5
    selected = min(
        components,
        key=lambda pixels: min((x - center_x) ** 2 + (y - center_y) ** 2 for x, y in pixels),
    )
    keep = np.zeros_like(mask, dtype=bool)
    for x, y in selected:
        keep[y, x] = True

    rgba[:, :, 3][~keep] = 0
    return Image.fromarray(rgba, "RGBA")


def build_variant(character_id: str, config, layout) -> None:
    character_root = CHAR_ROOT / character_id
    source = character_root / "Source"
    parts = character_root / "Parts"
    parts.mkdir(parents=True, exist_ok=True)

    sheet_path = source / config["sheet"]
    if not sheet_path.exists():
        raise FileNotFoundError(sheet_path)
    sheet = remove_chroma(Image.open(sheet_path))

    for source_part in POMELO_PARTS.glob("*.png"):
        name = source_part.stem
        target = parts / source_part.name
        if name in GENERATED_PARTS:
            if name not in layout:
                raise KeyError(f"Missing layout entry for {name}")
            target_size = Image.open(source_part).size
            resize_crop(sheet, layout[name], target_size, name).save(target)
        else:
            shutil.copy2(source_part, target)

    create_brow(parts / "brow_left.png", config["brow"], False)
    create_brow(parts / "brow_right.png", config["brow"], True)


def main() -> None:
    layout = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
    for character_id, config in VARIANTS.items():
        build_variant(character_id, config, layout)

    create_brow(POMELO_PARTS / "brow_left.png", (105, 58, 35, 255), False)
    create_brow(POMELO_PARTS / "brow_right.png", (105, 58, 35, 255), True)
    print("Built strawberry, oat, bean sprite parts and pomelo eyebrow sprites.")


if __name__ == "__main__":
    main()
