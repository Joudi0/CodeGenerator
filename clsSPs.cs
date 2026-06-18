using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenarator
{
    internal class clsSPs
    {
        private static string GetSPParameters(bool withFirstPar = false)
        {
            List<clsHelper.Column> cols = new List<clsHelper.Column>(clsHelper.Columns);
            if(!withFirstPar) cols.RemoveAt(0);
            string parameters = "";
            foreach (clsHelper.Column col in cols)
            {
                if(col.isNullable == "y" || col.isNullable == "Yes" || col.isNullable == "Y" || col.isNullable == "yes")
                    parameters += $"\t@{col.name} {FormatSqlType(col.type)} = NULL,\n";
                
                else parameters += $"\t@{col.name} {FormatSqlType(col.type)},\n";
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

        private static string FormatSqlType(string type)
        {
            string t = type.ToLower();
            if (t.Contains("char") || t.Contains("binary"))
                return type + "(MAX)";

            return type;
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
            string SP = $@"
            CREATE PROCEDURE [dbo].[SP_{clsHelper.tableName}_Paging]
                    @RowsPerPage INT, @PageNumber INT
            AS
            BEGIN
                SELECT * FROM {clsHelper.tableName} ORDER BY {clsHelper.Columns[0].name}
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



    }
}
