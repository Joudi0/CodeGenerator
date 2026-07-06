using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator
{
    public class clsAPIs
    {
        public static string tabs = "        ";

        public static string writeProperties(bool full = false)
        {
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.mappedColumns);
            string Properties = "";

            // Apply blacklist filter only if it's a Brief DTO
            if (!full)
            {
                columns.RemoveAll((clsHelper.Column c) => blackList.Contains(c.name.ToLower()));
            }

            foreach (clsHelper.Column col in columns)
            {
                if (col.composition)
                {
                    // 1. Keep the primitive ID for database and DAL operations
                    Properties += $"{tabs}public {col.type} {col.name} {{ get; set; }}\n";

                    // 2. Dynamically construct and append the clean Nested Brief DTO property
                    string baseEntity = clsHelper.GetCleanClassName(col.name);
                    string dtoType = "cls" + baseEntity + "BriefDTO";
                    string propName = char.ToUpper(col.name.Substring(0, col.name.Length - 2)[0]) + col.name.Substring(0, col.name.Length - 2).Substring(1) + "Details";

                    Properties += $"{tabs}public {dtoType} {propName} {{ get; set; }}\n";
                }
                else
                {
                    // Normal database column mapping
                    Properties += $"{tabs}public {col.type} {col.name} {{ get; set; }}\n";
                }
            }
            return Properties;
        }

        public static List<string> blackList = new List<string>
{ 
    // Authentication & Cryptography
    "password", "pass", "pwd", "passwd",
    "passwordhash", "password_hash", "passwordsalt", "password_salt",
    "secret", "privatekey", "private_key", "publickey", "public_key",
    "key", "iv", "apikey", "api_key",
    "token", "authtoken", "auth_token", "refreshtoken", "refresh_token",
    "sessionid", "session_id", "pin", "pincode",

    // Financial & Sensitive Data
    "salary", "balance", "income", "revenue",
    "creditcard", "credit_card", "cvv", "cvc",
    "cardnumber", "card_number", "bankaccount", "bank_account",

    // Infrastructure & Device Info
    "ip", "ipaddress", "ip_address", "mac", "macaddress", "mac_address",

    // Personal Identifiers
    "ssn", "nationalid", "national_id", "passport", "passportnumber", "passport_number"
};
        public static string BriefDTO()
        {
            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}BriefDTO
    {{
        {writeProperties()}
    }}
}}
";
            return DTO;
        }

        public static string FullDTO()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string extraProperties = "";

            // Password property is only included in the Full DTO for user tables to facilitate registration and updates
            if (isUser)
            {
                // Check if 'Password' already exists in the columns to avoid duplication!
                bool hasPassword = clsHelper.ColumnsForCsharp.Any(c => c.name.ToLower() == "password");

                // Password property is only included in the Full DTO for user tables if it's not already there
                if (isUser && !hasPassword)
                {
                    extraProperties = "        public string Password { get; set; }\n";
                }
            }

            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}FullDTO
    {{
        {writeProperties(true)}{extraProperties}
    }}
}}
";
            return DTO;
        }

        public static string SecurityDTO()
        {
            string DTO = $@"
using System;

namespace Shared
{{
    public class AuthDTO
    {{
        public int UserID {{ get; set; }}
        public string PasswordHash {{ get; set; }}
        public string PasswordSalt {{ get; set; }}
        public enRoles UserRoleID {{ get; set; }}
    }}
}}
";
            return DTO;
        }

        public static string RegisterRequestDTO()
        {
            StringBuilder dtoProperties = new StringBuilder();
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.ColumnsForCsharp);

            // Remove the first column (ID) since it is auto-generated and not provided during registration
            columns.RemoveAt(0);

            foreach (var col in columns)
            {
                // Skip cryptographic fields as they are generated on the server side, not sent by the client
                if (col.name.ToLower().Contains("hash") || col.name.ToLower().Contains("salt") || col.name.ToLower() == "password")
                    continue;

                // Skip the role column to prevent Mass Assignment vulnerability
                if (col.name.ToLower().Contains("roleid") || col.name.ToLower().Contains("role"))
                    continue;

                // NEW: Skip active status column to prevent client tampering
                if (col.name.ToLower().Contains("isactive") || col.name.ToLower() == "active")
                    continue;

                dtoProperties.AppendLine($"        public {col.type} {col.name} {{ get; set; }}");
            }

            // Explicitly add the plain-text password property required to receive user credentials
            dtoProperties.AppendLine("        public string Password { get; set; }");

            return $@"using System;

