from pathlib import Path
import argparse
import json
import math
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
        "sheet": "amai_male_rig_sheet_cyan_v2.png",
        "brow": (103, 67, 40, 255),
        "brow_width": 7,
    },
    "bean": {
        "sheet": "doudou_male_rig_sheet_cyan_v2.png",
        "brow": (67, 86, 45, 255),
        "brow_width": 7,
    },
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


def create_brow(path: Path, color, mirror: bool, width: int = 5) -> None:
    image = Image.new("RGBA", (80, 32), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    points = [(8, 22), (18, 15), (31, 11), (46, 11), (62, 16), (72, 21)]
    if mirror:
        points = [(79 - x, y) for x, y in reversed(points)]
    draw.line(points, fill=color, width=width, joint="curve")
    draw.line([(x, y + 1) for x, y in points], fill=(color[0], color[1], color[2], 150), width=2)
    image.save(path)


def find_components(image: Image.Image):
    mask = np.asarray(image.convert("RGBA"))[:, :, 3] > 18
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    components = []

    for y in range(height):
        for x in range(width):
            if not mask[y, x] or visited[y, x]:
                continue
            queue = deque([(x, y)])
            visited[y, x] = True
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while queue:
                px, py = queue.popleft()
                area += 1
                min_x = min(min_x, px)
                max_x = max(max_x, px)
                min_y = min(min_y, py)
                max_y = max(max_y, py)
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if 0 <= nx < width and 0 <= ny < height and mask[ny, nx] and not visited[ny, nx]:
                        visited[ny, nx] = True
                        queue.append((nx, ny))
            if area >= 12:
                components.append((min_x, min_y, max_x + 1, max_y + 1, area))

    return components


def component_score(rect, component) -> float:
    min_x, min_y, max_x, max_y, _ = component
    width = max_x - min_x
    height = max_y - min_y
    center_x = (min_x + max_x) * 0.5
    center_y = (min_y + max_y) * 0.5
    dx = (center_x - rect["cx"]) / max(rect["w"], 1)
    dy = (center_y - rect["cy"]) / max(rect["h"], 1)
    size_delta = abs(math.log(max(width, 1) / rect["w"])) + abs(math.log(max(height, 1) / rect["h"]))
    return dx * dx + dy * dy + size_delta * 0.20


def normalize_component(sheet: Image.Image, rect, components, template: Image.Image) -> Image.Image:
    min_x, min_y, max_x, max_y, _ = min(components, key=lambda item: component_score(rect, item))
    min_x = max(0, min_x - 3)
    min_y = max(0, min_y - 3)
    max_x = min(sheet.width, max_x + 3)
    max_y = min(sheet.height, max_y + 3)
    crop = sheet.crop((min_x, min_y, max_x, max_y))

    alpha_bbox = crop.getchannel("A").getbbox()
    if alpha_bbox:
        crop = crop.crop(alpha_bbox)

    target = template.convert("RGBA")
    target_alpha = target.getchannel("A").point(lambda value: 255 if value > 18 else 0)
    target_bbox = target_alpha.getbbox() or (0, 0, target.width, target.height)
    target_width = max(1, target_bbox[2] - target_bbox[0])
    target_height = max(1, target_bbox[3] - target_bbox[1])
    resized = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", target.size, (0, 0, 0, 0))
    result.alpha_composite(resized, (target_bbox[0], target_bbox[1]))
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
    components = find_components(sheet)

    for source_part in POMELO_PARTS.glob("*.png"):
        name = source_part.stem
        target = parts / source_part.name
        if name in layout:
            template = Image.open(source_part).convert("RGBA")
            normalize_component(sheet, layout[name], components, template).save(target)
        else:
            shutil.copy2(source_part, target)

    brow_width = config.get("brow_width", 5)
    create_brow(parts / "brow_left.png", config["brow"], False, brow_width)
    create_brow(parts / "brow_right.png", config["brow"], True, brow_width)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("character_ids", nargs="*", choices=sorted(VARIANTS))
    args = parser.parse_args()

    layout = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
    selected = args.character_ids or list(VARIANTS)
    for character_id in selected:
        build_variant(character_id, VARIANTS[character_id], layout)

    if not args.character_ids:
        create_brow(POMELO_PARTS / "brow_left.png", (105, 58, 35, 255), False)
        create_brow(POMELO_PARTS / "brow_right.png", (105, 58, 35, 255), True)
    print(f"Built sprite parts for: {', '.join(selected)}")


if __name__ == "__main__":
    main()
