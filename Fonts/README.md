# Fonts

이 폴더는 **비어 있으면 안 됩니다.** Godot 기본 폰트에는 CJK(한중일) 글리프가
포함되지 않아, 폰트가 없으면 era 게임의 일본어 텍스트가 전부 두부(□)로 표시됩니다.

## 넣어야 하는 파일

`Scripts/FontUtils.cs`가 아래 순서로 탐색합니다. 하나만 있으면 됩니다.

| 우선순위 | 파일명 | 비고 |
|---|---|---|
| 1 | `msgothic.ttc` | Windows 동봉 폰트. **재배포 불가** (개인 사용만) |
| 2 | `msgothic.ttf` | 위와 동일 |
| 3 | `NotoSansJP-Regular.ttf` | **권장.** SIL Open Font License 1.1 — 재배포 가능 |
| 4 | `NotoSansMono.ttf` | 고정폭. 단 일본어 커버리지가 부족할 수 있음 |

## 권장 설치 방법

Noto Sans JP를 사용하세요. OFL 라이선스라 APK에 동봉해 배포할 수 있습니다.

1. https://fonts.google.com/noto/specimen/Noto+Sans+JP 에서 다운로드
2. `NotoSansJP-Regular.ttf`를 이 폴더에 저장
3. Godot 에디터에서 프로젝트를 열면 자동으로 임포트됩니다

> Emuera는 원래 등폭(monospace) 폰트를 전제로 열 정렬을 계산합니다.
> Noto Sans JP는 비등폭이므로 표 형태 출력의 정렬이 다소 어긋날 수 있습니다.
> 정렬을 중시한다면 `Noto Sans Mono CJK JP`를 사용하세요.

## 왜 폰트를 저장소에 넣지 않았나

- `msgothic.*`는 Microsoft 소유로 재배포가 라이선스 위반입니다.
- Noto Sans JP는 재배포 가능하지만 파일 크기가 수 MB로, 저장소에 바이너리를
  넣는 대신 각자 받도록 했습니다.
