import unittest
from fix_representant_flag import boolean_columns, fix_statement, sql_statements


class DumpFixTests(unittest.TestCase):
    def test_multiline_preserves_other_values_and_format(self):
        ddl = '''CREATE TABLE "public"."sample" (
 "id" integer,
 "score" numeric(10,2),
 "note" text,
 "enabled" boolean,
 "legacy" boolean
) WITH (oids = false);'''
        insert = '''\nINSERT INTO "sample" ("id", "score", "note", "enabled", "legacy") VALUES
(1, 0, 'comma, semi; apostrophe''s', 0, 1),
(0, 1, 'two', NULL, FALSE);'''
        expected = insert.replace("apostrophe''s', 0, 1", "apostrophe''s', FALSE, TRUE")
        tables = boolean_columns(ddl)
        statements = list(sql_statements(ddl + insert))
        self.assertEqual(len(statements), 2)
        fixed, count = fix_statement(statements[1], tables)
        self.assertEqual((fixed, count), (expected, 2))
        self.assertEqual(fix_statement(fixed, tables), (fixed, 0))

    def test_implicit_column_order(self):
        ddl = 'CREATE TABLE sample (id integer, flag boolean);'
        self.assertEqual(
            fix_statement('INSERT INTO sample VALUES (1,0);', boolean_columns(ddl)),
            ('INSERT INTO sample VALUES (1,FALSE);', 1),
        )


if __name__ == '__main__':
    unittest.main()
