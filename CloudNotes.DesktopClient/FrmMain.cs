﻿


namespace CloudNotes.DesktopClient
{
    using System.Globalization;
    using System.Reflection;
    using System.Threading;

    using CloudNotes.DesktopClient.ClientModel;
    using CloudNotes.DesktopClient.Properties;
    using CloudNotes.DesktopClient.Settings;
    using CloudNotes.Infrastructure;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using YARTE.Buttons;
    using YARTE.UI.Buttons;
    using CloudNotes.DESecurity;

    public sealed partial class FrmMain : Form
    {
        private readonly ClientCredential credential;

        private readonly Crypto crypto = Crypto.CreateDefaultCrypto();

        private dynamic currentNote;

        private Workspace workspace;

        private volatile bool closingSignal; // Indicates the closing signal that is sent to the form's closing event.

        private readonly TreeNode notesNode;

        private readonly TreeNode trashNode;

        private readonly DesktopClientSettings settings;

        private CheckUpdateResult checkUpdateResult;

        public FrmMain(ClientCredential credential, DesktopClientSettings settings)
        {
            InitializeComponent();

            InitializeHtmlEditor();

            this.credential = credential;

            this.settings = settings;

            this.Text = string.Format("CloudNotes - {0}@{1}", credential.UserName, credential.ServerUri);

            this.notifyIcon.Text = string.Format("CloudNotes - {0}", credential.UserName);

            this.notesNode = this.tvNotes.Nodes.Add("NotesRoot", Resources.NotesNodeTitle, "Notes.png", "Notes.png");
            this.trashNode = this.tvNotes.Nodes.Add("TrashRoot", Resources.TrashNodeTitle, "Trash.png", "Trash.png");

            Application.Idle += (s, e) =>
                {
                    slblStatus.Text = Resources.Ready;
                };
        }

        private void InitializeHtmlEditor()
        {
            PredefinedButtonSets.SetupDefaultButtons(this.htmlEditor);
            this.htmlEditor.DocumentTextChanged += (s, e) =>
                {
                    if (workspace != null)
                    {
                        workspace.Content = this.htmlEditor.Html;
                    }
                };
            this.htmlEditor.DocumentPreviewKeyDown += async (kds, kde) =>
                {
                    if (kde.KeyData == (Keys.Control | Keys.S))
                    {
                        await this.DoSaveAsync();
                    }
                };
        }

        /// <summary>
        /// Corrects the current node selection in the notes tree view, before the current node is going
        /// to be removed.
        /// </summary>
        /// <param name="currentNode">The current node that is going to be removed.</param>
        private async Task CorrectNodeSelectionAsync(TreeNode currentNode)
        {
            var previousNode = currentNode.PrevNode;
            if (previousNode == null)
            {
                previousNode = currentNode.Parent;
            }
            if (previousNode == notesNode || previousNode == trashNode)
            {
                lblTitle.Text = string.Empty;
                lblDatePublished.Text = string.Empty;
                htmlEditor.Enabled = false;
                htmlEditor.Html = string.Empty;
                mnuPrint.Enabled = false;
            }
            else
            {
                dynamic note = previousNode.Tag;
                await this.LoadNoteAsync((Guid)note.ID);
            }
            tvNotes.SelectedNode = previousNode;
        }

        private void ResortNodes(TreeNode parent)
        {
            var tempNodes = new TreeNode[parent.Nodes.Count];
            parent.Nodes.CopyTo(tempNodes, 0);
            var sortedList = new List<TreeNode>(tempNodes);
            sortedList.Sort(
                (x, y) =>
                {
                    dynamic xTag = x.Tag;
                    dynamic yTag = y.Tag;
                    var xPublishedDate = (DateTime)xTag.DatePublished;
                    var yPublishedDate = (DateTime)yTag.DatePublished;
                    return yPublishedDate.CompareTo(xPublishedDate);
                });
            parent.Nodes.Clear();
            parent.Nodes.AddRange(sortedList.ToArray());
        }

        private void UpdateSettings()
        {

        }

        private static string ReplaceFileSystemImages(string html)
        {
            var matches = Regex.Matches(
                html,
                @"<img[^>]*?src\s*=\s*([""']?[^'"">]+?['""])[^>]*?>",
                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                string src = match.Groups[1].Value;
                src = src.Trim('\"');
                if (File.Exists(src))
                {
                    var ext = Path.GetExtension(src);
                    if (ext.Length > 0)
                    {
                        ext = ext.Substring(1);
                        src = string.Format(
                            "'data:image/{0};base64,{1}'",
                            ext,
                            Convert.ToBase64String(File.ReadAllBytes(src)));
                        html = html.Replace(match.Groups[1].Value, src);
                    }
                }
            }
            return html;
        }

