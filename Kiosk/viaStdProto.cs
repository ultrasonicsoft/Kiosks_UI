using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Kiosk
{
    public class ViaStdProto : IThreadable
    {
        // Nested Classes
        // ========================================================================================
        private class ViaTelegram
        {
            private int m_length;
            private string m_id;
            private string m_src;
            private string m_dest;
            private int m_seq;
            private string m_data;

            // default constructor
            public ViaTelegram()
            {
                m_length = 0;
                m_id = "";
                m_src = "";
                m_dest = "";
                m_seq = 0;
                m_data = "";
            }
            // assign data to this object
            public void Assign(string tele)
            {
                try
                {
                    m_length = 0;
                    m_id = "";
                    m_src = "";
                    m_dest = "";
                    m_seq = 0;
                    m_data = "";

                    m_length = Convert.ToInt32(tele.Substring(0, 4));

                    if (m_length < 6 * 4)
                    {
                        m_length = 0;
                        return;
                    }

                    if (tele.Substring(m_length - 4, 4) != "ETX_")
                    {
                        m_length = 0;
                        return;
                    }

                    m_id = tele.Substring(4, 4);
                    m_src = tele.Substring(8, 4);
                    m_dest = tele.Substring(12, 4);

                    m_seq = Convert.ToInt32(tele.Substring(16, 2));

                    m_data = tele.Substring(20, m_length - (6 * 4));
                }
                catch (Exception e)
                {
                    //VlsMain.VlsEventLog.WriteEntry(LogTools.getExceptionString("ViaStdProto", "Assign", e));
                    
                    throw (e);
                }

                return;
            }

            // Check if this is a DATA telegram
            public bool isData()
            {
                try
                {
                    if (m_length != 0 && m_id == "DATA") return true;
                }
                catch (Exception e)
                {
                    //VlsMain.VlsEventLog.WriteEntry(LogTools.getExceptionString("ViaStdProto", "isData", e));
                    // m_socketLog.log(LogTools.getExceptionString("viaStdProto","isData",e);
                    throw (e);
                }
                return false;
            }

            // Check if this is an ACKN telegram
            public bool isAck()
            {
                try
                {
                    if (m_length != 0 && m_id == "ACKN") return true;
                }
                catch (Exception e)
                {
                     //VlsMain.VlsEventLog.WriteEntry(LogTools.getExceptionString("ViaStdProto", "isAck", e));
                    throw (e);
                }
                return false;
            }

            // Check if this is a NAKN telegram
            public bool isNak()
            {
                try
                {
                    if (m_length != 0 && m_id == "NAKN") return true;
                }
                catch (Exception e)
                {
                    //VlsMain.VlsEventLog.WriteEntry(LogTools.getExceptionString("ViaStdProto", "isNak", e));
                    throw (e);
                }
                return false;
            }

            // Check if this is a keepalive telegram
            public bool isKeepAlive()
            {
                try
                {
                    if (isData())
                    {
                        if (m_data.Substring(0, 2) == "00") return true;
                    }
                }
                catch (Exception e)
                {
                    //VlsMain.VlsEventLog.WriteEntry(LogTools.getExceptionString("ViaStdProto", "isKeepAlive", e));
                    throw (e);
                }
                return false;
            }
            public int getLength() { return m_length; }
            public int getSeqNumber() { return m_seq; }
            public string getData() { return m_data; }
        }
        private class CSocketPacket
        {
            // Client  socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 1024*4;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];
        }

        // Enum Declarations
        // ========================================================================================
        public enum Status
        {
            NotConnected,
            WaitingForConnection,
            Connected
        }
        public enum SeqNumStatus
        {
            OK,
            DUPLICATE,
            INVALID_ORDER
        }      
        //public enum ReadState
        //{
        //    READY
        //}
        public enum WriteState
        {
            READY,
            WAIT_ASYNCH_DATASEND,
            WAIT_ASYNCH_ACKSEND,
            WAIT_ACK,
            WAIT_ASYNCH_INTERRUPT
        }
       
        // Private Fields
        // ========================================================================================
        private string m_threadName = "socket";
        
        // Volatile is used as hint to the compiler that this data member will be accessed by
        // multiple threads.
        private volatile bool m_exit;

        private bool m_server;
        private bool m_binary;
        private char m_fillchar;
        private int m_port;
        private int m_datalen;
        private int m_recSeqNum;
        private int m_sendSeqNum;
        private Status m_status;
        private AsyncCallback pfnWorkerCallBack;
        private string m_srcid;
        private string m_destid;
        private string m_keepaliveStr;
        private string m_recvAccumulator;
        private Socket m_socListener;
        private Socket m_socWorker;
        private IPAddress m_addr;
        private TimeSpan m_toacknak; 
        private TimeSpan m_toKeepaliveSend;
        private TimeSpan m_toKeepaliveRecv;
        private ViaTelegram m_readBuf;
        private ViaTelegram m_writeBuf;
        private LogClient m_socketLog;
        private ConcurrentQueue<string> m_recQ;     // queue for storing messages to upper level software
        private ConcurrentQueue<string> m_readQ;    // queue for internal use (internal to class), or from socket
        private DateTime m_lastConnected;

        private WriteState m_writeState;
        //private ReadState m_readState;
        private ConcurrentQueue<int> m_ackWriteQueue;
        private ConcurrentQueue<string> m_dataWriteQueue;
        private DateTime m_lastSend;
        private DateTime m_lastRecieve;
        private TimeSpan m_sendTimeout;
        private byte[] m_sendBuffer;
        private int m_numResends;

        // Public Methods
        // ========================================================================================

        // ----------------------------------------------------------------------------------------
        // Constructor with all fields
        //      addr: IP address
        //      port: port number
        //      server: true if we are the server, false if we are the client
        //      srcid: our id Ex: "VLS1"
        //      destid: id of target Ex: "MFC1", "IOC1" ...
        //      toacknak: time to wait for ack before re-sending (in seconds)
        //      datalen: length of telegram, pad telegram if shorter than datalen.
        //      fillchar: character to pad with
        //      binary: true: telegrams are binary (IO) false: telegrams are ASCII (data)
        //      tokeepalive: time to wait before sending keep alive mesage (seconds)  0 = disabled
        // ----------------------------------------------------------------------------------------
        public ViaStdProto(IPAddress addr, int port, bool server, string srcid, string destid, int toacknak, int datalen, char fillchar, bool binary, int tokeepalive)
        {
            m_addr = addr;
            m_port = port;
            m_server = server;
            m_srcid = srcid;
            m_destid = destid;
            m_datalen = datalen;
            m_fillchar = fillchar;
            m_binary = binary;
            m_recSeqNum = 0;
            m_sendSeqNum = 1;
            m_readBuf = new ViaTelegram();
            m_writeBuf = new ViaTelegram();
            m_numResends = 0;

            m_recQ = new ConcurrentQueue<string>();
            m_readQ = new ConcurrentQueue<string>();
            m_dataWriteQueue = new ConcurrentQueue<string>();
            m_ackWriteQueue = new ConcurrentQueue<int>();

            m_keepaliveStr = "00 keep alive telegram ";
            m_socketLog = null;// MainWindow.logBook.requestLog("Kiosk_Socket" + m_port + ".log");

            m_socketLog.log(LogTools.getStatusString("ViaStdProto", "ViaStdProto",
                "Creating socket: \n" + "Addr = " + m_addr +
                "\tPort = " + m_port + "\tServer = " + m_server +
                "\tSrc ID = " + m_srcid + "\tDest ID = " + m_destid +
                "\tData Length = " + m_datalen + "\tBinary = " + m_binary));

            m_recvAccumulator = "";

            if (toacknak <= 0)
            {
                toacknak = 5000; //default the value to 5 seconds if no value was passed
            }
            m_toacknak = new TimeSpan(0, 0, 0, 0, toacknak);
            m_sendTimeout = m_toacknak;

            m_toKeepaliveSend = new TimeSpan(0, 0, tokeepalive);
            m_toKeepaliveRecv = new TimeSpan(0, 0, 2*tokeepalive);

            m_lastConnected = DateTime.Now;
        }

        #region IThreadable Members
        public void threadLoop()
        {
            try
            {
                Thread.CurrentThread.Name = m_threadName + m_port;
                Thread.SetData(Thread.GetNamedDataSlot("Logclient"), m_socketLog);

                m_socketLog.log(LogTools.getStatusString("ViaStdProto", "threadLoop", "ViaStdProto Thread (" + Thread.CurrentThread.Name + ") is starting"));

                int c = 0; // for profiler

                while (!m_exit)
                {
                    //// For profiler
                    c = count(c);

                    switch (m_status)
                    {
                        case Status.NotConnected:
                            if (m_server)
                            {
                                listen();
                            }
                            else // client
                            {
                                connect();
                            }
                            break;
                        case Status.WaitingForConnection:
                            // do nothing, keep waiting
                            break;
                        case Status.Connected:
                            try
                            {
                                readStateMachine();
                                writeStateMachine();
                                checkKeepAlive();
                            }
                            catch (Exception ex)
                            {
                                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "threadLoop", ex));
                                disconnect();
                            }
                            break;
                        default:
                            throw new NotImplementedException("m_status = <" + m_status.ToString() + ">");
                    }

                    // if there is nothing to send, or if not connected, sleep
                    if ((m_ackWriteQueue.IsEmpty && m_dataWriteQueue.IsEmpty) || m_status != Status.Connected)
                    {
                        Thread.Sleep(50);
                    }

                }
            }
            catch (ThreadAbortException ex)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "threadLoop", ex, "ViaStdProto Thread is stopping"));
            }
        }

        private void writeStateMachine()
        {
            switch (m_writeState)
            {
                case WriteState.READY:
                    string msg;
                    int seqNum;

                    m_numResends = 0;
                    // check for ACKN
                    if (m_ackWriteQueue.TryDequeue(out seqNum))
                    {
                        m_sendBuffer = Encoding.ASCII.GetBytes(buildAck(seqNum, "0000"));
                        m_writeState = WriteState.WAIT_ASYNCH_ACKSEND;
                    }
                    // check for DATA messages
                    else if (m_dataWriteQueue.TryDequeue(out msg))
                    {
                        m_sendBuffer = Encoding.ASCII.GetBytes(buildData(m_sendSeqNum, msg));
                        m_writeState = WriteState.WAIT_ASYNCH_DATASEND;
                    }
                    // if nothing else to do, check for keep alive
                    else if (m_toKeepaliveSend > TimeSpan.Zero && m_lastSend + m_toKeepaliveSend < DateTime.Now)
                    {
                        m_sendBuffer = Encoding.ASCII.GetBytes(buildData(m_sendSeqNum, m_keepaliveStr));
                        m_writeState = WriteState.WAIT_ASYNCH_DATASEND;
                    }
                    // nothing to send
                    else
                    {
                        m_sendBuffer = null;
                    }

                    if (m_sendBuffer != null && m_sendBuffer.Length > 0)
                    {
                        
                        m_socWorker.BeginSend(m_sendBuffer, 0, m_sendBuffer.Length, 0, new AsyncCallback(SendCallback), m_socWorker);
                        m_lastSend = DateTime.Now; // set both here and in callback; do after so it doesn't have to be reset on exception
                        m_socketLog.log(LogTools.getCardString("ViaStdProto", "writeStateMachine", "Begin Send <" + Encoding.ASCII.GetString(m_sendBuffer) + ">"));
                    }
                    break;
                case WriteState.WAIT_ASYNCH_DATASEND:
                case WriteState.WAIT_ASYNCH_INTERRUPT:
                case WriteState.WAIT_ASYNCH_ACKSEND:
                    if (m_lastSend + m_sendTimeout < DateTime.Now)
                    {
                        m_socketLog.log(LogTools.getErrorString("ViaStdProto", "writeStateMachine", "Asynchronous send not completed in" + (DateTime.Now - m_lastSend).TotalSeconds + " seconds.  Reset socket"));                     
                        disconnect();
                    }
                    break;
                case WriteState.WAIT_ACK:
                    //check if we have pending acks to send first! (otherwise we might ack-deadlock)
                    if (m_ackWriteQueue.TryDequeue(out seqNum))
                    {
                        byte[] altBuffer;
                        altBuffer = Encoding.ASCII.GetBytes(buildAck(seqNum, "0000"));
                        m_writeState = WriteState.WAIT_ASYNCH_INTERRUPT;
                        if (m_sendBuffer != null && m_sendBuffer.Length > 0)
                        {
                            m_socWorker.BeginSend(altBuffer, 0, altBuffer.Length, 0, new AsyncCallback(SendCallback), m_socWorker);
                            m_lastSend = DateTime.Now; // set both here and in callback; do after so it doesn't have to be reset on exception
                            m_socketLog.log(LogTools.getCardString("ViaStdProto", "writeStateMachine", "Begin Send <" + Encoding.ASCII.GetString(m_sendBuffer) + ">"));
                        }
                    }
                    
                    if (m_lastSend + m_toacknak < DateTime.Now)
                    {
                        if (m_numResends > 10)
                        {
                            m_socketLog.log(LogTools.getErrorString("ViaStdProto", "writeStateMachine", "No Ack recieved after 10 retries, disconnect"));
                            disconnect();
                        }
                        else
                        {
                            ++m_numResends;
                            m_socketLog.log(LogTools.getErrorString("ViaStdProto", "writeStateMachine", "No ACK recieved after " + (DateTime.Now - m_lastSend).TotalSeconds + " seconds.  Resend telegram"));
                            m_writeState = WriteState.WAIT_ASYNCH_DATASEND; // do first to avoid race condition with callback
                            m_socWorker.BeginSend(m_sendBuffer, 0, m_sendBuffer.Length, 0, new AsyncCallback(SendCallback), m_socWorker);
                            m_lastSend = DateTime.Now; // set both here and in callback; do after so it doesn't have to be reset on exception
                            m_socketLog.log(LogTools.getCardString("ViaStdProto", "writeStateMachine", "Begin Send <" + Encoding.ASCII.GetString(m_sendBuffer)) + ">");
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void readStateMachine()
        {
            string telegram;
            if (m_readQ.TryDequeue(out telegram))
            {
                int loglinedisplay = Params.getParam("loglinedisplay", "viastdproto", 0);
                if (!m_readBuf.getData().Contains("line_display") || loglinedisplay == 1)
                {
                    m_socketLog.log(LogTools.getCardString("ViaStdProto", "readStateMachine", "Received: <" + telegram + ">"));
                }
                m_readBuf.Assign(telegram);
                
                if (m_readBuf.getLength() == 0) // bad telegram, disconnect
                {
                    m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead","Received telegram has invalid header or trailer (or no data) restarting the socket"));
                    disconnect();
                }

                // Data Telegram
                if (m_readBuf.isData())
                {
                    SeqNumStatus sNS = checkSeqErr(m_readBuf.getSeqNumber());
                    switch (sNS)
                    {
                        default:
                        case SeqNumStatus.INVALID_ORDER:
                            m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
                                "Sequence number received "
                                + m_readBuf.getSeqNumber() + ", but should have been " + incSeq(m_recSeqNum)));
                                //+ ". Sending ACKN and ingoring message."));
                            m_recQ.Enqueue(m_readBuf.getData());  // put it in the receive queue to be passed to the software.
                            m_recSeqNum = m_readBuf.getSeqNumber(); // accept whatever sequence number we just got...
                            break;
                        case SeqNumStatus.DUPLICATE:
                            m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
                                "Duplicate message received (sequence number: " + m_readBuf.getSeqNumber()
                                + "), send ACKN and ignored message."));
                            break;
                        case SeqNumStatus.OK:
                            m_recSeqNum = m_readBuf.getSeqNumber(); // accept whatever sequence number we just got...
                            m_recQ.Enqueue(m_readBuf.getData());  // put it in the receive queue to be passed to the software.
                            break;
                    }
                    
                    m_lastRecieve = DateTime.Now;
                    m_ackWriteQueue.Enqueue(m_readBuf.getSeqNumber());
                }
                // ACKN telegram
                else if (m_readBuf.isAck())
                {
                    //check if we sent a telegram which waits for an ACKN (or we were in that but got interrupted)
                    if ((m_writeState == WriteState.WAIT_ACK)||(m_writeState == WriteState.WAIT_ASYNCH_INTERRUPT))
                    {
                        // yes there is a telegram waiting for an ACKN
                        // compare the sequence numbers
                        ViaTelegram writeTele = new ViaTelegram();
                        writeTele.Assign(Encoding.ASCII.GetString(m_sendBuffer));
                        if (m_readBuf.getSeqNumber() == writeTele.getSeqNumber())
                        {
                            m_sendSeqNum = incSeq(writeTele.getSeqNumber());
                            m_writeState = WriteState.READY;
                        }
                        else
                        {
                            m_writeState = WriteState.READY;
                            m_socketLog.log(LogTools.getErrorString("viaStdProto", "internalRead",
                                "Received ack telegram has wrong sequence number, accept anyway. Expected sequence number: " + writeTele.getSeqNumber() + ", got: " + m_readBuf.getSeqNumber()));
                        }
                    }
                    else
                    {
                        m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
                            "Received an ACKN telegram without having sent a telegram that's waiting for an ACK"));
                    }
                    m_lastRecieve = DateTime.Now;
                }
                else if (m_readBuf.isKeepAlive())
                {
                    m_lastRecieve = DateTime.Now;
                }
                else if (m_readBuf.isNak())
                {
                    disconnect();
                    m_socketLog.log(LogTools.getErrorString("viaStdProto", "readStateMachine", "NAK recieved, reset socket"));
                }
                else
                {
                    m_socketLog.log(LogTools.getErrorString("viaStdProto", "readStateMachine", "unknown message recieved, ignore"));
                }
                
            }

        }
        
        public void terminate()
        {
            m_exit = true;
        } 
        #endregion

        private void disconnect()
        {
            m_status = Status.NotConnected;
            m_writeState = WriteState.READY;
            //m_readState = ReadState.READY;
            m_recvAccumulator = "";
            try
            {
                m_socWorker.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("viaStdProto", "disconnect", e, "Try Shutdown"));
            }
            try
            {
                m_socWorker.Close();
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("viaStdProto", "disconnect", e, "Try Close"));
            }
            m_socketLog.log(LogTools.getStatusString("viaStdProto", "disconnect", "Disconnect from socket"));

        }



        public void sendMsg(string msg)
        {
            try
            {
                //checkLongDisconnect();  // this throws if disconnected for a long time
                m_dataWriteQueue.Enqueue(msg);
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "sendMsg", e));
                throw e;
            }
        }

        public bool isRecvReady()
        {
            try
            {
                string result;
                //bool test = (m_recQ.Count > 0); // DEBUG
                //int test2 = m_recQ.Count;
                return (m_recQ.Count > 0) && (m_recQ.TryPeek(out result));
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "isRecvReady", e, "Socket has been closed"));
            }
            return false;
        }


        public string recvMsg()
        {
            string result = "";
                       
            if (m_recQ.TryDequeue(out result))
            {
                return result;
            }
            else
            {
                throw new InvalidOperationException("m_recQ.TryDequeue failed");
            }          
        }

        // ----------------------------------------------------------------------------------------
        // used to return the status of the socket (if it's connected or not)
        // ----------------------------------------------------------------------------------------
        public Status isConnected()
        {
            return (m_status);
        }

        // Private Methods
        // ========================================================================================

        // ----------------------------------------------------------------------------------------
        // starts the socket server listening for clients.
        // ----------------------------------------------------------------------------------------
        private void listen()
        {
            try
            {
                if (m_socListener != null)
                {
                    m_status = Status.NotConnected;
                    m_socListener.Close(0);
                }

                //create the listening socket...
                m_socListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, m_port);
                //bind to local IP Address...
                m_socListener.Bind(ipLocal);
                //start listening...
                m_socListener.Listen(1);
                // create the call back for any client connections...
                m_status = Status.WaitingForConnection;
                m_socListener.BeginAccept(new AsyncCallback(onConnectToClient), null);
                m_socketLog.log(LogTools.getStatusString("ViaStdProto", "listen", "Start socket server listening: local IP = " + ipLocal));

            }
            catch (Exception se)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "listen",  se, "Error listening for clients."));
                // try to close anything that might be open
                m_socListener.Close();
                disconnect();
                
            }

        }

        private void onConnectToClient(IAsyncResult ar)
        {
            try
            {
                // Get the socket that handles the client request.
                m_socWorker = m_socListener.EndAccept(ar);
                m_recSeqNum = 0;
                m_sendSeqNum = 1;
                m_status = Status.Connected;
                m_lastSend = DateTime.Now;
                m_lastRecieve = DateTime.Now;
                WaitForData(m_socWorker);
                m_socketLog.log(LogTools.getStatusString("ViaStdProto", "onConnectToClient",
                    "Retrieving socket for client request: " + DateTime.Now.ToString()));
            }
            catch (ObjectDisposedException ode)
            {
                System.Diagnostics.Debugger.Log(0, "1", "OnDataReceived: Socket has been closed");

                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onConnectToClient", ode, "Socket has been closed"));
                disconnect();
            }
            catch (SocketException se)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onConnectToClient", se));
                disconnect();
            }
            catch (Exception ex)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onConnectToClient", ex));
                disconnect();
            }
        }

        // ----------------------------------------------------------------------------------------
        // connect to server
        // ----------------------------------------------------------------------------------------
        private void connect()
        {
            // Connect to a remote device.
            try
            {
                // Establish the remote endpoint for the socket.
                IPAddress ipAddress = m_addr;
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, m_port);

                // Create a TCP/IP socket.
                m_socWorker = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // Connect to the remote endpoint.
                m_status = Status.WaitingForConnection;
                m_socWorker.BeginConnect(remoteEP, new AsyncCallback(onConnectToServer), m_socWorker);
                m_socketLog.log(LogTools.getStatusString("ViaStdProto", "connect", "Connecting to server: Remote endpoint = " + remoteEP.ToString())); 
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "connect", e, "Could not connect to server"));
                disconnect();
            }
        }

        // ----------------------------------------------------------------------------------------
        // connect to server callback
        // ----------------------------------------------------------------------------------------
        private void onConnectToServer(IAsyncResult ar)
        {
            try
            {
                // Complete the connection.
                m_socWorker.EndConnect(ar);
                m_recSeqNum = 0;
                m_sendSeqNum = 1;
                m_status = Status.Connected;

                m_socketLog.log(LogTools.getStatusString("ViaStdProto", "onConnectToServer",
                    "Socket connected to " + m_socWorker.RemoteEndPoint.ToString()));

                m_lastSend = DateTime.Now;
                m_lastRecieve = DateTime.Now;

                // Signal that the connection has been made.
                WaitForData(m_socWorker);
            }
            catch (ObjectDisposedException ode)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onConnectToServer", ode, "Problem connecting to server: Socket has been closed"));
                System.Diagnostics.Debugger.Log(0, "1", "onConnectToServer: Socket has been closed");

                disconnect();
                //throw (ode);

            }
            catch (SocketException se)
            {

                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onConnectToServer", se, "Problem connecting to server"));
                disconnect();
                //DEBUG::Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "\r\n" + "Error Desc: {0}, Error #: {1}", se.ToString(), se.ErrorCode);
                //todo: rethrow? retry?
                //throw (se);
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onConnectToServer", e, "Problem connecting to server"));
                disconnect();
                //DEBUG::Console.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "\r\n" + "Error Desc: {0}, Error #: {1}", se.ToString(), se.ErrorCode);
                //todo: rethrow? retry?
                //throw (se);
            }
        }

        public void WaitForData(System.Net.Sockets.Socket soc)
        {
            try
            {
                if (pfnWorkerCallBack == null)
                {
                    pfnWorkerCallBack = new AsyncCallback(onDataReceived);
                }
                CSocketPacket theSocPkt = new CSocketPacket();
                theSocPkt.workSocket = soc;
                // now start to listen for any data...
                soc.BeginReceive(theSocPkt.buffer, 0, CSocketPacket.BufferSize, SocketFlags.None, pfnWorkerCallBack, theSocPkt);
            }
            catch (ObjectDisposedException ode)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "WaitForData", ode, "Could not begin receiving on socket: Socket has been closed")); 

                m_status = Status.NotConnected;
                soc.Close();
            }
            catch (SocketException se)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "WaitForData", se, "Socket has been closed")); 
                m_status = Status.NotConnected;
                soc.Close();
                throw (se);
            }
            // TODO, can throw ArgumentOutOfRangeException also

        }

        private void onDataReceived(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                CSocketPacket theSockPkt = (CSocketPacket)ar.AsyncState;

                // Read data from the client socket. 
                int bytesRead = theSockPkt.workSocket.EndReceive(ar);

                if (bytesRead > 0)
                {
                    m_recvAccumulator = m_recvAccumulator + Encoding.ASCII.GetString(theSockPkt.buffer, 0, bytesRead);

                    if (m_recvAccumulator.Length > 24) // minimum length telegram (header + 0 lenth telegram + footer), don't bother doing further checks if we don't have at least this much
                    {
                        int length;
                        if (Int32.TryParse(m_recvAccumulator.Substring(0, 4), out length))
                        {
                            // have a valid length, see if we have at least that many characters
                            if (m_recvAccumulator.Length >= length)
                            {
                                string telegram = m_recvAccumulator.Substring(0, length);
                                if (telegram.Substring(length - 4, 4) == "ETX_") // good message
                                {
                                    m_recvAccumulator = m_recvAccumulator.Remove(0, length);
                                    m_readQ.Enqueue(telegram);
                                }
                                else
                                {
                                    m_socketLog.log(LogTools.getErrorString("ViaStdProto", "onDataReceived", "<ETX_> not found, shutting down socket. Data: <" + telegram + ">"));
                                    disconnect();
                                    return;
                                }
                            }
                        }
                    }
                }
                WaitForData(m_socWorker);
            }

            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "onDataReceived", e));
                disconnect();
            }
        }

        //private void Send(string data)
        //{
        //    try
        //    {
        //        // Convert the string data to byte data using ASCII encoding.
        //        byte[] byteData = Encoding.ASCII.GetBytes(data);

        //        // Begin sending the data to the remote device.
        //        //m_socketLog.log(LogTools.getDebugString("ViaStdProto", "Send","Begin Send:" + data));

        //        if (data.Substring(4, 4) == "ACKN")
        //        {
        //            //m_socketLog.log(LogTools.getTelegramString("ACK", data, m_sendQ.Count));
        //        }
        //        else
        //        {
        //            m_socketLog.log(LogTools.getTelegramString("Send", data, m_sendQ.Count));
        //        }
        //        m_socWorker.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), m_socWorker);
        //    }
        //    catch (ObjectDisposedException ode)
        //    {
        //        System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
        //        m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "Send", ode, "Socket has been closed"));

        //        m_status = Status.NotConnected;
        //        m_socWorker.Close();
        //        //todo: rethrow? retry?
        //        //throw (ode);
        //    }
        //    catch (SocketException se)
        //    {
        //        m_status = Status.NotConnected;
        //        m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "Send", se, "Socket has been closed"));
        //        m_socWorker.Close();
        //        //todo: rethrow? retry?
        //        //throw (se);
        //    }
        //}

        //private void syncSend(string data)
        //{
        //    try
        //    {
        //        // Convert the string data to byte data using ASCII encoding.
        //        byte[] byteData = Encoding.ASCII.GetBytes(data);

        //        // Begin sending the data to the remote device.
        //        //m_socketLog.log(LogTools.getDebugString("ViaStdProto", "Send","Begin Send:" + data));

        //        if (data.Substring(4, 4) == "ACKN")
        //        {
        //            //m_socketLog.log(LogTools.getTelegramString("ACK", data, m_sendQ.Count));
        //        }
        //        else
        //        {
        //            m_socketLog.log(LogTools.getTelegramString("Send", data, m_sendQ.Count));
        //        }
        //        //m_socWorker.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), m_socWorker);
        //        m_socWorker.Send(byteData);
        //    }
        //    catch (ObjectDisposedException ode)
        //    {
        //        System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
        //        m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "Send", ode, "Socket has been closed"));

        //        disconnect();
        //        //todo: rethrow? retry?
        //        //throw (ode);
        //    }
        //    catch (SocketException se)
        //    {
        //        disconnect();
        //        m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "Send", se, "Socket has been closed"));
                
        //        //todo: rethrow? retry?
        //        //throw (se);
        //    }
        //}

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                //m_socketLog.log(LogTools.getString("ViaStdProto", "SendCallback", "Sent " + bytesSent + " bytes to client."));
                
                m_lastSend = DateTime.Now;
                if (m_writeState == WriteState.WAIT_ASYNCH_DATASEND)
                {
                    m_writeState = WriteState.WAIT_ACK;
                }
                else if (m_writeState == WriteState.WAIT_ASYNCH_ACKSEND)
                {
                    m_writeState = WriteState.READY;
                }
                else if (m_writeState == WriteState.WAIT_ASYNCH_INTERRUPT)
                {
                    m_writeState = WriteState.WAIT_ACK;
                }
            }
            catch (ObjectDisposedException ode)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "SendCallback", ode, "Socket has been closed"));

                disconnect();
                //todo: rethrow? retry?
                //throw (ode);
            }
            catch (SocketException se)
            {
                disconnect();
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "SendCallback", se, "Socket has been closed"));
                
                //todo: rethrow? retry?
                //throw (se);
            }
        }

        //private void internalRead()
        //{
        //    try
        //    {
        //        if (m_ReadStat == ReadCycleStat.EMPTY || m_ReadStat == ReadCycleStat.READ)
        //        {
        //            string result;
        //            if (m_readQ.TryDequeue(out result))
        //            {
        //                m_ReadStat = ReadCycleStat.READY;
        //                m_readBuf.Assign(result);
        //            }
        //        }
        //        if (m_ReadStat == ReadCycleStat.READY)
        //        {
        //            // check for error in telegram header or trailer
        //            if (m_readBuf.getLength() == 0)
        //            {
        //                // close the connection
        //                m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
        //                    "Received telegram has " 
        //                    + "invalid header or trailer (or no data) restarting the socket"));
        //                disconnect();
        //            }

        //            // handle Keepalive Telegram
        //            else if (m_readBuf.isKeepAlive())
        //            {
        //                if (m_logSwitch)
        //                {
        //                    m_socketLog.log(LogTools.getStatusString("ViaStdProto", "internalRead",
        //                        "Keepalive telegram received"));
        //                }
        //                m_ReadStat = ReadCycleStat.SEND_ACK;
        //            }

        //            // handle DATA Telegram
        //            else if (m_readBuf.isData())
        //            {
        //                SeqNumStatus sNS = checkSeqErr(m_readBuf.getSeqNumber());
        //                switch (sNS)
        //                {
        //                    default:
        //                    case SeqNumStatus.INVALID_ORDER:
        //                        m_ReadStat = ReadCycleStat.SEND_ACK;

        //                        m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
        //                            "Sequence number received "
        //                            + m_readBuf.getSeqNumber() + ", but should have been " + incSeq(m_recSeqNum)
        //                            + ". Sending ACKN and ingoring message."));

        //                        break;
        //                    case SeqNumStatus.DUPLICATE:
        //                        m_ReadStat = ReadCycleStat.SEND_ACK;

        //                        m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
        //                            "Duplicate message received (sequence number: " + m_readBuf.getSeqNumber() 
        //                            + "), sent ACKN and ignored message."));
        //                        break;
        //                    case SeqNumStatus.OK:
        //                        m_ReadStat = ReadCycleStat.SEND_ACK;
        //                        m_recQ.Enqueue(m_readBuf.getData());  // put it in the receive queue to be passed to the software.
        //                        break;
        //                }
        //            }

        //            // handle Acknowledge telegram
        //            else if (m_readBuf.isAck())
        //            {
        //                //check if we sent a telegram which waits for an ACKN
        //                if (m_WriteStat == WriteCycleStat.WAIT_ACK)
        //                {
        //                    // yes there is a telegram waiting for an ACKN
        //                    // compare the sequence numbers
        //                    if (m_writeBuf.getSeqNumber() == m_readBuf.getSeqNumber())
        //                    {
        //                        m_sendSeqNum = incSeq(m_writeBuf.getSeqNumber());
        //                        m_WriteStat = WriteCycleStat.READY;
        //                    }
        //                    else
        //                    {
        //                      //  Console.WriteLine("Error: Received ack telegram has wrong sequence number.");
        //                      //  Console.WriteLine("  expected sequence number: {0}, got: {1}", m_writeBuf.getSeqNumber(), m_readBuf.getSeqNumber());
        //                        m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
        //                            "Received ack telegram has wrong sequence number. " +
        //                            "Expected sequence number: " + m_writeBuf.getSeqNumber() +
        //                            ", got: " + m_readBuf.getSeqNumber()));
                                    
        //                    }
        //                }
        //                else
        //                {

        //                    m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalRead",
        //                        "Received an ACKN telegram"
        //                        + " without having sent a telegram that's waiting for an ACK"));
        //                }
        //                m_ReadStat = ReadCycleStat.EMPTY;
        //            }
        //            m_KeepaliveSendStartTime = DateTime.Now;
        //            m_KeepaliveRecvStartTime = DateTime.Now;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        string errTxt = "Exceptionpathlogging: " + ShowDebugInfo() + " ";
        //        m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "internalRead", e));
        //        //Console.WriteLine(errTxt);
        //        throw (e);
        //    }
        //}

        //private void internalWrite()
        //{
        //    try
        //    {
        //        // check if we have to send an ACKN telegram
        //        if (m_ReadStat == ReadCycleStat.SEND_ACK)
        //        {
        //            m_KeepaliveSendStartTime = DateTime.Now;

        //            // update the recvSeq number
        //            m_recSeqNum = m_readBuf.getSeqNumber();

        //            // send an ACKN telegram
        //            Send(buildAck(m_readBuf.getSeqNumber(), "0000"));
        //            m_ReadStat = ReadCycleStat.EMPTY;
        //        }

        //        // get data telegram ready for send
        //        if (m_WriteStat == WriteCycleStat.EMPTY || m_WriteStat == WriteCycleStat.READY)
        //        {
        //            string result;
        //            if (m_sendQ.TryDequeue(out result))
        //            {
        //                m_WriteStat = WriteCycleStat.SEND;
        //                m_writeBuf.Assign(buildData(m_sendSeqNum, result));
        //            }
        //        }

        //        // send data telegram
        //        if (m_WriteStat == WriteCycleStat.SEND)
        //        {
        //            m_KeepaliveSendStartTime = DateTime.Now;
        //            string outMsg = buildData(m_writeBuf.getSeqNumber(), m_writeBuf.getData());
        //            Send(outMsg);
        //            if (m_logSwitch)
        //            {
        //                m_socketLog.log(LogTools.getStatusString("ViaStdProto", "internalWrite",
        //                    "Sent: " + outMsg + ", Bytes: " + m_writeBuf.getLength()));
        //            }
        //            m_WriteStat = WriteCycleStat.WAIT_ACK;
        //            m_WaitAckStartTime = DateTime.Now;
        //        }

        //        // Wait for an ACKN telegram.  ACKN telegrams are handled in the internalRead() function,
        //        // but here we check the timeout conditions and resend the telegram if necessary
        //        if (m_WriteStat == WriteCycleStat.WAIT_ACK)
        //        {
        //            m_KeepaliveSendStartTime = DateTime.Now;
        //            TimeSpan ts = DateTime.Now - m_WaitAckStartTime;

        //            if (ts > m_toacknak)
        //            {
        //                m_socketLog.log(LogTools.getErrorString("ViaStdProto", "internalWrite",
        //                    "waiting for Ack timed out = repeating same telegram."));
        //                m_WriteStat = WriteCycleStat.SEND;
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        string errTxt = "Exceptionpathlogging: " + ShowDebugInfo();
        //        m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "internalWrite", e, errTxt));
        //        throw (e);
        //    }
        //}

        // add the header and tail to the DATA telegram
        private string buildData(int messageNumber, string data)
        {
            try
            {
                data = "DATA" + m_srcid + m_destid + messageNumber.ToString("D2") + "__" + data + "ETX_";
                int length = data.Length + 4;
                data = length.ToString("D4") + data;
                return data;
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "buildData", e));
                throw (e);
            }
        }

        // add the header and tail to the ACKN telegram
        private string buildAck(int messageNumber, string data)
        {
            try
            {

                data = "ACKN" + m_srcid + m_destid + messageNumber.ToString("D2") + "__" + data + "ETX_";
                int length = data.Length + 4;
                data = length.ToString("D4") + data;
                return data;
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "buildAck", e));
                throw (e);
            }
        }

        // add the header and tail to the NAKN telegram
        private string buildNak(int messageNumber, string data)
        {
            try
            {

                data = "NAKN" + m_srcid + m_destid + messageNumber.ToString("D2") + "__" + data + "ETX_";
                int length = data.Length + 4;
                data = length.ToString("D4") + data;
                return data;
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "buildNak", e));
                throw (e);
            }
        }

        // check that the sequence number for the incoming message is what we expect it to be (the next one in the sequence)
        // this only applies to the m_recSeqNum
        private SeqNumStatus checkSeqErr(int curSeqNum)
        {
            try
            {
                if (curSeqNum == 0)
                    return SeqNumStatus.OK;

                if (curSeqNum == m_recSeqNum)
                    return SeqNumStatus.DUPLICATE;

                if (curSeqNum != incSeq(m_recSeqNum))
                    return SeqNumStatus.INVALID_ORDER;
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "checkSeqErr", e));
                throw (e);
            }

            return SeqNumStatus.OK;
        }

        //increment the sequence number
        private int incSeq(int seqNum)
        {
            try
            {
                seqNum++;
                if (seqNum > 99)
                    seqNum = 1;
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "incSeq", e));
                throw (e);
            }
            return (seqNum);
        }

        // ----------------------------------------------------------------------------------------
        // checkKeepAlive
        //  checks if it has been too long since we last recieved a keep alive.  Sending keepalive
        //      messages is done in writeStateMachine, not here.
        // ----------------------------------------------------------------------------------------
        private void checkKeepAlive()
        {
            try
            {

                if ((m_toKeepaliveRecv > TimeSpan.Zero) // if it equals zero, it means that we're not using keep alive's
                   && (m_lastRecieve + m_toKeepaliveRecv < DateTime.Now))
                {
                    // when a timeout is configured and no telegram received for configured time period
                    // assume connection lost -> close socket
                    m_socketLog.log(LogTools.getStatusString("ViaStdProto", "checkKeepAlive",
                        "No telegram received for " + (DateTime.Now - m_lastRecieve).TotalSeconds
                        + " sec.  Closing socket"));
                    disconnect();
                    return;
                }
            }
            catch (Exception e)
            {
                m_socketLog.log(LogTools.getExceptionString("ViaStdProto", "checkKeepAlive",  e));
                throw (e);
            }
        }

        // for profiler use only
        private int count(int c)
        {
            int a = 0;
            a = c + 1;
            return a;

        }

        // ------------------------------------------------------------------
        // if disconnected for a long time Throws! and empties the send queue
        // ------------------------------------------------------------------
        private void checkLongDisconnect()
        {
            TimeSpan aLongTime = new TimeSpan(0, 0, 30);
            if (m_lastConnected + aLongTime < DateTime.Now)
            {
                while (!m_dataWriteQueue.IsEmpty)
                {
                    string temp;
                    m_dataWriteQueue.TryDequeue(out temp);
                }
                while (!m_ackWriteQueue.IsEmpty)
                {
                    int temp;
                    m_ackWriteQueue.TryDequeue(out temp);
                }

                m_socketLog.log(LogTools.getErrorString("ViaStdProto", "checkLongDisconnect", "Disconnected for a long time, empty send queue and throw")); ;
                throw new LongDisconnectException();
            }
        }

    }

    public static class IPTools
    {
        /// <summary>
        /// Retrieves the translated ip address, or the ipv4 of localhost
        /// </summary>
        /// <param name="domainName">the domain or ip address</param>
        /// <returns>the ip address or non v4 of local address (NOT ::!)</returns>
        public static IPAddress decipherIpAddress(string domainName)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(domainName);
            for (int i = 0; i < addresses.Length; i++)
            {
                if (!addresses[i].ToString().Equals("::1"))
                    return addresses[i];
            }
            return null;
        }
    }

    public class LongDisconnectException : System.Exception
    {

    }
}
