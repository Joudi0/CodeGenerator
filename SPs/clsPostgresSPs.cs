using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator
{
    internal class clsPostgresSPs
    {
        public static string makeCase(clsHelper.Column C)
        {
            return $@"
                    CASE WHEN SortColumn = '{C.name}' AND Direction = 'ASC' THEN {clsHelper.tableName}.""{C.name}"" END ASC,
                    CASE WHEN SortColumn = '{C.name}' AND Direction = 'DESC' THEN {clsHelper.tableName}.""{C.name}"" END DESC";
        }

        private static string GetSPParameters(bool withFirstPar = false)
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            if (!withFirstPar) cols.RemoveAt(0);
            string parameters = "";
            foreach (clsHelper.Column col in cols)
            {
                parameters += $"\t{col.name} {FormatSqlType(col)},\n";
            }
            return parameters.TrimEnd('\n', ',');
        }

        private static string GetColumnNames()
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            cols.RemoveAt(0);
            return string.Join(", ", cols.Select(c => $@"""{c.name}"""));
        }

        private static string GetParameterValues()
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            cols.RemoveAt(0);
            return string.Join(", ", cols.Select(c => c.name));
        }

        private static string makeUpdateStatement()
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            string whereClause = $@"WHERE {clsHelper.tableName}.""{cols[0].name}"" = {cols[0].name}";
            cols.RemoveAt(0);

            string updateLines = string.Join(",\n\t\t\t", cols.Select(c => $@"{clsHelper.tableName}.""{c.name}"" = {c.name}"));
            return updateLines + "\n\t\t" + whereClause;
        }

        private static string FormatSqlType(clsHelper.Column C)
        {
            string t = C.type.ToLower();
            if (t.Contains("char") || t.Contains("text") || t == "string") return "VARCHAR";
            if (t == "int" || t == "integer" || t == "int32") return "INTEGER";
            if (t == "bigint" || t == "long" || t == "int64") return "BIGINT";
            if (t == "smallint" || t == "byte" || t == "short") return "SMALLINT";
            if (t == "bit" || t == "boolean" || t == "bool") return "BOOLEAN";
            if (t == "datetime" || t == "timestamp") return "TIMESTAMP";
            return C.type;
        }

        public static string addSP()
        {
            clsHelper.Column idCol = clsHelper.Columns[0];
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_insert(
            {GetSPParameters()}
            ) RETURNS INTEGER AS $$             #variable_conflict use_variable             DECLARE inserted_id INTEGER;             BEGIN                 INSERT INTO {clsHelper.tableName} ({GetColumnNames()})                 VALUES ({GetParameterValues()})                 RETURNING ""{idCol.name}"" INTO inserted_id;                 RETURN inserted_id;             END;             $$ LANGUAGE plpgsql;";
        }

        public static string updateSP()
        {
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_update(
            {GetSPParameters(true)}
            ) RETURNS VOID AS $$             #variable_conflict use_variable             BEGIN                 UPDATE {clsHelper.tableName} SET {makeUpdateStatement()};             END;             $$ LANGUAGE plpgsql;";
        }

        public static string deleteSP()
        {
            clsHelper.Column firstColumn = clsHelper.Columns[0];
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_delete(
                {firstColumn.name} {FormatSqlType(firstColumn)}
            ) RETURNS VOID AS $$             #variable_conflict use_variable             BEGIN                 DELETE FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.""{firstColumn.name}"" = {firstColumn.name};             END;             $$ LANGUAGE plpgsql;";
        }

        public static string selectAllSP()
        {
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_selectall()
            RETURNS SETOF {clsHelper.tableName} AS $$             BEGIN                 RETURN QUERY SELECT * FROM {clsHelper.tableName};             END;             $$ LANGUAGE plpgsql;";
        }

        public static string selectAllBySP(clsHelper.Column C)
        {
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_selectallby{C.name.ToLower()}(
                {C.name} {FormatSqlType(C)}
            ) RETURNS SETOF {clsHelper.tableName} AS $$             #variable_conflict use_variable             BEGIN                 RETURN QUERY SELECT * FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.""{C.name}"" = {C.name};             END;             $$ LANGUAGE plpgsql;";
        }

        public static string selectByColumnSP(clsHelper.Column col)
        {
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_selectby{col.name.ToLower()}(
                {col.name} {FormatSqlType(col)}
            ) RETURNS SETOF {clsHelper.tableName} AS $$             #variable_conflict use_variable             BEGIN                 RETURN QUERY SELECT * FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.""{col.name}"" = {col.name};             END;             $$ LANGUAGE plpgsql;";
        }

        public static string PagingSP()
        {
            List<string> casesList = new List<string>();
            foreach (clsHelper.Column C in clsHelper.mappedColumns)
            {
                casesList.Add(makeCase(C));
            }
            string allCases = string.Join(",\n\t\t\t\t", casesList);

            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_paging(
                RowsPerPage INT, 
                PageNumber INT, 
                SortColumn VARCHAR, 
                Direction VARCHAR
            ) RETURNS SETOF {clsHelper.tableName} AS $$             #variable_conflict use_variable             BEGIN                 RETURN QUERY                  SELECT * FROM {clsHelper.tableName}                  ORDER BY {allCases}                 LIMIT RowsPerPage OFFSET (PageNumber - 1) * RowsPerPage;             END;             $$ LANGUAGE plpgsql;";
        }

        public static string isExistByColumnSP(clsHelper.Column col)
        {
            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_isexistby{col.name.ToLower()}(
                {col.name} {FormatSqlType(col)}
            ) RETURNS BOOLEAN AS $$             #variable_conflict use_variable             BEGIN                 RETURN EXISTS(SELECT 1 FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.""{col.name}"" = {col.name});             END;             $$ LANGUAGE plpgsql;";
        }

        public static string loginSP()
        {
            clsHelper.Column userID = clsHelper.Columns[0];
            clsHelper.Column hash = clsHelper.Columns.Find(c => c.name.ToLower().Contains("hash"));
            clsHelper.Column salt = clsHelper.Columns.Find(c => c.name.ToLower().Contains("salt"));
            clsHelper.Column role = clsHelper.Columns.Find(c => c.name.ToLower().Contains("role"));
            clsHelper.Column username = clsHelper.Columns.Find(c => c.name.ToLower().Contains("user") && !c.name.ToLower().Contains("id") && !c.name.ToLower().Contains("role"));

            string hashName = (hash.name != null) ? hash.name : "PasswordHash";
            string saltName = (salt.name != null) ? salt.name : "PasswordSalt";
            string roleName = (role.name != null) ? role.name : "UserRoleID";
            string userNameCol = (username.name != null) ? username.name : "Username";

            return $@"
            CREATE OR REPLACE FUNCTION public.sp_{clsHelper.tableName.ToLower()}_getsecuritydatabyusername(
                {userNameCol} VARCHAR
            ) RETURNS TABLE(""{userID.name}"" INTEGER, ""{hashName}"" VARCHAR, ""{saltName}"" VARCHAR, ""{roleName}"" INTEGER) AS $$             #variable_conflict use_variable             BEGIN                 RETURN QUERY                  SELECT {clsHelper.tableName}.""{userID.name}"", {clsHelper.tableName}.""{hashName}"", {clsHelper.tableName}.""{saltName}"", {clsHelper.tableName}.""{roleName}"" FROM {clsHelper.tableName}                  WHERE {clsHelper.tableName}.""{userNameCol}"" = {userNameCol};             END;             $$ LANGUAGE plpgsql;";
        }
    }
}