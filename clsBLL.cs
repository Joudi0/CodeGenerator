using System;
using System.Collections.Generic;
using System.Text;
using static CodeGenarator.clsHelper;

namespace CodeGenarator
{
    public class clsBLL
    {
        public static string DALName = $@"cls{clsHelper.objectName}DAL";
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
        public static async Task<int?> checkLogin(string Username, string Password)
        {{
            // 1. Retrieve cryptographic security data from the database
            AuthDTO authData = await {clsHelper.className}DAL.getHashAndSalt(Username);

            if (authData == null) return null;

            // 2. Compute the hash using the provided password and the retrieved salt
            string generatedHash = clsSecurityHelper.ComputeHash(Password, authData.PasswordSalt);
            
            // 3. Verify if the computed hash matches the stored password hash
            if (generatedHash == authData.PasswordHash) 
            {{
                return authData.UserID; // Fixed property name from ID to UserID
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
                    string cleanName = col.name.Substring(0, col.name.Length - 2);
                    string targetBLL = "cls" + char.ToUpper(cleanName[0]) + cleanName.Substring(1);
                    string propName = char.ToUpper(cleanName[0]) + cleanName.Substring(1) + "Details";

                    compositionPopulation.AppendLine();
                    compositionPopulation.AppendLine($"            // Directly populate nested object using the single Brief method");
                    compositionPopulation.AppendLine($"            if (fullDto.{col.name} != default)");
                    compositionPopulation.AppendLine($"            {{");
                    compositionPopulation.AppendLine($"                fullDto.{propName} = await {targetBLL}.get{char.ToUpper(cleanName[0])}{cleanName.Substring(1)}BriefByID(fullDto.{col.name});");
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

            foreach (var col in columns)
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

                fieldsMapping.AppendLine($"            {col.name} = registerDto.{col.name},");
            }

            return $@"
    public static async Task<int> RegisterUser(Shared.RegisterRequestDTO registerDto)
    {{
        if (registerDto == null || string.IsNullOrEmpty(registerDto.Password))
            throw new ArgumentException(""Password is required for registration."");

        string salt = clsSecurityHelper.GenerateSalt();
        string hash = clsSecurityHelper.ComputeHash(registerDto.Password, salt);

        var fullDto = new {clsHelper.className}FullDTO
        {{
{fieldsMapping}
        }};

        return await {clsHelper.className}DAL.add{clsHelper.objectName}(fullDto);
    }}";
        }

        public static string addFunc()
        {
            return $@"
        public static Task<int> add{clsHelper.objectName}({clsHelper.className}FullDTO dto)
        {{
            return {DALName}.add{clsHelper.objectName}(dto);
        }}";
        }

        public static string updateFunc()
        {
            return $@"
        public static Task<bool> update{clsHelper.objectName}({clsHelper.className}FullDTO dto)
        {{
            return {DALName}.update{clsHelper.objectName}(dto);
        }}";
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

            string functionName = (columnIndex == 0) ? $"getAllBriefByID" : $"getAllBriefBy{C.name}";
            string dalFunctionName = (columnIndex == 0) ? $"getAllByID" : $"getAllBy{C.name}";
            string existFuncName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";

            return $@"
        public static async Task<List<{clsHelper.className}BriefDTO>> {functionName}({C.type} {C.name})
        {{
            if (await {existFuncName}({C.name}))
            {{
                List<{clsHelper.className}FullDTO> fullList = await {DALName}.{dalFunctionName}({C.name});
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
            return new List<{clsHelper.className}BriefDTO>();
        }}
";
        }

        public static string getAllFullByFunc(clsHelper.Column C)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string dalFunctionName = (columnIndex == 0) ? $"getAllByID" : $"getAllBy{C.name}";
            string functionName = (columnIndex == 0) ? $"getAllFullByID" : $"getAllFullBy{C.name}";
            string existFuncName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            return $@"
        public static async Task<List<{clsHelper.className}FullDTO>> {functionName}({C.type} {C.name})
        {{
            if (await {existFuncName}({C.name}))
            {{
                List<{clsHelper.className}FullDTO> fullList = await {DALName}.{dalFunctionName}({C.name});
                return fullList;
            }}
            return new List<{clsHelper.className}FullDTO>();
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
    public class cls{clsHelper.objectName}
    {{
{injectedString}
    }}
}}
";
        }
    }
}