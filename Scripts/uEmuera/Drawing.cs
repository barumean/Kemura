using System;
using Godot;

namespace uEmuera.Drawing
{
    public class Bitmap : IDisposable
    {
        public Bitmap(string path)
        {
            this.path = path;
            this.filename = path != null ? GenericUtils.GetFilename(path) : "";
        }

        public readonly string path;
        public readonly string filename;
        public string name;
        public Size size;

        public void Dispose() { }
        public int Width => size.Width;
        public int Height => size.Height;
        public Size Size => size;

        // CPU側のImageを直接使う。ImageTexture.GetImage()はGPUからの読み戻しを伴い、
        // メインスレッド以外から呼ぶと危険なうえ非常に遅い。
        public Color GetPixel(int x, int y)
        {
            var img = SpriteManager.GetTextureInfo(name, path)?.Image;
            if (img == null) return Color.Black;
            if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight())
                return Color.Black;
            var gc = img.GetPixel(x, y);
            return new Color(gc.R, gc.G, gc.B, gc.A);
        }
        public void SetPixel(Color c, int x, int y)
        {
            var ti = SpriteManager.GetTextureInfo(name, path);
            var img = ti?.Image;
            if (ti == null || img == null) return;
            if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight())
                return;
            img.SetPixel(x, y, new Godot.Color(c.r, c.g, c.b, c.a));
            ti.Invalidate();
        }
        public void Save(string savePath)
        {
            var img = SpriteManager.GetTextureInfo(name, path)?.Image;
            if (img == null) return;
            img.SavePng(savePath);
        }
    }

    public class BitmapTexture : Bitmap
    {
        // 以前はワーカースレッドへ投げてMutexで待っていたが、呼び出し側は結果を
        // 即座に必要とするため実質同期処理だった。加えてMutexはスレッド親和性が
        // あり別スレッドからのReleaseMutex()がApplicationExceptionを投げていた
        // (かつ所有スレッドでのWaitOne()は再入するため待機自体が無効だった)。
        // ファイルI/Oとデコードは呼び出しスレッドで完結させるのが正しい。
        public BitmapTexture(string path) : base(path)
        {
            var texName = string.Concat(":FILE:", filename);
            textureinfo = SpriteManager.GetTextureInfo(texName, path);
            if (textureinfo == null)
                return;
            size.Width = textureinfo.width;
            size.Height = textureinfo.height;
        }

        public ImageTexture? texture => textureinfo?.texture;
        internal SpriteManager.TextureInfo? textureinfo = null;
    }

    public class BitmapRenderTexture : Bitmap
    {
        public BitmapRenderTexture(int x, int y) : base(null)
        {
            size.Width = x;
            size.Height = y;
        }
    }

    public enum GraphicsUnit { World=0, Display=1, Pixel=2, Point=3, Inch=4, Document=5, Millimeter=6 }

    public sealed class Graphics
    {
        public static Graphics instance => instance_ ??= new Graphics();
        static Graphics? instance_;
        private Graphics() { }
        public void Clear() { }
        public void DrawImage(Bitmap texture, Rectangle destrect, Rectangle srcrect, GraphicsUnit unit)
            => uEmuera.Logger.Info("Graphics.DrawImage " + texture.name);
        public void DrawImage(Bitmap texture, Rectangle destrect, int x, int y, int w, int h, GraphicsUnit unit, ImageAttributes ia)
            => uEmuera.Logger.Info("Graphics.DrawImage " + texture.name);
        public void DrawString(string s, Font font, Brush brush, Point point)
            => uEmuera.Logger.Info("Graphics.DrawString " + s);
        public void FillRectangel(SolidBrush brush, Rectangle rect) { }
        public void Clear(Color color)
            => uEmuera.Logger.Info("Graphics.Clear " + color.ToArgb());
    }

    public class Brush { }
    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color color) { Color = color; }
        public Color Color { get; set; }
    }
    public sealed class Pen
    {
        public Pen() { }
        public Pen(Color c, Int64 width) { }
    }

    public enum FontStyle { Regular=0, Bold=1, Italic=2, Underline=4, Strikeout=8 }

    public class FontFamily
    {
        public FontFamily(string name) { this.name = name; }
        public string Name => name;
        string name;
    }

    public sealed class Font : IDisposable
    {
        static bool GetMonospaced(string name)
            => !monospaced_disable_set.Contains(name);
        static readonly System.Collections.Generic.HashSet<string> monospaced_disable_set =
            new System.Collections.Generic.HashSet<string> { "ＭＳ Ｐゴシック", "MS PGothic" };

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
        {
            fontFamily = new FontFamily(familyName);
            monospaced = GetMonospaced(familyName);
            size = emSize; fontStyle = style; graphicsUnit = unit;
        }
        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
            : this(familyName, emSize, style, unit) { }
        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVericalFont)
            : this(familyName, emSize, style, unit) { }

        public void Dispose() { }
        public FontFamily FontFamily => fontFamily;
        FontFamily fontFamily;
        public bool Monospaced => monospaced;
        bool monospaced = true;
        public float Size => size;
        float size;
        public FontStyle Style => fontStyle;
        FontStyle fontStyle;
        public bool Bold => (fontStyle & FontStyle.Bold) > 0;
        public bool Italic => (fontStyle & FontStyle.Italic) > 0;
        public bool Underline => (fontStyle & FontStyle.Underline) > 0;
        public bool Strikeout => (fontStyle & FontStyle.Underline) > 0;
        public GraphicsUnit Unit => graphicsUnit;
        GraphicsUnit graphicsUnit;
    }

    public struct Color
    {
        public static Color FromArgb(int argb)
            => FromArgb((argb >> 24), ((argb >> 16) & 0xFF), ((argb >> 8) & 0xFF), (argb & 0xFF));
        public static Color FromArgb(int red, int green, int blue)
            => FromArgb(255, red, green, blue);
        public static Color FromArgb(int alpha, int red, int green, int blue)
            => new Color { a = alpha/255f, r = red/255f, g = green/255f, b = blue/255f };
        public Color(int R, int G, int B) { r=R/255f; g=G/255f; b=B/255f; a=1f; }
        public Color(int R, int G, int B, int A) { r=R/255f; g=G/255f; b=B/255f; a=A/255f; }
        public Color(float R, float G, float B, float A) { r=R; g=G; b=B; a=A; }
        public int R => (int)(r*255); public int G => (int)(g*255);
        public int B => (int)(b*255); public int A => (int)(a*255);
        public int ToArgb() => (A<<24)+(R<<16)+(G<<8)+B;
        public int ToRGBA() => (R<<24)+(G<<16)+(B<<8)+A;
        public float a, r, g, b;
        public static readonly Color Black = new Color(0,0,0);
        public static readonly Color White = new Color(255,255,255);
        public static readonly Color Blue = new Color(0,0,255);
        public static readonly Color Red = new Color(255,0,0);
        public static readonly Color Green = new Color(0,255,0);
        public static readonly Color Grey = new Color(128,128,128);
        public static readonly Color Gray = Grey;
        public static readonly Color Transparent = new Color(0,0,0,0);
        public static Color FromName(string name) => name switch {
            "Black" => Black, "Blue" => Blue, "Gray" => Gray, "Green" => Green,
            "Grey" => Grey, "Red" => Red, "White" => White,
            _ => Black
        };
        public static bool operator ==(Color l, Color r) => l.A==r.A && l.R==r.R && l.G==r.G && l.B==r.B;
        public static bool operator !=(Color l, Color r) => !(l==r);
        public override bool Equals(object obj) => obj is Color c && c==this;
        public override int GetHashCode() => 0;
    }

    public struct Point
    {
        public static readonly Point Empty = new Point(0,0);
        public Point(Size size) { X=size.Width; Y=size.Height; }
        public Point(int x, int y) { X=x; Y=y; }
        public int X { get; set; }
        public int Y { get; set; }
        public void Offset(Point pt) { X+=pt.X; Y+=pt.Y; }
        public bool IsEmpty => X==0 && Y==0;
    }

    public struct Size
    {
        public static readonly Size zero = new Size(0,0);
        public Size(Point pt) { Width=pt.X; Height=pt.Y; }
        public Size(int width, int height) { Width=width; Height=height; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsEmpty => Width==0 && Height==0;
    }

    public struct Rectangle
    {
        public static Rectangle Intersect(Rectangle left, Rectangle right)
        {
            int l=Math.Max(left.Left,right.Left), r=Math.Min(left.Right,right.Right);
            int t=Math.Max(left.Top,right.Top), b=Math.Min(left.Bottom,right.Bottom);
            return (l<r && t<b) ? new Rectangle(l,t,r-l,b-t) : new Rectangle(0,0,0,0);
        }
        public Rectangle(Point location, Size size) { X=location.X; Y=location.Y; Width=size.Width; Height=size.Height; }
        public Rectangle(int x, int y, int width, int height) { X=x; Y=y; Width=width; Height=height; }
        public int X{get;set;} public int Y{get;set;} public int Width{get;set;} public int Height{get;set;}
        public int Top=>Y; public int Bottom=>Y+Height; public int Left=>X; public int Right=>X+Width;
        public Size Size => new Size(Width,Height);
        public bool IsEmpty => Width==0 && Height==0;
        public bool Contains(Point p) => Left<=p.X && p.X<Right && Top<=p.Y && p.Y<Bottom;
        public bool IntersectsWith(Rectangle r) => !(r.Bottom<=Top||r.Top>Bottom||r.Right<=Left||r.Left>Right);
    }

    public struct RectangleF
    {
        public RectangleF(float x, float y, float width, float height) { X=x; Y=y; Width=width; Height=height; }
        public float X{get;set;} public float Y{get;set;} public float Width{get;set;} public float Height{get;set;}
        public float Top=>Y; public float Bottom=>Y+Height; public float Left=>X; public float Right=>X+Width;
    }

    public class ImageAttributes { }
    public enum StringFormatFlags { DirectionRightToLeft=1, DirectionVertical=2, FitBlackBox=4, DisplayFormatControl=32, NoFontFallback=1024, MeasureTrailingSpaces=2048, NoWrap=4096, LineLimit=8192, NoClip=16384 }
    public class StringFormat { }
    public struct CharacterRange
    {
        public CharacterRange(int first, int length) { First=first; Length=length; }
        public int First{get;set;} public int Length{get;set;}
    }
}
