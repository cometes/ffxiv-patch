# 현재 알려진 제한 사항

이 문서는 현재 동작에 영향을 주는 제한과 회귀 방지 규칙만 기록한다. 과거 실험 산출물, 폐기된 구현의 상세 이력, 세션별 작업 로그는 포함하지 않는다.

## 실기 확인 대기

### 선택 구성과 마수 조련사 텍스트

- WPF 선택·저장·확인·제거 흐름과 합성 EXD/ULD/SqPack 입력을 이용한 생성·복구·재추출을 검증했다.
- 별도 백업에서 내보낸 실제 한국어/글로벌 데이터로 전체 폰트 포함 패치를 생성하고, 실제 게임에서 JA/EN 스토리·마수 조련사 문자열 및 화면을 확인하는 단계는 아직 수행하지 않았다.
- 합성 데이터의 PASS를 실제 클라이언트 호환성이나 무충돌 보장으로 해석하지 않는다.

### 케프카 전투 시작 상용구

- `InstanceContentTextData#45500`의 한국어 RSV 본문과 `Icon(54/55)` 매크로 바이트 생성은 검증됐다.
- 실제 전투 대화창에서 한국어 본문과 상용구 외형이 함께 표시되는지는 사용자 확인 전까지 미확인이다.

### UI 배율별 큰 한글 라벨

- 비로비 `TrumpGothic_23/34/68.fdt`의 한글 시각 크기 보정은 생성 산출물 검증을 통과했다.
- ActionDetail, PvP 프로필 등 실제 100%/150%/200%/300% 화면의 상대 크기는 사용자 확인이 필요하다.

### 저배율 파티 번호 마커

- 120% 이하에서 파티 목록·채팅 번호 마커 주변 픽셀이 오염될 가능성이 남아 있다.
- `U+E031`, `U+E037`, `U+E0B1`~`U+E0B8`, `U+E0E1`~`U+E0E8`의 glyph와 base/mip texture 주변을 clean 기준으로 보호해야 한다.

## 기능 제한

- `ExcelVariant.Subrows` 시트는 지원하지 않으며 진단 파일에 `unsupported-subrows`로 기록한다.
- 퀘스트 Say 문구 익명화는 커버리지가 불완전해 비활성화되어 있다. CLI 옵션은 경고만 출력하는 no-op이다.
- 글로벌·한국 클라이언트 버전이 다르거나 clean/original index를 확보할 수 없으면 실제 패치 생성을 중단한다.
- 실제 설치 폴더는 clean 기준선으로 사용하지 않는다. 별도 백업의 restore baseline 또는 산출물의 `orig.*` index를 사용한다.

## 폰트 회귀 방지 규칙

- `font-only` 범위는 [FONT_ONLY_PATCH_SCOPE.md](FONT_ONLY_PATCH_SCOPE.md)를 따른다.
- shared atlas를 수정할 때 `Jupiter_45/90` damage-number route와 `font3.tex`의 기존 한글 영역을 오염시키지 않는다.
- 로비 폰트 변경 전에는 [LOBBY_FONT_REWORK_PLAN.md](LOBBY_FONT_REWORK_PLAN.md), [LOBBY_ROUTE_SURVEY.md](LOBBY_ROUTE_SURVEY.md), [HD_FHD_LOBBY_PAGE_BUDGET.md](HD_FHD_LOBBY_PAGE_BUDGET.md)를 확인한다.
- 생성 결과가 보인다는 사실만으로 완료 처리하지 않는다. 관련 verifier와 실제 화면 확인을 구분한다.
