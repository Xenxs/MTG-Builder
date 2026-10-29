using SkiaSharp;

if (args.Length != 2) throw new ArgumentException("Provide original logo path and output asset directory.");
var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
using var bitmap=SKBitmap.Decode(args[0])??throw new Exception("Cannot decode supplied logo.");
File.Copy(args[0],Path.Combine(output,"magic-logo.webp"),true);
using(var original=SKImage.FromBitmap(bitmap)) using(var png=original.Encode(SKEncodedImageFormat.Png,100)) using(var file=File.Create(Path.Combine(output,"magic-logo.png"))) png.SaveTo(file);
int minX=bitmap.Width,minY=bitmap.Height,maxX=-1,maxY=-1;
for(var y=0;y<bitmap.Height;y++)for(var x=0;x<bitmap.Width/4;x++)
{var p=bitmap.GetPixel(x,y);if(p.Alpha>64&&p.Red>90&&p.Red>p.Green*1.35&&p.Blue<100){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}}
if(maxX<minX)throw new Exception("No orange brand mark found in the supplied logo.");
var source=new SKRect(minX,minY,maxX+1,maxY+1);int[] sizes=[16,24,32,48,64,128,256];var images=new List<byte[]>();
using var full=SKImage.FromBitmap(bitmap);
foreach(var size in sizes)
{
 using var surface=SKSurface.Create(new SKImageInfo(size,size,SKColorType.Bgra8888,SKAlphaType.Premul));surface.Canvas.Clear(SKColors.Transparent);
 var scale=size*0.88f/Math.Max(source.Width,source.Height);var w=source.Width*scale;var h=source.Height*scale;
 surface.Canvas.DrawImage(full,source,new SKRect((size-w)/2,(size-h)/2,(size+w)/2,(size+h)/2),new SKSamplingOptions(SKFilterMode.Linear));
 using var image=surface.Snapshot();using var png=image.Encode(SKEncodedImageFormat.Png,100);images.Add(png.ToArray());if(size==256)File.WriteAllBytes(Path.Combine(output,"magic-mark.png"),images[^1]);
}
using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"magic.ico"))))
{
 writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);var offset=6+16*sizes.Length;
 for(var i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
 foreach(var data in images)writer.Write(data);
}
Console.WriteLine($"Exact logo {bitmap.Width}×{bitmap.Height}; original orange mark {source}; seven ICO sizes.");
