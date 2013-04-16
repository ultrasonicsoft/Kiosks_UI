using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using Kiosk.KiosksServices;

namespace Kiosk
{
    class CCManagerConsumer
    {
        internal KiosksConfiguration CurrentConfiguration { get; set; }
        internal CCManagerServiceClient CCManagerSerivceProxy { get; set; }

        #region Constructor

        internal CCManagerConsumer()
        {
            InstanceContext site = new InstanceContext(new KiosksCallbackHandler());
            CCManagerSerivceProxy = new CCManagerServiceClient(site);
        }
        #endregion

        /// <summary>
        /// Reads the configuration.
        /// </summary>
        internal void ReadConfiguration()
        {
            // Calling WCF Service
            try
            {
                string kiosksMacID = Helper.GetMacAddress();
                CurrentConfiguration = CCManagerSerivceProxy.GetConfiguration(kiosksMacID);
            }
            catch (Exception ex)
            {
                //TODO: log exception
            }
        }

        /// <summary>
        /// Gets the monthly rates.
        /// </summary>
        internal void GetMonthlyRates(List<MonthlyRate> m_rates)
        {
            try
            {
                m_rates = CCManagerSerivceProxy.GetMonthlyRates().ToList();
            }
            catch (Exception ex)
            {
                //TODO: log exception    
            }
        }

        /// <summary>
        /// Gets the bay ready statsu fo srtorage.
        /// </summary>
        internal bool GetBayReadyStatsuFoSrtorage()
        {
            bool isBayReady = false;
            try
            {
                //This WCF Service call will result in invocation of KiosksCallbackHandler.IsBayReadyForStoreCallback method using callback
                isBayReady = CCManagerSerivceProxy.IsBayReadyForStore(CurrentConfiguration.BayID);
            }
            catch (Exception ex)
            {
                //TODO: log exception
            }
            return isBayReady;
        }

        /// <summary>
        /// Gets the bay ready statsu fo retrieval.
        /// </summary>
        internal bool GetBayReadyStatsuFoRetrieval()
        {
            bool isBayReady = false;
            try
            {
                //This WCF Service call will result in invocation of KiosksCallbackHandler.IsBayReadyForStoreCallback method using callback
                isBayReady = CCManagerSerivceProxy.IsBayReadyForRetrieval(CurrentConfiguration.BayID);
            }
            catch (Exception ex)
            {
                //TODO: log exception
            }
            return isBayReady;
        }

        /// <summary>
        /// Does the parking transaction.
        /// </summary>
        /// <param name="card_string">The card_string.</param>
        /// <param name="swipe">The swipe.</param>
        /// <returns></returns>
        internal bool DoParkingTransaction(KioskMsgType card_string, string swipe)
        {
            bool isValid = false;
            try
            {
                CreditCardDetails creditCard = new CreditCardDetails();
                creditCard.CreditCardNumber = swipe;
                creditCard.ValidTillDate = "1/1";

                isValid = CCManagerSerivceProxy.IsValidCreditCard(creditCard);
            }
            catch (Exception ex)
            {
                //TODO: log exception
            }
            return isValid;
        }
    }
}
