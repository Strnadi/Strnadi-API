#!/usr/bin/env python3
"""One-time ETL: old monolithic Strnadi DB -> split Administration + Tenant schemas.

Reads from the old single database (users/devices/recordings/photos/... all in one
schema) and writes into two already-migrated, empty target databases:
  - Administration (Identity/OpenIddict: users, user_logins, user_roles,
    project_memberships, ...)
  - Tenant (devices/recordings/achievements/articles/dialects/recording_photos/...)

Run this against fresh (schema-migrated, data-empty) target databases only - it
INSERTs unconditionally and will fail on conflict if run twice against the same
targets.

Usage:
    migrate_to_split_schema.py --old <dsn> --admin <dsn> --tenant <dsn> [--dry-run]

DSNs default to the OLD_DSN / ADMIN_DSN / TENANT_DSN environment variables if not
passed explicitly. Postgres DSN format: postgresql://user:password@host:port/dbname
"""

from __future__ import annotations

import argparse
import base64
import csv
import os
import sys
import time
import uuid

import psycopg2
import psycopg2.extras
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

# --- fixed IDs, already created in the target Administration DB -------------

STRNADI_PROJECT_ID = "01a08608-44b7-7aba-8d0c-542148b30bf2"
ADMIN_ROLE_ID = "01a0860e-9248-718e-b041-2794b8e3b940"

# TODO: fill in with the real base64 Encryption:Key before running for real.
ENCRYPTION_KEY_B64 = "PLACEHOLDER_BASE64_KEY"


# --- uuid v7 (time-ordered, matches Guid.CreateVersion7() on the C# side) ---

def uuid7() -> str:
    ts_ms = int(time.time() * 1000)
    b = bytearray(ts_ms.to_bytes(6, "big") + os.urandom(10))
    b[6] = (b[6] & 0x0F) | 0x70  # version 7
    b[8] = (b[8] & 0x3F) | 0x80  # variant 10
    return str(uuid.UUID(bytes=bytes(b)))


# --- encryption: matches AesEncryptionService (AES-CBC/PKCS7, base64(IV+ciphertext)) ---

def encrypt_field(plaintext: str | None) -> str | None:
    if not plaintext:
        return plaintext
    key = base64.b64decode(ENCRYPTION_KEY_B64)
    iv = os.urandom(16)
    padder = padding.PKCS7(algorithms.AES.block_size).padder()
    padded = padder.update(plaintext.encode("utf-8")) + padder.finalize()
    encryptor = Cipher(algorithms.AES(key), modes.CBC(iv)).encryptor()
    ciphertext = encryptor.update(padded) + encryptor.finalize()
    return base64.b64encode(iv + ciphertext).decode("ascii")


def write_id_map_csv(id_map: dict[int, str], path: str) -> None:
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["old_user_id", "new_user_uuid"])
        for old_id, new_id in sorted(id_map.items()):
            writer.writerow([old_id, new_id])
    print(f"id map written to {path} ({len(id_map)} rows) - keep this, it's the only record of old_id -> new_uuid")


def reset_serial(cur, table: str, column: str = "id") -> None:
    cur.execute(
        "SELECT setval(pg_get_serial_sequence(%s, %s), COALESCE((SELECT MAX(%s) FROM %s), 1))"
        % ("%s", "%s", column, table),
        (table, column),
    )
    
    
# --- users: the only table that needs the old_id -> new_uuid map -----------

