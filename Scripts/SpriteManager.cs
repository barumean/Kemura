using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera.Content;

internal static class SpriteManager
{
    /// <summary>
    /// 텍스처 한 장의 정보.
    /// Image(CPU 측)는 어느 스레드에서든 안전하게 생성할 수 있지만,
    /// ImageTexture(GPU 측)는 RenderingServer 를 건드리므로 메인 스레드 전용이다.
    /// 따라서 Image 를 로드해 보관하고, ImageTexture 는 첫 접근 시 지연 생성한다.
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

        /// <summary>메인 스레드에서만 접근할 것.</summary>
        internal ImageTexture? texture
        {
            get
            {
                if (texture_ == null && image != null)
                    texture_ = ImageTexture.CreateFromImage(image);
                return texture_;
            }
        }

        /// <summary>Image 를 수정한 뒤 GPU 측에 반영한다.</summary>
        internal void Invalidate()
        {
            if (texture_ != null && image != null)
                texture_.Update(image);
        }

        public void Dispose()
        {
            // ImageTexture/Image 는 RefCounted 네이티브 객체이므로,
            // 참조를 null 로 만드는 것만으로는 GPU/네이티브 메모리가 해제되지 않는다.
            texture_?.Dispose();
            texture_ = null;
            image?.Dispose();
            image = null;
        }
    }

    static readonly Dictionary<string, TextureInfo> textureCache = new();
    static readonly Dictionary<string, string[]> resourceCsvCache = new();
    // 두 캐시는 Emuera 스레드와 메인 스레드 양쪽에서 접근하므로 반드시 잠근다.
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

        // 잠금 밖에서 파일 I/O와 디코딩을 수행한다(무거운 처리 중에 잠금을 보유하지 않는다)
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
            // 다른 스레드가 먼저 같은 키를 넣었다면 자기 것을 버린다
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
