# 출시 · 버전 규칙

## 1. 명칭

출시물에 나가는 이름은 아래 네 개뿐이다. 서로 어긋나면 조용히 깨지므로 표로 고정한다.

| 항목 | 값 | 정의 위치 | 바꾸면 |
|---|---|---|---|
| 앱 표시 이름 (아이콘 아래) | `Kemura` | `export_presets.cfg` `package/name` | 표시만 바뀐다. 안전 |
| 프로젝트 이름 (창 제목) | `Kemura` | `project.godot` `application/config/name` | 표시만 바뀐다. 안전 |
| 패키지명 (applicationId) | `com.kemura.emuera` | `export_presets.cfg` `package/unique_name` + `Scripts/Settings.cs` `PackageName` | **출시 후 변경 금지.** 스토어 업데이트가 끊기고, 앱 전용 폴더(`Android/data/<패키지명>/files/emuera/`) 안의 게임 데이터에 접근할 수 없게 된다 |
| 어셈블리 이름 | `Kemura` | `kemura.csproj` `<AssemblyName>` + `project.godot` `[dotnet] project/assembly_name` | 두 값이 **대소문자까지** 같아야 한다. 다르면 Android/Linux 에서 C# 어셈블리를 못 찾아 화면에 아무것도 뜨지 않는다 |

`kemura.csproj` / `kemura.sln` 의 파일명은 소문자지만 이건 빌드 산출물 이름과
무관하다(`<AssemblyName>` 이 명시돼 있다). 파일명을 바꾸면 CI 의
`dotnet build kemura.csproj` 5곳을 함께 고쳐야 하므로 그대로 둔다.

## 2. 버전 규칙

버전은 `MAJOR.MINOR.PATCH` 다.

- **MAJOR** — 세이브 호환이 깨지거나 게임 폴더 구조가 바뀌는 변경
- **MINOR** — 기능 추가 (Emuera 명령 구현, 화면 기능)
- **PATCH** — 버그 수정만

Android `versionCode` 는 **단조 증가하는 정수**여야 하므로 위 세 숫자에서 기계적으로 만든다.

```
versionCode = MAJOR * 10000 + MINOR * 100 + PATCH
```

| 버전 | versionCode |
|---|---|
| 0.9.0 | 900 |
| 0.9.1 | 901 |
| 1.0.0 | 10000 |

이전 빌드는 `versionCode` 를 1씩 올리고 있었다(마지막 17). 900 은 그보다 크므로
스토어 업로드에 문제가 없고, 이후로는 버전 문자열만 정하면 코드가 자동으로 정해진다.

### 현재 버전: 0.9.0

`1.0.0` 이 아닌 이유 — 아직 아래가 남아 있다. 이것들이 닫히면 1.0.0 으로 올린다.

- 일부 Emuera 명령 미구현 (`GETVAR`, `GETTEXTBOX` 등). 자세한 목록은 [EMUERA_EM_GAP.md](EMUERA_EM_GAP.md)
- `DT_*`(DataTable) 계열이 Android 트리밍 후에도 동작하는지 미검증. CI 는 리눅스 데스크톱만 돈다
- '모든 파일 접근' 설정 화면을 여는 경로(`JavaClassWrapper` 리플렉션)가 실기에서 미검증
- 음원은 ogg / mp3 만 지원. wav, m4a 는 재생되지 않는다

## 3. 버전 올릴 때 고치는 곳

버전 문자열은 `project.godot` 이 **원본**이다. 앱 화면·로그에 표시되는 버전은
`Scripts/AppInfo.cs` 가 여기서 읽으므로 C# 쪽에는 손댈 것이 없다.

Godot 내보내기 설정은 별도 파일이라 `project.godot` 을 참조할 수 없어서 중복이
불가피하다. 그래서 두 곳뿐이다.

1. `project.godot` → `application/config/version`
2. `export_presets.cfg` → `version/name` 과 `version/code`

두 파일이 어긋나면 CI 의 **Version consistency** 단계가 실패한다. 버전만 바꾸고
`versionCode` 를 안 올린 채 스토어에 업로드하려다 거부당하는 상황을 여기서 막는다.

## 4. 출시 전 확인

- [ ] CI 전체 초록 (빌드 · 브리지 코드 경고 0 · 헤드리스 임포트 · 스모크 런 · 자기 검증)
- [ ] 위 두 파일의 버전이 일치 (CI 가 검사)
- [ ] `package/unique_name` 을 건드리지 않았다
- [ ] `use_gradle_build=true` 유지 (C# Android 내보내기의 필수 조건)
- [ ] 실기에서 게임 1개 실행 확인 (에뮬레이터로는 저장소 권한 동작을 못 본다)
