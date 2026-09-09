#!/usr/bin/env python3
"""Fix a pg_dump SQL file where filtered_recording_parts.representant_flag was
dumped as a bare integer (0/1) instead of a boolean literal.

Postgres rejects INSERT INTO ... VALUES (..., 0, ...) for a boolean column with:
    ERROR: column "representant_flag" is of type boolean but expression is of type integer
(an unquoted 0/1 is typed as integer by the parser before any cast to boolean is
even considered - quoted '0'/'1' would have worked fine, but that's not what got dumped here.)

This script finds every `INSERT INTO ... filtered_recording_parts ... VALUES (...);`
statement, parses each VALUES tuple with a real (quote-aware) tokenizer - not a
naive comma split, since probability_vector is a free-text varchar that may itself
contain commas inside quotes - and rewrites ONLY the representant_flag value:
    0    -> FALSE
    1    -> TRUE
    NULL -> left untouched
Every other value on the line, and every other line in the file, is left
byte-for-byte identical.

The representant_flag column index is taken from the statement's own explicit
column list when present (--column-inserts style dumps), otherwise from the
table's CREATE TABLE column order found earlier in the same file (plain
--inserts style dumps have no column list per statement).

Usage:
    fix_representant_flag.py --in strnadi_api.sql --out strnadi_api.fixed.sql
    fix_representant_flag.py --in strnadi_api.sql --out strnadi_api.fixed.sql --dry-run
    fix_representant_flag.py --in strnadi_api.sql --out strnadi_api.fixed.sql --show N

Never overwrites --in; always writes a separate --out file. Run --dry-run (or
--show) first and read the sample before trusting the real output file.
"""

from __future__ import annotations

import argparse
import re
import sys

TABLE_NAME = "filtered_recording_parts"
TARGET_COLUMN = "representant_flag"

CREATE_TABLE_RE = re.compile(
    r'CREATE TABLE\s+(?:[\w"]+\.)?"?%s"?\s*\((.*?)\);' % re.escape(TABLE_NAME),
    re.IGNORECASE | re.DOTALL,
)
INSERT_RE = re.compile(
    r'INSERT INTO\s+(?:[\w"]+\.)?"?%s"?\s*(\([^()]*\))?\s*VALUES\s*(.*);\s*$' % re.escape(TABLE_NAME),
    re.IGNORECASE,
)


def parse_column_list(paren_text: str) -> list[str]:
    """'(id, start_date, ..., representant_flag, recording_id)' -> ['id', 'start_date', ...]"""
    inner = paren_text.strip()[1:-1]
    return [c.strip().strip('"') for c in split_top_level(inner)]


def find_create_table_columns(sql_text_head: str) -> list[str] | None:
    match = CREATE_TABLE_RE.search(sql_text_head)
    if not match:
        return None
    columns = []
    for line in match.group(1).split(","):
        line = line.strip()
        if not line or line.upper().startswith(("PRIMARY KEY", "CONSTRAINT", "UNIQUE", "CHECK", "FOREIGN KEY")):
            continue
        name = line.split()[0].strip('"')
        columns.append(name)
    return columns or None


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
        values[flag_index] = " FALSE"
        changed = True
    elif raw == "1":
        values[flag_index] = " TRUE"
        changed = True
    else:
        changed = False  # already NULL / TRUE / FALSE / quoted - leave as-is

    return "(" + ",".join(values) + ")", changed


def fix_line(line: str, create_table_columns: list[str] | None) -> tuple[str, int]:
    match = INSERT_RE.search(line)
    if not match:
        return line, 0

    explicit_columns_text, values_text = match.groups()
    if explicit_columns_text:
        columns = parse_column_list(explicit_columns_text)
    elif create_table_columns:
        columns = create_table_columns
    else:
        print(
            f"warning: INSERT into {TABLE_NAME} with no column list and no CREATE TABLE seen yet - skipping line",
            file=sys.stderr,
        )
        return line, 0

    if TARGET_COLUMN not in columns:
        return line, 0
    flag_index = columns.index(TARGET_COLUMN)

    total_changed = 0
    new_tuples = []
    for tup in split_values_tuples(values_text):
        fixed, changed = fix_value_tuple(tup, flag_index)
        new_tuples.append(fixed)
        total_changed += int(changed)

    if total_changed == 0:
        return line, 0

    new_values_text = ", ".join(new_tuples)
    new_line = line[:match.start(2)] + new_values_text + line[match.end(2):]
    return new_line, total_changed


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
    if args.output_path and args.output_path == args.input_path:
        print("error: --out must not be the same file as --in", file=sys.stderr)
        return 1

    with open(args.input_path, encoding="utf-8") as f:
        head = f.read(2_000_000)  # CREATE TABLE is always near the top; 2MB is generous
    create_table_columns = find_create_table_columns(head)
    if create_table_columns and TARGET_COLUMN in create_table_columns:
        print(f"found CREATE TABLE {TABLE_NAME}, column order: {create_table_columns}")
    else:
        print(f"warning: could not find 'CREATE TABLE {TABLE_NAME}' with a '{TARGET_COLUMN}' column in the first 2MB - "
              f"will only be able to fix INSERTs that carry an explicit column list", file=sys.stderr)

    total_lines_changed = 0
    total_values_changed = 0
    shown = 0
    out_f = None if (args.dry_run or not args.output_path) else open(args.output_path, "w", encoding="utf-8")

    try:
        with open(args.input_path, encoding="utf-8") as in_f:
            for line in in_f:
                if TABLE_NAME in line and "INSERT INTO" in line.upper():
                    new_line, changed = fix_line(line, create_table_columns)
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

    print(f"\n{total_lines_changed} INSERT statement(s) changed, {total_values_changed} {TARGET_COLUMN} value(s) fixed"
          + (" (dry-run, nothing written)" if args.dry_run or not args.output_path else f" -> {args.output_path}"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
