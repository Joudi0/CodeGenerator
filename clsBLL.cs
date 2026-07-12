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

        public static string generateBriefInputToOutputMapping(string sourcePrefix = "dto.", string targetPrefix = "", string endChar = ",")
        {
            string script = "";
            string indent = "                ";
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.getColumnsForCsharp());

            columns.RemoveAll(c => clsAPIs.blackList.Contains(c.name.ToLower()));
            columns.RemoveAll(c =>
                c.name.ToLower().Contains("isactive") || c.name.ToLower() == "active" ||
                c.name.ToLower().Contains("ispremium") || c.name.ToLower() == "premium" ||
                c.name.ToLower().Contains("roleid") || c.name.ToLower().Contains("role")
            );

            foreach (clsHelper.Column col in columns)
            {
                script += $"{indent}{targetPrefix}{col.name} = {sourcePrefix}{col.name}{endChar}\n";
            }

            return endChar == "," ? script.TrimEnd('\n', ',') : script.TrimEnd('\n');
        }

        public static string generateFullInputToOutputMapping(string sourcePrefix = "dto.")
        {
            string script = "";
            string indent = "                ";
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.getColumnsForCsharp());

            columns.RemoveAll(c => clsAPIs.blackList.Contains(c.name.ToLower()));

            foreach (clsHelper.Column col in columns)
            {
                script += $"{indent}{col.name} = {sourcePrefix}{col.name},\n";
            }
            return script.TrimEnd('\n', ',');
        }
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
            string functionName = (columnIndex == 0) ? $"get{clsHelper.objectName}BriefOutputByID" : $"get{clsHelper.objectName}BriefOutputBy{C.name}";
            string dalFunctionName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";

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
                    compositionPopulation.AppendLine($"            // Populate nested object before mapping to brief");
                    compositionPopulation.AppendLine($"            if (fullDto.{col.name} != default)");
                    compositionPopulation.AppendLine($"            {{");
                    compositionPopulation.AppendLine($"                fullDto.{propName} = await {targetBLL}.get{baseEntity}BriefOutputByID(({cleanType})fullDto.{col.name});");
                    compositionPopulation.AppendLine($"            }}");
                }
            }

            return $@"
        public static async Task<{clsHelper.className}BriefOutputDTO> {functionName}({C.type} {C.name})
        {{
            // Fetch the full flat record from DAL
            var fullDto = await {DALName}.{dalFunctionName}({C.name});
            if (fullDto == null) return null;
{compositionPopulation}
            // Map it instantly in memory to BriefOutputDTO
            return new {clsHelper.className}BriefOutputDTO
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
                    compositionPopulation.AppendLine($"            // Directly populate nested object using the specialized Brief method");
                    compositionPopulation.AppendLine($"            if (fullDto.{col.name} != default)");
                    compositionPopulation.AppendLine($"            {{");
                    compositionPopulation.AppendLine($"                fullDto.{propName} = await {targetBLL}.get{baseEntity}BriefOutputByID(({cleanType})fullDto.{col.name});");
                    compositionPopulation.AppendLine($"            }}");
                }
            }

            return $@"
        public static async Task<{clsHelper.className}FullOutputDTO> {functionName}({C.type} {C.name})
        {{
            {clsHelper.className}FullOutputDTO fullDto = await {DALName}.{functionName}({C.name});
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

                if (col.name.ToLower().Contains("roleid"))
                {
                    fieldsMapping.AppendLine($"            {col.name} = (int)Shared.enRoles.User,");
                    continue;
                }

                if (col.name.ToLower().Contains("role")) continue;

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

        var fullDto = new {clsHelper.className}FullOutputDTO
        {{
{fieldsMapping}        }};

        return await {clsHelper.className}DAL.add{clsHelper.objectName}(fullDto);
    }}";
        }

        public static string updateBriefFunc()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string idFieldName = clsHelper.ColumnsForCsharp[0].name;
            string passwordUpdateLogic = "";

            if (isUser)
            {
                clsHelper.Column hashColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("hash"));
                clsHelper.Column saltColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("salt"));

                string hashName = (hashColumn.name != null) ? hashColumn.name : "PasswordHash";
                string saltName = (saltColumn.name != null) ? saltColumn.name : "PasswordSalt";

                passwordUpdateLogic = $@"
            // Business Logic: If a new password is provided by the user, re-hash it.
            if (!string.IsNullOrEmpty(dto.Password))
            {{
                string salt = Shared.clsSecurityHelper.GenerateSalt();
                existingRecord.{saltName} = salt;
                existingRecord.{hashName} = Shared.clsSecurityHelper.ComputeHash(dto.Password, salt);
            }}";
            }

            return $@"
        /// <summary>
        /// Safe update for regular users using BriefInputDTO. Preserves system-controlled fields.
        /// </summary>
        public static async Task<bool> update{clsHelper.objectName}({clsHelper.className}BriefInputDTO dto)
        {{
            // 1. Fetch the existing full record to preserve internal data (Roles, Active status, Balance, etc.)
            var existingRecord = await get{clsHelper.objectName}ByID(dto.{idFieldName});
            if (existingRecord == null) return false;

            // 2. Safely overwrite only the client-editable properties
{generateBriefInputToOutputMapping("dto.", "existingRecord.", ";")}
{passwordUpdateLogic}
            // 3. Forward the fully preserved record to the DAL layer
            return await {DALName}.update{clsHelper.objectName}(existingRecord);
        }}
