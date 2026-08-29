# Base Language Name Policy

## Purpose

Allow selected name columns to remain in the base global client language while
the rest of the text patch continues to use Korean source text.

This policy is text-routing only. It must not touch font, lobby font atlas, ULD,
or UI texture patch paths.

## UI Options

The patch UI exposes six independent checkboxes. All default to off.

- `BNpcName` original language: preserves the battle NPC name column from the
  selected base client language.
- Action name original language: preserves action/skill name columns from the
  selected base client language.
- Common phrase original language: preserves auto-translate/common phrase text
  from the selected base client language.
- Duty name original language: preserves duty name and short-name columns from
  the selected base client language.
- Item name original language: preserves item singular, plural, and display-name
  columns while leaving item descriptions on the Korean route.
- Place name original language: preserves all `PlaceName` text variants from the
  selected base client language.

The local UI values are saved in:

`%LOCALAPPDATA%\FFXIVKoreanPatch\patch-options.txt`

## Current Scope

When enabled, the policy preserves these target-global string columns:

- `bnpcname`: `BNpcName` offset `0`
- `actions`: `Action`, `BuddyAction`, `CraftAction`, `EventAction`,
  `GeneralAction`, and `PetAction` offset `0`
- `commonphrases`: `Completion` offsets `0`, `4`, and `8`
- `dutynames`: `ContentFinderCondition` offsets `0` and `4`
- `itemnames`: `Item` offsets `0`, `4`, and `12`
- `placenames`: `PlaceName` offsets `0`, `4`, and `8`

`Item` offset `8` is the description and intentionally remains on the Korean
replacement route. The `placenames` group is limited to EXD text routing; it
does not alter image-based regional titles or map textures handled through
`TerritoryType`, `CutScreenImage`, or `Map` UI resource paths.

`MountAction` and `PvPAction` currently have no string columns in the checked
2026.05.25 data set, so they are not part of the active verified scope.

`ENpcResident` is intentionally not included in the original-language options.
Resident/NPC UI text should continue through the normal Korean patch route.

For Japanese-client output, selected columns stay Japanese. For English-client
output, selected columns stay English. Item and place-name routing was verified
separately on English output as described below.

## Generator Flags

- `--preserve-base-bnpc-names`
- `--preserve-base-action-names`
- `--preserve-base-common-phrases`
- `--preserve-base-duty-names`
- `--preserve-base-item-names`
- `--preserve-base-place-names`
- `--preserve-base-language-groups <csv>`

Legacy `--preserve-base-language-names` maps to `bnpcname` and `actions` only.
It is kept only for compatibility and does not enable any other group.

## Verification Notes

Use local restore-baseline clean indexes, not the currently patched game folder.

Checked on 2026.05.25 `ja` data for the original groups:

- Default/off: `BNpcName`, action sheets, `Completion`, and `ENpcResident` use
  normal Korean replacement routing where Korean rows exist.
- `--preserve-base-bnpc-names`: `BNpcName` column offset `0` routes to
  `keep-global`; action sheets and `ENpcResident` remain on normal routing.
- `--preserve-base-action-names`: action sheet column offset `0` routes to
  `keep-global`; `BNpcName` and `ENpcResident` remain on normal routing.
- `--preserve-base-common-phrases`: `Completion` column offsets `0`, `4`, and
  `8` route to `keep-global`; `BNpcName`, action sheets, and `ENpcResident`
  remain on normal routing.

Checked on 2026.08.29 against 2026.08.11 `en` data:

- Default/off: `Item` and `PlaceName` name columns use normal Korean
  replacement routing.
- `--preserve-base-item-names`: `Item` offsets `0`, `4`, and `12` route to
  `keep-global`; description offset `8` remains on normal Korean replacement.
- `--preserve-base-place-names`: `PlaceName` offsets `0`, `4`, and `8` route to
  `keep-global`.