        /// <summary>
        /// Determines whether the note data contained within the specified tree node has already
        /// been marked as deleted.
        /// </summary>
        /// <param name="node">The tree node that contains the note data.</param>
        /// <returns>True if the note was marked as deleted. Otherwise false.</returns>
        private static bool IsMarkedAsDeletedNoteNode(TreeNode node)
        {
            if (node.Tag == null) return false;
            dynamic note = node.Tag;
            if (note == null || note.DeletedFlag == null) return false;
            return (int)note.DeletedFlag == (int)DeleteFlagModel.MarkDeleted;
        }

        private async Task LoadNotesAsync()
        {
            lblTitle.Text = string.Empty;
            lblDatePublished.Text = string.Empty;
            htmlEditor.Html = string.Empty;
            htmlEditor.Enabled = false;

            mnuOpen.Enabled = false;
            tbtnOpen.Enabled = false;
            mnuDelete.Enabled = false;
            tbtnDelete.Enabled = false;
            mnuPermanentDelete.Enabled = false;
            mnuRestore.Enabled = false;
            tbtnRestore.Enabled = false;
            mnuRename.Enabled = false;
            tbtnRename.Enabled = false;
            tbtnSave.Enabled = false;
            mnuSave.Enabled = false;
            mnuEmptyTrash.Enabled = false;
            cmnuEmptyTrash.Enabled = false;
            mnuPrint.Enabled = false;

            notesNode.Nodes.Clear();
            trashNode.Nodes.Clear();
            using (var proxy = new ServiceProxy(credential))
            {
                var result =
                    await
                    proxy.GetAsync(
                        @"api/notes/all?$filter=(DeletedString ne 'Deleted') and (DeletedString ne 'MarkDeleted')&$orderby=DatePublished desc");
                result.EnsureSuccessStatusCode();
                dynamic noteItems = JsonConvert.DeserializeObject(await result.Content.ReadAsStringAsync());
                foreach (dynamic note in noteItems)
                {
                    var treeNode = notesNode.Nodes.Add(
                        note.ID.ToString(),
                        note.Title.ToString(),
                        "Note.png",
                        "Note.png");
                    treeNode.Tag = note;
                }

                var markDeletedResult =
                    await
                    proxy.GetAsync(
                        @"api/notes/all?$filter=(DeletedString eq 'MarkDeleted')&$orderby=DatePublished desc");
                markDeletedResult.EnsureSuccessStatusCode();
                dynamic deletedNotes = JsonConvert.DeserializeObject(
                    await markDeletedResult.Content.ReadAsStringAsync());
                if (deletedNotes.Count > 0)
                {
                    mnuEmptyTrash.Enabled = true;
                    cmnuEmptyTrash.Enabled = true;
                    foreach (dynamic deletedNote in deletedNotes)
                    {
                        var treeNode = trashNode.Nodes.Add(
                            deletedNote.ID.ToString(),
                            deletedNote.Title.ToString(),
                            "DeletedNote.png",
                            "DeletedNote.png");
                        treeNode.Tag = deletedNote;
                    }
                }
            }

            if (notesNode.Nodes.Count > 0)
            {
                dynamic note = notesNode.Nodes[0].Tag;
                Guid id = note.ID;
                await this.LoadNoteAsync(id);

                tvNotes.SelectedNode = notesNode.Nodes[0];
            }
            else
            {
                ClearWorkspace();
            }

        }

        private void ClearWorkspace()
        {
            if (workspace != null)
            {
                workspace.PropertyChanged -= this.workspace_PropertyChanged;
                workspace = null;
            }
        }

        private async Task LoadNoteAsync(Guid id)
        {
            using (var proxy = new ServiceProxy(credential))
            {
                var result = await proxy.GetStringAsync(string.Format("api/notes/{0}", id));
                currentNote = JsonConvert.DeserializeObject(result);
                ClearWorkspace();
                workspace = new Workspace(currentNote);
                workspace.PropertyChanged += workspace_PropertyChanged;
                lblTitle.Text = workspace.Title;
                var datePublished = workspace.DatePublished;
                lblDatePublished.Text = datePublished.ToLocalTime().ToString("G", new CultureInfo(settings.General.Language));
                htmlEditor.Html = workspace.Content;
                htmlEditor.Enabled = true;
                tbtnSave.Enabled = false;
                mnuSave.Enabled = false;
                mnuPrint.Enabled = true;
            }
        }

