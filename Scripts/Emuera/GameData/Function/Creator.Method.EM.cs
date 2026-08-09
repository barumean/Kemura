using System;
using System.Collections.Generic;
using MinorShift.Emuera.GameData.Expression;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Sub;   // CodeEE

namespace MinorShift.Emuera.GameData.Function
{
	/// <summary>
	/// Emuera EM+EE 확장의 표현식 함수.
	/// 규격: https://gitlab.com/EvilMask/emuera.em.doc
	///
	/// EM 은 대부분의 확장을 「명령과 표현식 함수 양쪽 지원」으로 규정한다.
	/// 여기에 등록하면 <b>양쪽이 동시에</b> 생긴다. FunctionIdentifier 의 정적
	/// 초기화 마지막(391~398행)에서 methodList 의 모든 항목을
	/// METHOD_Instruction 으로 감싸 명령으로도 등록하고, 그 명령은 반환값을
	/// RESULT / RESULTS 에 넣는다. 즉 별도의 명령 클래스가 필요 없다.
	///
	/// 출력 인수(ref)를 받는 형태도 이 경로로 만들 수 있다. 표현식 인수로
	/// 넘어온 변수는 <c>VariableTerm</c> 이고, 여기에는 이미
	/// <c>SetValue</c> 와 <c>GetFixedVariableTerm</c> 이 있다. 전용
	/// ArgumentBuilder 나 AbstractInstruction 은 필요하지 않다.
	/// (예전에 "필요하다"고 적어 두고 DT_SELECT 의 4번째 인수와 XML_GET 의
	///  출력 배열 형태를 빼먹었는데, 그 결과 그 형태를 쓰는 게임이
	///  "인수가 너무 많습니다" / "3번째 인수가 숫자가 아닙니다" 로 멈췄다.)
	/// 헬퍼는 EmMethodBase 의 OutVar / WriteOut 이다.
	/// </summary>
	internal static partial class FunctionMethodCreator
	{
		/// <summary>Creator.cs 의 정적 초기화 마지막에서 호출된다.</summary>
		private static void AddEmMethods(Dictionary<string, FunctionMethod> list)
		{
			// --- MAP ---------------------------------------------------------
			list["MAP_CREATE"] = new EmMapCreateMethod();
			list["MAP_EXIST"] = new EmMapExistMethod();
			list["MAP_RELEASE"] = new EmMapReleaseMethod();
			list["MAP_CLEAR"] = new EmMapClearMethod();
			list["MAP_GET"] = new EmMapGetMethod();
			list["MAP_HAS"] = new EmMapHasMethod();
			list["MAP_SET"] = new EmMapSetMethod();
			list["MAP_REMOVE"] = new EmMapRemoveMethod();
			list["MAP_SIZE"] = new EmMapSizeMethod();

			// --- 기타 ---------------------------------------------------------
			list["EXISTFUNCTION"] = new EmExistFunctionMethod();
			list["REGEXPMATCH"] = new EmRegexpMatchMethod();

			// --- 이름으로 변수 다루기 (리플렉션) ---------------------------------
			list["EXISTVAR"] = new EmExistVarMethod();
			list["GETVAR"] = new EmGetVarMethod(wantStr: false);
			list["GETVARS"] = new EmGetVarMethod(wantStr: true);
			list["SETVAR"] = new EmSetVarMethod();
			list["EXISTMETH"] = new EmExistMethMethod();
			list["HTML_STRINGLEN"] = new EmHtmlStringLenMethod();

			// --- DataTable ----------------------------------------------------
			list["DT_CREATE"] = new EmDtNameMethod(EmDtNameMethod.Op.Create);
			list["DT_EXIST"] = new EmDtNameMethod(EmDtNameMethod.Op.Exist);
			list["DT_RELEASE"] = new EmDtNameMethod(EmDtNameMethod.Op.Release);
			list["DT_CLEAR"] = new EmDtNameMethod(EmDtNameMethod.Op.Clear);
			list["DT_ROW_LENGTH"] = new EmDtNameMethod(EmDtNameMethod.Op.RowLength);
			list["DT_COLUMN_LENGTH"] = new EmDtNameMethod(EmDtNameMethod.Op.ColumnLength);
			list["DT_NOCASE"] = new EmDtNoCaseMethod();
			list["DT_COLUMN_ADD"] = new EmDtColumnAddMethod();
			list["DT_COLUMN_EXIST"] = new EmDtColumnMethod(EmDtColumnMethod.Op.Exist);
			list["DT_COLUMN_REMOVE"] = new EmDtColumnMethod(EmDtColumnMethod.Op.Remove);
			list["DT_COLUMN_OPTIONS"] = new EmDtColumnOptionsMethod();
			list["DT_ROW_ADD"] = new EmDtRowWriteMethod(isAdd: true);
			list["DT_ROW_SET"] = new EmDtRowWriteMethod(isAdd: false);
			list["DT_ROW_REMOVE"] = new EmDtRowRemoveMethod();
			list["DT_CELL_GET"] = new EmDtCellGetMethod();
			list["DT_CELL_GETS"] = new EmDtCellGetsMethod();
			list["DT_CELL_ISNULL"] = new EmDtCellGetMethod(isNullCheck: true);
			list["DT_CELL_SET"] = new EmDtCellSetMethod();
			list["DT_SELECT"] = new EmDtSelectMethod();

			// --- XML ----------------------------------------------------------
			list["XML_DOCUMENT"] = new EmXmlDocumentMethod();
			list["XML_EXIST"] = new EmXmlNameMethod(EmXmlNameMethod.Op.Exist);
			list["XML_RELEASE"] = new EmXmlNameMethod(EmXmlNameMethod.Op.Release);
			list["XML_TOSTR"] = new EmXmlToStrMethod();
			list["XML_GET"] = new EmXmlGetMethod(byName: false);
			list["XML_GET_BYNAME"] = new EmXmlGetMethod(byName: true);
			list["XML_SET"] = new EmXmlSetMethod();
			list["XML_SET_BYNAME"] = new EmXmlSetMethod();
			list["XML_ADDNODE"] = new EmXmlAddNodeMethod();
			list["XML_ADDNODE_BYNAME"] = new EmXmlAddNodeMethod();
			list["XML_REMOVENODE"] = new EmXmlRemoveNodeMethod();
			list["XML_ADDATTRIBUTE"] = new EmXmlAttributeMethod(add: true);
			list["XML_REMOVEATTRIBUTE"] = new EmXmlAttributeMethod(add: false);

			// --- 오디오 --------------------------------------------------------
			list["PLAYBGM"] = new EmAudioMethod(EmAudioMethod.Op.PlayBgm);
			list["PLAYSOUND"] = new EmAudioMethod(EmAudioMethod.Op.PlaySound);
			list["STOPBGM"] = new EmAudioMethod(EmAudioMethod.Op.StopBgm);
			list["STOPSOUND"] = new EmAudioMethod(EmAudioMethod.Op.StopSound);
			list["SETBGMVOLUME"] = new EmAudioMethod(EmAudioMethod.Op.SetBgmVolume);
			list["SETSOUNDVOLUME"] = new EmAudioMethod(EmAudioMethod.Op.SetSoundVolume);
			list["EXISTSOUND"] = new EmAudioMethod(EmAudioMethod.Op.ExistSound);

			// --- MATH_EXTENSION ----------------------------------------------
			list["CBRT"] = new EmMathMethod(EmMathMethod.Kind.Cbrt);
			list["LOG"] = new EmMathMethod(EmMathMethod.Kind.Log);
			list["LOG10"] = new EmMathMethod(EmMathMethod.Kind.Log10);
			list["EXPONENT"] = new EmMathMethod(EmMathMethod.Kind.Exponent);
		}

