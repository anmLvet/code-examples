using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using DEVCOMP.APP.SRV.WebServices.Model;
using System.Data.Entity;
using Oracle.DataAccess.Client;
using System.Data;
using Oracle.DataAccess.Types;
using System.Linq.Expressions;
using System.Data.Objects;
using System.Data.Entity.Infrastructure;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using DEVCOMP.APP.SRV.WebServices.Extensions;

namespace DEVCOMP.APP.SRV.WebServices
{
    public static class DataAccessHelper
    {
        #region Ctors, initialization and credentials
        [ThreadStatic]
        private static Dictionary<Type, DbContext> contexts;

        [ThreadStatic]
        private static string currentUserLogin = null;
        public static string CurrentUserLogin
        {
            get
            {
#if USE_CERTIFICATES
                return OperationContext.Current.ServiceSecurityContext.PrimaryIdentity.Name;
#else
                return currentUserLogin;
#endif
            }
        } 

        [ThreadStatic]
        private static long currentUserId = -1;
        public static long CurrentUserId
        {
            get
            {
#if USE_CERTIFICATES
                return (long)(new RemoteUserProvider()).GetCurrentUserId();
#else
                return currentUserId;
#endif
            }
        }

        
        public static T GetDbContext<T>() where T:DbContext,new()
        {
            lock(contexts)
            {
            if (!contexts.ContainsKey(typeof(T)))
            {
                Database.SetInitializer<T>(null);
                contexts.Add(typeof(T), new T());
            }
            }

            return (T)contexts[typeof(T)];
        }

        public static object obj;

        static DataAccessHelper() 
        {
            obj = new object();
            contexts = new Dictionary<Type, DbContext>();
        }

        /// <summary>
        /// Force creation of new contexts. (not sure if this is necessary but let it be here)
        /// Function only clears list of contexts. New contexts will be created by GetDbContext(T).
        /// 
        /// WebServices work in InstanceContextMode.PerCall mode, with new instance created per each call,
        /// including static data. But to be safe lets clear here, to be guaranteed of clean dbContext each time.
        /// Especially as client caches data on its own.
        /// 
        /// 13.08.2013: Add authorization for "no certificates" mode
        /// </summary> 
        public static void CreateDbContext(string login, string password)
        {
            if (contexts == null)
                contexts = new Dictionary<Type, DbContext>();
            contexts.Clear();      
#if !USE_CERTIFICATES
            try
            {
                StackTrace trace = new StackTrace();
                StackFrame frame = trace.GetFrame(1);

                Trace(frame.GetMethod().Name+" - "+login,1);

              /*  DbTracing.Enable(
        new GenericDbTracingListener()
            .OnFailed(c => Trace(string.Format("-- Command failed - time: {0}{1}{2}", c.Duration, Environment.NewLine, c.Command.ToTraceString()),104))
    );*/
                //.OnFinished(c => Trace(string.Format("-- Command finished - time: {0}{1}{2}", c.Duration, Environment.NewLine, c.Command.ToTraceString()),104))
            

                //DataModelEntities context = DataAccessHelper.GetDbContext<DataModelEntities>();
                //currentUserId = context.ExecuteFunction<long>("CHECK_USER_RIGHTS", login, password);
                //Trace(string.Format("auth {0},{1},{2}", currentUserId, login, password), 2);
                //currentUserLogin = login;
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());// .Message);
            }
#endif
        }

        private static int currentTraceLevel = 200;
        
        internal static void Trace(string traceline, int tracelevel)
        {
            if (tracelevel <= currentTraceLevel)
            {
                lock (obj)
                {
                    try
                    {
                        if (ErrorHandler.loggingEnabled)
                        {
                            string filename = "stacktrace.txt";
                            if (tracelevel > 100) { filename = "stacktrace" + tracelevel.ToString() + ".txt"; }
                            using (StreamWriter writer = new StreamWriter(filename, true))
                            {
                                writer.WriteLine(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + "\t" + currentUserId.ToString() + "," + traceline);
                            }
                        }
                    }
                    catch { ErrorHandler.loggingEnabled = false; }
                }
            }
        }

        public static void EndWork(int sessionID)
        {
            foreach (DbContext context in contexts.Values)
            {
                context.Dispose();
            }
            StackTrace trace = new StackTrace();
            StackFrame frame = trace.GetFrame(1);
            Trace("end: "+frame.GetMethod().Name + " - " + sessionID.ToString(), 100+sessionID);
            
        }

        #endregion



        

        #region Function calls
        

