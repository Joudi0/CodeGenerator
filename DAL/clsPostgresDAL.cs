using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CodeGenerator.clsHelper;

namespace CodeGenerator
{
    public class clsPostgresDAL
    {
        static string tabs = "        ";
        private static string GetNpgsqlDbType(string csharpType)
        {
            switch (csharpType.Replace("?", "").Trim())
            {
                case "string": return "NpgsqlDbType.Varchar";
                case "int": return "NpgsqlDbType.Integer";
                case "long": return "NpgsqlDbType.Bigint";
                case "short": return "NpgsqlDbType.Smallint";
                case "byte": return "NpgsqlDbType.Smallint"; // there is no tinyint in Npgsql, using Smallint instead
                case "decimal": return "NpgsqlDbType.Numeric";
                case "float": return "NpgsqlDbType.Real";
                case "double": return "NpgsqlDbType.Double";
                case "bool": return "NpgsqlDbType.Boolean";
                case "Guid": return "NpgsqlDbType.Uuid";
                case "DateTime": return "NpgsqlDbType.Timestamp";
                case "TimeSpan": return "NpgsqlDbType.Time";
                default: return "NpgsqlDbType.Unknown";
            }
        }

        public static string addParametersAllScript(bool withoutFirst = true, string dtoParamName = "dto")
        {
            System.Collections.Generic.List<Column> newColumns = new System.Collections.Generic.List<Column>(getColumnsForCsharp());
            if (withoutFirst) newColumns.RemoveAt(0);
            string script = "";
            string tabs = "\n\t\t    ";

            foreach (Column col in newColumns)
            {
                script += tabs;
                string propPath = $"{dtoParamName}.{col.name}";
                string dbType = GetNpgsqlDbType(col.type);

                // exact length or Max if -1
                string sizeParam = (col.type.Contains("string") && col.length.HasValue) ? $", {col.length.Value}" : "";

                if (col.isNullable.ToUpper() == "NO")
                {
                    script += $@"command.Parameters.Add(@""{col.name}"", {dbType}{sizeParam}).Value = {propPath};";
                }
                else if (col.isNullable.ToUpper() == "YES")
                {
                    if (col.type == "string")
                    {
                        script += $@"command.Parameters.Add(@""{col.name}"", {dbType}{sizeParam}).Value = string.IsNullOrEmpty({propPath}) ? DBNull.Value : (object){propPath};";
                    }
                    else
                    {
                        script += $@"command.Parameters.Add(@""{col.name}"", {dbType}{sizeParam}).Value = ({propPath} == null) ? DBNull.Value : (object){propPath};";
                    }
                }
            }
            return script;
        }

        public static string generateFullDTOMapping()
        {
            string script = "";

            foreach (clsHelper.Column col in clsHelper.getColumnsForCsharp())
            {
                script += generateProprety(col);
            }
            return script.TrimEnd('\n', ',');
        }

        public static string generateProprety(clsHelper.Column col)
        {
            string script = "";
            if (col.isNullable == "NO")
            {
                script += $"{tabs}{col.name} = ({col.type})reader[\"{col.name}\"],\n";
            }
            else if (col.isNullable == "YES")
            {
                switch (col.type)
                {
                    case "byte": script += $"{tabs}{col.name} = (reader[\"{col.name}\"] == DBNull.Value) ? (byte)0 : ({col.type})reader[\"{col.name}\"],\n"; break;
                    case "decimal":
                    case "int":
                        script += $"{tabs}{col.name} = (reader[\"{col.name}\"] == DBNull.Value) ? -1 : ({col.type})reader[\"{col.name}\"],\n"; break;
                    case "string":
                        script += $"{tabs}{col.name} = (reader[\"{col.name}\"] == DBNull.Value) ? \"\" : ({col.type})reader[\"{col.name}\"],\n"; break;
                    case "DateTime":
                        script += $"{tabs}{col.name} = (reader[\"{col.name}\"] == DBNull.Value) ? DateTime.Now : ({col.type})reader[\"{col.name}\"],\n"; break;
                    case "bool": script += $"{tabs}{col.name} = (reader[\"{col.name}\"] == DBNull.Value) ? false : ({col.type})reader[\"{col.name}\"],\n"; break;
                    default: script += $"{tabs}{col.name} = (reader[\"{col.name}\"] == DBNull.Value) ? null : ({col.type})reader[\"{col.name}\"],\n"; break;
                }
            }
            return script;
        }

