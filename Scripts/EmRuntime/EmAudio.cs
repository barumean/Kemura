using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// Emuera EM+EE 확장의 오디오(<c>PLAYBGM</c> 계열).
///
/// 규격: https://gitlab.com/EvilMask/emuera.em.doc
/// EM 은 <c>sound</c> 폴더(Emuera.exe 와 같은 위치)의 파일을 재생한다.
/// 여기서는 게임 폴더 기준으로 <c>sound/</c> 를 찾고, 없으면 게임 폴더 자체에서
/// 상대 경로로도 찾는다(<c>PLAYBGM @"music/score/1.m4a"</c> 처럼 부르는 게임이 있다).
///
/// <para><b>스레드</b>: era 명령은 엔진 스레드에서 실행되지만
/// AudioStreamPlayer 는 Godot 메인 스레드에서만 만들고 조작해야 한다.
/// 그래서 요청을 큐에 넣고 <see cref="Pump"/> 가 메인 스레드에서 처리한다.
/// EmueraMain._Process 가 매 프레임 Pump 를 호출한다.</para>
/// </summary>
internal static class EmAudio
{
    /// <summary>메인 스레드에서 실행할 요청.</summary>
    static readonly ConcurrentQueue<Action> pending = new();

    static Node? host;
    static AudioStreamPlayer? bgm;
    static readonly List<AudioStreamPlayer> sounds = new();

    // 볼륨은 0~100 (EM 규격). 엔진 스레드에서 읽고 쓰므로 volatile.
    static volatile int bgmVolume = 100;
    static volatile int soundVolume = 100;

    /// <summary>메인 스레드에서 한 번 호출한다. 플레이어를 붙일 부모 노드.</summary>
    internal static void Attach(Node node) => host = node;

    /// <summary>메인 스레드에서 매 프레임 호출한다.</summary>
    internal static void Pump()
    {
        while (pending.TryDequeue(out var a))
        {
            try { a(); }
            catch (Exception e) { GD.PushWarning($"EmAudio: {e.Message}"); }
        }
        // 재생이 끝난 효과음 플레이어를 회수한다. 두면 노드가 계속 쌓인다.
        for (int i = sounds.Count - 1; i >= 0; --i)
        {
            var p = sounds[i];
            if (p == null || !GodotObject.IsInstanceValid(p))
            {
                sounds.RemoveAt(i);
                continue;
            }
            if (!p.Playing)
            {
                sounds.RemoveAt(i);
                p.QueueFree();
            }
        }
    }

    // ------------------------------------------------------------------
    // 엔진 스레드에서 부르는 입구
    // ------------------------------------------------------------------

    internal static long PlayBgm(string file)
    {
        pending.Enqueue(() => DoPlayBgm(file));
        return 1;
    }

    internal static long StopBgm()
    {
        pending.Enqueue(() => bgm?.Stop());
        return 1;
    }

    internal static long PlaySound(string file)
    {
        pending.Enqueue(() => DoPlaySound(file));
        return 1;
    }

    internal static long StopSound()
    {
        pending.Enqueue(() =>
        {
            foreach (var p in sounds)
                if (GodotObject.IsInstanceValid(p)) p.Stop();
        });
        return 1;
    }

    internal static long SetBgmVolume(long v)
    {
        bgmVolume = (int)Math.Clamp(v, 0, 100);
        pending.Enqueue(() =>
        {
            if (bgm != null) bgm.VolumeDb = ToDb(bgmVolume);
        });
        return 1;
    }

    internal static long SetSoundVolume(long v)
    {
        soundVolume = (int)Math.Clamp(v, 0, 100);
        pending.Enqueue(() =>
        {
            foreach (var p in sounds)
                if (GodotObject.IsInstanceValid(p)) p.VolumeDb = ToDb(soundVolume);
        });
        return 1;
    }

    internal static long BgmVolume => bgmVolume;
    internal static long SoundVolume => soundVolume;

    /// <summary>파일이 있는지. 엔진 스레드에서 직접 확인한다(파일 시스템만 본다).</summary>
    internal static long ExistSound(string file)
        => Resolve(file) != null ? 1 : 0;

