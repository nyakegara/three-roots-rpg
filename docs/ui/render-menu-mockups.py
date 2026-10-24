from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
W, H = 1600, 900
GOLD = (217, 184, 111)
PAPER = (224, 208, 164)
DARK = (42, 28, 18)
CREAM = (245, 231, 190)
FONT = r"C:\Windows\Fonts\consola.ttf"
FONT_BOLD = r"C:\Windows\Fonts\consolab.ttf"
FONT_TITLE = r"C:\Windows\Fonts\georgiab.ttf"
FONT_BODY = r"C:\Windows\Fonts\trebuc.ttf"


def f(size, bold=False, family="mono"):
    path = FONT_TITLE if family == "title" else FONT_BODY if family == "body" else FONT_BOLD if bold else FONT
    return ImageFont.truetype(path, size)


def fit(path, size):
    im = Image.open(ROOT / path).convert("RGBA")
    scale = max(size[0] / im.width, size[1] / im.height)
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.Resampling.LANCZOS)
    x, y = (im.width - size[0]) // 2, (im.height - size[1]) // 2
    return im.crop((x, y, x + size[0], y + size[1]))


def frame(draw, box, fill=(41, 27, 17, 239), border=GOLD, width=4):
    draw.rectangle(box, fill=fill, outline=(111, 76, 41), width=width + 4)
    x0, y0, x1, y1 = box
    draw.rectangle((x0 + 5, y0 + 5, x1 - 5, y1 - 5), outline=border, width=2)


def put(draw, xy, text, size, color=CREAM, bold=False, spacing=4, family="mono"):
    draw.text(xy, text, font=f(size, bold, family), fill=color, spacing=spacing, stroke_width=1, stroke_fill=(22, 15, 10))


def button(draw, x, y, w, label, primary=False):
    fill = (104, 75, 38, 248) if primary else (52, 36, 24, 242)
    draw.rectangle((x, y, x + w, y + 58), fill=fill, outline=GOLD if primary else (150, 111, 57), width=2)
    draw.rectangle((x + 4, y + 4, x + w - 4, y + 54), outline=(116, 83, 45), width=1)
    put(draw, (x + 22, y + 18), label, 19, (250, 236, 197) if primary else CREAM, bold=True, spacing=1)


def base_map():
    source = Image.open(ROOT / "content/art/source/exploration.flora-island-master-v5.png").convert("RGB")
    # Keep a soft ocean texture behind the menu rail and place the complete island
    # as a crisp inset map, so no coastline or encounter landmark sits under buttons.
    background = fit("content/art/source/exploration.flora-island-master-v5.png", (W, H)).convert("RGB").filter(ImageFilter.GaussianBlur(18))
    background = ImageEnhance.Brightness(background).enhance(.26)
    background = Image.blend(background, Image.new("RGB", (W, H), (5, 22, 39)), .62).convert("RGBA")
    map_size = (1230, 559)
    island = source.resize(map_size, Image.Resampling.LANCZOS).convert("RGBA")
    frame_layer = Image.new("RGBA", (map_size[0] + 20, map_size[1] + 20), (0, 0, 0, 0))
    fd = ImageDraw.Draw(frame_layer)
    fd.rectangle((0, 0, map_size[0] + 19, map_size[1] + 19), fill=(41, 27, 17), outline=(126, 82, 39), width=8)
    fd.rectangle((7, 7, map_size[0] + 12, map_size[1] + 12), outline=GOLD, width=2)
    frame_layer.alpha_composite(island, (10, 10))
    background.alpha_composite(frame_layer, (333, 160))
    return background


def base_splash(with_hero=True):
    im = fit("content/art/battlefield.forest.night.png", (W, H)).convert("RGB")
    im = ImageEnhance.Brightness(im).enhance(.72)
    im = ImageEnhance.Color(im).enhance(.9).convert("RGBA")
    shade = Image.new("RGBA", (W, H), (9, 12, 14, 94))
    im = Image.alpha_composite(im, shade)
    if with_hero:
        hero = Image.open(ROOT / "content/art/hero.human.png").convert("RGBA")
        # A restrained nearest-neighbour enlargement keeps the sprite pixels crisp.
        target_h = 470
        target_w = round(hero.width * target_h / hero.height)
        hero = hero.resize((target_w, target_h), Image.Resampling.NEAREST)
        im.alpha_composite(hero, (W - target_w - 70, H - target_h - 60))
    return im


