using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static CodeGenerator.clsHelper;

namespace CodeGenerator
{
    public class clsBLL
    {
        public static string DALName { get; set; }
        public static string tabs = "        ";

        // Helpers Functions:
        public static string generateBriefMapping(string sourcePrefix = "item.")
        {
            string script = "";
            string indent = "                    ";

            List<clsHelper.Column> briefColumns = new List<clsHelper.Column>(clsHelper.getColumnsForCsharp());
            briefColumns.RemoveAll(c => clsAPIs.blackList.Contains(c.name.ToLower()));

            foreach (clsHelper.Column col in briefColumns)
            {
                if (col.composition)
                {
                    script += $"{indent}{col.name} = {sourcePrefix}{col.name},\n";

                    string cleanName = col.name.Substring(0, col.name.Length - 2);
                    string propName = char.ToUpper(cleanName[0]) + cleanName.Substring(1) + "Details";

                    script += $"{indent}{propName} = {sourcePrefix}{propName},\n";
                }
                else
                {
                    script += $"{indent}{col.name} = {sourcePrefix}{col.name},\n";
                }
            }
            return script.TrimEnd('\n', ',');
        }

        // Actual Functions:
        public static string checkLogin()
        {
            string Function = $@"
        public static async Task<AuthDTO> checkLogin(string Username, string Password)
        {{
            // 1. Retrieve cryptographic security data from the database
            AuthDTO authData = await {clsHelper.className}DAL.getHashAndSalt(Username);

            if (authData == null) return null;

            // 2. Compute the hash using the provided password and the retrieved salt
            string generatedHash = clsSecurityHelper.ComputeHash(Password, authData.PasswordSalt);
            
            // 3. Verify if the computed hash matches the stored password hash
            if (generatedHash == authData.PasswordHash) 
            {{
                return authData; // Return the AuthDTO object if login is successful to generate the token
            }}
            else
            {{
                return null; 
            }}
        }}";

            return Function;
        }

