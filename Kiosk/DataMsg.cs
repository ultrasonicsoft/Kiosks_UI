using System;

namespace Kiosk
{
    public enum KioskMsgType
    {
        // Direction 'D'
        vls_ready,
        finding_location,
        authorizing_card,
        inbound_accepted,
        charge_amount,
        retrieving_vehicle,
        dl_storage_duration,
        sizing_car,
        kiosk_ready,
        size_ok,
        dl_confirm,
        m_account_preview,
        m_account_created,
        new_CC,
        vls_reset,
        location_found,
        restart_m_cre,
        ERROR_size_fail,
        ERROR_retrieval_fail,
        ERROR_unknown_card,
        ERROR_card_in_use,
        ERROR_no_avail_loc,
        ERROR_invalid_CC,
        ERROR_DL_not_known,
        ERROR_CC_auth_fail,
        ERROR_DL_auth_fail,
        ERROR_no_car,
        ERROR_m_CC_fail,
        ERROR_m_cre_fail,
        ERROR_m_info_fail,
        ERROR_m_max_reached,
        ERROR_account_exists,
        ERROR_no_data,
        ERROR_timeout,
        ERROR_geocl_fail,
        ERROR_alignment,
        ERROR_VehicleTooBig,
        ERROR_allignment,
        ERROR_510,
        ERROR_CannotSizeCar,
        ERROR_Vehicle2Small,
        ERROR_CarTooSmall,
        ERROR_storage_fail,
        ERROR_ReparkCar,
        ERROR_BayOutOfOrder,
        ERROR_tele,
        ERROR_System, // for db/filesystem, etc errors
        ERROR_Stale70,
        ERROR_Unstable70,


        // Direction 'U'
        user_ready,
        card_string,
        user_confirm,
        user_cancel,
        alt_card_string,
        dl_m_cardswipe,
        monthly_info,
        m_user_confirm,
        m_user_cancel,
        use_dif_CC,
        line_display,
        wait_on_card_string,
        wait_for_confirm,
        card_string_sim,
        wait_on_confirm,
        user_confirm_sim,
        CC_auth_fail,
        ERROR_sim,
        monthlyCC,
        ERROR_monthlyCC_fail,
        i_order_cre,
        ERROR_order_exists,
        EMPTY,
        ERROR_internetfail
    }
    
    public enum PadDirection
    {
        Left,
        Right,
        None
    }
    
    public class DataMsg
    {
        protected string m_kioskid;
        protected KioskMsgType m_msgty;
        protected string m_data;
        
        protected static readonly string m_dateTimeFormat = App.Current.Properties["DATEFORMAT"].ToString();
        
        public DataMsg()
        {
        }

        #region public accessors
        public string KioskID
        {
            get { return m_kioskid; }
        }

        public KioskMsgType MessageType
        {
            get { return m_msgty; }
        }

        public string Data
        {
            get { return m_data; }
            set { m_data = value; }
        }
        #endregion

        protected char getKioskTypeFromID(string id)
        {
            switch(id[0])
            {
                case '9':
                    return 'R';
                case '0':
                    return 'S';
                default:
                    throw new InvalidOperationException();
            }
        }
        
        public bool isError()
        {
            return m_msgty.ToString().StartsWith("ERROR");
        }

        public bool isLineDisplay()
        {
            return m_msgty == KioskMsgType.line_display;
        }

        public bool isReset()
        {
            return (m_msgty == KioskMsgType.vls_reset);
        }
        
    }
}
