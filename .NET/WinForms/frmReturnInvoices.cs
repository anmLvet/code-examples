using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using DevExpress.XtraGrid.Views.BandedGrid;
using AppClient.ADM;
using AppClient.Controls;
using AppClient.Promoters.Classes;
using AppClient.Promoters.DataSets;
using AppClient.Promoters.FilterForms;
using AppClient.Reports.DataSetForReports;
using AppClient.Reports.FormsWithReports;
using AppUtilsLibrary;
using AppUtilsLibrary.HQWS;
using AppUtilsLibrary.WSReports_Ref;
using AppClient.Properties;

namespace AppClient.Promoters.Forms
{
    


    [Serializable]
    public partial class frmReturnInvoices : MainForm
    {
        #region Variables and properties

        private WSReports reportsWebService; // webservice

        #region Form kinds
        private bool ifSubAgent = false;
        private bool ifFirm = false;
        #endregion

        #region Current return invoices data
        private ReturnInvoices serviceClass = new ReturnInvoices();
        private DataSet dsSeance = new DataSet();
        private XmlDocument docSeance = new XmlDocument();
        private SortedList<int, string> optionList;
        private string currentSeanceId = "-1";

        private DateTime endSaleDate = DateTimeTools.NullDate;
        
        private XmlDocument xmlInvoiceData = new XmlDocument();
        

        private DateTime lastInvoiceDate = DateTimeTools.NullDate;

        private ReturnInvoiceSeanceFilter seanceFilter = new ReturnInvoiceSeanceFilter();

        private ReturnInvoiceData returnInvoiceData = new ReturnInvoiceData();
        private dsPRMSeanceProblemTickets dsProblemTickets = new dsPRMSeanceProblemTickets();

        private string problemInvoicePermissionToken = "PROMOTER\\ProblemInvoice";
        #endregion

        // Confirmed return invoices data
        private XmlDocument xmlLoadedSeances = new XmlDocument();

        #region graphical components
        private bool initingCombobox = false;
        private ToolStripMenuItem mnOnlyWithReturnDate;
        private ToolStripMenuItem mnShowConfirmedInvoices;
        private ToolStripMenuItem mnReprintInvoice;
        private ToolStripMenuItem mnReprintInvoiceAct;
        private ToolStripMenuItem mnRefreshOptions;
        private GridBand bandOption = null;
        #endregion

        private DataSet dsReturnInvoiceTypes = new DataSet();

        #endregion

        #region Constructor, initialization
        public frmReturnInvoices()
        {
            InitializeComponent();

            reportsWebService = AdminManager.getmyWSReports();
            xmlLoadedSeances = XmlTools.CreateXmlDocument("seances");

            foreach (GridBand band in bandedGridViewConfirmedList.Bands)
            {
                if (band.Caption == "Option")
                {
                    bandOption = band;
                    break;
                }
            }

            gridConfirmedList.DataSource = null;
        }

        public frmReturnInvoices(bool ifSubAgentMode) : this()
        {
            this.ifSubAgent = ifSubAgentMode;
        }

        private void frmReturnInvoices_Load(object sender, EventArgs e)
        {
            #region Return invoice types
            try
            {
                dsReturnInvoiceTypes = AdminManager.getmyWSReports().dsPRMGetSeanceReturnInvoiceTypes(AccessManager.GetPassInfo());
                string error = AccessManager.GetErrorMessage(dsReturnInvoiceTypes);
                if (error != string.Empty)
                {
                    throw new ApplicationException(error);
                }

                if (dsReturnInvoiceTypes.Tables.Count < 1 || !dsReturnInvoiceTypes.Tables[0].Columns.Contains("Path") || !dsReturnInvoiceTypes.Tables[0].Columns.Contains("Label"))
                    throw new ApplicationException("load error");

                cbReturnInvoiceType.DataSource = dsReturnInvoiceTypes.Tables[0];
                cbReturnInvoiceType.DisplayMember = "Label";
                cbReturnInvoiceType.ValueMember = "Path";

            }
            catch (Exception ex)
            {
                SystemLog.ReportException(ex,"Form initializing error:");                
                Close();
            }
            #endregion


            #region Grid context menu initialization
            if (gridSeances.ContextMenuStrip != null)
            {
                mnOnlyWithReturnDate = gridSeances.AddContextMenuItem("With return date only");
                mnOnlyWithReturnDate.Click += new EventHandler(onlyWithReturnDate_Click);
                mnOnlyWithReturnDate.PerformClick();

                mnShowConfirmedInvoices = gridSeances.AddContextMenuItem("Return invoices history");
                mnShowConfirmedInvoices.Click += new EventHandler(mnShowConfirmedInvoices_Click);
                
                mnReprintInvoice = gridConfirmedList.AddContextMenuItem("Reprint");

                foreach (DataRow dr in dsReturnInvoiceTypes.Tables[0].Rows)
                {
                    ToolStripMenuItem mnReprintItem = new ToolStripMenuItem(dr["ReprintLabel"].ToString());
                    mnReprintItem.Tag = dr["Path"].ToString();
                    mnReprintItem.Click += new EventHandler(mnReprintInvoice_Click);

                    mnReprintInvoice.DropDownItems.Add(mnReprintItem);
                }
                mnRefreshOptions = gridConfirmedList.AddContextMenuItem("Refresh");
                mnRefreshOptions.Click += new EventHandler(mnRefreshOptions_Click);
            }
            #endregion

            bandedGridViewSeances.ActiveFilter.Changed += new EventHandler(ActiveFilter_Changed);
        }