		// =====================================================================
		// 인수 검사를 재사용하기 위한 공통 기반
		// =====================================================================

		/// <summary>
		/// 문자열 인수 n개(선택 인수 포함)를 받는 함수의 공통 검사.
		/// 엔진의 기본 CheckArgumentType 은 개수가 정확히 맞아야 하므로
		/// 선택 인수가 있는 EM 함수에는 쓸 수 없다.
		/// </summary>
		private abstract class EmMethodBase : FunctionMethod
		{
			protected int MinArgs;
			protected int MaxArgs;
			/// <summary>각 자리의 요구 타입. null 이면 아무 타입이나 허용.</summary>
			protected Type?[]? ArgTypes;

			public override string CheckArgumentType(string name, IOperandTerm[] arguments)
			{
				if (arguments.Length < MinArgs)
					return $"{name} 함수는 인수가 최소 {MinArgs}개 필요합니다";
				if (arguments.Length > MaxArgs)
					return $"{name} 함수의 인수가 너무 많습니다";
				for (int i = 0; i < arguments.Length; i++)
				{
					if (arguments[i] == null)
					{
						// 선택 인수 자리는 생략(null)을 허용한다
						if (i < MinArgs)
							return $"{name} 함수의 {i + 1}번째 인수는 생략할 수 없습니다";
						continue;
					}
					var want = ArgTypes != null && i < ArgTypes.Length ? ArgTypes[i] : null;
					if (want != null && arguments[i].GetOperandType() != want)
						return want == typeof(string)
							? $"{name} 함수의 {i + 1}번째 인수가 문자열이 아닙니다"
							: $"{name} 함수의 {i + 1}번째 인수가 숫자가 아닙니다";
				}
				return null;
			}

			protected static string Str(ExpressionMediator exm, IOperandTerm[] a, int i)
				=> i < a.Length && a[i] != null ? a[i].GetStrValue(exm) ?? "" : "";

			protected static long Int(ExpressionMediator exm, IOperandTerm[] a, int i, long def)
				=> i < a.Length && a[i] != null ? a[i].GetIntValue(exm) : def;

			// -----------------------------------------------------------------
			// 출력(ref) 인수
			//
			// EM 규격에서 DT_SELECT 의 output, XML_GET 의 outputArray,
			// REGEXPMATCH 의 groupCount/matches, MAP_GETKEYS 의 출력은 모두
			// "호출자의 변수에 결과를 써넣는" 인수다.
			//
			// 표현식 인수로 넘어온 변수는 VariableTerm 이므로 그대로 쓸 수 있다.
			// 상수와 일반 식은 쓸 수 없으므로 걸러낸다.
			// -----------------------------------------------------------------

			/// <summary>i 번째 인수가 쓸 수 있는 출력 변수면 그것을, 아니면 null.</summary>
			protected static VariableTerm? OutVar(IOperandTerm[] a, int i)
			{
				if (i >= a.Length || a[i] == null)
					return null;
				if (a[i] is not VariableTerm v)
					return null;
				if (v.Identifier == null || v.Identifier.IsConst)
					return null;   // 상수에는 쓸 수 없다
				return v;
			}

			/// <summary>
			/// 출력 인수 자리를 검증한다. 문제가 없으면 null, 있으면 오류 문구.
			/// </summary>
			protected static string? CheckOutVar(
				string name, IOperandTerm[] a, int i, bool wantStr)
			{
				if (i >= a.Length || a[i] == null)
					return null;    // 생략은 허용(호출부가 필수 여부를 정한다)
				var v = OutVar(a, i);
				if (v == null)
					return $"{name} 함수의 {i + 1}번째 인수는 대입할 수 있는 변수여야 합니다";
				bool isStr = v.GetOperandType() == typeof(string);
				if (isStr != wantStr)
					return wantStr
						? $"{name} 함수의 {i + 1}번째 인수는 문자열 변수여야 합니다"
						: $"{name} 함수의 {i + 1}번째 인수는 숫자 변수여야 합니다";
				return null;
			}

			/// <summary>출력 변수에 스칼라 하나를 쓴다.</summary>
			protected static void WriteOut(
				ExpressionMediator exm, IOperandTerm[] a, int i, long value)
				=> OutVar(a, i)?.SetValue(value, exm);

