"""Export the existing atlas clips unchanged as a review GIF at half speed."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
entries = json.loads((root / "manifest.js").read_text(encoding="utf-8-sig").split("=", 1)[1].rstrip(";\r\n"))
names = {"utburd": "Утбурд", "silat": "Силат", "nachzehrer": "Нахцерер", "vetala": "Ветала", "asura-preta": "Асура-прета"}
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
clips = [(entry, clip) for entry in entries for clip in entry["clips"]]
frames = []
for index in range(4):
    canvas = Image.new("RGB", (1248, 864), "#24271f")
    draw = ImageDraw.Draw(canvas)
    for slot, (entry, clip) in enumerate(clips):
        x, y = (slot % 3) * 416, (slot // 3) * 432
        draw.rectangle((x+3, y+3, x+412, y+428), fill="#3c483a", outline="#8c7755", width=2)
        action = "лечение" if clip["name"] == "heal" else "атака"
        draw.text((x+14, y+12), f'{names[entry["id"]]}: {action}', font=font, fill="#efe1be")
        atlas = Image.open(root / entry["image"]).convert("RGBA")
        f = clip["frames"][index]
        cell = atlas.crop(((f%4)*192, (f//4)*192, (f%4+1)*192, (f//4+1)*192))
        cell = cell.resize((384,384), Image.Resampling.NEAREST)
        canvas.paste(cell, (x+16,y+36), cell)
    frames.append(canvas)
frames[0].save(root / "undead-actions-half-speed.gif", save_all=True, append_images=frames[1:], duration=320, loop=0, disposal=2)
frames[2].save(root / "undead-actions-contact.png")
print("Exported 6 original clips, 4 frames each, 0.5x speed")

asura = next(entry for entry in entries if entry["id"] == "asura-preta")
atlas = Image.open(root / asura["image"]).convert("RGBA")
frames = []
for frame in asura["clips"][0]["frames"]:
    canvas = Image.new("RGB", (384,416), "#3c483a")
    ImageDraw.Draw(canvas).text((14,8), "Асура-прета: новая атака / 0,5x", font=font, fill="#efdbab")
    cell = atlas.crop((frame*192,0,(frame+1)*192,192)).resize((384,384), Image.Resampling.NEAREST)
    canvas.paste(cell, (0,32), cell)
    frames.append(canvas)
frames[0].save(root / "asura-preta-half-speed.gif", save_all=True, append_images=frames[1:], duration=320, loop=0, disposal=2)
