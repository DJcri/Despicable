from __future__ import annotations

import argparse
import re
import sys
import fnmatch
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

EXCLUDED_DIRS = {'.git', '.vs', 'bin', 'obj', '__pycache__', 'packages'}
SOURCE_SUFFIXES = {'.cs'}
GENERATED_DIR_NAMES = {'.vs', 'obj'}
GENERIC_NAME_HINTS = {
    'misc',
    'stuff',
    'temp',
    'temps',
    'thing',
    'things',
}
STATIC_COLLECTION_TYPES = (
    'Dictionary<',
    'HashSet<',
    'List<',
    'Queue<',
    'Stack<',
    'ConcurrentDictionary<',
    'ConcurrentQueue<',
)

# --- Localization checks ---

TRANSLATE_KEY_RE = re.compile(r'"([^"\r\n]+)"\s*\.\s*Translate\s*\(')
UI_LITERAL_PATTERNS = (
    re.compile(r'(?:Widgets|D2Widgets)\.\w+\s*\([^;\r\n]*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'(?:TooltipHandler|D2Text|D2Section|D2Tabs)\.\w+\s*\([^;\r\n]*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'Messages\.Message\s*\(\s*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'(?:new\s+)?FloatMenuOption\s*\(\s*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'D2ActionBar\.Item(?:Key)?\s*\([^;\r\n]*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'Dialog_MessageBox\.\w+\s*\([^;\r\n]*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'Dialog_TextEntrySimple\s*\([^;\r\n]*(?:\$?@?"[^"\r\n]+")'),
    re.compile(r'NextTextBlock\s*\([^;\r\n]*(?:\$?@?"[^"\r\n]+")'),
)
UI_LITERAL_ALLOW_MARKERS = ('loc-ignore', 'loc-allow-internal')
UI_LITERAL_FILE_HINTS = ('/UI/', '/Settings/', '/ManualInteraction/', '/AnimGroupStudio/')
UI_LITERAL_FALLBACK_RE = re.compile(r'(?:\$?@?"[^"\r\n]*[A-Za-z][^"\r\n]*\s[^"\r\n]*")')
UI_LITERAL_STRING_RE = re.compile(r'\$?@?"([^"\r\n]*)"')
DEF_FIELDS_TO_MIRROR = ('label', 'labelPlural', 'description', 'reportString', 'helpText', 'noun')


# Translation text guardrails:
# - RimWorld's translation loader treats raw '>' characters as tag delimiters and will emit
#   "Translation data ... has N errors" when they appear in Keyed/DefInjected strings.
# - Prefer Unicode arrows (→) or '/' separators for navigation paths.
TRANSLATION_FORBIDDEN_SUBSTRINGS = ('->',)
TRANSLATION_FORBIDDEN_CHARS = ('<', '>')

# Core must remain SFW: no optional add-on interaction icon references in Core source.
CORE_FORBIDDEN_SUBSTRINGS = ('UI/Interaction/',)

def iter_language_entries(root: Path):
    """Yield (kind, file, key, value_text). kind is 'Keyed' or 'DefInjected'."""
    for kind, base in (
        ('Keyed', root / 'Languages' / 'English' / 'Keyed'),
        ('DefInjected', root / 'Languages' / 'English' / 'DefInjected'),
    ):
        if not base.exists():
            continue
        for f in base.rglob('*.xml'):
            try:
                tree = ET.parse(f)
            except ET.ParseError:
                continue
            ld = tree.getroot()
            if ld.tag != 'LanguageData':
                continue
            for child in ld:
                if not isinstance(child.tag, str):
                    continue
                # Join itertext() so future rich-text nodes still validate for forbidden chars in plain text.
                value = ''.join(child.itertext())
                yield kind, f, child.tag, value

def validate_braces_and_placeholders(value: str):
    """Return None when OK, otherwise a short error string."""
    i = 0
    placeholders: list[str] = []
    while i < len(value):
        ch = value[i]
        if ch == '{':
            if i + 1 < len(value) and value[i + 1] == '{':
                i += 2
                continue
            j = value.find('}', i + 1)
            if j == -1:
                return 'unclosed {'
            content = value[i + 1:j]
            if content == '':
                return 'empty {} placeholder'
            placeholders.append(content)
            i = j + 1
            continue
        if ch == '}':
            if i + 1 < len(value) and value[i + 1] == '}':
                i += 2
                continue
            return 'unopened }'
        i += 1
    for p in placeholders:
        # allow numeric placeholders with optional format: {0:0.0}, {1,6}
        if not re.match(r'^\d+([,:].*)?$', p):
            return f'non-numeric placeholder {{{p}}}'
    return None
EMPTY_CATCH_RE = re.compile(r'catch\s*(?:\([^)]*\))?\s*\{\s*\}', re.MULTILINE)
FIELD_LINE_RE = re.compile(
    r'^\s*(?P<mods>(?:public|private|internal|protected)\s+(?:new\s+|sealed\s+|unsafe\s+)*)'
    r'static\s+(?!readonly\b)(?!class\b)(?!partial\b)(?!extern\b)(?!const\b)'
    r'(?P<type>[A-Za-z0-9_<>,.?\[\]\s]+?)\s+'
    r'(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)\s*(?://.*)?$'
)
LARGE_FILE_REASON_RE = re.compile(r'Guardrail-(?:Reason|Allow-LargeFile)\s*:\s*(?P<reason>.+)')
STATIC_ALLOW_RE = re.compile(r'Guardrail-Allow-Static\s*:\s*(?P<reason>.+)')
GENERIC_DECL_RE = re.compile(
    r'^\s*(?:\[.*?\]\s*)*(?:(?:public|private|internal|protected)\s+)?(?:static\s+)?'
    r'(?:readonly\s+)?(?:ref\s+|out\s+|in\s+)?'
    r'(?:var|[A-Za-z_][A-Za-z0-9_<>,.?\[\]]*)\s+'
    r'(?P<name>misc|stuff|temp|temps|thing|things)\b',
    re.MULTILINE,
)

REVIEW = 'REVIEW'
WARN = 'WARN'
ERROR = 'ERROR'


def iter_source_files(root: Path):
    for path in root.rglob('*'):
        if not path.is_file():
            continue
        if any(part in EXCLUDED_DIRS for part in path.parts):
            continue
        if path.suffix.lower() in SOURCE_SUFFIXES:
            yield path

def iter_cs_files_under(path: Path):
    if not path.exists():
        return
    for p in path.rglob('*'):
        if not p.is_file():
            continue
        if any(part in EXCLUDED_DIRS for part in p.parts):
            continue
        if p.suffix.lower() == '.cs':
            yield p


def load_keyed_keys(root: Path) -> set[str]:
    keys: set[str] = set()
    keyed_dir = root / 'Languages' / 'English' / 'Keyed'
    if not keyed_dir.exists():
        return keys
    for f in keyed_dir.glob('*.xml'):
        try:
            tree = ET.parse(f)
            ld = tree.getroot()
            if ld.tag != 'LanguageData':
                continue
            for child in ld:
                if isinstance(child.tag, str):
                    keys.add(child.tag)
        except ET.ParseError:
            # skip malformed xml rather than crashing the whole guardrail run
            continue
    return keys


def load_keep_keys(root: Path) -> set[str]:
    """Keys listed here are exempt from unused-key pruning.

    This is intentionally a plain text list so it works in any editor and doesn't depend on chat context.
    """
    keep: set[str] = set()
    p = root / 'Tools' / 'LocalizationKeepKeys.txt'
    if not p.exists():
        return keep
    try:
        lines = p.read_text(encoding='utf-8').splitlines()
    except UnicodeDecodeError:
        lines = p.read_text(encoding='utf-8', errors='ignore').splitlines()
    for line in lines:
        s = line.strip()
        if not s:
            continue
        if s.startswith('#') or s.startswith('//'):
            continue
        keep.add(s)
    return keep


def prune_unused_keyed(root: Path, unused: set[str]) -> int:
    removed = 0
    keyed_dir = root / 'Languages' / 'English' / 'Keyed'
    if not keyed_dir.exists():
        return 0
    for f in keyed_dir.glob('*.xml'):
        try:
            tree = ET.parse(f)
        except ET.ParseError:
            continue
        ld = tree.getroot()
        if ld.tag != 'LanguageData':
            continue
        changed = False
        for child in list(ld):
            if isinstance(child.tag, str) and child.tag in unused:
                ld.remove(child)
                removed += 1
                changed = True

        # If the file ends up with zero keyed entries, delete it to avoid shipping dead stubs.
        remaining_elements = [c for c in ld if isinstance(c.tag, str)]
        if len(remaining_elements) == 0:
            try:
                f.unlink()
            except OSError:
                # fallback: keep an empty file if delete fails
                tree.write(f, encoding='utf-8', xml_declaration=True)
            continue

        if changed:
            tree.write(f, encoding='utf-8', xml_declaration=True)
    return removed


def collect_translate_keys(source_dir: Path) -> dict[str, list[tuple[Path,int]]]:
    out: dict[str, list[tuple[Path,int]]] = defaultdict(list)
    for cs in iter_cs_files_under(source_dir):
        try:
            text = cs.read_text(encoding='utf-8')
        except UnicodeDecodeError:
            text = cs.read_text(encoding='utf-8', errors='ignore')
        for m in TRANSLATE_KEY_RE.finditer(text):
            key = m.group(1)
            if not key.strip():
                continue
            out[key].append((cs, count_line(text, m.start())))
    return out


def load_keep_definjected(root: Path) -> list[str]:
    """Load wildcard patterns for DefInjected orphan suppression."""
    p = root / 'Tools' / 'DefInjectedKeep.txt'
    if not p.exists():
        return []
    out: list[str] = []
    for line in p.read_text(encoding='utf-8', errors='ignore').splitlines():
        s = line.strip()
        if not s or s.startswith('#'):
            continue
        out.append(s)
    return out


def load_def_names_by_type(root: Path) -> dict[str, set[str]]:
    """Collect defName values from Defs, grouped by DefType."""
    out: dict[str, set[str]] = defaultdict(set)
    defs_root = root / 'Defs'
    if not defs_root.exists():
        return out
    for f in defs_root.rglob('*.xml'):
        try:
            tree = ET.parse(f)
        except ET.ParseError:
            continue
        root_el = tree.getroot()
        if root_el.tag != 'Defs':
            continue
        for def_el in list(root_el):
            if not isinstance(def_el.tag, str):
                continue
            def_type = def_el.tag
            dn = def_el.findtext('defName')
            if dn:
                out[def_type].add(dn.strip())
    return out


def iter_definjected_nodes(root: Path):
    """Yield (def_type, file_path, node_tag) for English DefInjected nodes."""
    di_root = root / 'Languages' / 'English' / 'DefInjected'
    if not di_root.exists():
        return
    for def_type_dir in di_root.iterdir():
        if not def_type_dir.is_dir():
            continue
        for f in def_type_dir.glob('*.xml'):
            try:
                tree = ET.parse(f)
            except ET.ParseError:
                continue
            ld = tree.getroot()
            if ld.tag != 'LanguageData':
                continue
            for child in ld:
                if isinstance(child.tag, str):
                    yield def_type_dir.name, f, child.tag


def is_kept_definjected(def_type: str, node_tag: str, patterns: list[str]) -> bool:
    if not patterns:
        return False
    full = f'{def_type}/{node_tag}'
    for pat in patterns:
        if '/' in pat:
            if fnmatch.fnmatch(full, pat):
                return True
        else:
            if fnmatch.fnmatch(node_tag, pat):
                return True
    return False


def prune_orphan_definjected_autogen(root: Path, def_names_by_type: dict[str, set[str]], keep_patterns: list[str]) -> int:
    """Remove orphan nodes from AutoGenerated_DefInjected.xml only. Returns removed count."""
    removed_total = 0
    di_root = root / 'Languages' / 'English' / 'DefInjected'
    if not di_root.exists():
        return 0
    for def_type_dir in di_root.iterdir():
        if not def_type_dir.is_dir():
            continue
        auto = def_type_dir / 'AutoGenerated_DefInjected.xml'
        if not auto.exists():
            continue
        try:
            tree = ET.parse(auto)
        except ET.ParseError:
            continue
        ld = tree.getroot()
        if ld.tag != 'LanguageData':
            continue
        keep_nodes = []
        def_set = def_names_by_type.get(def_type_dir.name, set())
        for child in list(ld):
            if not isinstance(child.tag, str):
                keep_nodes.append(child)
                continue
            tag = child.tag
            if is_kept_definjected(def_type_dir.name, tag, keep_patterns):
                keep_nodes.append(child)
                continue
            if '.' not in tag:
                keep_nodes.append(child)
                continue
            def_name = tag.split('.', 1)[0]
            if def_name in def_set:
                keep_nodes.append(child)
            else:
                removed_total += 1
        # rewrite if changed
        if removed_total:
            # clear and re-add
            ld[:] = keep_nodes
            tree.write(auto, encoding='utf-8', xml_declaration=True)
    return removed_total


def find_orphan_definjected_manual(root: Path, def_names_by_type: dict[str, set[str]], keep_patterns: list[str]) -> list[tuple[Path, str, str]]:
    """Return list of (file_path, def_type, node_tag) orphans in non-auto DefInjected files."""
    out: list[tuple[Path, str, str]] = []
    di_root = root / 'Languages' / 'English' / 'DefInjected'
    if not di_root.exists():
        return out
    for def_type_dir in di_root.iterdir():
        if not def_type_dir.is_dir():
            continue
        def_set = def_names_by_type.get(def_type_dir.name, set())
        for f in def_type_dir.glob('*.xml'):
            if f.name == 'AutoGenerated_DefInjected.xml':
                continue
            try:
                tree = ET.parse(f)
            except ET.ParseError:
                continue
            ld = tree.getroot()
            if ld.tag != 'LanguageData':
                continue
            for child in ld:
                if not isinstance(child.tag, str):
                    continue
                tag = child.tag
                if is_kept_definjected(def_type_dir.name, tag, keep_patterns):
                    continue
                if '.' not in tag:
                    continue
                def_name = tag.split('.', 1)[0]
                if def_name not in def_set:
                    out.append((f, def_type_dir.name, tag))
    return out



def is_ui_literal_candidate_path(path: Path) -> bool:
    normalized = '/' + path.as_posix() + '/'
    return any(hint in normalized for hint in UI_LITERAL_FILE_HINTS)


def is_probably_internal_literal(value: str) -> bool:
    if value is None:
        return True
    s = value.strip()
    if not s:
        return True
    if any(ch.isspace() for ch in s):
        return False
    if any(ch in s for ch in '/_'):
        return True
    if '.' in s and s.lower() != s and s.upper() != s:
        return True
    return bool(re.fullmatch(r'[A-Za-z0-9:#+\-\[\]]+', s)) is False and not any(ch in s for ch in '.!?()')


def line_has_suspicious_literal(line: str) -> bool:
    literals = [m.group(1) for m in UI_LITERAL_STRING_RE.finditer(line)]
    if not literals:
        return False
    for lit in literals:
        if not is_probably_internal_literal(lit):
            return True
    return False


def collect_ui_literal_warnings(source_dir: Path) -> list[tuple[Path,int,str]]:
    warnings: list[tuple[Path,int,str]] = []
    for cs in iter_cs_files_under(source_dir):
        try:
            text = cs.read_text(encoding='utf-8')
        except UnicodeDecodeError:
            text = cs.read_text(encoding='utf-8', errors='ignore')
        lines = text.splitlines()
        path_candidate = is_ui_literal_candidate_path(cs.relative_to(ROOT))
        for i, line in enumerate(lines, start=1):
            stripped = line.strip()
            if any(marker in line for marker in UI_LITERAL_ALLOW_MARKERS):
                continue
            if '.Translate(' in line or '.Translate (' in line:
                continue
            if '"' not in line:
                continue
            if stripped.startswith('//') or stripped.startswith('///') or stripped.startswith('using ') or stripped.startswith('namespace ') or stripped.startswith('['):
                continue
            if 'nameof(' in line:
                continue

            matched = False
            for pat in UI_LITERAL_PATTERNS:
                if pat.search(line):
                    matched = True
                    break

            if not matched and path_candidate and UI_LITERAL_FALLBACK_RE.search(line):
                matched = True

            if matched and line_has_suspicious_literal(line):
                warnings.append((cs, i, line.strip()))
    return warnings


def collect_def_text_fields(root: Path) -> dict[str, dict[str, str]]:
    # defType -> { entryName: text }
    out: dict[str, dict[str, str]] = defaultdict(dict)
    defs_dir = root / 'Defs'
    if not defs_dir.exists():
        return out
    for f in defs_dir.rglob('*.xml'):
        try:
            tree = ET.parse(f)
        except ET.ParseError:
            continue
        r = tree.getroot()
        if r.tag != 'Defs':
            continue
        for defnode in r:
            if not isinstance(defnode.tag, str):
                continue
            def_type = defnode.tag
            def_name = defnode.findtext('defName')
            if not def_name:
                continue
            for field in DEF_FIELDS_TO_MIRROR:
                node = defnode.find(field)
                if node is None:
                    continue
                entry = f'{def_name}.{field}'
                out[def_type][entry] = (node.text or '').strip()
    return out


def load_definjected_entries(root: Path) -> dict[str, set[str]]:
    out: dict[str, set[str]] = defaultdict(set)
    di_root = root / 'Languages' / 'English' / 'DefInjected'
    if not di_root.exists():
        return out
    for def_type_dir in di_root.iterdir():
        if not def_type_dir.is_dir():
            continue
        for f in def_type_dir.glob('*.xml'):
            try:
                tree = ET.parse(f)
            except ET.ParseError:
                continue
            ld = tree.getroot()
            if ld.tag != 'LanguageData':
                continue
            for child in ld:
                if isinstance(child.tag, str):
                    out[def_type_dir.name].add(child.tag)
    return out


def ensure_definjected_autofile(root: Path, def_type: str) -> Path:
    d = root / 'Languages' / 'English' / 'DefInjected' / def_type
    d.mkdir(parents=True, exist_ok=True)
    return d / 'AutoGenerated_DefInjected.xml'


def fix_missing_definjected(root: Path, missing: dict[str, dict[str, str]]) -> int:
    added = 0
    for def_type, entries in missing.items():
        if not entries:
            continue
        auto_path = ensure_definjected_autofile(root, def_type)
        if auto_path.exists():
            try:
                tree = ET.parse(auto_path)
                ld = tree.getroot()
            except ET.ParseError:
                # rebuild file if corrupted
                ld = ET.Element('LanguageData')
                tree = ET.ElementTree(ld)
        else:
            ld = ET.Element('LanguageData')
            tree = ET.ElementTree(ld)
            # XML declaration is added by ElementTree when writing with xml_declaration=True
        existing = {c.tag for c in ld if isinstance(c.tag, str)}
        for name, text in sorted(entries.items()):
            if name in existing:
                continue
            el = ET.SubElement(ld, name)
            el.text = text
            existing.add(name)
            added += 1
        tree.write(auto_path, encoding='utf-8', xml_declaration=True)
    return added


def run_localization_checks(root: Path, findings: list[tuple[str, str]], fix_definjected: bool, prune_unused: bool, fail_on_ui_literals: bool = False) -> None:
    source_dir = root / 'Source'
    keyed = load_keyed_keys(root)
    keep = load_keep_keys(root)
    used = collect_translate_keys(source_dir)
    missing_keys = sorted(k for k in used.keys() if k not in keyed)

    if missing_keys:
        for k in missing_keys:
            occ = used[k][0]
            add_finding(findings, ERROR, occ[0], occ[1], f'missing Keyed translation for Translate() key -> {k}')


    # Translation value validation (prevents RimWorld "Translation data ... has N errors")
    dup: dict[tuple[str,str], list[Path]] = defaultdict(list)
    for kind, f, k, value in iter_language_entries(root):
        dup[(kind, k)].append(f)

        # Forbidden navigation arrows / angle-bracket characters in plain text.
        if any(s in value for s in TRANSLATION_FORBIDDEN_SUBSTRINGS) or any(ch in value for ch in TRANSLATION_FORBIDDEN_CHARS):
            findings.append((ERROR, f'Localization: {kind} entry <{k}> in {f.relative_to(ROOT)} contains forbidden "->" / angle-bracket character(s). Use Unicode arrows (→) or "/" separators; avoid raw ">" in translation strings.'))

        brace_issue = validate_braces_and_placeholders(value)
        if brace_issue:
            findings.append((ERROR, f'Localization: {kind} entry <{k}> in {f.relative_to(ROOT)} has invalid placeholder/braces: {brace_issue}'))

    # Duplicate translation keys cause RimWorld translation load errors (and last-one-wins behavior).
    duplicates = [(kind, k, files) for (kind, k), files in dup.items() if len(files) > 1]
    if duplicates:
        for kind, k, files in sorted(duplicates, key=lambda x: (x[0], x[1]))[:12]:
            sample = ', '.join(str(p.relative_to(ROOT)) for p in files[:3])
            findings.append((ERROR, f'Localization: duplicate {kind} translation key <{k}> found in multiple files: {sample}'))
        findings.append((ERROR, f'Localization: found {len(duplicates)} duplicate translation key(s) total. Remove duplicates to avoid translation injection errors.'))

    # heuristic warnings / optional release blocker for likely unlocalized UI literals
    ui_warnings = collect_ui_literal_warnings(source_dir)
    severity = ERROR if fail_on_ui_literals else REVIEW
    for cs, line, sample in ui_warnings[:25]:
        add_finding(findings, severity, cs, line, f'possible unlocalized UI literal (wrap in .Translate() or add // loc-ignore / // loc-allow-internal) -> {sample}')
    if len(ui_warnings) > 25:
        findings.append((severity, f'Localization: found {len(ui_warnings)} possible unlocalized UI literal(s) total. Showing first 25 above.'))

    # DefInjected coverage
    need = collect_def_text_fields(root)
    have = load_definjected_entries(root)
    missing_di: dict[str, dict[str, str]] = defaultdict(dict)
    for def_type, entries in need.items():
        have_set = have.get(def_type, set())
        for entry_name, text in entries.items():
            if entry_name not in have_set:
                missing_di[def_type][entry_name] = text

    total_missing = sum(len(v) for v in missing_di.values())
    if total_missing:
        if fix_definjected:
            added = fix_missing_definjected(root, missing_di)
            if added:
                findings.append((WARN, f'Localization: auto-generated {added} missing English DefInjected entry/entries into AutoGenerated_DefInjected.xml'))
        else:
            findings.append((WARN, f'Localization: missing {total_missing} English DefInjected entry/entries (run with --fix-definjected to generate scaffolding)'))


    # DefInjected orphan cleanup/check (safe prune: auto-generated only)
    def_names = load_def_names_by_type(root)
    di_keep = load_keep_definjected(root)
    if fix_definjected:
        pruned = prune_orphan_definjected_autogen(root, def_names, di_keep)
        if pruned:
            findings.append((WARN, f'Localization: pruned {pruned} orphan DefInjected node(s) from AutoGenerated_DefInjected.xml (use Tools/DefInjectedKeep.txt to keep intentional orphans).'))
    manual_orphans = find_orphan_definjected_manual(root, def_names, di_keep)
    if manual_orphans:
        sample = '; '.join([f'{p.as_posix()} -> <{tag}>' for p, _dt, tag in manual_orphans[:8]])
        findings.append((ERROR, f'DefInjected: found {len(manual_orphans)} orphan node(s) in non-auto files (defName no longer exists). Remove them or add a keep pattern in Tools/DefInjectedKeep.txt. Sample: {sample}'))
    # Unused Keyed cleanup
    used_keys = set(used.keys())
    unused = sorted(k for k in keyed if (k not in used_keys and k not in keep))
    if unused:
        if prune_unused:
            removed = prune_unused_keyed(root, set(unused))
            findings.append((WARN, f'Localization: pruned {removed} unused Keyed key(s) from English/Keyed (kept {len(keep)} key(s) via Tools/LocalizationKeepKeys.txt).'))
        else:
            sample = ', '.join(unused[:10])
            findings.append((WARN, f'Localization: found {len(unused)} unused English Keyed key(s). Sample: {sample}. Run with --prune-unused-keyed to auto-remove (or add to Tools/LocalizationKeepKeys.txt to keep).'))


def count_line(text: str, index: int) -> int:
    return text.count('\n', 0, index) + 1


def add_finding(findings: list[tuple[str, str]], severity: str, path: Path, line: int, message: str) -> None:
    findings.append((severity, f'{path.relative_to(ROOT)}:{line}: {message}'))


def check_empty_catches(path: Path, text: str, findings: list[tuple[str, str]]) -> None:
    for match in EMPTY_CATCH_RE.finditer(text):
        add_finding(findings, ERROR, path, count_line(text, match.start()), 'empty catch block')


def has_thread_static_nearby(lines: list[str], index: int) -> bool:
    start = max(0, index - 2)
    context = ''.join(lines[start:index + 1])
    return 'ThreadStatic' in context


def nearby_static_allow_reason(lines: list[str], index: int) -> str | None:
    start = max(0, index - 2)
    end = min(len(lines), index + 3)
    context = ''.join(lines[start:end])
    match = STATIC_ALLOW_RE.search(context)
    if match:
        return ' '.join(match.group('reason').split())
    return None


def file_large_reason(text: str) -> str | None:
    match = LARGE_FILE_REASON_RE.search(text)
    if match:
        return ' '.join(match.group('reason').split())
    return None


def should_skip_static_field(path: Path, name: str, declared_type: str) -> bool:
    normalized_type = ' '.join(declared_type.split())

    if path.name.endswith('DefOf.cs'):
        return True

    if normalized_type == 'Harmony':
        return True

    if path.name == 'ModMain.cs' and name == 'Instance':
        return True

    return False


def check_mutable_statics(path: Path, text: str, findings: list[tuple[str, str]]) -> None:
    lines = text.splitlines(keepends=True)
    for index, line in enumerate(lines):
        match = FIELD_LINE_RE.match(line)
        if not match:
            continue

        if has_thread_static_nearby(lines, index):
            continue

        declared_type = ' '.join(match.group('type').split())
        name = match.group('name')
        line_number = index + 1

        if should_skip_static_field(path, name, declared_type):
            continue

        static_reason = nearby_static_allow_reason(lines, index)

        if any(kind in declared_type for kind in STATIC_COLLECTION_TYPES):
            if static_reason:
                continue
            add_finding(findings, WARN, path, line_number, f'shared static collection -> {name} ({declared_type}); add Guardrail-Allow-Static with owner/lifecycle or refactor')
            continue

        if static_reason:
            continue

        add_finding(findings, WARN, path, line_number, f'mutable static candidate -> {name} ({declared_type}); add Guardrail-Allow-Static with owner/lifecycle or refactor')


def check_large_files(path: Path, text: str, findings: list[tuple[str, str]], review_file_lines: int, reason_file_lines: int, max_file_lines: int) -> None:
    line_count = text.count('\n') + 1
    reason = file_large_reason(text)

    if line_count > max_file_lines:
        if reason:
            add_finding(findings, WARN, path, 1, f'large file -> {line_count} lines (>{max_file_lines}) but documented: {reason}')
        else:
            add_finding(findings, ERROR, path, 1, f'large file -> {line_count} lines (>{max_file_lines}); refactor by default or add Guardrail-Reason explaining the engine-seam/runtime-risk exception')
        return

    if reason and line_count > review_file_lines:
        return

    if line_count > reason_file_lines:
        add_finding(findings, WARN, path, 1, f'large file -> {line_count} lines (>{reason_file_lines}); add Guardrail-Reason or split at a real responsibility boundary')
        return

    if line_count > review_file_lines:
        add_finding(findings, REVIEW, path, 1, f'large file review -> {line_count} lines (>{review_file_lines}); review for multiple reasons to change')


def check_generic_names(path: Path, text: str, findings: list[tuple[str, str]]) -> None:
    for match in GENERIC_DECL_RE.finditer(text):
        hint = match.group('name')
        add_finding(findings, REVIEW, path, count_line(text, match.start('name')), f'generic naming smell -> "{hint}"')
        break


def check_generated_dirs(root: Path, findings: list[tuple[str, str]]) -> None:
    for name in GENERATED_DIR_NAMES:
        for path in root.rglob(name):
            if path.is_dir():
                findings.append((WARN, f'{path.relative_to(ROOT)}: generated folder should not be committed'))

def check_core_sfw_separation(root: Path, findings: list[tuple[str, str]]) -> None:
    """Prevent accidentally pulling optional add-on assets into Core."""
    src = root / 'Source'
    if src.exists():
        for path in src.rglob('*.cs'):
            try:
                text = path.read_text(encoding='utf-8')
            except UnicodeDecodeError:
                text = path.read_text(encoding='utf-8', errors='ignore')
            for s in CORE_FORBIDDEN_SUBSTRINGS:
                if s in text:
                    findings.append((ERROR, f'Core must not reference "{s}" (optional add-on icon path). Found in: {path.relative_to(root)}'))
                    break

    # Also ensure the icon textures are not accidentally copied into Core.
    tex_dir = root / 'Textures' / 'UI' / 'Interaction'
    if tex_dir.exists():
        findings.append((ERROR, f'Core must not ship UI/Interaction textures. Move them to the add-on. Found dir: {tex_dir.relative_to(root)}'))


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description='Lightweight repository guardrail checks.')
    parser.add_argument('--review-file-lines', type=int, default=300, help='Review files that exceed this many lines (default: 300).')
    parser.add_argument('--reason-file-lines', type=int, default=400, help='Warn when a source file exceeds this many lines without Guardrail-Reason (default: 400).')
    parser.add_argument('--max-file-lines', type=int, default=500, help='Error when a source file exceeds this many lines without Guardrail-Reason (default: 500).')
    parser.add_argument('--warn-only', action='store_true', help='Print findings but always exit successfully.')
    
    parser.add_argument('--fix-definjected', action='store_true', help='Generate missing English DefInjected entries into AutoGenerated_DefInjected.xml.')
    parser.add_argument('--prune-unused-keyed', action='store_true', help='Remove unused English Keyed entries (except those listed in Tools/LocalizationKeepKeys.txt).')
    parser.add_argument('--fail-on-ui-literals', action='store_true', help='Treat likely player-facing UI literals as errors instead of review findings.')
    args = parser.parse_args(argv)

    if not (args.review_file_lines < args.reason_file_lines < args.max_file_lines):
        parser.error('Expected review-file-lines < reason-file-lines < max-file-lines.')

    return args


def print_findings(findings: list[tuple[str, str]]) -> None:
    buckets: dict[str, list[str]] = defaultdict(list)
    for severity, message in findings:
        buckets[severity].append(message)

    print('Guardrail findings:')
    for severity in (ERROR, WARN, REVIEW):
        items = buckets.get(severity, [])
        if not items:
            continue
        print(f'[{severity}]')
        for item in items:
            print(f' - {item}')
    print()
    print(f'Total findings: {len(findings)}')
    print('Breakdown: ' + ', '.join(f'{severity}={len(buckets.get(severity, []))}' for severity in (ERROR, WARN, REVIEW)))


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    findings: list[tuple[str, str]] = []

    for path in iter_source_files(ROOT):
        try:
            text = path.read_text(encoding='utf-8')
        except UnicodeDecodeError:
            text = path.read_text(encoding='utf-8', errors='ignore')

        check_empty_catches(path, text, findings)
        check_mutable_statics(path, text, findings)
        check_large_files(path, text, findings, args.review_file_lines, args.reason_file_lines, args.max_file_lines)
        check_generic_names(path, text, findings)

    check_generated_dirs(ROOT, findings)

    # Localization checks (Keyed coverage + DefInjected scaffolding)
    run_localization_checks(ROOT, findings, fix_definjected=args.fix_definjected, prune_unused=args.prune_unused_keyed, fail_on_ui_literals=args.fail_on_ui_literals)

    if findings:
        print_findings(findings)
        if args.warn_only:
            print('Exiting successfully because --warn-only was used.')
            return 0
        return 1 if any(severity == ERROR for severity, _ in findings) else 0

    print('Guardrail check passed. No findings.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