			/// <summary>
			/// 출력 배열 변수에 순서대로 쓴다. 실제로 쓴 개수를 돌려준다.
			///
			/// 규격이 "반환값이 출력 배열의 요소 수를 넘을 수 있다"고 명시하므로
			/// <b>넘치면 예외가 아니라 잘라낸다.</b> 그대로 SetValue 에 넘기면
			/// 엔진이 "배열 변수의 요소 수를 넘겨 대입하려 했습니다"로 게임을
			/// 멈춘다.
			/// </summary>
			protected static int WriteOut(
				ExpressionMediator exm, IOperandTerm[] a, int i, IReadOnlyList<long> values)
			{
				var (fv, room) = OutRoom(exm, a, i);
				if (fv == null) return 0;
				int n = Math.Min(values.Count, room);
				if (n <= 0) return 0;
				var buf = new long[n];
				for (int k = 0; k < n; k++) buf[k] = values[k];
				fv.SetValue(buf, exm);
				return n;
			}

			/// <summary>문자열 배열용. 위와 같다.</summary>
			protected static int WriteOut(
				ExpressionMediator exm, IOperandTerm[] a, int i, IReadOnlyList<string> values)
			{
				var (fv, room) = OutRoom(exm, a, i);
				if (fv == null) return 0;
				int n = Math.Min(values.Count, room);
				if (n <= 0) return 0;
				var buf = new string[n];
				for (int k = 0; k < n; k++) buf[k] = values[k] ?? "";
				fv.SetValue(buf, exm);
				return n;
			}

			/// <summary>
			/// 출력 변수를 인덱스가 확정된 형태로 만들고, 그 위치부터 남은
			/// 칸 수를 함께 돌려준다.
			/// </summary>
			static (FixedVariableTerm? fv, int room) OutRoom(
				ExpressionMediator exm, IOperandTerm[] a, int i)
			{
				var v = OutVar(a, i);
				if (v == null) return (null, 0);
				var fv = v.GetFixedVariableTerm(exm);
				int cap = fv.GetLastLength();
				if (cap <= 0)
					return (null, 0);   // 배열이 아니면 배열 대입을 하지 않는다
				// 배열 대입은 마지막 차원의 인덱스부터 채워진다.
				long start = fv.Identifier.Dimension switch
				{
					<= 1 => fv.Index1,
					2 => fv.Index2,
					_ => fv.Index3,
				};
				if (start < 0) return (null, 0);
				int room = (int)Math.Max(0, cap - start);
				return (fv, room);
			}
		}

		/// <summary>문자열 인수를 받고 정수를 돌려주는 EM 함수.</summary>
		private abstract class EmIntMethod : EmMethodBase
		{
			protected EmIntMethod(int min, int max, params Type?[] types)
			{
				ReturnType = typeof(Int64);
				// 상태를 참조하므로 상수 접기(Restructure)를 허용하지 않는다.
				CanRestructure = false;
				MinArgs = min;
				MaxArgs = max;
				ArgTypes = types;
			}
		}

		/// <summary>문자열 인수를 받고 문자열을 돌려주는 EM 함수.</summary>
		private abstract class EmStrMethod : EmMethodBase
		{
			protected EmStrMethod(int min, int max, params Type?[] types)
			{
				ReturnType = typeof(string);
				CanRestructure = false;
				MinArgs = min;
				MaxArgs = max;
				ArgTypes = types;
			}
		}

		// =====================================================================
		// MAP
		// =====================================================================

		private sealed class EmMapCreateMethod : EmIntMethod
		{
			public EmMapCreateMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Create(Str(exm, a, 0));
		}

		private sealed class EmMapExistMethod : EmIntMethod
		{
			public EmMapExistMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Exists(Str(exm, a, 0));
		}

		private sealed class EmMapReleaseMethod : EmIntMethod
		{
			public EmMapReleaseMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Release(Str(exm, a, 0));
		}

		private sealed class EmMapClearMethod : EmIntMethod
		{
			public EmMapClearMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Clear(Str(exm, a, 0));
		}

		private sealed class EmMapGetMethod : EmStrMethod
		{
			public EmMapGetMethod() : base(2, 2, typeof(string), typeof(string)) { }
			public override string GetStrValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Get(Str(exm, a, 0), Str(exm, a, 1));
		}

		private sealed class EmMapHasMethod : EmIntMethod
		{
			public EmMapHasMethod() : base(2, 2, typeof(string), typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Has(Str(exm, a, 0), Str(exm, a, 1));
		}

		private sealed class EmMapSetMethod : EmIntMethod
		{
			// 값은 문자열/숫자 어느 쪽이든 받아 문자열로 저장한다.
			public EmMapSetMethod() : base(3, 3, typeof(string), typeof(string), null) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				string value = a[2].GetOperandType() == typeof(string)
					? a[2].GetStrValue(exm) ?? ""
					: a[2].GetIntValue(exm).ToString();
				return EmMapStore.Set(Str(exm, a, 0), Str(exm, a, 1), value);
			}
		}

		private sealed class EmMapRemoveMethod : EmIntMethod
		{
			public EmMapRemoveMethod() : base(2, 2, typeof(string), typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Remove(Str(exm, a, 0), Str(exm, a, 1));
		}

		private sealed class EmMapSizeMethod : EmIntMethod
		{
			public EmMapSizeMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmMapStore.Size(Str(exm, a, 0));
		}

		// =====================================================================
		// EXISTFUNCTION
		// =====================================================================

		private sealed class EmExistFunctionMethod : EmIntMethod
		{
			public EmExistFunctionMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = Str(exm, a, 0);
				if (string.IsNullOrEmpty(name)) return 0;
				var labelDic = GlobalStatic.Process?.LabelDictionary;
				if (labelDic == null) return 0;
				// 일반 함수와 이벤트 함수를 모두 본다.
				// 이벤트 함수(@EVENTCOM 등)는 같은 이름이 여러 개일 수 있어
				// 별도 조회 경로를 쓴다.
				if (labelDic.GetNonEventLabel(name) != null)
					return 1;
				var ev = labelDic.GetEventLabels(name);
				if (ev != null)
				{
					foreach (var list in ev)
						if (list != null && list.Count > 0)
							return 1;
				}
				return 0;
			}
		}

		// =====================================================================
		// HTML_STRINGLEN
		// =====================================================================

