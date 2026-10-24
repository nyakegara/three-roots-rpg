using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

// Builds the protagonists' walk atlases from separately drawn contact poses.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
foreach (var hero in new[] { (Name: "edric", Faction: "human"), (Name: "morven", Faction: "undead") })
    Build(hero.Name, hero.Faction);

void Build(string name, string faction)
{
    var sourceDir = Path.Combine(root, "docs", "art", "heroes", "sources");
    using var idleSource = new Bitmap(Path.Combine(sourceDir, $"{name}.front.png"));
    using var frontSource = new Bitmap(Path.Combine(sourceDir, $"{name}.walk-front.png"));
    using var backSource = new Bitmap(Path.Combine(sourceDir, $"{name}.walk-back.png"));
    using var northEastSource = new Bitmap(Path.Combine(sourceDir, $"{name}.walk-ne.png"));
    using var eastSource = new Bitmap(Path.Combine(sourceDir, $"{name}.walk-east.png"));
    using var southSource = new Bitmap(Path.Combine(sourceDir, $"{name}.walk-south.png"));
    var front = ExtractStrip(frontSource, 3);
    var back = ExtractStrip(backSource, 3);
    var northEast = ExtractStrip(northEastSource, 3);
    var east = ExtractStrip(eastSource, 3);
    var south = ExtractStrip(southSource, 3);
    using var idle = RenderIdle(idleSource);
    using var atlas = new Bitmap(256, 720, PixelFormat.Format32bppArgb);
    var rows = new[]
    {
        (Frames: back, Mirror: false),  // N
        (Frames: northEast, Mirror: false),  // NE
        (Frames: east, Mirror: false),// E
        (Frames: front, Mirror: false),// SE
        (Frames: south, Mirror: false),// S
        (Frames: front, Mirror: true), // SW
        (Frames: east, Mirror: true), // W
        (Frames: northEast, Mirror: true)   // NW
    };
    using (var draw = Graphics.FromImage(atlas))
    {
        draw.CompositingMode = CompositingMode.SourceCopy;
        for (var row = 0; row < 8; row++)
        for (var column = 0; column < 4; column++)
        {
            var poses = rows[row].Frames;
            var index = row is 0 or 4 ? (column % 2) * 2 : new[] { 0, 1, 2, 1 }[column];
            using var frame = rows[row].Mirror ? Flip(poses[index]) : (Bitmap)poses[index].Clone();
            draw.DrawImageUnscaled(frame, column * 64, row * 80);
        }
        for (var column = 0; column < 4; column++) draw.DrawImageUnscaled(idle, column * 64, 640);
    }
    foreach (var frame in front.Concat(back).Concat(northEast).Concat(east).Concat(south)) frame.Dispose();
    var artDir = Path.Combine(root, "content", "art");
    var output = Path.Combine(artDir, "animations", $"hero.{faction}.walk.png");
    atlas.Save(output, ImageFormat.Png);
    SaveCropped(idleSource, Path.Combine(artDir, $"hero.{faction}.png"));
    SavePortrait(idleSource, name, Path.Combine(artDir, $"portrait.{faction}.png"));
    SaveMetadata(output, faction);
    SaveContact(atlas, Path.Combine(root, "docs", "art", "heroes", $"hero.{faction}.contact.png"));
    Console.WriteLine($"{name}: {output} (256x720, 29 used frames)");
}