        /// <summary>
        /// Execute oracle function with name 'functionName', parameters 'parameters' and returning type T
        /// For numeric types, too large for decimal, OracleDecimal should be specified
        /// </summary>
        public static T ExecuteFunction<T>(this DbContext context, string functionName, params object[] parameters) where T : new()
        {
            OracleParameter returnParameter = new OracleParameter("function_result", (new T()));
            returnParameter.Direction = ParameterDirection.Output;
            returnParameter.Value = DBNull.Value;

            return (T)context.ExecuteFunction(functionName, returnParameter, parameters).Value;
        }

        /// <summary>
        /// Execute oracle function with name 'functionName', parameters 'parameters' and returning oracle type returnType
        /// </summary>
        public static OracleParameter ExecuteFunction(this DbContext context, string functionName, OracleDbType returnType, params object[] parameters)
        {
            OracleParameter returnParameter = new OracleParameter("function_result", returnType, ParameterDirection.Output);
            returnParameter.Value = DBNull.Value;

            return context.ExecuteFunction(functionName, returnParameter, parameters);
        }

        /// <summary>
        /// Execute oracle function with name 'functionName', parameters 'parameters' and returning oracle type returnType of size 'size'
        /// </summary>
        public static OracleParameter ExecuteFunction(this DbContext context, string functionName, int size, OracleDbType returnType, params object[] parameters)
        {
            OracleParameter returnParameter = new OracleParameter("function_result", returnType, ParameterDirection.Output);
            returnParameter.Size = size;
            returnParameter.Value = DBNull.Value;

            return context.ExecuteFunction(functionName, returnParameter, parameters);
        }

        /// <summary>
        /// Execute oracle function with name 'functionName', parameters 'parameters' and returning oracle parameter returnParameter
        /// Is called from all other ExecuteFunction functions
        /// </summary>
        public static OracleParameter ExecuteFunction(this DbContext context, string functionName, OracleParameter returnParameter, params object[] parameters) 
        {
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            oracleParameters.Add(returnParameter);
            
            int count = 1; String delimeter = "";
            StringBuilder paramList = new StringBuilder();

            parameters.ToList().ForEach(parameter => 
                  { if (parameter is OracleParameter)
                    {
                         OracleParameter oraParam = (OracleParameter)parameter;
                         paramList.Append(delimeter).Append(oraParam.ParameterName).Append(count); 
                         oracleParameters.Add(oraParam);
                    }
                    else
                    {
                         paramList.Append(delimeter).Append(":function_param").Append(count);  
                         oracleParameters.Add(new OracleParameter("function_param"+count.ToString(),parameter));
                         count++;
                    }
                    delimeter = ",";
                 });

            context.ExecuteSqlCommand(
                string.Format("begin :{2} := {0}({1}); end;", functionName, paramList.ToString(),returnParameter.ParameterName)
                , oracleParameters.ToArray());
            
            return returnParameter;

        }
        #endregion

