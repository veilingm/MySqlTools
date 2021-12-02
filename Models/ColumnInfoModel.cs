using MySqlTools.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySqlTools.Models
{
    public class ColumnInfoModel
    {
        public string TABLE_NAME { get; set; }

        public string COLUMN_NAME { get; set; }

        public string DATA_TYPE { get; set; }

        public string COLUMN_KEY { get; set; }

        public string COLUMN_COMMENT { get; set; }

        public string IS_NULLABLE { get; set; }

        public string COLUMN_NAME1 { get { return turnHungary2CamelCase.StartChange(this.COLUMN_NAME); } }
    }
}
