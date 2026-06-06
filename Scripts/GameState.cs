using Godot;

/// <summary>
/// 씬 간 공유 상태 및 메인 스레드 전용 값 캐시
/// </summary>
public static class GameState
{
    /// FirstWindow에서 선택된 게임 폴더 경로
    public static string SelectedGamePath { get; set; } = "";

    /// 메인 스레드에서 갱신되는 화면 크기 (EmueraConsole.ClientWidth/Height에서 읽음)
    public static int ScreenWidth { get; private set; } = 1080;
    public static int ScreenHeight { get; private set; } = 1920;

    public static void UpdateScreenSize(Vector2I size)
    {
        ScreenWidth = size.X;
        ScreenHeight = size.Y;
    }
}