		private sealed class EmHtmlStringLenMethod : EmIntMethod
		{
			// HTML_STRINGLEN html(, returnPixel)
			public EmHtmlStringLenMethod() : base(1, 2, typeof(string), typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var html = Str(exm, a, 0);
				bool pixel = Int(exm, a, 1, 0) != 0;
				// 태그를 걷어낸 순수 텍스트의 길이를 센다.
				var plain = GameView.HtmlManager.Html2PlainText(html) ?? "";
				// 전각을 2로 세는 것이 Emuera 의 표시폭 규약이다.
				int width = uEmuera.Utils.GetByteCount(plain);
				if (!pixel)
					return width;
				// 픽셀 요청 시엔 표시 폭 × 반각 1글자 폭으로 환산한다.
				// (엔진 내부 폰트 메트릭이 Godot 쪽과 분리돼 있어 근사값이다)
				return width * Math.Max(1, Config.FontSize / 2);
			}
		}

		// =====================================================================
		// DataTable (DT_*)
		//
		// 값 반환형만 여기에 둔다. 출력 배열을 받는 형태
		// (DT_SELECT 의 output, DT_COLUMN_NAMES 의 outputArray)는 전용
		// ArgumentBuilder 가 필요해 아직 없다. DT_SELECT 는 문서가 정한
		// 「output 을 생략하면 RESULT:1 부터 대입」 쪽만 구현했다.
		// =====================================================================

		/// <summary>인수가 테이블 이름 하나뿐인 DT 함수.</summary>
		private sealed class EmDtNameMethod : EmIntMethod
		{
			internal enum Op { Create, Exist, Release, Clear, RowLength, ColumnLength }
			readonly Op op;
			public EmDtNameMethod(Op o) : base(1, 1, typeof(string)) { op = o; }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var n = Str(exm, a, 0);
				return op switch
				{
					Op.Create => EmDataTableStore.Create(n),
					Op.Exist => EmDataTableStore.Exists(n),
					Op.Release => EmDataTableStore.Release(n),
					Op.Clear => EmDataTableStore.Clear(n),
					Op.RowLength => EmDataTableStore.RowLength(n),
					Op.ColumnLength => EmDataTableStore.ColumnLength(n),
					_ => -1,
				};
			}
		}

		private sealed class EmDtNoCaseMethod : EmIntMethod
		{
			public EmDtNoCaseMethod() : base(2, 2, typeof(string), typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmDataTableStore.NoCase(Str(exm, a, 0), Int(exm, a, 1, 0));
		}

		/// <summary>DT_COLUMN_ADD name, column(, type, nullable)</summary>
		private sealed class EmDtColumnAddMethod : EmIntMethod
		{
			// type 은 문자열("int16")도 숫자(2)도 올 수 있어 타입을 고정하지 않는다.
			public EmDtColumnAddMethod()
				: base(2, 4, typeof(string), typeof(string), null, typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				string? typeName = null;
				long typeNo = 0;
				if (a.Length > 2 && a[2] != null)
				{
					if (a[2].GetOperandType() == typeof(string))
						typeName = a[2].GetStrValue(exm);
					else
						typeNo = a[2].GetIntValue(exm);
				}
				// nullable 기본값은 「0 이 아니면 허용」이고 미지정도 허용이다.
				long nullable = Int(exm, a, 3, 1);
				return EmDataTableStore.ColumnAdd(
					Str(exm, a, 0), Str(exm, a, 1), typeName, typeNo, nullable);
			}
		}

		private sealed class EmDtColumnMethod : EmIntMethod
		{
			internal enum Op { Exist, Remove }
			readonly Op op;
			public EmDtColumnMethod(Op o) : base(2, 2, typeof(string), typeof(string)) { op = o; }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var n = Str(exm, a, 0);
				var c = Str(exm, a, 1);
				return op == Op.Exist
					? EmDataTableStore.ColumnExist(n, c)
					: EmDataTableStore.ColumnRemove(n, c);
			}
		}

		/// <summary>DT_COLUMN_OPTIONS name, column, option, value(, option, value ...)</summary>
		private sealed class EmDtColumnOptionsMethod : EmIntMethod
		{
			public EmDtColumnOptionsMethod() : base(4, 64, typeof(string), typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var n = Str(exm, a, 0);
				var c = Str(exm, a, 1);
				long last = -1;
				for (int i = 2; i + 1 < a.Length; i += 2)
					last = EmDataTableStore.ColumnOption(n, c, Str(exm, a, i), AnyToStr(exm, a, i + 1));
				return last;
			}
		}

