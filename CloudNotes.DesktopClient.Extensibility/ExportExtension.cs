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
    using CloudNotes.DesktopClient.Extensibility.Data;
    using System.Windows.Forms;

    public abstract class ExportExtension : Extension
    {
        private string fileName;

        public abstract string FileExtension { get; }
        public abstract string FileExtensionDescription { get; }

        internal void SetFileName(string fileName)
        {
            this.fileName = fileName;
        }

        protected virtual IExportOptionDialog OptionDialog
        {
            get
            {
                return null;
            }
        }

        protected abstract void DoExport(string fileName, Note note, object options);

        protected override void DoExecute(IShell shell)
        {
            object options = null;
            if (this.OptionDialog != null)
            {
                var dialogResult = this.OptionDialog.ShowDialog();
                if (dialogResult == DialogResult.OK)
                {
                    options = this.OptionDialog.Options;
                }
                else
                {
                    return;
                }
            }
            this.DoExport(this.fileName, shell.Note, options);
        }
    }
}