        public static string isExistsFunc(clsHelper.Column C)
        {
            string functionName = (clsHelper.getColumnIndex(C.name) == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            return $@"        public static Task<bool> {functionName}({C.type} {C.name})
        {{
            return {DALName}.{functionName}({C.name});
        }}
";
        }

        public static string getBriefFunc(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string functionName = (columnIndex == 0) ? $"get{clsHelper.objectName}BriefByID" : $"get{clsHelper.objectName}BriefBy{C.name}";
            string dalFunctionName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";

            return $@"
        public static async Task<{clsHelper.className}BriefDTO> {functionName}({C.type} {C.name})
        {{
            // Fetch the full flat record from DAL
            var fullDto = await {DALName}.{dalFunctionName}({C.name});
            if (fullDto == null) return null;

            // Map it instantly in memory to BriefDTO
            return new {clsHelper.className}BriefDTO
            {{
{generateBriefMapping("fullDto.")}
            }};
        }}";
        }
        public static string getByFunc(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string functionName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";

            StringBuilder compositionPopulation = new StringBuilder();
            foreach (var col in clsHelper.mappedColumns)
            {
                if (col.composition)
                {
                    string baseEntity = GetCleanClassName(col.name);
                    string targetBLL = clsHelper.Prefix + baseEntity;
                    string cleanProp = col.name.Substring(0, col.name.Length - 2);
                    string propName = char.ToUpper(cleanProp[0]) + cleanProp.Substring(1) + "Details";
                    string cleanType = col.type.Replace("?", "");

                    compositionPopulation.AppendLine();
                    compositionPopulation.AppendLine($"            // Directly populate nested object using the single Brief method");
                    compositionPopulation.AppendLine($"            if (fullDto.{col.name} != default)");
                    compositionPopulation.AppendLine($"            {{");
                    compositionPopulation.AppendLine($"                fullDto.{propName} = await {targetBLL}.get{baseEntity}BriefByID(({cleanType})fullDto.{col.name});");
                    compositionPopulation.AppendLine($"            }}");
                }
            }

            return $@"
        public static async Task<{clsHelper.className}FullDTO> {functionName}({C.type} {C.name})
        {{
            {clsHelper.className}FullDTO fullDto = await {DALName}.{functionName}({C.name});
            if (fullDto == null) return null;
{compositionPopulation}
            return fullDto;
        
        }}
";
        }

        public static string registerUser()
        {
            StringBuilder fieldsMapping = new StringBuilder();
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.ColumnsForCsharp);
            fieldsMapping.AppendLine($"            {columns[0].name} = -1,");
            columns.RemoveAt(0);

            foreach (clsHelper.Column col in columns)
            {
                if (col.name.ToLower().Contains("hash"))
                {
                    fieldsMapping.AppendLine($"            {col.name} = hash,");
                    continue;
                }
                if (col.name.ToLower().Contains("salt"))
                {
                    fieldsMapping.AppendLine($"            {col.name} = salt,");
                    continue;
                }

                if (col.name.ToLower().Contains("roleid")) // Sign up is for users only, so we set the role to User by default
                {
                    fieldsMapping.AppendLine($"            {col.name} = (int)Shared.enRoles.User,");
                    continue;
                }

                if (col.name.ToLower().Contains("role")) continue; // Skip role name if it exists

                // Enforce true for the active status on the server side and skip the regular mapping line
                if (col.name.ToLower().Contains("isactive") || col.name.ToLower() == "active")
                {
                    fieldsMapping.AppendLine($"            {col.name} = true,");
                    continue;
                }

                fieldsMapping.AppendLine($"            {col.name} = registerDto.{col.name},");
            }

            return $@"    public static async Task<int> RegisterUser(Shared.RegisterRequestDTO registerDto)
    {{
        if (registerDto == null || string.IsNullOrEmpty(registerDto.Password))
            throw new ArgumentException(""Password is required for registration."");

        string salt = clsSecurityHelper.GenerateSalt();
        string hash = clsSecurityHelper.ComputeHash(registerDto.Password, salt);

        var fullDto = new {clsHelper.className}FullDTO
        {{
{fieldsMapping}        }};

        return await {clsHelper.className}DAL.add{clsHelper.objectName}(fullDto);
    }}";
        }

        public static string addFunc()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string passwordHashingLogic = "";

            if (isUser)
            {
                clsHelper.Column hashColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("hash"));
                clsHelper.Column saltColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("salt"));

                string hashName = (hashColumn.name != null) ? hashColumn.name : "PasswordHash";
                string saltName = (saltColumn.name != null) ? saltColumn.name : "PasswordSalt";

                passwordHashingLogic = $@"
            // Business Logic: Automatically generate secure hash and salt for the new user record
            if (!string.IsNullOrEmpty(dto.Password))
            {{
                string salt = Shared.clsSecurityHelper.GenerateSalt();
                dto.{saltName} = salt;
                dto.{hashName} = Shared.clsSecurityHelper.ComputeHash(dto.Password, salt);
            }}
            else
            {{
                // In a real scenario, you might want to throw an exception here, 
                // but since the Controller checks it, returning -1 is a safe fallback.
                return -1;
            }}
";
            }

            return $@"
        public static async Task<int> add{clsHelper.objectName}({clsHelper.className}FullDTO dto)
        {{{passwordHashingLogic}
            return await {DALName}.add{clsHelper.objectName}(dto);
        }}
";
        }

        public static string updateFunc()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string passwordUpdateLogic = "";

            if (isUser)
            {
                string idFieldName = clsHelper.ColumnsForCsharp[0].name;
                clsHelper.Column hashColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("hash"));
                clsHelper.Column saltColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("salt"));

                string hashName = (hashColumn.name != null) ? hashColumn.name : "PasswordHash";
                string saltName = (saltColumn.name != null) ? saltColumn.name : "PasswordSalt";

                passwordUpdateLogic = $@"
            // Business Logic: If a new password is provided, re-hash it.
            // Otherwise, fetch the existing hash and salt to prevent data loss.
            if (!string.IsNullOrEmpty(dto.Password))
            {{
                string salt = Shared.clsSecurityHelper.GenerateSalt();
                dto.{saltName} = salt;
                dto.{hashName} = Shared.clsSecurityHelper.ComputeHash(dto.Password, salt);
            }}
            else
            {{
                var existingUser = await get{clsHelper.objectName}ByID(dto.{idFieldName});
                if(existingUser != null)
                {{
                    dto.{hashName} = existingUser.{hashName};
                    dto.{saltName} = existingUser.{saltName};
                }}
            }}