		/// <summary>
		/// DT_ROW_ADD name(, column, value) ...
		/// DT_ROW_SET name, idValue(, column, value) ...
		/// 형태 b(columnNames/columnValues 배열 + count)는 배열 인수를 받아야 해서
		/// 아직 지원하지 않는다.
		/// </summary>
		private sealed class EmDtRowWriteMethod : EmIntMethod
		{
			readonly bool isAdd;
			public EmDtRowWriteMethod(bool isAdd)
				: base(isAdd ? 1 : 2, 128, typeof(string)) { this.isAdd = isAdd; }

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = Str(exm, a, 0);
				int start = isAdd ? 1 : 2;
				var pairs = new List<KeyValuePair<string, string>>();
				for (int i = start; i + 1 < a.Length; i += 2)
					pairs.Add(new KeyValuePair<string, string>(
						Str(exm, a, i), AnyToStr(exm, a, i + 1)));
				return isAdd
					? EmDataTableStore.RowAdd(name, pairs)
					: EmDataTableStore.RowSet(name, Int(exm, a, 1, 0), pairs);
			}
		}

		private sealed class EmDtRowRemoveMethod : EmIntMethod
		{
			public EmDtRowRemoveMethod() : base(2, 2, typeof(string), typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmDataTableStore.RowRemove(Str(exm, a, 0), Int(exm, a, 1, 0));
		}

		/// <summary>DT_CELL_GET / DT_CELL_ISNULL name, row, column(, asId)</summary>
		private sealed class EmDtCellGetMethod : EmIntMethod
		{
			readonly bool isNullCheck;
			public EmDtCellGetMethod(bool isNullCheck = false)
				: base(3, 4, typeof(string), typeof(Int64), typeof(string), typeof(Int64))
			{
				this.isNullCheck = isNullCheck;
			}
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var n = Str(exm, a, 0);
				long row = Int(exm, a, 1, 0);
				var col = Str(exm, a, 2);
				long asId = Int(exm, a, 3, 0);
				return isNullCheck
					? EmDataTableStore.CellIsNull(n, row, col, asId)
					: EmDataTableStore.CellGetInt(n, row, col, asId);
			}
		}

		private sealed class EmDtCellGetsMethod : EmStrMethod
		{
			public EmDtCellGetsMethod()
				: base(3, 4, typeof(string), typeof(Int64), typeof(string), typeof(Int64)) { }
			public override string GetStrValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmDataTableStore.CellGetStr(
					Str(exm, a, 0), Int(exm, a, 1, 0), Str(exm, a, 2), Int(exm, a, 3, 0));
		}

		/// <summary>DT_CELL_SET name, row, column(, value, asId). value 생략은 null 대입.</summary>
		private sealed class EmDtCellSetMethod : EmIntMethod
		{
			public EmDtCellSetMethod()
				: base(3, 5, typeof(string), typeof(Int64), typeof(string), null, typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				string? value = a.Length > 3 && a[3] != null ? AnyToStr(exm, a, 3) : null;
				return EmDataTableStore.CellSet(
					Str(exm, a, 0), Int(exm, a, 1, 0), Str(exm, a, 2), value, Int(exm, a, 4, 0));
			}
		}

		/// <summary>
		/// DT_SELECT name(, filter, sort).
		/// 문서의 「output 을 생략하면 RESULT:1 부터 대입」 쪽만 구현했다.
		/// output 인수를 받는 형태는 전용 ArgumentBuilder 가 필요하다.
		/// </summary>
		private sealed class EmDtSelectMethod : EmIntMethod
		{
			// 규격: string(, string, string, ref int[])
			// 4번째가 출력 배열이다. 예전에는 3인수까지만 받아서, 문서대로
			// 출력 변수를 넘기는 게임이 "인수가 너무 많습니다"로 멈췄다.
			public EmDtSelectMethod()
				: base(1, 4, typeof(string), typeof(string), typeof(string), null) { }

			public override string CheckArgumentType(string name, IOperandTerm[] a)
			{
				var err = base.CheckArgumentType(name, a);
				if (err != null) return err;
				// 4번째는 정수 배열 변수여야 한다.
				return CheckOutVar(name, a, 3, wantStr: false);
			}

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var ids = EmDataTableStore.Select(
					Str(exm, a, 0),
					a.Length > 1 && a[1] != null ? Str(exm, a, 1) : null,
					a.Length > 2 && a[2] != null ? Str(exm, a, 2) : null);
				if (ids == null) return -1;

				if (OutVar(a, 3) != null)
				{
					// 출력 변수를 넘겼으면 그쪽에만 쓴다.
					WriteOut(exm, a, 3, ids);
					return ids.Count;
				}

				// 출력 변수가 없으면 문서대로 RESULT:1 부터 넣는다. SetResultX 는
				// 0번부터 쓰므로 선두에 자리표시자를 하나 넣어 한 칸 밀어준다.
				// RESULT:0 은 이 메서드가 끝난 뒤 METHOD_Instruction 이
				// 반환값으로 덮어쓰므로 자리표시자 값은 의미가 없다.
				var buf = new List<long>(ids.Count + 1) { 0 };
				buf.AddRange(ids);
				exm.VEvaluator.SetResultX(buf);
				return ids.Count;
			}
		}

		/// <summary>숫자든 문자열이든 문자열로 만든다. DT 는 열 타입에 맞춰 변환한다.</summary>
		private static string AnyToStr(ExpressionMediator exm, IOperandTerm[] a, int i)
		{
			if (i >= a.Length || a[i] == null) return "";
			return a[i].GetOperandType() == typeof(string)
				? a[i].GetStrValue(exm) ?? ""
				: a[i].GetIntValue(exm).ToString();
		}

		// =====================================================================
		// XML (XML_*)
		//
		// 문서의 네 형태를 모두 지원한다.
		//   1. XML_GET xml, xpath(, doOutput, outputType)
		//   2. XML_GET xml, xpath, ref string[] outputArray(, outputType)
		//   3·4. XML_GET_BYNAME 의 같은 두 형태
		// 3번째 인수가 문자열 변수면 형태 2·4, 아니면 형태 1·3 이다.
		// =====================================================================

		/// <summary>XML_DOCUMENT xmlId, xmlContent</summary>
		private sealed class EmXmlDocumentMethod : EmIntMethod
		{
			// xmlId 는 정수도 허용된다(TOSTR 되어 키가 된다).
			public EmXmlDocumentMethod() : base(2, 2, null, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmXmlStore.Create(AnyToStr(exm, a, 0), Str(exm, a, 1));
		}

		private sealed class EmXmlNameMethod : EmIntMethod
		{
			internal enum Op { Exist, Release }
			readonly Op op;
			public EmXmlNameMethod(Op o) : base(1, 1, (Type?)null) { op = o; }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var n = AnyToStr(exm, a, 0);
				return op == Op.Exist ? EmXmlStore.Exists(n) : EmXmlStore.Release(n);
			}
		}

		private sealed class EmXmlToStrMethod : EmStrMethod
		{
			public EmXmlToStrMethod() : base(1, 1, (Type?)null) { }
			public override string GetStrValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmXmlStore.ToStr(AnyToStr(exm, a, 0));
		}

		/// <summary>
		/// XML_GET xml, xpath(, doOutput, outputType)
		/// XML_GET_BYNAME xmlName, xpath(, doOutput, outputType)
		///
		/// XML_GET 은 첫 인수가 문자열이면 그 내용을 직접 파싱하고,
		/// 정수면 TOSTR 해서 보관된 문서의 키로 쓴다(EM 규격).
		/// XML_GET_BYNAME 은 항상 보관된 문서의 키다.
		/// </summary>
		private sealed class EmXmlGetMethod : EmIntMethod
		{
			readonly bool byName;
			// 3번째 자리는 형태에 따라 정수(doOutput) 또는 문자열 배열 변수
			// (outputArray) 이므로 타입을 고정하지 않는다(null = 아무 타입).
			public EmXmlGetMethod(bool byName)
				: base(2, 4, null, typeof(string), null, typeof(Int64))
			{
				this.byName = byName;
			}

			/// <summary>3번째 인수가 출력 배열 형태인지.</summary>
			static bool IsArrayForm(IOperandTerm[] a)
				=> a.Length > 2 && a[2] != null
					&& a[2].GetOperandType() == typeof(string);

			public override string CheckArgumentType(string name, IOperandTerm[] a)
			{
				var err = base.CheckArgumentType(name, a);
				if (err != null) return err;
				if (!IsArrayForm(a))
				{
					// 형태 1·3: 3번째는 정수식이어야 한다.
					if (a.Length > 2 && a[2] != null
						&& a[2].GetOperandType() != typeof(Int64))
						return $"{name} 함수의 3번째 인수는 정수 또는 문자열 배열 변수여야 합니다";
					return null;
				}
				// 형태 2·4: 3번째는 문자열 배열 변수여야 한다.
				return CheckOutVar(name, a, 2, wantStr: true);
			}

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var xpath = Str(exm, a, 1);
				bool arrayForm = IsArrayForm(a);
				// 형태 2·4 에서는 outputType 이 4번째, 형태 1·3 에서는 4번째가
				// outputType, 3번째가 doOutput 이다.
				// outputType 은 두 형태 모두 4번째 인수다.
				long outputType = Int(exm, a, 3, 0);
				long doOutput = arrayForm ? 1 : Int(exm, a, 2, 0);

				List<string>? hits;
				if (!byName && a[0].GetOperandType() == typeof(string))
					hits = EmXmlStore.GetFromContent(Str(exm, a, 0), xpath, outputType);
				else
					hits = EmXmlStore.Get(AnyToStr(exm, a, 0), xpath, outputType);

				if (hits == null) return -1;

				if (arrayForm)
				{
					WriteOut(exm, a, 2, hits);
				}
				else if (doOutput != 0)
				{
					// RESULTS 배열에 순서대로 넣는다. 배열 크기를 넘기지 않는다.
					var arr = exm.VEvaluator.RESULTS_ARRAY;
					int n = Math.Min(hits.Count, arr.Length);
					for (int i = 0; i < n; i++) arr[i] = hits[i];
				}
				return hits.Count;
			}
		}

		/// <summary>XML_SET(_BYNAME) xmlName, xpath, value(, doSetAll, outputType)</summary>
		private sealed class EmXmlSetMethod : EmIntMethod
		{
			public EmXmlSetMethod()
				: base(3, 5, null, typeof(string), null, typeof(Int64), typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmXmlStore.Set(AnyToStr(exm, a, 0), Str(exm, a, 1), AnyToStr(exm, a, 2),
					Int(exm, a, 3, 0), Int(exm, a, 4, 0));
		}

		/// <summary>XML_ADDNODE(_BYNAME) xmlName, xpath, nodeXml(, methodType, doSetAll)</summary>
		private sealed class EmXmlAddNodeMethod : EmIntMethod
		{
			public EmXmlAddNodeMethod()
				: base(3, 5, null, typeof(string), typeof(string), typeof(Int64), typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmXmlStore.AddNode(AnyToStr(exm, a, 0), Str(exm, a, 1), Str(exm, a, 2),
					Int(exm, a, 3, 0), Int(exm, a, 4, 0));
		}

		/// <summary>XML_REMOVENODE xmlName, xpath(, doSetAll)</summary>
		private sealed class EmXmlRemoveNodeMethod : EmIntMethod
		{
			public EmXmlRemoveNodeMethod()
				: base(2, 3, null, typeof(string), typeof(Int64)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> EmXmlStore.RemoveNode(AnyToStr(exm, a, 0), Str(exm, a, 1), Int(exm, a, 2, 0));
		}

		/// <summary>
		/// XML_ADDATTRIBUTE xmlName, xpath, name, value(, doSetAll)
		/// XML_REMOVEATTRIBUTE xmlName, xpath, name(, doSetAll)
		/// </summary>
		private sealed class EmXmlAttributeMethod : EmIntMethod
		{
			readonly bool add;
			public EmXmlAttributeMethod(bool add)
				: base(3, 5, null, typeof(string), typeof(string), null, typeof(Int64))
			{
				this.add = add;
			}
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = AnyToStr(exm, a, 0);
				var xpath = Str(exm, a, 1);
				var attr = Str(exm, a, 2);
				return add
					? EmXmlStore.AddAttribute(name, xpath, attr, AnyToStr(exm, a, 3), Int(exm, a, 4, 0))
					: EmXmlStore.RemoveAttribute(name, xpath, attr, Int(exm, a, 3, 0));
			}
		}

		// =====================================================================
		// 오디오
		//
		// 실제 재생은 EmAudio 가 Godot 메인 스레드에서 한다. era 명령은 엔진
		// 스레드에서 실행되므로 여기서는 요청만 큐에 넣는다.
		// =====================================================================

		private sealed class EmAudioMethod : EmIntMethod
		{
			internal enum Op
			{
				PlayBgm, PlaySound, StopBgm, StopSound,
				SetBgmVolume, SetSoundVolume, ExistSound,
			}
			readonly Op op;

			public EmAudioMethod(Op o) : base(MinOf(o), MaxOf(o), TypeOf(o)) { op = o; }

			static int MinOf(Op o) => o switch
			{
				Op.StopBgm or Op.StopSound => 0,
				_ => 1,
			};
			static int MaxOf(Op o) => o switch
			{
				Op.StopBgm or Op.StopSound => 0,
				_ => 1,
			};
			static Type? TypeOf(Op o) => o switch
			{
				Op.SetBgmVolume or Op.SetSoundVolume => typeof(Int64),
				Op.StopBgm or Op.StopSound => null,
				_ => typeof(string),
			};

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
				=> op switch
				{
					Op.PlayBgm => EmAudio.PlayBgm(Str(exm, a, 0)),
					Op.PlaySound => EmAudio.PlaySound(Str(exm, a, 0)),
					Op.StopBgm => EmAudio.StopBgm(),
					Op.StopSound => EmAudio.StopSound(),
					Op.SetBgmVolume => EmAudio.SetBgmVolume(Int(exm, a, 0, 100)),
					Op.SetSoundVolume => EmAudio.SetSoundVolume(Int(exm, a, 0, 100)),
					Op.ExistSound => EmAudio.ExistSound(Str(exm, a, 0)),
					_ => 0,
				};
		}

		// =====================================================================
		// 이름으로 변수·표현식 함수 다루기
		//
		//   int    EXISTVAR  varName
		//   int    GETVAR    varName
		//   string GETVARS   varName
		//   1      SETVAR    varName, value
		//   int    EXISTMETH functionName
		//
		// 엔진의 이름 조회는 IdentifierDictionary.GetVariableToken 이 이미
		// 다 해준다. 프라이빗 변수(함수 안의 #DIM)는 현재 실행 중인 함수
		// 범위에서만 보이고, 그 판단도 그 함수가 한다. 규격 문서의 예제도
		// 다른 함수의 #DIMS 는 보이지 않는다고 명시한다.
		// =====================================================================

		/// <summary>이름으로 변수 토큰을 찾는다. 없으면 null. 예외를 밖으로 내지 않는다.</summary>
		private static VariableToken? FindVar(string name)
		{
			if (string.IsNullOrEmpty(name))
				return null;
			var dic = GlobalStatic.IdentifierDictionary;
			if (dic == null)
				return null;
			try
			{
				// subKey=null, allowPrivate=true 로 현재 함수의 프라이빗 변수까지 본다.
				return dic.GetVariableToken(name, null, true);
			}
			catch (CodeEE)
			{
				// 설정으로 사용이 금지된 변수, 실행 중인 함수가 없어 LOCAL 을
				// 해석할 수 없는 경우 등. "없다"로 취급한다. 여기서 예외를
				// 올리면 EXISTVAR 로 존재를 확인하는 코드가 오히려 멈춘다.
				return null;
			}
		}

		/// <summary>인덱스가 필요한 변수도 0번 요소를 본다.</summary>
		static readonly Int64[] ZeroIndex = new Int64[] { 0, 0, 0 };

		/// <summary>
		/// EXISTVAR: 정의돼 있으면 종류에 따른 비트를 세운 양수, 없으면 0.
		///
		/// 문서의 "비트 N" 은 값 2^(N-1) 이다(문서 예제의 BIT 상수 배열로 확인).
		///   정수형 → 1, 문자열형 → 2, 상수 → 4, 2차원 배열 → 8, 3차원 배열 → 16
		/// 게임 쪽에서는 GETBIT(EXISTVAR(X), 0) 으로 "정수형인가"를 본다.
		/// </summary>
		private sealed class EmExistVarMethod : EmIntMethod
		{
			public EmExistVarMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var v = FindVar(Str(exm, a, 0));
				if (v == null) return 0;
				long bits = v.IsInteger ? 1 : 2;
				if (v.IsConst) bits |= 4;
				if (v.IsArray2D) bits |= 8;
				if (v.IsArray3D) bits |= 16;
				return bits;
			}
		}

		/// <summary>GETVAR / GETVARS: 이름으로 값을 읽는다.</summary>
		private sealed class EmGetVarMethod : EmMethodBase
		{
			readonly bool wantStr;
			public EmGetVarMethod(bool wantStr)
			{
				this.wantStr = wantStr;
				ReturnType = wantStr ? typeof(string) : typeof(Int64);
				CanRestructure = false;
				MinArgs = 1;
				MaxArgs = 1;
				ArgTypes = new Type?[] { typeof(string) };
			}

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = Str(exm, a, 0);
				var v = FindVar(name);
				if (v == null)
					throw new CodeEE($"GETVAR: \"{name}\"은(는) 해석할 수 없는 식별자입니다");
				if (!v.IsInteger)
					throw new CodeEE($"GETVAR: \"{name}\"은(는) 정수형이 아닙니다");
				return v.GetIntValue(exm, ZeroIndex);
			}

			public override string GetStrValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = Str(exm, a, 0);
				var v = FindVar(name);
				if (v == null)
					throw new CodeEE($"GETVARS: \"{name}\"은(는) 해석할 수 없는 식별자입니다");
				if (v.IsInteger)
					throw new CodeEE($"GETVARS: \"{name}\"은(는) 문자열형이 아닙니다");
				return v.GetStrValue(exm, ZeroIndex) ?? "";
			}
		}

		/// <summary>SETVAR varName, value — 항상 1을 돌려준다. 상수에는 쓸 수 없다.</summary>
		private sealed class EmSetVarMethod : EmIntMethod
		{
			// 두 번째 인수는 대상 변수의 타입에 맞춰야 하므로 여기서는 고정하지 않는다.
			public EmSetVarMethod() : base(2, 2, typeof(string), null) { }

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = Str(exm, a, 0);
				var v = FindVar(name);
				if (v == null)
					throw new CodeEE($"SETVAR: \"{name}\"은(는) 해석할 수 없는 식별자입니다");
				if (v.IsConst)
					throw new CodeEE($"SETVAR: \"{name}\"은(는) 상수이므로 대입할 수 없습니다");

				bool valueIsStr = a[1] != null && a[1].GetOperandType() == typeof(string);
				if (v.IsInteger)
				{
					if (valueIsStr)
						throw new CodeEE($"SETVAR: \"{name}\"은(는) 정수형인데 문자열을 대입하려 했습니다");
					v.SetValue(a[1].GetIntValue(exm), ZeroIndex);
				}
				else
				{
					if (!valueIsStr)
						throw new CodeEE($"SETVAR: \"{name}\"은(는) 문자열형인데 숫자를 대입하려 했습니다");
					v.SetValue(a[1].GetStrValue(exm) ?? "", ZeroIndex);
				}
				return 1;
			}
		}

		/// <summary>
		/// EXISTMETH: #FUNCTION 이면 1, #FUNCTIONS 이면 2, 없으면 0.
		/// </summary>
		private sealed class EmExistMethMethod : EmIntMethod
		{
			public EmExistMethMethod() : base(1, 1, typeof(string)) { }
			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var name = Str(exm, a, 0);
				if (string.IsNullOrEmpty(name)) return 0;
				var labelDic = GlobalStatic.LabelDictionary;
				if (labelDic == null) return 0;
				var label = labelDic.GetNonEventLabel(name);
				if (label == null || !label.IsMethod) return 0;
				return label.MethodType == typeof(string) ? 2 : 1;
			}
		}

		// =====================================================================
		// REGEXPMATCH
		//
		// 규격(두 형태):
		//   1. int REGEXPMATCH str, pattern(, output)
		//   2. int REGEXPMATCH str, pattern, ref groupCount, ref matches
		//
		// 반환값은 매치 수. 매치 결과는 "그룹 수 × 매치 수" 개가
		// matches:(i*groupCount + j) 순서로 들어간다(j=0 은 매치 전체).
		// 형태 1 에서 output 이 0 이 아니면 groupCount 는 RESULT:1,
		// 결과는 RESULTS 에 들어간다.
		// =====================================================================

		private sealed class EmRegexpMatchMethod : EmIntMethod
		{
			// 3번째는 형태 1 이면 정수식, 형태 2 이면 정수 변수다.
			// 4번째가 있으면 형태 2 이고 문자열 배열 변수여야 한다.
			public EmRegexpMatchMethod()
				: base(2, 4, typeof(string), typeof(string), null, null) { }

			static bool IsRefForm(IOperandTerm[] a) => a.Length > 3 && a[3] != null;

			public override string CheckArgumentType(string name, IOperandTerm[] a)
			{
				var err = base.CheckArgumentType(name, a);
				if (err != null) return err;
				if (!IsRefForm(a))
				{
					if (a.Length > 2 && a[2] != null
						&& a[2].GetOperandType() != typeof(Int64))
						return $"{name} 함수의 3번째 인수가 숫자가 아닙니다";
					return null;
				}
				// 형태 2 는 3번째가 필수다(생략하면 어디에 그룹 수를 쓸지 없다).
				if (a[2] == null)
					return $"{name} 함수의 3번째 인수는 생략할 수 없습니다";
				return CheckOutVar(name, a, 2, wantStr: false)
					?? CheckOutVar(name, a, 3, wantStr: true);
			}

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				var input = Str(exm, a, 0);
				var pattern = Str(exm, a, 1);

				System.Text.RegularExpressions.MatchCollection ms;
				try
				{
					ms = GetRegex(pattern).Matches(input);
				}
				catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
				{
					// 파멸적 백트래킹으로 엔진 스레드가 멈추는 것을 막는다.
					return 0;
				}
				catch (ArgumentException)
				{
					// 잘못된 정규식. 게임을 죽이지 않고 "매치 없음"으로 둔다.
					return 0;
				}

				int count = ms.Count;
				if (count == 0)
				{
					if (IsRefForm(a))
						WriteOut(exm, a, 2, 0L);
					else if (Int(exm, a, 2, 0) != 0)
						exm.VEvaluator.SetResultX(new List<long> { 0, 0 });
					return 0;
				}

				// 그룹 수는 매치 전체(그룹 0)를 포함한다.
				int groupCount = ms[0].Groups.Count;
				var flat = new List<string>(groupCount * count);
				for (int i = 0; i < count; i++)
					for (int j = 0; j < groupCount; j++)
						flat.Add(j < ms[i].Groups.Count ? ms[i].Groups[j].Value : "");

				if (IsRefForm(a))
				{
					WriteOut(exm, a, 2, (long)groupCount);
					WriteOut(exm, a, 3, flat);
				}
				else if (Int(exm, a, 2, 0) != 0)
				{
					// RESULT:1 에 그룹 수. RESULT:0 은 이 메서드가 끝난 뒤
					// METHOD_Instruction 이 반환값으로 덮어쓴다.
					exm.VEvaluator.SetResultX(new List<long> { 0, groupCount });
					var arr = exm.VEvaluator.RESULTS_ARRAY;
					int n = Math.Min(flat.Count, arr.Length);
					for (int i = 0; i < n; i++) arr[i] = flat[i];
				}
				return count;
			}

			// 같은 패턴을 루프에서 반복 호출하는 게임이 많아 컴파일 결과를
			// 재사용한다. 무한히 늘지 않게 상한을 둔다.
			const int CacheMax = 64;
			static readonly Dictionary<string, System.Text.RegularExpressions.Regex> cache = new();

			static System.Text.RegularExpressions.Regex GetRegex(string pattern)
			{
				if (cache.TryGetValue(pattern, out var re))
					return re;
				re = new System.Text.RegularExpressions.Regex(
					pattern,
					System.Text.RegularExpressions.RegexOptions.None,
					TimeSpan.FromSeconds(1));
				if (cache.Count >= CacheMax)
					cache.Clear();
				cache[pattern] = re;
				return re;
			}
		}

		// =====================================================================
		// MATH_EXTENSION
		// =====================================================================

		private sealed class EmMathMethod : EmIntMethod
		{
			internal enum Kind { Cbrt, Log, Log10, Exponent }
			readonly Kind kind;

			public EmMathMethod(Kind k) : base(1, 1, typeof(Int64))
			{
				kind = k;
				// 인수가 상수면 접어도 안전하다(상태를 참조하지 않음).
				CanRestructure = true;
			}

			public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] a)
			{
				long v = a[0].GetIntValue(exm);
				double d = kind switch
				{
					Kind.Cbrt => Math.Cbrt(v),
					Kind.Log => v <= 0 ? 0 : Math.Log(v),
					Kind.Log10 => v <= 0 ? 0 : Math.Log10(v),
					Kind.Exponent => Math.Exp(v),
					_ => 0,
				};
				if (double.IsNaN(d) || double.IsInfinity(d))
					return 0;
				// EM 의 반환형이 int 이므로 잘라낸다.
				if (d >= long.MaxValue) return long.MaxValue;
				if (d <= long.MinValue) return long.MinValue;
				return (long)d;
			}
		}
	}
}
