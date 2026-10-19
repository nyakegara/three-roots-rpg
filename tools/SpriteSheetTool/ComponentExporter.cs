using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

static class ComponentExporter
{
    public static int Run(Dictionary<string, string> options)
    {
        var sourcePath = Path.GetFullPath(options["--source"]);
        var outputPath = Path.GetFullPath(options["--output"]);
        if (sourcePath.Equals(outputPath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Source and output must differ.");
        var kind = options["--kind"]; var id = options["--id"];
        var anchorMode = options.GetValueOrDefault("--anchor-mode") ?? "body";
        if (anchorMode is not ("body" or "cell")) throw new ArgumentException("--anchor-mode must be body or cell.");
        if (kind is not ("hero" or "unit" or "support")) throw new ArgumentException("Invalid kind.");
        var columns = kind == "hero" ? 6 : 4; var rows = kind == "hero" ? 9 : kind == "support" ? 4 : 2;
        var cutoff = int.Parse(options.GetValueOrDefault("--alpha-cutoff") ?? "128");
        if (cutoff is < 1 or > 255) throw new ArgumentException("Component alpha cutoff must be1–255.");
        var box = (options.GetValueOrDefault("--content-box") ?? "8,8,48,64").Split(',').Select(int.Parse).ToArray();
        if (box.Length != 4 || box[0] < 0 || box[1] < 0 || box[2] < 1 || box[3] < 1 || box[0] + box[2] > 64 || box[1] + box[3] > 80 || box[0] >= 32 || box[0] + box[2] <= 32)
            throw new ArgumentException("Invalid component content box around anchor32.");
        var baseline = int.Parse(options.GetValueOrDefault("--align-bottom") ?? "72");
        if (baseline is < 1 or > 80 || baseline <= box[1]) throw new ArgumentException("Invalid baseline.");
        var frameMs = int.Parse(options.GetValueOrDefault("--frame-ms") ?? (kind == "hero" ? "110" : "100"));
        var impact = int.Parse(options.GetValueOrDefault("--impact-frame") ?? "4");
        if (frameMs < 1 || impact is < 0 or > 7) throw new ArgumentException("Invalid clip timing.");
        var atlases = new Dictionary<string, Atlas>(StringComparer.OrdinalIgnoreCase);
        Atlas Load(string path, int cols, int rs)
        {
            path = Path.GetFullPath(path);
            if (!atlases.TryGetValue(path, out var atlas)) atlases.Add(path, atlas = new Atlas(path, cols, rs, cutoff, box, baseline, anchorMode));
            else if (atlas.Columns != cols || atlas.Rows != rs) throw new ArgumentException("A source path cannot have multiple grid definitions.");
            return atlas;
        }
        var primary = Load(sourcePath, columns, rows);
        var bindings = Enumerable.Range(0, rows).Select(row => new Binding(primary, row)).ToArray();
        var requestedScales = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (options.TryGetValue("--row-map", out var mapPath))
        {
            using var map = JsonDocument.Parse(File.ReadAllText(mapPath));
            var mappedRows = new HashSet<int>();
            foreach (var entry in map.RootElement.EnumerateArray())
            {
                var target = entry.GetProperty("targetRow").GetInt32(); var row = entry.GetProperty("sourceRow").GetInt32();
                var cols = entry.TryGetProperty("sourceColumns", out var c) ? c.GetInt32() : columns;
                var rs = entry.TryGetProperty("sourceRows", out var r) ? r.GetInt32() : rows;
                if (target < 0 || target >= rows || row < 0 || row >= rs || cols != columns || !mappedRows.Add(target)) throw new ArgumentException("Invalid component row map.");
                var path = Path.GetFullPath(entry.GetProperty("source").GetString()!, Path.GetDirectoryName(Path.GetFullPath(mapPath))!);
                var atlas = Load(path, cols, rs);
                if (entry.TryGetProperty("sourceScale", out var requestedScale))
                {
                    var value = requestedScale.GetDouble();
                    if (!double.IsFinite(value) || value <= 0 || value > atlas.FitScale)
                        throw new ArgumentException($"sourceScale must be positive and no larger than safe fit scale{atlas.FitScale:R}: {path}");
                    if (requestedScales.TryGetValue(path, out var previous) && previous != value)
                        throw new ArgumentException($"Conflicting sourceScale values for {path}; scaling must be constant per source.");
                    requestedScales[path] = value; atlas.Scale = value;
                }
                bindings[target] = new Binding(atlas, row);
            }
        }
        using var output = new Bitmap(columns * 64, rows * 80, PixelFormat.Format32bppArgb);
        var frameRecords = new List<object>(); var registration = new List<object>(); var diagnostics = new List<object>();
        var stripLabels = new List<string>();
        for (var frame = 0; frame < columns * rows; frame++)
        {
            var binding = bindings[frame / columns]; var atlas = binding.Atlas;
            var group = atlas.Groups[binding.SourceRow * columns + frame % columns]; var main = group.Main;
            var sampled = new Color[64, 80]; var primaryBottom = 0; var sampledBodyPixels = 0;
            for (var y = 0; y < 80; y++)
            for (var x = 0; x < 64; x++)
            {
                var sx = (int)Math.Floor(group.PivotX + (x + .5 - 32) / atlas.Scale);
                var sy = (int)Math.Floor(group.PivotY + (y + .5 - baseline) / atlas.Scale);
                if (sx < 0 || sy < 0 || sx >= atlas.Width || sy >= atlas.Height) continue;
                var label = atlas.Labels[sy * atlas.Width + sx];
                if (!group.Ids.Contains(label)) continue;
                var source = atlas.Pixel(sx, sy); sampled[x, y] = Color.FromArgb(255, source.R, source.G, source.B);
                if (label == main.Id) { primaryBottom = Math.Max(primaryBottom, y + 1); sampledBodyPixels++; }
            }
            if (sampledBodyPixels == 0) throw new InvalidOperationException($"Empty sampled primary body atframe{frame}.");
            var dy = anchorMode == "cell" ? 0 : baseline - primaryBottom; var clipped = 0; var sourceClipped = 0;
            var left = 64; var top = 80; var right = -1; var bottom = -1; var edge = 0;
            for (var y = 0; y < 80; y++)
            for (var x = 0; x < 64; x++)
            {
                var pixel = sampled[x, y]; if (pixel.A == 0) continue;
                var ty = y + dy; if (ty < 0 || ty >= 80) { clipped++; continue; }
                output.SetPixel(frame % columns * 64 + x, frame / columns * 80 + ty, pixel);
                left = Math.Min(left, x); top = Math.Min(top, ty); right = Math.Max(right, x); bottom = Math.Max(bottom, ty);
                if (x is 0 or 63 || ty is 0 or 79) edge++;
            }
            for (var sy = group.Top; sy < group.Bottom; sy++)
            for (var sx = group.Left; sx < group.Right; sx++)
            {
                if (!group.Ids.Contains(atlas.Labels[sy * atlas.Width + sx])) continue;
                var tx = (int)Math.Floor(32 + (sx + .5 - group.PivotX) * atlas.Scale);
                var ty = (int)Math.Floor(baseline + (sy + .5 - group.PivotY) * atlas.Scale) + dy;
                if (tx < 0 || tx >= 64 || ty < 0 || ty >= 80) sourceClipped++;
            }
            if (clipped > 0 || sourceClipped > 0) Console.WriteLine($"CLIPPING REVIEW frame{frame}: native{clipped}, source{sourceClipped}");
            registration.Add(new { frame, translationY = dy, originalBottomExclusive = primaryBottom, registeredBottomExclusive = primaryBottom + dy, clippedOpaquePixels = clipped });
            diagnostics.Add(new { frame, empty = false, visibleBounds = new { left, top, right, bottom }, edgePixels = edge });
            frameRecords.Add(new {
                frame, source = Path.GetRelativePath(Path.GetDirectoryName(outputPath)!, atlas.Path).Replace('\\', '/'), sourceFrame = group.Frame,
                primaryComponent = main.Id, componentIds = group.Ids.Order().ToArray(),
                sourceCropBounds = new { left = group.Left, top = group.Top, right = group.Right, bottom = group.Bottom },
                primaryBounds = new { left = main.Left, top = main.Top, right = main.Right, bottom = main.Bottom },
                scale = atlas.Scale, pivotX = group.PivotX, pivotY = group.PivotY, primaryBottom = main.Bottom,
                transform = new { offsetX = 32 - group.PivotX * atlas.Scale, offsetY = baseline - group.PivotY * atlas.Scale + dy },
                nativeBounds = new { left, top, rightExclusive = right + 1, bottomExclusive = bottom + 1 },
                translatedClipping = clipped, sourcePixelsProjectedOutsideFrame = sourceClipped
            });
            stripLabels.Add($"{frame} unpack {group.Ids.Count}p");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!); output.Save(outputPath, ImageFormat.Png);
        if (options.TryGetValue("--source-copy", out var copy))
        {
            copy = Path.GetFullPath(copy); Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            if (!copy.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) File.Copy(sourcePath, copy, true);
        }
        object Clip(string name, int first, int count, bool loop, int? hit) => new { name, frames = Enumerable.Range(first, count).ToArray(), frameMs, loop, impactFrame = hit };
        var clips = kind == "hero" ? new[] {"N","NE","E","SE","S","SW","W","NW","IdleSE"}.Select((direction, row) => Clip(row == 8 ? "idle.SE" : "walk." + direction, row * 6, 6, true, null)).ToArray()
            : kind == "support" ? new[] { Clip("attack", 0, 8, false, impact), Clip(options.GetValueOrDefault("--ability-name") ?? "ability", 8, 8, false, impact) }
            : new[] { Clip("attack", 0, 8, false, impact) };
        var metadata = new {
            schemaVersion = 2, id, image = Path.GetFileName(outputPath), kind, width = output.Width, height = output.Height, columns, rows,
            cell = new { width = 64, height = 80 }, anchor = new { x = 32, y = baseline },
            contentBox = new { x = box[0], y = box[1], width = box[2], height = box[3] }, alphaCutoff = cutoff, alignBottom = baseline, anchorMode,
            frameOrder = "row-major", clips, frameDiagnostics = diagnostics, registration,
            sourceBorderDiagnostics = Array.Empty<object>(),
            unpacking = new {
                enabled = true, connectivity = 8, majorMinAreaFraction = .1, majorMinHeightFraction = .35,
                attachment = "nearest main bounding-box distance; actual boundary pixel distance breaks ties; far threshold0.3cellmax; ambiguous/far attachments reported",
                pivot = anchorMode == "cell" ? "source cell-center X; common max primary bottom across source row Y; no per-frame normalization" : "mean pixel-center X of bottom15% primary body; primary exclusive bottom to baseline",
                scale = "one constant scale per source path, fitting maximum anchored group extents into content box",
                sampling = "sourceX=floor(pivotX+(x+0.5-anchorX)/scale); sourceY=floor(pivotY+(y-translationY+0.5-anchorY)/scale); use this pivot form to reproduce floating-point sampling",
                sources = atlases.Values.Select(a => new {
                    source = Path.GetRelativePath(Path.GetDirectoryName(outputPath)!, a.Path).Replace('\\', '/'),
                    a.Width, a.Height, a.Columns, a.Rows, expected = a.Columns * a.Rows, primaryComponents = a.Groups.Count,
                    totalComponents = a.Components.Count, detachedComponents = a.Components.Count - a.Groups.Count,
                    a.Scale, a.FitScale, a.Attachments, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Path))).ToLowerInvariant()
                }).ToArray(), frames = frameRecords
            },
            source = new { image = Path.GetRelativePath(Path.GetDirectoryName(outputPath)!, options.TryGetValue("--source-copy", out var stored) ? Path.GetFullPath(stored) : sourcePath).Replace('\\', '/'), width = primary.Width, height = primary.Height, transparentPixels = primary.Transparent, partialAlphaPixels = primary.Partial, opaquePixels = primary.Opaque }
        };
        File.WriteAllText(Path.ChangeExtension(outputPath, ".json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        if (options.TryGetValue("--contact-strip", out var contactPath)) Contact(output, columns, rows, baseline, stripLabels, contactPath);
        Console.WriteLine($"Component exported {id}: {columns * rows}frames; sources{atlases.Count}; scales {string.Join(",", atlases.Values.Select(a => a.Scale.ToString("F9")))}");
        return 0;
    }

    static void Contact(Bitmap output, int columns, int rows, int baseline, List<string> labels, string path)
    {
        using var contact = new Bitmap(columns * 128, rows * 184); using var g = Graphics.FromImage(contact);
        g.Clear(Color.FromArgb(35,43,37)); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var font = new Font(FontFamily.GenericMonospace, 8, FontStyle.Regular, GraphicsUnit.Pixel); using var border = new Pen(Color.FromArgb(110,130,110)); using var line = new Pen(Color.FromArgb(180,80,80));
        for (var frame = 0; frame < columns * rows; frame++)
        {
            var x = frame % columns * 128; var y = frame / columns * 184;
            g.DrawImage(output, new Rectangle(x,y,128,160), new Rectangle(frame%columns*64,frame/columns*80,64,80), GraphicsUnit.Pixel);
            g.DrawRectangle(border,x,y,127,159); g.DrawLine(line,x,y+baseline*2,x+127,y+baseline*2); g.DrawString(labels[frame],font,Brushes.White,x+3,y+164);
        }
        path = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); contact.Save(path,ImageFormat.Png);
    }

