﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.ServiceModel;
using Client.FileSystemServiceReference;
using System.Threading;
using System.Diagnostics;
using NLog;

namespace Client
{
    public partial class MainForm : Form
    {
        List<MyFile> files = new List<MyFile>();

        Sinchronize sinchronize = new Sinchronize();
        ProgressForm progressForm = new ProgressForm();

        private static Logger logger = LogManager.GetCurrentClassLogger();

        
        public MainForm()
        {
            InitializeComponent();

            // установка культуры
            Localization.Init();
            LocalizateForm();

            InitializeSinchronize();
            this.ShowInTaskbar = false;

            logger.Debug("Старт приложения");
            
        }

        // локализация формы
        public void LocalizateForm()
        {
            Localization.LocalizeForm(this, Localization.GetPropertiesCulture());
        }
        

        //инициализация класса синхронизации
        private void InitializeSinchronize()
        {
            logger.Debug("Инициализация класса синхронизации");
            sinchronize.OnChangeSinchronizeStatus += sinchronize_OnChangeSinchronizeStatus;
            sinchronize.OnProcessFileInfo += sinchronize_OnProcessFileInfo;
            sinchronize.OnCreateFileListForSincronization += sinchronize_OnCreateChangedFilesList;
        }
        
       
        //получен список файлов для синхронизации
        void sinchronize_OnCreateChangedFilesList(Sinchronize sender, List<MyFile> changedFileList)
        {
            if (changedFileList.Count == 0)
                return;
            
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new CreateFileListForSincronization(sinchronize_OnCreateChangedFilesList), new object[] { sender, changedFileList });
                return;
            }