static Bitmap[] ExtractStrip(Bitmap source, int columns)
{
    var cells = Enumerable.Range(0, 3).Select(i => new Rectangle(i * source.Width / columns, 0,
        (i + 1) * source.Width / columns - i * source.Width / columns, source.Height)).ToArray();
    var visible = cells.Select(cell => AlphaBounds(source, cell)).ToArray();
    var maxHeight = visible.Max(box => box.Height);
    var maxWidth = visible.Max(box => box.Width);
    if (maxHeight < 100) throw new InvalidOperationException("Walk source contains an empty or tiny figure.");
    var scale = Math.Min(56.0 / maxHeight, 58.0 / maxWidth);
    var pivot = visible.Select((box, i) => UpperBodyCenter(source, cells[i], box) - cells[i].X).Average();
    var ground = visible.Max(box => box.Bottom);
    var result = new Bitmap[3];
    for (var i = 0; i < 3; i++)
    {
        var cell = cells[i];
        var target = new Bitmap(64, 80, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(target))
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            var destination = new Rectangle((int)Math.Round(32 - pivot * scale),
                (int)Math.Round(72 - ground * scale),
                (int)Math.Round(cell.Width * scale), (int)Math.Round(cell.Height * scale));
            graphics.DrawImage(source, destination, cell, GraphicsUnit.Pixel);
        }
        HardAlpha(target);
        RemoveSmallIslands(target);
        result[i] = target;
    }
    return result;
}

static Bitmap RenderIdle(Bitmap source)
{
    var box = AlphaBounds(source, new Rectangle(0, 0, source.Width, source.Height));
    var scale = Math.Min(56.0 / box.Height, 58.0 / box.Width);
    var target = new Bitmap(64, 80, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(target))
    {
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(source, new Rectangle((int)Math.Round(32 - box.Width * scale / 2),
            (int)Math.Round(72 - box.Height * scale),
            (int)Math.Round(box.Width * scale), (int)Math.Round(box.Height * scale)), box, GraphicsUnit.Pixel);
    }
    HardAlpha(target);
    return target;
}

static double UpperBodyCenter(Bitmap image, Rectangle cell, Rectangle visible)
{
    var top = visible.Top + visible.Height / 5;
    var bottom = visible.Top + visible.Height * 11 / 20;
    var left = visible.Right;
    var right = visible.Left;
    for (var y = top; y < bottom; y += 3)
    for (var x = cell.Left; x < cell.Right; x += 3)
        if (image.GetPixel(x, y).A >= 128) { left = Math.Min(left, x); right = Math.Max(right, x); }
    return right >= left ? (left + right) / 2.0 : (visible.Left + visible.Right) / 2.0;
}

static Rectangle AlphaBounds(Bitmap image, Rectangle region)
{
    var left = region.Right;
    var top = region.Bottom;
    var right = region.Left - 1;
    var bottom = region.Top - 1;
    for (var y = region.Top; y < region.Bottom; y++)
    for (var x = region.Left; x < region.Right; x++)
    {
        if (image.GetPixel(x, y).A < 128) continue;
        left = Math.Min(left, x); top = Math.Min(top, y);
        right = Math.Max(right, x); bottom = Math.Max(bottom, y);
    }
    if (right < left) throw new InvalidOperationException("Transparent source frame.");
    return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
}

static void HardAlpha(Bitmap image)
{
    for (var y = 0; y < image.Height; y++)
    for (var x = 0; x < image.Width; x++)
    {
        var color = image.GetPixel(x, y);
        image.SetPixel(x, y, color.A >= 128 ? Color.FromArgb(255, color.R, color.G, color.B) : Color.Transparent);
    }
}

static void RemoveSmallIslands(Bitmap image)
{
    // Generated strips can contain a few pixels of the neighbouring pose at a cell edge.
    // Keep connected costume/prop shapes, but discard tiny detached fragments.
    var seen = new bool[image.Width * image.Height];
    for (var y = 0; y < image.Height; y++)
    for (var x = 0; x < image.Width; x++)
    {
        var start = y * image.Width + x;
        if (seen[start] || image.GetPixel(x, y).A == 0) continue;
        var pixels = new List<Point>();
        var pending = new Queue<Point>();
        pending.Enqueue(new Point(x, y));
        seen[start] = true;
        while (pending.Count > 0)
        {
            var point = pending.Dequeue();
            pixels.Add(point);
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = point.X + dx;
                var ny = point.Y + dy;
                if (nx < 0 || ny < 0 || nx >= image.Width || ny >= image.Height) continue;
                var index = ny * image.Width + nx;
                if (seen[index] || image.GetPixel(nx, ny).A == 0) continue;
                seen[index] = true;
                pending.Enqueue(new Point(nx, ny));
            }
        }
        if (pixels.Count >= 40) continue;
        foreach (var point in pixels) image.SetPixel(point.X, point.Y, Color.Transparent);
    }
}

