using Godot;

/// <summary>
/// 앱 이름·버전을 한 곳에서 읽는다.
///
/// 버전 문자열은 <c>project.godot</c> 의 <c>application/config/version</c> 이
/// 원본이다. C# 쪽에 같은 숫자를 또 적으면 한쪽만 올린 채로 출시되어
/// 화면에 표시되는 버전과 스토어 버전이 어긋난다. 그래서 하드코딩하지 않고
/// ProjectSettings 에서 읽는다.
///
/// 유일하게 중복이 불가피한 곳은 <c>export_presets.cfg</c> 의
/// <c>version/name</c> / <c>version/code</c> 다(Godot 내보내기 설정이 별도
/// 파일이라 참조할 수 없다). 그 둘이 어긋나지 않도록 CI 가 검사한다.
/// 자세한 규칙은 <c>docs/RELEASE.md</c>.
/// </summary>
internal static class AppInfo
{
    /// <summary>표시용 앱 이름. project.godot 의 config/name.</summary>
    public static string Name => Get("application/config/name", "Kemura");

    /// <summary>배포 버전. 예: "0.9.0".</summary>
    public static string Version => Get("application/config/version", "0.0.0");

    /// <summary>앱 패키지명(applicationId).</summary>
    public static string PackageName => Settings.PackageName;

    /// <summary>화면·로그에 쓰는 한 줄 표기. 예: "Kemura v0.9.0".</summary>
    public static string NameWithVersion => $"{Name} v{Version}";

    static string Get(string key, string fallback)
    {
        // 설정이 없으면 fallback 이 그대로 돌아온다. 자기 검증처럼 프로젝트
        // 설정이 로드되지 않은 상태에서 불려도 빈 문자열을 내놓지 않는다.
        var s = ProjectSettings.GetSetting(key, fallback).AsString();
        return string.IsNullOrWhiteSpace(s) ? fallback : s;
    }
}
