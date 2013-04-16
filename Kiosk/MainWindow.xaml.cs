using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Net;
using System.ServiceModel;
using Kiosk.KiosksServices;
using System.Linq;

namespace Kiosk
{

    public partial class MainWindow : Window
    {
        #region Constants
       
        //the index of the button needs to equal the index of its corresponding rate in m_rates
        List<Button> rateButtons = new List<Button>();
        private bool m_handicapped;
        private string m_handicappedText;
        private bool monthlyCreEnabled;

        private const StorageStates m_initialStorageState = StorageStates.WaitForReady;
        private const RetrievalStates m_initialRetrievalState = RetrievalStates.Initial;
        #endregion

        #region private variables
        private DispatcherTimer m_timer1;
        private DispatcherTimer m_cardSwipeTimer;
        private DispatcherTimer m_displayTimer;
        private DispatcherTimer m_timeoutTimer;
        private TimeSpan m_displayTimerDefaultInterval;

        private StorageStates m_storageState;
        private RetrievalStates m_retrievalState;
        private MonthlyAccountStates m_accountState;

        private Thread m_logThread;

        private KioskMode m_kioskMode;
        private bool m_monthlyAccountCre;
        private string m_savedCard;
        private List<MonthlyRate> m_rates = new List<MonthlyRate>();
        private List<Border> m_rateBorders = new List<Border>();
        private int chosenMonthlyRateIndex;
        private bool vehicleRetrieved;
        private bool m_createMonthlyOnStart;
        private int offset;
        private string m_sortser;

        private DateTime m_lastKillBitCheck;
        private TimeSpan m_killBitInterval;

        private const string TK_Handi_Dsc = "Accessible Bay";
        private const string TK_Handi_On = TK_Handi_Dsc + " ON ";
        private const string TK_Handi_Off = TK_Handi_Dsc + " OFF";

        private CCManagerConsumer ccManagerConsumer = new CCManagerConsumer();

        #endregion

        #region Constructor

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                ccManagerConsumer.ReadConfiguration();

                ccManagerConsumer.GetMonthlyRates(m_rates);

                DoUILayoutCalculation();

