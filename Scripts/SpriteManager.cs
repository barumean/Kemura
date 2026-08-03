using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera.Content;

internal static class SpriteManager
{
    /// <summary>
    /// テクスチャ1枚の情報。
    /// Image(CPU側)はどのスレッドからでも安全に生成できるが、
    /// ImageTexture(GPU側)はRenderingServerに触るためメインスレッド専用。
    /// 従ってImageをロードして保持し、ImageTextureは初回アクセス時に遅延生成する。
    /// </summary>
    internal sealed class TextureInfo : IDisposable
    {
        internal TextureInfo(string name, Image img)
        {
            imagename = name;
            image = img;
            width = img.GetWidth();
            height = img.GetHeight();
        }

        internal readonly string imagename;
        internal readonly int width;
        internal readonly int height;

        Image? image;
        ImageTexture? texture_;

        internal Image? Image => image;

        /// <summary>メインスレッドからのみアクセスすること。</summary>
        internal ImageTexture? texture
        {
            get
            {
                if (texture_ == null && image != null)
                    texture_ = ImageTexture.CreateFromImage(image);
                return texture_;
            }
        }

        /// <summary>Imageを書き換えた後にGPU側へ反映する。</summary>
        internal void Invalidate()
        {
            if (texture_ != null && image != null)
                texture_.Update(image);
        }

        public void Dispose()
        {
            // ImageTexture/ImageはRefCountedなネイティブオブジェクトなので、
            // 参照をnullにするだけではGPU/ネイティブメモリが解放されない。
            texture_?.Dispose();
            texture_ = null;
            image?.Dispose();
            image = null;
        }
    }

    static readonly Dictionary<string, TextureInfo> textureCache = new();
    static readonly Dictionary<string, string[]> resourceCsvCache = new();
    // 両キャッシュはEmueraスレッドとメインスレッドの双方から触られるため必ずロックする。
    static readonly object sync = new object();

    internal static TextureInfo? GetTextureInfo(string name, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string key = name ?? path;

        lock (sync)
        {
            if (textureCache.TryGetValue(key, out var cached))
                return cached;
        }

        if (!File.Exists(path)) return null;

        // ロックの外でファイルI/Oとデコードを行う(重い処理中にロックを保持しない)
        var img = new Image();
        var err = img.Load(path);
        if (err != Error.Ok)
        {
            img.Dispose();
            return null;
        }

        var ti = new TextureInfo(key, img);

        lock (sync)
        {
            // 別スレッドが先に同じキーを入れていた場合は自分の分を捨てる
            if (textureCache.TryGetValue(key, out var raced))
            {
                ti.Dispose();
                return raced;
            }
            textureCache[key] = ti;
        }
        return ti;
    }

    internal static void ForceClear()
    {
        TextureInfo[] snapshot;
        lock (sync)
        {
            snapshot = new TextureInfo[textureCache.Count];
            textureCache.Values.CopyTo(snapshot, 0);
            textureCache.Clear();
        }
        foreach (var ti in snapshot)
            ti.Dispose();
    }

    internal static string[]? GetResourceCSVLines(string path)
    {
        lock (sync)
        {
            resourceCsvCache.TryGetValue(path, out var lines);
            return lines;
        }
    }

    internal static void SetResourceCSVLine(string path, string[] lines)
    {
        lock (sync)
            resourceCsvCache[path] = lines;
    }

    internal static void ClearResourceCSVLines(string path)
    {
        lock (sync)
            resourceCsvCache.Remove(path);
    }
}