        #region Procedure calls
        private static String getParamString(int count)
        {
            String delimeter = "";
            StringBuilder parameterString = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                parameterString.Append(delimeter + ":param" + i.ToString());
                delimeter = ",";
            }
            return parameterString.ToString();
        }


        /// <summary>
        /// Execute oracle procedure with name 'functionName' and parameters 'parameters'
        /// </summary>
        public static void ExecuteStoredProcedure(this DbContext context, string procedureName, params object[] parameters)
        {
            String parameterString = getParamString(parameters.Length);
            context.ExecuteSqlCommand(string.Format("begin {0}({1}); end;",procedureName,parameterString), parameters);
        }
        #endregion

        #region Primitives: find or delete BsnObject object, load BsnObject dictionary


        #region Look for an object: FindObject
        /// <summary>
        /// Look for an object by id. Returns null if not found
        /// </summary>
        public static T TryFindObject<T>(this DbContext context, long id) where T : class, ILongCodeObject, new()
        {
            try
            {
                //context.SetClientId();
                var query = from ent in context.Set<T>() where ent.ID == id select ent;
                Trace("(" + query.ToString() + ").First()", 2);
                return query.First();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Look for an object by id. Gives exception if not found
        /// </summary>
        public static T FindObject<T>(this DbContext context, long id) where T : class, ILongCodeObject, new()
        {  
            try
            {
                //context.SetClientId();
                var query = from ent in context.Set<T>() where ent.ID == id select ent;
                Trace("("+query.ToString() + ").First()",2);
                return query.First();
            }
            catch
            {
                throw new Exception("Not found object of type " + typeof(T).Name + " with id = " + id.ToString());
            }            
        }

        /// <summary>
        /// Look for an object by id. Gives exception if not found
        /// If id can fit in short type.
        /// </summary>
        public static T FindObject<T>(this DbContext context, short id) where T : class, IShortCodeObject, new()
        {
            try
            {
                //context.SetClientId();
                var query = from ent in context.Set<T>() where ent.ID == id select ent;
                Trace("(" + query.ToString() + ").First()", 2);
                return query.First();
            }
            catch
            {
                throw new Exception("Not found object of type " + typeof(T).Name + " with id = " + id.ToString());
            }
        }

        /// <summary>
        /// Look for an object by id. 
        /// </summary>
        public static T LookForObject<T>(this DbContext context, long id) where T : class, ILongCodeObject, new()
        {
                var query = from ent in context.Set<T>() where ent.ID == id select ent;
                Trace("(" + query.ToString() + ").First()", 2);
                return query.FirstOrDefault();
           
        }
        /// <summary>
        /// Look for an object by condition. Gives exception if not found
        /// </summary>
        public static T FindObject<T>(this DbContext context, Expression<Func<T,bool>> expr, string condition) where T : class, new()
        {
            try
            {
                var query = (from ent in context.Set<T>() select ent).Where(expr);//.First();
                context.SetClientId();
                Trace("(" + query.ToString() + ").First()", 2);
                return query.First();
            }
            catch (Exception ex)
            {
                throw new Exception("Not found object of type " + typeof(T).Name + " with condition " + condition); // expr.Body.ToString() - didn't work
            }
        }
        #endregion


        

        /// <summary>
        /// Return entire table or view, corresponding to entity T.
        /// </summary>
        public static List<T> LoadAll<T>(this DbContext context) where T : class, new()
        {
            var query = from ent in context.Set<T>() select ent;
            return context.LinqList(query);            
        }



        public static bool HasRecords<T>(this DbContext context, Expression<Func<T, bool>> expr, bool throwException) where T : BsnObject, new()
        {
            var query = (from ent in context.Set<T>() select ent).Where(expr);
                //context.SetClientId();
                Trace("(" + query.ToString() + ").Take(1).Count()", 2);
                //string trace = ((ObjectQuery<T>)query).ToTraceString();
                return query.Take(1).Count() > 0; // (from ent in context.Set<T>() select ent).Where(expr).ToList();
            
        }

        public static String GetTableName<T>(this DbContext context) where T : class
        {
            ObjectContext objectContext = ((IObjectContextAdapter)context).ObjectContext;

            string sql = objectContext.CreateObjectSet<T>().ToTraceString();
            Regex regex = new Regex("FROM (?<table>.*) AS");
            Match match = regex.Match(sql);

            string table = match.Groups["table"].Value;
            return table;
        }
        
        #endregion

        #region Basic primitives
        public static void SetClientId(this DbContext context)
        {
            try { context.Database.Connection.Open(); }
            catch { }
            ((OracleConnection)context.Database.Connection).ClientId = CurrentUserId.ToString();
        }

        public static int ExecuteSqlCommand(this DbContext context, string sql, params object[] parameters)
        {
            //context.SetClientId();
            Trace(sql + "," + GetParamValuesString(parameters), 2);
            int result = context.Database.ExecuteSqlCommand(sql, parameters);
            return result;
        }

        public static IEnumerable<T> SqlQuery<T>(this DbContext context, string sql, params object[] parameters)
        {
            //context.SetClientId();
            Trace(sql + "," + GetParamValuesString(parameters), 2);
            var result = context.Database.SqlQuery<T>(sql, parameters);
            return result;
        }

        internal static string GetParamValuesString(params object[] parameters)
        {
            string result = ""; string delim = "\n";
            foreach (object parameter in parameters)
            {
                result += delim;
                if (parameter is OracleParameter)
                {
                    OracleParameter oraPar = parameter as OracleParameter;
                    if (oraPar.Direction == ParameterDirection.Output)
                    {
                        result += oraPar.ToString();
                    }
                    else
                    {
                        result += ((oraPar.Value==null)?"null":oraPar.Value.ToString());
                    }
                }
                else
                {
                    result += ((parameter==null)?"null":parameter.ToString());
                }
                delim = ",\n";
            }
            return result;
        }

        public static List<T> LinqList<T>(this DbContext context, IQueryable<T> query)
        {
           // context.SetClientId();
            Trace("(" + query.ToString() + ").ToList()", 2);
            var result = query.ToList();
            return result;
        }
        #endregion

        public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> source)
        {
            return source ?? Enumerable.Empty<T>();
        }
    }
}
