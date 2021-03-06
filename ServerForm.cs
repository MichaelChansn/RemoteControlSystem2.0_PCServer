﻿using RemoteControlSystem2._0.CopyScreenAndBitmapTools;
using RemoteControlSystem2._0.ErrorMessage;
using RemoteControlSystem2._0.ScreenBitmap;
using RemoteControlSystem2._0.StreamLine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using RemoteControlSystem2._0.BitMapComparer;
using RemoteControlSystem2._0.DataPacket;
using RemoteControlSystem2._0.BitmapComparer;
using RemoteControlSystem2._0.BitmapTools;
using ICSharpCode.SharpZipLib.Zip;
using DesktopDuplication;
using RemoteControlSystem2._0.OSInfos;
using RemoteControlSystem2._0.CommandProcess;
using System.Drawing.Drawing2D;

namespace RemoteControlSystem2._0
{
    public partial class ServerForm : Form
    {
        private static int TCP_PORT = 8888;
        private static Socket serverSocket = null;
        private static Socket clientSocket = null;
        private static Thread serverSocketThread = null;//服务器等待连接线程
        private static Thread clientSocketHandlerThread = null;//客户端连接处理线程


        private static Thread copyScreenThread = null;//截屏线程
        private static Thread compressThread = null;//jpeg和Zip压缩线程
        private static Thread bitmapCmpThread = null;//图形差异比较线程
        private static Thread sendPacketThread = null;//数据发送线程
        private static Thread recPacketThread = null;//数据接收线程

        private static int UDP_PORT = 9999;
        private static Thread DUPScanThread = null;
        private static UdpClient udpClient = null;

        private static bool isServerRun = false;
        private static bool isClientRun = false;
        private static bool isSendPic = false;
        private static bool isWin8Above = false;
        private ManualResetEvent manulResetEvent = new ManualResetEvent(false);

        /**创建一个10帧大小的屏幕截图队列*/
        private BlockingQueue<BitmapWithCursor> screenCopyQueue = new BlockingQueue<BitmapWithCursor>(10);

        /**创建一个5帧大小的jpeg屏幕差异队列*/
        private BlockingQueue<DifferentBitmapWithCursor> screenCopyDifQueue = new BlockingQueue<DifferentBitmapWithCursor>(10);


        /**创建一个大小为10的发送队列*/
        private BlockingQueue<SendPacket> sendPacketQueue = new BlockingQueue<SendPacket>(10);

        /**创建一个大小为10的接受队列*/
        private BlockingQueue<RecPacket> recpacketQueue = new BlockingQueue<RecPacket>(10);

        /**全局比较图形*/
        private static Bitmap globalComparerBitmap = null;
        public ServerForm()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
            isWin8Above = OperatingSystemInfos.isWin8Above();
            Console.WriteLine("system is win8 above:" + isWin8Above);
        }

