using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    class Decrypter
    {
        public static byte[] AES128Decrypt(string filePath, byte[] keyByte, byte[] ivByte, CipherMode mode = CipherMode.CBC)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            //获取文件大小
            long size = fs.Length;
            byte[] inBuff = new byte[size];
            fs.Read(inBuff, 0, inBuff.Length);
            fs.Close();

            Aes dcpt = Aes.Create("AES");
            dcpt.BlockSize = 128;
            dcpt.KeySize = 128;
            dcpt.Key = keyByte;
            dcpt.IV = ivByte;
            dcpt.Mode = mode;
            dcpt.Padding = PaddingMode.PKCS7;

            ICryptoTransform cTransform = dcpt.CreateDecryptor();
            Byte[] resultArray = cTransform.TransformFinalBlock(inBuff, 0, inBuff.Length);
            return resultArray;
        }

        public static byte[] AES128Decrypt(byte[] encryptedBuff, byte[] keyByte, byte[] ivByte, CipherMode mode = CipherMode.CBC)
        {
            byte[] inBuff = encryptedBuff;

            Aes dcpt = Aes.Create("AES");
            dcpt.BlockSize = 128;
            dcpt.KeySize = 128;
            dcpt.Key = keyByte;
            dcpt.IV = ivByte;
            dcpt.Mode = mode;
            dcpt.Padding = PaddingMode.PKCS7;

            ICryptoTransform cTransform = dcpt.CreateDecryptor();
            Byte[] resultArray = cTransform.TransformFinalBlock(inBuff, 0, inBuff.Length);
            return resultArray;
        }

        public static byte[] HexStringToBytes(string hexStr)
        {
            if (string.IsNullOrEmpty(hexStr))
            {
                return new byte[0];
            }

            if (hexStr.StartsWith("0x") || hexStr.StartsWith("0X")) 
            {
                hexStr = hexStr.Remove(0, 2);
            }

            int count = hexStr.Length;

            if (count % 2 == 1)
            {
                throw new ArgumentException("Invalid length of bytes:" + count);
            }

            int byteCount = count / 2;
            byte[] result = new byte[byteCount];
            for (int ii = 0; ii < byteCount; ++ii)
            {
                var tempBytes = Byte.Parse(hexStr.Substring(2 * ii, 2), System.Globalization.NumberStyles.HexNumber);
                result[ii] = tempBytes;
            }

            return result;
        }
    }
}