        // Actual Functions
        public static string getAuthData()
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

            string Function = $@"
        public static async Task<AuthDTO> getHashAndSalt(string Username)
        {{
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_GetSecurityDataByUsername"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{userNameCol}"", Username);
            try
            {{
                await connection.OpenAsync();
                using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {{
                    return new AuthDTO
                    {{
                        UserID = (reader[""{userID.name}""] == DBNull.Value) ? 0 : (int)reader[""{userID.name}""],
                        PasswordHash = (reader[""{hashName}""] == DBNull.Value) ? """" : (string)reader[""{hashName}""],
                        PasswordSalt = (reader[""{saltName}""] == DBNull.Value) ? """" : (string)reader[""{saltName}""],
                        UserRoleID = (enRoles)((reader[""{roleName}""] == DBNull.Value) ? 0 : (int)reader[""{roleName}""])
                    }};
                }}
            }}
            catch (Exception) {{ throw; }}

            return null;
        }}";
            return Function;
        }

        public static string getRecordByColumnFunc(Column C)
        {
            if (Columns.Count == 0) return "Error in the lists";
            string FunctionName = (getColumnIndex(C.name) == 0) ? $"get{objectName}ByID" : $"get{objectName}By{C.name}";

            string Function = $@"
        public static async Task<{clsHelper.className}FullOutputDTO> {FunctionName}({C.type} {C.name})
        {{
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_SelectBy{C.name}"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});
            try
            {{
                await connection.OpenAsync();
                using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {{
                    return new {clsHelper.className}FullOutputDTO
                    {{
{generateFullDTOMapping()}
                    }};
                }}
            }}
            catch (Exception) {{ throw; }}

            return null;
        }}";

            return Function;
        }

