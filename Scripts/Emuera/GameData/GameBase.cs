using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using MinorShift.Emuera.Sub;

namespace MinorShift.Emuera.GameData
{
	internal sealed class GameBase
	{
		public string ScriptAutherName = "";
		public string ScriptDetail = "";//詳細な説明
		public string ScriptYear = "";
		public string ScriptTitle = "";
		public Int64 ScriptUniqueCode = 0;
		//1.713 訂正。eramakerのバージョンの初期値は1000ではなく0だった
		public Int64 ScriptVersion = 0;//1000;
		//1.713 上の変更とあわせて。セーブデータのバージョンが1000であり、現在のバージョンが未定義である場合、セーブデータのバージョンを同じとみなす
		public bool ScriptVersionDefined = false;
		public Int64 ScriptCompatibleMinVersion = -1;
        public string Compatible_EmueraVer = "0.000.0.0";

		//1.727 追加。Form.Text
		public string ScriptWindowTitle = null;
		public string ScriptVersionText
		{
			get
			{
				StringBuilder versionStr = new StringBuilder();
				versionStr.Append((ScriptVersion / 1000).ToString());
				versionStr.Append(".");
				if ((ScriptVersion % 10) != 0)
					versionStr.Append((ScriptVersion % 1000).ToString("000"));
				else
					versionStr.Append((ScriptVersion % 1000 / 10).ToString("00"));
				return versionStr.ToString();
			}
		}
		public bool UniqueCodeEqualTo(Int64 target)
		{
			//1804 UniqueCode Int64への拡張に伴い修正
			if (target == 0L)
				return true;
			return target == ScriptUniqueCode;
		}

		public bool CheckVersion(Int64 target)
		{
			if (!ScriptVersionDefined && target != 1000)
				return true;
			if (ScriptCompatibleMinVersion <= target)
				return true;
			return ScriptVersion == target;
		}

		public Int64 DefaultCharacter = -1;
		public Int64 DefaultNoItem = 0;

		private bool tryatoi(string str, out Int64 i)
		{
			if (Int64.TryParse(str, out i))
				return true;
			StringStream st = new StringStream(str);
			StringBuilder sb = new StringBuilder(str.Length);
			while (true)
			{
				if (st.EOS)
					break;
				if (!char.IsNumber(st.Current))
					break;
				sb.Append(st.Current);
				st.ShiftNext();
			}
			if (sb.Length > 0)
				if (Int64.TryParse(sb.ToString(), out i))
					return true;
			return false;
		}

		/// <summary>
		/// GAMEBASE読み込み。GAMEBASE.csvの存在は必須ではないので読み込み失敗したらなかったことにする。
		/// </summary>
		/// <param name="basePath"></param>
		/// <returns>読み込み続行するなら真、エラー終了なら偽</returns>
		public bool LoadGameBaseCsv(string basePath)
		{
			// 대소문자가 달라도 찾도록 실제 경로로 해석한다(Android/Linux 대응)
			basePath = PathResolver.ResolveFile(basePath);
            if (!File.Exists(basePath))
            {
                return true;
            }
			ScriptPosition pos = null;
			EraStreamReader eReader = new EraStreamReader(false);
			if (!eReader.Open(basePath))
			{
				//output.PrintLine(eReader.Filename + " 열기에 실패했습니다");
				return true;
			}
			try
			{
				StringStream st = null;
				while ((st = eReader.ReadEnabledLine()) != null)
				{
					string[] tokens = st.Substring().Split(',');
					if (tokens.Length < 2)
						continue;
					// 表計算ソフトで書き出したGAMEBASE.CSVは項目名の後ろに空白が
					// 残ることがある。トリムしないと switch がどれにも一致せず、
					// 「コード」や「バージョン」が黙って無視される。コードが0の
					// セーブデータはどのゲームからでも読めてしまうので、
					// 静かに落ちるのが一番まずい。
					// (行頭の空白は ReadEnabledLine が既に落としている)
					string name = tokens[0].Trim();
					string param = tokens[1].Trim();
					pos = new ScriptPosition(eReader.Filename, eReader.LineNo);
					switch (name)
					{
						case "コード":
							if (tryatoi(tokens[1], out ScriptUniqueCode))
							{
								if (ScriptUniqueCode == 0L)
									ParserMediator.Warn("コード:0のセーブデータはいかなるコードのスクリプトからも読めるデータとして扱われます", pos, 0);
							}							
							break;
						case "バージョン":
							ScriptVersionDefined = tryatoi(tokens[1], out ScriptVersion);
							break;
						case "バージョン違い認める":
							tryatoi(tokens[1], out ScriptCompatibleMinVersion);
							break;
						case "最初からいるキャラ":
							tryatoi(tokens[1], out DefaultCharacter);
							break;
						case "アイテムなし":
							tryatoi(tokens[1], out DefaultNoItem);
							break;
						case "タイトル":
							ScriptTitle = param;
							break;
						case "作者":
							ScriptAutherName = param;
							break;
						case "製作年":
							ScriptYear = param;
							break;
						case "追加情報":
							ScriptDetail = param;
							break;
						case "ウィンドウタイトル":
							ScriptWindowTitle = param;
							break;
							
                        case "動作に必要なEmueraのバージョン":
                            Compatible_EmueraVer = param;
                            if (!Regex.IsMatch(Compatible_EmueraVer, @"^\d+\.\d+\.\d+\.\d+$"))
                            {
                                ParserMediator.Warn("バージョン指定を読み取れなかったので処理を省略します", pos, 0);
                                break;
                            }
                            // エンジン側のバージョン文字列が空・不正だと new Version が
                            // 例外を投げ、それが下のcatchに飲まれてGAMEBASE.CSVの
                            // 残り(コード・バージョン・タイトル等)が全て失われていた。
                            // バージョン判定だけを飛ばして読み込みは続ける。
                            // エンジン自身のバージョンはビルド定数であって
                            // ウィンドウの状態ではない。GlobalStatic.MainWindow
                            // 経由で読むと、ウィンドウ生成前にCSVを読む経路では
                            // 常にnullになりバージョン判定が丸ごと飛んでいた。
                            string engineVer = uEmuera.Window.MainWindow.uEmueraVer;
                            if (!Regex.IsMatch(engineVer ?? "", @"^\d+\.\d+\.\d+\.\d+$"))
                            {
                                ParserMediator.Warn("엔진 버전을 알 수 없어 버전 판정을 건너뜁니다", pos, 1);
                                break;
                            }
                            Version curerntVersion = new Version(engineVer);
                            Version targetVersoin = new Version(Compatible_EmueraVer);
                            if (curerntVersion < targetVersoin)
                            {
                                // 元のメッセージは「必要なバージョン」ではなく
                                // エンジン自身のバージョンを表示していた。
                                // ユーザーには「今入っているものが必要」と読めてしまう。
                                ParserMediator.Warn(
                                    $"이 게임을 실행하려면 Emuera {Compatible_EmueraVer} 이상이 필요합니다"
                                    + $" (현재 {engineVer})", pos, 2);
                                return false;
                            }
                            break;
					}
				}
			}
			catch
			{
                ParserMediator.Warn("GAMEBASE.CSVの読み込み中にエラーが発生したため、読みこみを中断します", pos, 1);
				return true;
			}
			finally
			{
				eReader.Close();
			}
			if (ScriptWindowTitle == null)
			{
				if (string.IsNullOrEmpty(ScriptTitle))
					ScriptWindowTitle = "Emuera";
				else
					ScriptWindowTitle = ScriptTitle + " " + ScriptVersionText;
			}
			return true;
		}
	}





}