        #endregion

        //------------------ Filter -------------------------------------------------------------

        #region Filter
 
        public override DialogResult Filter()
        {
            DialogResult filterResult = DialogResult.Cancel;
            using (frmReturnInvoicesFilter filterFrm = new frmReturnInvoicesFilter(FilterConfig))
            {
                filterResult = filterFrm.ShowDialog();
                if (filterResult == DialogResult.OK)
                {
                    filterFrm.Hide();
                    this.Refresh();

                    #region Load seance data from local db
                    seanceFilter.dtStart = filterFrm.ReturnDateStart;
                    seanceFilter.dtEnd = filterFrm.ReturnDateEnd;
                    seanceFilter.ifOnlyNotReturned = filterFrm.IfOnlyNotReturned;
                    seanceFilter.isAllPromoters = filterFrm.IsAllPromoters;
                    this.ifFirm = filterFrm.IfFirm;

                    ActRefreshGridSeances(true);
                    #endregion
                }
            }
            return filterResult;
        }

        private void filterToolStripMenuItem_Click(object sender, EventArgs e)
        {                   
            Filter();
        }  
        
        void ActiveFilter_Changed(object sender, EventArgs e)
        {
            if (mnOnlyWithReturnDate != null)
            {
                string filterString = colReturnDate.FilterInfo.FilterString;
                mnOnlyWithReturnDate.Checked = (filterString.ToLower().IndexOf("is not null") > -1);
            }
        }

