# Despicable 2 (NSFW) Localization Addendum

Shared localization workflow, guardrails, and audit behavior live in the Core docs:
- `../../Despicable2-Core/Docs/Localization_Guide.md`
- `../../Despicable2-Core/Docs/Localization_Guardrails.md`

Use this addendum for NSFW-only differences.

## What stays in NSFW

- Keys and DefInjected strings that only exist when the NSFW add-on is enabled
- LovinTypeDef localization and other add-on-only DefInjected scaffolding
- Package-specific code-string cleanup notes in `Localization_CodeStringMap.md`

## Audit notes

- The shared audit script lives at repo root: `Tools/LocalizationAudit.ps1`
- The NSFW build passes `-ModRoot` to the shared script automatically
- Keep Core-safe/shared UI keys in Core; do not move them into NSFW just because an NSFW feature also uses them

## Typical workflow

1. Add or update NSFW-only keys in `Languages/English/Keyed/` or `Languages/English/DefInjected/`.
2. Rebuild the DLL and replace it under `1.6/Assemblies/`.
3. Use `Localization_CodeStringMap.md` when converting remaining hardcoded NSFW strings to keyed translations.
4. QA in-game: verify no raw keys appear and wrapping still behaves.
