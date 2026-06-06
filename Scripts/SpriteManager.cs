using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera.Content;

internal static class SpriteManager
{
    internal class TextureInfo : IDisposable
    {
        internal TextureInfo(string name, ImageTexture tex, int w, int h)
        {
            imagename = name;
            texture = tex;
            width = w;
            height = h;
        }
        public void Dispose()
        {
            texture = null!;
        }
        internal string imagename;
        internal ImageTexture? texture;
        internal int width;
        internal int height;
    }

    class TextureInfoOtherThread
    {
        public System.Threading.Mutex? mutex;
    }

    static readonly Dictionary<string, TextureInfo> textureCache = new();
    static readonly Dictionary<string, string[]> resourceCsvCache = new();

    internal static TextureInfo? GetTextureInfo(string name, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string key = name ?? path;
        if (textureCache.TryGetValue(key, out var ti)) return ti;

        if (!File.Exists(path)) return null;

        var img = new Image();
        var err = img.Load(path);
        if (err != Error.Ok) return null;

        var tex = ImageTexture.CreateFromImage(img);
        ti = new TextureInfo(key, tex, img.GetWidth(), img.GetHeight());
        textureCache[key] = ti;
        return ti;
    }

    internal static TextureInfoOtherThread GetTextureInfoOtherThread(
        string name, string path, Action<TextureInfo?> callback)
    {
        var tiot = new TextureInfoOtherThread();
        tiot.mutex = new System.Threading.Mutex();
        tiot.mutex.WaitOne();

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            var ti = GetTextureInfo(name, path);
            callback(ti);
            tiot.mutex!.ReleaseMutex();
        });

        return tiot;
    }

    internal static void ForceClear()
    {
        foreach (var ti in textureCache.Values) ti.Dispose();
        textureCache.Clear();
    }

    internal static string[]? GetResourceCSVLines(string path)
    {
        resourceCsvCache.TryGetValue(path, out var lines);
        return lines;
    }

    internal static void SetResourceCSVLine(string path, string[] lines)
    {
        resourceCsvCache[path] = lines;
    }

    internal static void ClearResourceCSVLines(string path)
    {
        resourceCsvCache.Remove(path);
    }
}
