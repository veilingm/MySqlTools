# MySqlTools
根据MySQL数据自动生成相关文件

1、项目先使用Scaffold-DbContext生成数据库实体类
  Scaffold-DbContext "server=;port=;user=;password=;database=" Pomelo.EntityFrameworkCore.MySql -OutputDir Generated
2、再使用此工具生成相关数据库文件
