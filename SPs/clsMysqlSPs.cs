using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CodeGenerator.clsHelper;

namespace CodeGenerator
{
    internal class clsMysqlSPs
    {
        public static string makeCase(clsHelper.Column C)
        {
            return $@"
                    CASE WHEN SortColumn = '{C.name}' AND Direction = 'ASC' THEN {clsHelper.tableName}.`{C.name}` END ASC,
                    CASE WHEN SortColumn = '{C.name}' AND Direction = 'DESC' THEN {clsHelper.tableName}.`{C.name}` END DESC";
        }

        private static string GetSPParameters(bool withFirstPar = false)
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            if (!withFirstPar) cols.RemoveAt(0);
            string parameters = "";
            foreach (clsHelper.Column col in cols)
            {
                parameters += $"\tIN {col.name} {FormatSqlType(col)},\n";
            }
            return parameters.TrimEnd('\n', ',');
        }

        private static string GetColumnNames()
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            cols.RemoveAt(0);
            return string.Join(", ", cols.Select(c => $"`{c.name}`"));
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
            string whereClause = $"WHERE {clsHelper.tableName}.`{cols[0].name}` = {cols[0].name};";
            cols.RemoveAt(0);

            string updateLines = string.Join(",\n\t\t\t", cols.Select(c => $"{clsHelper.tableName}.`{c.name}` = {c.name}"));
            return updateLines + "\n\t\t" + whereClause;
        }

        private static string FormatSqlType(clsHelper.Column C)
        {
            string t = C.type.ToLower();
            if (t.Contains("char") || t.Contains("binary"))
            {
                string len = (C.length == -1 || C.length == null) ? "255" : C.length.ToString();
                return C.type + $"({len})";
            }
            return C.type;
        }

        public static string addSP()
        {
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_Insert(
            {GetSPParameters()}
            )
            BEGIN
                INSERT INTO {clsHelper.tableName} ({GetColumnNames()})
                VALUES ({GetParameterValues()});
                SELECT LAST_INSERT_ID();
            END";
        }

        public static string updateSP()
        {
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_Update(
            {GetSPParameters(true)}
            )
            BEGIN
                UPDATE {clsHelper.tableName} SET {makeUpdateStatement()}
            END";
        }

        public static string deleteSP()
        {
            clsHelper.Column firstColumn = clsHelper.Columns[0];
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_Delete(
                IN {firstColumn.name} {firstColumn.type}
            )
            BEGIN
                DELETE FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.`{firstColumn.name}` = {firstColumn.name};
            END";
        }

        public static string selectAllSP()
        {
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_SelectAll()
            BEGIN
                SELECT * FROM {clsHelper.tableName};
            END";
        }

        public static string selectAllBySP(clsHelper.Column C)
        {
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_SelectAllBy{C.name}(
                IN {C.name} {C.type}
            )
            BEGIN
                SELECT * FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.`{C.name}` = {C.name};
            END";
        }

        public static string selectByColumnSP(clsHelper.Column col)
        {
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_SelectBy{col.name}(
                IN {col.name} {col.type}
            )
            BEGIN
                SELECT * FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.`{col.name}` = {col.name};
            END";
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
            CREATE PROCEDURE SP_{clsHelper.tableName}_Paging(
                IN RowsPerPage INT, 
                IN PageNumber INT, 
                IN SortColumn VARCHAR(128), 
                IN Direction VARCHAR(4)
            )
            BEGIN
                DECLARE v_offset INT;
                SET v_offset = (PageNumber - 1) * RowsPerPage;

                SELECT * FROM {clsHelper.tableName} 
                ORDER BY {allCases}
                LIMIT RowsPerPage OFFSET v_offset;
            END;";
        }

        public static string isExistByColumnSP(clsHelper.Column col)
        {
            return $@"
            CREATE PROCEDURE SP_{clsHelper.tableName}_IsExistBy{col.name}(
                IN {col.name} {col.type}
            )
            BEGIN
                SELECT EXISTS(SELECT 1 FROM {clsHelper.tableName} WHERE {clsHelper.tableName}.`{col.name}` = {col.name}) AS IsFound;
            END";
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
            CREATE PROCEDURE SP_{clsHelper.tableName}_GetSecurityDataByUsername(
                IN {userNameCol} VARCHAR(150)
            )
            BEGIN
                SELECT {userID.name}, {hashName}, {saltName}, {roleName} FROM {clsHelper.tableName} 
                WHERE {clsHelper.tableName}.`{userNameCol}` = {userNameCol};
            END";
        }
    }
}
