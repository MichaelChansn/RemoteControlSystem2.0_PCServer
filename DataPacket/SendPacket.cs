﻿using ControlServer1._0.ScreenBitmap;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace ControlServer1._0.DataPacket
{
    class SendPacket
    {
        /**消息结构
         *   
         *______________________________________________________________________________________________________________________________________________________________
         *   PTYPE   |     LEN    | BTYPE  |    C_POSITION_X   |     C_POSITION_Y    |        DIF_NUM          |      DIF_LIST          |         BITMAP_DATA         |
         *--------------------------------------------------------------------------------------------------------------------------------------------------------------
         *
         * PTYPE:byte型，表示是图像包还是文字包
         * LEN:4bytes,int型，表示BITMAP_DATA的大小，不包括消息头（LEN,TYPE,POSITION_X,POSITION_Y）
         * BTYPE:bitmap的类型，详见下列枚举
         * C_POSITION_X: 鼠标的坐标
         * D_POSITION_X: 差异图形开始坐标
         * DIF_NUM:各个图形块的起始坐标，用于恢复图形。
         * BITMAP_DATA:要发送的bitmap数据
         */
        /**枚举图像类型*/
        public  enum BitmapType : byte
        {
            BLOCK = (byte)0x00,//s帧，补帧，用于图形复原
            COMPLETE = (byte)0x01//k帧，用于直接显示

        }
        public  enum PacketType : byte
        {
            BITMAP=(byte)0x00,//包类型是图像
            TEXT=(Byte)0x01,//包类型是文字
        }

      
        /**要发送的图像的数据大小(不包括消息头)最大表示4G文件2^32*/
        private int bitmapBytesLength = 0;

        /**图像的类型
         * 0x00:普通图像差一块，用于拼接到主图
         * 0x01:key帧，一副完整的图像帧，直接显示，为了抵消解码错误引起的图像显示不正确，相当于H264内部的I帧
         *
         */
        private BitmapType bitmapType = BitmapType.BLOCK;
       
        /**鼠标的坐标*/
        private ShortPoint cursorPoint=null;
        /**差异图形的起点坐标*/
        private List<ShortRec> difPointsList=null;
        /**要发送的图形数据*/
        private byte[] bitmapBytes = null;


        /*非图像包的结构*/
        private PacketType packetType;
        private String stringValue;
        private int intValue1;
        private int intValue2;

        public void setPacketType(PacketType packetType)
        {
            this.packetType = packetType;
        }

        public PacketType getPacketType()
        {
            return this.packetType;
        }

        public void setStringValue(string stringValue)
        {
            this.stringValue = stringValue;
        }
        public String getStringValue()
        {
            return this.stringValue;
        }
        public void setIntValue1(int intValue1)
        {
            this.intValue1 = intValue1;
        }
        public int getIntValue1()
        {
            return this.intValue1;
        }
        public void setIntValue2(int intValue2)
        {
            this.intValue2 = intValue2;
        }
        public int getIntValue2()
        {
            return this.intValue2;
        }
        public void setBitByts(byte[] bytes)
        {
            this.bitmapBytes = bytes;
        }
        public byte[] getBitByts()
        {
            return this.bitmapBytes;
        }
        public void setBitmapBytesLength(int length)
        {
            this.bitmapBytesLength=length;
        }
        public int getbitmapBytesLength()
        {
            return this.bitmapBytesLength;
        }

        public void setBitmapType(BitmapType type)
        {
            this.bitmapType = type;
        }
        public BitmapType getBitmapType()
        {
            return this.bitmapType;
        }

        public ShortPoint getCursorPoint()
        {
            return this.cursorPoint;
        }
        public void setCursorPoint(ShortPoint cursorPoint)
        {
            this.cursorPoint = cursorPoint;
        }


        public List<ShortRec> getDifPointsList()
        {
            return this.difPointsList;
        }
        public void setDifPointsList(List<ShortRec> difPointsList)
        {
            this.difPointsList = difPointsList;
        }

      
        
    }
}
