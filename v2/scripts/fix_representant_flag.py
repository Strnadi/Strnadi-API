#!/usr/bin/env python3
"""Fix bare 0/1 values in boolean columns of an INSERT-format SQL dump.

Supports multiline INSERTs and quoted strings containing commas and semicolons.
Column types come from CREATE TABLE; only bare 0/1 boolean values are changed.
Other values and whitespace are preserved. Writes a separate output copy.

Usage:
    fix_representant_flag.py --in original.sql --out fixed.sql --show 0
    fix_representant_flag.py --in original.sql --dry-run --show 0
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

def parse_column_list(paren_text: str) -> list[str]:
    """'(id, start_date, ..., representant_flag, recording_id)' -> ['id', 'start_date', ...]"""
    inner = paren_text.strip()[1:-1]
    return [c.strip().strip('"') for c in split_top_level(inner)]


def split_top_level(text: str) -> list[str]:
    """Split on top-level commas only, respecting '...' quoted strings (with ''
    as the escaped-quote-inside-a-string convention) and (...) nesting."""
    parts: list[str] = []
    depth = 0
    in_string = False
    current: list[str] = []
    i = 0
    while i < len(text):
        ch = text[i]
        if in_string:
            if ch == "'":
                if i + 1 < len(text) and text[i + 1] == "'":
                    current.append("''")
                    i += 2
                    continue
                in_string = False
                current.append(ch)
            else:
                current.append(ch)
        else:
            if ch == "'":
                in_string = True
                current.append(ch)
            elif ch == "(":
                depth += 1
                current.append(ch)
            elif ch == ")":
                depth -= 1
                current.append(ch)
            elif ch == "," and depth == 0:
                parts.append("".join(current))
                current = []
            else:
                current.append(ch)
        i += 1
    parts.append("".join(current))
    return parts


def split_values_tuples(values_text: str) -> list[str]:
    """'(1,2,3), (4,5,6)' -> ['(1,2,3)', '(4,5,6)'] - same quote/paren awareness as split_top_level."""
    tuples: list[str] = []
    depth = 0
    in_string = False
    start = None
    i = 0
    while i < len(values_text):
        ch = values_text[i]
        if in_string:
            if ch == "'":
                if i + 1 < len(values_text) and values_text[i + 1] == "'":
                    i += 2
                    continue
                in_string = False
        else:
            if ch == "'":
                in_string = True
            elif ch == "(":
                if depth == 0:
                    start = i
                depth += 1
            elif ch == ")":
                depth -= 1
                if depth == 0 and start is not None:
                    tuples.append(values_text[start:i + 1])
                    start = None
        i += 1
    return tuples


def fix_value_tuple(tuple_text: str, flag_index: int) -> tuple[str, bool]:
    inner = tuple_text.strip()
    assert inner.startswith("(") and inner.endswith(")")
    values = split_top_level(inner[1:-1])

    if flag_index >= len(values):
        return tuple_text, False

    raw = values[flag_index].strip()
    if raw == "0":
        values[flag_index] = values[flag_index].replace(raw, "FALSE", 1)
        changed = True
    elif raw == "1":
        values[flag_index] = values[flag_index].replace(raw, "TRUE", 1)
        changed = True
    else:
        changed = False  # already NULL / TRUE / FALSE / quoted - leave as-is

    return "(" + ",".join(values) + ")", changed


def boolean_columns(sql_text: str) -> dict[str, tuple[list[str], list[str]]]:
    """Read boolean column names from each table declaration."""
    result = {}
    pattern = re.compile(
        r'CREATE TABLE\s+(?:[\w"]+\.)?"?(\w+)"?\s*\((.*?)\)\s*(?:WITH\s*\([^;]*\))?;',
        re.IGNORECASE | re.DOTALL,
    )
    for match in pattern.finditer(sql_text):
        columns = []
        flags = []
        for definition in split_top_level(match.group(2)):
            parts = definition.strip().split()
            if len(parts) < 2 or parts[0].upper() in {"CONSTRAINT", "PRIMARY", "UNIQUE", "CHECK", "FOREIGN"}:
                continue
            name = parts[0].strip('"')
            columns.append(name)
            if parts[1].lower() in {"boolean", "bool"}:
                flags.append(name)
        result[match.group(1)] = (columns, flags)
    return result


def fix_statement(statement: str, tables: dict) -> tuple[str, int]:
    match = re.search(
        r'INSERT INTO\s+(?:[\w"]+\.)?"?(\w+)"?\s*(\([^()]*\))?\s*VALUES\s*(.*);\s*$',
        statement, re.IGNORECASE | re.DOTALL,
    )
    if not match or match.group(1) not in tables:
        return statement, 0
    declared, flags = tables[match.group(1)]
    columns = parse_column_list(match.group(2)) if match.group(2) else declared
    indexes = [columns.index(flag) for flag in flags if flag in columns]
    if not indexes:
        return statement, 0
    values_text = match.group(3)
    pieces = []
    offset = changed_count = 0
    for tup in split_values_tuples(values_text):
        fixed = tup
        for index in indexes:
            fixed, changed = fix_value_tuple(fixed, index)
            changed_count += int(changed)
        start = values_text.index(tup, offset)
        pieces.extend([values_text[offset:start], fixed])
        offset = start + len(tup)
    if not changed_count:
        return statement, 0
    pieces.append(values_text[offset:])
    return statement[:match.start(3)] + "".join(pieces) + statement[match.end(3):], changed_count


def sql_statements(text: str):
    """Yield complete SQL statements, preserving whitespace and quoted semicolons."""
    start = i = 0
    quote = None
    line_comment = False
    block_depth = 0
    while i < len(text):
        ch = text[i]
        pair = text[i:i + 2]
        if line_comment:
            if ch == "\n":
                line_comment = False
        elif block_depth:
            if pair == "/*":
                block_depth += 1
                i += 1
            elif pair == "*/":
                block_depth -= 1
                i += 1
        elif quote:
            if ch == quote:
                if i + 1 < len(text) and text[i + 1] == quote:
                    i += 1
                else:
                    quote = None
        elif pair == "--":
            line_comment = True
            i += 1
        elif pair == "/*":
            block_depth = 1
            i += 1
        elif ch in ("'", '"'):
            quote = ch
        elif ch == ";":
            yield text[start:i + 1]
            start = i + 1
        i += 1
    if quote or block_depth:
        raise ValueError("Unterminated SQL quote or comment")
    if start < len(text):
        yield text[start:]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--in", dest="input_path", required=True, help="Original dump file (never modified)")
    parser.add_argument("--out", dest="output_path", help="Where to write the fixed copy (required unless --dry-run)")
    parser.add_argument("--dry-run", action="store_true", help="Only report how many values would change, write nothing")
    parser.add_argument("--show", type=int, default=5, metavar="N", help="Print the first N changed lines (before/after) for review")
    args = parser.parse_args()

    if not args.dry_run and not args.output_path:
        print("error: --out is required unless --dry-run", file=sys.stderr)
        return 1
    if args.output_path and Path(args.output_path).resolve() == Path(args.input_path).resolve():
        print("error: --out must not be the same file as --in", file=sys.stderr)
        return 1

    with open(args.input_path, encoding="utf-8", newline="") as f:
        sql_text = f.read()
    tables = boolean_columns(sql_text)
    if not tables:
        print("error: no CREATE TABLE declarations found", file=sys.stderr)
        return 1

    total_lines_changed = 0
    total_values_changed = 0
    shown = 0
    out_f = None if (args.dry_run or not args.output_path) else open(args.output_path, "w", encoding="utf-8", newline="")

    try:
        with open(args.input_path, encoding="utf-8", newline="") as in_f:
            for line in sql_statements(in_f.read()):
                if "INSERT INTO" in line.upper():
                    new_line, changed = fix_statement(line, tables)
                    if changed:
                        total_lines_changed += 1
                        total_values_changed += changed
                        if shown < args.show:
                            print(f"\n--- line {total_lines_changed} ({changed} value(s) fixed) ---")
                            print(f"before: {line.rstrip()[:200]}")
                            print(f"after:  {new_line.rstrip()[:200]}")
                            shown += 1
                    if out_f:
                        out_f.write(new_line)
                else:
                    if out_f:
                        out_f.write(line)
    finally:
        if out_f:
            out_f.close()

    print(f"\n{total_lines_changed} INSERT statement(s) changed, {total_values_changed} boolean value(s) fixed"
          + (" (dry-run, nothing written)" if args.dry_run or not args.output_path else f" -> {args.output_path}"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
