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

        public static string writeProperties(bool isFull, bool isOutput)
        {
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.mappedColumns);
            string Properties = "";

            // 1. Apply blacklist filter only if it's a Brief DTO (Input or Output)
            if (!isFull)
            {
                columns.RemoveAll((clsHelper.Column c) => blackList.Contains(c.name.ToLower()));

                // Anti-Tampering: For Brief INPUT, automatically strip out system-controlled fields
                if (!isOutput)
                {
                    columns.RemoveAll((clsHelper.Column c) =>
                        c.name.ToLower().Contains("isactive") ||
                        c.name.ToLower() == "active" ||
                        c.name.ToLower().Contains("ispremium") ||
                        c.name.ToLower() == "premium" ||
                        c.name.ToLower().Contains("roleid") ||
                        c.name.ToLower().Contains("role")
                    );
                }
            }

            foreach (clsHelper.Column col in columns)
            {
                // Always render the primitive database column property
                Properties += $"{tabs}public {col.type} {col.name} {{ get; set; }}\n";

                // 2. Render Composition (Nested DTOs) ONLY for Output DTOs to keep Inputs purely Flat
                if (isOutput && col.composition)
                {
                    string baseEntity = clsHelper.GetCleanClassName(col.name);

                    // Nested details dynamically follow the fullness context of the current output DTO
                    string suffix = isFull ? "FullOutputDTO" : "BriefOutputDTO";
                    string dtoType = clsHelper.Prefix + baseEntity + suffix;
                    string propName = char.ToUpper(col.name.Substring(0, col.name.Length - 2)[0]) + col.name.Substring(0, col.name.Length - 2).Substring(1) + "Details";

                    Properties += $"{tabs}public {dtoType} {propName} {{ get; set; }}\n";
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

        public static string BriefInputDTO()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string extraProperties = "";

            if (isUser && !clsHelper.ColumnsForCsharp.Any(c => c.name.ToLower() == "password"))
            {
                extraProperties = $"{tabs}public string Password {{ get; set; }}\n";
            }

            return $@"using System;

namespace Shared
{{
    public class {clsHelper.className}BriefInputDTO
    {{
{writeProperties(isFull: false, isOutput: false)}{extraProperties}    }}
}}";
        }

        public static string FullInputDTO()
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string extraProperties = "";

            if (isUser && !clsHelper.ColumnsForCsharp.Any(c => c.name.ToLower() == "password"))
            {
                extraProperties = $"{tabs}public string Password {{ get; set; }}\n";
            }

            return $@"using System;

namespace Shared
{{
    public class {clsHelper.className}FullInputDTO
    {{
{writeProperties(isFull: true, isOutput: false)}{extraProperties}    }}
}}";
        }

        public static string BriefOutputDTO()
        {
            return $@"using System;

namespace Shared
{{
    public class {clsHelper.className}BriefOutputDTO
    {{
{writeProperties(isFull: false, isOutput: true)}    }}
}}";
        }

        public static string FullOutputDTO()
        {
            return $@"using System;

namespace Shared
{{
    public class {clsHelper.className}FullOutputDTO
    {{
{writeProperties(isFull: true, isOutput: true)}    }}
}}";
        }

        public static string SecurityDTO()
        {
            return $@"using System;

namespace Shared
{{
    public class AuthDTO
    {{
        public int UserID {{ get; set; }}
        public string PasswordHash {{ get; set; }}
        public string PasswordSalt {{ get; set; }}
        public enRoles UserRoleID {{ get; set; }}
    }}
}}";
        }

        public static string TokenResponseDTO()
        {
            return $@"using System;

namespace Shared
{{
    public class TokenResponseDTO
    {{
        public string AccessToken {{ get; set; }}
        public string RefreshToken {{ get; set; }}
    }}
}}";
        }

        public static string RefreshRequestDTO()
        {
            return $@"using System;

namespace Shared
{{
    public class RefreshRequestDTO
    {{
        public int UserID {{ get; set; }}
        public string Username {{ get; set; }}
        public string RefreshToken {{ get; set; }}
    }}
}}";
        }

        public static string LogoutRequestDTO()
        {
            return $@"using System;

namespace Shared
{{
    public class LogoutRequestDTO
    {{
        public int UserID {{ get; set; }}
        public string RefreshToken {{ get; set; }}
    }}
}}";
        }
        public static string RegisterRequestDTO()
        {
            StringBuilder dtoProperties = new StringBuilder();
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.ColumnsForCsharp);

            if (columns.Count > 0) columns.RemoveAt(0); // Remove ID

            foreach (var col in columns)
            {
                if (col.name.ToLower().Contains("hash") || col.name.ToLower().Contains("salt") || col.name.ToLower() == "password")
                    continue;

                if (col.name.ToLower().Contains("roleid") || col.name.ToLower().Contains("role"))
                    continue;

                if (col.name.ToLower().Contains("isactive") || col.name.ToLower() == "active")
                    continue;

                dtoProperties.AppendLine($"{tabs}public {col.type} {col.name} {{ get; set; }}");
            }

            dtoProperties.AppendLine($"{tabs}public string Password {{ get; set; }}");

            return $@"using System;

