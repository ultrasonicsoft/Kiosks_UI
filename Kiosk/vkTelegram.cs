using System;

namespace Kiosk
{
    
    /// <summary>
    /// Custom telegram class to handle communication between kiosks and kiosk
    /// managers
    /// </summary>
    class vkTelegram : DataMsg
    {
        private char m_kioskty;

        //field delimiter = ASCII 30 (Record Seperator)
        private readonly char[] m_delim = {Convert.ToChar(30)};

        private Crypto m_crypt = new Crypto(Crypto.HashType.SHA1, 256, 2, "@1a2c3D6E5F6g7H8", "!p@55w0rd", "SeaS@lt");

        #region constructors

        public vkTelegram(string kioskID, KioskMsgType messageType, string data)
        {
            //todo: add validation (kioskty must be 'S' or 'R', kioskid must be c_idlen, 
            //      msgty must be valid, mdata.len must be <=180), error handling (throw)
            m_kioskty = getKioskTypeFromID(kioskID);
            m_kioskid = kioskID;
            m_msgty = messageType;
            m_data = data ?? "";
        }

        /// <summary>
        /// Generates telegram object from delimited string. 
        /// Expected fields are (char kiosk type)(int kiosk id)(KioskMsgType message type)(base64 string encrypted data)
        /// </summary>
        /// <param name="telegram">'RS' delimited 4-field string</param>
        public vkTelegram(string telegram)
        {
            string[] fields;

            try
            {
                fields = telegram.Split(m_delim);

                if (fields.Length != 4)
                {
                    Exception e = new Exception("Telegram contains an invalid number of fields: " + fields.Length);
                    App.AppEventLog.WriteEntry(LogTools.getExceptionString("vkTelegram", "vkTelegram", e));
                    throw e;
                }
                m_kioskty = fields[0][0];

                m_kioskid = fields[1];

                m_msgty = (KioskMsgType)Enum.Parse(typeof(KioskMsgType), fields[2]);
                switch (m_msgty)
                {
                    case KioskMsgType.alt_card_string:
                    case KioskMsgType.card_string:
                    case KioskMsgType.new_CC:
                    case KioskMsgType.dl_m_cardswipe:
                    case KioskMsgType.monthlyCC:
                    case KioskMsgType.use_dif_CC:
                        if (fields[3].Length > 0)
                        {
                            m_data = m_crypt.decryptAES(fields[3]);
                        }
                        else
                        {
                            m_data = fields[3]; 
                        }
                        break;
                    default:
                        m_data = fields[3];
                        break;
                }
                //m_data = m_crypt.decryptAES(fields[3]);
            }
            catch (Exception e)
            {
                //todo: add validation (see above), error handling
                App.AppEventLog.WriteEntry(LogTools.getExceptionString("vkTelegram", "vkTelegram", e));
                throw e;
            }
        }
        #endregion

        /// <summary>
        /// assembles telegram string by delimiting fields
        /// </summary>
        /// <returns>telegram string</returns>
        public override string ToString()
        {
            string telegram = "";

            try
            {
                telegram += m_kioskty;
                telegram += m_delim[0];
                telegram += m_kioskid;
                telegram += m_delim[0];
                telegram += m_msgty.ToString();
                telegram += m_delim[0];
                switch (m_msgty)
                {
                    case KioskMsgType.alt_card_string:
                    case KioskMsgType.card_string:
                    case KioskMsgType.new_CC:
                    case KioskMsgType.dl_m_cardswipe:
                    case KioskMsgType.use_dif_CC:
                    case KioskMsgType.monthlyCC:
                        if (m_data.Length > 0)
                        {
                            telegram += m_crypt.encryptAES(m_data);
                        }
                        else
                        {
                            telegram += m_data;
                        }
                        break;
                    default:
                        telegram += m_data;
                        break;
                }
                //telegram += m_crypt.encryptAES(m_data);
            }
            catch (Exception e)
            {
                //todo: exception handling
                App.AppEventLog.WriteEntry(LogTools.getExceptionString("vkTelegram", "ToString", e));
                throw e;
            }

            return telegram;
        }
       
    }
}