    // ------------------------------------------------------------------
    // 메인 스레드 실제 처리
    // ------------------------------------------------------------------

    static void DoPlayBgm(string file)
    {
        var path = Resolve(file);
        if (path == null)
        {
            GD.PushWarning($"PLAYBGM: 파일을 찾을 수 없습니다 ({file})");
            return;
        }
        var stream = Load(path);
        if (stream == null) return;

        if (bgm == null || !GodotObject.IsInstanceValid(bgm))
        {
            if (host == null) return;
            bgm = new AudioStreamPlayer { Name = "EmBgm" };
            host.AddChild(bgm);
        }
        bgm.Stream = stream;
        bgm.VolumeDb = ToDb(bgmVolume);
        bgm.Play();
    }

    static void DoPlaySound(string file)
    {
        var path = Resolve(file);
        if (path == null)
        {
            GD.PushWarning($"PLAYSOUND: 파일을 찾을 수 없습니다 ({file})");
            return;
        }
        var stream = Load(path);
        if (stream == null || host == null) return;

        // 효과음은 겹쳐 나야 하므로 재생마다 플레이어를 만들고 끝나면 회수한다.
        var p = new AudioStreamPlayer { Stream = stream, VolumeDb = ToDb(soundVolume) };
        host.AddChild(p);
        sounds.Add(p);
        p.Play();
    }

    /// <summary>0~100 을 데시벨로. 0 은 무음.</summary>
    static float ToDb(int volume)
        => volume <= 0 ? -80f : (float)(20.0 * Math.Log10(volume / 100.0));

    /// <summary>
    /// 게임 폴더 기준으로 파일을 찾는다. 대소문자를 무시한다.
    /// sound/ 아래를 먼저 보고, 없으면 게임 폴더 기준 상대 경로로 본다.
    /// </summary>
    static string? Resolve(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        var root = MinorShift._Library.Sys.ExeDir;
        if (string.IsNullOrEmpty(root)) return null;
        foreach (var candidate in new[]
        {
            PathResolver.ResolveFile(System.IO.Path.Combine(root, "sound", file)),
            PathResolver.ResolveFile(System.IO.Path.Combine(root, file)),
        })
        {
            if (System.IO.File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// 확장자에 맞는 AudioStream 을 만든다.
    ///
    /// Godot 이 기본으로 다루는 것은 ogg / mp3 / wav 다. era 게임이 흔히 쓰는
    /// m4a(AAC)는 <b>지원하지 않는다</b> — 그 경우 경고만 남기고 조용히 넘어간다.
    /// 소리가 안 나는 것이 게임을 멈추는 것보다 낫다.
    /// </summary>
    static AudioStream? Load(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        try
        {
            switch (ext)
            {
                case ".ogg":
                    return AudioStreamOggVorbis.LoadFromFile(path);
                case ".mp3":
                {
                    var bytes = System.IO.File.ReadAllBytes(path);
                    return new AudioStreamMP3 { Data = bytes };
                }
                case ".wav":
                    // Godot 4 는 런타임 WAV 로더를 노출하지 않는다.
                    // ResourceLoader 는 res:// 전용이라 외부 파일에는 쓸 수 없다.
                    GD.PushWarning($"PLAYBGM/PLAYSOUND: wav 런타임 로딩은 미지원 ({path})");
                    return null;
                default:
                    GD.PushWarning(
                        $"PLAYBGM/PLAYSOUND: 지원하지 않는 형식 {ext} ({path}). ogg 또는 mp3 로 변환해주세요.");
                    return null;
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"오디오 로딩 실패 ({path}): {e.Message}");
            return null;
        }
    }

    /// <summary>게임 전환 / RESETDATA 시 호출. 메인 스레드에서만.</summary>
    internal static void StopAll()
    {
        while (pending.TryDequeue(out _)) { }
        if (bgm != null && GodotObject.IsInstanceValid(bgm))
        {
            bgm.Stop();
            bgm.QueueFree();
        }
        bgm = null;
        foreach (var p in sounds)
            if (GodotObject.IsInstanceValid(p)) { p.Stop(); p.QueueFree(); }
        sounds.Clear();
        bgmVolume = 100;
        soundVolume = 100;
    }
}