def menu(im, map_option):
    d = ImageDraw.Draw(im, "RGBA")
    x = 94 if map_option else 102
    put(d, (x, 79), "ХРОНИКИ ТРЁХ КОРНЕЙ", 19, GOLD, True, 6)
    put(d, (x - 2, 122), "ТРИ", 68, (255, 235, 187), True, 1, "title")
    put(d, (x - 2, 185), "КОРНЯ", 68, (255, 235, 187), True, 1, "title")
    put(d, (x, 272), "ТРИ НАРОДА · ОДНА ЗЕМЛЯ", 16, (222, 204, 157), True, 2)
    if map_option:
        panel = (47, 334, 300, 737)
        frame(d, panel)
        labels = ["НОВАЯ ИГРА", "КАМПАНИИ", "ПРОДОЛЖИТЬ", "НАСТРОЙКИ", "ВЫХОД"]
        for idx, label in enumerate(labels):
            button(d, 63, 351 + idx * 72, 221, label, idx == 0)
        put(d, (57, 792), "СТРАТЕГИЯ · RPG", 14, (237, 218, 170), True, 2)
        put(d, (58, 824), "ДЕМО · 2026", 14, GOLD, True, 2)
    else:
        panel = (96, 369, 578, 744)
        frame(d, panel, (33, 24, 18, 196), (135, 101, 57), 3)
        labels = ["НОВАЯ ИГРА", "КАМПАНИИ", "ПРОДОЛЖИТЬ", "НАСТРОЙКИ", "ВЫХОД"]
        for idx, label in enumerate(labels):
            button(d, 119, 390 + idx * 65, 435, label, idx == 0)
        put(d, (103, 817), "ПАМЯТЬ ДОЛЬШЕ ЖИЗНИ", 15, GOLD, True, 3)
        put(d, (103, 849), "НА ЗЕМЛЕ ПРОСЫПАЮТСЯ МЁРТВЫЕ", 13, (229, 217, 185), True, 2)
    d.rectangle((8, 8, W - 9, H - 9), outline=(202, 156, 79), width=3)
    d.rectangle((14, 14, W - 15, H - 15), outline=(68, 47, 28), width=2)
    return im.convert("RGB")


CAMPAIGNS = [
    ("I", "ЗАЩИТА ОСТРОВА", "Лиара собирает флору, чтобы остановить высадку людей.", "ДОСТУПНА · ГЛАВА I", (91, 113, 51), "content/art/hero.flora.png"),
    ("II", "ГРАНИЦА РАССВЕТА", "Эдрик ищет причину пробуждения мёртвых.", "СЛЕДУЮЩАЯ · ГЛАВА II", (96, 63, 37), "content/art/hero.human.png"),
    ("III", "САД БЕЗ РАССВЕТА", "Морвен хранит забытые имена некрополя.", "ОТДЕЛЬНАЯ ХРОНИКА", (54, 65, 75), "content/art/hero.undead.png"),
]


def campaign_sprite(path, target_height=210):
    """Trim transparent canvas, then give every card character the same visual height."""
    art = Image.open(ROOT / path).convert("RGBA")
    bounds = art.getchannel("A").getbbox()
    if bounds:
        art = art.crop(bounds)
    width = max(1, round(art.width * target_height / art.height))
    art = art.resize((width, target_height), Image.Resampling.NEAREST)
    alpha = art.getchannel("A").point(lambda value: round(value * .8))
    art.putalpha(alpha)
    return art