";
        }

        public static string updateFullFunc()
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
            // Business Logic: Admin-driven password overwrite
            if (!string.IsNullOrEmpty(dto.Password))
            {{
                string salt = Shared.clsSecurityHelper.GenerateSalt();
                fullDto.{saltName} = salt;
                fullDto.{hashName} = Shared.clsSecurityHelper.ComputeHash(dto.Password, salt);
            }}
            else
            {{
                var existingUser = await get{clsHelper.objectName}ByID(dto.{idFieldName});
                if(existingUser != null)
                {{
                    fullDto.{hashName} = existingUser.{hashName};
                    fullDto.{saltName} = existingUser.{saltName};
                }}
            }}";
            }

            return $@"
        /// <summary>
        /// Administrative full update using FullInputDTO. Allows modification of all columns.
        /// </summary>
        public static async Task<bool> update{clsHelper.objectName}({clsHelper.className}FullInputDTO dto)
        {{
            var fullDto = new {clsHelper.className}FullOutputDTO
            {{
{generateFullInputToOutputMapping("dto.")}
            }};
{passwordUpdateLogic}
            return await {DALName}.update{clsHelper.objectName}(fullDto);
        }}
";
        }

        public static string addFunc()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string passwordHashingLogic = "";
            string defaultInjections = "";

            foreach (var col in clsHelper.ColumnsForCsharp)
            {
                if (col.name.ToLower().Contains("isactive") || col.name.ToLower() == "active")
                {
                    defaultInjections += $"            fullDto.{col.name} = true;\n";
                }
                if (isUser && (col.name.ToLower().Contains("roleid")))
                {
                    defaultInjections += $"            fullDto.{col.name} = (int)Shared.enRoles.User;\n";
                }
            }

            if (isUser)
            {
                clsHelper.Column hashColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("hash"));
                clsHelper.Column saltColumn = clsHelper.ColumnsForCsharp.Find(c => c.name.ToLower().Contains("salt"));

                string hashName = (hashColumn.name != null) ? hashColumn.name : "PasswordHash";
                string saltName = (saltColumn.name != null) ? saltColumn.name : "PasswordSalt";

                passwordHashingLogic = $@"
            // Business Logic: Generate cryptography properties securely behind the scenes
            if (!string.IsNullOrEmpty(dto.Password))
            {{
                string salt = Shared.clsSecurityHelper.GenerateSalt();
                fullDto.{saltName} = salt;
                fullDto.{hashName} = Shared.clsSecurityHelper.ComputeHash(dto.Password, salt);
            }}
            else
            {{
                return -1;
            }}";
            }

            return $@"
        /// <summary>
        /// Adds a new record into the system, automatically embedding internal business rules and states.
        /// </summary>
        public static async Task<int> add{clsHelper.objectName}({clsHelper.className}BriefInputDTO dto)
        {{
            var fullDto = new {clsHelper.className}FullOutputDTO
            {{
{generateBriefInputToOutputMapping("dto.")}
            }};
{defaultInjections}{passwordHashingLogic}
            return await {DALName}.add{clsHelper.objectName}(fullDto);
        }}
