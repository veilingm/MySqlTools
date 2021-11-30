using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySqlTools.Models
{
    public class TableInfoModel
    {
        public string TABLE_NAME { get; set; }
        public string TABLE_COMMENT { get; set; }
        public string TABLE_ROWS { get; set; }
    }
}
