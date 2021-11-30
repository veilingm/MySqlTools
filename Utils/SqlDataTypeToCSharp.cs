using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySqlTools.Utils
{
    public class SqlDataTypeToCSharp
    {
        public static string handle(string sqlDataType)
        {
            string CSharpDataType = string.Empty;
            switch (sqlDataType)
            {
                case "bigint":
                    CSharpDataType = "long";
                    break;
                case "int":
                    CSharpDataType = "int";
                    break;
                case "smallint":
                    CSharpDataType = "short";
                    break;
                case "guid":
                    CSharpDataType = "Guid";
                    break;
                case "smalldatetime":
                case "date":
                case "datetime":
                case "timestamp":
                    CSharpDataType = "DateTime";
                    break;
                case "float":
                    CSharpDataType = "float";
                    break;
                case "double":
                    CSharpDataType = "double";
                    break;
                case "numeric":
                case "smallmoney":
                case "decimal":
                case "money":
                    CSharpDataType = "decimal";
                    break;
                case "bit":
                case "bool":
                case "boolean":
                    CSharpDataType = "bool";
                    break;
                case "tinyint":
                    CSharpDataType = "Byte";
                    break;
                case "varchar":
                case "longtext":
                case "char":
                case "mediumtext":
                case "text":
                    CSharpDataType = "string";
                    break;
                default:
                    break;
            }
            return CSharpDataType;
        }
    }
}