";
        }

        public static string addAdminFunc()
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
            // Business Logic: Generate cryptography properties for Admin-driven user creation
            if (!string.IsNullOrEmpty(dto.Password))
            {{
                string salt = Shared.clsSecurityHelper.GenerateSalt();
                fullDto.{saltName} = salt;
                fullDto.{hashName} = Shared.clsSecurityHelper.ComputeHash(dto.Password, salt);
            }}";
            }

            return $@"
        /// <summary>
        /// Administrative add: Accepts FullInputDTO to allow setting all fields (Role, Status, etc.).
        /// </summary>
        public static async Task<int> addAdmin{clsHelper.objectName}({clsHelper.className}FullInputDTO dto)
        {{
            var fullDto = new {clsHelper.className}FullOutputDTO
            {{
{generateFullInputToOutputMapping("dto.")}
            }};
{passwordHashingLogic}
            return await {DALName}.add{clsHelper.objectName}(fullDto);
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
                    compositionPopulation.AppendLine($"                // Populate nested object for brief list item");
                    compositionPopulation.AppendLine($"                if (item.{col.name} != default)");
                    compositionPopulation.AppendLine($"                {{");
                    compositionPopulation.AppendLine($"                    item.{propName} = await {targetBLL}.get{baseEntity}BriefOutputByID(({cleanType})item.{col.name});");
                    compositionPopulation.AppendLine($"                }}");
                }
            }

            string compositionLoop = compositionPopulation.Length > 0 ? compositionPopulation.ToString() : "";

            return $@"
        public static async Task<List<{clsHelper.className}BriefOutputDTO>> Paging(int rowsPerPage, int pageNumber, string sortColumn, string direction)
        {{
            List<{clsHelper.className}FullOutputDTO> fullList = await {DALName}.PagingDAL(rowsPerPage, pageNumber, sortColumn, direction);
            List<{clsHelper.className}BriefOutputDTO> briefList = new List<{clsHelper.className}BriefOutputDTO>();
            
            foreach ({clsHelper.className}FullOutputDTO item in fullList)
            {{
{compositionLoop}
                briefList.Add(new {clsHelper.className}BriefOutputDTO
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
            string BLLparam = (columnIndex == 0) ? "" : $"{C.type} {C.name}";
            string DALparam = (columnIndex == 0) ? "" : $"{C.name}";

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
                    compositionPopulation.AppendLine($"                // Populate nested object for brief list item");
                    compositionPopulation.AppendLine($"                if (item.{col.name} != default)");
                    compositionPopulation.AppendLine($"                {{");
                    compositionPopulation.AppendLine($"                    item.{propName} = await {targetBLL}.get{baseEntity}BriefOutputByID(({cleanType})item.{col.name});");
                    compositionPopulation.AppendLine($"                }}");
                }
            }

            string compositionLoop = compositionPopulation.Length > 0 ? compositionPopulation.ToString() : "";

            return $@"
        public static async Task<List<{clsHelper.className}BriefOutputDTO>> {functionName}({BLLparam})
        {{
            List<{clsHelper.className}FullOutputDTO> fullList = await {DALName}.{dalFunctionName}({DALparam});
            List<{clsHelper.className}BriefOutputDTO> briefList = new List<{clsHelper.className}BriefOutputDTO>();
                
            foreach ({clsHelper.className}FullOutputDTO item in fullList)
            {{
{compositionLoop}
                briefList.Add(new {clsHelper.className}BriefOutputDTO
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
            string BLLparam = (columnIndex == 0) ? "" : $"{C.type} {C.name}";
            string DALparam = (columnIndex == 0) ? "" : $"{C.name}";

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
                    compositionPopulation.AppendLine($"                if (item.{col.name} != default)");
                    compositionPopulation.AppendLine($"                {{");
                    compositionPopulation.AppendLine($"                    item.{propName} = await {targetBLL}.get{baseEntity}BriefOutputByID(({cleanType})item.{col.name});");
                    compositionPopulation.AppendLine($"                }}");
                }
            }

            string compositionLoop = compositionPopulation.Length > 0 ? $@"
            foreach (var item in fullList)
            {{
{compositionPopulation}            }}" : "";

            return $@"
        public static async Task<List<{clsHelper.className}FullOutputDTO>> {functionName}({BLLparam})
        {{
            List<{clsHelper.className}FullOutputDTO> fullList = await {DALName}.{dalFunctionName}({DALparam});{compositionLoop}
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