        public static string updateFunc()
        {
            if (Columns.Count == 0) return "Error in the lists, The Column List is Empty";
            string Function = $@"
        public static async Task<bool> update{objectName}({clsHelper.className}FullOutputDTO dto)
        {{
            int rowsAffected = 0;
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_Update"", connection);
            command.CommandType = CommandType.StoredProcedure;
            {addParametersAllScript(false)}
            try
            {{
                await connection.OpenAsync();
                rowsAffected = await command.ExecuteNonQueryAsync();
            }}
            catch (Exception) {{ throw; }}
            return (rowsAffected > 0);
        }}";
            return Function;
        }

        public static string addFunc()
        {
            if (Columns.Count == 0) return "// There is no Columns to work on!";
            string Function = $@"
        public static async Task<int> add{objectName}({clsHelper.className}FullOutputDTO dto)
        {{
            int {objectName}ID = -1;
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_Insert"", connection);
            command.CommandType = CommandType.StoredProcedure;
            {addParametersAllScript(true)}
            try
            {{
                await connection.OpenAsync();
                object result = await command.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int insertedID))
                {{
                    {objectName}ID = insertedID;
                }}
            }}
            catch (Exception) {{ throw; }}

            return {objectName}ID;
        }}";
            return Function;
        }

        public static string deleteFunc(Column C)
        {
            if (Columns.Count == 0) return "Error in the lists";

            string Function = $@"        public static async Task<bool> delete{objectName}({C.type} {C.name})
        {{
            int rowsAffected = 0;
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_Delete"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});
            try
            {{
                await connection.OpenAsync();
                rowsAffected = await command.ExecuteNonQueryAsync();
            }}
            catch (Exception) {{ throw; }}
            return (rowsAffected > 0);
        }}";
            return Function;
        }

        public static string getAllFunc()
        {
            string Function = $@"
        public static async Task<List<{clsHelper.className}FullOutputDTO>> getAll()
        {{
            List<{clsHelper.className}FullOutputDTO> list = new List<{clsHelper.className}FullOutputDTO>();
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_SelectAll"", connection);
            command.CommandType = CommandType.StoredProcedure;

            try
            {{
                await connection.OpenAsync();
                using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {{
                    while (await reader.ReadAsync())
                    {{
                        list.Add(new {clsHelper.className}FullOutputDTO
                        {{
{generateFullDTOMapping()}
                        }});
                    }}
                }}
            }}
            catch (Exception) {{ throw; }}

            return list;
        }}";
            return Function;
        }

        public static string getAllByColumnFunc(Column C)
        {
            string FunctionName = "";
            if (getColumnIndex(C.name) == 0)
            {
                FunctionName = $@"getAllByID";
            }
            else FunctionName = $@"getAllBy{C.name}";
            if (Columns.Count == 0) return "Error in the lists";

            string Function = $@"
        public static async Task<List<{clsHelper.className}FullOutputDTO>> {FunctionName}({C.type} {C.name})
        {{
            List<{clsHelper.className}FullOutputDTO> list = new List<{clsHelper.className}FullOutputDTO>();
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_SelectAllBy{C.name}"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});

            try
            {{
                await connection.OpenAsync();
                using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {{
                    while (await reader.ReadAsync())
                    {{
                        list.Add(new {clsHelper.className}FullOutputDTO
                        {{
{generateFullDTOMapping()}
                        }});
                    }}
                }}
            }}
            catch (Exception) {{ throw; }}

            return list;
        }}";
            return Function;
        }

        public static string isExistsFunc(Column C)
        {
            string FunctionName = "";
            if (getColumnIndex(C.name) == 0)
            {
                FunctionName = $@"is{objectName}ExistByID";
            }
            else FunctionName = $@"is{objectName}ExistBy{C.name}";
            string Function = $@"
        public static async Task<bool> {FunctionName}({C.type} {C.name})
        {{
            bool isFound = false;
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_IsExistBy{C.name}"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@{C.name}"", {C.name});

            try
            {{
                await connection.OpenAsync();
                object result = await command.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int res))
                {{
                    isFound = (res == 1);
                }}
            }}
            catch (Exception) {{ throw; }}

            return isFound;
        }}";
            return Function;
        }

        public static string PagingFunc()
        {
            if (Columns.Count == 0) return "Error in the lists";

            string Function = $@"
        public static async Task<List<{clsHelper.className}FullOutputDTO>> PagingDAL(int RowsPerPage, int PageNumber, string SortColumn, string Direction)
        {{
            List<{clsHelper.className}FullOutputDTO> list = new List<{clsHelper.className}FullOutputDTO>();
            using NpgsqlConnection connection = new NpgsqlConnection(clsDataSettings.connectionString);
            using NpgsqlCommand command = new NpgsqlCommand(""SP_{tableName}_Paging"", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue(""@RowsPerPage"", RowsPerPage);
            command.Parameters.AddWithValue(""@PageNumber"", PageNumber);              
            command.Parameters.AddWithValue(""@SortColumn"", string.IsNullOrEmpty(SortColumn) ? (object)DBNull.Value : SortColumn);
            command.Parameters.AddWithValue(""@Direction"", string.IsNullOrEmpty(Direction) ? (object)DBNull.Value : Direction);    

            try
            {{
                await connection.OpenAsync();
                using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {{
                    while (await reader.ReadAsync())
                    {{
                        list.Add(new {clsHelper.className}FullOutputDTO
                        {{
{generateFullDTOMapping()}
                        }});
                    }}
                }}
            }}
            catch (Exception) {{ throw; }}

            return list;
        }}";
            return Function;
        }

        public static string classStructure(StringBuilder injectedString)
        {
            string structure = $@"using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Shared;
namespace DAL
{{
    public class cls{objectName}DAL
    {{  
{injectedString}  
    }}
}}";
            return structure;
        }
    }
}