using CSChaCha20;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace N_m3u8DL_CLI
{
    class Decrypter
    {
        public static byte[] AES128Decrypt(string filePath, byte[] keyByte, byte[] ivByte, CipherMode mode = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            //获取文件大小
            long size = fs.Length;
            byte[] inBuff = new byte[size];
            fs.Read(inBuff, 0, inBuff.Length);
            fs.Close();

            Aes dcpt = Aes.Create();
            dcpt.BlockSize = 128;
            dcpt.KeySize = 128;
            dcpt.Key = keyByte;
            dcpt.IV = ivByte;
            dcpt.Mode = mode;
            dcpt.Padding = padding;

            ICryptoTransform cTransform = dcpt.CreateDecryptor();
            Byte[] resultArray = cTransform.TransformFinalBlock(inBuff, 0, inBuff.Length);
            return resultArray;
        }

        public static byte[] AES128Decrypt(byte[] encryptedBuff, byte[] keyByte, byte[] ivByte, CipherMode mode = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            byte[] inBuff = encryptedBuff;

            Aes dcpt = Aes.Create();
            dcpt.BlockSize = 128;
            dcpt.KeySize = 128;
            dcpt.Key = keyByte;
            dcpt.IV = ivByte;
            dcpt.Mode = mode;
            dcpt.Padding = padding;

            ICryptoTransform cTransform = dcpt.CreateDecryptor();
            Byte[] resultArray = cTransform.TransformFinalBlock(inBuff, 0, inBuff.Length);
            return resultArray;
        }

        public static byte[] CHACHA20Decrypt(byte[] encryptedBuff, byte[] keyBytes, byte[] nonceBytes)
        {
            if (keyBytes.Length != 32)
                throw new Exception("Key must be 32 bytes!");
            if (nonceBytes.Length != 12 && nonceBytes.Length != 8)
                throw new Exception("Key must be 12 or 8 bytes!");
            if (nonceBytes.Length == 8)
                nonceBytes = (new byte[4] { 0, 0, 0, 0 }).Concat(nonceBytes).ToArray();

            var decStream = new MemoryStream();
            using (BinaryReader reader = new BinaryReader(new MemoryStream(encryptedBuff)))
            {
                using (BinaryWriter writer = new BinaryWriter(decStream))
                {
                    while (true)
                    {
                        var buffer = reader.ReadBytes(1024);
                        byte[] dec = new byte[buffer.Length];
                        if (buffer.Length > 0)
                        {
                            ChaCha20 forDecrypting = new ChaCha20(keyBytes, nonceBytes, 0);
                            forDecrypting.DecryptBytes(dec, buffer);
                            writer.Write(dec, 0, dec.Length);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return decStream.ToArray();
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