namespace Shared{{
    public class RegisterRequestDTO
    {{
{dtoProperties}    }}
}}";
        }

        public static string TokenResponseDTO()
        {
            string DTO = $@"using System;

namespace Shared
{{
    public class TokenResponseDTO
    {{
        public string AccessToken {{ get; set; }}
        public string RefreshToken {{ get; set; }}
    }}
}}";
            return DTO;
        }

        public static string RefreshRequestDTO()
        {
            string DTO = $@"using System;

namespace Shared
{{
    public class RefreshRequestDTO
    {{
        public int UserID {{ get; set; }}
        public string Username {{ get; set; }}
        public string RefreshToken {{ get; set; }}
    }}
}}";
            return DTO;
        }

        public static string LogoutRequestDTO()
        {
            string DTO = $@"using System;

namespace Shared
{{
    public class LogoutRequestDTO
    {{
        public int UserID {{ get; set; }}
        public string RefreshToken {{ get; set; }}
    }}
}}";
            return DTO;
        }
        // ==========================================
        //  Web API Controllers
        // ==========================================
        public static string loginAction()
        {
            return $@"
        /// <summary>
        /// Authenticates a user and returns separate short-lived Access Token and secure Refresh Token.
        /// </summary>
        [HttpPost(""login"")]
        [ProducesResponseType(typeof(TokenResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO loginDto, [FromServices] clsTokenService tokenService)
        {{
            if (loginDto == null || string.IsNullOrEmpty(loginDto.Username) || string.IsNullOrEmpty(loginDto.Password))
                return BadRequest(""Username and Password are required."");

            var authData = await {clsHelper.className}.checkLogin(loginDto.Username, loginDto.Password);
            if (authData == null) 
                return Unauthorized(""Invalid username or password."");

            string accessToken = tokenService.GenerateAccessToken(authData.UserID, loginDto.Username, authData.UserRoleID.ToString());
            string refreshToken = await tokenService.GenerateAndSaveRefreshTokenAsync(authData.UserID);

            return Ok(new TokenResponseDTO
            {{
                AccessToken = accessToken,
                RefreshToken = refreshToken
            }});
        }}
";
        }

        public static string refreshAction()
        {
            return $@"
        /// <summary>
        /// Rotates the refresh token and grants a new access token securely.
        /// </summary>
        [HttpPost(""refresh"")]
        [ProducesResponseType(typeof(TokenResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDTO request, [FromServices] clsTokenService tokenService)
        {{
            if (request == null || string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest(""Refresh token is required."");

            // Validate the refresh token and extract user authentication data directly from the server side
            var userAuthData = await tokenService.ValidateAndRevokeRefreshTokenAsync(request.RefreshToken);
            if (userAuthData == null)
                return Unauthorized(""Invalid or expired refresh token."");

            // Generate new pair of tokens based on verified database records only
            string newAccessToken = tokenService.GenerateAccessToken(userAuthData.UserID, userAuthData.Username, userAuthData.UserRoleID.ToString());
            string newRefreshToken = await tokenService.GenerateAndSaveRefreshTokenAsync(userAuthData.UserID);

            return Ok(new TokenResponseDTO
            {{
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            }});
        }}
";
        }

        public static string logoutAction()
        {
            return $@"
        /// <summary>
        /// Revokes the refresh token inside the database securely.
        /// </summary>
        [HttpPost(""logout"")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDTO request, [FromServices] clsTokenService tokenService)
        {{
            if (request == null || string.IsNullOrEmpty(request.RefreshToken))
                return Ok(); // Returns OK to prevent user enumeration attacks

            // Revoke the token using its raw value without relying on unverified client inputs
            await tokenService.RevokeTokenByRawAsync(request.RefreshToken);
            return Ok(""Logged out successfully."");
        }}
";
        }

        public static string registerAction()
        {
            return $@"
        /// <summary>
        /// Registers a new user with automated password hashing and salting.
        /// </summary>
        [HttpPost(""register"")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDTO registerDto)
        {{
            if (registerDto == null || string.IsNullOrEmpty(registerDto.Username) || string.IsNullOrEmpty(registerDto.Password))
                return BadRequest(""Invalid registration data. Username and Password are required."");

            try
            {{
                // The DTO properties are strictly filtered to exclude server-managed fields like IsActive
                int insertedId = await {clsHelper.className}.RegisterUser(registerDto);
                
                if (insertedId == -1)
                    return BadRequest(""Registration failed. Could not create the user."");

                return Ok(new {{ Id = insertedId }});
            }}
            catch (Exception ex)
            {{
                return BadRequest(ex.Message);
            }}
        }}
";
        }

        public static string getByAction(clsHelper.Column C, string roles)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? $"get{clsHelper.objectName}ByID" : $"get{clsHelper.objectName}By{C.name}";
            string actionName = (columnIndex == 0) ? "GetByID" : $"GetBy{C.name}";
            string route = (columnIndex == 0) ? "{id}" : $"{C.name}/{{{C.name}}}";
            string paramName = (columnIndex == 0) ? "id" : C.name;

            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser && columnIndex == 0)
            {
                serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            Microsoft.AspNetCore.Authorization.AuthorizationResult authResult = await authorizationService.AuthorizeAsync(User, {paramName}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
            }

            return $@"
        /// <summary>
        /// Retrieves a record by its {C.name}.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof({clsHelper.className}FullDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> {actionName}({C.type} {paramName}{serviceInjection})
        {{{ownershipCheck}            {clsHelper.className}FullDTO result = await cls{clsHelper.objectName}.{bllMethodName}({paramName});
            if (result == null) return NotFound($""{clsHelper.objectName} with {C.name} {{{paramName}}} not found."");
            return Ok(result);
        }}
";
        }

        public static string updateAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser)
            {
                string idFieldName = clsHelper.ColumnsForCsharp[0].name;
                serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            Microsoft.AspNetCore.Authorization.AuthorizationResult authResult = await authorizationService.AuthorizeAsync(User, dto.{idFieldName}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
            }

            return $@"
        /// <summary>
        /// Updates an existing record in the database.
        /// </summary>
        {authAttribute}
        [HttpPut]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromBody] {clsHelper.className}FullDTO dto{serviceInjection})
        {{
            if (dto == null) return BadRequest(""Invalid data payload."");{ownershipCheck}
            bool isUpdated = await cls{clsHelper.objectName}.update{clsHelper.objectName}(dto);
            if (!isUpdated) return NotFound($""{clsHelper.objectName} update failed or record not found."");
            return Ok(""Updated successfully."");
        }}
";
        }

        public static string addAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Adds a new record to the database.
        /// </summary>
        {authAttribute}
        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        [ProducesResponseType(typeof({clsHelper.className}FullDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Add([FromBody] {clsHelper.className}FullDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data payload."");
            int insertedID = await cls{clsHelper.objectName}.add{clsHelper.objectName}(dto);
            if (insertedID == -1) return StatusCode(500, ""An error occurred while adding the record."");
            return CreatedAtAction(""GetByID"", new {{ id = insertedID }}, dto);
        }}
";
        }

        public static string deleteAction(clsHelper.Column C, string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string bllMethodName = $"delete{clsHelper.objectName}";
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser)
            {
                serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            Microsoft.AspNetCore.Authorization.AuthorizationResult authResult = await authorizationService.AuthorizeAsync(User, {C.name}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
            }

            return $@"
        /// <summary>
        /// Deletes a specific record using its unique identifier.
        /// </summary>
        {authAttribute}
        [HttpDelete(""{C.name}/{{{C.name}}}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete({C.type} {C.name}{serviceInjection})
        {{
{ownershipCheck}            bool isDeleted = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            if (!isDeleted) return NotFound($""{clsHelper.objectName} not found or couldn't be deleted."");
            return Ok(""Deleted successfully."");
        }}
";
        }

        public static string isExistAction(clsHelper.Column C, string roles)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? $"is{clsHelper.objectName}ExistByID" : $"is{clsHelper.objectName}ExistBy{C.name}";
            string actionName = (columnIndex == 0) ? "ExistsByID" : $"ExistsBy{C.name}";
            string route = $"exists/{C.name}/{{{C.name}}}";

            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string ownershipCheck = "";
            string serviceInjection = "";

            if (isUser)
            {
                if (columnIndex == 0)
                {
                    serviceInjection = ", [FromServices] IAuthorizationService authorizationService";
                    ownershipCheck = $@"
            // Centralized Policy-Based Resource Authorization Check
            Microsoft.AspNetCore.Authorization.AuthorizationResult authResult = await authorizationService.AuthorizeAsync(User, {C.name}, ""UserOwnerOrAdmin"");
            if (!authResult.Succeeded) return Forbid();
";
                }
                else if (clsHelper.AvailableRoles.Count > 0)
                {
                    roles = clsHelper.AvailableRoles[0];
                }
            }

            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Checks whether a record exists based on the provided criteria.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({C.type} {C.name}{serviceInjection})
        {{{ownershipCheck}      
            bool exists = await cls{clsHelper.objectName}.{bllMethodName}({C.name});
            return Ok(exists);
        }}
";
        }

        public static string pagingAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Retrieves a paginated list of records based on query filters.
        /// </summary>
        {authAttribute}
        [HttpGet(""page"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(List<{clsHelper.className}BriefDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPage([FromQuery] int rowsPerPage = 10, [FromQuery] int pageNumber = 1, [FromQuery] string sortColumn = ""{clsHelper.Columns[0].name}"", [FromQuery] string direction = ""ASC"")
        {{
            List<{clsHelper.className}BriefDTO> list = await cls{clsHelper.objectName}.Paging(rowsPerPage, pageNumber, sortColumn, direction);
            return Ok(list);
        }}
";
        }

        public static string getAllBriefByAction(clsHelper.Column C, string roles)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? "getAllBrief" : $"getAllBriefBy{C.name}";
            string actionName = (columnIndex == 0) ? "GetAllBrief" : $"GetAllBriefBy{C.name}";
            string route = (columnIndex == 0) ? "all-brief" : $"all-brief/by/{C.name}/{{{C.name}}}";

            string BLLparam = (columnIndex == 0) ? "" : $"{C.type} {C.name}";
            string bllCallParam = (columnIndex == 0) ? "" : C.name;
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }

            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Retrieves all matching records in brief format filtered by a specific column.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(List<{clsHelper.className}BriefDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({BLLparam})
        {{
            List<{clsHelper.className}BriefDTO> list = await cls{clsHelper.objectName}.{bllMethodName}({bllCallParam});
            return Ok(list);
        }}
";
        }

        public static string getAllFullByAction(clsHelper.Column C, string roles)
        {
            int columnIndex = clsHelper.getColumnIndex(C.name);
            string bllMethodName = (columnIndex == 0) ? "getAllFull" : $"getAllFullBy{C.name}";
            string actionName = (columnIndex == 0) ? "GetAllFull" : $"GetAllFullBy{C.name}";
            string route = (columnIndex == 0) ? "all-full" : $"all-full/by/{C.name}/{{{C.name}}}";

            string BLLparam = (columnIndex == 0) ? "" : $"{C.type} {C.name}";
            string bllCallParam = (columnIndex == 0) ? "" : C.name;
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");

            if (isUser && clsHelper.AvailableRoles.Count > 0)
            {
                roles = clsHelper.AvailableRoles[0];
            }

            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Retrieves all matching records in full format filtered by a specific column.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(List<{clsHelper.className}FullDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({BLLparam})
        {{
            List<{clsHelper.className}FullDTO> list = await cls{clsHelper.objectName}.{bllMethodName}({bllCallParam});
            return Ok(list);
        }}
";
        }

        public static string ProjectPolicies()
        {
            return $@"using System;

namespace Shared
{{
    public static class clsProjectPolicies
    {{
        // Authorization Policies
        public const string UserOwnerOrAdmin = ""UserOwnerOrAdmin"";

        // Rate Limiting Policies
        public const string AuthPolicy = ""AuthPolicy"";
        public const string WritePolicy = ""WritePolicy"";
        public const string ReadPolicy = ""ReadPolicy"";
    }}
}}";
        }

        public static string controllerStructure(StringBuilder injectedActions)
        {
            return $@"using BLL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Shared;

namespace WebAPI.Controllers
{{
    [ApiController]
    [Route(""api/[controller]"")]
    public class {clsHelper.objectName}Controller : ControllerBase
    {{
{injectedActions}
    }}
}}
";
        }
    }
}
