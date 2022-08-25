using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using AppClient.ADM;
using AppClient.Controls;
using AppClient.Properties;
using AppClient.Promoters.DataSets;
using AppClient.Reports.DataSetForReports;
using AppClient.Reports.FormsWithReports;
using AppClient.Reports.StimulReports;
using AppUtilsLibrary;
using AppUtilsLibrary.HQWS;
using AppUtilsLibrary.WSReports_Ref;

namespace AppClient.Promoters.Classes
{
    public class ReturnInvoices
    {
        #region Variables and properties
        // Statuses list for debug
        private static List<string> statusList = new List<string>(new string[] {"1","5","9","21","29","37","2147483657"});
        
        // Seance prices list
        private SortedList<string, QuotaPriceData> quotaPriceData = new SortedList<string, QuotaPriceData>();

        private int? maxOrderBasketTime; // in minutes
        public int MaxOrderBasketTime
        {
            get 
            {
                if (!maxOrderBasketTime.HasValue)
                {
                    try
                    {
                        string error = string.Empty;
                        float? parValue = (float?)AdminManager.getmyAccessControl().fGetSysParam(AccessManager.GetPassInfo(), "MaxOrderBasketTime", out error);
                        if (error != string.Empty || !parValue.HasValue)
                        {
                            maxOrderBasketTime = 45; // defaults to 45 
                        }
                        else
                        {
                            maxOrderBasketTime = (int)Math.Ceiling(parValue.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        SystemLog.WriteException(ex);
                        throw;
                    }
                }
                return maxOrderBasketTime.Value;
            }
        }

        private string exceptionPromoters = null;
        public string ExceptionPromoters
        {
            get
            {
                if (exceptionPromoters == null)
                {
                    string error = string.Empty;
                    try 
                    {
                        exceptionPromoters = AdminManager.getmyAccessControl().sGetSysParam(AccessManager.GetPassInfo(), "ExceptionPromoters", out error);
                        if (error != string.Empty || exceptionPromoters == null)
                            exceptionPromoters = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        SystemLog.WriteException(ex);
                        exceptionPromoters = string.Empty; 
                    }
                    
                }
                return exceptionPromoters;
            }
        }

        #endregion

        // ---------- Short module documentation ---------------------------------------------------------------------------

        // xml-document format
        // 1. GetSeanceData forms this xml-document based on HQ webservice: all data starts with 'part' elements
        // 2. ValidateQuota function writes option data to nodes: validateInfo, priceInfo (?), option - see format description below
        // 3. FillReturnInvoiceData use ValidateQuota function to check specific options, puts return invoice data
        //     to dataset (old version) and separate xml-document. Also, general information about return invoice
        //     and seance from HQ webservice is returned in structure ReturnInvoiceData.
        // 4. HasProblemTickets function returns detailed information about problem tickets as xml-document

        // ---------- Get seance data, return it as xml-document, build dataset, get option list ------------

        #region Get seance data, return it as xml-document, build dataset, get option list 

        public XmlDocument GetSeanceData(string seanceID, ref SortedList<int, string> optionList, ref DataSet resultDataSet, ref XmlNode ndError)
        {
            return GetSeanceData(seanceID, ref optionList, ref resultDataSet, ref ndError, true);
        }

        /// <summary>
        /// Get seance data (seat statuses) or throw exception
        /// </summary>
        public XmlDocument GetSeanceData(string seanceID, ref SortedList<int, string> optionList, ref DataSet resultDataSet, ref XmlNode ndError, bool checkFinalInvoice)
        {
            if (ndError == null)
            {
                XmlDocument docError = XmlTools.CreateXmlDocument("error");
                ndError = docError.DocumentElement;
            }

            #region Get seance data

            #region Clear data
            XmlDocument docSeance = XmlTools.CreateXmlDocument("perfData");
            optionList.Clear();
            
            foreach (DataTable table in resultDataSet.Tables)
            {
                table.Rows.Clear();
            }
            #endregion

            int iSeanceID = -1;
            try
            {
                iSeanceID = int.Parse(seanceID);
            }
            catch { }

            try
            {
                #region Get seance data from HQ webservice, create single xml-document
                string errorText = string.Empty;
                
                List<string> quotaStrings = Quota.lsSeanceQuota(iSeanceID, ref errorText);


                SystemLog.WriteLog(string.Format("Seance: {0}, total parts got: {1}", seanceID, quotaStrings.Count));

                foreach (string sDocPart in quotaStrings)
                {
                    XmlDocument tmpDoc = new XmlDocument();
                    tmpDoc.LoadXml(sDocPart);

                    XmlNode nd = docSeance.DocumentElement.AppendChild(docSeance.CreateElement("part"));
                    nd.InnerXml = tmpDoc.DocumentElement.OuterXml;// sDocPart;

                }
                #endregion

                #region Some debugging information (written only with relevant permissions)
                if (Environment.UserName == "developer1")// || Environment.UserName == "developer2" || Environment.UserName == "developer3")
                {
                    docSeance.Save("Quota" + seanceID.ToString() + ".xml");
                }

                if (errorText != string.Empty && errorText != null)
                {
                    SaveWarningMessage(LogStatus.Error, ndError, string.Format("Got error {1} when getting quota for seance {0}: ", seanceID, errorText));
                }
 
                XmlNodeList freePlaces = docSeance.SelectNodes("//PlatzReport[dwStatus = 1 or dwStatus = 9 or (dwStatus = 29 and dwOptionID = 0 )]");
                SystemLog.WriteLog("Free seats count: " + freePlaces.Count.ToString());

                SortedList<long, SortedList<int, int>> optionStatusList = new SortedList<long, SortedList<int, int>>();
                optionStatusList.Add(29, new SortedList<int, int>());
                optionStatusList.Add(37, new SortedList<int, int>());

                foreach (XmlNode ndNamedPlace in docSeance.SelectNodes("//PlatzReport[dwPKID < 4294967295 and dwOptionID > 0]"))
                {
                    try
                    {
                        int optionID = Convert.ToInt32(XmlTools.SafeGetChildValue(ndNamedPlace, "dwOptionID"));
                        long status = Convert.ToInt64(XmlTools.SafeGetChildValue(ndNamedPlace, "dwStatus"));

                        if (!optionStatusList.ContainsKey(status))
                            optionStatusList.Add(status, new SortedList<int, int>());

                        if (!optionStatusList[status].ContainsKey(optionID))
                            optionStatusList[status].Add(optionID, 1);
                        else
                            optionStatusList[status][optionID]++;
                    }
                    catch { }
                }


                foreach (string status in statusList)
                {
                    SystemLog.WriteLog("Numbered seats with status {0} : {1}", status, docSeance.SelectNodes(string.Format("//PlatzReport[dwStatus = {0} and dwPKID < 4294967295]", status)).Count);
                    if (status == "29" || status == "37")
                    {
                        foreach (int option in optionStatusList[Convert.ToInt64(status)].Keys)
                        {
                            SystemLog.WriteLog("Of these in option {0} - {1}", option, optionStatusList[Convert.ToInt64(status)][option]);
                        }
                    }
                }
                foreach (long status in optionStatusList.Keys)
                {
                    if (status != 29 && status != 37)
                    {
                        foreach (int option in optionStatusList[status].Keys)
                        {
                            SystemLog.WriteLog("Unexpected numbered seats in option {0} with status {1} - {2} seats", option, status, optionStatusList[status][option]);
                        }
                    }
                }
                #endregion
                
                #region Creating option list
                // *** Final return invoice always allowed, even without seats
                optionList.Add(0, "Final return invoice");

                foreach (XmlNode ndOption in docSeance.SelectNodes("//PlatzReport[dwPKID < 4294967295 and dwOptionID > 0]/dwOptionID"))
                {
                    string option = ndOption.InnerText;
                    try
                    {
                        optionList.Add(Convert.ToInt32(option), "Return invoice for option " + option);
                    }
                    catch { }
                }

                foreach (XmlNode ndOption in docSeance.SelectNodes("//PlatzOptBelegung[dwOptionID > 0]/dwOptionID"))
                {
                    string option = ndOption.InnerText;
                    try
                    {
                        optionList.Add(Convert.ToInt32(option), "Return invoice for option " + option);
                    }
                    catch { }
                }

                // Validate quota, but for now only log when return invoice can't be created for an option
                foreach (int optionId in optionList.Keys)
                {
                    SortedList<string, ReturnInvoiceRowInfo> tmpList = new SortedList<string, ReturnInvoiceRowInfo>();
                    try
                    {
                        ValidateQuota(ref docSeance, optionId.ToString(), ref tmpList, iSeanceID);
                    }
                    catch
                    {
                        SaveWarningMessage(LogStatus.Warning, ndError, (optionId == 0 ? "Final return invoice" : "Return invoice for option " + optionId.ToString()) + " cannot be created because of data loading error");
                    }
                }

                if (optionList.Count == 0 && checkFinalInvoice)
                {
                    SaveWarningMessage(LogStatus.Error, ndError, "Return invoices cannot be created, because there are neither free tickets, nor tickets in any of options");
                }
                #endregion

                #region New dataset (old version)
                DataTable dtb = DataTools.SafeGetTable(resultDataSet, "RepTable");
                if (dtb.Columns.Count == 0)
                {
                    dtb.Columns.Add(new DataColumn("sFragment", Type.GetType("System.String")));
                    dtb.Columns.Add(new DataColumn("sRow", Type.GetType("System.String")));
                    dtb.Columns.Add(new DataColumn("SeatList", Type.GetType("System.String")));
                    dtb.Columns.Add(new DataColumn("SeatCount", Type.GetType("System.Int32")));
                    dtb.Columns.Add(new DataColumn("dPrice", Type.GetType("System.Decimal")));
                    dtb.Columns.Add(new DataColumn("dSum", Type.GetType("System.Decimal")));
                }
                #endregion
            }
            catch (Exception ex)
            {
                #region Exception processing

                optionList.Clear();
                docSeance = new XmlDocument();
                foreach (DataTable table in resultDataSet.Tables)
                    table.Rows.Clear();

                #endregion

                ExceptionTools.ProcessException(ex);                
            }
            finally
            {
                WaitForm.HideForm(); 
            }

            return docSeance;
            #endregion
        }

        // skip..

        #endregion

        // ---------- Report data generation ------------------------------------------------------------------------------------

        #region Report data generation 
        
        #region Validating data for specified option. Generates return invoice data (if option is specified)

        // Validated option (only if option is specified) data is written in big xml-document with following format
        // <root>
        // <validInfo result="success|fail"> <!-- Either successful validation for all options, or fatal error for any option -->
        // <priceInfo><price id="priceId" priceAmount="priceAmount" priceName="priceName"/>... </priceInfo> 
        // <!-- List of prices is created in the first pass, after that prices are read only from here -->
        // <option optionId = "optionId" [result="success"]>
        //   <rowInfo><row fragment="fragment" row="row" seats="seats" seatCount="seatCount" price="price" amount="amount"/>... </rowinfo> 
        //   <warning>Warning 1</warning>
        //   <warning>Warning 2</warning>
        //   ...
        //   <error>ERROR!!!</error>
        // </option></root>
        /// <summary>
        /// This function validates ticket quota. If an option is specified, return invoice data is returned.
        /// In validation fails exception is thrown.
        /// </summary>        
        public bool ValidateQuota(ref XmlDocument docQuota, string strOptionNumber, ref SortedList<string, ReturnInvoiceRowInfo> quotaOptionInfo, int seanceId)
        {
            #region Data prepare
            if (strOptionNumber == null)
                strOptionNumber = string.Empty;

            NumberFormatInfo extNumberFormat = new NumberFormatInfo();
            extNumberFormat.NumberDecimalSeparator = ".";
            
            string validatingOptionInfo = "Option " + strOptionNumber + ".";
            #endregion

            quotaOptionInfo.Clear(); // previous return invoice data clear
           
            #region First pass. Check for previous validations
            /*if (XmlTools.SafeGetAttribute(docQuota.SelectSingleNode("//validInfo"), "result").ToLower() == "false")
            {
                return false;
            }*/
 
            XmlNode ndOption = null; // information about option to validate

            // Only check for previous errors, not adding return invoice data to xml yet
            if (strOptionNumber != string.Empty)
            {
                ndOption = GetOptionNode(docQuota, strOptionNumber); // Gets node with validation results or adds new node for these results                
                ExceptionTools.GenerateUserMessageException(ndOption); // Gives exception with validation results if option was already validated
                                                                    // and return invoice can't be generated
                ndOption.RemoveAll(); // If there was no previous errors, clean results, validate again and generate return invoice data if needed
            }
            #endregion

            try
            {
                #region Writing price list to quotePriceData for entire seance!
                // Give error if price is not Decimal, and only log a warning for repeating price

                quotaPriceData.Clear(); // SortedList<string, QuotaPriceData>

                //SortedList<string, XmlNode> quotaPriceNodes = XmlTools.GetSortedList(docQuota.DocumentElement, "//Preise", "dwPreisKlass");

                foreach (XmlNode ndPrice in docQuota.SelectNodes("//Preise"))
                {
                    try
                    {
                        string priceId = XmlTools.SafeGetChildValue(ndPrice, "dwPreisKlass");

                        if (priceId != null && priceId != string.Empty)
                        {
                            if (!quotaPriceData.ContainsKey(priceId))
                            {
                                QuotaPriceData qpd = new QuotaPriceData();
                                qpd.PriceID = priceId;
                                qpd.PriceName = XmlTools.SafeGetChildValue(ndPrice, "szPreisGrp");

                                string strLfdID = XmlTools.SafeGetChildValue(ndPrice, "dwLfd");
                                try
                                {
                                    qpd.PriceLfdID = Convert.ToInt32(strLfdID);
                                }
                                catch (Exception ex)
                                {
                                    SaveWarningMessage(ndOption, LogStatus.Error, validatingOptionInfo + "Incorrect price number in price table: " + ex.Message
                                        , validatingOptionInfo + "Incorrect price number in price table: " + strLfdID + ",price id " + priceId
                                        );
                                }
                                string strPrice = XmlTools.SafeGetChildValue(ndPrice, "dPreisGrp");
                                
                                try
                                {
                                    qpd.Price = Convert.ToDecimal(strPrice, extNumberFormat);
                                    quotaPriceData.Add(priceId, qpd);
                                }
                                catch (Exception ex)
                                {
                                    SaveWarningMessage(ndOption, LogStatus.Error, validatingOptionInfo + "Incorrect format of price value in price table: " + ex.Message
                                        , validatingOptionInfo + "Incorrect format of price value in price table: " + strPrice + ",price id " + priceId
                                        );
                                }
                            }
                            else
                            {
                                SaveWarningMessage(LogStatus.Warning, ndOption, validatingOptionInfo + "Repeating price with id=" + priceId);
                            }
                        }
                    }
                    catch { }
                }
                #endregion
                
                // skip ~100 lines
                
                #region Non-numbered seats

                #region Validating all options

                // Check only: free seats without price, or seats in options without offset or price
                if (strOptionNumber == string.Empty)
                {
                    if (docQuota.SelectSingleNode("//StehPlatzReport[(iFree > iAnzOpt) and (dwPKID < 4294967295) and count(//Preise[dwPreisKlass = ./dwPKID]) = 0]") != null)
                    {
                        SaveWarningMessage(LogStatus.Error, ndOption,"Free non-numbered seats without price");
                    }

                    if (docQuota.SelectSingleNode("//PlatzOptBelegung[iAnzOptFree > 0 and count(//StehPlatzReport[dwOffset = ./dwOffset])=0]") != null)
                    {
                        SaveWarningMessage(LogStatus.Error, ndOption,"Non-numbered seats in an option without offset information");
                    }

                    if (docQuota.SelectSingleNode("//PlatzOptBelegung" +
                        "[iAnzOptFree > 0 and (//StehPlatzReport[dwOffset = ./dwOffset]/dwPKID < 4294967295) and count(" +
                              "//Preise[dwPreisKlass = //StehPlatzReport[dwOffset = ./dwOffset]/dwPKID]" +
                        ")=0]") != null)
                    {
                        SaveWarningMessage(LogStatus.Error, ndOption,"Non-numbered seats in an option without price");
                    }
                }
                #endregion
                #region Free non-numbered seats (final return invoice)
                else if (strOptionNumber == "0")
                {
                    // while HQ webservice still has that bug
                    #region Our information about sold tickets in non-numbered fragments (workaround for HQ webservice bug).

                    DataSet dsOur = new DataSet();
                    DataTable dtOur = null;

                    if (docQuota.SelectSingleNode("//StehPlatzReport[dwPKID<4294967295 and iAnzOpt < iFree]") != null)
                    {
                        dsOur = AdminManager.getmyWSReports().dsPRMPromoterReport5(AccessManager.GetPassInfo(), seanceId);

                        string error = AccessManager.GetErrorMessage(dsOur);
                        if (error == string.Empty && dsOur.Tables.Count > 0)
                            dtOur = dsOur.Tables[0];
                    }
                    #endregion

                    // For final return invoice read table StehPlatzReport and add free seats to quotaOptionInfo
                    // Group by price category to avoid data duplication

                    SortedList<String, NonNumberedPriceData> nonNumberedFreeSeats = new SortedList<string, NonNumberedPriceData>();

                    foreach (XmlNode ndOffset in docQuota.SelectNodes("//StehPlatzReport[dwPKID<4294967295 and iAnzOpt < iFree]"))
                    {
                        string offset = XmlTools.SafeGetChildValue(ndOffset,"dwOffset");
                        string PKID = XmlTools.SafeGetChildValue(ndOffset, "dwPKID");

                        #region Checks
                        if (!quotaPriceData.ContainsKey(PKID))
                        {
                            SaveWarningMessage(LogStatus.Error, ndOption,string.Format("No price for offset {0}",offset));
                            //continue;
                        }

                        // HQ webservice data for non-numbered seats
                        if (!nonNumberedFreeSeats.ContainsKey(PKID))
                        {
                            nonNumberedFreeSeats.Add(PKID, new NonNumberedPriceData());
                            nonNumberedFreeSeats[PKID].PriceID = PKID;
                        }
                        nonNumberedFreeSeats[PKID].HQFreeCount += Convert.ToInt32(XmlTools.SafeGetChildValue(ndOffset, "iFree")) - Convert.ToInt32(XmlTools.SafeGetChildValue(ndOffset, "iAnzOpt"));

                        // Our data for non-numbered seats
                        int freeCount = -1;
                        try
                        {
                            if (dtOur != null)
                            {
                                DataRow[] drFragments = dtOur.Select("Fragment = '" + quotaPriceData[PKID].PriceName + "' and TicketPrice = "+quotaPriceData[PKID].Price.ToString().Replace(",","."));
                                if (drFragments.Length > 0)
                                {
                                    int cpRow = 0;
                                    if (drFragments.Length > 1)
                                    {
                                        for(int tRow = 0; tRow < drFragments.Length; tRow++)
                                        {
                                            if ( drFragments[tRow]["Fragment"].ToString() == quotaPriceData[PKID].PriceName)
                                            {
                                                cpRow = tRow; break;
                                            }
                                        }
                                    }

                                    freeCount = ((drFragments[cpRow]["FreeCount"] is int) ? Convert.ToInt32(drFragments[cpRow]["FreeCount"]) : 0)
                                        - ((drFragments[cpRow]["SoldPRMCount"] is int) ? Convert.ToInt32(drFragments[cpRow]["SoldPRMCount"]) : 0);

                                    if (freeCount >= 0) nonNumberedFreeSeats[PKID].OurFreeCount = freeCount;
                                }
                            }

                            if (freeCount < 0)
                            {
                                freeCount = Convert.ToInt32(XmlTools.SafeGetChildValue(ndOffset, "iFree")) - Convert.ToInt32(XmlTools.SafeGetChildValue(ndOffset, "iAnzOpt"));
                            }
                        }
                        catch
                        {
                            SaveWarningMessage(LogStatus.Error, ndOption,string.Format("Non-numbered quota for offset {0} has non-integer seat counts",offset));                            
                        }
                        #endregion
                    }

                    foreach (NonNumberedPriceData nnData in nonNumberedFreeSeats.Values)
                    {
                        int freeCount = (nnData.OurFreeCount.HasValue && nnData.OurFreeCount.Value >= 0) ? nnData.OurFreeCount.Value : nnData.HQFreeCount;
                        
                        #region Adding to quotaOptionInfo
                        if (freeCount > 0)
                        {
                            string PKID = nnData.PriceID;
                            string key = string.Format("{0} | Non-numbered for {1}", PKID, quotaPriceData[PKID].Price);
                            if (!quotaOptionInfo.ContainsKey(key))
                            {
                                ReturnInvoiceRowInfo rowInfo = new ReturnInvoiceRowInfo();

                                rowInfo.Fragment = quotaPriceData[PKID].PriceName;
                                rowInfo.Row = "-";
                                rowInfo.Seats = "-";
                                rowInfo.SeatCount = freeCount;
                                rowInfo.Price = quotaPriceData[PKID].Price;
                                rowInfo.PriceLfdID = quotaPriceData[PKID].PriceLfdID;
                                rowInfo.Amount = rowInfo.SeatCount * rowInfo.Price;
                                quotaOptionInfo.Add(key, rowInfo);
                            }
                            else
                            {
                                SaveWarningMessage(LogStatus.Warning, ndOption,"Internal error: non-numbered seats data duplication");
                            }
                        }
                        #endregion
                    }
                }
                #endregion
                
                // skip ~50 lines               

                #endregion
            }
            catch (Exception ex)
            {
                ExceptionTools.ProcessException(ex);
                return false;
            }

            return true;
        }

        #endregion

        private XmlNode GetOptionNode(XmlDocument docQuota, string optionName)
        {   
            XmlNode node = XmlTools.SafeAddIdNode(docQuota.DocumentElement, "option", optionName, "optionId");
            return node;
        }

        #region Returns data as an xml-file for report
        public XmlDocument FillReturnInvoiceData(DataSet dsSeance, ref XmlDocument docSeance, string seanceId, string sOptionNumber
            , ref DateTime endSaleDateTime, ref ReturnInvoiceData invoiceInfo)
        {
            XmlDocument xmlInvoiceData = XmlTools.CreateXmlDocument("Windows-1251","repTable");
            //invoiceInfo = new ReturnInvoiceData();
            SortedList<string, ReturnInvoiceRowInfo> quotaInfo = new SortedList<string, ReturnInvoiceRowInfo>();

            int seanceId = Convert.ToInt32(seanceId);
            try
            {
                #region Generate return invoice and validate for specified option
                if (!ValidateQuota(ref docSeance, sOptionNumber, ref quotaInfo, seanceId))
                {
                    //string error = XmlTools.GetErrorMessage(GetOptionNode(docSeance, sOptionNumber));
                    ExceptionTools.GenerateUserMessageException(GetOptionNode(docSeance, sOptionNumber));
                    return xmlInvoiceData;
                }
                #endregion

                XmlNode ndOption = GetOptionNode(docSeance,sOptionNumber); // used only to save loading data error 

                DataTools.SafeGetTable(dsSeance, "RepTable").Rows.Clear();

                #region Creating a dataset of return invoice data to further to bind to grid and transform to xml-document
                foreach (string sk in quotaInfo.Keys)
                {
                    DataRow dr = dsSeance.Tables["RepTable"].NewRow();

                    XmlNode ndRow = xmlInvoiceData.DocumentElement.AppendChild(xmlInvoiceData.CreateElement("item"));
                    XmlTools.SafeAddAttribute(ndRow, "TicketSection", quotaInfo[sk].Fragment);
                    XmlTools.SafeAddAttribute(ndRow, "Row", quotaInfo[sk].Row);
                    XmlTools.SafeAddAttribute(ndRow, "Seats", quotaInfo[sk].Seats);
                    XmlTools.SafeAddAttribute(ndRow, "SeatCount", quotaInfo[sk].SeatCount.ToString());
                    XmlTools.SafeAddAttribute(ndRow, "Price", quotaInfo[sk].Price.ToString().Replace(",", "."));
                    XmlTools.SafeAddAttribute(ndRow, "Amount", quotaInfo[sk].Amount.ToString().Replace(",", "."));
                    XmlTools.SafeAddAttribute(ndRow, "PriceLfdID", quotaInfo[sk].PriceLfdID.ToString());

                    dr["sFragment"] = quotaInfo[sk].Fragment;
                    dr["sRow"] = quotaInfo[sk].Row;
                    dr["SeatList"] = quotaInfo[sk].Seats;
                    dr["SeatCount"] = quotaInfo[sk].SeatCount.ToString();
                    dr["dPrice"] = quotaInfo[sk].Price;
                    dr["dSum"] = quotaInfo[sk].Amount;

                    dsSeance.Tables["RepTable"].Rows.Add(dr);
                    dsSeance.Tables["RepTable"].AcceptChanges();
                }
                #endregion

                #region and now loading extra information about seance:
                if (invoiceInfo.SeanceId.ToString() != seanceId)
                {                    
                    invoiceInfo = GetInvoiceDataFromHQ(Convert.ToInt32(seanceId), true, invoiceInfo);
                }
                endSaleDateTime = invoiceInfo.SalesEndDateTime;

                invoiceInfo.InvoiceOption = Convert.ToInt32(sOptionNumber);
                invoiceInfo.InvoiceOptionName = sOptionNumber == "0" ? "free" : sOptionNumber;

                invoiceInfo.TotalTickets = docSeance.SelectNodes("//PlatzReport[dwPKID < 4294967295]").Count;
                foreach (XmlNode ndKont in docSeance.SelectNodes("//StehPlatzReport/iKont"))
                {
                    try
                    {
                        invoiceInfo.TotalTickets += Convert.ToInt32(ndKont.InnerText);
                    }
                    catch { }
                }


                #endregion
            }
            catch (Exception ex)
            {
                dsSeance.Tables["RepTable"].Rows.Clear();
                xmlInvoiceData.DocumentElement.RemoveAll();

                ExceptionTools.ProcessException(ex);
            }

            return xmlInvoiceData;
        }

        internal ReturnInvoiceData GetInvoiceDataFromHQ(int SeanceId, bool throwEx)
        {
            return GetInvoiceDataFromHQ(SeanceId, throwEx, new ReturnInvoiceData());
        }

        internal ReturnInvoiceData GetInvoiceDataFromHQ(int SeanceId,bool throwEx, ReturnInvoiceData previousInvoiceData)
        {
            ReturnInvoiceData invoiceInfo = new ReturnInvoiceData();
            invoiceInfo.QuotaDateTime = previousInvoiceData.QuotaDateTime;
            
            try
            {
                #region Get info from HQ webservice
                // getting seance and promoter data from HQ webservice

                invoiceInfo.SeanceId = SeanceId;
                DataSet dsSeanceInfo = AdminManager.getHQWSWrapper().getSeanceInfo(AccessManager.GetPassInfo(), invoiceInfo.SeanceId, false);

                DataRow rowSeanceInfo = dsSeanceInfo.Tables[0].Rows[0];

                int seanceStatus = Convert.ToInt32(rowSeanceInfo["SeanceStatus"]);     //1
                
                invoiceInfo.SeanceName = (string)rowSeanceInfo["SeanceName"];          //2
                invoiceInfo.SeanceDateTime = (DateTime)rowSeanceInfo["SeanceDate"];    //3 

                invoiceInfo.SalesEndDateTime = (DateTime)rowSeanceInfo["SalesEndDate"]; //4

                int seanceNumber = Convert.ToInt32(rowSeanceInfo["SeanceNumber"]);     //5
                int returnMonth = (seanceNumber % 100);
                int returnDate = (int)(seanceNumber / 100);
                int returnYear = invoiceInfo.SalesEndDateTime.Year;
                // In case if sales end date is in December, but return date should be in January next year
                if (invoiceInfo.SalesEndDateTime.Month - returnMonth > 6)
                    returnYear++;
                try
                {
                    invoiceInfo.ReturnDateTime = new DateTime(returnYear, returnMonth, returnDate);
                }
                catch { }
                
                invoiceInfo.EventCode = rowSeanceInfo["SeanceInfoText"].ToString();                //6
                try
                {
                    DataSet dsEventCategories = AdminManager.getHQObjects().dsHQEventCategories();
                    int categoryID = Convert.ToInt32(rowSeanceInfo["CategoryID"]);
                    DataRow drCategory = dsEventCategories.Tables[0].Select("COMP_CategoryID = " + categoryID.ToString())[0];

                    object mainCategory = drCategory["ParentRusCategoryName"];
                    if (mainCategory == DBNull.Value || mainCategory.ToString().Trim().Length == 0) mainCategory = drCategory["ParentCategoryName"];
                    invoiceInfo.EventMainCategoryName = mainCategory.ToString();

                    object category = drCategory["RusCategoryName"];
                    if (category == DBNull.Value || category.ToString().Trim().Length == 0) category = drCategory["CategoryName"];
                    invoiceInfo.EventCategoryName = category.ToString();
                }
                catch (Exception ex)
                {
                    SystemLog.WriteException(ex);
                    invoiceInfo.EventMainCategoryName = rowSeanceInfo["EventMainCategoryName"].ToString();  //7 
                    invoiceInfo.EventCategoryName = rowSeanceInfo["EventCategoryName"].ToString();          //8 
                }
                invoiceInfo.EventName = rowSeanceInfo["EventName"].ToString();                          //9

                invoiceInfo.IfIsInSale = ((seanceStatus & 2) == 2);

                invoiceInfo.VenueName = rowSeanceInfo["VenueName"].ToString();                          //10

                invoiceInfo.PromoterId = (int)rowSeanceInfo["PromoterID"];

                invoiceInfo.PromoterCompanyName = (string)rowSeanceInfo["PromoterCompanyName"];

                try
                {
                    invoiceInfo.SeanceShortText = (string)rowSeanceInfo["EventShortText"];
                    invoiceInfo.SeanceLongText = (string)rowSeanceInfo["EventLongText"];
                    invoiceInfo.SeanceComment = (string)rowSeanceInfo["EventComment"];
                    invoiceInfo.AgeRatingText = (string)rowSeanceInfo["AgeRatingText"];
                    invoiceInfo.AdditionalText = (string)rowSeanceInfo["AdditionalText"];
                }
                catch { }

                // Check if HQ data for seance says that sales are still open
                if ((seanceStatus & 1) == 1)
                {
                    throw new ApplicationException("Can't generate return invoice, probably seance is marked as blocked in HQ database");
                }

                DataSet dsPromoterSettings = AdminManager.getmyWSReports().dsPRMGetPromoterSettings(AccessManager.GetPassInfo(), SeanceId);
                string error = AccessManager.GetErrorMessage(dsPromoterSettings);
                if (error == string.Empty && dsPromoterSettings.Tables[0].Rows.Count > 0)
                {
                    invoiceInfo.SendType = (int)dsPromoterSettings.Tables[0].Rows[0]["AutoSentReturnInvoiceType"];
                }
                else
                {
                    invoiceInfo.SendType = 0;
                }

                // Get current company name
                // Here for correcting return invoices data from previous return invoice should be used
                invoiceInfo.OwnerCompanyName = AdminManager.getmyWSBO().sGetCompanyName(AccessManager.GetPassInfo());

                #endregion
            }
            catch (Exception ex)
            {
                if (throwEx)
                    ExceptionTools.ProcessException(ex, "Couldn't get seance data from HQ webservice: ");
                else
                    SystemLog.WriteException(ex, "Couldn't get seance data from HQ webservice: ");
            }
            return invoiceInfo;
        }
        #endregion

        #endregion

        // --------- Reprinting return invoice ----------------------------------------------------------

        // skip ~200 lines

        //-------- Problem tickets --------------------------------------------------------------

        #region Problem tickets

        /// <summary>
        /// Checking for problem tickets for a seance. Same check for period of dates probably will be similar,
        /// only with two different parameters
        /// </summary>
        public bool HasProblemTickets(int SeanceId, XmlDocument docSeance, ref XmlDocument docProblemTickets, ref dsPRMSeanceProblemTickets dsProblemTickets)
        {
            #region Generating xml-document with counts of sold tickets in non-numbered fragments using HQ webservice
            XmlDocument docSoldFreeTickets = XmlTools.CreateXmlDocument("Windows-1251","soldFreeTickets");

            List<string> freeOffsetIds = new List<string>();

            foreach (XmlNode ndFreeOffset in docSeance.SelectNodes("//StehPlatzReport[dwPKID!=4294967295]"))
            {
                string strPriceClassID = XmlTools.SafeGetChildValue(ndFreeOffset, "dwPKID");

                // General Information about price category
                XmlNode ndPriceClass = docSeance.SelectSingleNode(string.Format("//Preise[dwPreisKlass = {0}]", strPriceClassID));
                XmlNode ndOffsetInfo = XmlTools.AddNode(docSoldFreeTickets.DocumentElement, "offset");
                XmlTools.SafeAddAttribute(ndOffsetInfo, "seanceID", SeanceId.ToString());
                
                string strPriceCategoryID = XmlTools.SafeGetChildValue(ndPriceClass, "dwLfd");
                XmlTools.SafeAddAttribute(ndOffsetInfo, "priceCategoryID", strPriceCategoryID);
                freeOffsetIds.Add(strPriceCategoryID);

                string priceCategoryName = XmlTools.SafeGetChildValue(ndPriceClass, "szPreisGrp");
                XmlTools.SafeAddAttribute(ndOffsetInfo, "priceCategoryName", priceCategoryName);

                // Sold tickets count
                int soldTickets = 0;
                try
                {
                    soldTickets += Convert.ToInt32(XmlTools.SafeGetChildValue(ndFreeOffset, "iKont"));
                    soldTickets -= Convert.ToInt32(XmlTools.SafeGetChildValue(ndFreeOffset, "iFree"));
                    soldTickets -= Convert.ToInt32(XmlTools.SafeGetChildValue(ndFreeOffset, "iAnzOpt"));

                    int dwOffset = Convert.ToInt32(XmlTools.SafeGetChildValue(ndFreeOffset, "dwOffset"));
                    foreach (XmlNode ndOptionNode in docSeance.SelectNodes(string.Format("//PlatzOptBelegung[dwOffset = {0}]", dwOffset)))
                    {
                        soldTickets += Convert.ToInt32(XmlTools.SafeGetChildValue(ndOptionNode, "iAnzOptFree"));
                    }                    
                }
                catch (Exception ex)
                {
                    if (SystemLog.ReportException
                                             (ex
                                             , "An error occurred while counting sold tickets in fragment "
                                                    + priceCategoryName
                                                    + " using HQ webservice data. Tickets count won't be determined."
                                                    + " Continue return invoice generation?"
                                             , MessageBoxButtons.YesNo
                                             ) == DialogResult.No
                       )
                    {
                        docProblemTickets = XmlTools.CreateXmlDocument("Windows-1251", "errorOccurred");
                        AccessManager.AddErrorToXml(ref docProblemTickets, ex.Message);
                        return false;
                    }
                }

                XmlTools.SafeAddAttribute(ndOffsetInfo, "soldTickets", soldTickets.ToString());
            }
            #endregion
                       
            // calling webservice with arguments: SeanceId, docSoldFreeTickets.OuterXml
            try
            {

                #region Regular kind of problem tickets (reserved, not delivered)
                DataSet dsWSProblemTickets = AdminManager.getmyWSReports().dsPRMGetReturnProblemTickets(AccessManager.GetPassInfo(), SeanceId, null, null, docSoldFreeTickets.InnerXml);

                // dsWS -> docProblemTickets (if there are), freeOffsetIds determine free offsets: Row column set to "-"
                if (!GetDocProblemTickets(dsWSProblemTickets, ref docProblemTickets, freeOffsetIds))
                    return false;
                #endregion


                #region Report dataset generation
                dsProblemTickets.ProblemTicketRow.Merge(DataTools.GetTableByColumn(dsWSProblemTickets, "RowSeatCount"));

                // fix values in dsProblemTickets from other data
                SummarizeProblemTickets(ref dsProblemTickets, docProblemTickets, freeOffsetIds);

                // ultimately method returns docProblemTickets (for saving) and dsProblemTickets (for showing in report)
                #endregion
            }
            catch (Exception ex)
            {
                SystemLog.WriteException(ex);
                throw;
            }

            return true;            
        }
        // skip..

        private bool GetDocProblemTickets(DataSet dsWSProblemTickets, ref XmlDocument docProblemTickets, List<string> freeOffsetIds)
        {
            // Ош(dsWSProblemTickets) -> docProblemTickets, 0
            #region Check if dataset contains error information
            string errorProblemTickets = AccessManager.GetErrorMessage(dsWSProblemTickets);
            if (errorProblemTickets != null && errorProblemTickets != string.Empty)
            {
                SystemLog.ReportError(errorProblemTickets);
                docProblemTickets = XmlTools.CreateXmlDocument("Windows-1251", "errorOccurred");
                AccessManager.AddErrorToXml(ref docProblemTickets, errorProblemTickets);
                return false;
            }
            #endregion

            // dsWS -> (new)docProblemTickets
            #region Generation of xml-document to save in database
            docProblemTickets = XmlTools.CreateXmlDocument("Windows-1251", "problemTickets");

            DataTable dtProblemTickets = DataTools.GetTableByColumn(dsWSProblemTickets, "TicketSection");
            if (dtProblemTickets == null || dtProblemTickets.Rows.Count == 0)
            {
                return false;
            }

            // for all non-numbered (detected by freeOffsetIDs) Row -> "-"
            XmlDocument tmpDocTickets = new XmlDocument();
            tmpDocTickets.LoadXml(dsWSProblemTickets.GetXml());
            foreach (XmlNode ndTicket in tmpDocTickets.SelectNodes("//" + dtProblemTickets.TableName))
            {
                string PriceCategoryId = XmlTools.SafeGetChildValue(ndTicket, "PriceCategoryId");
                if (freeOffsetIds.Contains(PriceCategoryId))
                {
                    XmlTools.SafeAddChild(ndTicket, "Row").InnerText = "-";
                }

                XmlTools.AddNode(docProblemTickets.DocumentElement, "ticket").InnerXml = ndTicket.InnerXml;
            }
            #endregion

            return true;
        }

        private void SummarizeProblemTickets(ref dsPRMSeanceProblemTickets dsProblemTickets, XmlDocument docProblemTickets, List<string> freeOffsetIds)
        {
            SortedList<int, int> orderSeatCount = new SortedList<int, int>();
            SortedList<int, decimal> orderPriceTotal = new SortedList<int, decimal>();

            foreach (dsPRMSeanceProblemTickets.ProblemTicketRowRow rowRow in dsProblemTickets.ProblemTicketRow.Rows)
            {
                if (freeOffsetIds.Contains(rowRow.PriceCategoryID.ToString()) || rowRow.Row == "-")
                {
                    rowRow.Row = "-";
                    rowRow.Seats = "-";
                }
                else
                {
                    foreach (XmlNode ndRowTicket in docProblemTickets.SelectNodes(
                        string.Format("//ticket[COMP_OrderID={0} and TicketSection='{1}' and Row='{2}']", rowRow.COMP_OrderID, rowRow.TicketSection, rowRow.Row)))
                    {
                        rowRow.Seats += XmlTools.SafeGetChildValue(ndRowTicket, "Seat") + ",";
                    }
                    rowRow.Seats = rowRow.Seats.TrimEnd(',');
                }

                if (!orderSeatCount.ContainsKey(rowRow.COMP_OrderID))
                {
                    orderSeatCount.Add(rowRow.COMP_OrderID, 0);
                    orderPriceTotal.Add(rowRow.COMP_OrderID, 0);
                }
                orderSeatCount[rowRow.COMP_OrderID] += rowRow.RowSeatCount;
                orderPriceTotal[rowRow.COMP_OrderID] += rowRow.RowPriceNominal;
            }

            int currentOrder = -1;
            foreach (dsPRMSeanceProblemTickets.ProblemTicketRowRow rowRow in dsProblemTickets.ProblemTicketRow.Rows)
            {
                if (currentOrder != rowRow.COMP_OrderID)
                {
                    rowRow.OrderSeatCount = orderSeatCount[rowRow.COMP_OrderID];
                    rowRow.OrderPriceNominal = orderPriceTotal[rowRow.COMP_OrderID];
                    currentOrder = rowRow.COMP_OrderID;
                }
                else
                {
                    rowRow.SaleOutletName = "";
                    rowRow.OrderSeatCount = 0;
                    rowRow.OrderPriceNominal = 0;
                }

            }
        }

        #endregion

        // --------- Service methods ---------------------------------------------------------------

        #region Save logs
        /// <summary>
        /// ! This method throws UserMessageException, if logStatus == LogStatus.Error | LogStatus.Fatal
        /// </summary>
        private void SaveWarningMessage(XmlNode ndOption, LogStatus logStatus, string warning, string userWarning)
        {
            SystemLog.WriteLog(warning);

            if (ndOption != null)
            {
                XmlNode ndErrorNode = XmlTools.AddNode(ndOption, logStatus.ToString().ToLower(), warning);
                XmlTools.SafeAddAttribute(ndErrorNode, "userWarning", userWarning);
            }

            if (logStatus == LogStatus.Error || logStatus == LogStatus.Fatal)
            {
                throw new UserMessageException(warning, userWarning);
            }
        }

        /// <summary>
        /// ! This method throws exception, if logStatus == LogStatus.Error | LogStatus.Fatal
        /// </summary>
        private void SaveWarningMessage(LogStatus logStatus, XmlNode ndOption, string warning)
        {
            SaveWarningMessage(ndOption, logStatus, warning, warning);
        }
        #endregion

        
    } // FillReturnInvoiceData

    public class ReturnInvoiceRowInfo
    {
        internal string Fragment;
        internal string Row;
        internal string Seats;
        internal int SeatCount;
        internal decimal Price;
        internal decimal Amount;
        internal int PriceLfdID;
    }

    internal class QuotaPriceData
    {
        internal string PriceID;
        internal Decimal Price;
        internal int PriceLfdID;
        internal string PriceName;
    }

    internal class NonNumberedPriceData
    {
        internal string PriceID;
        internal int? OurFreeCount = null;
        internal int HQFreeCount = 0;
    }
}
