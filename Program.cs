using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.PlatformAbstractions;
using MySqlTools.Models;
using MySqlTools.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MySqlTools
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("启动成功");
            using (IDbConnection connection = BaseRepository.GetMySqlConnection())
            {
                var sql = string.Format(@"
SELECT TABLE_NAME, TABLE_COMMENT, TABLE_ROWS
FROM information_schema.tables
WHERE TABLE_SCHEMA = '{0}' 
ORDER BY TABLE_NAME;", GetSettings("TABLE_SCHEMA"));
                var listTable = connection.Query<TableInfoModel>(sql).ToList();
                foreach (var table in listTable)
                {
                    GeneratedCodeFile(table);
                }
                GeneratedServicesDI(listTable);
                GeneratedDataAccessDI(listTable);
            }
        }

        #region 辅助函数

        private static bool GeneratedCodeFile(TableInfoModel model)
        {
            var resFlag = true;
            using (IDbConnection connection = BaseRepository.GetMySqlConnection())
            {
                var sql = string.Format(@"
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, COLUMN_KEY, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.Columns 
WHERE table_name='{1}' AND table_schema='{0}'", GetSettings("TABLE_SCHEMA"), model.TABLE_NAME);
                var listColumn = connection.Query<ColumnInfoModel>(sql).ToList();
                //GeneratedTableEntity(listColumn, model.TABLE_NAME);
                GeneratedIBaseServices();
                GeneratedIServices(listColumn, model.TABLE_NAME);
                GeneratedServicesImpl(listColumn, model.TABLE_NAME);
                GeneratedIBaseDao();
                GeneratedBaseDaoImpl();
                GeneratedIDao(listColumn, model.TABLE_NAME);
                GeneratedDaoImpl(listColumn, model.TABLE_NAME);
            }

            return resFlag;
        }

        /// <summary>
        /// 生成服务层依赖注入文件
        /// </summary>
        /// <param name="listTable"></param>
        /// <returns></returns>
        private static bool GeneratedServicesDI(List<TableInfoModel> listTable)
        {
            var list = new List<string>();

            list.Add("using Ebos.DataAccess;");
            list.Add("using Ebos.Services.Implement;");
            list.Add("using Ebos.Services.Interface;");
            list.Add("using Microsoft.Extensions.DependencyInjection;");
            list.Add("");
            list.Add("namespace Ebos.Services");
            list.Add("{");
            list.Add("    public class ServiceDIRegister");
            list.Add("    {");
            list.Add("        public void DIRegister_DataAccess(IServiceCollection services)");
            list.Add("        {");
            list.Add("          //配置一个依赖注入映射关系");

            foreach (var table in listTable)
            {
                var tableNameNew = turnHungary2CamelCase.StartChange(table.TABLE_NAME); // 如 TSysUserInfo
                var fileName = tableNameNew.Substring(1, tableNameNew.Length - 1); // 如 SysUserInfo
                list.Add("            services.AddTransient(typeof(I" + fileName + "Service), typeof(" + fileName + "ServiceImpl));");
            }

            list.Add("");
            list.Add("            //注册DAL层的依赖注入");
            list.Add("            DataAccessDIRegister sdr = new DataAccessDIRegister();");
            list.Add("            sdr.DIRegister_DataAccess(services);");
            list.Add("        }");
            list.Add("    }");
            list.Add("}");

            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.Services\\", "ServiceDIRegister.cs", list);
            return true;
        }

        /// <summary>
        /// 生成数据访问层依赖注入文件
        /// </summary>
        /// <param name="listTable"></param>
        /// <returns></returns>
        private static bool GeneratedDataAccessDI(List<TableInfoModel> listTable)
        {
            var list = new List<string>();
            list.Add("using Ebos.DataAccess.Implement;");
            list.Add("using Ebos.DataAccess.Interface;");
            list.Add("using Microsoft.Extensions.DependencyInjection;");
            list.Add("");
            list.Add("namespace Ebos.DataAccess");
            list.Add("{");
            list.Add("    /// <summary>");
            list.Add("    /// 数据访问层 依赖注入注册");
            list.Add("    /// </summary>");
            list.Add("    public class DataAccessDIRegister");
            list.Add("    {");
            list.Add("        public void DIRegister_DataAccess(IServiceCollection services)");
            list.Add("        {");
            foreach (var table in listTable)
            {
                var tableNameNew = turnHungary2CamelCase.StartChange(table.TABLE_NAME); // 如 TSysUserInfo
                var fileName = tableNameNew.Substring(1, tableNameNew.Length - 1); // 如 SysUserInfo
                list.Add("            services.AddTransient(typeof(I" + fileName + "Dao), typeof(" + fileName + "DaoImpl));");
            }
            list.Add("        }");
            list.Add("    }");
            list.Add("}");

            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.DataAccess\\", "DataAccessDIRegister.cs", list);
            return true;
        }

        /// <summary>
        /// 弃用 先自动生成
        /// </summary>
        /// <param name="listColumn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static bool GeneratedTableEntity(List<ColumnInfoModel> listColumn, string tableName)
        {
            var resFlag = true;
            var tableNameNew = turnHungary2CamelCase.StartChange(tableName);

            var list = new List<string>();
            list.Add("using System;");
            list.Add("using System.Collections.Generic;");
            list.Add("");
            list.Add("namespace Ebos.Entity.Generated");
            list.Add("{");
            list.Add("  public partial class " + tableNameNew);
            list.Add("  {");

            foreach (var col in listColumn)
            {
                list.Add("      /// <summary>");
                list.Add("      /// " + col.COLUMN_COMMENT);
                list.Add("      /// <summary>");
                list.Add("      public " + SqlDataTypeToCSharp.handle(col.DATA_TYPE) + " " + turnHungary2CamelCase.StartChange(col.COLUMN_NAME) + " { get; set; }");
                list.Add("      ");
                if (col.COLUMN_KEY == "UNI")
                {
                    list.Add("      public " + tableNameNew + "()");
                    list.Add("      {");
                    using (IDbConnection connection = BaseRepository.GetMySqlConnection())
                    {
                        var sql = string.Format(@"
                   SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE CONSTRAINT_SCHEMA ='{0}' AND
REFERENCED_TABLE_NAME = '{1}';
", GetSettings("TABLE_SCHEMA"), tableName);
                        var listFKTableName = connection.Query<string>(sql).ToList();
                        foreach (var fkTableName in listFKTableName)
                        {
                            list.Add("          " + turnHungary2CamelCase.StartChange(fkTableName) + "s = new HashSet<" + turnHungary2CamelCase.StartChange(fkTableName) + ">();");
                        }
                        list.Add("      }");
                        list.Add("      ");
                        foreach (var fkTableName in listFKTableName)
                        {
                            list.Add("      public virtual ICollection<" + turnHungary2CamelCase.StartChange(fkTableName) + "> " + turnHungary2CamelCase.StartChange(fkTableName) + "s { get; set; }");
                            list.Add("      ");
                        }
                    }
                }
            }

            list.Add("  }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Generated\\", tableNameNew + ".cs", list);

            return resFlag;
        }

        /// <summary>
        /// 生成IBaseServices
        /// </summary>
        /// <returns></returns>
        private static bool GeneratedIBaseServices()
        {
            var list = new List<string>();
            list.Add("using Ebos.Entity;");
            list.Add("using Ebos.Entity.Conditions;");
            list.Add("using System;");
            list.Add("using System.Collections.Generic;");
            list.Add("");
            list.Add("namespace Ebos.Services.Interface");
            list.Add("{");
            list.Add("    public interface IBaseService<T1,T2>");
            list.Add("    {");
            list.Add("        Boolean Exists(T2 Id);");
            list.Add("");
            list.Add("        T1 Get(T2 Id);");
            list.Add("");
            list.Add("        List<T1> FindAll();");
            list.Add("");
            list.Add("        Boolean Add(T1 entity);");
            list.Add("");
            list.Add("        Boolean Update(T1 entity);");
            list.Add("");
            list.Add("        Boolean Save(T1 entity);");
            list.Add("");
            list.Add("        Boolean Delete(T2 Id);");
            list.Add("");
            list.Add("        Pager<T1> GetPager(BaseCondition condition);");
            list.Add("  }");
            list.Add("");
            list.Add("    public interface IBaseService<T1, T2, T3> : IBaseService<T1, T2>");
            list.Add("    {");
            list.Add("        Boolean Exists(T2 Id1, T3 Id2);");
            list.Add("");
            list.Add("        T1 Get(T2 Id1, T3 Id2);");
            list.Add("");
            list.Add("        Boolean Delete(T2 Id1, T3 Id2);");
            list.Add("    }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.Services\\Interface\\", "IBaseService.cs", list);
            return true;
        }

        /// <summary>
        /// 生成服务层接口文件
        /// </summary>
        /// <param name="listColumn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static bool GeneratedIServices(List<ColumnInfoModel> listColumn, string tableName)
        {
            var listPK = new List<string>();
            foreach (var col in listColumn)
            {
                if (col.COLUMN_KEY == "PRI")
                {
                    listPK.Add(SqlDataTypeToCSharp.handle(col.DATA_TYPE));
                }
            }
            var tableNameNew = turnHungary2CamelCase.StartChange(tableName);
            var fileName = tableNameNew.Substring(1, tableNameNew.Length - 1);
            var list = new List<string>();
            list.Add("using Ebos.Entity.Generated;");
            list.Add("");
            list.Add("namespace Ebos.Services.Interface");
            list.Add("{");
            list.Add("    /// <summary>");
            list.Add("    /// 数据库中表[" + tableName + "] 服务层接口文件");
            list.Add("    /// <summary>");
            list.Add("    public interface I" + fileName + "Service : IBaseService<" + tableNameNew + ", " + string.Join(", ", listPK) + ">");
            list.Add("    {");
            list.Add("    }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.Services\\Interface\\", "I" + fileName + "Service.cs", list);
            return true;
        }

        /// <summary>
        /// 生成服务层接口实现文件
        /// </summary>
        /// <param name="listColumn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static bool GeneratedServicesImpl(List<ColumnInfoModel> listColumn, string tableName)
        {
            var listPK = new List<ColumnInfoModel>();
            foreach (var col in listColumn)
            {
                if (col.COLUMN_KEY == "PRI")
                {
                    listPK.Add(col);
                }
            }
            var tableNameNew = turnHungary2CamelCase.StartChange(tableName); // 如 TSysUserInfo
            var fileName = tableNameNew.Substring(1, tableNameNew.Length - 1); // 如 SysUserInfo
            var fileName1 = fileName.Substring(0, 1).ToLower() + fileName.Substring(1, fileName.Length - 1); // 如 sysUserInfo
            var list = new List<string>();
            list.Add("using Ebos.DataAccess.Interface;");
            list.Add("using Ebos.Entity;");
            list.Add("using Ebos.Entity.Conditions;");
            list.Add("using Ebos.Entity.Generated;");
            list.Add("using Ebos.Services.Interface;");
            list.Add("using System.Collections.Generic;");
            list.Add("");
            list.Add("namespace Ebos.Services.Implement");
            list.Add("{");
            list.Add("    /// <summary>");
            list.Add("    /// 数据库中表[" + tableName + "] 服务层接口实现文件");
            list.Add("    /// <summary>");
            list.Add("    public class " + fileName + "ServiceImpl : I" + fileName + "Service");
            list.Add("    {");
            list.Add("        private I" + fileName + "Dao _" + fileName1 + "Dao;");
            list.Add("");
            list.Add("        //构造函数注入");
            list.Add("        public " + fileName + "ServiceImpl(I" + fileName + "Dao " + fileName1 + "Dao)");
            list.Add("        {");
            list.Add("            _" + fileName1 + "Dao = " + fileName1 + "Dao;");
            list.Add("        }");
            list.Add("");
            list.Add("        #region 自动生成 非必要无需更改");
            list.Add("");
            list.Add("        public bool Add(" + tableNameNew + " entity)");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.Add(entity);");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Delete(" + string.Join(", ", listPK.Select(a => SqlDataTypeToCSharp.handle(a.DATA_TYPE) + " " + a.COLUMN_NAME)) + ")");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.Delete(" + string.Join(", ", listPK.Select(a => a.COLUMN_NAME)) + ");");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Exists(" + string.Join(", ", listPK.Select(a => SqlDataTypeToCSharp.handle(a.DATA_TYPE) + " " + a.COLUMN_NAME)) + ")");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.Exists(" + string.Join(", ", listPK.Select(a => a.COLUMN_NAME)) + ");");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public List<" + tableNameNew + "> FindAll()");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.FindAll();");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public " + tableNameNew + " Get(" + string.Join(", ", listPK.Select(a => SqlDataTypeToCSharp.handle(a.DATA_TYPE) + " " + a.COLUMN_NAME)) + ")");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.Get(" + string.Join(", ", listPK.Select(a => a.COLUMN_NAME)) + ");");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public Pager<" + tableNameNew + "> GetPager(BaseCondition condition)");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.GetPager(condition);");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Save(" + tableNameNew + " entity)");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.Save(entity);");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Update(" + tableNameNew + " entity)");
            list.Add("        {");
            list.Add("            using (var ctx = _" + fileName1 + "Dao.Begin())");
            list.Add("            {");
            list.Add("                var result = _" + fileName1 + "Dao.Update(entity);");
            list.Add("                return result;");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        #endregion");
            list.Add("");
            list.Add("        #region 公共方法");
            list.Add("");
            list.Add("        #endregion");
            list.Add("");
            list.Add("        #region 私有方法");
            list.Add("");
            list.Add("        #endregion");
            list.Add("");
            list.Add("    }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.Services\\Implement\\", fileName + "ServiceImpl.cs", list);

            return true;
        }


        /// <summary>
        /// 生成IBaseDao文件
        /// </summary>
        /// <returns></returns>
        private static bool GeneratedIBaseDao()
        {
            var list = new List<string>();
            list.Add("using Ebos.DataAccess.Core;");
            list.Add("using Ebos.DataAccess.Implement;");
            list.Add("using Ebos.Entity;");
            list.Add("using Ebos.Entity.Conditions;");
            list.Add("using System;");
            list.Add("using System.Collections.Generic;");
            list.Add("");
            list.Add("namespace Ebos.DataAccess.Interface");
            list.Add("{");
            list.Add("    public interface IBaseDao<T1, T2> : IDisposable");
            list.Add("    {");
            list.Add("        /// <summary>");
            list.Add("        /// 获取sql上下文");
            list.Add("        /// </summary>");
            list.Add("        /// <returns></returns>");
            list.Add("        /// ");
            list.Add("        MyDbContext GetContext();");
            list.Add("");
            list.Add("        BaseDaoImpl Begin(bool useTran = false);");
            list.Add("");
            list.Add("        #region CRUD");
            list.Add("");
            list.Add("        Boolean Exists(T2 Id);");
            list.Add("");
            list.Add("        T1 Get(T2 Id);");
            list.Add("");
            list.Add("        List<T1> FindAll();");
            list.Add("");
            list.Add("        Boolean Add(T1 entity);");
            list.Add("");
            list.Add("        Boolean Update(T1 entity);");
            list.Add("");
            list.Add("        Boolean Save(T1 entity);");
            list.Add("");
            list.Add("        Boolean Delete(T2 Id);");
            list.Add("");
            list.Add("        public Pager<T1> GetPager(BaseCondition condition);");
            list.Add("");
            list.Add("        #endregion");
            list.Add("    }");
            list.Add("");
            list.Add("    public interface IBaseDao<T1, T2, T3> : IBaseDao<T1, T2>");
            list.Add("    {");
            list.Add("        Boolean Exists(T2 Id1, T3 Id2);");
            list.Add("");
            list.Add("        T1 Get(T2 Id1, T3 Id2);");
            list.Add("");
            list.Add("        Boolean Delete(T2 Id1, T3 Id2);");
            list.Add("    }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.DataAccess\\Interface\\", "IBaseDao.cs", list);
            return true;
        }

        /// <summary>
        /// 生成BaseDaoImpl文件
        /// </summary>
        /// <returns></returns>
        private static bool GeneratedBaseDaoImpl()
        {
            var list = new List<string>();
            list.Add("using Ebos.DataAccess.Core;");
            list.Add("using System;");
            list.Add("");
            list.Add("namespace Ebos.DataAccess.Implement");
            list.Add("{");
            list.Add("    public class BaseDaoImpl : IDisposable");
            list.Add("    {");
            list.Add("        public MyDbContext _context;");
            list.Add("");
            list.Add("        public BaseDaoImpl(MyDbContext context)");
            list.Add("        {");
            list.Add("            _context = context;");
            list.Add("        }");
            list.Add("");
            list.Add("        public MyDbContext GetContext()");
            list.Add("        {");
            list.Add("            return _context;");
            list.Add("        }");
            list.Add("");
            list.Add("        public BaseDaoImpl Begin(bool useTran = false)");
            list.Add("        {");
            list.Add("            if (useTran)");
            list.Add("            {");
            list.Add("                _context.Database.BeginTransaction();");
            list.Add("            }");
            list.Add("");
            list.Add("            return this;");
            list.Add("        }");
            list.Add("");
            list.Add("        public void Commit()");
            list.Add("        {");
            list.Add("            if (_context.Database.AutoTransactionsEnabled)");
            list.Add("            {");
            list.Add("                _context.Database.CommitTransaction();");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public void Rollback()");
            list.Add("        {");
            list.Add("            if (_context.Database.AutoTransactionsEnabled)");
            list.Add("            {");
            list.Add("                _context.Database.RollbackTransaction();");
            list.Add("            }");
            list.Add("        }");
            list.Add("");
            list.Add("        public void Dispose()");
            list.Add("        {");
            list.Add("            //_context?.Dispose();");
            list.Add("        }");
            list.Add("    }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.DataAccess\\Implement\\", "BaseDaoImpl.cs", list);
            return true;
        }

        /// <summary>
        /// 生成数据访问层接口文件
        /// </summary>
        /// <param name="listColumn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static bool GeneratedIDao(List<ColumnInfoModel> listColumn, string tableName)
        {
            var listPK = new List<string>();
            foreach (var col in listColumn)
            {
                if (col.COLUMN_KEY == "PRI")
                {
                    listPK.Add(SqlDataTypeToCSharp.handle(col.DATA_TYPE));
                }
            }
            var tableNameNew = turnHungary2CamelCase.StartChange(tableName);
            var fileName = tableNameNew.Substring(1, tableNameNew.Length - 1);
            var list = new List<string>();
            list.Add("using Ebos.Entity.Generated;");
            list.Add("");
            list.Add("namespace Ebos.DataAccess.Interface");
            list.Add("{");
            list.Add("    /// <summary>");
            list.Add("    /// 数据库中表[" + tableName + "] 数据访问层接口文件");
            list.Add("    /// <summary>");
            list.Add("    public interface I" + fileName + "Dao : IBaseDao<" + tableNameNew + ", " + string.Join(", ", listPK) + ">");
            list.Add("    {");
            list.Add("    }");
            list.Add("}");

            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.DataAccess\\Interface\\", "I" + fileName + "Dao.cs", list);
            return true;
        }

        /// <summary>
        /// 生成数据访问层接口实现文件
        /// </summary>
        /// <param name="listColumn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static bool GeneratedDaoImpl(List<ColumnInfoModel> listColumn, string tableName)
        {
            var listPK = new List<ColumnInfoModel>();
            foreach (var col in listColumn)
            {
                if (col.COLUMN_KEY == "PRI")
                {
                    listPK.Add(col);
                }
            }
            var tableNameNew = turnHungary2CamelCase.StartChange(tableName); // 如 TSysUserInfo
            var fileName = tableNameNew.Substring(1, tableNameNew.Length - 1); // 如 SysUserInfo
            var fileName1 = fileName.Substring(0, 1).ToLower() + fileName.Substring(1, fileName.Length - 1); // 如 sysUserInfo
            var list = new List<string>();
            list.Add("using Ebos.DataAccess.Core;");
            list.Add("using Ebos.DataAccess.Interface;");
            list.Add("using Ebos.Entity;");
            list.Add("using Ebos.Entity.Conditions;");
            list.Add("using Ebos.Entity.Generated;");
            list.Add("using Microsoft.EntityFrameworkCore;");
            list.Add("using System;");
            list.Add("using System.Collections.Generic;");
            list.Add("using System.Linq;");
            list.Add("");
            list.Add("namespace Ebos.DataAccess.Implement");
            list.Add("{");
            list.Add("    /// <summary>");
            list.Add("    /// 数据库中表[" + tableName + "] 服务层接口实现文件");
            list.Add("    /// <summary>");
            list.Add("    public class " + fileName + "DaoImpl : BaseDaoImpl, I" + fileName + "Dao");
            list.Add("    {");
            list.Add("        #region 自动生成 非必要无需更改");
            list.Add("");
            list.Add("        public " + fileName + "DaoImpl(MyDbContext dbContext) : base(dbContext)");
            list.Add("        {");
            list.Add("            _context = dbContext;");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Add(" + tableNameNew + " entity)");
            list.Add("        {");
            list.Add("            _context.Add(entity);");
            list.Add("            return _context.SaveChanges() > 0;");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Delete(" + string.Join(",", listPK.Select(a => SqlDataTypeToCSharp.handle(a.DATA_TYPE) + " " + a.COLUMN_NAME1)) + ")");
            list.Add("        {");
            list.Add("            var ent = _context." + tableNameNew + "s.Single(a => " + string.Join("&& ", listPK.Select(a => "a." + a.COLUMN_NAME1 + ".Equals(" + turnHungary2CamelCase.StartChange(a.COLUMN_NAME) + ")")) + ");");
            list.Add("            _context.Remove(ent);");
            list.Add("            return _context.SaveChanges() > 0;");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Exists(" + string.Join(",", listPK.Select(a => SqlDataTypeToCSharp.handle(a.DATA_TYPE) + " " + a.COLUMN_NAME1)) + ")");
            list.Add("        {");
            list.Add("            return _context." + tableNameNew + "s.Where(a => " + string.Join("&& ", listPK.Select(a => "a." + a.COLUMN_NAME1 + ".Equals(" + a.COLUMN_NAME1 + ")")) + ").Any();");
            list.Add("        }");
            list.Add("");
            list.Add("        public List<" + tableNameNew + "> FindAll()");
            list.Add("        {");
            list.Add("            return _context." + tableNameNew + "s.ToList();");
            list.Add("        }");
            list.Add("");
            list.Add("        public " + tableNameNew + " Get(" + string.Join(",", listPK.Select(a => SqlDataTypeToCSharp.handle(a.DATA_TYPE) + " " + a.COLUMN_NAME1)) + ")");
            list.Add("        {");
            list.Add("            return _context." + tableNameNew + "s.Where(a => " + string.Join("&& ", listPK.Select(a => "a." + a.COLUMN_NAME1 + ".Equals(" + a.COLUMN_NAME1 + ")")) + ").FirstOrDefault();");
            list.Add("        }");
            list.Add("");
            list.Add("        public Pager<" + tableNameNew + "> GetPager(BaseCondition condition)");
            list.Add("        {");
            list.Add("            var total = _context." + tableNameNew + "s.Count();");
            list.Add("            var resList = _context." + tableNameNew + "s");
            list.Add("                        .Skip((condition.PageIndex - 1) * condition.PageSize)");
            list.Add("                        .Take(condition.PageSize).ToList();");
            list.Add("            return new Pager<" + tableNameNew + ">(condition.PageSize, condition.PageSize, total, resList);");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Save(" + tableNameNew + " entity)");
            list.Add("        {");
            list.Add("            if (" + string.Join("&& ", listPK.Select(a => "(entity." + a.COLUMN_NAME1 + " == null || entity." + a.COLUMN_NAME1 + " == default(" + SqlDataTypeToCSharp.handle(a.DATA_TYPE) + "))")) + ")");
            list.Add("            {");
            list.Add("                return Add(entity);");
            list.Add("            }");
            list.Add("");
            list.Add("            _context.Update(entity);");
            list.Add("            return _context.SaveChanges() > 0;");
            list.Add("        }");
            list.Add("");
            list.Add("        public bool Update(" + tableNameNew + " entity)");
            list.Add("        {");
            list.Add("            _context.Update(entity);");
            list.Add("            return _context.SaveChanges() > 0;");
            list.Add("        }");
            list.Add("");
            list.Add("        #endregion");
            list.Add("");
            list.Add("        #region 公共方法");
            list.Add("");
            list.Add("        #endregion");
            list.Add("");
            list.Add("        #region 私有方法");
            list.Add("");
            list.Add("        #endregion");
            list.Add("");
            list.Add("    }");
            list.Add("}");
            SaveToFile(PlatformServices.Default.Application.ApplicationBasePath + "Ebos.DataAccess\\Implement\\", fileName + "DaoImpl.cs", list);
            return true;
        }


        static string GetSettings(string key)
        {
            var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();
            return configuration[key];
        }


        private static void SaveToFile(string path, string fileName, List<string> list)
        {
            //if (!File.Exists(path))
            //{
            //    File.Create(path);
            //}
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            StreamWriter writer = File.CreateText(path + fileName);
            foreach (var item in list)
            {
                writer.WriteLine(item);
            }
            writer.Close();
        }

        #endregion

    }
}