            FillListView(changedFileList);
            notifyIcon.ShowBalloonTip(200, "Sinchosaur", changedFileList.Count + Localization.GetFormCultureString(this, "CountFilesForSynch"), ToolTipIcon.Info);
            logger.Debug("Сформирован список из {0} файлов для синхронизации", changedFileList.Count);
        }
        

        //обработан файл
        void sinchronize_OnProcessFileInfo(Sinchronize sender, ProgressFileInfo progressFileInfo)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new ProcessFileInfo(sinchronize_OnProcessFileInfo), new object[] { sender, progressFileInfo });
                return;
            }
            string action= "Загрузка"; 
            switch (progressFileInfo.Action)
            {
                case FileStatus.Upload:
                    action = "Отправка на сервер";
                    break;
                case FileStatus.Delete:
                    action = "Удаление";
                    break;
                case FileStatus.Download:
                    action = "Получение с сервера";
                    break;
            }
            logger.Debug(action + " " + progressFileInfo.File.Name);
        }
        

        //Изменен состояние синхронизации
        void sinchronize_OnChangeSinchronizeStatus(Sinchronize sender, SinchronizeStatus status)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new ChangeSinchronizeStatus(sinchronize_OnChangeSinchronizeStatus), new object[] { sender, status });
                return;
            }
            
            switch (status)
            {
                case SinchronizeStatus.SinchronizeStarted: // начало синхронизации
                    notifyIcon.Text = "Sinchosaur\n" + Localization.GetFormCultureString(this, "SynchProcesing");
                    progressForm =new ProgressForm(sender.SinchronizeFileProgressInfo) ;
                    notifyIcon.Icon = Resurces.package_update;
                    trayContextMenu.Items[1].Visible = true;
                    timerSinchronize.Enabled = false;
                    logger.Info("Синхронизация начата");
                    break;

                case SinchronizeStatus.SinchronizeFinished: // если синхронизация закончена
                    progressForm.Dispose();
                    notifyIcon.Icon = Resurces.package_ok;
                    trayContextMenu.Items[1].Visible = true;
                    timerSinchronize.Enabled = true;
                    logger.Info("Синхронизация завершена");
                    break;

                case SinchronizeStatus.ServerNotAvailable: // сервер не доступен
                    trayContextMenu.Items[1].Visible = false;
                    notifyIcon.Text = "Sinchosaur\n" + Localization.GetFormCultureString(this, "ServerNotAvailable");
                    progressForm.Dispose();
                    notifyIcon.Icon = Resurces.package_bad;
                    trayContextMenu.Items[1].Visible = false;
                    timerSinchronize.Enabled = true;
                    logger.Trace("Сервер не доступен");
                    break;

                case SinchronizeStatus.NoFilesChanges: // нет изменений
                    notifyIcon.Text = "Sinchosaur\n" + Localization.GetFormCultureString(this, "AllFilesDone");
                    notifyIcon.Icon = Resurces.package_ok;
                    trayContextMenu.Items[1].Visible = true;
                    timerSinchronize.Enabled = true;
                    logger.Trace("Все файлы синхронизированы");
                    break;

                case SinchronizeStatus.DiskSpacelimit: // Файл добавить нельзя, так как закончилось место на диске
                    notifyIcon.Text = "Sinchosaur\n bad" ;
                    notifyIcon.Icon = Resurces.package_bad;
                    trayContextMenu.Items[1].Visible = true;
                    timerSinchronize.Enabled = true;
                    notifyIcon.ShowBalloonTip(200, "Sinchosaur", "Не могу залить файл " + sender.SinchronizeFileProgressInfo.File.Name + ", лимит на сервере", ToolTipIcon.Info);
                    logger.Warn("Не могу залить файл " + sender.SinchronizeFileProgressInfo.File.ToString() + ", лимит на сервере");
                    break;

                case SinchronizeStatus.GetServerFilesList: // Получение списка файлов на сервере
                    trayContextMenu.Items[1].Visible = false;
                    timerSinchronize.Enabled = false;
                    progressForm.Dispose();
                    break;

                case SinchronizeStatus.UserNotExist: // такой пользователь не существует на сервере
                    trayContextMenu.Items[1].Visible = false;
                    timerSinchronize.Enabled = false;
                    notifyIcon.Icon = Resurces.package_bad;
                    notifyIcon.ShowBalloonTip(200, "Sinchosaur", Localization.GetFormCultureString(this, "UserNotExist"), ToolTipIcon.Info);
                    logger.Warn("Такой пользователь не существует");
                    
                    SettingForm settingsForm = new SettingForm();
                    settingsForm.Disposed += new EventHandler(settingsForm_Deactivate);
                    settingsForm.Activate();
                    settingsForm.Show();
                    break;

                case SinchronizeStatus.ServerUrlNotCorrect: // указан некорректный ip адресс сервера
                    trayContextMenu.Items[1].Visible = true;
                    timerSinchronize.Enabled = false;
                    notifyIcon.Icon = Resurces.package_bad;
                    notifyIcon.ShowBalloonTip(200, "Sinchosaur", Localization.GetFormCultureString(this, "ServerIpNotCorrect"), ToolTipIcon.Info);
                    logger.Warn("Указан не корректный ip-адрес сервера");

                    settingsForm = new SettingForm();
                    settingsForm.Disposed += new EventHandler(settingsForm_Deactivate);
                    settingsForm.Activate();
                    settingsForm.Show();
                    break;
             }
        }
        

        //заполняет на форме список файлов
        private void FillListView(List<MyFile> files)
        {
            int i = 0;
            listServerFiles.Items.Clear();
            foreach (MyFile file in files)
            {
                ListViewItem item = new ListViewItem(i.ToString());
                item.SubItems.Add(file.Name);
                item.SubItems.Add(Properties.Settings.Default.StorageFolder+file.Path);
                item.SubItems.Add((file.Size / 1024+1).ToString() + " (кб)");
                item.SubItems.Add((file.status).ToString());
                listServerFiles.Items.Add(item);
                i++;
            }
        }
        

        
        //обработчик ресайза формы
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == WindowState)
            {
                WindowState = FormWindowState.Minimized;
                notifyIcon.Visible = true;
                Hide();
            }
        }
        

        //синхронизация файлов
        private void timerSinchronize_Tick(object sender, EventArgs e)
        {
           // new Thread(new ThreadStart(sinchronize.Start)).Start();
            Thread thread =  new Thread(new ThreadStart(sinchronize.Start));
            thread.IsBackground=true;
            thread.Start();
        }
        

        //клик по иконке в трее
        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            Process.Start(Properties.Settings.Default.StorageFolder);
        }

        // деактивация окна настроек
        void settingsForm_Deactivate(object sender, EventArgs e)
        {
            LocalizateForm();
            timerSinchronize.Enabled = true;
        }

        #region  Context menu

        
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            logger.Info("Завершение приложения");
            Application.Exit();
        }

        
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Process.Start(Properties.Settings.Default.StorageFolder);
        }

        
        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            // отлючение синхронизаци
            timerSinchronize.Enabled = false;
            SettingForm settingsForm = new SettingForm();
            settingsForm.Disposed += new EventHandler(settingsForm_Deactivate);
            settingsForm.Activate();
            settingsForm.Show();
            
        }

        #endregion

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            new Thread(new ThreadStart(sinchronize.Start)).Start();
        }

        private void startTimerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timerSinchronize.Enabled = true;
        }

        private void stopTimerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            timerSinchronize.Enabled = false;
        }

        private void eventsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EventForm eventForm = new EventForm();
            eventForm.Show();
            eventForm.FillListView();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm aboutForm = new AboutForm();
            aboutForm.Show();
        }
        
    }
}
