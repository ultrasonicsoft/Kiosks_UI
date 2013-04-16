
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System;
using Kiosk.KiosksServices;
namespace Kiosk
{
    #region Enum Definitions
    public enum StorageStates
    {
        WaitForReady,
        WaitForCard,
        WaitForSize,
        WaitForLocation,
        WaitForAuth,
        WaitForConfirm,
        InboundAccepted,
        DisplayError,
        Processing
    }

    public enum RetrievalStates
    {
        Reset,
        Initial,
        WaitForDuration,
        DisplayDurationCC,
        DisplayDurationDL,
        WaitForAuth,
        ProcessingDL,
        OutboundAccepted,
        AlternateCardForRetrieve,
        CreateAccountInit,
        AccountEdit,
        AlternateCardForMonthly,
        AccountVerify,
        DisplayError,
        SwipeNewMonthlyDL,
        WaitForDlAuth,
        VerifyAlternateMonthlyCC,
        CreateAccountAndRetrieveCar,
        ConfirmMonthlyCreation,
        GetNewCC,
        Processing,
        SwipeNewMonthlyCC,
        Receipt
    }

    public enum MonthlyAccountStates
    {
        NotSelected,
        SwipeDl,
        DlVerified,
        DlError,
        AccountCanceled,
        CustomizeAccount,
        AccountVerificationFail,
        SwipeNewCC,
        NewCcVerified,
        NewCcError,
        AccountVerified,
        ConfirmAccoumtCreation,
        CreatingMonthlyAccount,
        AccountCcFail,
        AccountCreFail,
        MaxedMonthlyAccounts,
        MCCError
    }

    public enum KioskMode
    {
        Storage,
        Retrieval
    }

    public enum BackgroundType
    {
        Active,
        Inactive,
        BlueButton,
        GreenButton
    }
    #endregion

  
    internal class Constants
    {
        internal static string ClassIdentifier = "MainWindow";
        internal static readonly string KIOSKID = App.Current.Properties["KIOSKID"].ToString();

        internal static readonly Brush m_normalMsgForeground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        internal static readonly Brush m_normalMsgBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x23, 0x1F, 0x20));
        internal static readonly Brush m_errorMsgForeground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xCE, 0x06, 0x10));
        internal static readonly Brush m_errorMsgBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xCE, 0x06, 0x10));

        internal static readonly ImageBrush m_activeBackground = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/Images/Kiosk1.png")));
        internal static readonly ImageBrush m_inactiveBackground = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/Images/Kiosk2.png")));
        internal static readonly ImageBrush m_blueButtonBackground = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/Images/buttonBlue3.png")));
        internal static readonly ImageBrush m_greenButtonBackground = new ImageBrush(new BitmapImage(new Uri(@"pack://application:,,,/Images/buttonGreen.png")));

        internal static int btnLoc0 = 800;
        internal static int btnLoc1 = 650;
        internal static int btnLoc2 = 500;
        internal static int btnLoc3 = 350;
        internal static int btnLoc4 = 200;

        internal static readonly string[] geocl2str =
        {
            "UNKNOWN",
            "Compact",
            "Sedan",
            "SUV",
            "Large SUV",
            "Truck/Van"
        };

        internal const bool SENDTOSIM = false; // set to true if using Criterion's eValet to simulate user interaction with the Kiosk

        internal static readonly char[] dataDelimiters = { '#' };
        internal static readonly char[] zero = { '0' };
        internal static readonly char[] newline = { '\n', '\r' };

    }

    //internal class MonthlyRate
    //{
    //    public int duration;
    //    public decimal rate;

    //    public MonthlyRate(int _duration, decimal _rate)
    //    {
    //        rate = _rate;
    //        duration = _duration;
    //    }
    //}

  
}
