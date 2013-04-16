using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using Kiosk.KiosksServices;

namespace Kiosk
{
    class KiosksCallbackHandler : ICCManagerServiceCallback
    {
        public bool IsBayReadyForStoreCallback(bool bayStatus)
        {
            //TODO: what functionality should be here
            bool isBayReady = true;
            return isBayReady;
        }

        public bool IsBayReadyForRetrievalCallback(bool bayStatus)
        {
            //TODO: what functionality should be here
            bool isBayReady = true;
            return isBayReady;
        }
    }
}
