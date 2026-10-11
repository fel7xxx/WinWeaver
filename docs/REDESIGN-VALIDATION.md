# Redesign validation — 2026-10-11

## Verified implementation changes

- English TSV is embedded in the main assembly (`WithCulture=false`); the loader previously could not find it. Verification invokes the production translator.
- Screenshot language overrides are applied before constructing the UI. Screenshot errors return a failure exit code; missing frames are errors. `WW_SCALE` supports layout probes from 0.75 to 2.
- Section typography increased to 9pt semibold (10pt with larger text).
- Home headline uses the localization pipeline. Missing navigation, inspector and dialog translations were added.
- Home, auto-tune confirmation and history no longer promise that every operation is reversible.
- Registry history detects case-only and registry-type-only changes.
- Partial tracked failures retain the Explorer restart flag.
- `KeepReg` no longer drops a non-registry undo command when registry values are unchanged.
- Registry deletion access errors and failed service-start restoration fail rollback instead of marking it complete.
- DNS selection validates provider indices and propagates DNS setter errors.
- Fast boot checks powercfg's exit code before updating its registry flag; failure paths are mocked in regression tests.
- Verification builds use isolated output directories so reflection-loaded executables do not block subsequent builds.

## Executed checks

- Both target frameworks built: net48 and net8.0. Three existing compiler warnings remain (Hero.Tag name hiding on both targets; obsolete WebRequest on net8.0).
- Mocked production orchestration tests pass for exact snapshots, partial failures, rollback ordering, registry case/type changes, KeepReg, denied deletion, service restore failure and DNS validation/errors.
- Version parsing and installer dependency-copy fixtures pass.
- Headless fake-engine rendering completed with exit code 0 for all 12 pages and 8 dialogs. Screenshots: `artifacts/redesign-all-en`.
- Settings, privacy and home also rendered at internal layout scale 1.25: `artifacts/redesign-scale125`. This is not a substitute for Windows monitor-DPI testing.
- Static engine inventory contains 259 functions. This is an inventory, NOT proof that each tweak was manually or dynamically audited.

## Not yet verified / release gates

The complete redesign and per-tweak audit are NOT certified complete by these checks.

- Each data-driven tweak and callable engine action still needs an explicit applicability, failure, cancellation, restart and reversibility review.
- DNS currently identifies a provider from the first adapter; exact restoration of custom multi-adapter DNS requires further work.
- Native commands in several setters still discard exit codes (for example context-menu operations).
- App removal and feature disabling are not fully reversible through registry snapshots.
- History persistence failure, interrupted writes and cancellation during partial system changes need dedicated tests.
- Full localization coverage, narrow-window layouts, keyboard-only navigation, screen readers, all dialog states and actual Windows DPI transitions need additional validation.
- Real effects, restart-dependent settings, Defender, installer/update execution and rollback require a disposable Windows VM with a snapshot. No system tweaks were applied to the host during this validation.

No release version was changed and no release package was published.