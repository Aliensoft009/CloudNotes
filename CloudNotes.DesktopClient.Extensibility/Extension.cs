﻿// =======================================================================================================
//
//    ,uEZGZX  LG                             Eu       iJ       vi                                              
//   BB7.  .:  uM                             8F       0BN      Bq             S:                               
//  @X         LO    rJLYi    :     i    iYLL XJ       Xu7@     Mu    7LjL;   rBOii   7LJ7    .vYUi             
// ,@          LG  7Br...SB  vB     B   B1...7BL       0S i@,   OU  :@7. ,u@   @u.. :@:  ;B  LB. ::             
// v@          LO  B      Z0 i@     @  BU     @Y       qq  .@L  Oj  @      5@  Oi   @.    MB U@                 
// .@          JZ :@      :@ rB     B  @      5U       Eq    @0 Xj ,B      .B  Br  ,B:rv777i  :0ZU              
//  @M         LO  @      Mk :@    .@  BL     @J       EZ     GZML  @      XM  B;   @            Y@             
//   ZBFi::vu  1B  ;B7..:qO   BS..iGB   BJ..:vB2       BM      rBj  :@7,.:5B   qM.. i@r..i5. ir. UB             
//     iuU1vi   ,    ;LLv,     iYvi ,    ;LLr  .       .,       .     rvY7:     rLi   7LLr,  ,uvv:  
//
//
// Copyright 2014-2015 daxnet
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// =======================================================================================================

namespace CloudNotes.DesktopClient.Extensibility
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class Extension
    {
        private Extension() { }

        protected Extension(string name)
        {
            this.Name = name;
        }

        private static string GetExtensionSettingFileName(Extension extension)
        {
            var relativeFileName = Path.Combine("extensions", string.Format("extension.{0}.setting.json", extension.ID.ToString().ToUpper()));
            return Directories.GetFullName(relativeFileName);
        }

        protected internal static T ReadSetting<T>(Extension extension)
            where T : class, IExtensionSetting
        {
            var settingFile = GetExtensionSettingFileName(extension);
            if (!File.Exists(settingFile))
            {
                return null;
            }
            var settingJson = File.ReadAllText(settingFile);
            return JsonConvert.DeserializeObject<T>(settingJson);
        }

        protected internal static void WriteSetting<T>(Extension extension, T setting)
            where T : class, IExtensionSetting
        {
            var settingFile = GetExtensionSettingFileName(extension);
            var settingJson = JsonConvert.SerializeObject(setting);
            File.WriteAllText(settingFile, settingJson);
        }

        public Guid ID
        {
            get
            {
                var extensionAttributes = this.GetType().GetCustomAttributes(typeof(ExtensionAttribute), false);
                if (extensionAttributes != null && extensionAttributes.Length > 0)
                {
                    return (extensionAttributes[0] as ExtensionAttribute).ID;
                }
                throw new InvalidOperationException("The extension was not decorated with ExtensionAttribute attribute.");
            }
        }

        protected abstract Task DoExecuteAsync(IShell shell);

        public string Name { get; private set; }
        public abstract string DisplayName { get; }

        public virtual ExtensionSettingControl SettingControl
        {
            get
            {
                return null;
            }
        }

        public void Execute(IShell shell)
        {
            if (shell == null)
                throw new ArgumentNullException("shell");
            var task = Task.Run(async () => await this.DoExecuteAsync(shell));
            task.Wait();
        }
    }
}