def migrate_users(old, admin, dry_run: bool) -> dict[int, str]:
    id_map: dict[int, str] = {}

    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, admin.cursor() as admin_cur:
        old_cur.execute("""
            SELECT DISTINCT ON (u.id) u.*, p.file_path AS profile_photo_path, p.format AS profile_photo_format
            FROM users u
            LEFT JOIN photos p ON p.user_id = u.id
            ORDER BY u.id, p.id DESC
        """)

        for row in old_cur:
            new_id = uuid7()
            id_map[row["id"]] = new_id

            admin_cur.execute("""
                INSERT INTO users (
                    id, user_name, normalized_user_name, email, normalized_email,
                    email_confirmed, password_hash, security_stamp, concurrency_stamp,
                    phone_number, phone_number_confirmed, two_factor_enabled,
                    lockout_end, lockout_enabled, access_failed_count,
                    first_name, last_name, created_at, post_code, city,
                    legacy, deleted, profile_photo_path, profile_photo_format, consent
                ) VALUES (
                    %s, %s, %s, %s, %s,
                    %s, %s, %s, %s,
                    %s, %s, %s,
                    %s, %s, %s,
                    %s, %s, %s, %s, %s,
                    %s, %s, %s, %s, %s
                )
            """, (
                new_id,
                row["nickname"], (row["nickname"].upper() if row["nickname"] else None),
                row["email"], (row["email"].upper() if row["email"] else None),
                bool(row["is_email_verified"]), row["password"],
                str(uuid.uuid4()), str(uuid.uuid4()),
                None, False, False,
                None, True, 0,
                encrypt_field(row["first_name"]), encrypt_field(row["last_name"]),
                row["creation_date"], row["post_code"], encrypt_field(row["city"]),
                bool(row["legacy"]), bool(row["deleted"]),
                row["profile_photo_path"], row["profile_photo_format"],
                bool(row["consent"]),
            ))

            if row["appleid"]:
                admin_cur.execute("""
                    INSERT INTO user_logins (login_provider, provider_key, provider_display_name, user_id)
                    VALUES ('Apple', %s, 'Apple', %s)
                """, (row["appleid"], new_id))

            if row["google_id"]:
                admin_cur.execute("""
                    INSERT INTO user_logins (login_provider, provider_key, provider_display_name, user_id)
                    VALUES ('Google', %s, 'Google', %s)
                """, (row["google_id"], new_id))

            if row["role"] == "admin":
                admin_cur.execute(
                    "INSERT INTO user_roles (user_id, role_id) VALUES (%s, %s)",
                    (new_id, ADMIN_ROLE_ID),
                )

            admin_cur.execute(
                "INSERT INTO project_memberships (id, user_id, project_id) VALUES (%s, %s, %s)",
                (uuid7(), new_id, STRNADI_PROJECT_ID),
            )

    print(f"users: {len(id_map)} migrated" + (" (dry-run, will roll back)" if dry_run else ""))
    return id_map


# --- tenant: devices / recordings / user_achievement need the id_map -------

def migrate_devices(old, tenant, id_map: dict[int, str]) -> None:
    skipped = 0
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM devices")
        rows = old_cur.fetchall()
        for row in rows:
            new_user_id = id_map.get(row["user_id"])
            if new_user_id is None:
                skipped += 1
                continue
            t_cur.execute("""
                INSERT INTO devices (id, fcm_token, device_platform, device_model, user_id)
                VALUES (%s, %s, %s, %s, %s)
            """, (row["id"], row["fcm_token"], row["device_platform"], row["device_model"], new_user_id))
        reset_serial(t_cur, "devices")
    print(f"devices: {len(rows) - skipped} migrated, {skipped} skipped (no matching user)")


def migrate_recordings(old, tenant, id_map: dict[int, str]) -> None:
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM recordings")
        rows = old_cur.fetchall()
        for row in rows:
            new_user_id = id_map.get(row["user_id"]) if row["user_id"] is not None else None
            t_cur.execute("""
                INSERT INTO recordings (
                    id, created_at, estimated_birds_count, by_app, name, note, note_post,
                    device, user_id, deleted, legacy, expected_parts_count, upload_confirmed
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            """, (
                row["id"], row["created_at"], row["estimated_birds_count"], row["by_app"],
                row["name"], row["note"], row["note_post"], row["device"], new_user_id,
                bool(row["deleted"]), bool(row["legacy"]), row["expected_parts_count"],
                True,  # predates the upload-confirmation flow - historical data is already complete
            ))
        reset_serial(t_cur, "recordings")
    print(f"recordings: {len(rows)} migrated")


