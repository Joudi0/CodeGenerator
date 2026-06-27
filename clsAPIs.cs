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

        public static string writeProperties()
        {
            string Properties = "";
            List<clsHelper.Column> l = new List<clsHelper.Column>(clsHelper.mappedColumns);
            l.RemoveAll(c => blackList.Contains(c.name));
            foreach (clsHelper.Column c in l)
            {
                Properties += $@"{tabs}public {c.type} {c.name} {{ get; set; }}" + "\n";
            }
            return Properties;
        }

        public static List<string> blackList = new List<string> { "password",
        "Password", "IP", "secret", "Salary", "salary", "balance", "Balance",
        "privateKey"
        };
        
        public static string DTOs()
        {
            string DTO = $@"
using System;
using System.Data;
using System.Threading.Tasks;
namespace Shared
{{
    public class {clsHelper.className}DTO
    {{
        {writeProperties()}
    }}
}}

";
            return DTO;
        }
    }
}
