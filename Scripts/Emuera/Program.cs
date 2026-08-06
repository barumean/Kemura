using System;
//using System.Drawing;
using System.Collections.Generic;
//using System.Windows.Forms;
using MinorShift._Library;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.GameData.Expression;
using System.IO;
using uEmuera;
using uEmuera.Drawing;
using uEmuera.Forms;
using uEmuera.Window;

namespace MinorShift.Emuera
{
	public static class Program
	{
		/*
		コードの開始地点。
		ここでMainWindowを作り、
		MainWindowがProcessを作り、
		ProcessがGameBase・ConstantData・Variableを作る。
		
		
		*.ERBの読み込み、実行、その他の処理をProcessが、
		入出力をMainWindowが、
		定数の保存をConstantDataが、
		変数の管理をVariableが行う。
		 
		と言う予定だったが改変するうちに境界が曖昧になってしまった。
		 
		後にEmueraConsoleを追加し、それに入出力を担当させることに。
        
        1750 DebugConsole追加
         Debugを全て切り離すことはできないので一部EmueraConsoleにも担当させる
		
		TODO: 1819 MainWindow & Consoleの入力・表示組とProcess&Dataのデータ処理組だけでも分離したい

		*/
		/// <summary>
		/// ゲームフォルダ配下のサブフォルダを大文字小文字を無視して解決する。
		/// 見つからない場合は小文字の想定パスを返すので、呼び出し側の
		/// 「フォルダが見つかりません」処理がそのまま動く。
		/// </summary>
		static string ResolveGameSubDir(string name)
		{
			var lower = ExeDir + name.ToLowerInvariant() + "/";
			if (Directory.Exists(lower))
				return lower;
			var upper = ExeDir + name.ToUpperInvariant() + "/";
			if (Directory.Exists(upper))
				return upper;
			var resolved = PathResolver.ResolveDirectory(lower);
			if (Directory.Exists(resolved))
				return resolved.TrimEnd('/', '\\') + "/";
			return lower;
		}

		/// <summary>
		/// アプリケーションのメイン エントリ ポイントです。
		/// </summary>
		//[STAThread]
		public static void Main(string[] args)
		{

			ExeDir = Sys.ExeDir;
#if UEMUERA_DEBUG
			//debugMode = true;

			//ExeDirにバリアントのパスを代入することでテスト実行するためのコード。
			//ローカルパスの末尾には\必須。
			//ローカルパスを記載した場合は頒布前に削除すること。
			ExeDir = @"";
			
#endif
			// 소문자 → 대문자 → 그 외 대소문자 조합 순으로 찾는다.
			// Windows는 대소문자를 무시하므로 두 번의 시도로 충분했지만,
			// Android/Linux 에서는 Csv/ 같은 혼합 표기를 놓친다.
			CsvDir = ResolveGameSubDir("csv");
			ErbDir = ResolveGameSubDir("erb");
			DebugDir = ResolveGameSubDir("debug");
			DatDir = ResolveGameSubDir("dat");
			ContentDir = ResolveGameSubDir("resources");
			//エラー出力用
			//1815 .exeが東方板のNGワードに引っかかるそうなので除去
			//ExeName = Path.GetFileNameWithoutExtension(Sys.ExeName);

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			ConfigData.Instance.LoadConfig();
            //二重起動の禁止かつ二重起動
			//if ((!Config.AllowMultipleInstances) && (Sys.PrevInstance()))
			//{
			//	MessageBox.Show("多重起動を許可する場合、emuera.configを書き換えて下さい", "既に起動しています");
			//	return;
			//}
			if (!Directory.Exists(CsvDir))
			{
				MessageBox.Show("\"" + CsvDir + "\" csv 폴더를 찾을 수 없습니다", "폴더 없음");
				return;
			}
			if (!Directory.Exists(ErbDir))
			{
				MessageBox.Show("\"" + ErbDir + "\" erb 폴더를 찾을 수 없습니다", "폴더 없음");
				return;
			}
            int argsStart = 0;
            if ((args.Length > 0)&&(args[0].Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase)))
            {
                argsStart = 1;//デバッグモードかつ解析モード時に最初の1っこ(-DEBUG)を飛ばす
				debugMode = true;
            }
			if(debugMode)
			{
				ConfigData.Instance.LoadDebugConfig();
				if (!Directory.Exists(DebugDir))
				{
					try
					{
						Directory.CreateDirectory(DebugDir);
					}
					catch
					{
						MessageBox.Show("debug 폴더 생성에 실패했습니다", "폴더 없음");
						return;
					}
				}
			}
            if (args.Length > argsStart)
            {
                AnalysisFiles = new List<string>();
                for (int i = argsStart; i < args.Length; i++)
                {
                    if (!File.Exists(args[i]) && !Directory.Exists(args[i]))
                    {
                        MessageBox.Show("与えられたファイル・フォルダは存在しません");
                        return;
                    }
                    if ((File.GetAttributes(args[i]) & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        List<KeyValuePair<string, string>> fnames = Config.GetFiles(args[i] + "\\", "*.ERB");
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                        fnames.AddRange(Config.GetFiles(args[i] + "\\", "*.erb"));
#endif
                        for(int j = 0; j < fnames.Count; j++)
                        {
                            AnalysisFiles.Add(fnames[j].Value);
                        }
                    }
                    else
                    {
                        if (Path.GetExtension(args[i]).ToUpper() != ".ERB")
                        {
                            MessageBox.Show("ドロップ可能なファイルはERBファイルのみです");
                            return;
                        }
                        AnalysisFiles.Add(args[i]);
                    }
                }
                AnalysisMode = true;
            }
			MainWindow win = null;


			//while (true)
			//{
				StartTime = WinmmTimer.TickCount;
                //using (win = new MainWindow())
                //{
                    win = new MainWindow();
                    Application.Run(win);
				//	Content.AppContents.UnloadContents();
				//	if (!Reboot)
				//		break;

				//	RebootWinState = win.WindowState;
				//	if (win.WindowState == FormWindowState.Normal)
				//	{
				//		RebootClientY = win.ClientSize.Height;
				//		RebootLocation = win.Location;
				//	}
				//	else
				//	{
				//		RebootClientY = 0;
				//		RebootLocation = new Point();
				//	}
				//}
				////条件次第ではParserMediatorが空でない状態で再起動になる場合がある
				//ParserMediator.ClearWarningList();
				//ParserMediator.Initialize(null);
				//GlobalStatic.Reset();
				////GC.Collect();
				//Reboot = false;
				//ConfigData.Instance.LoadConfig();
			//}
		}

		/// <summary>
		/// 実行ファイルのディレクトリ。最後に\を付けたstring
		/// </summary>
		public static string ExeDir { get; private set; }
		public static string CsvDir { get; private set; }
		public static string ErbDir { get; private set; }
		public static string DebugDir { get; private set; }
		public static string DatDir { get; private set; }
		public static string ContentDir { get; private set; }
		public static string ExeName { get; private set; }

		public static bool Reboot = false;
		//public static int RebootClientX = 0;
		public static int RebootClientY = 0;
        public static FormWindowState RebootWinState = FormWindowState.Normal;
		public static Point RebootLocation;

        public static bool AnalysisMode = false;
        public static List<string> AnalysisFiles = null;

		public static bool debugMode = false;
		public static bool DebugMode { get { return debugMode; } }


		public static uint StartTime { get; private set; }

	}
}