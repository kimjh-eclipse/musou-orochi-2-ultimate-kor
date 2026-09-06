# 빠른 패처

20 GB ISO에 xdelta 같은 범용 델타를 쓰는 것은 만들기도 적용하기도 비현실적이다.
모든 수정이 제자리 교체라 세 파일의 크기가 원본과 같으므로, **원본과 완성본을 바이트 비교한
차이 구간만 ISO 안의 해당 위치에 덮어쓰는** 방식을 택했다. ISO9660/UDF 메타데이터는 바뀌지 않는다.

## 구성

| 파일 | 역할 |
|---|---|
| `tools/iso_quickpatch/build_range_pack.py` | 원본 ISO(읽기 전용)와 완성본 폴더를 비교해 차이 구간 팩 `WO3U_ISO_ranges.bin`을 만든다 |
| `tools/iso_quickpatch/verify_range_pack.py` | 원본 ISO에 팩을 덧씌운 스트림의 SHA-256이 완성본 해시와 같은지 독립 검증 |
| `tools/iso_quickpatch/WO3UIsoQuickPatch.cs` | 팩을 리소스로 내장한 GUI/CLI 패처. .NET Framework `csc`로 빌드 |
| `tools/iso_quickpatch/build_package.py` | 검증 → EXE 빌드 → README·SHA256SUMS → ZIP |

## 팩 형식

리틀엔디언.

```
"WO3URNG1" | u32 format=1 | u16 len + utf8 version | u32 file_count
file: u16 len + utf8 iso_path | u64 size | 32B source_sha256 | 32B target_sha256
      | u32 range_count | range: u64 offset | u32 length | bytes
```

16바이트 이내로 떨어진 차이는 한 구간으로 합친다. v20260906b 팩은 3,066구간, 35,806,779바이트다.
EBOOT.BIN은 디버그 SELF로 재포장돼 거의 전체가 바뀌므로 그중 14.1 MB를 차지한다.

## 적용 절차

1. RPCS3 프로세스가 있으면 중단한다.
2. ISO 모드는 ISO9660 PVD → 루트 → `PS3_GAME/USRDIR`를 따라가 세 파일의 extent를 찾는다.
   LINKDATA.BIN처럼 여러 extent로 나뉜 파일은 논리 오프셋을 extent별 물리 오프셋으로 변환한다.
   폴더 모드는 `USRDIR`의 세 파일을 각각 하나의 extent로 본다.
3. 파일 크기가 팩과 다르면 중단한다.
4. 상태 판정: 백업 파일이 있으면 변경 구간만 읽어 원본/패치 상태를 가르고, 없으면 세 파일 전체 SHA-256을 구해
   팩의 `source`/`target` 해시와 비교한다. 어느 쪽도 아니면 "불일치"로 보고 쓰지 않는다.
5. 원본 상태면 덮어쓸 구간의 현재 바이트를 `.wo3u-backup`에 저장한다(파일 끝에 백업 자체의 SHA-256 푸터).
6. 구간을 기록하고 디스크에 플러시한 뒤 세 파일 전체 해시를 다시 구해 `target`과 대조한다.
   불일치면 백업으로 되돌리고 원본 해시를 재확인한 뒤 실패로 끝낸다.
7. `--restore`는 백업의 구간을 되쓰고 원본 해시를 확인한다. 백업은 재적용에 대비해 남긴다.
8. 이전 버전이 적용된 대상에 새 버전을 적용할 때는 그 버전의 백업으로 먼저 원본을 복원한 뒤 진행한다.

## 백업 형식

```
"WO3UBAK1" | i32 version=1 | i32 kind(0=ISO,1=폴더) | i32 stream_count | i64 length × stream_count
| i32 segment_count | (i32 stream, i64 offset, i32 length, bytes) × segment_count | 32B sha256
```

## 재빌드

완성본 폴더가 갱신되면 `build_range_pack.py`의 `VERSION_TEXT`와 `TARGET_DIR`를 맞춘 뒤 실행한다.

```
python tools/iso_quickpatch/build_package.py --rebuild
```

`csc`는 Git Bash에서 `/옵션`이 경로로 해석되므로 PowerShell 또는 파이썬 `subprocess`로 호출한다.

## 검증 기록

v20260906b:

- 독립 검증 스크립트: 세 파일 모두 원본 해시·완성본 해시 일치.
- 초판(v20260906)이 적용된 ISO 복사본에 새 패처 적용: 이전 버전으로 감지 → 초판 백업으로 원본 복원(3/3 원본 해시) → b 적용 → 최종 해시 3/3 일치. 재검사는 갱신된 백업으로 빠른 판정.
- 추출 폴더: 패치 3/3 → 복구 3/3 → 백업 없이 전체 해시로 원본 복귀 확인.
- 패치된 ISO(v20260906b)를 RPCS3에서 직접 부팅해 정상 실행 확인.

v20260906(초판):

- 독립 검증 스크립트: 세 파일 모두 원본 해시·완성본 해시 일치.
- ISO 복사본: 패치 → 복구 → 재적용, 매 단계 세 파일 해시 일치. 재적용 시 백업 기반 빠른 판정 동작 확인.
- 추출 폴더: 패치 → 복구 후 전체 해시로 원본 복귀 확인.
- 미확인: 패치된 ISO의 RPCS3 직접 부팅.
