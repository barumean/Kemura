# Emuera EM+EE 확장 명령 대응 검토

출처: <https://gitlab.com/EvilMask/emuera.em.doc> (EvilMask 판 Emuera = **EM+EE**)
문서 사이트: <https://evilmask.gitlab.io/emuera.em.doc/en/>

이 문서는 **무엇을 얼마나 들여 구현할 수 있는지** 판단하기 위한 검토 결과입니다.
구현 계획이 아니고, 아직 아무것도 구현하지 않았습니다.

## 현재 위치

| 항목 | 값 |
|---|---|
| Kemura 엔진 기반 | Emuera **1.824** 계열 |
| `BuiltInFunctionCode` 항목 | 266 |
| 등록된 명령(`addFunction`) | 192 |
| EM 문서의 Reference 항목 | **279** |

### 확장 지점

새 명령을 넣는 비용은 낮습니다. 기존 구조가 그대로 받아줍니다.

- **문(statement) 명령**
  1. `Scripts/Emuera/GameProc/Function/BuiltInFunctionCode.cs` 에 enum 추가
  2. `FunctionIdentifier.cs` 에 `addFunction(FunctionCode.X, new X_Instruction())`
  3. `Instraction.Child.cs` 에 `AbstractInstruction` 파생 클래스
- **표현식 함수**
  1. `Scripts/Emuera/GameData/Function/Creator.cs` 의 딕셔너리에 `["X"] = new XMethod()`
  2. `Method` 파생 클래스

EM 문서는 대부분의 확장을 "명령과 표현식 함수 양쪽 지원"으로 규정하므로
보통 두 곳 모두 등록해야 합니다.

## 핵심 발견 — 비용이 생각보다 낮습니다

EM 은 자체 의미를 **.NET BCL 타입으로 직접 규정**합니다. net9.0 에 이미 있는
것들이라 밑바닥부터 만들 필요가 없습니다.

| EM 명령군 | 문서가 지정한 구현 근거 |
|---|---|
| `DT_*` | `System.Data.DataTable`. `DT_SELECT` 는 **`DataTable.Select` 그대로** — 필터식/정렬 문법이 공짜로 따라옵니다. `DT_TOXML`/`DT_FROMXML` → `WriteXml`/`ReadXml` |
| `XML_*` | `System.Xml` + XPath (`SelectNodes`) |
| `MAP_*` | `Dictionary<string,string>` |
| `REGEXPMATCH` | `System.Text.RegularExpressions` |
| 오디오 | Godot `AudioStreamPlayer` — WinForms 보다 오히려 쉽습니다 |

앞서 제가 "별도의 큰 작업"이라고만 말한 것은 근거가 부족했습니다.
`DT_SELECT` 가 SQL 유사 질의 엔진을 새로 만드는 일이 아니라 BCL 바인딩이라는
점이 비용 판단을 크게 바꿉니다.

## 비용 계층

### 1계층 — 저렴 (BCL 위임 + 등록만)

| 명령 | 비고 |
|---|---|
| `MAP_CREATE` `MAP_EXIST` `MAP_RELEASE` `MAP_CLEAR` | 반환값 규약까지 문서에 명시 |
| `MAP_GET` `MAP_HAS` `MAP_SET` `MAP_REMOVE` `MAP_SIZE` | 맵 없으면 `-1`, `GET` 은 빈 문자열 |
| `MAP_GETKEYS` `MAP_TOXML` `MAP_FROMXML` | |
| `REGEXPMATCH` | |
| `EXISTFUNCTION` | `LabelDictionary` 조회 |
| `HTML_STRINGLEN` | `HTML_PRINT` 은 이미 구현되어 있음 |
| `CBRT` `LOG` `LOG10` `EXPONENT` | `MATH_EXTENSION` |
| `LOADTEXT` 문자열 경로 오버로드 | 현재는 `int` 만. 확장자 허용 목록 config 항목도 함께 필요 |

### 2계층 — 보통

- `XML_DOCUMENT` `XML_RELEASE` `XML_EXIST` `XML_GET(_BYNAME)` `XML_SET(_BYNAME)`
  `XML_ADDNODE(_BYNAME)` `XML_REMOVENODE` `XML_REPLACE` `XML_TOSTR`
  `XML_ADDATTRIBUTE` `XML_REMOVEATTRIBUTE`
- 오디오: `PLAYBGM` `PLAYSOUND` `STOPBGM` `STOPSOUND` `SETBGMVOLUME` `SETSOUNDVOLUME` `EXISTSOUND`
- 리플렉션: `GETVAR` `GETVARS` `SETVAR` `GETMETH` `GETMETHS` `EXISTVAR` `EXISTMETH`
  — 엔진의 `varTokenDic` / `LabelDictionary` 를 이름으로 훑어야 하므로 신중해야 합니다

