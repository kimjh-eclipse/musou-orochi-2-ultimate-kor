# 무쌍 오로치 2 얼티메이트 한국어 패치

PS3 일본판 **無双OROCHI2 Ultimate / BLJM61084**용 비공식 한국어 패치입니다. 직접 준비한 복호화 ISO 또는 RPCS3 폴더형 게임에 적용합니다. 게임 원본·ISO·세이브는 배포하지 않습니다.

## 최신 배포

**v20260910b — 2026-09-10 정식 릴리즈** · 문서 갱신: 2026-09-19

[패치 ZIP 다운로드](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases/download/v20260910b/WO3U_KR_v20260910b.zip) · [릴리즈 설명](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases/tag/v20260910b) · [문서 사이트](https://kimjh-eclipse.github.io/musou-orochi-2-ultimate-kor/)

이번 릴리즈는 기존 번역에 **한국어 게임 제목과 목록 아이콘**을 추가합니다. `PARAM.SFO`의 제목은 `무쌍 오로치 2 얼티메이트`이며 게임 ID는 유지합니다. RPCS3 설정을 변경하는 방식이 아니라 게임 파일에 반영하는 패치입니다.

> 최신 패처는 반드시 위 릴리즈 ZIP에서 받으세요. 저장소 루트의 EXE·사용법·해시 파일과 `tools/`는 이전 3파일 패처 시점의 자료이며, 최신 5파일 배포물과 같지 않습니다.

## 설치 요약

1. RPCS3를 완전히 종료하고 ISO를 마운트했다면 해제합니다.
2. ZIP을 풀고 `WO3U_ISO_QuickPatch.exe`를 실행합니다.
3. **실제로 실행하는 ISO 또는 게임 폴더**를 지정해 상태를 검사합니다.
4. `.wo3u-backup` 경로를 확인하고 패치를 적용합니다. 구버전 사용자는 기존 백업을 지정합니다.
5. 로그에서 적용 후 **5개 파일 전체 해시 검증 성공**을 확인합니다. 새로 부팅하고 게임 내부 세이브를 불러옵니다.

세이브나 게임 등록을 지울 필요는 없습니다. 복구·갱신에 쓰는 `.wo3u-backup`은 보관하세요. 구버전 백업이 없는 경우 임의로 덮어쓰지 말고 원본을 다시 준비해야 합니다.

[자세한 설치·업데이트·복구](docs/install.md) · [알려진 문제](docs/known-issues.md)

## 변경 범위

| 파일 (`PS3_GAME` 기준) | 역할 |
|---|---|
| `USRDIR/EBOOT.BIN` | 실행 파일 내 UI 문자열 |
| `USRDIR/LINKDATA.IDX` | 수정 리소스의 저장 크기 등 인덱스 |
| `USRDIR/LINKDATA.BIN` | 번역 텍스트, 한글 폰트, 메뉴 이미지 |
| `PARAM.SFO` | 게임 목록·기본 창 제목용 한국어 제목 |
| `ICON0.PNG` | 게임 목록용 한국어 아이콘 |

게임 파일 크기와 ISO 배치를 유지하는 차이 구간 패치입니다. 음성·영상·세이브·RPCS3 설정은 변경하지 않습니다. `ICON0.PNG`는 게임 목록 아이콘이며 인게임 타이틀 화면과 별개입니다.

대사·이벤트·UI, 무기 속성·소재·BGM, 던전 임무·선녀 의뢰, 공통 메뉴 이미지 등에 번역이 반영되어 있습니다. **모든 화면의 완역·실행 검증을 뜻하지는 않습니다.** 편집 화면 일부 이미지, 미점검 텍스처, 선택 시 확대 잘림·줄넘침 및 일부 이름 표시는 추가 확인이 필요합니다.

## 파일 확인

```text
WO3U_KR_v20260910b.zip  21,130,509 bytes
SHA-256 A024F14565CCD1910055B35D9E136AE64452708AD6EBECC59A878D70EB86B68B
```

```powershell
Get-FileHash .\WO3U_KR_v20260910b.zip -Algorithm SHA256
```

[배포물 및 적용 전후 전체 해시](docs/hashes.md)

## 기술 문서

문서의 바이너리 수치는 이 일본판과 현재 빌드에서 확인한 값입니다. 다른 리전·플랫폼 전체에 일반화하지 않습니다.

- [아카이브·압축·LX 문자열 구조](docs/formats.md): IDX 필드, 압축 블록, 상대 포인터, 문자열 풀 재배치.
- [한글 인코딩과 폰트](docs/font-text.md): CP932 대체 코드, 2048×4096 아틀라스, 32×32 글리프, 이름의 별표·특수문자 원인.
- [메뉴 이미지 처리](docs/images.md): PS3 G1T 치수·채널 순서, BC2/BC3 블록, 이미지 누락 추적과 잘림 검증.
- [실행 파일·제목·아이콘](docs/metadata.md): EBOOT 문자열, SFO 필드, 고정 길이 PNG.
- [패처·ISO·백업 설계](docs/patcher.md): 팩 형식, 다중 extent, 버전 갱신, 복구 및 검증 한계.
- [빌드·검증·조사 범위](docs/validation.md): 재현 절차, 증거 수준, 임무 31개라는 수치의 정확한 의미.
- [변경 이력](CHANGELOG.md).

`docs/`는 GitBook용 `SUMMARY.md`와 GitHub Pages용 Docsify 탐색을 함께 제공합니다. 공개 `tools/`만으로 최신 번역·이미지 전체를 재빌드할 수 있는 상태는 아닙니다. [공개 소스 범위](docs/validation.md)를 먼저 확인하세요.

## 오류 제보

[Issues](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/issues)에 화면 캡처, 진입 경로, 릴리즈 버전, 상태 검사 로그, RPCS3 버전을 올려 주세요. 게임 원본·ISO·개인 세이브는 첨부하지 마세요. 이름 오류는 머리 위 표시와 대화창 표시를 각각 알려 주시면 좋습니다.

코드의 이용 조건은 [LICENSE](LICENSE)를 확인하세요. 원작 게임·상표·음성·이미지의 권리는 각 권리자에게 있습니다.
