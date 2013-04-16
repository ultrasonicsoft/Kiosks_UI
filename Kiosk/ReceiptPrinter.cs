using System;
using System.Drawing.Printing;
using System.Drawing;
using System.Text;
using System.Data.SqlClient;

namespace Kiosk
{
    class ReceiptPrinter
    {
        private LogClient m_log;
        private string m_printString;
        private string m_printerName;

        // receipt fields
        private string m_title;
        private string m_address1;
        private string m_address2;
        private string m_address3;
        private string m_phone;
        private string m_date;
        private string m_storeDateTime;
        private string m_retrieveDateTime;
        private string m_hoursStored;
        private string m_minutesStored;
        private string m_cardType;
        private string m_cardLast4;
        private string m_firstname;
        private string m_lastname;
        private string m_chargeAmount;
        private string m_kioskId;
        private string m_transactId;

        private string m_divider = "**********************************";

        public ReceiptPrinter(LogClient log)
        {
            //m_log = log;
            m_printString = "";
        }
        public bool printReceipt(string primaryKey)
        {
            //From Matt's Code:
                //Dim pdPrinter As New Printing.PrintDocument
                //Dim strFormatted(32) As String
                //pdPrinter.PrintController = New Printing.StandardPrintController
                //Using (pdPrinter)
                //    pdPrinter.PrinterSettings.PrinterName = "CUSTOM TG2480-H"
                //    AddHandler pdPrinter.PrintPage, AddressOf Me.PrintPageHandler
                //    pdPrinter.Print()
                //    RemoveHandler pdPrinter.PrintPage, AddressOf Me.PrintPageHandler
                //End Using

            if (primaryKey == null)
            {
                throw new ArgumentNullException();
            }
            else if (primaryKey.Length == 0)
            {
                throw new ArgumentException();
            }

            bool successful = false;
            try
            {
                m_kioskId = App.Current.Properties["KIOSKID"].ToString();

                // fetch paramaters data
                // -------------------------------------------------------------------------------
                m_printerName = Params.getParam("printerName" + m_kioskId, "rkman", ""); 
                m_title = Params.getParam("receipt_title", "rkman", "");
                m_address1 = Params.getParam("receipt_address1", "rkman", "");
                m_address2 = Params.getParam("receipt_address2", "rkman", "");
                m_address3 = Params.getParam("receipt_address3", "rkman", "");
                m_phone = Params.getParam("receipt_phone", "rkman", "");

                // fetch fields from receiptinfo table
                // -------------------------------------------------------------------------------
                string selectStmt = "SELECT dtimestore, dtimeretrieve, creditcardtype, cardlastfourpay, userfirstname, userlastname, amountpaid, transactionid FROM receiptinfo WHERE sortser = '" + primaryKey + "'";
                using (SqlConnection conn = new SqlConnection(App.viadatConnString))
                {
                    try
                    {
                        conn.Open();
                        SqlCommand selReceiptInfo = new SqlCommand(selectStmt, conn);
                        SqlDataReader reader = selReceiptInfo.ExecuteReader();
                        reader.Read();
                        m_storeDateTime = reader["dtimestore"].ToString();
                        m_retrieveDateTime = reader["dtimeretrieve"].ToString();
                        m_cardType = reader["creditcardtype"].ToString();
                        m_cardLast4 = reader["cardlastfourpay"].ToString();
                        m_firstname = reader["userfirstname"].ToString().TrimEnd();
                        m_lastname = reader["userlastname"].ToString().TrimEnd();
                        m_chargeAmount = "$" + reader["amountpaid"].ToString();
                        m_transactId = reader["transactionid"].ToString();                        
                    }
                    catch (SqlException sqlex)
                    {
                        //m_log.log(LogTools.getStatusString("ReceiptPrinter", "printReceipt", selectStmt));
                        //m_log.log(LogTools.getExceptionString("ReceiptPrinter", "printReceipt", sqlex));
                        return false;
                    }
                }
                // calculate rest of fields
                // -------------------------------------------------------------------------------
                m_date = DateTime.Today.ToString("MM/dd/yy");
                
                TimeSpan timeStored = DateTime.Parse(m_retrieveDateTime) - DateTime.Parse(m_storeDateTime);
                int minutes = timeStored.Minutes;
                if (timeStored.Seconds > 0 || timeStored.Milliseconds > 0)
                {
                    ++minutes;
                }
                int hours = (int)Math.Floor(timeStored.TotalHours);
                if (minutes >= 60)
                {
                    minutes -= 60;
                    ++hours;
                }
                m_hoursStored = hours.ToString();
                m_minutesStored = minutes.ToString();
                m_kioskId = m_kioskId.Substring(m_kioskId.Length - 1, 1);

                //build print string
                // -------------------------------------------------------------------------------
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(center(m_title));
                sb.AppendLine();
                sb.AppendLine(center(m_address1));
                sb.AppendLine(center(m_address2));
                sb.AppendLine(center(m_address3));
                sb.AppendLine(center(m_phone));
                sb.AppendLine();
                sb.AppendLine(m_divider);
                sb.AppendLine("DATE: " + m_date);
                sb.AppendLine();
                sb.AppendLine("STORED:    " + m_storeDateTime);
                sb.AppendLine("RETRIEVED: " + m_retrieveDateTime);
                sb.AppendLine("TOTAL STORED: " + m_hoursStored + " HOURS, " + m_minutesStored + " MINS");
                sb.AppendLine(m_divider);
                sb.AppendLine();
                sb.AppendLine("CUSTOMER RECEIPT COPY");
                sb.AppendLine();
                sb.AppendLine("AMOUNT TENDERED");
                sb.AppendLine(m_cardType);
                sb.AppendLine("ORIGNAL TRANS INFORMATION");
                sb.AppendLine("CARD #: ************" + m_cardLast4);
                sb.AppendLine("CARDHOLDER: " + m_firstname + " " + m_lastname);
                sb.AppendLine();
                sb.AppendLine("TOTAL PAYMENT: " + m_chargeAmount);
                sb.AppendLine();
                sb.AppendLine(m_divider);
                sb.AppendLine("Kiosk ID: Retrieve " + m_kioskId);
                sb.AppendLine("Transaction ID: " + m_transactId);
                sb.AppendLine(m_divider);
                sb.AppendLine();

                m_printString = sb.ToString();
                ////m_log.log(LogTools.getStatusString("ReceiptPrinter", "printReceipt", "Would Print:\n" + m_printString));

                // print
                // -------------------------------------------------------------------------------
                PrintDocument printer = new PrintDocument();
                printer.PrintController = new StandardPrintController();
                using (printer)
                {
                    printer.PrinterSettings.PrinterName = m_printerName;
                    printer.PrintPage += new PrintPageEventHandler(this.printPageHandler);
                    printer.Print();
                }
                successful = true;
            }
            catch (Exception ex)
            {
                //m_log.log(LogTools.getStatusString("ReceiptPrinter", "printReceipt", "Printer: " + m_printerName));
                //m_log.log(LogTools.getExceptionString("ReceiptPrinter", "print", ex));
                successful = false;
            }
            finally
            {
                m_printString = "";
            }
            return successful;
        }

        private void printPageHandler(object sender, PrintPageEventArgs ev)
        {
            ev.Graphics.DrawString(m_printString, new Font(FontFamily.GenericMonospace, 8, FontStyle.Bold), Brushes.Black, 0, 30);
        }

        private string center(string str)
        {
            string spaces = new String(' ', (m_divider.Length - str.Length) / 2);
            return spaces + str;
        }
    }
}