def campaigns(im):
    shade = Image.new("RGBA", (W, H), (7, 8, 8, 235))
    im.alpha_composite(shade)
    d = ImageDraw.Draw(im, "RGBA")
    board = (139, 151, 1461, 754)
    d.rectangle(board, fill=(222, 207, 166, 255), outline=(117, 78, 39), width=8)
    d.rectangle((148, 160, 1452, 745), outline=(231, 203, 136), width=3)
    put(d, (189, 185), "ВЫБЕРИ ХРОНИКУ", 31, (48, 34, 21), True, 3, "title")
    put(d, (191, 229), "ТРИ ИСТОРИИ · ОДНА ЗЕМЛЯ", 14, (101, 77, 43), True, 2)
    d.line((188, 263, 1410, 263), fill=(139, 109, 64), width=2)
    cw, gap, top = 386, 24, 294
    for i, (chapter, title, desc, status, tone, art_path) in enumerate(CAMPAIGNS):
        x = 189 + i * (cw + gap)
        d.rectangle((x, top, x + cw, top + 349), fill=(*tone, 255), outline=(131, 93, 47), width=3)
        d.rectangle((x + 7, top + 7, x + cw - 7, top + 342), outline=(218, 187, 117), width=1)
        art = campaign_sprite(art_path)
        # Keep a consistent visible character height while reserving a narrow
        # text column on the left side of each card.
        if art.width > 180:
            new_height = round(art.height * 180 / art.width)
            art = art.resize((180, new_height), Image.Resampling.NEAREST)
        im.alpha_composite(art, (x + cw - art.width - 14, top + 115 + (210 - art.height)))
        d.rectangle((x + 14, top + 13, x + 45, top + 45), fill=(35, 28, 19), outline=GOLD, width=2)
        put(d, (x + 21, top + 19), chapter, 15, (244, 218, 151), True, 0)
        d.rectangle((x + 11, top + 58, x + 226, top + 285), fill=(*tone, 190))
        put(d, (x + 20, top + 70), title, 18, (255, 238, 190), True, 1, "title")
        words = desc.split()
        lines, line = [], ""
        for word in words:
            trial = (line + " " + word).strip()
            if d.textbbox((0, 0), trial, font=f(17, family="body"))[2] > 190:
                lines.append(line)
                line = word
            else:
                line = trial
        if line:
            lines.append(line)
        for j, text in enumerate(lines):
            put(d, (x + 20, top + 124 + j * 24), text, 17, (248, 237, 215), False, 1, "body")
        put(d, (x + 20, top + 303), status, 12, (255, 224, 155), True, 1)
    put(d, (190, 692), "ESC · НАЗАД", 13, (91, 70, 42), True, 1)
    d.rectangle((1273, 675, 1410, 719), fill=(56, 39, 25), outline=(125, 86, 44), width=2)
    put(d, (1300, 688), "НАЗАД", 14, CREAM, True, 1)
    return im.convert("RGB")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    sprite_dir = OUT
    for _, _, _, _, _, art_path in CAMPAIGNS:
        art = campaign_sprite(art_path)
        art.save(sprite_dir / Path(art_path).name)
    map_base = base_map()
    splash_base = base_splash()
    map_menu = menu(map_base.copy(), True)
    splash_menu = menu(splash_base.copy(), False)
    map_select = campaigns(map_base.copy())
    splash_select = campaigns(base_splash(with_hero=False))
    map_menu.save(OUT / "main-menu-map.png")
    splash_menu.save(OUT / "main-menu-splash.png")
    map_select.save(OUT / "campaign-select-map.png")
    splash_select.save(OUT / "campaign-select-splash.png")
    for name, left, right in (("main-menu-options.png", map_menu, splash_menu), ("campaign-select-options.png", map_select, splash_select)):
        sheet = Image.new("RGB", (1940, 590), (24, 17, 12))
        dd = ImageDraw.Draw(sheet)
        put(dd, (20, 11), "КАРТА И КНОПКИ", 14, GOLD, True, 1)
        put(dd, (986, 11), "АТМОСФЕРНАЯ ЗАСТАВКА", 14, GOLD, True, 1)
        left = left.resize((950, 534), Image.Resampling.LANCZOS)
        right = right.resize((950, 534), Image.Resampling.LANCZOS)
        sheet.paste(left, (10, 42))
        sheet.paste(right, (980, 42))
        sheet.save(OUT / name)
