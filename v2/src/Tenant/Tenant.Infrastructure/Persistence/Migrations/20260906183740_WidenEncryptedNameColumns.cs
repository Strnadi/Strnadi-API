using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenEncryptedNameColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => ChangeNameColumnType(migrationBuilder, "text");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => ChangeNameColumnType(migrationBuilder, "character varying(50)");

        private static void ChangeNameColumnType(MigrationBuilder migrationBuilder, string columnType)
        {
            // Preserve the deployed legacy view; fresh databases do not have it.
            // No CASCADE: unexpected dependencies abort the transaction.
            migrationBuilder.Sql($$"""
                DO $migration$
                DECLARE
                    view_oid oid := to_regclass('public.dialects_to_do');
                    view_definition text;
                    view_owner text;
                    view_options text[];
                    view_comment text;
                    view_acl aclitem[];
                    privilege record;
                BEGIN
                    IF view_oid IS NOT NULL THEN
                        IF NOT EXISTS (SELECT 1 FROM pg_class WHERE oid = view_oid AND relkind = 'v') THEN
                            RAISE EXCEPTION 'public.dialects_to_do must be an ordinary view';
                        END IF;

                        -- Fail rather than lose column metadata not present in the legacy view.
                        IF EXISTS (
                            SELECT 1 FROM pg_attribute
                            WHERE attrelid = view_oid AND attnum > 0
                              AND (attacl IS NOT NULL OR col_description(view_oid, attnum) IS NOT NULL)
                        ) THEN
                            RAISE EXCEPTION 'dialects_to_do has column metadata requiring explicit preservation';
                        END IF;

                        SELECT pg_get_viewdef(oid), pg_get_userbyid(relowner), reloptions,
                               obj_description(oid, 'pg_class'), COALESCE(relacl, acldefault('r', relowner))
                        INTO view_definition, view_owner, view_options, view_comment, view_acl
                        FROM pg_class WHERE oid = view_oid;

                        DROP VIEW public.dialects_to_do;
                    END IF;

                    ALTER TABLE public.users ALTER COLUMN last_name TYPE {{columnType}};
                    ALTER TABLE public.users ALTER COLUMN first_name TYPE {{columnType}};

                    IF view_definition IS NOT NULL THEN
                        EXECUTE 'CREATE VIEW public.dialects_to_do'
                            || CASE WHEN view_options IS NULL THEN ''
                                    ELSE ' WITH (' || array_to_string(view_options, ', ') || ')' END
                            || ' AS ' || view_definition;

                        -- Remove grants introduced by current default privileges.
                        FOR privilege IN
                            SELECT DISTINCT grantee FROM pg_class,
                                LATERAL aclexplode(COALESCE(relacl, acldefault('r', relowner)))
                            WHERE oid = 'public.dialects_to_do'::regclass
                        LOOP
                            EXECUTE 'REVOKE ALL ON public.dialects_to_do FROM '
                                || CASE WHEN privilege.grantee = 0 THEN 'PUBLIC'
                                        ELSE quote_ident(pg_get_userbyid(privilege.grantee)) END;
                        END LOOP;

                        FOR privilege IN SELECT * FROM aclexplode(view_acl)
                        LOOP
                            EXECUTE 'GRANT ' || privilege.privilege_type
                                || ' ON public.dialects_to_do TO '
                                || CASE WHEN privilege.grantee = 0 THEN 'PUBLIC'
                                        ELSE quote_ident(pg_get_userbyid(privilege.grantee)) END
                                || CASE WHEN privilege.is_grantable THEN ' WITH GRANT OPTION' ELSE '' END;
                        END LOOP;

                        EXECUTE format('ALTER VIEW public.dialects_to_do OWNER TO %I', view_owner);
                        EXECUTE format('COMMENT ON VIEW public.dialects_to_do IS %L', view_comment);
                    END IF;
                END
                $migration$;
                """);
        }
    }
}
