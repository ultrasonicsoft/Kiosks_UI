using System;
using System.Net.NetworkInformation;

namespace Kiosk
{
    internal static class Helper
    {
        internal static string GetMacAddress()
        {
            string macAddress = Guid.NewGuid().ToString();
            try
            {
                const int MIN_MAC_ADDR_LENGTH = 12;
                long maxSpeed = -1;

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string tempMac = nic.GetPhysicalAddress().ToString();
                    if (nic.Speed > maxSpeed &&
                        !string.IsNullOrEmpty(tempMac) &&
                        tempMac.Length >= MIN_MAC_ADDR_LENGTH)
                    {
                        maxSpeed = nic.Speed;
                        macAddress = tempMac;
                    }
                }
            }
            catch (Exception ex)
            {
                //Helper.LogMessage(ex.Message + Environment.NewLine);
            }
            return macAddress;
        }
    }
}
