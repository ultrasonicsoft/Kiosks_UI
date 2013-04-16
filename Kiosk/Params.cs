using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Threading;

namespace Kiosk
{
    class Params
    {
        private static string getVal(string field, string vlsProcess, string column)
        {
            string sqlParamSelect = "SELECT " + column + " FROM vlsparams WHERE( vlsconfigfield = @field AND vlsprocess = @vlsprocess)";
            string value = "";
            object objValue = null;
            using (SqlConnection conn = new SqlConnection(App.viadatConnString))
            {
                try
                {
                    conn.Open();
                    SqlCommand getConfigValue = new SqlCommand(sqlParamSelect, conn);
                    getConfigValue.Parameters.AddWithValue("@field", field);
                    getConfigValue.Parameters.AddWithValue("@vlsprocess", vlsProcess);
                    objValue = getConfigValue.ExecuteScalar();
                }
                catch (SqlException ex)
                {
                    object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                    if (obj != null)
                    {
                        ((LogClient)obj).log(DateTime.Now.ToLongTimeString() + " " + "Error: Problem loading " + field +
                                 "/" + vlsProcess + " from the vlsparams table: " + ex.Message);
                    }

                }
            }
            if (objValue == null || objValue.Equals(DBNull.Value))
            {
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Error: Problem loading " + field +
                                             "/" + vlsProcess + " from the vlsparams table. " + "The value was not initialized.");
                }

            }
            else
            {
                value = objValue.ToString();
            }

            return value;
        }

        public static decimal getParam(string field, string vlsProcess, decimal defaultValue)
        {
            string sResult = getVal(field, vlsProcess, "vlsvalue");

            decimal result;
            bool success = decimal.TryParse(sResult, out result);
            if (!success || result < 0)
            {
                result = defaultValue;
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");
                }
            }

            return result;
        }

        public static double getParam(string field, string vlsProcess, double defaultValue)
        {
            string sResult = getVal(field, vlsProcess, "vlsvalue");

            double result;
            bool success = double.TryParse(sResult, out result);
            if (!success || result < 0)
            {
                result = defaultValue;
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");

                }
            }
            return result;
        }

        public static string getParam(string field, string vlsProcess, string defaultValue)
        {
            string result = getVal(field, vlsProcess, "vlsvalue");

            if (result == null || result.Equals(DBNull.Value) || result.Equals(""))
            {
                result = defaultValue;
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");

                }
            }

            return result;
        }

        public static int getParam(string field, string vlsProcess, int defaultValue)
        {
            string sResult = getVal(field, vlsProcess, "vlsvalue");

            int result;
            bool success = Int32.TryParse(sResult, out result);
            if (!success || result < 0)
            {
                result = defaultValue;
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");
                }
            }
            return result;
        }

        public static bool getParam(string field, string vlsProcess, bool defaultValue)
        {
            string sResult = getVal(field, vlsProcess, "vlsvalue");
            int iResult;
            bool result;
            bool success = Boolean.TryParse(sResult, out result);
            if (!success)
            {
                //try and read 1 or 0
                if (Int32.TryParse(sResult, out iResult))
                {
                    switch (iResult)
                    {
                        case 0:
                            result = false;
                            break;
                        case 1:
                            result = true;
                            break;
                        default:
                            result = defaultValue;
                            object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                            if (obj != null)
                            {
                                ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");
                            
                            }
                            break;
                    }
                }
                else
                {
                    result = defaultValue;
                    object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                    if (obj != null)
                    {
                        ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");

                    }
                }
            }
            else
            {
                result = defaultValue;
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");
                }
            }
            return result;
        }

        public static string getUnit(string field, string vlsProcess, string defaultValue)
        {
            string unit = getVal(field, vlsProcess, "vlsvalueunits");

            if (unit.Equals("") || unit == null || unit.Equals(DBNull.Value))
            {
                unit = defaultValue;
                object obj = Thread.GetData(Thread.GetNamedDataSlot("Logclient"));
                if (obj != null)
                {
                    ((LogClient)Thread.GetData(Thread.GetNamedDataSlot("Logclient"))).log(DateTime.Now.ToLongTimeString() + " " + "Setting " + field + " to default value.");
                }
            }

            return unit;
        }

    }
}
