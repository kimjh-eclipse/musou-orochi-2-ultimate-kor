# 도구

## iso_quickpatch/

빠른 패처의 소스와 빌드 스크립트. 설계는 [docs/patcher.md](../docs/patcher.md) 참고.

| 파일 | 역할 |
|---|---|
| `build_range_pack.py` | 원본 ISO와 완성본 `USRDIR`를 비교해 차이 구간 팩 생성. `SOURCE_ISO`, `TARGET_DIR`, `VERSION_TEXT`를 환경에 맞게 수정 |
| `verify_range_pack.py` | 팩을 원본 ISO에 덧씌운 결과 해시를 독립 검증 |
| `WO3UIsoQuickPatch.cs` | 패처 본체 (C# 5, .NET Framework 4.x, WinForms) |
| `build_package.py` | 검증 → `csc` 빌드 → 패키지 폴더·ZIP 조립 |
| `WO3U_ISO_ranges.manifest.json` | 현재 팩의 파일별 해시·구간 수·ISO extent 기록 |

빌드:

```
python build_package.py            # 기존 팩으로 EXE 리빌드 + 패키지
python build_package.py --rebuild  # 팩부터 다시 생성
```

필요: Python 3.11+, numpy, Windows .NET Framework 4.x `csc.exe`.

번역 텍스트 추출·삽입·이미지 도구는 작업이 정리되는 대로 추가할 예정이다.