        private async Task SaveWorkspaceSlientlyAsync()
        {
            var currentNoteId = workspace.ID;
            var currentNoteTitle = workspace.Title;

            string currentNoteContent = string.Empty;
            if (!string.IsNullOrEmpty(workspace.Content))
            {
                var content = ReplaceFileSystemImages(workspace.Content);
                currentNoteContent = crypto.Encrypt(content);
            }

            using (var proxy = new ServiceProxy(credential))
            {
                if (currentNoteId == Guid.Empty)
                {
                    var result =
                        await
                        proxy.PostAsJsonAsync(
                            "api/notes/create",
                            new { Title = currentNoteTitle, Content = currentNoteContent, Weather = "Unspecified" });
                    result.EnsureSuccessStatusCode();
                    workspace.ID = new Guid((await result.Content.ReadAsStringAsync()).Trim('\"'));
                }
                else
                {
                    var result =
                        await
                        proxy.PostAsJsonAsync(
                            "api/notes/update",
                            new
                                {
                                    ID = currentNoteId,
                                    Title = currentNoteTitle,
                                    Content = currentNoteContent,
                                    Weather = "Unspecified"
                                });
                    result.EnsureSuccessStatusCode();
                }
                workspace.IsSaved = true;

            }
        }

        private async Task<bool> SaveWorkspaceAsync()
        {
            var canceled = false;
            if (workspace != null)
            {
                if (!workspace.IsSaved)
                {
                    var saveConfirm = MessageBox.Show(
                        Resources.SaveConfirm,
                        Resources.Confirmation,
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                    switch (saveConfirm)
                    {
                        case DialogResult.Yes:
                            await this.SaveWorkspaceSlientlyAsync();
                            break;
                        case DialogResult.No:
                            break;
                        case DialogResult.Cancel:
                            canceled = true;
                            break;
                    }
                }
                //if (!canceled)
                //{
                //    workspace.PropertyChanged -= workspace_PropertyChanged;
                //    workspace = null;
                //}
            }
            return canceled;
        }

        private async Task OpenTreeNodeAsync(TreeNode treeNode)
        {
            var canceled = await this.SaveWorkspaceAsync();
            if (!canceled)
            {
                dynamic noteItem = treeNode.Tag;
                await this.LoadNoteAsync((Guid)noteItem.ID);
            }
            else
            {
                if (workspace != null)
                {
                    Parallel.ForEach(
                        notesNode.Nodes.Cast<TreeNode>(),
                        p =>
                        {
                            dynamic note = p.Tag;
                            if (workspace.ID == (Guid)note.ID) tvNotes.SelectedNode = p;
                        });
                }
            }
        }

        #region Async Handlers

        private async Task DoNewAsync()
        {
            try
            {
                var canceled = await this.SaveWorkspaceAsync();
                if (!canceled)
                {
                    var newNoteForm = new FrmNewNote(this.notesNode.Nodes.Cast<TreeNode>().Select(tn => tn.Text));
                    if (newNoteForm.ShowDialog() == DialogResult.OK)
                    {
                        var title = newNoteForm.NoteTitle;
                        dynamic note =
                            new
                                {
                                    ID = Guid.Empty,
                                    Title = title,
                                    Content = string.Empty,
                                    DatePublished = DateTime.UtcNow
                                };
                        ClearWorkspace();
                        this.workspace = new Workspace(note);
                        this.workspace.PropertyChanged += this.workspace_PropertyChanged;
                        await this.SaveWorkspaceSlientlyAsync();
                        await this.LoadNotesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
        }

        private async Task DoOpenAsync()
        {
            try
            {
                slblStatus.Text = Resources.Opening;
                sp.Visible = true;
                await this.OpenTreeNodeAsync(tvNotes.SelectedNode);
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
            finally
            {
                sp.Visible = false;
            }
        }

        private async Task DoSaveAsync()
        {
            if (!workspace.IsSaved)
            {
                try
                {
                    slblStatus.Text = Resources.Saving;
                    sp.Visible = true;
                    await this.SaveWorkspaceSlientlyAsync();
                }
                catch (Exception ex)
                {
                    FrmExceptionDialog.ShowException(ex);
                }
                finally
                {
                    sp.Visible = false;
                }
            }
        }

        private async Task DoMarkDeleteAsync()
        {
            try
            {
                slblStatus.Text = Resources.Deleting;
                sp.Visible = true;
                var treeNode = tvNotes.SelectedNode;
                if (treeNode != null)
                {
                    dynamic note = treeNode.Tag;
                    Guid id = note.ID;
                    using (var serviceProxy = new ServiceProxy(credential))
                    {
                        var result = await serviceProxy.PostAsJsonAsync("api/notes/markdelete", id);
                        result.EnsureSuccessStatusCode();
                        await this.CorrectNodeSelectionAsync(treeNode);
                        treeNode.Remove();
                        var markDeletedNoteNode = trashNode.Nodes.Add(
                            note.ID.ToString(),
                            note.Title.ToString(),
                            "DeletedNote.png",
                            "DeletedNote.png");
                        note.DeletedFlag = (int)DeleteFlagModel.MarkDeleted;
                        markDeletedNoteNode.Tag = note;
                        ResortNodes(trashNode);
                        if (trashNode.Nodes.Count > 0)
                        {
                            mnuEmptyTrash.Enabled = true;
                            cmnuEmptyTrash.Enabled = true;
                        }
                    }
                    mnuEmptyTrash.Enabled = true;
                    cmnuEmptyTrash.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
            finally
            {
                sp.Visible = false;
            }
        }

        private async Task DoDeleteAsync()
        {
            try
            {
                var confirmResult = MessageBox.Show(
                    Resources.DeleteNoteConfirm,
                    Resources.Confirmation,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (confirmResult == DialogResult.Yes)
                {
                    slblStatus.Text = Resources.Deleting;
                    sp.Visible = true;
                    var treeNode = tvNotes.SelectedNode;
                    if (treeNode != null)
                    {
                        dynamic note = treeNode.Tag;
                        Guid id = note.ID;
                        using (var serviceProxy = new ServiceProxy(credential))
                        {
                            var result = await serviceProxy.DeleteAsync(string.Format("api/notes/delete/{0}", id));
                            result.EnsureSuccessStatusCode();
                            await this.LoadNotesAsync();
                        }
                    }
                    if (trashNode.Nodes.Count == 0)
                    {
                        mnuEmptyTrash.Enabled = false;
                        cmnuEmptyTrash.Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
            finally
            {
                sp.Visible = false;
            }
        }

        private async Task DoRestoreAsync()
        {
            try
            {
                slblStatus.Text = Resources.Restoring;
                sp.Visible = true;
                var treeNode = tvNotes.SelectedNode;
                if (treeNode != null)
                {
                    dynamic note = treeNode.Tag;
                    Guid id = note.ID;
                    using (var serviceProxy = new ServiceProxy(credential))
                    {
                        var result = await serviceProxy.PostAsJsonAsync("api/notes/restore", id);
                        result.EnsureSuccessStatusCode();
                    }
                    await this.CorrectNodeSelectionAsync(treeNode);
                    treeNode.Remove();

                    var restoredNoteNode = notesNode.Nodes.Add(
                        note.ID.ToString(),
                        note.Title.ToString(),
                        "Note.png",
                        "Note.png");
                    note.DeletedFlag = (int)DeleteFlagModel.None;
                    restoredNoteNode.Tag = note;
                    ResortNodes(notesNode);
                    if (trashNode.Nodes.Count == 0)
                    {
                        mnuEmptyTrash.Enabled = false;
                        cmnuEmptyTrash.Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
            finally
            {
                sp.Visible = false;
            }
        }

        private async Task DoEmptyTrashAsync()
        {
            try
            {
                var confirmResult = MessageBox.Show(
                    Resources.DeleteNoteConfirm,
                    Resources.Confirmation,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (confirmResult == DialogResult.Yes)
                {
                    slblStatus.Text = Resources.Deleting;
                    sp.Visible = true;
                    using (var serviceProxy = new ServiceProxy(credential))
                    {
                        var result = await serviceProxy.DeleteAsync("api/notes/emptytrash");
                        result.EnsureSuccessStatusCode();
                        trashNode.Nodes.Clear();
                        if (notesNode.Nodes.Count > 0)
                        {
                            var firstNode = notesNode.Nodes[0];
                            dynamic firstNote = firstNode.Tag;
                            await this.LoadNoteAsync((Guid)firstNote.ID);
                        }
                        else
                        {
                            tvNotes.SelectedNode = notesNode;
                            lblTitle.Text = string.Empty;
                            lblDatePublished.Text = string.Empty;
                            htmlEditor.Enabled = false;
                            htmlEditor.Html = string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
            finally
            {
                sp.Visible = false;
            }
        }

        private void DoRename()
        {
            var node = this.tvNotes.SelectedNode;
            node.BeginEdit();
        }

        private async Task DoReconnectAsync()
        {
            bool canceled = false;
            var localCredential = LoginProvider.Login(delegate { canceled = true; }, settings, true);
            if (!canceled && localCredential != null)
            {
                this.credential.UserName = localCredential.UserName;
                this.credential.Password = localCredential.Password;
                this.credential.ServerUri = localCredential.ServerUri;
                this.Text = string.Format("CloudNotes - {0}@{1}", this.credential.UserName, this.credential.ServerUri);
                await this.LoadNotesAsync();
            }
        }

        #endregion

        private void workspace_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var obj = sender as Workspace;
            if (obj != null)
            {
                switch (e.PropertyName)
                {
                    case "Content":
                        mnuSave.Enabled = true;
                        tbtnSave.Enabled = true;
                        break;
                    case "IsSaved":
                        mnuSave.Enabled = !obj.IsSaved;
                        tbtnSave.Enabled = !obj.IsSaved;
                        break;
                }
            }
        }

        private async void Action_New(object sender, EventArgs e)
        {
            await this.DoNewAsync();
        }

        private async void Action_Open(object sender, EventArgs e)
        {
            await this.DoOpenAsync();
        }

        private async void Action_Save(object sender, EventArgs e)
        {
            await this.DoSaveAsync();
        }

        private void Action_Print(object sender, EventArgs e)
        {
            this.htmlEditor.ExecuteButtonFunction<PrintButton>();
        }

        private async void Action_Delete(object sender, EventArgs e)
        {
            var treeNode = tvNotes.SelectedNode;
            if (treeNode != null)
            {
                if (IsMarkedAsDeletedNoteNode(treeNode)) await this.DoDeleteAsync();
                else await this.DoMarkDeleteAsync();
            }
        }

        private async void Action_PermanentDelete(object sender, EventArgs e)
        {
            var treeNode = tvNotes.SelectedNode;
            if (treeNode != null)
            {
                await this.DoDeleteAsync();
            }
        }

        private async void Action_Restore(object sender, EventArgs e)
        {
            await this.DoRestoreAsync();
        }

        private async void Action_EmptyTrash(object sender, EventArgs e)
        {
            await this.DoEmptyTrashAsync();
        }

        private void Action_Rename(object sender, EventArgs e)
        {
            this.DoRename();
        }

        private async void Action_Reconnect(object sender, EventArgs e)
        {
            await this.DoReconnectAsync();
        }

        private void Action_ChangePassword(object sender, EventArgs e)
        {
            var changePasswordForm = new FrmChangePassword(credential);
            changePasswordForm.ShowDialog();
        }

        private void Action_Settings(object sender, EventArgs e)
        {
            var settingsForm = new FrmSettings(this.settings);
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                this.UpdateSettings();
            }
        }

        private void Action_OpenMainWindow(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
        }

        private void Action_Exit(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Action_About(object sender, EventArgs e)
        {
            new FrmAbout().ShowDialog();
        }

        private void Action_CloudNotesTech(object sender, EventArgs e)
        {
            "http://daxnetsvr.cloudapp.net/schen/cloudnotes".Navigate();
        }

        private async void FrmMain_Load(object sender, EventArgs e)
        {
            try
            {
                slblStatus.Text = Resources.Loading;
                sp.Visible = true;
                var desktopClientService = new DesktopClientService(this.settings);
                this.checkUpdateResult = await desktopClientService.CheckUpdateAsync();
                slblUpdateAvailable.Visible = this.checkUpdateResult.HasUpdate;
                await this.LoadNotesAsync();
            }
            catch (Exception ex)
            {
                FrmExceptionDialog.ShowException(ex);
            }
            finally
            {
                sp.Visible = false;
            }
        }

        private async void tvNotes_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Node != null && !ReferenceEquals(e.Node, notesNode)
                && !ReferenceEquals(e.Node, trashNode))
            {
                try
                {
                    slblStatus.Text = Resources.Opening;
                    sp.Visible = true;
                    await this.OpenTreeNodeAsync(e.Node);
                }
                catch (Exception ex)
                {
                    FrmExceptionDialog.ShowException(ex);
                }
                finally
                {
                    sp.Visible = false;
                }
            }
        }

        private async void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If the closing event has not received the closing signal
            if (!closingSignal)
            {
                // Suppress the form from closing
                e.Cancel = true;

                // awaiting for the workspace saving task
                var canceled = await this.SaveWorkspaceAsync();

                // If the user didn't cancel the closing operation
                if (!canceled)
                {
                    // Signal the form's closing event
                    closingSignal = true;
                    // Closes the form
                    this.Close();
                }
            }
            else e.Cancel = false;
        }

        private void tvNotes_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Right:
                    if (e.Node == notesNode)
                    {
                        ctxMnuNotesNode.Show(tvNotes, e.X, e.Y);
                    }
                    else if (e.Node == trashNode)
                    {
                        ctxMnuTrashNode.Show(tvNotes, e.X, e.Y);
                    }
                    else
                    {
                        cmnuRestore.Visible = IsMarkedAsDeletedNoteNode(e.Node);
                        ctxMnuNoteNode.Show(tvNotes, e.X, e.Y);
                    }
                    break;
            }
            tvNotes.SelectedNode = e.Node;
        }

        private void tvNotes_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null && !ReferenceEquals(e.Node, notesNode) && !ReferenceEquals(e.Node, trashNode))
            {
                mnuOpen.Enabled = true;
                tbtnOpen.Enabled = true;
                mnuDelete.Enabled = true;
                tbtnDelete.Enabled = true;
                mnuPermanentDelete.Enabled = true;
                mnuRename.Enabled = true;
                tbtnRename.Enabled = true;
                mnuRestore.Enabled = IsMarkedAsDeletedNoteNode(e.Node);
                tbtnRestore.Enabled = IsMarkedAsDeletedNoteNode(e.Node);
            }
            else
            {
                mnuOpen.Enabled = false;
                tbtnOpen.Enabled = false;
                mnuDelete.Enabled = false;
                tbtnDelete.Enabled = false;
                mnuPermanentDelete.Enabled = false;
                mnuRestore.Enabled = false;
                tbtnRestore.Enabled = false;
                mnuRename.Enabled = false;
                tbtnRename.Enabled = false;
            }
        }

        private void FrmMain_Resize(object sender, EventArgs e)
        {
            this.ShowInTaskbar = this.WindowState != FormWindowState.Minimized;
        }

        private async void tvNotes_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (!e.CancelEdit)
            {
                try
                {
                    var title = e.Label;
                    if (string.IsNullOrEmpty(title)) return;
                    slblStatus.Text = Resources.Renaming;
                    sp.Visible = true;
                    if (notesNode.Nodes.Cast<TreeNode>().Any(n => n != e.Node && n.Text == title))
                    {
                        MessageBox.Show(
                            Resources.TitleExists,
                            Resources.Error,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        e.CancelEdit = true;
                    }
                    else
                    {
                        dynamic nodeMetadata = e.Node.Tag;
                        Guid id = nodeMetadata.ID;
                        using (var proxy = new ServiceProxy(credential))
                        {
                            var getNoteResult = await proxy.GetStringAsync(string.Format("api/notes/{0}", id));
                            dynamic selectedNote = JsonConvert.DeserializeObject(getNoteResult);
                            var result =
                                await
                                proxy.PostAsJsonAsync(
                                    "api/notes/update",
                                    new { ID = id, Title = title, selectedNote.Content, Weather = "Unspecified" });
                            result.EnsureSuccessStatusCode();
                            lblTitle.Text = title;
                        }
                    }
                }
                catch (Exception ex)
                {
                    FrmExceptionDialog.ShowException(ex);
                }
                finally
                {
                    sp.Visible = false;
                }
            }
        }

        private void tvNotes_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (ReferenceEquals(e.Node, notesNode) || ReferenceEquals(e.Node, trashNode))
            {
                e.CancelEdit = true;
            }
        }

        private async void slblUpdateAvailable_Click(object sender, EventArgs e)
        {
            if (this.checkUpdateResult != null)
            {
                var canceled = await this.SaveWorkspaceAsync();
                if (!canceled)
                {
                    var updatePackageForm = new FrmUpdatePackage(this.checkUpdateResult.UpdatePackage);
                    updatePackageForm.ShowDialog();
                }
            }
        }
    }
}
