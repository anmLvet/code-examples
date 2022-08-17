using DEVCOMP.Common.Data.DataGeneral;
using DEVCOMP.APP.SRV.WebServices.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace DEVCOMP.APP.SRV.WebServices
{
    public class MsgProvider
    {
        #region Reading messages
        private readonly int maxAttempts = 9;
        internal static readonly string APPNAME = "APP";

        private static object msgObject = new object();

        public SRV_MSG GetMessage(out int envStatus, out int requestStatus)
        {
            try
            {
                envStatus = 0;
                requestStatus = 0;
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var msgQuery = from m in context.SRV_MSG where m.ISPROCESSED == 0 && m.DIR == 1 && m.COUNT < maxAttempts && m.APPCODE == APPNAME orderby m.MSGID select m;
                SRV_MSG msg = null;

                lock (msgObject)
                {
                    List<SRV_MSG> tempSt = msgQuery.ToList();
                    DataAccessHelper.Trace("Found " + tempSt.Count + " incoming messages", 2);
                    msg = (tempSt.Count > 0) ? tempSt[0] : null; //msgQuery.FirstOrDefault();
                    if (msg != null)
                    {
                        DataAccessHelper.Trace("Processing " + msg.TYPE + " message", 2);
                        msg.COUNT++;
                        var envQuery = from m in context.SRV_MSG where m.ENVID == msg.ENVID && m.DIR == 1 && m.APPCODE == APPNAME select m.COUNT;
                        envStatus = 0;
                        foreach (int? cnt in envQuery.ToList())
                        { envStatus += cnt.GetValueOrDefault(0); }

                        var requestQuery = from m in context.SRV_REQUEST where m.APPID == msg.APPID  select m;
                        requestStatus = requestQuery.Count();
                        context.SaveChanges();
                    }
                }
                return msg;
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }

        public SRV_MSG GetSendMessage()
        {
            try
            {
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var msgQuery = from m in context.SRV_MSG where m.ISPROCESSED == 0 && m.DIR == 2 && m.APPCODE == APPNAME orderby m.MSGID select m;

                SRV_MSG msg = null;
                lock (msgObject)
                {
                    msg = msgQuery.FirstOrDefault();
                }
                
                return msg;
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }
        #endregion

        #region Updating Msg table
        public void UpdateMessageTable(DataSet ds)
        {
            try
            {
                if (ds.Tables.Count > 0)
                {
                    MsgDS msgDS = new MsgDS();
                    msgDS.Msg.Merge(ds.Tables[0]);

                    foreach (MsgDS.MsgRow row in msgDS.Msg.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted)
                        {
                            DeleteMessage(row.MsgID);
                        }
                        else if (row.RowState == DataRowState.Added || row.RowState == DataRowState.Modified)
                        {
                            UpdateMessage(GetMsgFromRow(row),true);
                        } 
                    }
                    lock (msgObject)
                    {
                       DataAccessHelper.GetDbContext<SRV>().SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }

        private SRV_MSG GetMsgFromRow(MsgDS.MsgRow row)
        {
            SRV_MSG msg = new SRV_MSG();
            msg.MSGID =  row.MsgID;
            msg.SOFTVERSION = row.IsSoftVersionNull()?null:row.SoftVersion;
            msg.DIR = (short)(row.IsDirNull()?0:row.Dir);
            msg.TYPE = row.IsTypeNull()?null:row.Type;
            msg.MSGDATE = row.IsMsgDateNull()?DateTime.MinValue:row.MsgDate;
            msg.XML = row.IsXMLNull()?null:row.XML;
            msg.APPID = row.IsAppIDNull()?null:row.AppID;
            msg.ENVID = row.IsEnvIDNull()?null:row.EnvID;
            msg.INITIALENVID = row.IsInitialEnvIDNull()?null:row.InitialEnvID;
            msg.DOCID = row.IsDocIDNull()?null:row.DocID;
            msg.REFDOCID = row.IsRefDocIDNull()?null:row.RefDocID;
            msg.ISPROCESSED = (short?)(row.IsProcessedNull()?(short?)null:(short)row.Processed);
            msg.COUNT = (short?)(row.IsCountNull()?(short?)0:(short)row.Count);
            msg.RESULT = row.IsResultNull()?null:row.Result;
            msg.DESC = row.IsDescriptionNull()?null:row.Description;
            msg.PROCID = row.IsProcIDNull()?null:row.ProcID;
            return msg;
           
        }
        internal void UpdateMessage(SRV_MSG msg, bool checkNew)
        {
            //try
            //{
            msg.RESULT = msg.RESULT.Length > 100 ? msg.RESULT.Substring(0, 100) : msg.RESULT;
            msg.DESC = msg.DESC.Length > 2000 ? msg.DESC.Substring(0, 2000) : msg.DESC;

            SRV context = DataAccessHelper.GetDbContext<SRV>();
            bool doAdd = true;
            SRV_MSG msgRecord = null;

            if (checkNew && msg.MSGID != null && msg.MSGID != 0)
            {
                var msgQuery = from m in context.SRV_MSG where m.MSGID == msg.MSGID select m;
                msgRecord = msgQuery.FirstOrDefault();
                if (msgRecord != null)
                {
                    // update branch (selected on complex condition with optional database query)
                    doAdd = false;
                    msgRecord.ISPROCESSED = msg.ISPROCESSED;
                    msgRecord.RESULT = msg.RESULT;
                    msgRecord.DESC = msg.DESC;
                    msgRecord.APPCODE = APPNAME;
                }
            }

            if (doAdd)
            {
                // insert branch
                if (msg.ENVID != null && msg.ENVID.Length > 64)
                    msg.ENVID = null;
                if (msg.INITIALENVID != null && msg.INITIALENVID.Length > 64)
                    msg.INITIALENVID = null;
                if (msg.DOCID != null && msg.DOCID.Length > 64)
                    msg.DOCID = null;
                if (msg.REFDOCID != null && msg.REFDOCID.Length > 64)
                    msg.REFDOCID = null;
                if (msg.APPID != null && msg.APPID.Length > 64)
                    msg.APPID = null;
                if (msg.PROCID != null && msg.PROCID.Length > 36)
                    msg.PROCID = null;
                if (msg.SOFTVERSION != null && msg.SOFTVERSION.Length > 30)
                    msg.SOFTVERSION = null;
                msg.APPCODE = APPNAME;
                msg.MSGDATE = DateTime.Now;

                if (msg.APPID == null && msg.INITIALENVID != null)
                {
                    var getApp = from m in context.SRV_MSG where m.ENVID == msg.INITIALENVID select m.APPID;
                    msg.APPID = getApp.FirstOrDefault();
                }

                msg.MSGID = context.SqlQuery<decimal>("select SRV_SQ.nextval from dual").First();
                context.SRV_MSG.Add(msg);

            }

            SaveStatusFromMsg(msgRecord ?? msg, context);

            //context.SaveChanges();
            //}
            //catch (Exception ex)
            //{
            //    throw new FaultException(ex.GetDescription());
            //}
        }

        private void SaveStatusFromMsg(SRV_MSG msg, SRV context)
        {
            // saving to SRV_STATE also
            if (msg.TYPE == "_Msg1" || msg.TYPE == "_Msg3" || msg.TYPE == "_Msg2" || msg.TYPE == "Comp" || msg.TYPE == "Add")
            {
                short msgKind = 0;
                switch (msg.TYPE)
                {
                    case "_Msg1": msgKind = 1; break;
                    case "_Msg2": msgKind = 2; break;
                    case "_Msg3": msgKind = 3; break;
                    case "Comp": msgKind = 4; break;
                    case "Add": msgKind = 5; break;
                    default: msgKind = 0; break;
                }

                if (msgKind > 0)
                {
                    //short msgKind = (short)((msg.TYPE == "_Msg1") ? 1 : ((msg.TYPE == "_Msg2")?2:3));

                    var sRequestQuery = from s in context.SRV_STATEREQUEST
                                             where s.SRV_MSG.DOCID == msg.REFDOCID
                                             select s;
                    SRV_STATEREQUEST request = sRequestQuery.FirstOrDefault();

                    if (request != null)
                    {
                        if (request.SRV_SReplies == null)
                            request.SRV_SReplies = new List<SRV_STATEREPLY>();

                        bool newReply = false;
                        SRV_STATEREPLY reply = request.SRV_SReplies.Where(r => r.SRV_MSGID == msg.MSGID).FirstOrDefault();
                        if (reply == null)
                        {
                            reply = new SRV_STATEREPLY();
                            reply.ID = (long)ServiceProvider.GetNewId();
                            newReply = true;
                        }


                        reply.SRV_MSGID = msg.MSGID;
                        reply.SRV_STATEREQID = request.ID;
                        reply.SRV_MSGKINDID = msgKind;
                        reply.RECEIVEDATE = DateTime.Now;
                        switch (msg.TYPE)
                        {
                            case "Comp": reply.RDESC = "Got reply. Result is: "+msg.DESC; break;
                            case "Add": reply.RDESC = "Got additional info. Result is: "+msg.DESC; break;
                            default: reply.RDESC = msg.DESC; break;
                        }

                        reply.RCODE = msg.RESULT;
                        if (msg.TYPE != "Comp")
                        {
                            XDocument xMsg = XDocument.Parse(msg.XML);
                            XElement xResultSource = xMsg.XPathSelectElement("//*[local-name()='Body']//*[local-name()='Response']//*[local-name()='ResultSource']");

                            if (xResultSource != null)
                                reply.RSOURCE = xResultSource.Value;
                        }
                        if (newReply)
                            request.SRV_SReplies.Add(reply);

                    }
                }
            }
        }

        private void DeleteMessage(decimal msgID)
        {
            //try
            //{
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var msgQuery = from m in context.SRV_MSG where m.MSGID == msgID && m.APPCODE == APPNAME select m;
                SRV_MSG msgRecord = msgQuery.FirstOrDefault();
                if (msgRecord != null)
                {
                    context.SRV_MSG.Remove(msgRecord);
                    //context.SaveChanges();
                }
            //}
            //catch (Exception ex)
            //{
            //   throw new FaultException(ex.GetDescription());
            //}
        }
        #endregion 

        #region UpdateLogActionTable
        private static object logActionObject = new object();

        public void UpdateLogActionTable(DataSet ds)
        {
            try
            {
                if (ds.Tables.Count > 0)
                {
                    DataAccessHelper.Trace("Log action rows: "+ds.Tables[0].Rows.Count.ToString(), 2);
                    LogActionDS actionDS = new LogActionDS();
                    actionDS.LogAction.Merge(ds.Tables[0]);
                    foreach (LogActionDS.LogActionRow row in actionDS.LogAction.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted)
                        {
                            DeleteLogAction(row.MsgID);
                        }
                        else if (row.RowState == DataRowState.Added || row.RowState == DataRowState.Modified)
                        {
                            UpdateLogAction(GetLogActionFromRow(row));
                        }
                    }
                    lock (logActionObject)
                    {
                        DataAccessHelper.GetDbContext<SRV>().SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }

        private SRV_LOGACTION GetLogActionFromRow(LogActionDS.LogActionRow row)
        {
            SRV_LOGACTION logAction = new SRV_LOGACTION();
            logAction.LOGACTIONID = row.LogActionID;
            logAction.ACTTYPE = (short)(row.IsActTypeNull()?0:row.ActType);
            logAction.DEPCODE = row.IsDepCodeNull()?"":row.DepCode;
            logAction.MSGID = row.IsMsgIDNull()?-1:row.MsgID;
            logAction.ACTTEXT = row.IsActTextNull()?"":row.ActText;
            logAction.EXCEPTIONMESSAGE = row.IsExceptionMessageNull()?"":row.ExceptionMessage;
            logAction.ACTTIME = row.IsActTimeNull()?DateTime.MinValue:row.ActTime;
            return logAction;
        }

        private void UpdateLogAction(SRV_LOGACTION act)
        {
            //try
            //{
                
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var actQuery = from m in context.SRV_LOGACTION where m.LOGACTIONID == act.LOGACTIONID select m;
                SRV_LOGACTION actRecord = actQuery.FirstOrDefault();
                if (actRecord != null)
                {
                    actRecord.ACTTYPE = act.ACTTYPE;
                    actRecord.DEPCODE = act.DEPCODE;
                    actRecord.MSGID = act.MSGID;
                    actRecord.ACTTEXT = act.ACTTEXT;
                    actRecord.EXCEPTIONMESSAGE = act.EXCEPTIONMESSAGE;
                    actRecord.ACTTIME = act.ACTTIME;
                    actRecord.APPCODE = APPNAME;
                }
                else
                {
                    act.LOGACTIONID = context.SqlQuery<decimal>("select SRV_SQ.nextval from dual").First();

                    context.SRV_LOGACTION.Add(act);
                   
                } 
                //context.SaveChanges();
            //}
            //catch (Exception ex)
            //{
            //    throw new FaultException(ex.GetDescription());
            //}
        }

        private void DeleteLogAction(decimal logActionID)
        {
            //try
            //{
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var actQuery = from m in context.SRV_LOGACTION where m.LOGACTIONID == logActionID && m.APPCODE == APPNAME select m;
                SRV_LOGACTION actRecord = actQuery.FirstOrDefault();
                if (actRecord != null)
                {
                    context.SRV_LOGACTION.Remove(actRecord);
                    //context.SaveChanges();
                }
            //}
            //catch (Exception ex)
            //{
            //    throw new FaultException(ex.GetDescription());
            //}
        }
        #endregion

        #region Monitoring messages
        public List<SRV_MSG> GetMessageList(DateTime dtFrom, DateTime dtTo)
        {
            try
            {
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var query = from m in context.SRV_MSG where m.MSGDATE >= dtFrom && m.MSGDATE < dtTo orderby m.MSGID descending select m;
                return query.ToList();
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
            
        }


        #endregion
    }
}
