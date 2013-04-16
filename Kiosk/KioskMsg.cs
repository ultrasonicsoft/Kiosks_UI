using System;
using System.Data.SqlClient;

namespace Kiosk
{

    public enum SqlMode
    {
        Send,
        Archive,
        Delete
    }
    // =============================================================================================
    // Class KioskMsg
    // =============================================================================================
    public class KioskMsg : DataMsg
    {
        private long m_lineid;    // created by db Identity field, 0 until read from db
        private char m_direction;  // U = up = kiosk -> VLS; D = down = VLS -> Kiosk
        private char m_kioskty;    // R = retrieve; S = store



        // -----------------------------------------------------------------------------------------
        // Constructor for use when creating a new kioskMsg to send (lineid not yet known)
        // -----------------------------------------------------------------------------------------
        public KioskMsg(char direction, char kioskty, string kioskid, KioskMsgType msgty, string data)
        {
            m_direction = direction;
            m_kioskty = kioskty;
            m_kioskid = kioskid;
            m_msgty = msgty;
            m_data = data ?? "";
        }

        // -----------------------------------------------------------------------------------------
        // Constructor for use when reading a kioskMsg from the database (lineid known)
        // -----------------------------------------------------------------------------------------
        public KioskMsg(long lineid, char direction, char kioskty, string kioskid, KioskMsgType msgty, string data)
        {
            m_lineid = lineid;
            m_direction = direction;
            m_kioskty = kioskty;
            m_kioskid = kioskid;
            m_msgty = msgty;
            m_data = data ?? "";
        }

        #region Properties
        public long Lineid
        {
            get { return m_lineid; }
            set { m_lineid = value; }
        }

        public char Direction
        {
            get { return m_direction; }
            set { m_direction = value; }
        }

        #endregion

        public override string ToString()
        {
            return "(" + m_direction.ToString() + ")" + "(" + m_kioskty.ToString() + ")" + "(" + m_kioskid.ToString() + ")" + "(" + m_msgty.ToString() + ")" + "(" + m_data.ToString() + ")";
        }

        #region Sql statements
        private string delStmt()
        {
            return "DELETE FROM kioskmsg WHERE lineid = " + m_lineid.ToString();
        }

        // -----------------------------------------------------------------------------------------
        // generates a SQL statement to insert this KioskMsg into the archive table kioskmsgarch
        // -----------------------------------------------------------------------------------------
        private string insArchStmt()
        {
            return "INSERT INTO kioskmsgarch VALUES (" +
                "'" + m_direction.ToString() + "', " +
                "'" + m_kioskty.ToString() + "', " +
                "'" + m_kioskid.ToString() + "', " +
                "'" + m_msgty.ToString() + "', " +
                "'" + m_data.ToString() + "', " +
                "'" + DateTime.Now.ToString(m_dateTimeFormat) + "', " + //dtimecre
                "'" + DateTime.Now.ToString(m_dateTimeFormat) + "', " + //dtimemod
                "'Kiosk', 'Kiosk', 0)"; //pgmmod, usrmod, modcnt
        }

        // -----------------------------------------------------------------------------------------
        // generates a SQL statement to insert this KioskMsg into the message table kioskmsg
        // -----------------------------------------------------------------------------------------
        private string insStmt()
        {
            return "INSERT INTO kioskmsg VALUES (" +
                "'" + m_direction.ToString() + "', " +
                "'" + m_kioskty.ToString() + "', " +
                "'" + m_kioskid.ToString() + "', " +
                "'" + m_msgty.ToString() + "', " +
                "'" + m_data.ToString() + "', " +
                "'" + DateTime.Now.ToString(m_dateTimeFormat) + "', " + //dtimecre
                "'" + DateTime.Now.ToString(m_dateTimeFormat) + "', " + //dtimemod
                "'Kiosk', 'Kiosk', 0)"; //pgmmod, usrmod, modcnt
        }

        private void sqlNonQuery(string connString, SqlMode mode)
        {
            int numRows = 0;
            using (SqlConnection conn = new SqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    SqlCommand sc;
                    switch (mode)
                    {
                        case SqlMode.Send:
                            sc = new SqlCommand(insStmt(), conn);
                            break;
                        case SqlMode.Archive:
                            sc = new SqlCommand(insArchStmt(), conn);
                            break;
                        case SqlMode.Delete:
                            sc = new SqlCommand(delStmt(), conn);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    numRows = sc.ExecuteNonQuery();
                    if (numRows > 1)
                    {
                        Exception e = new Exception("SQL NonQuery affected more than 1 row");
                        throw e;
                    }
                    else if(numRows < 1)
                    {
                        Exception e = new Exception("SQL NonQuery affected 0 rows");
                        throw e;
                    }
                }
                catch (Exception e)
                {
                    //todo: handle/throw other exceptions (connection, etc)
                    //poss: numRows null (execute failure)
                    throw e;
                }
                finally
                {
                    conn.Close();
                }
            }
        }

        public void sendMsg(string connString)
        {
            sqlNonQuery(connString, SqlMode.Send);
        }

        public void archMsg(string connString)
        {
            sqlNonQuery(connString, SqlMode.Archive);
        }

        public void delMsg(string connString)
        {
            sqlNonQuery(connString, SqlMode.Delete);
        }
        #endregion
    }
}
