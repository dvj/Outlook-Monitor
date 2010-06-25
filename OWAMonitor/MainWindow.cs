using System;
using System.Windows.Forms;
using Microsoft.Exchange.WebServices.Data;
using Growl.Connector;


namespace OWAMonitor
{
    public partial class MainWindow : Form
    {
        private readonly GrowlConnector growl;
        private readonly NotificationType notificationType;
        private Growl.Connector.Application application;
        private const string SampleNotificationType = "OWAMonitor";
        private Timer _timer;
        private FormWindowState _mPreviousWindowState;
        private readonly ExchangeService _exchangeService;
        private PullSubscription _ps;
        private DateTime _latestMessageDate = DateTimeOffset.Now.LocalDateTime;
        private FindFoldersResults _folders;
        /* Using this doesn't work either
        private PropertySet _propertySet = new PropertySet(BasePropertySet.IdOnly)
                                  {
                                      ItemSchema.Body
                                  };
        */
        public MainWindow()
        {
            InitializeComponent();
            notificationType = new NotificationType(SampleNotificationType, "Sample Notification");
            growl = new GrowlConnector();
            growl.NotificationCallback += new GrowlConnector.CallbackEventHandler(GrowlNotificationCallback);
            RegisterGrowl();

            growl.EncryptionAlgorithm = Cryptography.SymmetricAlgorithmType.PlainText;            
            _exchangeService = new ExchangeService(ExchangeVersion.Exchange2007_SP1);
            //_exchangeService.Credentials = new NetworkCredential("{Active Directory ID}", "{Password}", "{Domain Name}");

            bool success = ConfigureExchange();

            if (!success)
            {
                messageTextBox.AppendText("Not connected");
            }
            
        }

        protected bool ConfigureExchange()
        {           
            try
            {
                _exchangeService.AutodiscoverUrl(emailTextBox.Text);
            }
            catch (Exception e)
            {
                messageTextBox.Text += e;
                return false;
            }
            try
            {
                _folders = _exchangeService.FindFolders(WellKnownFolderName.Inbox, new FolderView(50));
            }
            catch (Exception e)
            {
                messageTextBox.Text += e;
                return false;
            }

            _timer = new Timer { Interval = 1000 * 10 };
            _timer.Tick += CheckMailHandler;
            _timer.Start();
            CheckMailHandler(null, null);
         
            return true;
        }

        protected void CheckMailHandler(object o, EventArgs e)
        {
            _timer.Stop();
                        
            FindItemsResults<Item> findResults;
            SearchFilter sf = new SearchFilter.IsGreaterThan(ItemSchema.DateTimeReceived, _latestMessageDate);
            
            //process the Inbox            
            findResults = _exchangeService.FindItems(WellKnownFolderName.Inbox, sf, new ItemView(10));
            if (findResults.Items.Count > 0)
                ProcessResults(findResults);

            // and process all the other folders too
            foreach (Folder folder in _folders.Folders)
            {
                findResults = _exchangeService.FindItems(folder.Id, sf, new ItemView(10));
                if (findResults.Items.Count > 0)
                    ProcessResults(findResults);
            }
            _timer.Start();
        }

        private void ProcessResults(FindItemsResults<Item> results)
        {
            //_exchangeService.LoadPropertiesForItems(results.Items, _propertySet); //this throws exception
            foreach (Item item in results.Items)
            {
                //item.Load(); //this throws exception
                if (item.DateTimeReceived > _latestMessageDate)
                {
                    _latestMessageDate = item.DateTimeReceived + TimeSpan.FromSeconds(1);
                }
                EmailMessage email = (EmailMessage)item;
                if (email.IsRead) continue;
                string indicator = "";
                if (item.Importance == Importance.High) indicator += "!";
                if (email.ToRecipients.Count == 0 && email.CcRecipients.Count == 0) indicator += ">>";
                SendGrowl(indicator + " " + email.From.Name, item.Subject);                
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);          
            if (WindowState != FormWindowState.Minimized) _mPreviousWindowState = WindowState;            
            notifyIcon1.Visible = (WindowState == FormWindowState.Minimized);
            Visible = !notifyIcon1.Visible;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Visible = true;
            notifyIcon1.Visible = false;
            WindowState = _mPreviousWindowState;
        }
        private void RegisterGrowl()
        {
            application = new Growl.Connector.Application("OWAMonitor");
            growl.Register(application, new[] { notificationType });
        }

        private void SendGrowl(string title, string message)
        {
            CallbackContext callbackContext = new CallbackContext("", "");

            Notification notification = new Notification(application.Name, notificationType.Name, DateTime.Now.Ticks.ToString(), title, message);
            growl.Notify(notification, callbackContext);
        }

        static void GrowlNotificationCallback(Response response, CallbackData callbackData)
        {
            //string text = String.Format("Response Type: {0}\r\nNotification ID: {1}\r\nCallback Data: {2}\r\nCallback Data Type: {3}\r\n", callbackData.Result, callbackData.NotificationID, callbackData.Data, callbackData.Type);
            //MessageBox.Show(text, "Callback received", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            ConfigureExchange();
        }
    }
}