";
            }

            return $@"
        public static async Task<bool> update{clsHelper.objectName}({clsHelper.className}FullDTO dto)
        {{{passwordUpdateLogic}
            return await {DALName}.update{clsHelper.objectName}(dto);
        }}
";
        }

        public static string deleteFunc(clsHelper.Column C)
        {
            string functionName = $"delete{clsHelper.objectName}";
            string existFuncName = (clsHelper.getColumnIndex(C.name) == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";

            return $@"
        public static async Task<bool> {functionName}({C.type} {C.name})
        {{
            if (await {existFuncName}({C.name}))
            {{
                return await {DALName}.delete{clsHelper.objectName}({C.name});
            }}
            return false;
        }}
";
        }

        public static string PagingFunc()
        {
            return $@"
        public static async Task<List<{clsHelper.className}BriefDTO>> Paging(int rowsPerPage, int pageNumber, string sortColumn, string direction)
        {{
            List<{clsHelper.className}FullDTO> fullList = await {DALName}.PagingDAL(rowsPerPage, pageNumber, sortColumn, direction);
            List<{clsHelper.className}BriefDTO> briefList = new List<{clsHelper.className}BriefDTO>();
            
            foreach ({clsHelper.className}FullDTO item in fullList)
            {{
                briefList.Add(new {clsHelper.className}BriefDTO
                {{
{generateBriefMapping()}
                }});
            }}
            
            return briefList;
        }}
";
        }

        public static string getAllBriefByFunc(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);

            string functionName = (columnIndex == 0) ? $"getAllBrief" : $"getAllBriefBy{C.name}";
            string dalFunctionName = (columnIndex == 0) ? $"getAll" : $"getAllBy{C.name}";
            string existFuncName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            string BLLparam = (columnIndex == 0) ? "" : $"{C.type} {C.name}";
            string DALparam = (columnIndex == 0) ? "" : $"{C.name}";

            return $@"
        public static async Task<List<{clsHelper.className}BriefDTO>> {functionName}({BLLparam})
        {{
            List<{clsHelper.className}FullDTO> fullList = await {DALName}.{dalFunctionName}({DALparam});
            List<{clsHelper.className}BriefDTO> briefList = new List<{clsHelper.className}BriefDTO>();
                
            foreach ({clsHelper.className}FullDTO item in fullList)
            {{
                briefList.Add(new {clsHelper.className}BriefDTO
                {{
{generateBriefMapping("item.")}
                }});
            }}
            return briefList;
        }}
";
        }

        public static string getAllFullByFunc(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string dalFunctionName = (columnIndex == 0) ? $"getAll" : $"getAllBy{C.name}";
            string functionName = (columnIndex == 0) ? $"getAllFull" : $"getAllFullBy{C.name}";
            string existFuncName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            string BLLparam = (columnIndex == 0) ? "" : $"{C.type} {C.name}";
            string DALparam = (columnIndex == 0) ? "" : $"{C.name}";

            return $@"
        public static async Task<List<{clsHelper.className}FullDTO>> {functionName}({BLLparam})
        {{
            List<{clsHelper.className}FullDTO> fullList = await {DALName}.{dalFunctionName}({DALparam});
            return fullList;
        }}
";
        }

        public static string classStructure(StringBuilder injectedString)
        {
            return $@"using DAL;
using Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL
{{
    public class {clsHelper.className}
    {{
{injectedString}
    }}
}}
";
        }
    }
}