        void onlyWithReturnDate_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem)
            {
                ToolStripMenuItem item = (ToolStripMenuItem)sender;
                item.Checked = !item.Checked;

                if (item.Checked)
                {
                    colReturnDate.FilterInfo = new DevExpress.XtraGrid.Columns.ColumnFilterInfo("[ReturnDate] Is Not Null", "With return date only");                    
                }
                else
                {
                    colReturnDate.FilterInfo = new DevExpress.XtraGrid.Columns.ColumnFilterInfo();                    
                }            
            }
        }
  
        #endregion

        //------------------ Seance grid update -------------------------------------------

        #region Seance grid update

        private void ActRefreshGridSeances(bool ifResetConfirmedGrid)
        {
            try
            {
                WaitForm.ShowForm("Loading list of seances");

                DataSet dsSeanceList = reportsWebService.dsPRMGetSeanceReturnInvoices(AccessManager.GetPassInfo()
                                , seanceFilter.dtStart
                                , seanceFilter.dtEnd
                                , seanceFilter.ifOnlyNotReturned
                                , seanceFilter.isAllPromoters
                                );
                
                string error = AccessManager.GetErrorMessage(dsSeanceList);
                if (error != string.Empty) throw new ApplicationException(error);

                ActRefreshGridSeances(dsSeanceList, ifResetConfirmedGrid);
            }
            catch (Exception ex)
            {
                SystemLog.WriteException(ex,"Error while refreshing seances list grid: ");
            }
            finally
            {
                WaitForm.HideForm(); 
            }
        }

        private void ActRefreshGridSeances(DataSet dsSeanceList, bool ifResetConfirmedGrid)
        {
            DataTable dtSeance = DataTools.GetTableByColumn(dsSeanceList,"PromoterID");
            DataTable dtLastInvoiceList = DataTools.GetTableByColumn(dsSeanceList, "InvoiceOption");

            gridSeances.SetCurrentId();
 
            if (dtSeance != null)
            {
                dsReturnInvoicesList1.Seance.Rows.Clear();
                dsReturnInvoicesList1.Seance.Merge(dtSeance);
                
                xmlLoadedSeances.DocumentElement.RemoveAll();

                if (ifFirm)
                {
                    gridSeances.DataSource = new DataView(dsReturnInvoicesList1.Seance, "ifFirm=1", "", DataViewRowState.CurrentRows);
                }
                else
                {
                    gridSeances.DataSource = dsReturnInvoicesList1.Seance;
                }

                gridSeances.RefreshDataSource();
                gridSeances.FocusCurrentId();
                gridSeances.Select();                               
            }
            else
            {
                SystemLog.WriteLog("Webservice didn't return a list of seances");
                return;
            }

            if (dtLastInvoiceList != null)
            {
                ActRefreshGridConfirmedInvoices(dtLastInvoiceList,false);
            }
            else
            {
                SystemLog.WriteLog("Webservice didn't return a list of confirmed return invoices");
            }

            foreach (DataRow row in dsReturnInvoicesList1.Seance.Rows)
            {
                XmlTools.SafeAddIdNode(xmlLoadedSeances.DocumentElement, "seance", row["SeanceID"].ToString());
            }
        }

        // TODO: Here data should be added to main dataset as well
        private DataSet GetExtraConfirmedData(int seanceId)
        {
            try
            {
                DataSet dsExtraSeance = this.reportsWebService.dsPRMGetSeanceReturnInvoices1(AccessManager.GetPassInfo(), null, null, seanceId, false, seanceFilter.isAllPromoters);

                string error = AccessManager.GetErrorMessage(dsExtraSeance);
                if (error != string.Empty) throw new ApplicationException(error);

                XmlTools.SafeAddIdNode(xmlLoadedSeances.DocumentElement, "seance", seanceId.ToString());
                return dsExtraSeance;
            }
            catch (Exception ex)
            {
                SystemLog.WriteException(ex);
            }

            return new DataSet();
        }

        void mnRefreshOptions_Click(object sender, EventArgs e)
        {
            ActRefreshGridConfirmedInvoices(new DataTable(), true);
        }

        private void ActRefreshGridConfirmedInvoices(DataTable dtConfirmed, bool forceInvoiceRebuild)
        {
            gridConfirmedList.SetCurrentId();
            initingCombobox = true;

            if (dtConfirmed != null && dtConfirmed.Rows.Count > 0)
            {
                dsReturnInvoicesList1.ConfirmedInvoice.Merge(dtConfirmed);
            }

            if (currentSeanceId != "-1")
            {

                DataTable extraData = DataTools.GetTableByColumn(GetExtraConfirmedData(Convert.ToInt32(currentSeanceId)), "InvoiceOption");
                if (extraData != null)
                    dsReturnInvoicesList1.ConfirmedInvoice.Merge(extraData);

                foreach (KeyValuePair<int, string> option in optionList)
                {
                    try
                    {
                        dsReturnInvoicesList.ConfirmedInvoiceRow rowOption = dsReturnInvoicesList1.ConfirmedInvoice.NewConfirmedInvoiceRow();
                        rowOption.SeanceID = Convert.ToInt32(currentSeanceId);
                        rowOption.InvoiceOption = option.Key;
                        dsReturnInvoicesList1.ConfirmedInvoice.Rows.Add(rowOption);
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is ConstraintException))
                            SystemLog.WriteException(ex);
                    }
                }
            }            

            gridConfirmedList.DataSource = new DataView(dsReturnInvoicesList1.ConfirmedInvoice, "SeanceID = " + currentSeanceId.ToString(), null, DataViewRowState.CurrentRows);
            gridConfirmedList.RefreshDataSource();
            bool focusedPrevious = gridConfirmedList.FocusCurrentId();

            if (!focusedPrevious || forceInvoiceRebuild)
                ActShowInvoiceData();

            initingCombobox = false;
        }

        private void gridSeances_StandardAction(object sender, StandardActionEventArgs e)
        {
            if (e.Action == StandardActions.Refresh)
            {
                ActRefreshGridSeances(false);
            }
            else if (e.Action == StandardActions.Update)
            {
                ActGetSeanceData();
            }
        }
        #endregion

        //------------------ Seance selection ----------------------------------------------------------

        #region Choose seance in grid
        private void bandedGridView1_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            List<DataRow> rows = gridSeances.CurrentRows;
            if (rows.Count > 0 )
            {
                textSeanceID.EditValue = rows[0]["SeanceID"].ToString();                
            }
        }
        #endregion

        //------------------ Load seance details -------------------------------------------------------

        #region Load seance details, display information about option
        private void btnGetSeanceData_Click(object sender, EventArgs e)
        {
            ActGetSeanceData();
        }

        private void ActGetSeanceData()
        {
            XmlNode ndError = null;
            try
            {
                bool bResult = AdminManager.getmyWSReports().SeanceAccess(AccessControlObject.CurrentInstance.UserId, Convert.ToInt32(textSeanceID.EditValue));
                if (!bResult)
                {
                    MessageBox.Show("You don't have permissions to create return invoices for this seance!");
                    return;
                }

                WaitForm.ShowForm("Getting seance details");
                optionList = new SortedList<int, string>();
                int optcount = optionList.Count;

                SystemLog.WriteLog("Started getting data for seance " + textSeanceID.EditValue.ToString());

                docSeance = serviceClass.GetSeanceData(textSeanceID.EditValue.ToString(), ref optionList, ref dsSeance, ref ndError);

                returnInvoiceData = new ReturnInvoiceData();
                returnInvoiceData.QuotaDateTime = DateTime.Now;
                returnInvoiceData.ConfirmedBeforeBasketTime = false;

                txtContractNumber.Text = string.Empty;

                SystemLog.WriteLog("Data for seance " + textSeanceID.EditValue.ToString() + " is loaded");

                if (optionList.Count == 0)
                {  
                    throw new UserMessageException("Neither free seats, nor tickets in options weren't found");                    
                }
                else
                {
                    currentSeanceId = textSeanceID.EditValue.ToString();
                }
                ActRefreshGridConfirmedInvoices(new DataTable(),true);
            }
            catch (Exception ex)
            {
                currentSeanceId = "-1";
                ActClearSeanceInfo();

                WaitForm.HideForm();
                ExceptionTools.ShowException(ex, "Exception occurred while getting seance data:");
            }
            finally
            {                
                WaitForm.HideForm();
            }

        }
        #endregion

        //------------------ Return invoice generation --------------------------------------------------

        #region Generate return invoice from already loaded data (re-requests only general seance parameters)

        private void bandedGridViewConfirmedList_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            DataRow rowOption = gridConfirmedList.CurrentRow;
            Color reprintForeColor = (rowOption == null || rowOption["LastConfirmDateTime"] == DBNull.Value) ? Color.Gray : Color.Black;

            mnReprintInvoice.ForeColor = reprintForeColor;

            foreach (ToolStripMenuItem mnReprintItem in mnReprintInvoice.DropDownItems)
                mnReprintItem.ForeColor = reprintForeColor;

            /*if (rowOption != null)
            {
                mnReprintInvoiceAct.ForeColor = mnReprintInvoice.ForeColor = (rowOption["LastConfirmDateTime"] == DBNull.Value) ? Color.Gray : Color.Black; ;
            }
            else
                mnReprintInvoiceAct.ForeColor = mnReprintInvoice.ForeColor = Color.Gray;*/

            if (!initingCombobox) 
            {
                initingCombobox = true;
                ActRefreshGridConfirmedInvoices(new DataTable(), true);
                initingCombobox = false;
            }
        }
 
        /// <summary>
        /// Displays current return invoice, selected from available options grid, or clears return invoice data
        /// </summary>
        private void ActShowInvoiceData()
        {
            try
            {
                WaitForm.HideForm();

                WaitForm.ShowForm("Return invoice is generated");

                // Show return invoice data in the grid
                btnReturnInvoice.Text = "Confirm return invoice";
                btnReturnInvoice.Enabled = (bandedGridViewConfirmedList.RowCount > 0);

                dataTable2.Rows.Clear();

                DataRow rowOption = bandedGridViewConfirmedList.GetDataRow(bandedGridViewConfirmedList.FocusedRowHandle);

                if (rowOption != null)
                {
                    endSaleDate = DateTimeTools.NullDate;

                    string option = rowOption["InvoiceOption"].ToString();

                    SystemLog.WriteLog("Return invoice generation for seance {0}, option {1} started ",textSeanceID.EditValue.ToString(),option);
                    
                    xmlInvoiceData = serviceClass.FillReturnInvoiceData(dsSeance, ref docSeance, currentSeanceId, option
                        , ref endSaleDate, ref returnInvoiceData);

                    SystemLog.WriteLog("Return invoice generation for seance {0}, option {1} ended ", textSeanceID.EditValue.ToString(), option);

                    #region Show data in the form
                    if (dsSeance.Tables["RepTable"].Rows.Count > 0 || option=="0")
                    {
                        textRepType.EditValue = (option == "0") ? "Final" : "Option " + option;
                        textRepPromoter.EditValue = returnInvoiceData.PromoterCompanyName;
                        textRepSeanceId.EditValue = returnInvoiceData.SeanceId.ToString();
                        try
                        {
                            textRepSeanceDate.EditValue = returnInvoiceData.SeanceDateTime.ToString("dd.MM.yyyy HH:mm");
                        }
                        catch { }
                        textRepSeanceName.EditValue = returnInvoiceData.SeanceName;
                        textRepLogin.EditValue = AccessManager.login;// Environment.UserDomainName + "\\" + Environment.UserName;

                        cbAutoSend.Checked = false;
                        cbAutoSend.Enabled = (option == "0" && returnInvoiceData.SendType > 0);
                    }
                    else
                    {
                        textRepType.EditValue =
                        textRepPromoter.EditValue = textRepSeanceId.EditValue = textRepSeanceDate.EditValue = textRepSeanceName.EditValue = textRepLogin.EditValue = string.Empty;
                        cbAutoSend.Checked = false;
                        cbAutoSend.Enabled = false;
                    }

                    bool alreadyConfirmed = ActCheckAlreadyConfirmed();

                    int maxOrderBasketTime = serviceClass.MaxOrderBasketTime;
                    string exceptionPromoters = serviceClass.ExceptionPromoters;

                    if (option == "0" && endSaleDate > returnInvoiceData.QuotaDateTime)
                    {
                        WaitForm.HideForm();
                        btnReturnInvoice.Enabled = false;
                        btnReturnInvoice.Text = "Seance sales are not closed";
                        MessageBox.Show("Final return invoice creation is not permitted, seance sales are not closed (at the time of last data request)!");
                    }
                    else if (option == "0" && endSaleDate > returnInvoiceData.QuotaDateTime.AddMinutes(-maxOrderBasketTime))
                    {
                        WaitForm.HideForm();
                        bool giveException = (exceptionPromoters.Contains(returnInvoiceData.PromoterId.ToString()));
                        if (!giveException || !alreadyConfirmed)
                        { 
                            btnReturnInvoice.Enabled = giveException;
                            btnReturnInvoice.Text = string.Format("Sales are closed less than {0:d} minutes ago", maxOrderBasketTime);
                            if (giveException)
                            {
                                MessageBox.Show(string.Format("Attention! At the time of seance data request less then {0:d} minutes passed since seance sales end."
                                    + " For this promoter it is permitted to confirm return invoice at this time, but it can contain incorrect data. "
                                    + " After return invoice is confirmed, it should be checked later, when {0:d} minutes will pass after sales end."
                                    , maxOrderBasketTime), "Attention");
                            }
                            else
                            {
                                MessageBox.Show(string.Format("Final return invoice is not permitted, becase less then {0:d} minutes passed after sales end (at the time of seance data request).", maxOrderBasketTime));
                            }
                        }
                        else
                        {
                            MessageBox.Show(string.Format("Sales are closed less then {0:d} minutes ago. You can confirm this return invoice if you cancel previous one, but the data in this return invoice still can be incorrect.", maxOrderBasketTime));
                        }
                    }

                    dataTable2.Merge(dsSeance.Tables["RepTable"]);

                    // *** Final return invoice without tickets is permitted
                    if (dataTable2.Rows.Count == 0 && option != "0")
                    {
                        WaitForm.HideForm();
                        btnReturnInvoice.Enabled = false;
                        MessageBox.Show("There are no tickets to return in " + ((option == "0") ? "final return invoice" : "return invoice for this option"));
                    }

                    gridInvoiceData.RefreshDataSource();
                    #endregion
                } // something non-null selected
                else
                {
                    ActClearInvoiceInfo();
                }
            }
            catch (Exception ex)
            {
                ActClearInvoiceInfo();
                WaitForm.HideForm();

                ExceptionTools.ShowException(ex, "Return invoice cannot be generated: ");
            }
            finally
            {
                WaitForm.HideForm();
            }

        }

        private void ActClearSeanceInfo()
        {
            currentSeanceId = "-1";
            ActRefreshGridConfirmedInvoices(new DataTable(),false);

            ActClearInvoiceInfo();
        }

        private void ActClearInvoiceInfo()
        {
            btnReturnInvoice.Text = "Confirm return invoice";
            btnReturnInvoice.Enabled = false;

            textRepType.EditValue =
            textRepPromoter.EditValue = textRepSeanceId.EditValue = textRepSeanceDate.EditValue = textRepSeanceName.EditValue = textRepLogin.EditValue = string.Empty;

            dataTable2.Rows.Clear();
            gridInvoiceData.RefreshDataSource();
        }
        #endregion

        //------------------Confirmation and printing  --------------------------------------------------

        #region Return invoice preparing and printing
        private void btnReturnInvoice_Click(object sender, EventArgs e)
        {
            ActConfirmInvoice();
        }

        private bool ActCheckAlreadyConfirmed()
        {
            DataRow rowConfirmed = gridConfirmedList.CurrentRow;
            if (rowConfirmed != null && rowConfirmed["LastConfirmDateTime"] != DBNull.Value)
            {
                btnReturnInvoice.Enabled = false;
                btnReturnInvoice.Text = "Return invoice already confirmed";
                return true;
            }
            return false;
        }

        private void ActConfirmInvoice()
        {
            #region Check if return invoice is confirmed 
            initingCombobox = true;
            ActRefreshGridConfirmedInvoices(new DataTable(), false);
            initingCombobox = false;

            if (ActCheckAlreadyConfirmed())
            {
                MessageBox.Show("Return invoice is already confirmed. To confirm new return invoice, you need to first cancel confirmed invoice. You can also reprint confirmed invoice from option list's context menu.","Error");
                return;
            }
            #endregion


            #region Initial checks
            if (!dsSeance.Tables.Contains("RepTable"))
            {
                MessageBox.Show("Internal error: no RepTable");
                return; 
            }

            DataTable repTable = dsSeance.Tables["RepTable"];

            if (bandedGridViewConfirmedList.RowCount == 0 || bandedGridViewConfirmedList.GetDataRow(bandedGridViewConfirmedList.FocusedRowHandle) == null)
            {
                MessageBox.Show("Internal error: return invoice's option is not selected");
                return;
            }

            string optionName;

            try
            {
                DataRow rowOption = bandedGridViewConfirmedList.GetDataRow(bandedGridViewConfirmedList.FocusedRowHandle);
                optionName = rowOption["InvoiceOption"].ToString();
            }
            catch
            {
                MessageBox.Show("Internal error: return invoice's option is not selected");
                return;
            }
            #endregion

            returnInvoiceData.MaxOrderBasketTime = 0;
            if (returnInvoiceData.InvoiceOption == 0)
            {
                if (returnInvoiceData.SalesEndDateTime > returnInvoiceData.QuotaDateTime.AddMinutes(-serviceClass.MaxOrderBasketTime))
                {
                    returnInvoiceData.ConfirmedBeforeBasketTime = true;
                    returnInvoiceData.MaxOrderBasketTime = serviceClass.MaxOrderBasketTime;
                }
            }

            returnInvoiceData.ContractNumber = txtContractNumber.Text;

            returnInvoiceData.seanceFilter = seanceFilter;
            returnInvoiceData.docInvoiceTickets = xmlInvoiceData.InnerXml;

            #region Check problem tickets (only for final return invoice)
            bool hasProblemTickets = false;
            XmlDocument docProblemTickets = new XmlDocument();

            if (optionName == "0")
            {
                WaitForm.ShowForm("Seance is checked for problem tickets");
                
                hasProblemTickets = serviceClass.HasProblemTickets(returnInvoiceData.SeanceId, docSeance, ref docProblemTickets, ref dsProblemTickets);

                int showMaxBasketTime = 0;
                if (returnInvoiceData.ConfirmedBeforeBasketTime)
                {
                    showMaxBasketTime = serviceClass.MaxOrderBasketTime;
                }

                if (hasProblemTickets || returnInvoiceData.ConfirmedBeforeBasketTime)
                {
                    returnInvoiceData.docProblemTickets = docProblemTickets.InnerXml;
                    // Problem tickets report
                    SortedList<string, int> problemTicketsByOrders = new SortedList<string, int>();
                    foreach (XmlNode ndProblemTicket in docProblemTickets.SelectNodes("//ticket"))
                    {
                        string orderId = XmlTools.SafeGetChildValue(ndProblemTicket, "COMP_OrderID");
                        if (problemTicketsByOrders.ContainsKey(orderId))
                            problemTicketsByOrders[orderId]++;
                        else
                            problemTicketsByOrders.Add(orderId, 1);
                    }

                    WaitForm.HideForm();

                    string problemMessage = "";
                    if (hasProblemTickets)
                        problemMessage = "Seance has problem tickets. ";
                    if (returnInvoiceData.ConfirmedBeforeBasketTime)
                        problemMessage += "Seance data is loaded earlier than " + serviceClass.MaxOrderBasketTime.ToString() + " minutes after sales end. ";

                    DialogResult optionContinue = MessageBox.Show(problemMessage+"Show report?\r\n(" + problemTicketsByOrders.Keys.Count.ToString() + " orders with problem tickets found)", "Attention", MessageBoxButtons.YesNoCancel);
                    if (optionContinue == DialogResult.Cancel)
                        return;

                    if (optionContinue == DialogResult.Yes)
                    {
                        using (frmPRMSeanceProblemOrders problemOrdersReport = new frmPRMSeanceProblemOrders(returnInvoiceData, dsProblemTickets, showMaxBasketTime))
                        {
                            problemOrdersReport.ShowDialog();
                        }
                    }

                    if (!AccessManager.IsSuccess(AccessControlObject.CurrentInstance.CheckObjectAccess(problemInvoicePermissionToken, false)))
                    {
                        MessageBox.Show("You don't have permissions to confirm final return invoice with problem tickets", "Error");
                        return;
                    }
                }
                else
                {
                    WaitForm.HideForm();
                }
            }

            #endregion

            if (MessageBox.Show(string.Format("You are going to confirm {0}.\n" +
                       "You will be able to cancel this action later from the confirmed invoices list form\nonly if you have permissions for return invoices cancellation.\n" +
                       "Continue?"
                    , optionName == "0" 
                        ? "final return invoice for seance "+returnInvoiceData.SeanceId.ToString()  
                        : string.Format("return invoice for seance {0} and option {1}",returnInvoiceData.SeanceId,optionName)
                ), "Attention!", MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }

            if (returnInvoiceData.ConfirmedBeforeBasketTime)
            {
                if (MessageBox.Show(string.Format("You are going to confirm final return invoice earlier than {0:d} minutes after seance's sales end. "
                    + "Return invoice data may be incorrect. In {0:d} minutes after the sales end you should check the return invoice data again "
                    + "using, for example, seance quota and lost fragments report forms. If required time has passed right now, "
                    + "load seance data again by pressing button \"Get seance data\" and confirm the return invoice. "
                    + "Continue return invoice confirmation?", serviceClass.MaxOrderBasketTime), "Attention!", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return;
                }
            }            

            string sError = string.Empty;
            int errorCode = 1;

            try
            {
                WaitForm.ShowForm("Report is generating");

                returnInvoiceData.NeedAutoSend = cbAutoSend.Checked;

                DataSet dsSeanceList = reportsWebService.dsPRMAddReturnInvoice(AccessManager.GetPassInfo(), returnInvoiceData, ref errorCode);

                string error = AccessManager.GetErrorMessage(dsSeanceList);
                if (error != string.Empty) throw new ApplicationException(error);

                if (errorCode > 1)
                {
                    DataTable dtInvoiceDateTime = DataTools.GetTableByColumn(dsSeanceList, "SetInvoiceDateTime");

                    returnInvoiceData.InvoiceDateTime = DateTime.Now;

                    if (dtInvoiceDateTime != null)
                    {
                        try
                        {
                            returnInvoiceData.InvoiceDateTime = Convert.ToDateTime(dtInvoiceDateTime.Rows[0][0]);
                        }
                        catch { }
                    }

                    string typePath = cbReturnInvoiceType.SelectedValue.ToString();

                    if (typePath == "RETURN_INVOICE") //(!cbIfAct.Checked)
                    {
                        using (AppClient.Reports.FormsWithReports.frmPRMReturnInvoiceStimul frmReturnInvoice
                           = new AppClient.Reports.FormsWithReports.frmPRMReturnInvoiceStimul(
                                          xmlInvoiceData
                                        , returnInvoiceData
                            ))
                        {
                            frmReturnInvoice.ShowDialog();
                        }
                    }
                    else
                    {
                        using (AppClient.Reports.FormsWithReports.frmPRMReturnInvoiceAct frmReturnInvoice
                            = new AppClient.Reports.FormsWithReports.frmPRMReturnInvoiceAct(
                                           xmlInvoiceData
                                         , returnInvoiceData
                                         , typePath
                            ))
                        {
                            frmReturnInvoice.ShowDialog();
                        }
                    }

                    ActRefreshGridSeances(dsSeanceList, false);
                    ActCheckAlreadyConfirmed();
                    this.textSeanceID.Text = currentSeanceId;
                }
                else
                {
                    WaitForm.HideForm();
                    if (errorCode == 1)
                        MessageBox.Show("You don't have permissions to perform this task");
                    else
                        MessageBox.Show("Failed to add return invoice: " + AccessManager.GetErrorMessage(dsSeanceList) + ", return code is " + errorCode);
                }
            }
            catch (Exception ex)
            {
                SystemLog.WriteException(ex, "While confirming return invoice");
                ex.ShowMessage();
            }
            finally
            {
                WaitForm.HideForm();
            }
        }
        #endregion

        //----------------- Return invoices history -----------------------------------------------------------

        #region Confirmed return invoices form
  
        void mnShowConfirmedInvoices_Click(object sender, EventArgs e)
        {
            DataRow rowSeance = gridSeances.CurrentRow;
            if (rowSeance != null)
            {
                ActShowReturnInvoiceHistory(Convert.ToInt32(rowSeance["SeanceID"]));
            }
        }

        private void btnReturnInvoiceHistory_Click(object sender, EventArgs e)
        {
            try
            {
                ActShowReturnInvoiceHistory(Convert.ToInt32(textSeanceID.Text));
            }
            catch { }

        }

        private void ActShowReturnInvoiceHistory(int seanceId)
        {
            using (frmConfirmedReturnInvoices confirmedFrm = new frmConfirmedReturnInvoices(seanceId, dsReturnInvoiceTypes.Tables[0]))
            {
                confirmedFrm.ShowDialog();
            }
        }

        #endregion

        //----------------- Reprinting -------------------------------------------------------------
        // skip..

        #region Invoices to return form

        private void invoicesToReturnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AppClient.Reports.FormsWithReports.frmPRMInvoicesToReturn invoicesToReturn =
                new AppClient.Reports.FormsWithReports.frmPRMInvoicesToReturn(this.seanceFilter.dtStart.Value, this.seanceFilter.dtEnd.Value, !mnOnlyWithReturnDate.Checked)
            )
            {
                invoicesToReturn.ShowDialog();
            }
        }

        #endregion

        //----------------- User interface -----------------------------------------------------


        #region User interface 
        
        private void textSeanceID_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnGetSeanceData.PerformClick();
            }
        }



        private void bandedGridViewSeances_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            DataRow row = this.bandedGridViewSeances.GetDataRow(e.RowHandle);
            if (row != null && row["ReturnDate"] != null && row["ReturnDate"] != DBNull.Value)
            {
                try
                {
                    DateTime returnTime = Convert.ToDateTime(row["ReturnDate"]);
                    if (returnTime < DateTime.Now && (row["LastConfirmDateTime"] == null || row["LastConfirmDateTime"] == DBNull.Value))
                        e.Appearance.ForeColor = Color.Red;

                    
                }
                catch { }
            }
            try
            {
                if (row["LastConfirmDateTime"] != null && row["LastConfirmDateTime"] != DBNull.Value && Convert.ToInt32(row["CountReturnTickets"]) == 0)
                {
                    e.Appearance.ForeColor = Color.Brown;
                }
            } catch {}
        }
        
        private void frmReturnInvoices_Resize(object sender, EventArgs e)
        {
            /*gridConfirmedList.Width = Math.Min (this.Width - 300, Screen.PrimaryScreen.WorkingArea.Width / 2 - 20);
            gridConfirmedList.Left = this.Width - 20 - gridConfirmedList.Width;// Math.Min(Screen.PrimaryScreen.Bounds.Width/2, this.Width/2)*/
            //gridConfirmedList.Width = (Screen.PrimaryScreen.Bounds.Width / 2, this.Width)
        }
        #endregion

        private void gridConfirmedList_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && mnReprintInvoice != null)
            {
                DataRow rowOption = gridConfirmedList.CurrentRow;
                Color reprintForeColor = (rowOption == null || rowOption["LastConfirmDateTime"] == DBNull.Value) ? Color.Gray : Color.Black;

                mnReprintInvoice.ForeColor = reprintForeColor;

                foreach (ToolStripMenuItem mnReprintItem in mnReprintInvoice.DropDownItems)
                    mnReprintItem.ForeColor = reprintForeColor;

                /*if (rowOption != null)
                {
                    mnReprintInvoiceAct.ForeColor = mnReprintInvoice.ForeColor = (rowOption["LastConfirmDateTime"] == DBNull.Value) ? Color.Gray : Color.Black; ;
                }
                else
                    mnReprintInvoiceAct.ForeColor = mnReprintInvoice.ForeColor = Color.Gray;*/
            }
        }

        private void gridConfirmedList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && mnReprintInvoice != null)
            {
                DataRow rowOption = gridConfirmedList.CurrentRow;
                Color reprintForeColor = (rowOption == null || rowOption["LastConfirmDateTime"] == DBNull.Value) ? Color.Gray : Color.Black;

                mnReprintInvoice.ForeColor = reprintForeColor;

                foreach (ToolStripMenuItem mnReprintItem in mnReprintInvoice.DropDownItems)
                    mnReprintItem.ForeColor = reprintForeColor;
            }
        }

        private void butTmp_Click(object sender, EventArgs e)
        {
            WaitForm.ShowForm("Saving quota data");
            DataSet ds = new DataSet();
            ds.Tables.Add(new DataTable());
            ds.Tables[0].Columns.Add("SeanceID", typeof(int));
            DataRow row = ds.Tables[0].NewRow();
            row[0] = Convert.ToInt32(this.textSeanceID.Text);
            ds.Tables[0].Rows.Add(row);
            Quota.GetQuotaByDS(ds);
            WaitForm.HideForm();
        }

        private void confirmDeliveryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if (!frmMain.ShowUnstableFeatures)
            //    return;

            List<DataRow> rows = gridSeances.CurrentRows;
            if (rows.Count > 0)
            {
                int returnStatus = (int)rows[0]["ReturnInvoiceStatusID"];
                int seanceId = (int)rows[0]["SeanceID"];

                if (returnStatus >= 2 && returnStatus <= 10 && returnStatus != 9)
                {
                    if (MessageBox.Show("Confirm final return invoice delivery for seance " + seanceId.ToString() + "?", "Attention", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        try
                        {
                            string result = AdminManager.getmyWSReports().PRMSetSeanceReturnInvoiceStatus(AccessManager.GetPassInfo(), seanceId, 9);

                            string error = XmlTools.GetErrorMessage(result);
                            if (error != string.Empty) throw new ApplicationException(error);

                            ActRefreshGridSeances(false);
                        }
                        catch (Exception ex)
                        {
                            SystemLog.ReportException(ex);
                        }
                    }
                }
            }

        }

        private void cbReturnInvoiceType_SelectedIndexChanged(object sender, EventArgs e)
        {
            try {
                DataRow rowInvoiceType = dsReturnInvoiceTypes.Tables[0].Select("Path='" + cbReturnInvoiceType.SelectedValue.ToString() + "'")[0];
                txtContractNumber.Properties.Enabled = Convert.ToBoolean(rowInvoiceType["RequireContractDate"]);
            }
            catch 
            {
                txtContractNumber.Properties.Enabled = false;
            }
        }


        
    }
}