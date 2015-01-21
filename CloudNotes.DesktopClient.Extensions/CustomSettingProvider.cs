﻿using CloudNotes.DesktopClient.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CloudNotes.DesktopClient.Extensions
{
    public sealed class CustomSettingProvider : ExtensionSettingProvider
    {
        public CustomSettingProvider(Extension extension)
            : base(extension)
        { }

        protected override Type SettingControlType
        {
            get { return typeof(CustomSettingControl); }
        }

        protected override Type ExtensionSettingType
        {
            get { return typeof(CustomSetting); }
        }

        protected override void DoBindSettings(IExtensionSetting setting)
        {
            var localSetting = (CustomSetting)setting;
            
            (this.SettingControl as CustomSettingControl).txtGreeting.Text = localSetting.Greeting;
        }

        protected override IExtensionSetting DefaultSetting
        {
            get { return new CustomSetting { Greeting = "Hello World" }; }
        }
    }
}
