using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QuickFix;
using QuickFix.Transport;
using Message = QuickFix.Message;

namespace FixInitiator
{
    public partial class frmMain : Form
    {
        private FixClient50Sp2 _client;

        public frmMain()
        {
            InitializeComponent();
        }


        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

           
            // FIX app settings and related
            var settings = new SessionSettings("C:\\initiator.cfg");

            // FIX application setup
            MessageStoreFactory storeFactory = new FileStoreFactory(settings);
            LogFactory logFactory = new FileLogFactory(settings);
            _client = new FixClient50Sp2(settings);

            IInitiator initiator = new SocketInitiator(_client, storeFactory, settings, logFactory);
            _client.Initiator = initiator;

            _client.OnProgress += _client_OnProgress;
            _client.LogonEvent += ClientLogonEvent;
            _client.MessageEvent += ClientMessageEvent;
            _client.LogoutEvent += ClientLogoutEvent;
            _client.OnMarketDataIncrementalRefresh += Client_OnMarketDataIncrementalRefresh;
            
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            _client.Stop();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            //_client.onst
            _client.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_client != null && _client.Initiator.IsLoggedOn())
                _client.Initiator.Stop();

            base.OnClosing(e);
        }

        private void _client_OnProgress(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(_client_OnProgress), msg);
                return;
            }

            AddItem(msg);
        }

      
        private void ClientLogoutEvent()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClientLogoutEvent), null);
                return;
            }
            AddItem("Log out called");
            enableControls(false);
        }

        private void AddItem(string message)
        {
            if (listBox1.Items.Count > 50)
                listBox1.Items.Clear();

            listBox1.Items.Add(message);
        }

        private void ClientLogonEvent()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClientLogonEvent), null);
                return;
            }

            AddItem("Logged on");

            enableControls(true);
        }

        private void enableControls(bool enable)
        {
            btnDisconnect.Enabled = enable;
            btnStartMarketPrice.Enabled = enable;
            // btnUpdateSecurities.Enabled = enable;
            btnConnect.Enabled = !enable;
        }

        private void ClientMessageEvent(Message arg1, bool arg2)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Message, bool>(ClientMessageEvent), arg1, arg2);
                return;
            }
            AddItem(arg1.ToString());
        }


        private void Client_OnMarketDataIncrementalRefresh(MarketPrice obj)
        {
            AddItem(obj.ToString());
        }

        private void LogError(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(LogError), msg);
                return;
            }
            AddItem(msg);
        }

        private void Client_OnMarketDataSnapshotFullRefresh(MarketPrice obj)
        {
            AddItem(obj.ToString());
        }


        private void btnStartMarketPrice_Click(object sender, EventArgs e)
        {
            if (btnStartMarketPrice.Text == "&Start Marketprice")
            {
                //TODO: this is sample symbol id, it can be replaced by real id
                string sym = "MSFT";
                _client.Subscribe(sym, _client.ActiveSessionId);

                btnStartMarketPrice.Text = "&Stop";
            }
            else
            {
                btnStartMarketPrice.Text = "&Start Marketprice";

            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}