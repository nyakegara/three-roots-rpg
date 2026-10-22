using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
// Deterministic extraction and nearest-neighbor packaging of generated art.
var root=Path.GetFullPath(args[0]);var entries=new List<object>();
foreach(var id in new[]{"utburd","silat","nachzehrer","vetala","asura-preta"}){
 using var src=new Bitmap(Path.Combine(root,"sources",id+".png"));
 int w=src.Width,h=src.Height;var labels=new int[w*h];var groups=new List<List<int>>();
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){
  int p=y*w+x;if(labels[p]!=0||src.GetPixel(x,y).A<128)continue;
  int n=groups.Count+1;var pixels=new List<int>();var queue=new Queue<int>();queue.Enqueue(p);labels[p]=n;
  while(queue.TryDequeue(out int q)){pixels.Add(q);int qx=q%w,qy=q/w;
   for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
    int nx=qx+dx,ny=qy+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int z=ny*w+nx;
    if(labels[z]==0&&src.GetPixel(nx,ny).A>=128){labels[z]=n;queue.Enqueue(z);}
   }
  }groups.Add(pixels);
 }
 Rectangle Bounds(List<int> a){int l=a.Min(p=>p%w),r=a.Max(p=>p%w),t=a.Min(p=>p/w),b=a.Max(p=>p/w);return new(l,t,r-l+1,b-t+1);}
 int count=id=="utburd"?8:4;
 var bodies=groups.OrderByDescending(g=>g.Count).Take(count).Select(g=>(Pixels:g,Box:Bounds(g))).ToList();
 int cols=id is "vetala" or "asura-preta"?2:4;
 bodies=bodies.OrderBy(g=>g.Box.Top+g.Box.Height/2).Chunk(cols).SelectMany(row=>row.OrderBy(g=>g.Box.Left)).ToList();
 Console.WriteLine(id+" "+w+"x"+h+" "+JsonSerializer.Serialize(bodies.Select(g=>new[]{g.Box.X,g.Box.Y,g.Box.Width,g.Box.Height})));
 if(args.Contains("--inspect"))continue;
 var all=bodies.Select(b=>new HashSet<int>(b.Pixels)).ToArray();
 foreach(var g in groups.Where(g=>!bodies.Any(b=>ReferenceEquals(b.Pixels,g)))){
  if(g.Count<4)continue;var b=Bounds(g);double cx=b.X+b.Width/2.0,cy=b.Y+b.Height/2.0;
  int nearest=Enumerable.Range(0,count).MinBy(i=>{var a=bodies[i].Box;double dx=Math.Max(a.Left-cx,Math.Max(0,cx-a.Right)),dy=Math.Max(a.Top-cy,Math.Max(0,cy-a.Bottom));return dx*dx+dy*dy;});
  foreach(int p in g)all[nearest].Add(p);
 }
 using var reference=new Bitmap(Path.Combine(root,"../../../../content/art/prototypes/undead",id+".png"));
 int top=128,bottom=0;for(int y=0;y<128;y++)for(int x=0;x<128;x++)if(reference.GetPixel(x,y).A>=128){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
 double scale=(bottom-top+1.0)/bodies[0].Box.Height;
 using var sheet=new Bitmap(192*4,192*(count/4));var records=new List<object>();
 for(int i=0;i<count;i++){
  var b=bodies[i].Box;
  var feet=bodies[i].Pixels.Where(p=>p/w>=b.Bottom-18).ToArray();
  double px=(feet.Min(p=>p%w)+feet.Max(p=>p%w))/2.0,py=b.Bottom;
  if(id=="asura-preta"){double[] xs={.265,.755,.255,.775},ys={.524,.524,.962,.994};px=w*xs[i];py=h*ys[i];}
  int occupied=0,edge=0;
  for(int y=0;y<192;y++)for(int x=0;x<192;x++){
   int sx=(int)Math.Floor(px+(x+.5-80)/scale),sy=(int)Math.Floor(py+(y+.5-168)/scale);
   if(sx<0||sy<0||sx>=w||sy>=h||!all[i].Contains(sy*w+sx))continue;
   var c=src.GetPixel(sx,sy);sheet.SetPixel(i%4*192+x,i/4*192+y,Color.FromArgb(255,c.R,c.G,c.B));occupied++;if(x==0||y==0||x==191||y==191)edge++;
  }
  if(occupied==0||edge>0)throw new Exception($"Empty or clipped {id} frame {i}");
  records.Add(new{frame=i,pivot=new{px,py},scale,occupied,edge});
 }
 sheet.Save(Path.Combine(root,id+".png"),ImageFormat.Png);
 var clips=id=="utburd"?new[]{new{name="attack",frames=new[]{0,1,2,3}},new{name="heal",frames=new[]{4,5,6,7}}}:new[]{new{name="attack",frames=new[]{0,1,2,3}}};
 var entry=new{id,image=id+".png",cell=192,anchor=new{x=80,y=168},frameMs=160,impactFrame=2,clips,records};entries.Add(entry);
 File.WriteAllText(Path.Combine(root,id+".json"),JsonSerializer.Serialize(entry,new JsonSerializerOptions{WriteIndented=true}));
}
if(!args.Contains("--inspect"))File.WriteAllText(Path.Combine(root,"manifest.js"),"window.undeadActions="+JsonSerializer.Serialize(entries)+";");
