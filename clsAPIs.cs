using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CodeGenarator
{
    public class clsAPIs
    {
        public static string tabs = "        ";

        public static string writeProperties(bool full = false)
        {
            List<clsHelper.Column> columns = new List<clsHelper.Column>(clsHelper.getColumnsForCsharp());
            string Properties = "";
            if(full)
            {
                foreach (clsHelper.Column c in columns)
                {
                    Properties += $@"{tabs}public {c.type} {c.name} {{ get; set; }}" + "\n";
                }
            }
            else
            {
                columns.RemoveAll(c => blackList.Contains(c.name.ToLower()));
                foreach (clsHelper.Column c in columns)
                {
                    Properties += $@"{tabs}public {c.type} {c.name} {{ get; set; }}" + "\n";
                }
            }
            return Properties;
        }

        public static List<string> blackList = new List<string> { "password",
        "ip", "secret", "salary", "balance", "privatekey"
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
            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}FullDTO
    {{
        {writeProperties(true)}
    }}
}}
";
            return DTO;
        }
    }
}
