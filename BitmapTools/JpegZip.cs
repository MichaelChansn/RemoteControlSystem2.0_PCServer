using ICSharpCode.SharpZipLib.Zip;
using RemoteControlSystem2._0.CopyScreenAndBitmapTools;
using Simplicit.Net.Lzo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RemoteControlSystem2._0.BitmapTools
{
    class JpegZip
    {

        private static LZOCompressor lzoCompress = new LZOCompressor();

       
        public static byte[] jpegAndZip(Bitmap btm, bool isMove)
        {
            MemoryStream msIzip = new MemoryStream();
            MemoryStream ms = new MemoryStream();
            byte[] retByte;
            //Console.WriteLine(isMove);
            if (isMove)
            {
                retByte = lzoCompress.Compress(Compress2JepgWithQty.compressPictureToJpegBytesWithQty(btm, 40));
            }
            else
            {
                btm.Save(ms, ImageFormat.Jpeg);
                ms.Close();
                retByte = lzoCompress.Compress(ms.ToArray());
            }

            /*zip compress ,it is cost too much cpu ,so we do not use this compress way.
            ZipOutputStream outZip = new ZipOutputStream(msIzip);
            outZip.SetLevel(9);
            outZip.PutNextEntry(new ZipEntry("KS"));
            byte[] temp=ms.ToArray();
            outZip.Write(temp, 0, temp.Length);
            outZip.CloseEntry();
            outZip.Close();
            msIzip.Close();
            return msIzip.ToArray();
             * */
            return retByte;

        }
    }
}