    sealed record Binding(Atlas Atlas, int SourceRow);
    sealed record Part(int Id, int Area, int Left, int Top, int Right, int Bottom, double Cx, double Cy)
    { public int Width => Right-Left; public int Height => Bottom-Top; }
    sealed class Group(Part main, int frame, double pivotX)
    {
        public Part Main = main; public int Frame = frame; public double PivotX = pivotX; public int PivotY = main.Bottom;
        public HashSet<int> Ids = [main.Id]; public int Left = main.Left, Top = main.Top, Right = main.Right, Bottom = main.Bottom;
        public void Add(Part part) { Ids.Add(part.Id); Left=Math.Min(Left,part.Left); Top=Math.Min(Top,part.Top); Right=Math.Max(Right,part.Right); Bottom=Math.Max(Bottom,part.Bottom); }
    }
    sealed class Atlas
    {
        public string Path; public int Width,Height,Columns,Rows,Stride; public int[] Labels; public byte[] Bytes;
        public List<Part> Components=[]; public Dictionary<int,Group> Groups=[]; public List<object> Attachments=[];
        public double Scale, FitScale; public long Transparent,Partial,Opaque;
        public Color Pixel(int x,int y) { var i=y*Stride+x*4; return Color.FromArgb(Bytes[i+3],Bytes[i+2],Bytes[i+1],Bytes[i]); }
        public Atlas(string path,int columns,int rows,int cutoff,int[] box,int baseline,string anchorMode)
        {
            Path=path;Columns=columns;Rows=rows;using var image=new Bitmap(path);Width=image.Width;Height=image.Height;
            var data=image.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);Stride=data.Stride;
            Bytes=new byte[Stride*Height];Marshal.Copy(data.Scan0,Bytes,0,Bytes.Length);image.UnlockBits(data);Labels=new int[Width*Height];var queue=new int[Width*Height];
            bool Visible(int x,int y)=>Bytes[y*Stride+x*4+3]>=cutoff;
            for(var y=0;y<Height;y++)for(var x=0;x<Width;x++) { var a=Bytes[y*Stride+x*4+3];if(a==0)Transparent++;else if(a==255)Opaque++;else Partial++; }
            for(var y=0;y<Height;y++)for(var x=0;x<Width;x++)
            {
                var start=y*Width+x;if(Labels[start]!=0||!Visible(x,y))continue;
                var id=Components.Count+1;var head=0;var tail=1;queue[0]=start;Labels[start]=id;var left=x;var right=x;var top=y;var bottom=y;long sx=0,sy=0;
                while(head<tail)
                {
                    var index=queue[head++];var px=index%Width;var py=index/Width;sx+=px;sy+=py;left=Math.Min(left,px);right=Math.Max(right,px);top=Math.Min(top,py);bottom=Math.Max(bottom,py);
                    for(var ny=Math.Max(0,py-1);ny<=Math.Min(Height-1,py+1);ny++)for(var nx=Math.Max(0,px-1);nx<=Math.Min(Width-1,px+1);nx++)
                    {var next=ny*Width+nx;if(Labels[next]==0&&Visible(nx,ny)){Labels[next]=id;queue[tail++]=next;}}
                }
                Components.Add(new Part(id,tail,left,top,right+1,bottom+1,sx/(double)tail,sy/(double)tail));
            }
            var cw=Width/(double)columns;var ch=Height/(double)rows;var mains=Components.Where(c=>c.Area>=cw*ch*.1&&c.Height>=ch*.35).ToArray();
            if(mains.Length!=columns*rows)throw new InvalidOperationException($"{path}: expected{columns*rows}primary components, found{mains.Length}; no automatic split/repair performed.");
            foreach(var main in mains)
            {
                var frame=Math.Clamp((int)(main.Cy/ch),0,rows-1)*columns+Math.Clamp((int)(main.Cx/cw),0,columns-1);
                if(Groups.ContainsKey(frame))throw new InvalidOperationException($"{path}: duplicate primary assignment frame{frame}; no automatic repair.");
                double mass=0,sum=0;
                for(var y=main.Bottom-(int)Math.Ceiling(main.Height*.15);y<main.Bottom;y++)for(var x=main.Left;x<main.Right;x++)if(Labels[y*Width+x]==main.Id){mass++;sum+=x+.5;}
                Groups.Add(frame,new Group(main,frame,sum/mass));
            }
            if(anchorMode=="cell")
            {
                var grounds=Groups.Values.GroupBy(g=>g.Frame/columns).ToDictionary(row=>row.Key,row=>row.Max(g=>g.Main.Bottom));
                foreach(var group in Groups.Values){group.PivotX=(group.Frame%columns+.5)*cw;group.PivotY=grounds[group.Frame/columns];}
            }
            double BoxDistance(Part a,Part b) {var dx=Math.Max(0,Math.Max(a.Left-b.Right,b.Left-a.Right));var dy=Math.Max(0,Math.Max(a.Top-b.Bottom,b.Top-a.Bottom));return Math.Sqrt(dx*dx+dy*dy);}
            var boundaryCache=new Dictionary<int,List<Point>>();
            List<Point> Boundary(Part part)
            {
                if(boundaryCache.TryGetValue(part.Id,out var result))return result;
                result=[];for(var y=part.Top;y<part.Bottom;y++)for(var x=part.Left;x<part.Right;x++)
                    if(Labels[y*Width+x]==part.Id&&(x==0||y==0||x==Width-1||y==Height-1||Labels[y*Width+x-1]!=part.Id||Labels[y*Width+x+1]!=part.Id||Labels[(y-1)*Width+x]!=part.Id||Labels[(y+1)*Width+x]!=part.Id))result.Add(new Point(x,y));
                boundaryCache.Add(part.Id,result);return result;
            }
            double PixelDistance(Part a,Part b) {var min=double.MaxValue;foreach(var p in Boundary(a))foreach(var q in Boundary(b)){var dx=p.X-q.X;var dy=p.Y-q.Y;min=Math.Min(min,dx*dx+dy*dy);}return Math.Sqrt(min);}
            foreach(var small in Components.Except(mains))
            {
                var ranked=Groups.Values.Select(g=>new {group=g,distance=BoxDistance(g.Main,small)}).OrderBy(c=>c.distance).ToArray();
                var tie=ranked.Where(c=>Math.Abs(c.distance-ranked[0].distance)<.00001).ToArray();
                var chosen=tie.Length==1?tie[0]:tie.OrderBy(c=>PixelDistance(c.group.Main,small)).First();
                var far=chosen.distance>.3*Math.Max(cw,ch);var ambiguity=false;
                if(tie.Length>1){var ds=tie.Select(c=>PixelDistance(c.group.Main,small)).Order().ToArray();ambiguity=ds[1]-ds[0]<4;}
                if(!far)chosen.group.Add(small);
                Attachments.Add(new {component=small.Id,small.Area,bounds=new{small.Left,small.Top,small.Right,small.Bottom},frame=chosen.group.Frame,bboxDistance=chosen.distance,ambiguous=ambiguity,far,assigned=!far});
                if(far||ambiguity)Console.WriteLine($"ATTACHMENT REVIEW {System.IO.Path.GetFileName(path)}: component{small.Id}, frame{chosen.group.Frame}, far={far}, ambiguous={ambiguity}");
            }
            var maxLeft=Groups.Values.Max(g=>g.PivotX-g.Left);var maxRight=Groups.Values.Max(g=>g.Right-g.PivotX);var maxAbove=Groups.Values.Max(g=>g.PivotY-g.Top);var maxBelow=Groups.Values.Max(g=>g.Bottom-g.PivotY);
            Scale=Math.Min(Math.Min((32-box[0])/maxLeft,(box[0]+box[2]-32)/maxRight),(baseline-box[1])/(double)maxAbove);
            if(maxBelow>0)Scale=Math.Min(Scale,(box[1]+box[3]-baseline)/(double)maxBelow);
            if(Scale<=0||!double.IsFinite(Scale))throw new InvalidOperationException($"{path}: effects extend below baseline/contentbox; needs explicit review.");
            FitScale=Scale;
            Console.WriteLine($"UNPACK {System.IO.Path.GetFileName(path)}: primary{mains.Length}/{columns*rows}, total{Components.Count}, fixedscale{Scale:F9}");
        }
    }
}
