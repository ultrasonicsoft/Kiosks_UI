using System;
using System.Collections.Generic;
using System.Text;

namespace Kiosk
{
    interface IThreadable
    {
        void threadLoop();
        void terminate();
    }
}