### 3계층 — 큼

`DT_*` 약 20종. 컬럼 타입(`int8`/`int16`/`int64`/`string` ...), `nullable`,
`DT_COLUMN_OPTIONS` 의 옵션 집합, 행 `id` 의미(`asId` 인수), 직렬화까지 포함.
BCL 위임이라 난이도는 낮지만 표면적이 넓습니다.

### 4계층 — UI/그래픽에 묶임

`MOUSEB` `GETTEXTBOX` `INPUTMOUSEKEY` `TOOLTIP_*`,
`GCREATE`/`GDRAW*`/`SPRITE*` 계열.
**이미지 출력이 아직 스텁**이라(`Graphics.DrawImage` 가 로그만 남김)
이 계층은 그래픽 파이프라인 작업이 먼저입니다.

## 반드시 짚어야 할 위험

1. **Android 트리밍/AOT.** `System.Data.DataTable` 의 필터식 파서와
   XPath 는 리플렉션에 의존합니다. Android 내보내기에서 트리밍이 걸리면
   런타임에 깨질 수 있습니다. **미검증 — 실기에서 확인해야 합니다.**
   데스크톱에서 되는 것만 보고 판단하면 안 됩니다.
2. **세이브 호환성.** EM 에서 `MAP`/`XML`/`DataTable` 은 세이브 데이터에
   직렬화됩니다("XML, MAP, DataTable Save Function"). 다르게 구현하면
   EM 과 세이브가 호환되지 않습니다. 또 "타이틀로 돌아가기"와 `RESETDATA`
   시 자동 삭제되는 규약도 맞춰야 합니다.
3. **명령만 채워도 부족할 수 있습니다.** EM+EE 는 코어 거동도 바꿨습니다.
   (`ERH` 정의 배열에 `CSV`/`ERD` 로 이름 부여, `VariableSize.csv` 의 `COUNT`
   제한 변수 취급 등) 특정 게임이 이런 것에 의존하면 명령 구현만으로는
   안 돌아갑니다.
4. **`TIMESF` 는 이 문서 세트에서 찾지 못했습니다.** 279개 Reference 목록에
   없습니다. 다른 계열의 확장일 가능성이 있어 별도 확인이 필요합니다.

## 실패한 게임(업로드된 emuera.log)에 대한 판단

그 로그의 게임은 **1~4계층에 걸쳐 전부** 필요합니다.

| 계층 | 그 게임이 쓰는 것 |
|---|---|
| 1 | `MAP_*` 7종, `REGEXPMATCH`, `EXISTFUNCTION`, `HTML_STRINGLEN`, `LOADTEXT` 문자열 |
| 2 | `XML_GET/ADDNODE/REMOVENODE`, `PLAYBGM/STOPBGM/STOP·SETVOLUME`, `GETVAR(S)`/`GETMETH(S)`/`EXISTVAR` |
| 3 | `DT_*` 13종 — **캐릭터 데이터 로딩 경로에서** 사용 |
| 4 | `GETTEXTBOX`, `MOUSEB` |

즉 이 게임은 점진적으로 도달할 수 없습니다. `DT_*` 를 캐릭터 데이터
로딩에 쓰고 있어서 1·2계층만 채워도 정상 플레이는 안 됩니다.
반대로 **1계층은 확장 명령을 주변부에만 쓰는 다른 게임에는 바로 효과**가
있습니다.

## 권고

1. EM 전체(279종)를 목표로 삼지 않습니다.
2. **1계층부터** 하고, 그 시점에 다시 실제 게임 로그로 측정합니다.
   무엇이 남는지는 추측하지 말고 로그가 말해주게 합니다.
3. `DT_*` 는 착수 전에 **Android 에서 `System.Data.DataTable` 이 살아있는지**
   최소 예제로 먼저 확인합니다. 여기서 막히면 3계층 전체 접근법이 바뀝니다.
4. 자기 검증(`--kemura-selftest`)에 명령별 검사를 추가해 회귀를 막습니다.

## 함께 참고할 문서

이 저장소에는 기반 Emuera 문서도 있어 이식 작업에 직접 쓸 수 있습니다.

- `docs/Emuera/` — `config.md`(설정 키 전체), `ERH.md`, `expression.md`,
  `function.md`, `differences_of_Emuera_and_eramaker.md`
- `docs/eramaker/` — eramaker 원전 규격
- `docs/EMEE/EMEE_Summary.md` — EM+EE 가 바꾼 것 전체 요약