static Bitmap Flip(Bitmap original)
{
    var clone = (Bitmap)original.Clone();
    clone.RotateFlip(RotateFlipType.RotateNoneFlipX);
    return clone;
}

static void SaveCropped(Bitmap source, string output)
{
    var visible = AlphaBounds(source, new Rectangle(0, 0, source.Width, source.Height));
    var pad = Math.Max(4, visible.Height / 60);
    visible.Inflate(pad, pad);
    visible.Intersect(new Rectangle(0, 0, source.Width, source.Height));
    using var cropped = source.Clone(visible, PixelFormat.Format32bppArgb);
    cropped.Save(output, ImageFormat.Png);
}

static void SavePortrait(Bitmap source, string name, string output)
{
    var visible = AlphaBounds(source, new Rectangle(0, 0, source.Width, source.Height));
    // Morven's staff begins above his hood; frame the face and shoulders.
    var region = name == "morven"
        ? new Rectangle(visible.Left + visible.Width / 3, visible.Top + visible.Height / 8,
            visible.Width * 2 / 3, visible.Height / 2)
        : new Rectangle(visible.Left + visible.Width / 5, visible.Top,
            visible.Width * 3 / 5, visible.Height * 3 / 5);
    region.Intersect(new Rectangle(0, 0, source.Width, source.Height));
    using var cropped = source.Clone(region, PixelFormat.Format32bppArgb);
    cropped.Save(output, ImageFormat.Png);
}

static void SaveMetadata(string output, string faction)
{
    var names = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    var clips = names.Select((direction, row) => new
    {
        name = $"walk.{direction}",
        frames = row is 0 or 4 ? new[] { row * 4, row * 4 + 1 } : Enumerable.Range(row * 4, 4).ToArray(),
        frameMs = row is 0 or 4 ? 220 : 110,
        loop = true,
        impactFrame = (int?)null
    }).Concat(new[] { new { name = "idle.SE", frames = new[] { 32 }, frameMs = 110,
        loop = false, impactFrame = (int?)null } });
    var data = new
    {
        schemaVersion = 2, id = $"hero.{faction}.walk", image = Path.GetFileName(output), kind = "hero",
        width = 256, height = 720, columns = 4, rows = 9,
        cell = new { width = 64, height = 80 }, anchor = new { x = 32, y = 72 },
        frameOrder = "row-major", heroCompactWalk = true, usedFrameCount = 29,
        unusedSlots = new[] { 2, 3, 18, 19, 33, 34, 35 }, clips
    };
    File.WriteAllText(Path.ChangeExtension(output, ".json"),
        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
}

static void SaveContact(Bitmap atlas, string output)
{
    using var contact = new Bitmap(512, 9 * 170, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(contact);
    graphics.Clear(Color.FromArgb(29, 38, 35));
    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
    using var border = new Pen(Color.FromArgb(148, 104, 65));
    using var ground = new Pen(Color.FromArgb(222, 130, 99));
    for (var index = 0; index < 36; index++)
    {
        var x = index % 4 * 128;
        var y = index / 4 * 170;
        graphics.DrawImage(atlas, new Rectangle(x, y, 128, 160),
            new Rectangle(index % 4 * 64, index / 4 * 80, 64, 80), GraphicsUnit.Pixel);
        graphics.DrawLine(ground, x, y + 144, x + 127, y + 144);
        graphics.DrawRectangle(border, x, y, 127, 159);
    }
    contact.Save(output, ImageFormat.Png);
}
