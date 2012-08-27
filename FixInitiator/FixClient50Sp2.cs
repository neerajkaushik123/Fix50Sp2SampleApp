using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using QuickFix;
using QuickFix.FIX50;
using QuickFix.Fields;
using Application = QuickFix.Application;
using Message = QuickFix.Message;
using SecurityStatus = QuickFix.FIX50.SecurityStatus;

namespace FixInitiator
{
    public class FixClient50Sp2 : MessageCracker, Application
    {

        private IInitiator _initiator;
        private int _subscriptionId;
        private int _securityrequestid = 1;

        public FixClient50Sp2(SessionSettings settings)
        {
            ActiveSessionId = null;
        }

        public SessionID ActiveSessionId { get; set; }

        public IInitiator Initiator
        {
            set
            {
                if (_initiator != null)
                    throw new Exception("You already set the initiator");
                _initiator = value;
            }
            get
            {
                if (_initiator == null)
                    throw new Exception("You didn't provide an initiator");
                return _initiator;
            }
        }

        #region Application Members
        /// <summary>
        /// every inbound admin level message will pass through this method, 
        /// such as heartbeats, logons, and logouts. 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionId"></param>
        public void FromAdmin(Message message, SessionID sessionId)
        {
            Log(message.ToString());
        }


        /// <summary>
        /// every inbound application level message will pass through this method, 
        /// such as orders, executions, secutiry definitions, and market data.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void FromApp(Message message, SessionID sessionID)
        {
            Trace.WriteLine("## FromApp: " + message);

            Crack(message, sessionID);
        }

        /// <summary>
        /// this method is called whenever a new session is created.
        /// </summary>
        /// <param name="sessionID"></param>
        public void OnCreate(SessionID sessionID)
        {
            if (OnProgress != null)
                Log(string.Format("Session {0} created", sessionID));
        }

        /// <summary>
        /// notifies when a successful logon has completed.
        /// </summary>
        /// <param name="sessionID"></param>
        public void OnLogon(SessionID sessionID)
        {
            ActiveSessionId = sessionID;
            Trace.WriteLine(String.Format("==OnLogon: {0}==", ActiveSessionId));

            if (LogonEvent != null)
                LogonEvent();
        }

        /// <summary>
        /// notifies when a session is offline - either from 
        /// an exchange of logout messages or network connectivity loss.
        /// </summary>
        /// <param name="sessionID"></param>
        public void OnLogout(SessionID sessionID)
        {
            // not sure how ActiveSessionID could ever be null, but it happened.
            string a = (ActiveSessionId == null) ? "null" : ActiveSessionId.ToString();
            Trace.WriteLine(String.Format("==OnLogout: {0}==", a));

            if (LogoutEvent != null)
                LogoutEvent();
        }


        /// <summary>
        /// all outbound admin level messages pass through this callback.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void ToAdmin(Message message, SessionID sessionID)
        {
            Log("To Admin : " + message);
        }

        /// <summary>
        /// all outbound application level messages pass through this callback before they are sent. 
        /// If a tag needs to be added to every outgoing message, this is a good place to do that.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionId"></param>
        public void ToApp(Message message, SessionID sessionId)
        {
            Log("To App : " + message);
        }

        #endregion

        public event Action LogonEvent;
        public event Action LogoutEvent;

        public event Action<MarketPrice> OnMarketDataIncrementalRefresh;
        public event Action<string> OnProgress;

        /// <summary>
        /// Triggered on any message sent or received (arg1: isIncoming)
        /// </summary>
        public event Action<Message, bool> MessageEvent;


        public void Log(string message)
        {
            Trace.WriteLine(message);

            if (OnProgress != null)
                OnProgress(message);
        }

        public void Start()
        {
            Log("Application starting....");

            if (Initiator.IsStopped)
                Initiator.Start();
            else
                Log("(already started)");
        }

        public void Stop()
        {
            Log("Stopping.....");

            Initiator.Stop();
        }

        /// <summary>
        /// Tries to send the message; throws if not logged on.
        /// </summary>
        /// <param name="m"></param>
        public void Send(Message m)
        {
            if (Initiator.IsLoggedOn() == false)
                throw new Exception("Can't send a message.  We're not logged on.");
            if (ActiveSessionId == null)
                throw new Exception("Can't send a message.  ActiveSessionID is null (not logged on?).");

            Session.SendToTarget(m, ActiveSessionId);
        }


        public void Subscribe(string symbol, SessionID sessionId)
        {

           var marketDataRequest = new MarketDataRequest
                                        {
                                            MDReqID = new MDReqID(symbol),
                                            SubscriptionRequestType = new SubscriptionRequestType('1'),
                                            //incremental refresh
                                            MarketDepth = new MarketDepth(1), //yes market depth need
                                            MDUpdateType = new MDUpdateType(1) //
                                        };

            var relatedSymbol = new MarketDataRequest.NoRelatedSymGroup { Symbol = new Symbol(symbol) };

            marketDataRequest.AddGroup(relatedSymbol);

            var noMdEntryTypes = new MarketDataRequest.NoMDEntryTypesGroup();

            var mdEntryTypeBid = new MDEntryType('0');

            noMdEntryTypes.MDEntryType = mdEntryTypeBid;

            marketDataRequest.AddGroup(noMdEntryTypes);

            noMdEntryTypes = new MarketDataRequest.NoMDEntryTypesGroup();

            var mdEntryTypeOffer = new MDEntryType('1');

            noMdEntryTypes.MDEntryType = mdEntryTypeOffer;

            marketDataRequest.AddGroup(noMdEntryTypes);

            //Send message
            Session.SendToTarget(marketDataRequest, sessionId);
        }


        public void OnMessage(MarketDataRequestReject message, SessionID session)
        {
            Trace.WriteLine("MarketDataRequestReject" + message);

            if (MessageEvent != null)
                MessageEvent(message, false);
        }

        public void OnMessage(MarketDataIncrementalRefresh message, SessionID session)
        {
            var noMdEntries = message.NoMDEntries;
            var listOfMdEntries = noMdEntries.getValue();
            //message.GetGroup(1, noMdEntries);
            var group = new MarketDataIncrementalRefresh.NoMDEntriesGroup();

            Group gr = message.GetGroup(1, group);

            string sym = message.MDReqID.getValue();

            var price = new MarketPrice();


            for (int i = 1; i <= listOfMdEntries; i++)
            {
                group = (MarketDataIncrementalRefresh.NoMDEntriesGroup)message.GetGroup(i, group);

                price.Symbol = group.Symbol.getValue();

                MDEntryType mdentrytype = group.MDEntryType;

                if (mdentrytype.getValue() == '0') //bid
                {
                    decimal px = group.MDEntryPx.getValue();
                    price.Bid = px;
                }
                else if (mdentrytype.getValue() == '1') //offer
                {
                    decimal px = group.MDEntryPx.getValue();
                    price.Offer = px;
                }

                price.TimeStamp = group.MDEntryTime.ToString();
            }

            if (OnMarketDataIncrementalRefresh != null)
            {
                OnMarketDataIncrementalRefresh(price);
            }
        }
    }
}