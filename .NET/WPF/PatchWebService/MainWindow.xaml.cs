using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PatchClientWebservices
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string fName;
        public string FolderName
        {
            get { return fName; }
            set { fName = value; this.txProjectFolder.Text = value; } 
        }
        public ObservableCollection<string> WebServices { get; set; }
        public ObservableCollection<string> EntityFiles { get; set; }

        private string cfName; 
        public string CommonFolderName {
            get { return cfName; }
            set { cfName = value; this.txCommonProjectFolder.Text = value; } 
        }
        public ObservableCollection<string> CommonEntityFiles { get; set; }
        public SortedDictionary<string, WSClass> CommonBaseClasses { get; set; }
        
        public ObservableCollection<WSClass> Classes { get; set; }
        public SortedDictionary<string,WSClass> BaseClasses { get; set; }
        public SortedDictionary<string, string> ClassCode { get; set; }
        private String ClassHeader = "";
        private String ClassNamespace = "";
        private bool hasWCFEntitiesNamespace = false;
        private bool hasWCFCommonNamespace = false;
        private string wcfEntitiesNamespace = "WCF.Entities";
        private string wcfCommonNamespace = "Common.Entities";

        private string defaultFolderName = "D:\\Work\\WCF";
        private string defaultCommonFolderName = "D:\\Work\\Common";
        private string defaultSplittingService = "D:\\work\\devexpress\\service";
        private string defaultSplittingEntities = "D:\\work\\devexpress\\entities";

        public MainWindow()
        {
            InitializeComponent();
            WebServices = new ObservableCollection<string>();
            EntityFiles = new ObservableCollection<string>();
            Classes = new ObservableCollection<WSClass>();
            BaseClasses = new SortedDictionary<string,WSClass>();
            CommonEntityFiles = new ObservableCollection<string>();
            CommonBaseClasses = new SortedDictionary<string, WSClass>();
            FolderName = defaultFolderName;
            CommonFolderName = defaultCommonFolderName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try {
            this.DataContext = this;
            RefreshCommonData();
            RefreshData();
            if (WebServices.Count > 0) cbServices.SelectedIndex = 0;

            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void btnFindFolder_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog folderBrowser = new FolderBrowserDialog())
            {
                folderBrowser.SelectedPath = "D:\\Work\\WCF";
                if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FolderName = folderBrowser.SelectedPath;
                    RefreshData();
                    
                }
            }
        }

        // Refreshing data for current webservices:
        // List of webservices is stored in WebServices collection
        // List of files from the Entities folder is stored in EntityFiles collection
        // List of structures, describing classes in EntityFiles is saved in Classes collection
        // Same structures are saved to BaseClasses dict, grouped by names of files, containing sourcecode, as keys
        //
        // This method is called when app starts, when folder with webservices references is changed,
        // and when classes are transferred
        private void RefreshData()
        {
            WebServices.Clear();
            foreach (string webserviceName in Directory.GetDirectories(FolderName + "\\Service References"))
            {
                WebServices.Add(webserviceName.Substring(webserviceName.LastIndexOf("\\") + 1));
            }

            EntityFiles.Clear();

            if (Directory.Exists(FolderName + "\\Entities"))
            {
                foreach (string entityFileName in Directory.GetFiles(FolderName + "\\Entities"))
                {
                    EntityFiles.Add(entityFileName.Substring(entityFileName.LastIndexOf("\\") + 1));
                }
            }

            Classes.Clear();
            BaseClasses.Clear();
            foreach (string entityFile in EntityFiles)
            {
                List<string> classes = GetClassList(FolderName + "\\Entities\\" + entityFile);
                foreach (string className in classes)
                {
                    WSClass wsClass = new WSClass();
                    wsClass.EntityFile = entityFile;
                    wsClass.Name = className;
                    Classes.Add(wsClass);
                    BaseClasses.Add(wsClass.Name, wsClass);
                }
            }

            foreach (WSClass wsClass in CommonBaseClasses.Values)
            {
                Classes.Add(wsClass);
                BaseClasses.Add(wsClass.Name, wsClass);
            }
        }

        #region Get list of classes from file
        private List<String> GetClassList(string filename)
        {
            List<String> classes = new List<string>();
            string line;
            Regex classRe = new Regex("public\\s*partial\\s*class\\s*(\\S+)\\s*\\:");
            using (StreamReader reader = new StreamReader(filename))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    MatchCollection mc = classRe.Matches(line);
                    if (mc.Count > 0)
                    {
                        foreach (Match m in mc)
                        {
                            classes.Add(m.Groups[1].Value);
                        }
                    }
                }
            }
            return classes;
        }
        #endregion

        #region Read complete file
        public SortedDictionary<string,string> GetClassTexts(string filename, ref string header, ref string classNamespace, ref bool hasCommon, ref bool hasWCF)
        {
            SortedDictionary<string, string> classes = new SortedDictionary<string, string>();
            string line;
            Regex classRe = new Regex("public\\s*partial\\s*class\\s*(\\S+)\\s*\\:");
            Regex enumRe = new Regex("public\\s*enum\\s*(\\S+)\\s*\\:");
            Regex interfaceRe = new Regex("public\\s*interface\\s*(\\S+)\\s");
            string attributeRe = "^\\s*\\[.*\\]\\s*$";
            Regex emptylineRe = new Regex("public\\s*partial\\s*class\\s*(.*)\\s*\\:");

            Regex namespaceRe = new Regex("^\\s*namespace\\s*(\\S+)\\s*{\\s*$");

            string buf = "";
            string currentClass = "";
            bool internalStarted = false;
            
            header = "";
            classNamespace = "";

            hasCommon = false;
            hasWCF = false;

            using (StreamReader reader = new StreamReader(filename))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    MatchCollection nsC = namespaceRe.Matches(line);
                    if (nsC.Count > 0)
                    {
                        classNamespace = nsC[0].Groups[1].Value;
                    }

                    #region Check additional namespaces
                    if (Regex.IsMatch(line, "using\\s*" + wcfCommonNamespace))
                        hasWCF = true;
                    if (Regex.IsMatch(line, "using\\s*" + wcfEntitiesNamespace))
                        hasCommon = true;
                    #endregion

                    #region Remove home namespace
                    if (classNamespace != "")
                    {
                        line = line.Replace(classNamespace + ".", "");
                    }
                    #endregion

                    #region for [DataContractAttribute]: add spaces near = - for diff
                    if (line.IndexOf("DataContractAttribute") > -1)
                    {
                        MatchCollection equalMc = Regex.Matches(line,"(\\S=.|.=\\S)");
                        foreach (Match m in equalMc)
                        {
                            string findString = m.Groups[1].Value;
                            string replaceString = string.Format("{0} = {1}", findString[0], findString[2]);
                            line = line.Replace(findString, replaceString);
                        }
                    }
                    #endregion

                    #region Actual writing to bufs and list of classes and header
                    if (Regex.Matches(line, attributeRe).Count > 0)
                    {
                        buf += FormatLine(line) + Environment.NewLine;
                        internalStarted = true;
                    }
                    else if (internalStarted)
                    {
                        MatchCollection ic = interfaceRe.Matches(line);
                        if (ic.Count > 0)
                        {
                            currentClass = "interface "+ic[0].Groups[1].Value;
                            classes.Add(currentClass, buf + FormatLine(line) + Environment.NewLine);
                        }
                        else
                        {
                            if (!Regex.IsMatch(line, "^}\\s*$"))
                            {
                                MatchCollection rc = classRe.Matches(line);
                                if (rc.Count > 0)
                                {
                                    currentClass = rc[0].Groups[1].Value;
                                    classes.Add(currentClass, buf + FormatLine(line) + Environment.NewLine);
                                }
                                else
                                {
                                    MatchCollection ec = enumRe.Matches(line);
                                    if (ec.Count > 0)
                                    {
                                        currentClass = ec[0].Groups[1].Value;
                                        classes.Add(currentClass, buf + FormatLine(line) + Environment.NewLine);
                                    }
                                    else
                                    {
                                        classes[currentClass] += buf + FormatLine(line) + Environment.NewLine;
                                    }
                                }
                            }
                        }
                        buf = "";
                    }
                    else
                    {
                        header += line + Environment.NewLine;
                    }
                    #endregion

                    // for additional in-code documentation see method btnMove_Click, where all this is written back into files
                }
            }
            return classes;
        }

        // Adds NewLine before opening '{'
        public string FormatLine(string line)
        {
            string regendq = "^(\\s*)\\S.*({\\s*$)";
            MatchCollection mc = Regex.Matches(line, regendq);
            if (mc.Count > 0)
            {
                line = line.Replace(mc[0].Groups[2].Value, Environment.NewLine +  mc[0].Groups[1].Value + mc[0].Groups[2].Value);
            }
            return line;
        }
        #endregion

        #region Refresh grid on source service reselection
        private void cbServices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbServices.SelectedItem != null)
            {
                Classes.Clear();
                foreach (WSClass wsClass in BaseClasses.Values)
                    Classes.Add(wsClass);

                ClassCode = GetClassTexts(FolderName + "\\Service References\\" + cbServices.SelectedItem.ToString() + "\\Reference.cs", ref ClassHeader, ref ClassNamespace
                    , ref hasWCFCommonNamespace, ref hasWCFEntitiesNamespace);
                List<string> inputClasses = GetClassList(FolderName + "\\Service References\\" + cbServices.SelectedItem.ToString() + "\\Reference.cs");
                foreach (string inputClassName in inputClasses)
                {
                    if (!BaseClasses.ContainsKey(inputClassName))
                    {
                        WSClass wsClass = new WSClass();
                        wsClass.Name = inputClassName;
                        
                        if (inputClassName != cbServices.SelectedItem.ToString() + "Client")
                            wsClass.IsChecked = true;

                        Classes.Add(wsClass);
                    }
                }

                

                
            }
        }
        #endregion

        // Returns file content before last closing '}'
        private string GetAllButLast(string filename)
        {
            string result = "";
            string line = "";
            using (StreamReader reader = new StreamReader(filename))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (!Regex.IsMatch(line,"^}\\s*$"))
                    {
                        result += line + Environment.NewLine;
                    }
                }
 
            }
            return result;
        }

        #region Do the move
        // Is called only when Move button is clicked
        private void btnMove_Click(object sender, RoutedEventArgs e)
        {
            SortedDictionary<string, WSClass> classesToForce = new SortedDictionary<string, WSClass>();
            bool doForce = false;

            if (cbEntities.SelectedItem == null)
            {
                //System.Windows.MessageBox.Show("No file was selected. Please select one.");
                // Makes a list of only those classes that were selected for forced transfer. 
                // Doesn't do regular transfer
                foreach (WSClass wsClass in Classes)
                {
                    if (wsClass.Force)
                    {
                        doForce = true;
                        classesToForce.Add(wsClass.Name, wsClass);
                    }
                }
            }
            else
            {


                #region move non-forced
                string entFile = FolderName + "\\Entities\\" + cbEntities.SelectedItem.ToString();
                string serviceClass = FolderName + "\\Service References\\" + cbServices.SelectedItem.ToString() + "\\Reference.cs";

                string firstContent = GetAllButLast(entFile);

                // Writes source code for selected for transfer classes to entity file. 
                // Source code for classes, that weren't selected for transfer and for interfaces is left in Reference.cs
                //
                // Also composes list of classes with source code to replace.
                using (StreamWriter writer = new StreamWriter(entFile))
                {
                    writer.Write(firstContent);
                    using (StreamWriter serviceWriter = new StreamWriter(serviceClass))
                    {
                        serviceWriter.Write(ClassHeader);
                        if (!hasWCFEntitiesNamespace)
                            serviceWriter.WriteLine("\tusing " + wcfEntitiesNamespace + ";");
                        if (!hasWCFCommonNamespace)
                            serviceWriter.WriteLine("\tusing " + wcfCommonNamespace + ";");

                        // writes to ClassCode when webservice is selected
                        foreach (string key in ClassCode.Keys)
                        {
                            if (key.StartsWith("interface "))
                                serviceWriter.Write(ClassCode[key]);
                        }

                        foreach (WSClass wsClass in Classes)
                        {
                            if (wsClass.Force)
                            {
                                doForce = true;
                                classesToForce.Add(wsClass.Name, wsClass);
                            }

                            if (wsClass.IsChecked && wsClass.EntityFile == null)
                            {
                                writer.Write(ClassCode[wsClass.Name]);
                            }
                            else
                            {
                                if (ClassCode.ContainsKey(wsClass.Name))
                                    serviceWriter.Write(ClassCode[wsClass.Name]);
                            }
                        }
                        serviceWriter.WriteLine("}");
                    }
                    writer.WriteLine("}");
                }
                #endregion

            }
            if (doForce)
            {
                foreach (string filename in classesToForce.Values.Select(n => n.EntityFile).Distinct().ToList())
                {
                    if (filename.StartsWith("common "))
                    {
                        ForceParts(CommonFolderName + "\\Entities\\" + filename.Substring(7), classesToForce);
                    }
                    else
                    {
                        ForceParts(FolderName + "\\Entities\\" + filename, classesToForce);
                    }

                }
            }

            RefreshData();

            string todoString = "TODO: 1.Check ExtensionData fields and fix them either with [Transient] attribute, or fixing source.\n 2. Check changes. \n 3.Remove extra classes from Reference.cs";
            System.Windows.MessageBox.Show(todoString);
            //System.Windows.MessageBox.Show(className);



        }

        // Replaces parts of source code saved in filename with source code from parts dict
        private void ForceParts(string filename, SortedDictionary<string, WSClass> parts)
        {
            SortedDictionary<string, string> targetParts;
            string targetHeader = "";
            string targetNsp = "";
            bool target1 = true;
            bool target2 = true;

            targetParts = GetClassTexts(filename, ref targetHeader, ref targetNsp, ref target1, ref target2);

            using (StreamWriter writer = new StreamWriter(filename))
            {
                writer.Write(targetHeader);
                foreach (string targetKey in targetParts.Keys)
                {
                    if (parts.ContainsKey(targetKey))
                        writer.Write(ClassCode[targetKey]);
                    else
                        writer.Write(targetParts[targetKey]);
                }
                writer.Write("\n}\n");
            }
        }
        #endregion

        private void btnCommonEntities_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog folderBrowser = new FolderBrowserDialog())
            {
                folderBrowser.SelectedPath = "D:\\Work\\Common";
                if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    CommonFolderName = folderBrowser.SelectedPath;
                    RefreshCommonData();

                }
            }
        }

        // Saves information about common classes:
        // list of files to CommonEntityFiles and list of class names to CommonBaseClasses 
        // The method is called on app start and when folder with common files is changed
        private void RefreshCommonData()
        {
            CommonEntityFiles.Clear();
            foreach (string entityFileName in Directory.GetFiles(CommonFolderName + "\\Entities"))
            {
                CommonEntityFiles.Add(entityFileName.Substring(entityFileName.LastIndexOf("\\") + 1));
            }

            //Classes.Clear();
            CommonBaseClasses.Clear();
            foreach (string entityFile in CommonEntityFiles)
            {
                List<string> classes = GetClassList(CommonFolderName + "\\Entities\\" + entityFile);
                foreach (string className in classes)
                {
                    WSClass wsClass = new WSClass();
                    wsClass.EntityFile = "common "+entityFile;
                    wsClass.Name = className;
                    //Classes.Add(wsClass);
                    CommonBaseClasses.Add(wsClass.Name, wsClass);
                }
            }
        }

        #region Save parts (for debug)
        private void btnServiceParts_Click(object sender, RoutedEventArgs e)
        {
            if (cbServices.SelectedItem != null)
                SaveParts(FolderName + "\\Service References\\" + cbServices.SelectedItem.ToString() + "\\Reference.cs",null,true);
             
        }

        private void btnCommonParts_Click(object sender, RoutedEventArgs e)
        {
            if (cbCommonEntities.SelectedItem != null)
                SaveParts(CommonFolderName + "\\Entities\\" + cbCommonEntities.SelectedItem.ToString(),null,true);
        }

        private void btnClientParts_Click(object sender, RoutedEventArgs e)
        {
            if (cbEntities.SelectedItem != null)
                SaveParts(FolderName + "\\Entities\\" + cbEntities.SelectedItem.ToString(),null,true);

        }

        // Saves parts of source code for selected classes in selected folder.
        // Asks for save folder if needed.
        // Source code is split by classes in GetClassTexts
        //
        // This method is called when diff operation is performed in Diff window 
        // * results will be saved in folders, specified in that window:
        //   - selected webservice - to WCFFolder
        //   - files from CommonEntityFiles and EntityFiles - to ServiceFolder
        // Previously this was also called when debugging from Main window - not used anymore
        public void SaveParts(string filename, string folderName, bool doClean)
        {
            if (folderName == null)
            using (FolderBrowserDialog tempFolder = new FolderBrowserDialog())
            {
                tempFolder.SelectedPath = "D:\\Work\\devexpress";
                if (tempFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    folderName = tempFolder.SelectedPath;
                }
            }

            if (folderName != null)
            {
                if (!Directory.Exists(folderName))
                    Directory.CreateDirectory(folderName);

                    if (doClean)
                    {
                        DirectoryInfo di = new DirectoryInfo(folderName);
                        foreach (FileInfo fi in di.GetFiles())
                            fi.Delete();
                    }

                    string thisHeader = "";
                    string thisNsp = "";
                    bool thisWCF = false;
                    bool thisCommom = false;

                    SortedDictionary<string, string> thisClasses = GetClassTexts(filename, ref thisHeader, ref thisNsp, ref thisWCF, ref thisCommom);

                    foreach (string key in thisClasses.Keys)
                    {
                        using (StreamWriter writer = new StreamWriter(folderName + "\\" + key,true))
                        {
                            writer.Write(thisClasses[key]);
                        }
                    }
                }
            }

        private void btnDiff_Click(object sender, RoutedEventArgs e)
        {
            DiffWindow diffWindow = new DiffWindow(defaultSplittingService, defaultSplittingEntities, this);
            
                diffWindow.ShowDialog();
            

        }


        
        #endregion

        // Just rereads Reference.cs, nothing saved (what for? possibly unfinished)
        private void btnNorm_Click(object sender, RoutedEventArgs e)
        {
            if (cbServices.SelectedItem != null)
            {
                SortedDictionary<string, string> serviceCode = new SortedDictionary<string,string>();
                string serviceHeader = "";
                string serviceNamespace = "";
                bool hasCommonNamespace = false;
                bool hasEntitiesNamespace = false;

                serviceCode =  GetClassTexts(FolderName + "\\Service References\\" + cbServices.SelectedItem.ToString() + "\\Reference.cs", ref serviceHeader, ref serviceNamespace
                    , ref hasCommonNamespace, ref hasEntitiesNamespace);
                // source in ClassCode, ref ClassHeader, ref ClassNamespace                     , ref hasWCFCommonNamespace, ref hasWCFEntitiesNamespace
            }
        }
    }
}
