# RSV token support

## Data source

- The generator accepts `--rsv-map <file>` containing a flat JSON object of RSV keys and text values.
- Without the option, it checks `rsv.json` beside `FFXIVPatchGenerator.exe`, then the current working directory.
- Release builds embed the current map from `Bing-su/my-ffxiv-toolkit`.
- Set `FFXIV_RSV_MAP_PATH` when building to use a staged local map instead of downloading the default.

## Resolution

- RSV replacement runs after the Korean source string is selected and before the EXD row is serialized.
- The resolver converts EXD language codes to RSV language IDs: `ja=0`, `en=1`, `de=2`, `fr=3`, `chs=4`, `cht=5`, `ko=6`.
- Rows or columns intentionally preserved in the base language keep the base-client RSV token.
- Diagnostics report resolved and unresolved RSV counts in `patch-diagnostics.tsv`.

## Kefka greeting

`InstanceContentTextData#45500` contains two Korean auto-translate phrases. Its external listing represents the opening and closing markers as `"   7 "` and `"   8 "`.

For the two Korean row-45500 keys only, `RsvStringResolver` restores:

- open: `02 12 02 37 03` — `Icon(54)`
- close: `02 12 02 38 03` — `Icon(55)`

The phrase text still comes from the external map. If the expected two marker pairs change shape, map loading fails instead of emitting guessed bytes.

## Verification

The route verifier requires:

- two opening and two closing Icon macros;
- no flattened ASCII marker;
- no residual `_rsv_` token;
- the following Kefka RSV row to remain Korean.

This verifies generated EXD bytes, not the final battle-dialogue rendering. Client confirmation remains required.