                reset(m_kioskMode);
            }
            catch (Exception ex)
            {
                //m_kioskMainLog.log(LogTools.getExceptionString(Constants.ClassIdentifier, "MainWindow", ex));
            }
        }

        #endregion

        private void DoUILayoutCalculation()
        {
            m_createMonthlyOnStart = false;
            chosenMonthlyRateIndex = -1;
            m_handicapped = false;
            m_handicappedText = TK_Handi_Off;
            m_sortser = "";

            switch (m_rates.Count)
            {
                //will have to add 12 to the margin numbers
                case 1:
                    offset = 396;
                    break;
                case 2:
                    offset = 257;
                    break;
                case 3:
                    offset = 138;
                    break;
                case 4:
                    offset = 12;
                    break;
                default:
                    break;
            }

            rateButtons.Add(rateBtn1);
            rateButtons.Add(rateBtn2);
            rateButtons.Add(rateBtn3);
            rateButtons.Add(rateBtn4);

            m_rateBorders.Add(borderRateBtn1);
            m_rateBorders.Add(borderRateBtn2);
            m_rateBorders.Add(borderRateBtn3);
            m_rateBorders.Add(borderRateBtn4);

            foreach (Button b in rateButtons)
            {
                b.FontSize = 36.0;
                b.Visibility = Visibility.Hidden;
            }

            handicappedBtn.Content = m_handicappedText;
            handicappedBtn.Visibility = Visibility.Hidden;
            handicappedBtn.FontSize = 35.0;
            setButtonImage(BackgroundType.BlueButton, rateBtn2);

            m_timer1 = new DispatcherTimer();
            m_timer1.Tick += new EventHandler(timer1_Tick);
            m_timer1.Interval = new TimeSpan(0, 0, 0, 0, 200); // 200 ms

            m_cardSwipeTimer = new DispatcherTimer();
            m_cardSwipeTimer.Tick += new EventHandler(cardSwipeTimer_Tick);
            m_cardSwipeTimer.Interval = new TimeSpan(0, 0, 0, 0, 200); // 200 ms

            m_displayTimer = new DispatcherTimer();
            m_displayTimer.Tick += new EventHandler(displayTimer_Tick);
            m_displayTimerDefaultInterval = new TimeSpan(0, 0, 0, 10, 0); // 10 s
            m_displayTimer.Interval = m_displayTimerDefaultInterval;

            m_timeoutTimer = new DispatcherTimer();
            m_timeoutTimer.Tick += new EventHandler(timeoutTimer_Tick);
            m_timeoutTimer.Interval = new TimeSpan(0, 0, 2, 0, 0); // 120 s (2 minutes)

            m_storageState = m_initialStorageState;
            m_retrievalState = m_initialRetrievalState;
            m_accountState = MonthlyAccountStates.NotSelected;
            vehicleRetrieved = false;

            m_lastKillBitCheck = DateTime.Now;
            m_killBitInterval = new TimeSpan(0, 0, 0, 2, 0); // 2 s

            switch (Constants.KIOSKID[0])
            {
                case '0':
                    m_kioskMode = KioskMode.Storage;
                    break;
                case '9':
                    m_kioskMode = KioskMode.Retrieval;
                    break;
                default:
                    throw new InvalidOperationException("Invalid kiosk id from command line: " + Constants.KIOSKID);
            }

            //initComm();
        }

       
        private void displayTimer_Tick(object sender, EventArgs e)
        {
            m_displayTimer.Stop();
            m_displayTimer.Interval = m_displayTimerDefaultInterval;

            switch (m_kioskMode)
            {
                case KioskMode.Storage:
                    setState(StorageStates.WaitForReady, "");
                    //sendMsg(KioskMsgType.kiosk_ready, "");
                    break;
                case KioskMode.Retrieval:
                    try
                    {
                        if (m_accountState == MonthlyAccountStates.DlError)
                        {
                            setState(RetrievalStates.SwipeNewMonthlyDL, "");
                            m_monthlyAccountCre = false;
                        }
                        else if (m_accountState == MonthlyAccountStates.MCCError)
                        {
                            setState(RetrievalStates.SwipeNewMonthlyCC, "");
                            m_monthlyAccountCre = false;
                        }
                        else if (m_accountState == MonthlyAccountStates.AccountVerificationFail ||
                                 m_accountState == MonthlyAccountStates.NewCcError)
                        {
                            setState(RetrievalStates.CreateAccountInit, m_savedCard);
                            m_monthlyAccountCre = false;
                        }
                        else if (m_accountState == MonthlyAccountStates.AccountCreFail)
                        {
                            if (!m_createMonthlyOnStart)
                            {
                                setState(RetrievalStates.DisplayDurationCC, m_savedCard);
                                m_monthlyAccountCre = false;
                            }
                            else
                            {
                                reset(m_kioskMode);
                                m_monthlyAccountCre = false;
                            }
                        }
                        else if (m_accountState == MonthlyAccountStates.MaxedMonthlyAccounts)
                        {
                            if (!m_createMonthlyOnStart)
                            {
                                setState(RetrievalStates.DisplayDurationCC, m_savedCard);
                                m_monthlyAccountCre = false;
                                disableContextButton();
                            }
                            else
                            {
                                reset(m_kioskMode);
                                m_monthlyAccountCre = false;
                            }
                        }
                        else
                        {
                            reset(m_kioskMode);
                        }

                    }
                    catch (Exception ex)
                    {
                        //m_kioskMainLog.log(LogTools.getExceptionString("MainWindow.xaml", "displayTimer_Tick", ex, "m_accountState<" + m_accountState.ToString() + ">"));
                        reset(m_kioskMode);
                    }
                    break;
            }
        }
        #region Helper Functions
        
        private char getKioskType()
        {
            switch (m_kioskMode)
            {
                case KioskMode.Retrieval:
                    return 'R';
                case KioskMode.Storage:
                    return 'S';
                default:
                    throw new InvalidOperationException();
            }
        }

        
        private string formatSize(string size)
        {
            try
            {
                return Double.Parse(size).ToString("F1") + "\"";
            }
            catch (Exception)
            {
                return "";
            }
        }
        #endregion

        #region GUI Interface Functions

        public void disableRateButtons()
        {
            foreach (Button b in rateButtons)
            {
                b.Visibility = Visibility.Hidden;
            }

            borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
            borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);

            if (isContextEnabled())
            {
                if (areOkCancelEnabled())
                {
                    if (ishandicappedEnabled())
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc3);
                    }
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }
                else
                {
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                    if (ishandicappedEnabled())
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                    }
                }
            }
        }

        public bool isContextEnabled()
        {
            if (btnContext.Visibility == Visibility.Visible)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool areOkCancelEnabled()
        {
            if (btnOK.IsEnabled && btnCancel.IsEnabled)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        
        private bool waitingForCardSwipe()
        {
            switch (m_kioskMode)
            {
                case KioskMode.Storage:
                    return (m_storageState == StorageStates.WaitForCard);
                case KioskMode.Retrieval:
                    return ((m_retrievalState == RetrievalStates.Initial) || (m_retrievalState == RetrievalStates.SwipeNewMonthlyDL)
                            || (m_retrievalState == RetrievalStates.AlternateCardForMonthly) || (m_retrievalState == RetrievalStates.GetNewCC) || (m_retrievalState == RetrievalStates.SwipeNewMonthlyCC)); //todo add other states
                default:
                    return false;
            }
        }


        
        public void setMsg(string msg)
        {
            setMsg(msg, false);
        }

        
        public void setMsg(string msg, bool error)
        {
            if (error)
            {
                tbMsg.Foreground = Constants.m_errorMsgForeground;
                border1.BorderThickness = new Thickness(4);
                border1.BorderBrush = Constants.m_errorMsgBorder;
            }
            else
            {
                tbMsg.Foreground = Constants.m_normalMsgForeground;
                border1.BorderThickness = new Thickness(3);
                border1.BorderBrush = Constants.m_normalMsgBorder;
            }

            double lineHeight = tbMsg.FontSize + 8;
            int lines = msg.Length - msg.Replace("\n", "").Length + 1;

            border1.Height = (lineHeight * lines) + tbMsg.Padding.Top + tbMsg.Padding.Bottom + 8;
            tbMsg.Height = border1.Height;

            tbMsg.Text = msg;

        }

        
        public void setInfo(string msg)
        {
            tbInfo.Text = msg;
            if (msg.Length == 0)
            {
                infoBorder.Visibility = Visibility.Hidden;
            }
            else
            {
                infoBorder.Visibility = Visibility.Visible;
            }
        }

        
        public void appendInfo(string msg)
        {
            tbInfo.Text = tbInfo.Text + "\n" + msg;
            infoBorder.Visibility = Visibility.Visible;
        }

        
        public void lineDisplay(string msg)
        {
            // completely disable showing line display on kiosk
            disableLineDisplay();
            //label1.Content = msg;
            //if (msg.Trim().Length == 0)
            //{
            //    border2.Visibility = Visibility.Hidden;
            //}
            //else
            //{
            //    border2.Visibility = Visibility.Visible;
            //}
        }

        
        public void disableLineDisplay()
        {
            label1.Content = "";
            border2.Visibility = Visibility.Hidden;
        }

        
        public void enableContextButton(string content)
        {
            enableContextButton(content, 48);
        }
        public void enableContextButton(string content, int font)
        {
            btnContext.FontSize = font;
            btnContext.Visibility = Visibility.Hidden;
            if (isRateButtonEnabled())
            {
                if (areOkCancelEnabled())
                {
                    borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                    borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }
                else
                {
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                }
            }
            else if (areOkCancelEnabled())
            {
                borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
            }
            else
            {
                borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
            }

            btnContext.Content = content;
            btnContext.Visibility = Visibility.Visible;
        }

        
        public void disableContextButton()
        {
            if (isRateButtonEnabled())
            {
                if (areOkCancelEnabled())
                {
                    borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                    borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                    if (ishandicappedEnabled())
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                    }
                }
                else
                {
                    if (ishandicappedEnabled())
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                    }
                }
            }
            else if (areOkCancelEnabled())
            {
                borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                if (ishandicappedEnabled())
                {
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }
            }
            else
            {
                if (ishandicappedEnabled())
                {
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                }
            }
            btnContext.Visibility = Visibility.Hidden;
        }


        public bool isRateButtonEnabled()
        {
            if (rateBtn1.Visibility == Visibility.Visible || rateBtn2.Visibility == Visibility.Visible ||
                rateBtn3.Visibility == Visibility.Visible || rateBtn4.Visibility == Visibility.Visible)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ishandicappedEnabled()
        {
            if (handicappedBtn.Visibility == Visibility.Visible)
            {
                return true;
            }
            return false;
        }

        public void enableAllRateBtns()
        {
            if (areOkCancelEnabled())
            {
                if (isContextEnabled())
                {
                    borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                    borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                    if (ishandicappedEnabled())
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc3);
                    }
                }
                else
                {
                    borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                    borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }
            }

            for (int i = 0; i < m_rates.Count; i++)
            {
                if (rateButtons.Count > 0)
                {
                    m_rateBorders[i].Margin = new Thickness(offset + 258 * i, 0, 0, Constants.btnLoc0);
                    string content = m_rates[i].Duration + " Months\n";
                    content += "$" + m_rates[i].Rate + "/month";
                    rateButtons[i].Content = content;
                    rateButtons[i].Visibility = Visibility.Visible;
                }

                if (rateButtons.Count == i + 1)
                {
                    break;
                }
            }
        }

        public void enableHandicappedBtn(string text)
        {
            handicappedBtn.Content = text;
            if (isRateButtonEnabled())
            {
                if (areOkCancelEnabled())
                {
                    if (isContextEnabled())
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc3);
                    }
                    else
                    {
                        borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                    }
                }
                else
                {

                }
            }
            else if (areOkCancelEnabled())
            {

                if (isContextEnabled())
                {
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc3);
                }
                else
                {
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }
            }
            else if (isContextEnabled())
            {
                borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
            }
            else
            {
                borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2); //dirty
            }
            handicappedBtn.Visibility = Visibility.Visible;
        }

        public void disableHandicappedBtn()
        {
            handicappedBtn.Visibility = Visibility.Hidden;
        }

        // ---------------------------------------------------------------------------------------
        // enableOkCancel: makes the OK and Cancel buttons visible
        // ---------------------------------------------------------------------------------------
        public void enableOkCancel()
        {
            enableOkCancel("OK", "Cancel");
        }

        public void enableOkCancel(string okText, string cancelText)
        {
            btnCancel.Content = cancelText;
            enableOkCancel(okText);
        }

        // ---------------------------------------------------------------------------------------
        // enableOkCancel: makes the OK and Cancel buttons visible and sets the text for the OK
        //      button
        // ---------------------------------------------------------------------------------------
        public void enableOkCancel(string okText)
        {
            if (isRateButtonEnabled())
            {
                if (isContextEnabled())
                {
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                    borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                    borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc3);
                }
                else
                {
                    borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                    borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }
            }
            else if (isContextEnabled())
            {
                borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc3);
            }
            else
            {
                borderBtnOK.Margin = new Thickness(0, 0, 500, Constants.btnLoc1);
                borderBtnCancel.Margin = new Thickness(500, 0, 0, Constants.btnLoc1);
                borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
            }

            btnOK.Content = okText;
            btnOK.Visibility = Visibility.Visible;
            btnCancel.Visibility = Visibility.Visible;
        }

        // ---------------------------------------------------------------------------------------
        // disableOkCancel: hides the OK and Cancel buttons
        // ---------------------------------------------------------------------------------------
        public void disableOKCancel()
        {
            btnOK.Visibility = Visibility.Hidden;
            btnCancel.Visibility = Visibility.Hidden;

            if (isRateButtonEnabled())
            {
                if (isContextEnabled())
                {
                    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                    borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
                }

                //foreach (Button b in rateButtons)
                //{
                //    borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                //}

            }
            else if (isContextEnabled())
            {
                btnContext.Visibility = Visibility.Hidden;
                borderBtnContext.Margin = new Thickness(0, 0, 0, Constants.btnLoc1);
                btnContext.Visibility = Visibility.Visible;
                borderHandicapped.Margin = new Thickness(0, 0, 0, Constants.btnLoc2);
            }
            else
            {

            }
        }

        // ---------------------------------------------------------------------------------------
        // setBackground: sets the background picture
        // ---------------------------------------------------------------------------------------
        public void setBackground(BackgroundType type)
        {
            switch (type)
            {
                case BackgroundType.Active:
                    image1.Visibility = Visibility.Visible;
                    tbSwipe.Visibility = Visibility.Visible;
                    tbSwipe.Focus();
                    mainGrid.Background = Constants.m_activeBackground;
                    break;
                case BackgroundType.Inactive:
                    image1.Visibility = Visibility.Visible;
                    tbSwipe.Visibility = Visibility.Hidden;
                    mainGrid.Background = Constants.m_inactiveBackground;
                    break;
                default:
                    image1.Visibility = Visibility.Visible;
                    tbSwipe.Visibility = Visibility.Visible;
                    tbSwipe.Focus();
                    mainGrid.Background = Constants.m_activeBackground;
                    break;
            }
        }

        public void setButtonImage(BackgroundType type, Button b)
        {
            switch (type)
            {
                case BackgroundType.BlueButton:
                    b.Background = Constants.m_blueButtonBackground;
                    break;
                case BackgroundType.GreenButton:
                    b.Background = Constants.m_greenButtonBackground;
                    break;
                default:
                    b.Background = Constants.m_blueButtonBackground;
                    break;
            }

        }

        #endregion

        // GUI Event Handlers
        // =======================================================================================
        #region GUI Event Handlers
        // ---------------------------------------------------------------------------------------
        // Window_Loaded:
        // ---------------------------------------------------------------------------------------
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_timer1.Start();

            tbSwipe.Focus();

        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //m_logThread.Join(5000);

            base.OnClosing(e);
        }

        #region Timers
        // ---------------------------------------------------------------------------------------
        // timer1_Tick: This is effectivly the main loop for the Kiosk.  Using the timer to run in
        //      the GUI thread to avoid dispatcher issues.
        // ---------------------------------------------------------------------------------------
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                m_timer1.Stop();

                //checkKillSwitch();

                switch (m_kioskMode)
                {
                    case KioskMode.Storage:
                        ccManagerConsumer.GetBayReadyStatsuFoSrtorage();
                        //getMessages();
                        break;
                    case KioskMode.Retrieval:
                        ccManagerConsumer.GetBayReadyStatsuFoRetrieval();
                        //getMessages();
                        break;
                    default:
                        throw new NotImplementedException();
                }

                m_timer1.Start();
            }
            catch (Exception ex)
            {
                //m_kioskMainLog.log(LogTools.getExceptionString(Constants.ClassIdentifier, "timer1_Tick", ex));
            }
        }

        private void checkKillSwitch()
        {
            try
            {
                if (DateTime.Now < (m_lastKillBitCheck + m_killBitInterval))
                {
                    return;
                }
                else
                {
                    m_lastKillBitCheck = DateTime.Now;
                }

                using (SqlConnection conn = new SqlConnection(App.viadatConnString))
                {
                    conn.Open();
                    SqlCommand selKillSwitch = new SqlCommand("SELECT bitvalue FROM bits WHERE bitid = 'O_KILL_KIOSKS'", conn);
                    string result = selKillSwitch.ExecuteScalar().ToString();
                    if (result.Equals("0"))
                    {
                        // normal case, do nothing
                    }
                    else if (result.Equals("1"))
                    {
                        // shutdown
                        //m_kioskMainLog.log(LogTools.getStatusString(Constants.ClassIdentifier, "checkKillSwitch", "shut down kiosk due to DB killswitch"));
                        this.Close();
                    }
                    else
                    {
                        //something bad happened log and ignore
                        //m_kioskMainLog.log(LogTools.getErrorString(Constants.ClassIdentifier, "checkKillSwitch", "unexpected value <" + result + ">"));
                    }
                }
            }
            catch (Exception e)
            {
                // do nothing except log if exception thrown
                //m_kioskMainLog.log(LogTools.getExceptionString(Constants.ClassIdentifier, "checkKillSwitch", e));

            }
        }

        // ---------------------------------------------------------------------------------------
        // cardSwipeTimer_Tick:
        // ---------------------------------------------------------------------------------------
        private void cardSwipeTimer_Tick(object sender, EventArgs e)
        {
            m_cardSwipeTimer.Stop();
            if (waitingForCardSwipe())
            {
                switch (m_kioskMode)
                {
                    case KioskMode.Storage:
                        switch (m_storageState)
                        {
                            case StorageStates.WaitForCard:
                                ////sendMsg(KioskMsgType.card_string, tbSwipe.Text);
                                ccManagerConsumer.DoParkingTransaction(KioskMsgType.card_string, tbSwipe.Text);
                                setState(StorageStates.WaitForSize, "");

                                break;
                        }
                        break;
                    case KioskMode.Retrieval:
                        switch (m_retrievalState)
                        {
                            case RetrievalStates.Initial:
                                ////sendMsg(KioskMsgType.card_string, tbSwipe.Text);
                                setState(RetrievalStates.WaitForDuration, "");
                                break;
                            case RetrievalStates.SwipeNewMonthlyDL:
                                string info = tbSwipe.Text;
                                if (m_createMonthlyOnStart)
                                {
                                    info += "__#TRUE";
                                }
                                ////sendMsg(KioskMsgType.dl_m_cardswipe, info);
                                setState(RetrievalStates.WaitForDlAuth, "Verifying license..");
                                break;
                            case RetrievalStates.AlternateCardForMonthly:
                                ////sendMsg(KioskMsgType.use_dif_CC, tbSwipe.Text);
                                setState(RetrievalStates.VerifyAlternateMonthlyCC, "Verifying new credit card..");
                                break;
                            case RetrievalStates.SwipeNewMonthlyCC:
                                ////sendMsg(KioskMsgType.monthlyCC, tbSwipe.Text);
                                setState(RetrievalStates.SwipeNewMonthlyDL, "");
                                break;
                            case RetrievalStates.GetNewCC:
                                ////sendMsg(KioskMsgType.alt_card_string, tbSwipe.Text);
                                setState(RetrievalStates.WaitForDuration, "");
                                break;
                        }
                        break;
                }
            }
            tbSwipe.Text = "";
        }

    
        private void timeoutTimer_Tick(object sender, EventArgs e)
        {
            m_timeoutTimer.Stop();
            switch (m_kioskMode)
            {
                case KioskMode.Storage:
                    setState(StorageStates.DisplayError, "Lost connection to server");
                    break;
                case KioskMode.Retrieval:
                    setState(RetrievalStates.DisplayError, "Lost connection to server");
                    break;
            }
        }
        #endregion

        #region Buttons
        // ---------------------------------------------------------------------------------------
        // btnOK_Click
        // ---------------------------------------------------------------------------------------
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            switch (m_kioskMode)
            {
                #region case KioskMode.Storage:
                case KioskMode.Storage:
                    if (m_storageState == StorageStates.WaitForConfirm)
                    {
                        setState(StorageStates.Processing, "");
                        //setState(StorageStates.InboundAccepted, "");
                        //sendMsg(KioskMsgType.user_confirm, "");
                    }
                    break;
                #endregion
                #region case KioskMode.Retrieval:
                case KioskMode.Retrieval:
                    switch (m_retrievalState)
                    {
                        case RetrievalStates.DisplayDurationCC:
                            //sendMsg(KioskMsgType.user_confirm, m_handicapped.ToString());
                            setState(RetrievalStates.WaitForAuth, "");
                            break;
                        case RetrievalStates.DisplayDurationDL:
                            //sendMsg(KioskMsgType.user_confirm, m_handicapped.ToString());
                            setState(RetrievalStates.WaitForAuth, "");
                            break;
                        case RetrievalStates.CreateAccountInit:
                            //TODO: monthly info needs to be tested
                            if (chosenMonthlyRateIndex == -1)
                            {
                                m_accountState = MonthlyAccountStates.AccountVerificationFail;
                                setState(RetrievalStates.DisplayError, "ERROR: No monthly account was selected");
                            }
                            else
                            {
                                //sendMsg(KioskMsgType.monthly_info, m_rates[chosenMonthlyRateIndex].duration + "" + Constants.dataDelimiters[0] + m_handicapped.ToString());
                                setState(RetrievalStates.AccountVerify, "");
                            }
                            break;
                        case RetrievalStates.ConfirmMonthlyCreation:
                            //sendMsg(KioskMsgType.m_user_confirm, "");
                            setState(RetrievalStates.Processing, "");
                            break;
                        case RetrievalStates.OutboundAccepted:
                            ReceiptPrinter printer = new ReceiptPrinter(null);
                            if (printer.printReceipt(m_sortser))
                            {
                                reset(m_kioskMode);
                            }
                            else
                            {
                                setState(RetrievalStates.DisplayError, "Error printing receipt.\nPlease see attendant if\nyou need a receipt.");
                            }
                            break;
                    }
                    break;
                #endregion
                default:
                    throw new InvalidOperationException();
            }
        }

        // ---------------------------------------------------------------------------------------
        // btnCancel_Click
        // ---------------------------------------------------------------------------------------
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            switch (m_kioskMode)
            {
                case KioskMode.Retrieval:
                    if (m_accountState != MonthlyAccountStates.NotSelected && !m_createMonthlyOnStart)
                    {
                        m_accountState = MonthlyAccountStates.NotSelected;
                        //sendMsg(KioskMsgType.m_user_cancel, "");
                    }
                    else if (m_createMonthlyOnStart)
                    {
                        reset(m_kioskMode);
                        //sendMsg(KioskMsgType.user_cancel, "");
                    }
                    else
                    {
                        reset(m_kioskMode);
                    }
                    break;
                default:
                    reset(m_kioskMode);
                    break;
            }
        }

        // ---------------------------------------------------------------------------------------
        // btnContext_Click: onClick handler for context sensitive button
        // ---------------------------------------------------------------------------------------
        private void btnContext_Click(object sender, RoutedEventArgs e)
        {
            //resetMousePosition();
            switch (m_kioskMode)
            {
                #region case KioskMode.Storage:
                case KioskMode.Storage:
                    if (m_storageState == StorageStates.WaitForSize || m_storageState == StorageStates.WaitForLocation || m_storageState == StorageStates.WaitForAuth)
                    {
                        btnCancel_Click(sender, e);
                    }
                    break;
                #endregion
                #region case KioskMode.Retrieval:
                case KioskMode.Retrieval:
                    switch (m_retrievalState)
                    {
                        case RetrievalStates.Initial:
                            setState(RetrievalStates.SwipeNewMonthlyCC, "");
                            m_createMonthlyOnStart = true;
                            break;
                        case RetrievalStates.SwipeNewMonthlyCC:
                            setState(RetrievalStates.Initial, "");
                            //sendMsg(KioskMsgType.user_cancel, "");
                            break;
                        case RetrievalStates.WaitForDuration:
                            setState(RetrievalStates.Initial, "");
                            //sendMsg(KioskMsgType.user_cancel, "");
                            break;
                        case RetrievalStates.DisplayDurationCC:
                            setState(RetrievalStates.SwipeNewMonthlyDL, "");
                            break;
                        case RetrievalStates.SwipeNewMonthlyDL:
                            if (!m_createMonthlyOnStart)
                            {
                                //sendMsg(KioskMsgType.m_user_cancel, "");
                            }
                            else
                            {
                                //sendMsg(KioskMsgType.user_cancel, "");
                                setState(RetrievalStates.Initial, "");
                            }
                            break;
                        case RetrievalStates.AlternateCardForMonthly:
                            setState(RetrievalStates.CreateAccountInit, m_savedCard);
                            break;
                        case RetrievalStates.CreateAccountInit:
                            setState(RetrievalStates.AlternateCardForMonthly, "");
                            break;
                        case RetrievalStates.ConfirmMonthlyCreation:
                            setState(RetrievalStates.CreateAccountInit, "");
                            break;
                        case RetrievalStates.GetNewCC:
                            btnCancel_Click(sender, e);
                            break;
                        default:
                            break;
                    }
                    break;
                #endregion
                default:
                    throw new InvalidOperationException();
            }
        }

        // TODO: cleanup from here

        private void rateBtn1_Click(object sender, RoutedEventArgs e)
        {
            //resetMousePosition();
            chosenMonthlyRateIndex = rateButtons.IndexOf(rateBtn1);
            foreach (Button b in rateButtons)
            {
                setButtonImage(BackgroundType.BlueButton, b);
            }
            setButtonImage(BackgroundType.GreenButton, rateBtn1);
        }

        private void rateBtn2_Click(object sender, RoutedEventArgs e)
        {
            //resetMousePosition();
            chosenMonthlyRateIndex = rateButtons.IndexOf(rateBtn2);
            foreach (Button b in rateButtons)
            {
                setButtonImage(BackgroundType.BlueButton, b);
            }
            setButtonImage(BackgroundType.GreenButton, rateBtn2);
        }

        private void rateBtn3_Click(object sender, RoutedEventArgs e)
        {
            //resetMousePosition();
            chosenMonthlyRateIndex = rateButtons.IndexOf(rateBtn3);
            foreach (Button b in rateButtons)
            {
                setButtonImage(BackgroundType.BlueButton, b);
            }
            setButtonImage(BackgroundType.GreenButton, rateBtn3);
        }

        private void rateBtn4_Click(object sender, RoutedEventArgs e)
        {
            //resetMousePosition();
            chosenMonthlyRateIndex = rateButtons.IndexOf(rateBtn4);
            foreach (Button b in rateButtons)
            {
                setButtonImage(BackgroundType.BlueButton, b);
            }
            setButtonImage(BackgroundType.GreenButton, rateBtn4);
        }

        private void handicappedBtn_Click(object sender, RoutedEventArgs e)
        {
            //resetMousePosition();
            m_handicapped = !m_handicapped;
            if (m_handicapped)
            {
                setButtonImage(BackgroundType.GreenButton, handicappedBtn);
                m_handicappedText = TK_Handi_On;
                enableHandicappedBtn(m_handicappedText);

            }
            else
            {
                setButtonImage(BackgroundType.BlueButton, handicappedBtn);
                m_handicappedText = TK_Handi_Off;
                enableHandicappedBtn(m_handicappedText);
            }
        }

        #endregion

        //TODO: remove in final version, for testing only
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
           
        }

        private void resetMousePosition()
        {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(4, 4);
        }
        
        private void tbSwipe_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (waitingForCardSwipe() && tbSwipe.Text.Length > 0)
            {
                m_cardSwipeTimer.Stop();
                m_cardSwipeTimer.Start();
            }
            else
            {
                tbSwipe.Text = "";
            }
        }

        
        private void tbSwipe_LostFocus(object sender, RoutedEventArgs e)
        {
            tbSwipe.Focus();
        }
        #endregion


        #region State Management
        // ---------------------------------------------------------------------------------------
        // reset: resets kiosk to initial state
        // ---------------------------------------------------------------------------------------
        private void reset(KioskMode kioskMode)
        {
            m_kioskMode = kioskMode;
            Title = "Kiosk " + Constants.KIOSKID;
            disableOKCancel();
            disableLineDisplay();
            disableContextButton();
            m_monthlyAccountCre = false;
            m_savedCard = null;
            vehicleRetrieved = false;
            chosenMonthlyRateIndex = -1;
            m_handicapped = false;
            m_handicappedText = TK_Handi_Off;
            m_sortser = "";
            resetMousePosition();

            foreach (Button b in rateButtons)
            {
                setButtonImage(BackgroundType.BlueButton, b);
            }
            setBackground(BackgroundType.Active);

            switch (kioskMode)
            {
                case KioskMode.Storage:
                    setState(StorageStates.WaitForReady, "");
                    //sendMsg(KioskMsgType.user_cancel, "");
                    break;
                case KioskMode.Retrieval:
                    setState(RetrievalStates.Initial, "");
                    //sendMsg(KioskMsgType.user_cancel, "");
                    break;
            }
        }

        // ---------------------------------------------------------------------------------------
        // setState: changes to the given storage state.  Handles all UI changes (enable/disable
        //      components, set text for messages, etc) for the new state
        // ---------------------------------------------------------------------------------------
        private void setState(StorageStates state, string msgData)
        {
            m_timeoutTimer.Stop();
            m_displayTimer.Stop();
            m_retrievalState = m_initialRetrievalState;
            m_storageState = state;
            setBackground(BackgroundType.Active);

            Title = "Kiosk " + Constants.KIOSKID + " <" + state.ToString() + ">";

            switch (state)
            {
                case StorageStates.DisplayError:
                    setMsg(msgData, true);
                    // leave info as is
                    disableContextButton();
                    disableOKCancel();
                    m_displayTimer.Start();
                    //debug
                    //if (SENDTOSIM)
                    //{
                        //sendMsg(KioskMsgType.ERROR_sim, "", true);
                    //}
                    break;
                case StorageStates.InboundAccepted:
                    setMsg("Your vehicle will now be stored\nThank you");
                    // leave info as is
                    disableContextButton();
                    disableOKCancel();
                    m_displayTimer.Start();
                    break;
                case StorageStates.WaitForAuth:
                    {
                        setMsg("Authorizing Card\nPlease wait..");
                        // keep info as is
                        enableContextButton("Cancel");
                        disableOKCancel();
                        m_timeoutTimer.Start();
                        break;
                    }
                case StorageStates.WaitForCard:
                    setMsg("Please swipe card to start");
                    setInfo("Monthly Users:\n\u2022Swipe your driver's license,\nUCLA card, or UCLA RFID\n\nHourly Users:\n\u2022Swipe your credit card\n\u2022The card you swipe will be required to retrieve your vehicle\n\u2022Your card will not be charged until you retrieve your vehicle");
                    disableContextButton();
                    disableOKCancel();
                    //debug
                    //if (SENDTOSIM)
                    //{
                        //sendMsg(KioskMsgType.wait_on_card_string, "", true);
                    //}
                    break;
                case StorageStates.WaitForConfirm:
                    {
                        string[] data = msgData.Split(Constants.dataDelimiters);
                        string infoString = "";
                        if (data.Length < 2 || data[0].Length != 4)
                        {
                            // invalid data, skip info msg creation
                        }
                        else
                        {
                            infoString = "\nLast 4 of Card#: " + data[0] + "\nUser Type: " + data[1]; // first \n is for extra newline in addtion to the one added by appendInfo
                        }

                        setMsg("Ready to store vehicle\n\nBy pressing START you confirm that\neveryone is out of the vehicle", false);
                        //debug
                        //if (SENDTOSIM)
                        //{
                            //sendMsg(KioskMsgType.wait_for_confirm, "", true);
                        //}
                        appendInfo(infoString);
                        disableContextButton();
                        enableOkCancel("START");
                        break;
                    }
                case StorageStates.Processing:
                    {
                        setMsg("Processing");
                        setInfo("");
                        disableContextButton();
                        disableOKCancel();
                        m_timeoutTimer.Start();
                    }
                    break;
                case StorageStates.WaitForLocation:
                    {
                        string infoString = "";
                        string[] data = msgData.Split(Constants.dataDelimiters);
                        if (data.Length < 4)
                        {
                            // invalid data, skip info msg creation
                        }
                        else
                        {
                            int geocl;
                            if (!(Int32.TryParse(data[3], out geocl) && geocl >= 0 && geocl < Constants.geocl2str.Length))
                            {
                                geocl = 0; // "UNKNOWN"
                            }
                            for (int i = 0; i < 3; i++) // first 3 are height, length, width
                            {
                                data[i] = "000" + data[i];
                                data[i] = data[i].Substring(0, data[i].Length - 3) + "." + data[i].Substring(data[i].Length - 3, 3);
                                data[i].TrimStart(Constants.zero);
                                if (data[i][0] == '.')
                                {
                                    data[i] = "0" + data[i];
                                }
                            }
                            infoString = "Your vehicle:\nHeight: " + formatSize(data[0]) + "\nLength: "
                                + formatSize(data[1]) + "\nWidth: " + formatSize(data[2]) + "\nType: " + Constants.geocl2str[geocl];

                        }
                        setMsg("Finding storage location\nfor your vehicle\nplease wait..", false);
                        setInfo(infoString);
                        enableContextButton("Cancel");
                        disableOKCancel();
                        m_timeoutTimer.Start();
                        break;
                    }
                case StorageStates.WaitForReady:
                    setMsg("Kiosk Disabled");
                    setInfo("");
                    disableContextButton();
                    disableOKCancel();
                    setBackground(BackgroundType.Inactive);
                    break;
                case StorageStates.WaitForSize:
                    setMsg("Sizing vehicle\nPlease wait..");
                    setInfo("");
                    enableContextButton("Cancel");
                    disableOKCancel();
                    m_timeoutTimer.Start();
                    break;
                default:
                    throw new InvalidOperationException();

            }
        }

        // ---------------------------------------------------------------------------------------
        // setState: changes to the given retrieval state.  Handles all UI changes (enable/disable
        //      components, set text for messages, etc) for the new state
        // ---------------------------------------------------------------------------------------
        private void setState(RetrievalStates state, string msgData)
        {
            m_displayTimer.Stop();
            m_timeoutTimer.Stop();
            m_storageState = m_initialStorageState;
            m_retrievalState = state;
            Title = "Kiosk " + Constants.KIOSKID + " <" + state.ToString() + ">";
            disableLineDisplay();
            switch (state)
            {
                case RetrievalStates.Initial:
                    m_handicapped = false;
                    setButtonImage(BackgroundType.BlueButton, handicappedBtn);
                    setMsg("Swipe card to retrieve vehicle");
                    setInfo("To retrieve your car, please swipe the same card you used to store the car\n\nOr, you may create a monthly account by pushing the \"Create Monthly Account\" button");
                    //enableContextButton("Create Monthly Account");
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    m_accountState = MonthlyAccountStates.NotSelected;
                    enableContextButton("Create Monthly Account", 40);
                    m_createMonthlyOnStart = false;
                    //rateButtons[0].Visibility = Visibility.Hidden;
                    //debug
                    //if (SENDTOSIM)
                    //{
                        //sendMsg(KioskMsgType.wait_on_card_string, "", true);
                    //}
                    break;
                case RetrievalStates.SwipeNewMonthlyCC:
                    setMsg("Please swipe a credit card");
                    //TODO: create a better info message
                    setInfo("This credit card will be charged for\n" +
                             "the recurring monthly payments.\n" +
                             "If you already have a\n" +
                             "car stored in the garage,\n" +
                             "don't make a monthly account\n" +
                             "You will have the option to\n" +
                             "create an account as you\n" +
                             "retrieve your vehicle");

                    enableContextButton("Cancel");
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    m_accountState = MonthlyAccountStates.MCCError;
                    break;
                case RetrievalStates.WaitForDuration:
                    setMsg("Finding vehicle..");
                    setInfo("");
                    enableContextButton("Cancel");
                    disableOKCancel();
                    disableRateButtons();
                    break;
                case RetrievalStates.GetNewCC:
                    setMsg("Your card could not be charged.\nPlease swipe a new credit card for this\ntransaction.");
                    setInfo("");
                    enableContextButton("Cancel");
                    disableOKCancel();
                    disableRateButtons();
                    break;
                case RetrievalStates.DisplayDurationCC:
                    {
                        m_savedCard = msgData;
                        m_accountState = MonthlyAccountStates.NotSelected;
                        string[] data = msgData.Split(Constants.dataDelimiters);
                        TimeSpan duration;
                        if (data.Length < 3 || !TimeSpan.TryParse(data[0], out duration))
                        {
                            setState(RetrievalStates.DisplayError, "No valid duration found\nPlease see attendant");
                            break;
                        }
                        else
                        {

                            int minutes = duration.Minutes + Math.Sign(duration.Seconds);
                            int hours = duration.Hours;
                            int days = (int)duration.TotalDays;
                            if (minutes == 60)
                            {
                                ++hours;
                                minutes = 0;
                            }
                            if (hours == 24)
                            {
                                ++days;
                                hours = 0;
                            }

                            string durationString = "Your car has been parked for:\n";
                            // days
                            if (days == 1)
                            {
                                durationString += days.ToString() + " Day";
                            }
                            else if (days > 1)
                            {
                                durationString += days.ToString() + " Days";
                            }
                            // hours
                            if (hours == 1)
                            {
                                if (days >= 1)
                                {
                                    durationString += ", ";
                                }
                                durationString += hours.ToString() + " Hour";
                            }
                            else if (hours > 1 || days >= 1) // show 0 hours when showing days
                            {
                                if (days >= 1)
                                {
                                    durationString += ", ";
                                }
                                durationString += hours.ToString() + " Hours";
                            }
                            // minutes
                            if (minutes == 1)
                            {
                                if (hours > 0 || days > 0)
                                {
                                    durationString += ", ";
                                }
                                durationString += minutes.ToString() + " Minute";
                            }
                            else if (minutes > 1 || hours >= 1)
                            {
                                if (hours > 0 || days > 0)
                                {
                                    durationString += ", ";
                                }
                                durationString += minutes.ToString() + " Minutes";
                            }

                            string cost = "Total: $" + data[1];

                            setMsg(durationString + "\n\n" + cost + "\n\nThis will be charged to your\ncard ending in: " + data[2]);
                            setInfo("Press OK to charge your card and retrieve your car\n\nNo charges will be made until you press OK");
                            enableOkCancel();
                            //max number of accounts already reached
                            if (data[3].ToLower().Contains("t") || !monthlyCreEnabled)
                            {
                                disableContextButton();
                            }
                            else
                            {
                                enableContextButton("Create Monthly Account", 40);
                            }
                            disableRateButtons();
                            enableHandicappedBtn(m_handicappedText);
                            //debug
                            //if (SENDTOSIM)
                            //{
                                //sendMsg(KioskMsgType.wait_for_confirm, "", true);
                                Title = "Kiosk " + Constants.KIOSKID + " <" + state.ToString() + "> <wait for confirm sent>";
                            //}
                        }
                        break;
                    }
                case RetrievalStates.DisplayDurationDL:
                    {
                        string[] data = msgData.Split(Constants.dataDelimiters);
                        TimeSpan duration;
                        if (data.Length < 1 || !TimeSpan.TryParse(data[0], out duration))
                        {
                            // error
                        }
                        else
                        {
                            string days = ((int)duration.TotalDays).ToString();
                            string hours = duration.Hours.ToString();
                            string minutes = (duration.Minutes + Math.Sign(duration.Seconds)).ToString(); //round any seconds up to the next minute
                            string durationString = "Your car has been parked for:\n" + days + " Days\n" + hours + " Hours\n" + minutes + " Minutes";

                            setMsg(durationString);
                            setInfo("Press OK to retrieve your car");
                            disableContextButton();
                            enableOkCancel();
                            disableRateButtons();
                            if (data.Length > 1 && data[1].ToLower().Equals("true"))
                            {
                                handicappedBtn.Content = TK_Handi_On;
                                setButtonImage(BackgroundType.GreenButton, handicappedBtn);
                            }
                            enableHandicappedBtn(m_handicappedText);
                        }
                    }
                    break;
                case RetrievalStates.SwipeNewMonthlyDL:
                    setMsg("Please swipe your California driver's\n license");
                    setInfo("Your monthly account will be associated with this card.");
                    enableContextButton("Cancel");
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    m_accountState = MonthlyAccountStates.SwipeDl;
                    m_monthlyAccountCre = true;
                    break;
                case RetrievalStates.WaitForDlAuth:
                    setMsg("Verifying license..");
                    setInfo("");
                    disableContextButton();
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    break;
                case RetrievalStates.CreateAccountInit:
                    m_accountState = MonthlyAccountStates.CustomizeAccount;
                    setMsg("Configure Your Account: ");
                    disableContextButton();
                    disableOKCancel();
                    enableAllRateBtns();
                    //TODO: Display rates.  Create GUI.
                    setInfo("1) Choose your account duration." +
                            "\n2) Select a credit card. This" +
                            "\n    defaults to the card with" +
                            "\n    which you originally stored" +
                            "\n    your vehicle, if you didn't." +
                            "\n    start the account creation" +
                            "\n    from the initial retrieve" +
                            "\n    kiosk screen. " +
                            "\n3) Select whether or not you" +
                            "\n    want " + TK_Handi_Dsc + ".");
                    enableContextButton("Input Credit Card");
                    enableOkCancel();
                    enableHandicappedBtn(m_handicappedText);
                    break;
                case RetrievalStates.AlternateCardForMonthly:
                    setMsg("Please swipe a credit card");
                    //TODO: create a better info message
                    setInfo("This credit card will be charged for\nthe recurring monthly payments.");
                    enableContextButton("Cancel");
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    break;
                case RetrievalStates.VerifyAlternateMonthlyCC:
                    setMsg("Verifying new credit card..");
                    disableContextButton();
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    break;
                case RetrievalStates.WaitForAuth:
                    setMsg("Authorizing card\nplease wait..");
                    setInfo("");
                    disableContextButton();
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    break;
                case RetrievalStates.AccountVerify:
                    setMsg("Verifying account details");
                    setInfo("");
                    disableContextButton();
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    break;
                case RetrievalStates.ConfirmMonthlyCreation:
                    //firstname, last4digits, duration, monthlyrate, total cost, disabled parking T or F
                    string[] accountInfo = msgData.Split(Constants.dataDelimiters, StringSplitOptions.RemoveEmptyEntries);
                    string verifyMsg = "Create Account?";
                    verifyMsg += "\n\nName: " + accountInfo[0];
                    verifyMsg += "\nCredit Card last 4: " + accountInfo[1];
                    verifyMsg += "\nAccount Duration: " + accountInfo[2] + " months";
                    verifyMsg += "\nMonthly Rate: $" + accountInfo[3];
                    verifyMsg += "\n" + TK_Handi_Dsc + ": ";

                    if (accountInfo.Length >= 5 && accountInfo[5].ToLower().Contains("t"))
                    {
                        verifyMsg += "Yes";
                    }
                    else
                    {
                        verifyMsg += "No";
                    }

                    setMsg(verifyMsg);
                    //TODO: Display data that was sent in the msgData string
                    setInfo("");
                    disableRateButtons();
                    enableContextButton("Edit Account");
                    enableOkCancel();
                    disableHandicappedBtn();
                    break;
                case RetrievalStates.CreateAccountAndRetrieveCar:
                    disableContextButton();
                    disableOKCancel();
                    disableHandicappedBtn();
                    string msg = "Creating Account";
                    bool retrieve = true;
                    if (retrieve)
                    {
                        msg += " and Retrieving Vehicle";
                        msg += "\nWatch the overhead display to see where\nyour car will be delivered";
                    }
                    setInfo("");
                    m_displayTimer.Start();
                    setMsg(msg);
                    break;
                case RetrievalStates.ProcessingDL:
                    setMsg("Processing,\nplease wait..");
                    setInfo("");
                    disableContextButton();
                    disableOKCancel();
                    break;
                case RetrievalStates.Processing:
                    disableContextButton();
                    disableOKCancel();
                    disableRateButtons();
                    disableHandicappedBtn();
                    setMsg("Processing..");
                    break;
                case RetrievalStates.OutboundAccepted:
                    string message = "";
                    setInfo("");
                    if (m_accountState == MonthlyAccountStates.AccountVerified)
                    {
                        message = "Account created.";

                    }

                    if (!m_createMonthlyOnStart)
                    {
                        if (message.Length > 0)
                        {
                            //account created
                            message += "\n";
                        }
                        message += "Retrieval request created successfully";
                        if (m_accountState != MonthlyAccountStates.AccountVerified && m_sortser.Length > 0)
                        {
                            message += "\n\nWould you like a receipt of this\ntransaction?";
                            enableOkCancel("Yes", "No");
                        }
                        else
                        {
                            disableOKCancel();
                        }
                        setInfo("Watch the overhead display to see which bay your car will be retrieved to");
                    }
                    setMsg(message);

                    //setMsg("Watch the overhead display to see the bay to which your car will be retrieved");
                    m_displayTimer.Interval = new TimeSpan(0, 0, 0, 25, 0); // Tom says 25 seconds for this
                    m_displayTimer.Start();
                    disableContextButton();
                    disableRateButtons();

                    break;
                case RetrievalStates.DisplayError:
                    setMsg(msgData, true);
                    setInfo("");
                    disableContextButton();
                    disableOKCancel();
                    disableHandicappedBtn();
                    disableRateButtons();
                    //debug
                    //if (SENDTOSIM)
                    //{
                        //sendMsg(KioskMsgType.ERROR_sim, "", true);
                    //}
                    m_displayTimer.Start();
                    break;
            }
        }
        #endregion

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            resetMousePosition();
        }

        private void Window_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            resetMousePosition();
        }
    }
}
