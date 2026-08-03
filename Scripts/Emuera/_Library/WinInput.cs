using System;
using System.Runtime.InteropServices;

namespace MinorShift._Library
{
	internal sealed class WinInput
	{
		// Windows仮想キーコード
		const int VK_SHIFT = 0x10;
		const int VK_CONTROL = 0x11;
		const int VK_MENU = 0x12;   // Alt

		/// <summary>
		/// user32.dll GetKeyState の代替。押下時は最上位ビットを立てて返す。
		/// EmueraはShift/Ctrl押下でスキップ動作を切り替えるため、
		/// 常に0を返すとその機能が無効になる。
		/// </summary>
		public static short GetKeyState(int nVirtKey)
		{
			bool pressed = nVirtKey switch
			{
				VK_SHIFT => Godot.Input.IsKeyPressed(Godot.Key.Shift),
				VK_CONTROL => Godot.Input.IsKeyPressed(Godot.Key.Ctrl),
				VK_MENU => Godot.Input.IsKeyPressed(Godot.Key.Alt),
				_ => false,
			};
			return pressed ? unchecked((short)0x8000) : (short)0;
		}
	}

    public enum MouseButtons
    {
        None = 0,
        Left = 1048576,
        Right = 2097152,
        Middle = 4194304,
        XButton1 = 8388608,
        XButton2 = 16777216
    }
}