# Fonts

`Scripts/FontUtils.cs`가 아래 순서로 탐색합니다.

| 우선순위 | 파일명 | 상태 |
|---|---|---|
| 1 | `msgothic.ttc` | — |
| 2 | `msgothic.ttf` | ✅ **현재 포함됨** (구 Unity 프로젝트에서 승계) |
| 3 | `NotoSansJP-Regular.ttf` | 대체 후보 |
| 4 | `NotoSansMono.ttf` | 고정폭, 일본어 커버리지 부족 가능 |

폰트가 하나도 없으면 Godot 기본 폰트에 CJK 글리프가 없어 일본어가
전부 두부(□)로 표시되며, `FontUtils`가 경고를 출력합니다.

## 라이선스 주의

현재 포함된 `msgothic.ttf`(MS Gothic)는 **Microsoft 소유 폰트**입니다.
uEmuera 원본 저장소에서 승계된 파일로, Unity 프로젝트를 정리할 때
기능을 유지하기 위해 이 위치로 옮겼습니다.

**공개 배포(스토어 등록·APK 재배포)를 계획한다면 교체가 필요합니다.**
MS Gothic의 재배포는 라이선스 위반입니다.

### 교체 방법

1. https://fonts.google.com/noto/specimen/Noto+Sans+JP 에서 다운로드
   (SIL Open Font License 1.1 — 재배포 가능)
2. `NotoSansJP-Regular.ttf`를 이 폴더에 저장
3. `msgothic.ttf` 삭제 — `FontUtils`가 자동으로 Noto를 사용합니다

> Emuera는 등폭 폰트를 전제로 열 정렬을 계산합니다. Noto Sans JP는
> 비등폭이라 표 형태 출력의 정렬이 어긋날 수 있습니다. 정렬이 중요하면
> `Noto Sans Mono CJK JP`를 사용하세요.
