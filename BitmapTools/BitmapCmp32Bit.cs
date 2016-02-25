﻿using RemoteControlSystem2._0.ScreenBitmap;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RemoteControlSystem2._0.BitmapTools
{
    /**this class is used for compare two 32bppArgb format Bimaps, 
     * 24bpprgb is  not the same with 32bppArgb,
     * so we can not use the same function to compare them.*/
    class BitmapCmp32Bit
    {
        /// <summary>
        /// 图像颜色32位argb
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct ICColor
        {
            [FieldOffset(0)]
            public byte B;
            [FieldOffset(1)]
            public byte G;
            [FieldOffset(2)]
            public byte R;
            [FieldOffset(3)]
            public byte A;
        }
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int memcmp(byte* b1, byte* b2, int count);
        private static int BOTTOMLINE = 10;//颜色阀值，低于此值认为是相同的像素
        private static int SCANSTRIDE = 5;//隔行扫描，每隔5行/列，扫描一次

        /// <summary>
        /// 比较两个图像,固定块各行扫描
        /// </summary>
        /// <param name="bmp1"></param>
        /// <param name="bmp2"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        /// 
        public static List<ShortRec> Compare(Rectangle[] dirtyRecs, Bitmap globalBtm, Bitmap lastFrame, Size block)
        {
            // if (globalBtm.Size != lastFrame.Size) return null;
            List<ShortRec> difPoint = new List<ShortRec>();
            PixelFormat pf = PixelFormat.Format32bppArgb;
            Bitmap retBtm = new Bitmap(lastFrame.Width, lastFrame.Height, lastFrame.PixelFormat);
            Graphics g = Graphics.FromImage(retBtm);
            BitmapData bd1 = globalBtm.LockBits(new Rectangle(0, 0, globalBtm.Width, globalBtm.Height), ImageLockMode.ReadOnly, pf);
            BitmapData bd2 = lastFrame.LockBits(new Rectangle(0, 0, lastFrame.Width, lastFrame.Height), ImageLockMode.ReadOnly, pf);

            try
            {

                unsafe
                {
                    
                    foreach (Rectangle dirRec in dirtyRecs)
                    {
                        //新图形坐标
                        int startX = dirRec.Left;
                        int startY = dirRec.Top;
                        int width = dirRec.Width;
                        int height = dirRec.Height;
                        int endX = dirRec.Right;
                        int endY = dirRec.Bottom;
                        int w = startX, h = startY;
                        int start = 0;// new Random().Next(0, SCANSTRIDE);
                        while (h <= endY)
                        {
                            byte* p1 = (byte*)bd1.Scan0 + h * bd1.Stride;
                            byte* p2 = (byte*)bd2.Scan0 + h * bd2.Stride;
                             w = startX;
                             while (w <= endX)
                             {
                                 for (int j = start; j < block.Height; j += j%SCANSTRIDE+1)
                                 {
                                     int hj = h + j;
                                     if (hj >= endY) break;
                                     byte* pc1 = p1 + j * bd1.Stride + w * 4;
                                     byte* pc2 = p2 + j * bd1.Stride + w * 4;
                                     if (memcmp(pc1, pc2, Math.Min(block.Width, endX - w) * 4) != 0)
                                     {
                                         int bw = Math.Min(block.Width, endX - w);
                                         int bh = Math.Min(block.Height, endY - h);
                                         difPoint.Add(new ShortRec(w, h, bw, bh));
                                         break;
                                     }
                                 }
                                 w += block.Width;
                             }
                             h += block.Height;
 
                        }
                    }
                    /**2*2随机点阵探测*/
                    /*
                    foreach (Rectangle dirRec in dirtyRecs)
                    {
                        //新图形坐标
                        int startX = dirRec.Left;
                        int startY = dirRec.Top;
                        int endX = dirRec.Right;
                        int endY = dirRec.Bottom;

                        int w = startX, h = startY;
                        int start =  new Random().Next(0, 2);//确定随机监测点，保证随机探测

                        while (h < endY)
                        {
                            byte* p1 = (byte*)bd1.Scan0 + h * bd1.Stride;
                            byte* p2 = (byte*)bd2.Scan0 + h * bd2.Stride;

                            w = startX;
                            while (w < endX)
                            {
                               
                                //按块大小进行扫描
                                for (int j = start; j < block.Height; j += 2)
                                {
                                    int hj = h + j;
                                    if (hj >= endY) break;

                                    for (int i = start%2+1; i < block.Width; i +=2)
                                    {
                                        int wi = w + i;
                                        if (wi >= endX) break;


                                        ICColor* pc1 = (ICColor*)(p1 + wi * 4 + bd1.Stride * j);
                                        ICColor* pc2 = (ICColor*)(p2 + wi * 4 + bd2.Stride * j);

                                        //忽略A值
                                        if (Math.Abs(pc1->R - pc2->R) > BOTTOMLINE || Math.Abs(pc1->G - pc2->G) > BOTTOMLINE || Math.Abs(pc1->B - pc2->B) > BOTTOMLINE)
                                        {
                                            int bw = Math.Min(block.Width, endX - w);
                                            int bh = Math.Min(block.Height, endY);
                                            difPoint.Add(new ShortRec(w, h, bw, bh));
                                            //可以继续使用clone()
                                            //bmp1.Clone(new Rectangle(w, h, 19, 19), bmp1.PixelFormat).Save("D:\\test.jpeg", ImageFormat.Jpeg);
                                            goto E;
                                        }
                                    }
                                }
                            E:
                                w += block.Width;
                            }

                            h += block.Height;
                        }


                    }
                    */

                   

                }
            }
            finally
            {
                globalBtm.UnlockBits(bd1);
                lastFrame.UnlockBits(bd2);
            }

            return difPoint;
        }


        /// <summary>
        /// 比较两个图像,多级动态块扫描
        /// </summary>
        /// <param name="bmp1"></param>
        /// <param name="bmp2"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        /// 
        public static List<ShortRec> Compare2(Rectangle[] dirtyRecs, Bitmap globalBtm, Bitmap lastFrame, Size block)
        {
            // if (globalBtm.Size != lastFrame.Size) return null;
            List<ShortRec> difPoint = new List<ShortRec>();
            PixelFormat pf = PixelFormat.Format32bppArgb;
            Bitmap retBtm = new Bitmap(lastFrame.Width, lastFrame.Height, lastFrame.PixelFormat);
            Graphics g = Graphics.FromImage(retBtm);
            BitmapData bd1 = globalBtm.LockBits(new Rectangle(0, 0, globalBtm.Width, globalBtm.Height), ImageLockMode.ReadOnly, pf);
            BitmapData bd2 = lastFrame.LockBits(new Rectangle(0, 0, lastFrame.Width, lastFrame.Height), ImageLockMode.ReadOnly, pf);
            

            try
            {

                unsafe
                {
                   
                    /******************************************************************************************************/

                    foreach (Rectangle dirRec in dirtyRecs)
                    {
                        //新图形坐标
                        int startX = dirRec.Left;
                        int startY = dirRec.Top;
                        int endX = dirRec.Right;
                        int endY = dirRec.Bottom;

                        int w = startX, h = startY;
                        int start = 0;
                        bool isFirstDif = false;
                        bool isInit = false;
                        ShortRec tempShortRec ;
                        while (h <= endY)
                        {
                            byte* p1 = (byte*)bd1.Scan0 + h * bd1.Stride;
                            byte* p2 = (byte*)bd2.Scan0 + h * bd2.Stride;

                            w = startX;
                            while (w <= endX)
                            {
                                //按块大小进行扫描
                                tempShortRec = new ShortRec(0, 0, 0, 0);
                                bool difLine = false;
                                for (int j = start; j < block.Height; j += SCANSTRIDE)
                                {
                                    
                                    int hj = h + j;
                                    if (hj >= endY) break;

                                    difLine = false;
                                    for (int i = start; i < block.Width; i += 1)
                                    {
                                        int wi = w + i;
                                        if (wi >= endX) break;


                                        ICColor* pc1 = (ICColor*)(p1 + wi * 4 + bd1.Stride * j);
                                        ICColor* pc2 = (ICColor*)(p2 + wi * 4 + bd2.Stride * j);
                                        //忽略A值
                                        if (Math.Abs(pc1->R - pc2->R) > BOTTOMLINE || Math.Abs(pc1->G - pc2->G) > BOTTOMLINE || Math.Abs(pc1->B - pc2->B) > BOTTOMLINE)
                                        {
                                            /*int bw = Math.Min(block.Width, endX - w);
                                            int bh = Math.Min(block.Height, endY);
                                            difPoint.Add(new ShortRec(w, h, bw, bh));
                                            //可以继续使用clone()
                                            //bmp1.Clone(new Rectangle(w, h, 19, 19), bmp1.PixelFormat).Save("D:\\test.jpeg", ImageFormat.Jpeg);
                                           */
                                            difLine=true;
                                            if (!isFirstDif)
                                            {
                                                isFirstDif = true;
                                                isInit = true;
                                                tempShortRec.xPoint = (short)wi;
                                                tempShortRec.yPoint = (short)hj;
                                            }
                                            else
                                            {
                                                short rightOld=(short)(tempShortRec.xPoint+tempShortRec.width);
                                                short bottomOld=(short)(tempShortRec.yPoint+tempShortRec.height);
                                                tempShortRec.xPoint = Math.Min(tempShortRec.xPoint, (short)wi);
                                                //tempShortRec.yPoint = tempShortRec.yPoint;
                                                short rightNew=Math.Max(rightOld,(short)wi);
                                                short bottomNew=Math.Max(bottomOld,(short)hj);
                                                tempShortRec.width =(short)(rightNew-tempShortRec.xPoint);
                                                tempShortRec.height = (short)(bottomNew -tempShortRec.yPoint);
                                            }

                                        }
                                    }
                                    if (!difLine&&isInit)
                                    {
                                        if (!(tempShortRec.width == 0 && tempShortRec.height == 0))
                                        {
                                            difPoint.Add(tempShortRec);
                                            
                                        }
                                        isFirstDif = false;
                                        isInit = false;
                                        tempShortRec = new ShortRec(0, 0, 0, 0);
                                    }
                                    
                                    
                                }
                                if (difLine && isInit)
                                {
                                    if (!(tempShortRec.width == 0 && tempShortRec.height == 0))
                                    {
                                        difPoint.Add(tempShortRec);
                                    }
                                    isFirstDif = false;
                                    isInit = false;
                                }

                                w += block.Width-1;
                            }

                            h += block.Height-1;
                        }


                    }




                }
            }
            finally
            {
                globalBtm.UnlockBits(bd1);
                lastFrame.UnlockBits(bd2);
            }

            return difPoint;
        }
    }
}