        private void stopUDP()
        {
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
            if (DUPScanThread != null)
            {
                DUPScanThread.Interrupt();
                DUPScanThread.Abort();
                DUPScanThread.Join();
            }

        }
        private void startUDPReceiverThread()
        {
            DUPScanThread = new Thread(udpReceive);
            DUPScanThread.IsBackground = true;
            DUPScanThread.Priority = ThreadPriority.Lowest;
            DUPScanThread.Start();
        }
        void udpReceive()
        {
            try
            {
                udpClient = new UdpClient(UDP_PORT);
            }
            catch (SocketException se)
            {
                MessageBox.Show("UDP Scan Thread Is Crashed\r\n" + se.Message, "UDP ERROR");
                return;
            }
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (isServerRun)
            {
                try
                {
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);//这个方法是阻塞的
                    string returnData = Encoding.UTF8.GetString(receiveBytes);
                    Console.WriteLine(returnData);
                    string[] rec = returnData.Split(ENUMS.NETSEPARATOR.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (rec[0] == ENUMS.UDPSCANMESSAGE && rec.Length == 2)
                    {
                        this.setMessageHost(rec[1] + " Scanning...");
                        Console.WriteLine(rec[1] + ":" + RemoteIpEndPoint.ToString());
                        byte[] buf = Encoding.UTF8.GetBytes(ENUMS.UDPSCANRETURN + ENUMS.NETSEPARATOR + System.Environment.UserName + ENUMS.NETSEPARATOR + TCP_PORT);
                        udpClient.Send(buf, buf.Length, RemoteIpEndPoint);

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    if (udpClient != null)
                    {
                        udpClient.Close();
                        udpClient = null;
                    }
                    return;
                }


            }
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }

        }
        private void buttonServer_Click(object sender, EventArgs e)
        {
            if (!isServerRun)
            {

                try
                {
                    isServerRun = true;
                    startUDPReceiverThread();
                    serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    serverSocket.Bind(new IPEndPoint(IPAddress.Any, TCP_PORT));
                    serverSocket.Blocking = true;
                    serverSocket.Listen(5);

                    serverSocketThread = new Thread(new ParameterizedThreadStart(serverSocketFun));
                    serverSocketThread.Priority = ThreadPriority.Lowest;
                    serverSocketThread.IsBackground = true;
                    serverSocketThread.Start(serverSocket);

                    textBoxHost.Text = "SERVER IS RUNNING...";
                    textBoxAddr.Text = "WAIT FOR CLIENT...";
                    buttonServer.Text = "STOP SERVER";

                    this.buttonServer.BackColor = System.Drawing.Color.Red;
                    this.buttonServer.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(158)))), ((int)(((byte)(146)))));
                    this.buttonServer.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(158)))), ((int)(((byte)(146)))));
                }
                catch (SocketException se)
                {
                    isServerRun = false;
                    MessageBox.Show("TCP Server Is Crashed\r\n" + se.Message, "ERROR!", MessageBoxButtons.OK);
                    ErrorInfo.getErrorWriter().writeErrorMassageToFile(se.Message);
                }
            }
            else
            {
                try
                {
                    isServerRun = false;
                    stopUDP();
                    stopAllThreads();
                    buttonServer.Text = "START SERVER";
                    textBoxHost.Text = "SERVER IS CLOSE...";
                    textBoxAddr.Text = "";

                    this.buttonServer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(158)))), ((int)(((byte)(146)))));
                    this.buttonServer.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Red;
                    this.buttonServer.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Red;

                }
                catch (SocketException se)
                {
                    isServerRun = true;
                    Console.WriteLine(se.Message);
                    ErrorInfo.getErrorWriter().writeErrorMassageToFile(se.Message);
                }

            }

        }

        public void setMessageHost(String message)
        {
            textBoxHost.Text = message;
        }
        public void setClientCloseMessage()
        {
            textBoxHost.Text = "Client Off Line";
            textBoxAddr.Text = "No Client On Line";
        }
        /**
         * 服务线程，用来接收链接
         */
        private void serverSocketFun(object serverSocket)
        {
            while (isServerRun)
            {
                try
                {
                    Socket client = ((Socket)serverSocket).Accept();
                    if (clientSocket != null)
                    {
                        sendMessage2Client("you are kicked off by other people!");
                        Thread.Sleep(100);//wait for last message
                        stopClient();
                        Thread.Sleep(100);//wait for clean socket

                    }
                    clientSocket = client;
                    textBoxHost.Text = "CLIENT IS CONNECTED!";
                    textBoxAddr.Text = clientSocket.RemoteEndPoint.ToString();
                    clientSocketHandlerThread = new Thread(new ParameterizedThreadStart(clientSocketHandlerFun));
                    clientSocketHandlerThread.Priority = ThreadPriority.Normal;
                    clientSocketHandlerThread.IsBackground = true;
                    clientSocketHandlerThread.Start(clientSocket);
                }
                catch (Exception ex)
                {

                    Console.WriteLine(ex.Message);
                    ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message + "\r\n" + ex.StackTrace);
                    break;

                }
            }
        }



        private void clientSocketHandlerFun(object clientSocket)
        {

            isClientRun = true;

            /**开启各种工作线程*/

            /*1*数据接收线程*/
            recPacketThread = new Thread(new ParameterizedThreadStart(recPacketFun));
            recPacketThread.Priority = ThreadPriority.Normal;
            recPacketThread.IsBackground = true;
            recPacketThread.Start(clientSocket);

            /*2*数据发送线程*/
            sendPacketThread = new Thread(new ParameterizedThreadStart(sendPacketFun));
            sendPacketThread.Priority = ThreadPriority.Normal;
            sendPacketThread.IsBackground = true;
            sendPacketThread.Start(clientSocket);

            startSendPicThreads();

        }


        /**线程接收函数*/
        private void recPacketFun(object clientSocket)
        {
            if (clientSocket == null) return;
            Socket client = (Socket)clientSocket;
            if (!client.Connected) return;
            NetworkStream stream = new NetworkStream(client);
            BinaryReader reader = new BinaryReader(stream);
            while (isClientRun)
            {
                //Console.WriteLine("recPacketThread***********");
                try
                {
                    ENUMS.MESSAGETYPE messageType = (ENUMS.MESSAGETYPE)reader.ReadByte();
                    CmdProcess.processCmd(this, reader, messageType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("readThread:client is off line");
                    stopClient();/**clean socket*/
                    Console.WriteLine(ex.Message);
                    ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message + "\r\n" + ex.StackTrace + "\r\n");
                }

            }

        }
        /**数据发送函数*/
        private void sendPacketFun(object clientSocket)
        {
            if (clientSocket == null) return;
            Socket client = (Socket)clientSocket;
            if (!client.Connected) return;
            NetworkStream stream = new NetworkStream(client);
            BinaryWriter writer = new BinaryWriter(stream);
            sendMessage2Client("hello,this is server");
            while (isClientRun)
            {
                //TODO
                SendPacket sendpacket = sendPacketQueue.Dequeue();
                if (sendpacket != null)
                {
                    // Console.WriteLine("sendPacketThread***********");
                    try
                    {
                        SendPacket.PacketType packetType = sendpacket.getPacketType();
                        switch (packetType)
                        {
                            case SendPacket.PacketType.BITMAP:
                                writer.Write((byte)packetType);
                                writer.Write((Int32)sendpacket.getbitmapBytesLength());
                                writer.Write((byte)sendpacket.getBitmapType());
                                writer.Write((Int16)sendpacket.getCursorPoint().getXPoint());
                                writer.Write((Int16)sendpacket.getCursorPoint().getYPoint());
                                List<ShortRec> difPointsList = sendpacket.getDifPointsList();
                                Int16 difNum = 0;
                                if (difPointsList != null)
                                {
                                    difNum = (Int16)difPointsList.Count;
                                }
                                writer.Write((Int16)difNum);

                                if (difNum > 0)
                                {
                                    List<ShortRec> difPoints = sendpacket.getDifPointsList();
                                    foreach (ShortRec dif in difPoints)
                                    {
                                        writer.Write(dif.xPoint);
                                        writer.Write(dif.yPoint);
                                        writer.Write(dif.width);
                                        writer.Write(dif.height);
                                    }
                                }
                                writer.Write(sendpacket.getBitByts(), 0, sendpacket.getbitmapBytesLength());
                                writer.Flush();
                                break;
                            case SendPacket.PacketType.TEXT:
                                writer.Write((byte)packetType);
                                byte[] sendBytes = Encoding.UTF8.GetBytes(sendpacket.getStringValue());
                                writer.Write(sendBytes.Length);
                                writer.Write(sendBytes);
                                writer.Flush();
                                break;
                            default:
                                break;
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("sendThread:client is off line");
                        stopClient();/**clean socket*/
                        Console.WriteLine(ex.Message);
                        ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message + "\r\n" + ex.StackTrace + "\r\n");
                    }
                }
            }


        }



        private bool picFlag = false;
        public void startSendPicFlags()
        {
            picFlag = true;
            manulResetEvent.Set();
        }
        public void stopSendPicFlags()
        {
            picFlag = false;
            manulResetEvent.Reset();
        }
        private void sendMessage2Client(String message)
        {
            SendPacket sendpacket = new SendPacket();
            sendpacket.setPacketType(SendPacket.PacketType.TEXT);
            sendpacket.setStringValue(message);
            sendPacketQueue.Enqueue(sendpacket);
            textBoxSDQ.Text = sendPacketQueue.getQueueSize() + "";

        }
        /**开始发送截图，流水线开始工作*/
        public void startSendPicThreads()
        {
            isSendPic = true;
            /*3*截屏线程*/
            copyScreenThread = new Thread(new ThreadStart(copyScreenToBlockingQueue));
            copyScreenThread.Priority = ThreadPriority.AboveNormal;
            copyScreenThread.IsBackground = true;
            copyScreenThread.Start();

            /*4*图像差异比较线程*/
            bitmapCmpThread = new Thread(new ThreadStart(bitmapCmpToBlockingQueue));
            bitmapCmpThread.Priority = ThreadPriority.AboveNormal;
            bitmapCmpThread.IsBackground = true;
            bitmapCmpThread.Start();

            /*5*压缩线程*/
            compressThread = new Thread(new ThreadStart(bitmapZipToBlockingQueue));
            compressThread.Priority = ThreadPriority.AboveNormal;
            compressThread.IsBackground = true;
            compressThread.Start();

        }
        /**停止发送截图，流水线停止工作*/
        public void stopSendPicThreads()
        {

            isSendPic = false;
            isFirstFrame = true;
            if (copyScreenThread != null && copyScreenThread.IsAlive)
            {
                copyScreenThread.Interrupt();
                copyScreenThread = null;
            }
            if (bitmapCmpThread != null && bitmapCmpThread.IsAlive)
            {
                bitmapCmpThread.Interrupt();
                bitmapCmpThread = null;
            }
            if (compressThread != null && compressThread.IsAlive)
            {
                compressThread.Interrupt();
                compressThread = null;
            }


        }

        /*
         * 根据屏幕变化的率的大小，动态的调整截屏间隔，优化流量和流畅度。
         * 最小50，最大950;
         */
        private static int dynamicTime = 90;
        private static int perWin8BelowFPSTime = 50;
        private static int perWin8AboveFPSTime = 25;
        private static bool isMove = false;
        private static int QTY = 100;
        
        private void copyScreenToBlockingQueue()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int fps = 0;//统计FPS
            bool nullFrame = true;

            while (isSendPic)
            {
                while (!picFlag)
                {
                    try
                    {
                        manulResetEvent.WaitOne();
                    }
                    catch (ThreadInterruptedException ex)
                    {
                        manulResetEvent.Reset();
                        Console.WriteLine(ex.Message);
                        ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message);
                        return;
                    }
                }
                while (picFlag)
                {

                    try
                    {
                        Thread.Sleep(dynamicTime);
                        // Console.WriteLine("screenCopyThread***********");
                        if (isWin8Above)//above win8 version
                        {
                            /**采用DXGI形式获取桌面，只能使用在win8以上系统，效率比较高，用来代替Mirror Driver*/
                            DesktopFrame frame = CopyScreen.getScreenPicDXGI();
                            if (frame != null)
                            {
                                if (nullFrame)
                                {
                                    //第一针总是黑屏，所以直接舍弃
                                    nullFrame = false;
                                    continue;
                                }
                                fps++;
                                BitmapWithCursor bitmapWithCursor = new BitmapWithCursor();
                                bitmapWithCursor.setCursorPoint(new ShortPoint(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y));
                                bitmapWithCursor.setScreenBitmap(frame.DesktopImage);
                                bitmapWithCursor.dirtyRecs = frame.UpdatedRegions;
                                screenCopyQueue.Enqueue(bitmapWithCursor);
                                textBoxCSQ.Text = "" + screenCopyQueue.getQueueSize();
                            }

                        }
                        else//below win8 
                        {
                            int cursorX, cursorY;
                            /* 采用的GDI形式获取桌面图形，效率比较低*/
                            Bitmap btm = CopyScreen.getScreenPic(out cursorX, out cursorY);
                            if (btm != null)
                            {
                                fps++;
                                BitmapWithCursor bitmapWithCursor = new BitmapWithCursor();
                                bitmapWithCursor.setCursorPoint(new ShortPoint(cursorX, cursorY));
                                bitmapWithCursor.setScreenBitmap(btm);
                                screenCopyQueue.Enqueue(bitmapWithCursor);
                                textBoxCSQ.Text = "" + screenCopyQueue.getQueueSize();
                            }
                        }

                        /**fps count*/
                        if (sw.ElapsedMilliseconds > 1000)
                        {
                            sw.Restart();
                            textBoxFPS.Text = "" +fps;
                            isMove = isMoving(fps);
                            fps = 0;
                        }
                        dynamicTime = timeSpan();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message);
                        return;
                    }
                }
            }
        }
        private int timeSpan()
        {
            int seed=Math.Max(Math.Max(screenCopyQueue.getQueueSize(),screenCopyDifQueue.getQueueSize()),sendPacketQueue.getQueueSize());
            int time=(int)(9.2*seed*seed+80);
            return time;
        }
        private static int bottomLine = 0;
        private static int maxValue = 10;
        private static int minValue = (int)(maxValue * 0.8);
        private static int countTime = 0;
       
        private  bool isMoving(int FPS)
        {
           /* Console.WriteLine(FPS);
            bool isMove = false;
            int qty = 100;
            if (FPS <= 5)
            {
                qty = 100;
            }
            else
            {
                qty = 550 / FPS - 10;
            }
            if (qty == 100)
            {
                if (bottomLine > 0)
                    bottomLine -= 1;
               
            }
            else
            {
                if (bottomLine < maxValue)
                    bottomLine += 1;
            }
            countTime++;
            if (countTime > maxValue)
            {
                countTime = maxValue+1;
                isMove = bottomLine > minValue ? true : false;
            }
            Console.WriteLine(bottomLine);
            Console.WriteLine(isMove);
            return isMove;*/
            
            if (FPS >= 10)
            {
                if (bottomLine < maxValue)
                    bottomLine += 1;
            }
            else
            {
                if (bottomLine > 0)
                    bottomLine -= 1;
            }
            Console.WriteLine(bottomLine);
            return bottomLine > minValue ? true : false;
            
        }
        private int getQty(int FPS)
        {
            int qty = 100;
            if (FPS <= 5) qty = 100;
            else
                qty = 550 / FPS - 10;
            return qty;
        }
        /**差异比较函数*/
        /**
         * 控制扫描块的大小，块越大，扫描速度越快，但是发送的数据量就越大;
         * 块越小，扫描速度就越慢，但是发送的数据量就小；
         * 局域网一般100*100
         * 广域网一般40*40 或 20*20
         * 是否需要协商块的大小？？？？进一步实验决定。默认的事16*8
         * 为了优化宽带占用，越小越好，因为后边会有矩形合并处理，不影响传输效率
         **/
        private static Size bitCmpSize = new Size(32, 32);
        private static bool isFirstFrame = true;//用于第一比较帧的保存
        private static int keyFrameAdjusttimes = 0;
        private static double VPT07 = 0.7;
        private static int globalBtmWidth = 1;
        private static int globalBtmHeight = 1;
        private void bitmapCmpToBlockingQueue()
        {
            while (isSendPic)
            {
                while (!picFlag)
                {
                    try
                    {
                        manulResetEvent.WaitOne();
                    }
                    catch (ThreadInterruptedException ex)
                    {
                        manulResetEvent.Reset();
                        Console.WriteLine(ex.Message);
                        ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message);
                        return;
                    }
                }
                while (picFlag)
                {
                    BitmapWithCursor bitmapWithCursor = screenCopyQueue.Dequeue();

                    if (bitmapWithCursor != null)
                    {
                        // Console.WriteLine("btmCmpThread***********");
                        try
                        {
                            //发送关键帧，校准
                            //keyFrameAdjusttimes++;
                            //if (keyFrameAdjusttimes > 300)
                            //{
                            //    keyFrameAdjusttimes = 0;
                            //    sendKeyFrame();
                            //}
                            Bitmap btm1 = bitmapWithCursor.getScreenBitmap();

                            if (isFirstFrame)
                            {
                                globalBtmWidth = btm1.Width;
                                globalBtmHeight = btm1.Height;
                                upDateKeyFrame(btm1, bitmapWithCursor.getCursorPoint());
                                isFirstFrame = false;
                            }
                            else
                            {
                                int lastBtmWidth = btm1.Width;
                                int lastBtmHeight = btm1.Height;
                                if (lastBtmWidth != globalBtmWidth || lastBtmHeight != globalBtmHeight)
                                {
                                    globalBtmWidth = lastBtmWidth;
                                    globalBtmHeight = lastBtmHeight;
                                    upDateKeyFrame(btm1, bitmapWithCursor.getCursorPoint());
                                    continue;
                                }
                                Bitmap btm2 = globalComparerBitmap;

                                List<ShortRec> difPoints = null;
                                if (isWin8Above)
                                {

                                    // Stopwatch sw = new Stopwatch();
                                    //sw.Start();
                                    difPoints = BitmapCmp32Bit.Compare(bitmapWithCursor.dirtyRecs, btm2, btm1, bitCmpSize);
                                    //sw.Stop();
                                  /* foreach (ShortRec rec in difPoints)
                                    {
                                        Console.WriteLine("dif---->" + rec.xPoint+":"+rec.yPoint+":"+rec.width+":"+rec.height);
                                    }*/
                                }
                                else
                                {
                                    difPoints = BitmapCmp24Bit.Compare(btm1, btm2, bitCmpSize);
                                }
                                Bitmap sendPic = null;
                                if (difPoints.Count > 0)
                                {
                                    DifferentBitmapWithCursor differentBitmapWithCursor = new ScreenBitmap.DifferentBitmapWithCursor();
                                    double VPTNOW = (double)(CopyScreen.getReslution().Width * CopyScreen.getReslution().Height) / (bitCmpSize.Width * bitCmpSize.Height);
                                    if ((double)difPoints.Count >= VPT07 * VPTNOW)//超过70%的改变，直接发送K帧
                                    {
                                        sendPic = btm1;
                                        differentBitmapWithCursor.setBitmapType(SendPacket.BitmapType.COMPLETE);
                                    }
                                    else
                                    {
                                        difPoints=unionShortRecs(difPoints);
                                        //Stopwatch sw = new Stopwatch();
                                        //sw.Start();
                                        sendPic = GetDifBlocks.getBlocksIn1BitmapClone(difPoints, btm1, bitCmpSize);
                                        //sw.Stop();
                                        //Console.WriteLine(sw.ElapsedMilliseconds+"ms");
                                        differentBitmapWithCursor.setBitmapType(SendPacket.BitmapType.BLOCK);
                                        differentBitmapWithCursor.setDifPointsList(difPoints);

                                    }
                                    /**更新全局比较帧*/
                                    globalComparerBitmap.Dispose();
                                    globalComparerBitmap = (Bitmap)btm1.Clone();
                                    differentBitmapWithCursor.setCursorPoint(bitmapWithCursor.getCursorPoint());
                                    differentBitmapWithCursor.setDifBitmap(sendPic);


                                    screenCopyDifQueue.Enqueue(differentBitmapWithCursor);
                                    textBoxDBQ.Text = "" + screenCopyDifQueue.getQueueSize();

                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message);
                            return;
                        }
                    }
                }

            }
        }

        /// <summary>
        /// union the dirty rects
        /// </summary>
        /// <param name="difPoints"></param>
        /// <returns></returns>
        private List<ShortRec> unionShortRecs(List<ShortRec> difPoints)
        {

            List<ShortRec> unionRects = new List<ShortRec>();
            if (difPoints.Count > 0)
            {
                Region region = new Region(new Rectangle(0, 0, 0, 0));
                foreach (ShortRec difPoint in difPoints)
                {
                    int startX = difPoint.xPoint;
                    int startY = difPoint.yPoint;
                    int width = difPoint.width;
                    int height = difPoint.height;
                    Rectangle rect = new Rectangle(startX, startY, width, height);
                    region.Union(rect);
                }
                RectangleF[] rectFs = region.GetRegionScans(new Matrix());
                foreach (RectangleF rF in rectFs)
                {
                    ShortRec shortRec = new ShortRec((int)rF.Left, (int)rF.Top, (int)rF.Width, (int)rF.Height);
                    unionRects.Add(shortRec);
                }

            }
            return unionRects;
        }

        /**更新关键帧*/
        private void upDateKeyFrame(Bitmap newKeyFrame, ShortPoint point)
        {
            globalComparerBitmap = (Bitmap)newKeyFrame.Clone();

            DifferentBitmapWithCursor differentBitmapWithCursor = new ScreenBitmap.DifferentBitmapWithCursor();
            differentBitmapWithCursor.setBitmapType(SendPacket.BitmapType.COMPLETE);
            differentBitmapWithCursor.setCursorPoint(point);
            differentBitmapWithCursor.setDifBitmap(newKeyFrame);
            screenCopyDifQueue.Enqueue(differentBitmapWithCursor);
            textBoxDBQ.Text = "" + screenCopyDifQueue.getQueueSize();
        }

        /**为了保证图像质量，可以每隔一段时间，发送一次关键帧进行图形矫正*/
        private void sendKeyFrame()
        {
            isFirstFrame = true;
        }



        /**压缩函数*/
        private void bitmapZipToBlockingQueue()
        {
            while (isSendPic)
            {
                try
                {
                    manulResetEvent.WaitOne();
                }
                catch (ThreadInterruptedException ex)
                {
                    manulResetEvent.Reset();
                    Console.WriteLine(ex.Message);
                    ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message);
                    return;
                }
                while (picFlag)
                {

                    DifferentBitmapWithCursor differentBitmapWithCursor = screenCopyDifQueue.Dequeue();
                    if (differentBitmapWithCursor != null)
                    {
                        // Console.WriteLine("compressThread***********");
                        try
                        {

                            SendPacket sendPacket = new SendPacket();
                            sendPacket.setPacketType(SendPacket.PacketType.BITMAP);
                            sendPacket.setBitmapType(differentBitmapWithCursor.getBitmapType());
                            sendPacket.setCursorPoint(differentBitmapWithCursor.getCursorPoint());
                            sendPacket.setDifPointsList(differentBitmapWithCursor.getDifPointsList());
                            byte[] bmpBytes = JpegZip.jpegAndZip(differentBitmapWithCursor.getDifBitmap(), isMove);

                            sendPacket.setBitByts(bmpBytes);
                            sendPacket.setBitmapBytesLength(bmpBytes.Length);
                            sendPacketQueue.Enqueue(sendPacket);
                            textBoxSDQ.Text = sendPacketQueue.getQueueSize() + "";
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            ErrorInfo.getErrorWriter().writeErrorMassageToFile(ex.Message);
                            return;
                        }
                    }
                }

            }
        }


        public void stopClient()
        {
            Console.WriteLine("client is close...");
            isClientRun = false;
            isSendPic = false;
            picFlag = false;
            isFirstFrame = true;
            stopSendPicThreads();
            if (clientSocket != null)
                clientSocket.Close();
            clientSocket = null;
            setClientCloseMessage();
            stopSendPicFlags();
            if (sendPacketThread != null && sendPacketThread.IsAlive)
            {
                sendPacketThread.Interrupt();
                sendPacketThread = null;
            }
            if (recPacketThread != null && recPacketThread.IsAlive)
            {
                recPacketThread.Interrupt();
                recPacketThread = null;
            }
            if (clientSocketHandlerThread != null && clientSocketHandlerThread.IsAlive)
            {
                clientSocketHandlerThread.Interrupt();
                clientSocketHandlerThread = null;
            }
            /**重新创建队列*/
            screenCopyQueue = new BlockingQueue<BitmapWithCursor>(10);
            screenCopyDifQueue = new BlockingQueue<DifferentBitmapWithCursor>(10);
            sendPacketQueue = new BlockingQueue<SendPacket>(10);
            recpacketQueue = new BlockingQueue<RecPacket>(10);

        }

        /**stop all the threads when exit or close the main serverSocket*/
        private void stopAllThreads()
        {
            isServerRun = false;
            if (clientSocket != null)
            {
                stopClient();
            }
            if (serverSocket != null)
            {
                serverSocket.Close();
                serverSocket = null;
                isServerRun = false;
            }


            if (serverSocketThread != null && serverSocketThread.IsAlive)
            {
                serverSocketThread.Interrupt();
                serverSocketThread = null;
            }
        }



        private Point mousePoint;
        private int topA(Control cc)
        {
            if (cc == null || cc == this) return 0;
            if (cc.Parent == null || cc.Parent == this)
                return cc.Top;
            else
                return topA(cc.Parent) + cc.Top;
        }
        private int leftA(Control cc)
        {
            if (cc == null || cc == this) return 0;
            if (cc.Parent == null || cc.Parent == this)
                return cc.Left;
            else
                return leftA(cc.Parent) + cc.Left;
        }
        private void top_MouseDown(object sender, MouseEventArgs e)
        {
            Control cc = (Control)sender;
            if (e.Button == MouseButtons.Left)
            {
                mousePoint.X = e.X + leftA(cc);
                mousePoint.Y = e.Y + topA(cc);
            }
        }
        private void top_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Top = MousePosition.Y - mousePoint.Y;
                Left = MousePosition.X - mousePoint.X;
            }
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {

            // stopAllThreads();
            this.Controls.Clear();
            this.Close();
            stopAllThreads();
            Application.Exit();
        }

        private void buttonMin_Click(object sender, EventArgs e)
        {
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
        }

        private void timerGC_Tick(object sender, EventArgs e)
        {
            GC.Collect();
        }





    }


}