namespace Shared
{{
    public class RegisterRequestDTO
    {{
{dtoProperties}    }}
}}";
        }

        // ==========================================
        //  Web API Controllers Actions
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
        /// Rotates the refresh token and grants a new access token securely using original signatures.
        /// </summary>
        [HttpPost(""refresh"")]
        [ProducesResponseType(typeof(TokenResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDTO request, [FromServices] clsTokenService tokenService)
        {{
            if (request == null || string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest(""Invalid client request. RefreshToken is required."");

            int isValidToken = await tokenService.ValidateAndRevokeRefreshTokenAsync(request.UserID, request.RefreshToken);
            if (isValidToken == -1)
                return Unauthorized(""Invalid or expired refresh token."");

            string newAccessToken = tokenService.GenerateAccessToken(request.UserID, request.Username, Shared.enRoles.User.ToString());
            string newRefreshToken = await tokenService.GenerateAndSaveRefreshTokenAsync(request.UserID);

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
        /// Revokes the refresh token inside the database securely using original signatures.
        /// </summary>
        [HttpPost(""logout"")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDTO request, [FromServices] clsTokenService tokenService)
        {{
            if (request == null || string.IsNullOrEmpty(request.RefreshToken))
                return Ok(); 

            await tokenService.RevokeTokenByRawAsync(request.UserID, request.RefreshToken);
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
        public async Task<IActionResult> Register([FromBody] Shared.RegisterRequestDTO registerDto)
        {{
            if (registerDto == null || string.IsNullOrEmpty(registerDto.UserName) || string.IsNullOrEmpty(registerDto.Password))
                return BadRequest(""Invalid registration data. UserName and Password are required."");

            try
            {{
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
        /// Retrieves a record by its {C.name} wrapping it in a comprehensive FullOutputDTO containing nested entities.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof({clsHelper.className}FullOutputDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> {actionName}({C.type} {paramName}{serviceInjection})
        {{{ownershipCheck}            {clsHelper.className}FullOutputDTO result = await {clsHelper.className}.{bllMethodName}({paramName});
            if (result == null) return NotFound($""{clsHelper.objectName} with {C.name} {{{paramName}}} not found."");
            return Ok(result);
        }}
";
        }
        public static string updateAction(string roles)
        {
            bool isUser = (clsHelper.tableName.ToLower() == "user" || clsHelper.tableName.ToLower() == "users");
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";
            string serviceInjection = isUser ? ", [FromServices] IAuthorizationService authorizationService" : "";

            return $@"
        /// <summary>
        /// Updates a record using BriefInputDTO (Standard User Access).
        /// </summary>
        {authAttribute}
        [HttpPut]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        public async Task<IActionResult> Update([FromBody] {clsHelper.className}BriefInputDTO dto{serviceInjection})
        {{
            if (dto == null) return BadRequest(""Invalid data."");
            // Add ownership check here if isUser is true
            bool isUpdated = await {clsHelper.className}.update{clsHelper.objectName}(dto);
            if (!isUpdated) return NotFound();
            return Ok(""Updated successfully."");
        }}
";
        }

        public static string updateAdminAction(string roles)
        {
            string authAttribute = $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Updates a record using FullInputDTO (Admin Access - Full Control).
        /// </summary>
        {authAttribute}
        [HttpPut(""admin"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        public async Task<IActionResult> UpdateAdmin([FromBody] {clsHelper.className}FullInputDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data."");
            
            bool isUpdated = await {clsHelper.className}.update{clsHelper.objectName}(dto);
            if (!isUpdated) return NotFound();
            return Ok(""Updated successfully."");
        }}
";
        }

        public static string addAction(string roles)
        {
            string authAttribute = (roles.ToLower() == "anonymous") ? "[AllowAnonymous]" : $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Adds a new record using BriefInputDTO (Standard User Access).
        /// </summary>
        {authAttribute}
        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        [ProducesResponseType(typeof({clsHelper.className}FullOutputDTO), StatusCodes.Status201Created)]
        public async Task<IActionResult> Add([FromBody] {clsHelper.className}BriefInputDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data payload."");
            int insertedID = await {clsHelper.className}.add{clsHelper.objectName}(dto);
            if (insertedID == -1) return StatusCode(500, ""Error adding record."");
            
            var newRecord = await {clsHelper.className}.get{clsHelper.objectName}ByID(insertedID);
            return CreatedAtAction(""GetByID"", new {{ id = insertedID }}, newRecord);
        }}
";
        }

        public static string addAdminAction(string roles)
        {
            // Admin only access
            string authAttribute = $"[Authorize(Roles = \"{roles}\")]";

            return $@"
        /// <summary>
        /// Admin-only: Adds a new record using FullInputDTO to define system-level settings.
        /// </summary>
        {authAttribute}
        [HttpPost(""admin"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.WritePolicy)]
        [ProducesResponseType(typeof({clsHelper.className}FullOutputDTO), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddAdmin([FromBody] {clsHelper.className}FullInputDTO dto)
        {{
            if (dto == null) return BadRequest(""Invalid data."");
            
            int insertedID = await {clsHelper.className}.addAdmin{clsHelper.objectName}(dto);
            if (insertedID == -1) return StatusCode(500, ""Error adding record."");
            
            var newRecord = await {clsHelper.className}.get{clsHelper.objectName}ByID(insertedID);
            return CreatedAtAction(""GetByID"", new {{ id = insertedID }}, newRecord);
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
{ownershipCheck}            bool isDeleted = await {clsHelper.className}.{bllMethodName}({C.name});
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
            bool exists = await {clsHelper.className}.{bllMethodName}({C.name});
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
        /// Retrieves a paginated list of records returning a optimized List of BriefOutputDTOs.
        /// </summary>
        {authAttribute}
        [HttpGet(""page"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(List<{clsHelper.className}BriefOutputDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPage([FromQuery] int rowsPerPage = 10, [FromQuery] int pageNumber = 1, [FromQuery] string sortColumn = ""{clsHelper.Columns[0].name}"", [FromQuery] string direction = ""ASC"")
        {{
            List<{clsHelper.className}BriefOutputDTO> list = await {clsHelper.className}.Paging(rowsPerPage, pageNumber, sortColumn, direction);
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
        /// Retrieves all matching records filtered by a specific column wrapping them in clean BriefOutputDTOs.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(List<{clsHelper.className}BriefOutputDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({BLLparam})
        {{
            List<{clsHelper.className}BriefOutputDTO> list = await {clsHelper.className}.{bllMethodName}({bllCallParam});
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
        /// Retrieves all matching records filtered by a specific column wrapping them in heavy FullOutputDTOs with nested composition.
        /// </summary>
        {authAttribute}
        [HttpGet(""{route}"")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Shared.clsProjectPolicies.ReadPolicy)]
        [ProducesResponseType(typeof(List<{clsHelper.className}FullOutputDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> {actionName}({BLLparam})
        {{
            List<{clsHelper.className}FullOutputDTO> list = await {clsHelper.className}.{bllMethodName}({bllCallParam});
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
    public class {clsHelper.className}Controller : ControllerBase
    {{
{injectedActions}
    }}
}}
";
        }
    }
}
