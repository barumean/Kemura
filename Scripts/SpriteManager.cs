using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;

/// <summary>
/// 텍스처 캐시 관리.
/// Image.Load는 워커 스레드에서 OK.
/// ImageTexture.CreateFromImage는 메인 스레드 전용 → 큐잉 패턴 사용.
/// </summary>
internal static class SpriteManager
{
    internal class TextureInfo : IDisposable
    {
        internal TextureInfo(string name, int w, int h)
        {
            imagename = name;
            width = w;
            height = h;
        }
        public void Dispose() { texture = null; }

        internal string imagename;
        internal ImageTexture? texture;  // 메인 스레드에서만 설정
        internal Image? image;           // 워커 스레드에서 로드
        internal int width;
        internal int height;
    }

    // ConcurrentDictionary: 다중 스레드에서 안전한 읽기/쓰기
    static readonly ConcurrentDictionary<string, TextureInfo> textureCache = new();
    static readonly ConcurrentDictionary<string, string[]> resourceCsvCache = new();

    // 메인 스레드에서 처리할 텍스처 생성 요청 큐
    static readonly ConcurrentQueue<TextureInfo> pendingTextureCreate = new();

    /// <summary>
    /// 메인 스레드에서 매 프레임 호출해 텍스처 생성 큐 처리.
    /// EmueraMain._Process 또는 전용 AutoLoad에서 호출.
    /// </summary>
    internal static void ProcessPendingTextures()
    {
        int limit = 4; // 프레임당 최대 처리 수
        while (limit-- > 0 && pendingTextureCreate.TryDequeue(out var ti))
        {
            if (ti.image == null) continue;
            try
            {
                ti.texture = ImageTexture.CreateFromImage(ti.image);
            }
            catch (Exception e)
            {
                GD.PushError($"[SpriteManager] CreateFromImage failed: {e.Message}");
            }
        }
    }

    /// <summary>워커 스레드에서 호출 가능. 텍스처는 메인 스레드에서 비동기 생성.</summary>
    internal static TextureInfo? GetTextureInfo(string? name, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string key = name ?? path;

        if (textureCache.TryGetValue(key, out var ti)) return ti;

        if (!File.Exists(path)) return null;

        // Image.Load는 CPU 전용 — 어느 스레드에서도 안전
        var img = new Image();
        var err = img.Load(path);
        if (err != Error.Ok) return null;

        ti = new TextureInfo(key, img.GetWidth(), img.GetHeight());
        ti.image = img;

        // ConcurrentDictionary: 동시 삽입 경합 처리
        ti = textureCache.GetOrAdd(key, ti);

        // 아직 텍스처가 없으면 메인 스레드 생성 큐에 등록
        if (ti.texture == null && ti.image != null)
            pendingTextureCreate.Enqueue(ti);

        return ti;
    }

    internal static void ForceClear()
    {
        foreach (var ti in textureCache.Values) ti.Dispose();
        textureCache.Clear();
        resourceCsvCache.Clear();
        while (pendingTextureCreate.TryDequeue(out _)) { }
    }

    internal static string[]? GetResourceCSVLines(string path)
    {
        resourceCsvCache.TryGetValue(path, out var lines);
        return lines;
    }

    internal static void SetResourceCSVLine(string path, string[] lines)
        => resourceCsvCache[path] = lines;

    internal static void ClearResourceCSVLines(string path)
        => resourceCsvCache.TryRemove(path, out _);
}
