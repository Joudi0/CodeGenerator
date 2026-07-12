# Zagros Framework: Joudi's Advanced Clean Architecture Code Generator 🚀

An interactive, enterprise-grade Scaffolding Engine built in C# to automatically generate optimized **Clean Architecture** solutions in **.NET 10**  Moving far beyond simple 3-tier generation, this tool completely automates the creation of database procedures, asynchronous data access layers, complex business logic, split DTO mappings, and secure Web APIs, saving hundreds of hours of manual architectural engineering 

## Features ✨

### 🗄 Multi-Database Provider Engine
* **Universal Architecture**: Generates tailored Stored Procedures and asynchronous ADO.NET execution components for **MSSQL** (`Microsoft.Data.SqlClient`), **MySQL** (`MySql.Data.MySqlClient`), and **PostgreSQL** (`Npgsql`) 
* **Server-Side Optimization**: Offloads execution overhead by handling highly-optimized paging (`OFFSET ROWS`, `LIMIT OFFSET`) directly via database functions 
* **Intelligent Mapping**: Automated data-type translation between SQL variants and C# primitive types with comprehensive `DBNull` and parameter protection to completely block SQL Injection vulnerabilities 

### 🛡 Secure-by-Design Scaffolding
* **Anti-Mass Assignment DTOs**: Automatically splits contracts into strict `Brief` and `Full` context variants  Standard users are bounded to `BriefInputDTO` to safely restrict them from tampering with infrastructure fields like Roles or Account Status 
* **Implicit Data Blacklisting**: Features an embedded structural `blackList` that strips out passwords, hashes, salts, financial records, and personal identifiers from public API visibility 
* **Enterprise Cryptography**: Implements state-of-the-art password hashing using native .NET 10 `Rfc2898DeriveBytes.Pbkdf2` reinforced with secure, cryptographically random salt generation 
* **Token Management Stack**: Full automation for database-backed Refresh Token rotation, SHA256 token hashing, validation, and secure short-lived JWT Access Token issuance 

### 🚦 Traffic Protection & Governance
* **Fixed-Window Rate Limiting**: Bakes advanced IP-throttling directly into the system application pipeline with unified custom JSON error payloads  Features strict `AuthPolicy` (5 req/min), `WritePolicy` (30 req/min), and loose `ReadPolicy` (100 req/min) 
* **Policy-Based Authorization**: Integrates explicit resource-level authorization handlers (`UserOwnerOrAdmin`) to prevent ID Harvesting and horizontal privilege escalation 

### 🤖 Dynamic Automations & DX
* **Automated Composition Populating**: Autodetects database foreign key relationships to dynamically populate nested object details in-memory before mapping 
* **Spectre.Console CLI**: Driven by a modern, fully interactive, color-coded Command Line Interface for choosing tables, scaffolding solutions, or custom filtering variables 
* **Global Dopamine Metrics**: Tracks precisely how many clean lines of code, classes, DTOs, and stored procedures were auto-injected during runtime 

## How to Use It? 🛠️
1. Open the configuration components (`clsHelper.connectionString` / `App.config`) and adjust your database connection and target directory paths 
2. Execute the generator tool.
3. Select your target Database Type (SqlServer, MySql, Postgres) and specify your solution name 
4. Follow the interactive CLI prompts to map custom filtering schemas, actions, or lookup tables 
5. Your clean architecture code structure is safely generated and injected into your private project space !

## Screenshots 📸
![Console Interface](Code-Generator.png)

## Contact & Credits 💬
Developed with ❤️ by **Joudi** as a professional solution to accelerate modern .NET enterprise engineering  For any private inquiries or updates, feel free to reach out :
- **Telegram:** [@Joudi_Adeeb](https://t.me/Joudi_Adeeb) 
- **LinkedIn:** [Joudi Adeeb Mohammad](https://www.linkedin.com/in/joudi-adeeb) 
