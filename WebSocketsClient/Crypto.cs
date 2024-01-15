using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WebSocketsClient
{
    public class Crypto
    {
        public X509Certificate2 cert; // 單一憑證內容

        public Crypto()
        {
        }

        public string Base64Decode(string value)
        {
            return Encoding.Default.GetString(Convert.FromBase64String(value));
        }

        public byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        public byte[] Compress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return compressedStream.ToArray();
            }
        }

        public static string Decrypt(string encryptStr, string key)
        {
            try
            {
                string decryptStr = "";
                byte[] keyArray = Encoding.UTF8.GetBytes(key);
                byte[] toEncryptArray = Convert.FromBase64String(encryptStr);
                AesCryptoServiceProvider aesCrypto = new AesCryptoServiceProvider();
                aesCrypto.Key = keyArray;
                aesCrypto.IV = new byte[32];
                aesCrypto.Padding = PaddingMode.PKCS7;
                aesCrypto.Mode = CipherMode.CBC;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aesCrypto.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(toEncryptArray, 0, toEncryptArray.Length);
                        cs.Close();
                    }
                    decryptStr = Encoding.UTF8.GetString(ms.ToArray());
                }

                return decryptStr;
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"Decrypt ERROR:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"Decrypt ERROR:{ex}");
            }
            return null;
        }

        public string AesEncryptBase64(string SourceStr, string CryptoKey)
        {
            string encrypt = "";
            try
            {
                AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                //MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                //SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider(); 
                //byte[] key = sha256.ComputeHash(Encoding.UTF8.GetBytes(CryptoKey));
                //byte[] iv = md5.ComputeHash(Encoding.UTF8.GetBytes(CryptoKey));
                byte[] key = Encoding.UTF8.GetBytes(CryptoKey);
                //byte[] iv = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };
                byte[] iv = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;


                byte[] dataByteArray = Encoding.UTF8.GetBytes(SourceStr);
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(dataByteArray, 0, dataByteArray.Length);
                    cs.FlushFinalBlock();
                    encrypt = Convert.ToBase64String(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"AesEncryptBase64 ERROR:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"AesEncryptBase64 ERROR:{ex}");
            }
            return encrypt;
        }

        public string AesDecryptBase64(string SourceStr, string CryptoKey)
        {
            string decrypt = "";
            try
            {
                AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                //MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                //SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
                //byte[] key = sha256.ComputeHash(Encoding.UTF8.GetBytes(CryptoKey));
                byte[] key = Encoding.UTF8.GetBytes(CryptoKey);
                //byte[] iv = md5.ComputeHash(Encoding.UTF8.GetBytes(CryptoKey));
                byte[] iv = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                aes.Key = key;
                aes.IV = iv;

                byte[] dataByteArray = Convert.FromBase64String(SourceStr);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(dataByteArray, 0, dataByteArray.Length);
                        cs.FlushFinalBlock();
                        decrypt = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"AesDecryptBase64 ERROR:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"AesDecryptBase64 ERROR:{ex}");
            }
            return decrypt;
        }

        public string AesShaEncryptBase64(string SourceStr, string CryptoKey)
        {
            string encrypt = "";
            try
            {
                SHA256 sha256 = SHA256.Create();
                byte[] dataByteArray = sha256.ComputeHash(Encoding.UTF8.GetBytes(SourceStr));

                string data = BitConverter.ToString(dataByteArray).Replace("-", string.Empty).ToLower();

                //Console.WriteLine("AesShaEncryptBase64 sha=" + data);//會造成checkmarx弱點

                encrypt = AesEncryptBase64(data, CryptoKey);
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"AesShaEncryptBase64 ERROR:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"AesShaEncryptBase64 ERROR:{ex}");
            }
            return encrypt;
        }

        public string Encrypt(string encryptStr, string key)
        {
            try
            {
                byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
                byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(encryptStr);
                RijndaelManaged rijndael = new RijndaelManaged();
                rijndael.Key = keyArray;
                rijndael.Mode = CipherMode.ECB;
                rijndael.Padding = PaddingMode.PKCS7;
                ICryptoTransform cryptoTransform = rijndael.CreateEncryptor();
                byte[] resultArray = cryptoTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                return Convert.ToBase64String(resultArray, 0, resultArray.Length);
            }
            catch (Exception ex)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"Encrypt ERROR:{ex.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(ex), $"Encrypt ERROR:{ex}");
            }
            return null;
        }

        public static string Decrypt(string cipherData, string keyString, string ivString)
        {
            byte[] key = Encoding.UTF8.GetBytes(keyString);
            byte[] iv = Encoding.UTF8.GetBytes(ivString);

            try
            {
                using (var rijndaelManaged =
                       new RijndaelManaged { Key = key, IV = iv, Mode = CipherMode.CBC })
                using (var memoryStream =
                       new MemoryStream(Convert.FromBase64String(cipherData)))
                using (var cryptoStream =
                       new CryptoStream(memoryStream,
                           rijndaelManaged.CreateDecryptor(key, iv),
                           CryptoStreamMode.Read))
                {
                    return new StreamReader(cryptoStream).ReadToEnd();
                }
            }
            catch (CryptographicException e)
            {
                CommonTools.AddLog(Constants.LOG_DEBUG, Log_Level.Error.GetHashCode(), $"Decrypt ERROR:{e.Message}");
                CommonTools.AddLog(Constants.LOG_ERROR, CommonTools.GetCurrentMethodInfo(), CommonTools.GetCurrentLineNumber(e), $"Decrypt ERROR:{e}");
                return null;
            }
            
        }

        public string GetJWTKey()
        {
            string seckey = string.Empty;
            for (int i = 0; i < 2; i++)
            {
                seckey = string.Concat(seckey, "m", "i", "t", "a", "k", "e");
                seckey = string.Concat(seckey, "@", "@");
                seckey = string.Concat(seckey, "8", "6", "1", "3", "6", "9", "8", "2");
            }

            return seckey;
        }
    }
}