def migrate_user_achievements(old, tenant, id_map: dict[int, str]) -> None:
    skipped = 0
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM user_achievement")
        rows = old_cur.fetchall()
        for row in rows:
            new_user_id = id_map.get(row["user_id"])
            if new_user_id is None:
                skipped += 1
                continue
            t_cur.execute("""
                INSERT INTO user_achievement (id, user_id, achievement_id) VALUES (%s, %s, %s)
            """, (row["id"], new_user_id, row["achievement_id"]))
        reset_serial(t_cur, "user_achievement")
    print(f"user_achievement: {len(rows) - skipped} migrated, {skipped} skipped (no matching user)")


# --- tenant: recording-linked photos only (profile photos folded into users above) ---

def migrate_recording_photos(old, tenant) -> None:
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM photos WHERE recording_id IS NOT NULL")
        rows = old_cur.fetchall()
        for row in rows:
            t_cur.execute("""
                INSERT INTO recording_photos (id, recording_id, file_path, format) VALUES (%s, %s, %s, %s)
            """, (row["id"], row["recording_id"], row["file_path"], row["format"]))
        reset_serial(t_cur, "recording_photos")
    print(f"recording_photos: {len(rows)} migrated")


# --- tenant: everything else - straight copy, no user references at all ----

def migrate_achievements(old, tenant) -> None:
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM achievements")
        rows = old_cur.fetchall()
        for row in rows:
            t_cur.execute(
                "INSERT INTO achievements (id, image_path, sql) VALUES (%s, %s, %s)",
                (row["id"], row["image_path"], row["sql"]),
            )
        reset_serial(t_cur, "achievements")

        old_cur.execute("SELECT * FROM achievement_content")
        content_rows = old_cur.fetchall()
        for row in content_rows:
            t_cur.execute("""
                INSERT INTO achievement_content (id, title, description, language_code, achievement_id)
                VALUES (%s, %s, %s, %s, %s)
            """, (row["id"], row["title"], row["description"], row["language_code"], row["achievement_id"]))
        reset_serial(t_cur, "achievement_content")
    print(f"achievements: {len(rows)} migrated, achievement_content: {len(content_rows)} migrated")


def migrate_articles(old, tenant) -> None:
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM articles")
        for row in old_cur.fetchall():
            t_cur.execute(
                "INSERT INTO articles (id, name, description) VALUES (%s, %s, %s)",
                (row["id"], row["name"], row["description"]),
            )
        reset_serial(t_cur, "articles")

        old_cur.execute("SELECT * FROM article_categories")
        for row in old_cur.fetchall():
            t_cur.execute(
                'INSERT INTO article_categories (id, label, name, "order") VALUES (%s, %s, %s, %s)',
                (row["id"], row["label"], row["name"], row["order"]),
            )
        reset_serial(t_cur, "article_categories")

        old_cur.execute("SELECT * FROM article_attachments")
        for row in old_cur.fetchall():
            t_cur.execute(
                "INSERT INTO article_attachments (id, file_name, article_id) VALUES (%s, %s, %s)",
                (row["id"], row["file_name"], row["article_id"]),
            )
        reset_serial(t_cur, "article_attachments")

        old_cur.execute("SELECT * FROM article_category_assignment")
        for row in old_cur.fetchall():
            t_cur.execute(
                'INSERT INTO article_category_assignment (article_id, category_id, "order") VALUES (%s, %s, %s)',
                (row["article_id"], row["category_id"], row["order"]),
            )
    print("articles/article_categories/article_attachments/article_category_assignment migrated")


