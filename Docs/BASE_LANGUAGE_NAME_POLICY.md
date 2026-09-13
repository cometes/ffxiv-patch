# Text Scope Profile Policy

## Purpose

Select Korean source text or the chosen base client language (`ja`/`en`) by
observable text scope. This is a text-routing policy. It does not modify
in-game font repair logic, lobby font atlases, or lobby ULD routes.

## Profiles

- `full` (default): all eight text scopes use Korean source text and generated
  `060000` UI-image assets are included.
- `story`: Story uses Korean; the other seven text scopes use the selected base
  client language. Generated `060000` UI-image assets are excluded so maps,
  loading/title cards, and other image-based UI stay in the base language.
- `custom`: every text-scope outcome is supplied explicitly as `ko` or `base`;
  UI-image assets are selected independently.

The generator exposes all three profiles. The production UI exposes only Full
and Custom so the requested result is selected directly from the nine outcomes,
without a separate Story button. Story-only composition is Custom with Story
`ko` and the other seven text outcomes plus UI images set to `base`. Previously
saved `story` profiles migrate to that canonical Custom composition. Every
text-composition action includes the Hangul font patch; Font-only remains a
separate action.

## Scope Precedence and Coverage

Story is classified first, then the six named column groups, then Remainder.

| Scope ID | Coverage |
|---|---|
| `story` | `quest/*`, `cut_scene/*`, `opening/*`, `custom/*`; any leaf containing `Talk`; `Balloon`, `NpcYell`, `TopicSelect`, `Quest`, `CompleteJournal`, `QuestRedoChapterUI*`; `InstanceContentTextData` rows `>= 1000` |
| `bnpc` | `BNpcName` offset `0` |
| `actions` | `Action`, `BuddyAction`, `CraftAction`, `EventAction`, `GeneralAction`, `PetAction` offset `0` |
| `duty` | `ContentFinderCondition` offsets `0`, `4` |
| `item` | `Item` offsets `0`, `4`, `12` |
| `place` | `PlaceName` offsets `0`, `4`, `8` |
| `common` | `Completion` offsets `0`, `4`, `8` |
| `remainder` | Every other string cell, including `InstanceContentTextData` rows `< 1000` and non-name columns in mixed sheets |

Base-routed cells keep the exact selected global bytes. Korean-routed cells
continue through string-key/row fallback, remap, SeString merge, and RSV
resolution. Base routing wins over those Korean-source transformations.

### Beastmaster Monster Book and Trials

The following sheets belong to `remainder` and use the existing row-ID
matching route:

- `XBMPet`: monster-book descriptions and other string fields.
- `XBMItem`: singular/plural/display names, effect descriptions, and short descriptions.
- `XBMItemType`: trial-item category names.
- `XBMScoreBonus`: bonus names and completion conditions.
- `XBMActionEffectType`: action-effect categories.
- `XBMActionTarget`: action-target descriptions.
- `XBMElement`: element and damage-type labels, preserving native GUI control bytes.
- `XBMScoreRank`: trial score-rank titles.

Only these exact sheet names are enabled; other `XBM*` sheets remain outside
this allowlist.

In the UI, select **전체 한글**, or **직접 설정 → 기타 게임 텍스트 → 한국어**.
Setting Remainder to Base preserves the selected Japanese/English text even
when the other seven scopes use Korean. UI images and name-only scopes,
including Item names, do not control these strings. Remainder is not a
Beastmaster-only option; it
also controls the other text covered by that scope.

Missing or empty Korean XBM strings retain the selected base-language bytes;
Korean-only rows are not appended. The empty Korean Bestiary subtitle
(`Addon#17701`) also intentionally retains its Japanese/English original.
This change adds no subtitle-hiding remap.

## UI Settings and Migration

The UI stores the target language, current effective profile/outcomes, and a
separate nine-outcome Custom draft in:

`%LOCALAPPDATA%\FFXIVKoreanPatch\patch-options.txt`

Keys are `targetLanguage` (`ja`/`en`), `textProfile`, `story`, `bnpc`, `actions`,
`duty`, `item`, `place`, `common`, `remainder`, and `uiAssets`. The nine draft
keys use the same outcome names prefixed with `custom.`.

UI values are canonicalized when loaded or selected:

- `full` -> all eight text outcomes and `uiAssets` use `ko`
- `custom` -> all saved outcomes are preserved
- selecting Full changes effective outcomes without overwriting the Custom draft;
  selecting Custom restores the draft, including after restart
- a previously saved `story` profile -> `custom`, with Story `ko` and the other
  seven text outcomes plus `uiAssets` set to `base`

Old six-key settings remain readable:

- all six `preserveBase...` values false -> `full`
- any old value true -> `custom`
- each old true value -> matching text scope `base`
- Story and Remainder -> `ko`, preserving the old behavior
- a missing `uiAssets` key -> `ko`, preserving the old UI-image behavior for
  Full and Custom

The next settings save writes only the new format.
Missing draft keys in older settings initialize the draft from the migrated
outcomes. Writes use a flushed sibling temporary file and atomic replacement.
A failed save preserves the previous file and shows a persistent warning; an
unreadable or malformed settings file fails startup rather than selecting Full.

## Generator Flags

Primary interface:

- `--text-profile full|story|custom`
- `--text-scope-outcomes <csv>` for the eight text scopes in `custom`
- `--skip-ui-texture-fix` when the independent UI-image outcome is `base`

The image flag does not disable Korean UI text-font repairs. With fonts included,
Korean Remainder requires the existing PartyMemberList/ContentsFinder/RaidFinder
ULD corrections even when localized images are disabled. Those corrections
require neither Korean UI textures nor the global EXD archive.

All eight text outcomes set to Base omit the `0a0000` output/application package.
UI images and fonts remain independently selectable under the composition
contract; every WPF composition still includes fonts. Font-only always excludes
both text and UI packages. Manifests list only selected packages, their original
indexes, and the version marker, and record the independent `uiAssets` outcome.

Example:

```text
--text-profile custom --text-scope-outcomes "story=ko,bnpc=base,actions=base,duty=base,item=base,place=base,common=base,remainder=base"
```

Custom input requires all eight unique scopes. Missing, duplicate, or unknown
scopes and unsupported outcomes are errors.

The older `--preserve-base-*`, `--preserve-base-language-groups`, and
`--preserve-base-language-names` flags remain CLI compatibility inputs. The WPF
UI no longer emits them.

## Verification

`PatchRouteVerifier` provides:

- `story-text-profile-scopes`
- `story-instance-content-boundary`
- `story-sestring-structure`
- `story-ui-assets`
- `full-text-output-regression`

Story verification compares base-only cells byte-for-byte with a clean target,
Korean-selected cells with the Korean source, and structured cells by top-level
SeString payload type sequence and RSV count. Full regression compares SHA-256
for every top-level `0a/orig.0a` output file against a known full baseline.

Clean `ja` and `en` baselines must come from separate backups/exports. Installed
game folders are not valid development generator or verifier inputs.
