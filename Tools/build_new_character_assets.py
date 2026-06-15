from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
CHARACTER_ROOT = ROOT / "unity dev" / "New Client_Unity_Base_Cli" / "Assets" / "Art" / "Characters"


CHARACTERS = {
    "trainee": {
        "sheet": "trainee_rig_sheet_cyan.png",
        "skin": (249, 210, 53, 255),
        "parts": {
            "head": (30, 80, 320, 310),
            "body": (360, 85, 610, 395),
            "left_upper_arm": (655, 105, 750, 290),
            "right_upper_arm": (790, 105, 885, 290),
            "left_forearm": (895, 105, 995, 290),
            "right_forearm": (1015, 105, 1115, 290),
            "left_hand_relaxed": (665, 290, 755, 390),
            "right_hand_relaxed": (790, 290, 875, 390),
            "left_hand_raised": (915, 290, 995, 390),
            "right_hand_raised": (1015, 290, 1100, 390),
            "idle_prop": (1340, 235, 1500, 395),
            "eye_open_left": (60, 455, 165, 570),
            "eye_open_right": (160, 455, 260, 570),
            "eye_closed_left": (370, 480, 455, 550),
            "eye_closed_right": (470, 480, 555, 550),
            "mouth_neutral": (640, 475, 770, 565),
            "mouth_eat": (825, 470, 940, 565),
            "mouth_chew": (990, 470, 1115, 565),
            "mouth_happy": (1150, 470, 1270, 565),
            "mouth_fail": (1290, 470, 1470, 575),
            "brow_left": (595, 620, 675, 685),
            "brow_right": (690, 620, 785, 685),
        },
        "defeat": (1070, 585, 1375, 970),
    },
    "milkdragon": {
        "sheet": "milkdragon_rig_sheet_cyan.png",
        "skin": (251, 218, 71, 255),
        "parts": {
            "head": (55, 65, 405, 375),
            "body": (425, 110, 735, 430),
            "left_upper_arm": (770, 65, 895, 225),
            "right_upper_arm": (945, 65, 1075, 225),
            "left_forearm": (770, 220, 890, 375),
            "right_forearm": (950, 220, 1075, 375),
            "left_hand_raised": (1120, 125, 1245, 280),
            "right_hand_raised": (1305, 125, 1425, 280),
            "left_hand_relaxed": (770, 380, 900, 525),
            "right_hand_relaxed": (945, 380, 1075, 525),
            "left_leg": (70, 575, 210, 780),
            "right_leg": (225, 575, 370, 780),
            "eye_open_left": (445, 510, 555, 640),
            "eye_open_right": (575, 510, 690, 640),
            "eye_closed_left": (755, 575, 850, 655),
            "eye_closed_right": (880, 575, 975, 655),
            "brow_left": (440, 645, 560, 725),
            "brow_right": (570, 645, 690, 725),
            "mouth_neutral": (85, 790, 225, 885),
            "mouth_eat": (325, 775, 465, 890),
            "mouth_chew": (510, 780, 630, 880),
            "mouth_happy": (675, 765, 810, 895),
            "mouth_fail": (845, 750, 1010, 890),
        },
        "defeat": (1050, 325, 1500, 900),
    },
}


def remove_chroma(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32).copy()
    border = np.concatenate(
        [rgba[0, :, :3], rgba[-1, :, :3], rgba[:, 0, :3], rgba[:, -1, :3]], axis=0
    )
    key = np.median(border, axis=0)
    distance = np.linalg.norm(rgba[:, :, :3] - key, axis=2)
    matte = np.clip((distance - 10.0) / 68.0, 0.0, 1.0)
    safe_matte = np.maximum(matte, 0.04)[:, :, None]
    recovered = (rgba[:, :, :3] - key[None, None, :] * (1.0 - matte[:, :, None])) / safe_matte
    rgba[:, :, :3] = np.clip(recovered, 0.0, 255.0)
    rgba[:, :, 3] *= matte
    rgba[:, :, 3][matte < 0.035] = 0.0
    return Image.fromarray(np.uint8(np.clip(rgba, 0, 255)), "RGBA")


def crop_tight(sheet: Image.Image, rect, padding: int = 4) -> Image.Image:
    crop = sheet.crop(rect)
    bbox = crop.getchannel("A").getbbox()
    if not bbox:
        raise ValueError(f"No opaque pixels inside crop {rect}")
    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(crop.width, bbox[2] + padding)
    bottom = min(crop.height, bbox[3] + padding)
    return crop.crop((left, top, right, bottom))


def make_closed_eye(open_eye: Image.Image, closed_arc: Image.Image, skin) -> Image.Image:
    canvas = Image.new("RGBA", open_eye.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    margin_x = max(1, round(open_eye.width * 0.04))
    margin_y = max(1, round(open_eye.height * 0.08))
    draw.ellipse(
        (margin_x, margin_y, open_eye.width - margin_x, open_eye.height - margin_y),
        fill=skin,
    )
    target_width = max(1, round(open_eye.width * 0.72))
    scale = target_width / max(1, closed_arc.width)
    target_height = max(1, round(closed_arc.height * scale))
    arc = closed_arc.resize((target_width, target_height), Image.Resampling.LANCZOS)
    x = (canvas.width - arc.width) // 2
    y = (canvas.height - arc.height) // 2
    canvas.alpha_composite(arc, (x, y))
    return canvas


def build(character_id: str, config) -> None:
    character_root = CHARACTER_ROOT / character_id
    source_folder = character_root / "Source"
    parts_folder = character_root / "Parts"
    parts_folder.mkdir(parents=True, exist_ok=True)

    sheet = remove_chroma(Image.open(source_folder / config["sheet"]))
    extracted = {}
    for name, rect in config["parts"].items():
        extracted[name] = crop_tight(sheet, rect)

    for side in ("left", "right"):
        open_name = f"eye_open_{side}"
        closed_name = f"eye_closed_{side}"
        extracted[closed_name] = make_closed_eye(
            extracted[open_name], extracted[closed_name], config["skin"]
        )

    for name, image in extracted.items():
        image.save(parts_folder / f"{name}.png")

    crop_tight(sheet, config["defeat"], padding=8).save(character_root / "gameover_defeat_v1.png")
    print(f"Built {character_id}: {len(extracted)} parts")


def main() -> None:
    for character_id, config in CHARACTERS.items():
        build(character_id, config)


if __name__ == "__main__":
    main()
