using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;

try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--allow-opaque" or "--components") { options[args[i]] = "true"; continue; }
        if (!args[i].StartsWith("--") || i + 1 >= args.Length) throw new ArgumentException("Use --name value pairs.");
        options.Add(args[i], args[++i]);
    }
    PathSafety.Validate(options);
    if (options.ContainsKey("--components")) return ComponentExporter.Run(options);
    string Required(string key) => options.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Missing {key}");
    int Number(string key, int fallback) => options.TryGetValue(key, out var value) ? int.Parse(value) : fallback;
    var sourcePath = Path.GetFullPath(Required("--source"));
    var outputPath = Path.GetFullPath(Required("--output"));
    var id = Required("--id");
    var kind = Required("--kind");
    if (kind is not ("hero" or "unit" or "support")) throw new ArgumentException("--kind must be hero, unit, or support.");
    if (sourcePath.Equals(outputPath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Source and output must differ.");
    var columns = kind == "hero" ? 6 : 4;
    var rows = kind == "hero" ? 9 : kind == "support" ? 4 : 2;
    var frameMs = Number("--frame-ms", kind == "hero" ? 110 : 100);
    var impactFrame = Number("--impact-frame", 4);
    var box = (options.GetValueOrDefault("--content-box") ?? "8,8,48,64").Split(',').Select(int.Parse).ToArray();
    if (box.Length != 4 || box[0] < 0 || box[1] < 0 || box[2] < 1 || box[3] < 1 || box[0] + box[2] > 64 || box[1] + box[3] > 80)
        throw new ArgumentException("--content-box must be x,y,width,height inside the 64×80 frame.");
    int? alphaCutoff = options.ContainsKey("--alpha-cutoff") ? Number("--alpha-cutoff", 128) : null;
    if (alphaCutoff is < 1 or > 255) throw new ArgumentException("--alpha-cutoff must be 1–255.");
    int? alignBottom = options.ContainsKey("--align-bottom") ? Number("--align-bottom", 72) : null;
    if (alignBottom is < 1 or > 80) throw new ArgumentException("--align-bottom must be 1–80.");
    if (frameMs < 1 || impactFrame < 0 || impactFrame > 7) throw new ArgumentException("Invalid duration or zero-based impact frame (0–7).");
    using var source = new Bitmap(sourcePath);
    if (source.Width < columns || source.Height < rows) throw new ArgumentException("Source is smaller than its grid.");
    long transparent = 0, partial = 0, opaque = 0;
    for (var y = 0; y < source.Height; y++)
    for (var x = 0; x < source.Width; x++)
    {
        var alpha = source.GetPixel(x, y).A;
        if (alpha == 0) transparent++; else if (alpha == 255) opaque++; else partial++;
    }
    Console.WriteLine($"Source {source.Width}×{source.Height}: transparent={transparent}, partial={partial}, opaque={opaque}");
    if (transparent == 0 && !options.ContainsKey("--allow-opaque"))
        throw new InvalidOperationException("No fully transparent pixels. Possible baked background/fake alpha: inspect source. No chroma key applied. --allow-opaque permits an explicitly opaque review export.");
    var overrides = new Dictionary<int, RowSource>();
    if (options.TryGetValue("--row-map", out var mapPath))
    {
        using var map = JsonDocument.Parse(File.ReadAllText(mapPath));
        foreach (var entry in map.RootElement.EnumerateArray())
        {
            var file = entry.GetProperty("source").GetString()!;
            var path = Path.GetFullPath(file, Path.GetDirectoryName(Path.GetFullPath(mapPath))!);
            var targetRow = entry.GetProperty("targetRow").GetInt32();
            var sourceRow = entry.GetProperty("sourceRow").GetInt32();
            var sourceColumns = entry.TryGetProperty("sourceColumns", out var cols) ? cols.GetInt32() : columns;
            var sourceRows = entry.TryGetProperty("sourceRows", out var rs) ? rs.GetInt32() : rows;
            if (targetRow < 0 || targetRow >= rows || sourceRow < 0 || sourceRow >= sourceRows || sourceColumns != columns || sourceRows < 1)
                throw new ArgumentException("Invalid --row-map grid/row; sourceColumns must equal destination columns.");
            var bitmap = new Bitmap(path);
            var hasTransparentPixel = false;
            for (var sy = 0; sy < bitmap.Height && !hasTransparentPixel; sy++)
            for (var sx = 0; sx < bitmap.Width && !hasTransparentPixel; sx++)
                hasTransparentPixel = bitmap.GetPixel(sx, sy).A == 0;
            if (!hasTransparentPixel && !options.ContainsKey("--allow-opaque"))
                throw new InvalidOperationException($"Row source {path} has no transparent pixels; inspect fake alpha.");
            overrides.Add(targetRow, new RowSource(bitmap, path, sourceRow, sourceColumns, sourceRows));
        }
    }
    const int cellWidth = 64, cellHeight = 80;
    var sourceCellWidth = source.Width / (double)columns;
    var sourceCellHeight = source.Height / (double)rows;
    var scale = Math.Min(box[2] / sourceCellWidth, box[3] / sourceCellHeight);
    var offsetX = box[0] + (box[2] - sourceCellWidth * scale) / 2;
    var offsetY = box[1] + box[3] - sourceCellHeight * scale;
    using var output = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format32bppArgb);
    // One common transform for every proportional cell; sample centers with nearest neighbor.
    // No silhouette detection, chroma key, smoothing, or frame-specific cropping.
    for (var row = 0; row < rows; row++)
    for (var col = 0; col < columns; col++)
    for (var y = 0; y < cellHeight; y++)
    for (var x = 0; x < cellWidth; x++)
    {
        var rowSource = overrides.GetValueOrDefault(row);
        var bitmap = rowSource?.Image ?? source;
        var rowCellWidth = rowSource is null ? sourceCellWidth : bitmap.Width / (double)rowSource.Columns;
        var rowCellHeight = rowSource is null ? sourceCellHeight : bitmap.Height / (double)rowSource.Rows;
        var rowScale = Math.Min(box[2] / rowCellWidth, box[3] / rowCellHeight);
        var sampleX = (x + .5 - box[0] - (box[2] - rowCellWidth * rowScale) / 2) / rowScale;
        var sampleY = (y + .5 - box[1] - box[3] + rowCellHeight * rowScale) / rowScale;
        if (sampleX < 0 || sampleY < 0 || sampleX >= rowCellWidth || sampleY >= rowCellHeight) continue;
        var sx = Math.Clamp((int)Math.Floor(col * rowCellWidth + sampleX), 0, bitmap.Width - 1);
        var sy = Math.Clamp((int)Math.Floor((rowSource?.Row ?? row) * rowCellHeight + sampleY), 0, bitmap.Height - 1);
        var pixel = bitmap.GetPixel(sx, sy);
        if (alphaCutoff is int cutoff) pixel = Color.FromArgb(pixel.A < cutoff ? 0 : 255, pixel.R, pixel.G, pixel.B);
        output.SetPixel(col * cellWidth + x, row * cellHeight + y, pixel);
    }
    var sourceBorderDiagnostics = Enumerable.Range(0, columns * rows).Select(frame =>
    {
        var mapped = overrides.GetValueOrDefault(frame / columns);
        var bitmap = mapped?.Image ?? source;
        var cw = bitmap.Width / (double)(mapped?.Columns ?? columns);
        var ch = bitmap.Height / (double)(mapped?.Rows ?? rows);
        var sr = mapped?.Row ?? frame / columns;
        var x0 = (int)Math.Ceiling(frame % columns * cw); var x1 = (int)Math.Ceiling((frame % columns + 1) * cw) - 1;
        var y0 = (int)Math.Ceiling(sr * ch); var y1 = (int)Math.Ceiling((sr + 1) * ch) - 1;
        int left = 0, right = 0, top = 0, bottom = 0;
        bool Visible(int x, int y) => bitmap.GetPixel(x, y).A >= (alphaCutoff ?? 1);
        for (var y = y0; y <= y1; y++) { if (Visible(x0, y)) left++; if (Visible(x1, y)) right++; }
        for (var x = x0; x <= x1; x++) { if (Visible(x, y0)) top++; if (Visible(x, y1)) bottom++; }
        return new { frame, left, right, top, bottom, total = left + right + top + bottom };
    }).ToArray();
    var sourceSuspects = sourceBorderDiagnostics.Where(frame => frame.total > 0).ToArray();
    if (sourceSuspects.Length > 0)
        Console.WriteLine($"SOURCE BORDER REVIEW: {string.Join(", ", sourceSuspects.Select(frame => frame.frame))}; effects/art may cross a source-cell boundary. No source repair applied.");
    var registration = Enumerable.Range(0, columns * rows).Select(frame =>
    {
        var colors = new Color[cellWidth, cellHeight]; var bottom = 0;
        for (var y = 0; y < cellHeight; y++)
        for (var x = 0; x < cellWidth; x++)
        {
            colors[x, y] = output.GetPixel(frame % columns * cellWidth + x, frame / columns * cellHeight + y);
            if (colors[x, y].A > 0) bottom = Math.Max(bottom, y + 1);
        }
        var dy = alignBottom.HasValue && bottom > 0 ? alignBottom.Value - bottom : 0;
        var clippedOpaquePixels = 0;
        if (dy != 0)
        {
            for (var y = 0; y < cellHeight; y++)
            for (var x = 0; x < cellWidth; x++)
            {
                if (colors[x, y].A > 0 && (y + dy < 0 || y + dy >= cellHeight)) clippedOpaquePixels++;
                output.SetPixel(frame % columns * cellWidth + x, frame / columns * cellHeight + y,
                    y - dy >= 0 && y - dy < cellHeight ? colors[x, y - dy] : Color.Transparent);
            }
        }
        return new { frame, translationY = dy, originalBottomExclusive = bottom, registeredBottomExclusive = bottom == 0 ? 0 : bottom + dy, clippedOpaquePixels };
    }).ToArray();
    if (registration.Any(frame => frame.clippedOpaquePixels > 0))
        Console.WriteLine($"TRANSLATED CLIPPING REVIEW: {string.Join(", ", registration.Where(frame => frame.clippedOpaquePixels > 0).Select(frame => frame.frame))}");
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    output.Save(outputPath, ImageFormat.Png);
    string? sourceCopy = null;
    if (options.TryGetValue("--source-copy", out var copy))
    {
        sourceCopy = Path.GetFullPath(copy);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceCopy)!);
        if (!sourceCopy.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) File.Copy(sourcePath, sourceCopy, true);
    }
    object Clip(string name, int first, int count, bool loop, int? impact) => new
    { name, frames = Enumerable.Range(first, count).ToArray(), frameMs, loop, impactFrame = impact };
    var frameDiagnostics = Enumerable.Range(0, columns * rows).Select(frame =>
    {
        var left = cellWidth; var top = cellHeight; var right = -1; var bottom = -1; var edgePixels = 0;
        for (var y = 0; y < cellHeight; y++)
        for (var x = 0; x < cellWidth; x++)
        {
            if (output.GetPixel(frame % columns * cellWidth + x, frame / columns * cellHeight + y).A == 0) continue;
            left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            if (x == 0 || x == cellWidth - 1 || y == 0 || y == cellHeight - 1) edgePixels++;
        }
        return new { frame, empty = right < 0, visibleBounds = new { left, top, right, bottom }, edgePixels };
    }).ToArray();
    var suspectFrames = frameDiagnostics.Where(frame => frame.empty || frame.edgePixels > 0).ToArray();
    if (suspectFrames.Length > 0)
        Console.WriteLine($"REVIEW: {suspectFrames.Length} empty or edge-touching frames: {string.Join(", ", suspectFrames.Select(frame => frame.frame))}. Check source boundaries/clipping; no pixels were repaired.");
    var clips = kind == "hero"
        ? new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "IdleSE" }
            .Select((direction, row) => Clip(row == 8 ? "idle.SE" : "walk." + direction, row * 6, 6, true, null)).ToArray()
        : kind == "support" ? new[] { Clip("attack", 0, 8, false, impactFrame), Clip(options.GetValueOrDefault("--ability-name") ?? "ability", 8, 8, false, impactFrame) }
        : new[] { Clip("attack", 0, 8, false, impactFrame) };
    var metadata = new
    {
        schemaVersion = 1, id, image = Path.GetFileName(outputPath), kind,
        width = output.Width, height = output.Height, columns, rows,
        cell = new { width = cellWidth, height = cellHeight }, anchor = new { x = 32, y = 72 },
        contentBox = new { x = box[0], y = box[1], width = box[2], height = box[3], horizontalAlignment = "center", verticalAlignment = "bottom" },
        alphaCutoff, alignBottom,
        frameOrder = "row-major", clips, frameDiagnostics, sourceBorderDiagnostics, registration,
        rowOverrides = overrides.Select(pair => new
        {
            targetRow = pair.Key, sourceRow = pair.Value.Row,
            source = Path.GetRelativePath(Path.GetDirectoryName(outputPath)!, pair.Value.Path).Replace('\\', '/'),
            sourceWidth = pair.Value.Image.Width, sourceHeight = pair.Value.Image.Height,
            sourceColumns = pair.Value.Columns, sourceRows = pair.Value.Rows,
            scale = Math.Min(box[2] / (pair.Value.Image.Width / (double)pair.Value.Columns), box[3] / (pair.Value.Image.Height / (double)pair.Value.Rows)),
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Value.Path))).ToLowerInvariant()
        }).ToArray(),
        source = new
        {
            image = Path.GetRelativePath(Path.GetDirectoryName(outputPath)!, sourceCopy ?? sourcePath).Replace('\\', '/'),
            width = source.Width, height = source.Height,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant(),
            transparentPixels = transparent, partialAlphaPixels = partial, opaquePixels = opaque,
            export = "proportional grid cells; fixed asset content box; uniform contain scale; centered horizontally/bottom aligned; nearest-neighbor; no frame-specific crop; optional alpha-only binary threshold",
            scale, offset = new { x = offsetX, y = offsetY },
            cellBounds = Enumerable.Range(0, columns * rows).Select(index => new
            {
                frame = index, x = index % columns * sourceCellWidth, y = index / columns * sourceCellHeight,
                width = sourceCellWidth, height = sourceCellHeight
            }).ToArray()
        }
    };
    File.WriteAllText(Path.ChangeExtension(outputPath, ".json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    if (options.TryGetValue("--contact-strip", out var stripPath))
    {
        using var contact = new Bitmap(columns * 128, rows * 184, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(contact);
        graphics.Clear(Color.FromArgb(35, 43, 37));
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var font = new Font(FontFamily.GenericMonospace, 8, FontStyle.Regular, GraphicsUnit.Pixel);
        using var border = new Pen(Color.FromArgb(110, 130, 110));
        using var baseline = new Pen(Color.FromArgb(180, 80, 80));
        for (var frame = 0; frame < columns * rows; frame++)
        {
            var dx = frame % columns * 128; var dy = frame / columns * 184;
            graphics.DrawImage(output, new Rectangle(dx, dy, 128, 160), new Rectangle(frame % columns * 64, frame / columns * 80, 64, 80), GraphicsUnit.Pixel);
            graphics.DrawRectangle(border, dx, dy, 127, 159);
            graphics.DrawLine(baseline, dx, dy + 144, dx + 127, dy + 144);
            graphics.DrawString($"{frame} dy{registration[frame].translationY:+0;-0;0}" + (sourceBorderDiagnostics[frame].total > 0 ? " SRC!" : ""), font, Brushes.White, dx + 3, dy + 164);
        }
        var fullStripPath = Path.GetFullPath(stripPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullStripPath)!);
        contact.Save(fullStripPath, ImageFormat.Png);
    }
    foreach (var rowSource in overrides.Values) rowSource.Image.Dispose();
    Console.WriteLine($"Exported {outputPath} ({output.Width}×{output.Height}), {clips.Length} clips, anchor (32,72).");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Usage: dotnet run --project tools/SpriteSheetTool -- --source source.png --output content/art/animations/name.png --kind hero|unit|support --id name [--source-copy docs/art/sources/name.png] [--row-map rows.json] [--anchor-mode body|cell] [--content-box 8,8,48,64] [--alpha-cutoff 128] [--align-bottom 72] [--contact-strip review.png] [--ability-name heal] [--frame-ms 110] [--impact-frame 4] [--allow-opaque]");
    return 1;
}
return 0;

sealed record RowSource(Bitmap Image, string Path, int Row, int Columns, int Rows);
