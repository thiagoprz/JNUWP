using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.UI.Notifications;

namespace JNBackgroundTask
{
    public sealed class BackgroundPlayer
    {
        BackgroundTaskDeferral _deferral; // Note: defined at class scope so we can mark it complete inside the OnCancel() callback if we choose to support cancellation

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            _deferral.Complete();
        }
    }
}
