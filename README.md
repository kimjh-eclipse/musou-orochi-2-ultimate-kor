# 무쌍 오로치 2 얼티밋 한국어 패치

PS3 『無双OROCHI2 Ultimate』(Warriors Orochi 3 Ultimate, 일본판 `BLJM61084`)의 한국어 패치입니다.

> **패치 파일만 배포합니다.** 게임 ISO는 포함되어 있지 않으며 제공하지도 않습니다.
> 본인이 소유한 디스크에서 직접 덤프해 복호화한 ISO, 또는 그 ISO를 풀어 놓은 폴더형 게임에 적용해 주세요.

> **현재 상태: 작업 진행 중.** 번역과 이미지 교체가 계속 갱신되고 있어 패치 내용은 버전마다 바뀝니다.
> 아직 정식 배포판이 아니며, 패처 자체를 먼저 공개합니다.

📖 **기술 문서**: [docs/](docs/README.md) — LINKDATA 아카이브 구조, 폰트 아틀라스, 빠른 패처 설계

## 다운로드

[Releases](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases) 에서 최신 패치를 받으세요.
저장소에도 현재 버전의 패처(`WO3U_ISO_QuickPatch.exe`)와 사용법이 들어 있습니다.

## 적용 방법

패처 하나로 두 가지 대상을 처리합니다. 같은 대상에 두 방식을 겹쳐 적용하지 마세요.

### A. 복호화 ISO

1. RPCS3와 ISO 마운트 프로그램을 종료합니다.
2. `WO3U_ISO_QuickPatch.exe`를 실행하고 복호화된 일본판 ISO를 선택합니다.
3. **ISO 상태 검사**로 원본인지 확인한 뒤, 주의사항에 동의하고 **ISO에 한국어 패치 적용**을 누릅니다.

ISO 파일 자체가 수정됩니다. 전체를 복사하지 않고 달라지는 구간(약 36 MB)만 제자리에 기록하므로 몇 십 초면 끝나고 ISO 크기도 바뀌지 않습니다.
같은 위치에 `<ISO 이름>.iso.wo3u-backup` 백업이 생기며, 이 파일로 원본 복구와 다음 버전 갱신을 합니다. 삭제하지 마세요.

### B. RPCS3 폴더형 게임 / 추출 폴더

1. RPCS3를 완전히 종료합니다.
2. `RPCS3 / 폴더형 게임` 칸에 RPCS3 루트, `BLJM61084` 게임 폴더, `PS3_GAME` 또는 `USRDIR` 중 하나를 지정합니다. `dev_hdd0\disc\BLJM61084`는 자동으로 찾습니다.
3. **폴더 게임 상태 검사** 후 **폴더 게임에 직접 패치**를 누릅니다.

`USRDIR`의 `EBOOT.BIN`, `LINKDATA.IDX`, `LINKDATA.BIN` 세 파일이 수정되고, 게임 폴더 바깥에 `BLJM61084.wo3u-backup`이 생깁니다.

### C. 명령줄

```
WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --verify-only
WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --yes
WO3U_ISO_QuickPatch.exe --iso "D:\WO3U.iso" --restore --backup "D:\WO3U.iso.wo3u-backup"
WO3U_ISO_QuickPatch.exe --folder "C:\RPCS3" --yes
```

자세한 내용은 [README_사용법.txt](README_사용법.txt)에 있습니다.

### 주의

- 패치 후 RPCS3 저장 상태(Save State)로 이어하지 마세요. 저장 상태는 패치 전 폰트와 메모리를 되살릴 수 있습니다. 정상 부팅 후 게임 내부 세이브를 사용하세요.
- 영문판(`NPUB31505` 등)이나 다른 리전은 지원하지 않습니다. 영문판은 1바이트 폰트만 참조해 2바이트 글리프를 표시하지 못합니다.
- 세이브·savestate·PPU/SPU/셰이더 캐시는 건드리지 않습니다.

## 파일 해시

원본(일본판) → 패치 후, `PS3_GAME\USRDIR` 기준. 최신 값은 [README_사용법.txt](README_사용법.txt)와 [CHANGELOG.md](CHANGELOG.md)를 따릅니다.

| 파일 | 원본 SHA-256 | 패치 후 SHA-256 (v20260906) |
|---|---|---|
| EBOOT.BIN | `E59780C5DC9C9F27B1CECEF4C3D71AACC9E7C55B86323C7F25BEF54BFFFABF66` | `BCC84BDA4C20C984021CB5432190120209CDE960053858A2E2377BF931EC0612` |
| LINKDATA.IDX | `11872DF3C8E1026FBFBD9A4A508D2E20B3926DCD45F011C5F609168629A8558B` | `A5B5FAAF5ECB889ECC1D37365A684A805F87AED3342FF4BD4009920FFAE08CC6` |
| LINKDATA.BIN | `E8A491D3FA5B06154B98A56745DA598423555E32DF17DE73D24A4F2E0A6EDAA9` | `1EF9EA13065A0598BAD3DDCF6A150DD35236EAA9B4B42EF27A1AE16FC2AD3FED` |

## 동작 환경

RPCS3 v0.0.27 에서 폴더형 게임으로 확인하며 작업하고 있습니다. 패치된 ISO의 직접 부팅과 실기(CFW PS3)는 아직 확인하지 않았습니다.

## 기술 메모

| 항목 | 내용 | 문서 |
|---|---|---|
| LINKDATA.IDX / .BIN | 32바이트 빅엔디언 레코드(offset, unpacked, stored, compressed)와 9.26 GB 아카이브. 슬롯 크기를 넘지 않는 제자리 교체 | [formats](docs/formats.md) |
| PS3 zlib 블록 압축 | 엔트리 단위 커스텀 블록 압축의 해제·재압축·왕복 검증 | [formats](docs/formats.md) |
| 폰트 아틀라스 | G1T 4096×2048 DXT5, 32×16 셀 128×128 격자, 같은 문자의 face 2개 | [formats](docs/formats.md) |
| EBOOT.BIN | 실행 파일 안의 고정 UI 문자열을 제자리 교체(길이 이내), 디버그 SELF로 재포장 | [formats](docs/formats.md) |
| 빠른 패처 | 원본 ISO와 완성본을 바이트 비교한 차이 구간만 ISO9660 extent 위치에 기록 | [patcher](docs/patcher.md) |

## 문의

네이버 카페 **팬텀 게임천국**

## 라이선스

패치 파일과 도구에 한합니다. 게임의 저작권은 코에이 테크모 게임스에 있습니다.
