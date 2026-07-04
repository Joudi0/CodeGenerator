using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator
{
    internal class clsSPs
    {

        // Helpers

        public static string makeCase(clsHelper.Column C)
        {
            string s = $@"
                    CASE WHEN @SortColumn = '{C.name}' AND @Direction = 'ASC' THEN [{C.name}] END ASC,
                    CASE WHEN @SortColumn = '{C.name}' AND @Direction = 'DESC' THEN [{C.name}] END DESC
";
            return s;
        }

        // Actual Stored Procedures
        private static string GetSPParameters(bool withFirstPar = false)
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            if(!withFirstPar) cols.RemoveAt(0);
            string parameters = "";
            foreach (clsHelper.Column col in cols)
            {
                if(col.isNullable == "y" || col.isNullable == "Yes" || col.isNullable == "Y" || col.isNullable == "yes")
                    parameters += $"\t@{col.name} {FormatSqlType(col)} = NULL,\n";
                
                else parameters += $"\t@{col.name} {FormatSqlType(col)},\n";
            }
            return parameters.TrimEnd('\n', ',');
        }
        private static string GetColumnNames()
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            cols.RemoveAt(0);

            string columns = "";
            foreach (clsHelper.Column col in cols)
            {
                columns += $"{col.name}, ";
            }
            return columns.TrimEnd(' ', ',');
        }
        private static string GetParameterValues()
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            cols.RemoveAt(0);

            string values = "";
            foreach (clsHelper.Column col in cols)
            {
                values += $"@{col.name}, ";
            }
            return values.TrimEnd(' ', ',');
        }
        
        private static string makeUpdateLine(clsHelper.Column C, bool isLast)
        {
            string update = "";

            if (isLast) update += $"{C.name} = @{C.name}";
            else update += $"{C.name} = @{C.name}, ";
            return update;
        }

        private static string makeUpdateStatement()
        {
            string updateStatement = "";
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            string whereClause = $@"WHERE {cols[0].name} = @{cols[0].name};";
            cols.RemoveAt(0);
            foreach (clsHelper.Column col in cols)
            {
                updateStatement += makeUpdateLine(col, col.name == cols.Last().name) + "\n";
            }
            updateStatement += "\n" + whereClause + "\n";
            return updateStatement;
        }

        private static string FormatSqlType(clsHelper.Column C)
        {
            string t = C.type.ToLower();
            if (t.Contains("char") || t.Contains("binary"))
            {
                string len = (C.length == -1) ? "MAX" : C.length.ToString();
                return C.type + $"({len})";
            }
            return C.type;
        }
        // Acutal Functions:

        public static string addSP()
        {
            string SP = $@"
                    CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_Insert]
                    {GetSPParameters()}
                    AS  
                    BEGIN
                        INSERT INTO {clsHelper.tableName} ({GetColumnNames()})
                        VALUES ({GetParameterValues()})
                        SELECT SCOPE_IDENTITY();
                    END
            ";

            return SP;
        }

        public static string updateSP()
        {
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_Update]
                    {GetSPParameters(true)}
            AS
            BEGIN
                UPDATE {clsHelper.tableName} SET {makeUpdateStatement()}
            END
            ";
            return SP;
        }

        public static string deleteSP()
        {
            clsHelper.Column firstColumn = clsHelper.Columns[0];
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_Delete]
                    @{firstColumn.name} {firstColumn.type}
            AS
            BEGIN
                DELETE FROM {clsHelper.tableName} WHERE {firstColumn.name} = @{firstColumn.name};
            END
            ";
            return SP;
        }

        public static string selectAllSP()
        {
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_SelectAll]
            AS
            BEGIN
                SELECT * FROM {clsHelper.tableName};
            END
            ";
            return SP;
        }

        public static string selectAllBySP(clsHelper.Column C)
        {
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_SelectAllBy{C.name}]
                    @{C.name} {C.type}
            AS
            BEGIN
                SELECT * FROM {clsHelper.tableName} WHERE {C.name} = @{C.name};
            END
            ";
            return SP;
        }


        public static string selectByColumnSP(clsHelper.Column col)
        {
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_SelectBy{col.name}]
                    @{col.name} {col.type}
            AS
            BEGIN
                SELECT * FROM {clsHelper.tableName} WHERE {col.name} = @{col.name};
            END
            ";
            return SP;
        }

        public static string PagingSP()
        {
            List<string> casesList = new List<string>();

            foreach (clsHelper.Column C in clsHelper.mappedColumns)
            {
                casesList.Add(makeCase(C));
            }
            string allCases = string.Join(",\n\t\t\t\t", casesList);

            string SP = $@"
    CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_Paging]
            @RowsPerPage INT, 
            @PageNumber INT, 
            @SortColumn NVARCHAR(128) = '{clsHelper.Columns[0].name}', 
            @Direction NVARCHAR(4) = 'ASC'
    AS
    BEGIN
        SELECT * FROM {clsHelper.tableName} 
        ORDER BY 
                {allCases}
        OFFSET (@PageNumber -1) * @RowsPerPage ROWS
        FETCH NEXT @RowsPerPage ROWS ONLY;
    END
    ";
            return SP;
        }



        public static string isExistByColumnSP(clsHelper.Column col)
        {
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_IsExistBy{col.name}]
                    @{col.name} {col.type}
            AS
            BEGIN
                SELECT CASE WHEN EXISTS (SELECT 1 FROM {clsHelper.tableName} WHERE {col.name} = @{col.name}) THEN 1 ELSE 0 END;
            END
            ";
            return SP;
        }


        public static string loginSP()
        {
            // Assuming the table has columns for Username, PasswordHash, and PasswordSalt
            string SP = $@"
    CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_GetSecurityDataByUsername]
            @Username NVARCHAR(150)
    AS
    BEGIN
        SELECT UserID, PasswordHash, PasswordSalt, RoleName FROM {clsHelper.tableName} INNER  JOIN Roles ON {clsHelper.tableName}.RoleID = Roles.RoleID  WHERE Username = @Username;
    END
    ";
            return SP;
        }
    }
}
