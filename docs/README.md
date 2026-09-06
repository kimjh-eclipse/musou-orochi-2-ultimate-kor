# 무쌍 오로치 2 얼티밋 한국어화 — 기술 문서

PS3 『無双OROCHI2 Ultimate』(BLJM61084) 비공식 한국어화 과정에서 규명한 아카이브 구조, 폰트 아틀라스,
실행 파일 처리 방식과 배포용 빠른 패처의 설계를 정리한다.

- 패치 배포: [Releases](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor/releases)
- 도구 소스: [저장소 루트](https://github.com/kimjh-eclipse/musou-orochi-2-ultimate-kor)

## 문서 구성

| 문서 | 내용 |
|---|---|
| [파일 포맷](formats.md) | LINKDATA.IDX 레코드, LINKDATA.BIN 슬롯, PS3 zlib 블록 압축, G1T 폰트 아틀라스, EBOOT.BIN |
| [빠른 패처](patcher.md) | 차이 구간 팩 형식, ISO9660 다중 extent 처리, 백업·검증·복구 절차, 재빌드 방법 |

## 법적 고지

이 문서와 저장소는 **원본 게임에서 추출한 텍스트·이미지·번역 대역 데이터를 포함하지 않는다**.
패처에 든 것은 원본과 완성본의 차이 바이트뿐이며, 적용에는 본인이 합법적으로 소유한 원본이 필요하다.
문서 내 바이너리 오프셋·구조 명세는 상호운용을 위한 사실 정보이다.