def migrate_dialects_and_parts(old, tenant) -> None:
    with old.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as old_cur, tenant.cursor() as t_cur:
        old_cur.execute("SELECT * FROM dialects")
        for row in old_cur.fetchall():
            t_cur.execute(
                "INSERT INTO dialects (id, dialect_code, color, hint_order) VALUES (%s, %s, %s, %s)",
                (row["id"], row["dialect_code"], row["color"], row["hint_order"]),
            )
        reset_serial(t_cur, "dialects")

        old_cur.execute("SELECT * FROM recording_parts")
        for row in old_cur.fetchall():
            t_cur.execute("""
                INSERT INTO recording_parts (
                    id, recording_id, start_date, end_date, gps_latitude_start, gps_longitude_start,
                    gps_latitude_end, gps_longitude_end, file_path, length
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            """, (
                row["id"], row["recording_id"], row["start_date"], row["end_date"],
                row["gps_latitude_start"], row["gps_longitude_start"],
                row["gps_latitude_end"], row["gps_longitude_end"], row["file_path"], row["length"],
            ))
        reset_serial(t_cur, "recording_parts")

        old_cur.execute("SELECT * FROM filtered_recording_parts")
        for row in old_cur.fetchall():
            t_cur.execute("""
                INSERT INTO filtered_recording_parts (
                    id, start_date, end_date, probability_vector, state, representant_flag, recording_id
                ) VALUES (%s, %s, %s, %s, %s, %s, %s)
            """, (
                row["id"], row["start_date"], row["end_date"], row["probability_vector"],
                row["state"], row["representant_flag"], row["recording_id"],
            ))
        reset_serial(t_cur, "filtered_recording_parts")

        old_cur.execute("SELECT * FROM detected_dialects")
        for row in old_cur.fetchall():
            t_cur.execute("""
                INSERT INTO detected_dialects (
                    id, user_guess_dialect_id, confirmed_dialect_id, filtered_recording_part_id, predicted_dialect_id
                ) VALUES (%s, %s, %s, %s, %s)
            """, (
                row["id"], row["user_guess_dialect_id"], row["confirmed_dialect_id"],
                row["filtered_recording_part_id"], row["predicted_dialect_id"],
            ))
        reset_serial(t_cur, "detected_dialects")
    print("dialects/recording_parts/filtered_recording_parts/detected_dialects migrated")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--old", default=os.environ.get("OLD_DSN"), help="Old monolithic DB DSN")
    parser.add_argument("--admin", default=os.environ.get("ADMIN_DSN"), help="Target Administration DB DSN")
    parser.add_argument("--tenant", default=os.environ.get("TENANT_DSN"), help="Target Tenant DB DSN")
    parser.add_argument("--dry-run", action="store_true", help="Run everything, then roll back instead of commit")
    parser.add_argument(
        "--id-map-out", default="user_id_map.csv",
        help="Where to write the old_user_id -> new_user_uuid mapping (default: user_id_map.csv). "
             "This is the only surviving record of the mapping once the script exits - keep it.",
    )
    args = parser.parse_args()

    if not args.old or not args.admin or not args.tenant:
        print("error: --old, --admin and --tenant (or OLD_DSN/ADMIN_DSN/TENANT_DSN) are all required", file=sys.stderr)
        return 1

    if ENCRYPTION_KEY_B64 == "PLACEHOLDER_BASE64_KEY":
        print("error: fill in ENCRYPTION_KEY_B64 with the real key before running", file=sys.stderr)
        return 1

    old = psycopg2.connect(args.old)
    admin = psycopg2.connect(args.admin)
    tenant = psycopg2.connect(args.tenant)

    try:
        id_map = migrate_users(old, admin, args.dry_run)
        migrate_devices(old, tenant, id_map)
        migrate_recordings(old, tenant, id_map)
        migrate_user_achievements(old, tenant, id_map)
        migrate_recording_photos(old, tenant)
        migrate_achievements(old, tenant)
        migrate_articles(old, tenant)
        migrate_dialects_and_parts(old, tenant)

        if args.dry_run:
            admin.rollback()
            tenant.rollback()
            print("dry-run: rolled back, nothing was actually written (id map file NOT written - it would not match anything real)")
        else:
            admin.commit()
            tenant.commit()
            write_id_map_csv(id_map, args.id_map_out)
            print("done: committed")
        return 0
    except Exception:
        admin.rollback()
        tenant.rollback()
        raise
    finally:
        old.close()
        admin.close()
        tenant.close()


if __name__ == "__main__":
    raise SystemExit(main())
